using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models.Interfaces;

namespace Xemplo.ApiChecker.Core;

public sealed class ApiJsonSchemaAnalyzer : IApiJsonSchemaAnalyzer
{
    public ApiJsonSchemaAnalysis Analyze(IOpenApiSchema? schema, ApiSchemaContext context, string rootPath = "$")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        if (schema is null)
        {
            return ApiJsonSchemaAnalysis.Empty;
        }

        var nodes = new List<ApiJsonSchemaNode>();
        AnalyzeNode(
            schema,
            context,
            rootPath,
            propertyName: null,
            inheritedUsage: ApiSchemaUsage.Included,
            required: ApiSchemaCondition.NotApplicable,
            nodes);

        return new ApiJsonSchemaAnalysis(nodes.OrderBy(static node => node.SchemaPath, StringComparer.Ordinal).ToArray());
    }

    private static void AnalyzeNode(
        IOpenApiSchema schema,
        ApiSchemaContext context,
        string schemaPath,
        string? propertyName,
        ApiSchemaUsage inheritedUsage,
        ApiSchemaCondition required,
        ICollection<ApiJsonSchemaNode> nodes)
    {
        var ambiguityReasons = GetAmbiguityReasons(schema, inheritedUsage);
        var usage = DetermineUsage(schema, context, inheritedUsage);
        var nullable = DetermineNullable(schema);
        var hasDefault = DetermineHasDefault(schema);
        var kind = DetermineKind(schema);
        var enumValues = GetEnumValues(schema);

        nodes.Add(new ApiJsonSchemaNode(
            schemaPath,
            propertyName,
            kind,
            usage,
            required,
            nullable,
            hasDefault,
            enumValues,
            ambiguityReasons));

        if (HasOpaqueComposition(schema))
        {
            return;
        }

        var properties = GetProperties(schema);
        if (kind == ApiJsonSchemaNodeKind.Object && properties.Count > 0)
        {
            foreach (var property in properties)
            {
                if (property.Value is null)
                {
                    continue;
                }

                AnalyzeNode(
                    property.Value,
                    context,
                    $"{schemaPath}.{property.Key}",
                    property.Key,
                    usage,
                    DetermineRequired(schema, property.Key, properties),
                    nodes);
            }
        }

        var items = GetItems(schema);
        if (kind == ApiJsonSchemaNodeKind.Array && items is not null)
        {
            AnalyzeNode(
                items,
                context,
                $"{schemaPath}[]",
                propertyName: null,
                usage,
                ApiSchemaCondition.NotApplicable,
                nodes);
        }
    }

    private static ApiJsonSchemaNodeKind DetermineKind(IOpenApiSchema schema)
    {
        if (GetProperties(schema).Count > 0 || HasTypeToken(schema, "object"))
        {
            return ApiJsonSchemaNodeKind.Object;
        }

        if (GetItems(schema) is not null || HasTypeToken(schema, "array"))
        {
            return ApiJsonSchemaNodeKind.Array;
        }

        return ApiJsonSchemaNodeKind.Scalar;
    }

    private static ApiSchemaUsage DetermineUsage(IOpenApiSchema schema, ApiSchemaContext context, ApiSchemaUsage inheritedUsage)
    {
        if (inheritedUsage != ApiSchemaUsage.Included)
        {
            return inheritedUsage;
        }

        if (schema.ReadOnly && schema.WriteOnly)
        {
            return ApiSchemaUsage.Ambiguous;
        }

        if (HasOpaqueComposition(schema) && (schema.ReadOnly || schema.WriteOnly))
        {
            return ApiSchemaUsage.Ambiguous;
        }

        return context switch
        {
            ApiSchemaContext.Request when schema.ReadOnly => ApiSchemaUsage.Excluded,
            ApiSchemaContext.Response when schema.WriteOnly => ApiSchemaUsage.Excluded,
            _ => ApiSchemaUsage.Included
        };
    }

    private static ApiSchemaCondition DetermineRequired(
        IOpenApiSchema parentSchema,
        string propertyName,
        IReadOnlyDictionary<string, IOpenApiSchema?> resolvedProperties)
    {
        if (HasOpaqueComposition(parentSchema))
        {
            return ApiSchemaCondition.Ambiguous;
        }

        if (!resolvedProperties.ContainsKey(propertyName))
        {
            return ApiSchemaCondition.NotApplicable;
        }

        return EnumerateAllOfSchemas(parentSchema).Any(schema => schema.Required?.Contains(propertyName) == true)
            ? ApiSchemaCondition.True
            : ApiSchemaCondition.False;
    }

    private static ApiSchemaCondition DetermineNullable(IOpenApiSchema schema)
    {
        if (HasComposition(schema))
        {
            return ApiSchemaCondition.Ambiguous;
        }

        if (TryGetNullableFlag(schema, out var nullableFromFlag))
        {
            return nullableFromFlag ? ApiSchemaCondition.True : ApiSchemaCondition.False;
        }

        var typeText = schema.Type?.ToString();
        if (!string.IsNullOrWhiteSpace(typeText))
        {
            return typeText.Contains("null", StringComparison.OrdinalIgnoreCase)
                ? ApiSchemaCondition.True
                : ApiSchemaCondition.False;
        }

        if (schema.Properties is { Count: > 0 }
            || GetProperties(schema).Count > 0
            || GetItems(schema) is not null
            || schema.Enum is { Count: > 0 }
            || schema.Default is not null)
        {
            return ApiSchemaCondition.False;
        }

        return ApiSchemaCondition.Ambiguous;
    }

    private static ApiSchemaCondition DetermineHasDefault(IOpenApiSchema schema)
    {
        return schema.Default is not null
            ? ApiSchemaCondition.True
            : ApiSchemaCondition.False;
    }

    private static IReadOnlyList<string> GetEnumValues(IOpenApiSchema schema)
    {
        if (schema.Enum is not { Count: > 0 })
        {
            return Array.Empty<string>();
        }

        return schema.Enum.Cast<object>().Select(static value => SerializeAnyValue(value)).ToArray();
    }

    private static IReadOnlyList<string> GetAmbiguityReasons(IOpenApiSchema schema, ApiSchemaUsage inheritedUsage)
    {
        var reasons = new List<string>();

        if (inheritedUsage == ApiSchemaUsage.Ambiguous)
        {
            reasons.Add("Parent usage is ambiguous.");
        }

        if (HasOpaqueComposition(schema))
        {
            reasons.Add("Schema uses composition that prevents a proven property-level interpretation.");
        }
        else if (HasAllOfComposition(schema))
        {
            reasons.Add("Schema uses allOf composition; nullable and default interpretations stay conservative.");
        }

        if (schema.ReadOnly && schema.WriteOnly)
        {
            reasons.Add("Schema is both readOnly and writeOnly.");
        }

        if (!TryGetNullableFlag(schema, out _) && string.IsNullOrWhiteSpace(schema.Type?.ToString()))
        {
            reasons.Add("Schema nullability is not explicit.");
        }

        return reasons;
    }

    private static bool HasComposition(IOpenApiSchema schema)
    {
        return schema.AllOf is { Count: > 0 }
            || schema.AnyOf is { Count: > 0 }
            || schema.OneOf is { Count: > 0 };
    }

    private static bool HasOpaqueComposition(IOpenApiSchema schema)
    {
        return schema.AnyOf is { Count: > 0 }
            || schema.OneOf is { Count: > 0 };
    }

    private static bool HasAllOfComposition(IOpenApiSchema schema)
    {
        return schema.AllOf is { Count: > 0 };
    }

    private static IReadOnlyDictionary<string, IOpenApiSchema?> GetProperties(IOpenApiSchema schema)
    {
        var properties = new SortedDictionary<string, IOpenApiSchema?>(StringComparer.Ordinal);

        AddProperties(properties, schema.Properties);

        foreach (var component in EnumerateAllOfSchemas(schema))
        {
            if (ReferenceEquals(component, schema))
            {
                continue;
            }

            AddProperties(properties, component.Properties);
        }

        return properties;
    }

    private static void AddProperties(
        IDictionary<string, IOpenApiSchema?> destination,
        IDictionary<string, IOpenApiSchema>? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var property in source)
        {
            destination[property.Key] = property.Value;
        }
    }

    private static IOpenApiSchema? GetItems(IOpenApiSchema schema)
    {
        if (schema.Items is not null)
        {
            return schema.Items;
        }

        foreach (var component in EnumerateAllOfSchemas(schema))
        {
            if (ReferenceEquals(component, schema))
            {
                continue;
            }

            if (component.Items is not null)
            {
                return component.Items;
            }
        }

        return null;
    }

    private static IEnumerable<IOpenApiSchema> EnumerateAllOfSchemas(IOpenApiSchema schema)
    {
        yield return schema;

        if (schema.AllOf is null)
        {
            yield break;
        }

        foreach (var component in schema.AllOf)
        {
            if (component is null)
            {
                continue;
            }

            foreach (var nestedComponent in EnumerateAllOfSchemas(component))
            {
                yield return nestedComponent;
            }
        }
    }

    private static bool HasTypeToken(IOpenApiSchema schema, string token)
    {
        var typeText = schema.Type?.ToString();
        return !string.IsNullOrWhiteSpace(typeText)
            && typeText.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetNullableFlag(IOpenApiSchema schema, out bool value)
    {
        value = false;

        var nullableProperty = schema.GetType().GetProperty("Nullable", BindingFlags.Public | BindingFlags.Instance);
        if (nullableProperty is null)
        {
            return false;
        }

        var rawValue = nullableProperty.GetValue(schema);
        if (rawValue is bool booleanValue)
        {
            value = booleanValue;
            return true;
        }

        return false;
    }

    private static string SerializeAnyValue(object value)
    {
        return value switch
        {
            JsonNode jsonNode => SerializeJsonNode(jsonNode),
            OpenApiAny anyValue => anyValue.Node?.ToJsonString() ?? anyValue.ToString() ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string SerializeJsonNode(JsonNode jsonNode)
    {
        if (jsonNode is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out var stringValue))
            {
                return stringValue;
            }

            if (jsonValue.TryGetValue<bool>(out var booleanValue))
            {
                return booleanValue ? "true" : "false";
            }

            if (jsonValue.TryGetValue<long>(out var longValue))
            {
                return longValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<double>(out var doubleValue))
            {
                return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return jsonNode.ToJsonString();
    }
}