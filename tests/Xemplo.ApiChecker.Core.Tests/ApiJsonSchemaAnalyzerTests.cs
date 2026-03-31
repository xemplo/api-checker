using System.Text.Json.Nodes;
using Microsoft.OpenApi.Models;
using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Core.Tests;

public class ApiJsonSchemaAnalyzerTests
{
    [Fact]
    public void Analyze_NullSchema_ReturnsEmptyAnalysis()
    {
        var analyzer = new ApiJsonSchemaAnalyzer();

        var result = analyzer.Analyze(null, ApiSchemaContext.Request);

        Assert.Same(ApiJsonSchemaAnalysis.Empty, result);
    }

    [Fact]
    public void Analyze_BlankRootPath_Throws()
    {
        var analyzer = new ApiJsonSchemaAnalyzer();

        Assert.Throws<ArgumentException>(() => analyzer.Analyze(new OpenApiSchema(), ApiSchemaContext.Request, " "));
    }

    [Fact]
    public void Analyze_TraversesNestedObjectsAndArrays()
    {
        var analyzer = new ApiJsonSchemaAnalyzer();
        var schema = ObjectSchema(
            ("pets", ArraySchema(ObjectSchema(
                ("PetId", ScalarSchema(), false),
                ("petId", ScalarSchema(), false),
                ("status", EnumSchema("Draft", "draft"), false))), false),
            ("metadata", ObjectSchema(("createdBy", ScalarSchema(), false), ("labels", ArraySchema(ScalarSchema()), false)), false));

        var result = analyzer.Analyze(schema, ApiSchemaContext.Request);

        Assert.Equal(ApiJsonSchemaNodeKind.Object, AssertNode(result, "$").Kind);
        Assert.Equal(ApiJsonSchemaNodeKind.Array, AssertNode(result, "$.pets").Kind);
        Assert.Equal(ApiJsonSchemaNodeKind.Object, AssertNode(result, "$.pets[]").Kind);
        Assert.Equal("PetId", AssertNode(result, "$.pets[].PetId").PropertyName);
        Assert.Equal("petId", AssertNode(result, "$.pets[].petId").PropertyName);
        Assert.Equal(ApiJsonSchemaNodeKind.Object, AssertNode(result, "$.metadata").Kind);
        Assert.Equal(ApiJsonSchemaNodeKind.Array, AssertNode(result, "$.metadata.labels").Kind);
        Assert.Equal(ApiJsonSchemaNodeKind.Scalar, AssertNode(result, "$.metadata.labels[]").Kind);
    }

    [Fact]
    public void Analyze_PreservesCaseSensitiveEnumValues()
    {
        var analyzer = new ApiJsonSchemaAnalyzer();
        var schema = ObjectSchema(("status", EnumSchema("Draft", "draft"), false));

        var result = analyzer.Analyze(schema, ApiSchemaContext.Response);
        var node = AssertNode(result, "$.status");

        Assert.Equal(new[] { "Draft", "draft" }, node.EnumValues);
    }

    [Fact]
    public void Analyze_RequestScope_ExcludesReadOnlyButKeepsWriteOnly()
    {
        var analyzer = new ApiJsonSchemaAnalyzer();
        var schema = ObjectSchema(
            ("serverAssigned", ScalarSchema(readOnly: true), false),
            ("secret", ScalarSchema(writeOnly: true), false));

        var result = analyzer.Analyze(schema, ApiSchemaContext.Request);

        Assert.Equal(ApiSchemaUsage.Excluded, AssertNode(result, "$.serverAssigned").Usage);
        Assert.Equal(ApiSchemaUsage.Included, AssertNode(result, "$.secret").Usage);
    }

    [Fact]
    public void Analyze_ResponseScope_ExcludesWriteOnlyButKeepsReadOnly()
    {
        var analyzer = new ApiJsonSchemaAnalyzer();
        var schema = ObjectSchema(
            ("serverAssigned", ScalarSchema(readOnly: true), false),
            ("secret", ScalarSchema(writeOnly: true), false));

        var result = analyzer.Analyze(schema, ApiSchemaContext.Response);

        Assert.Equal(ApiSchemaUsage.Included, AssertNode(result, "$.serverAssigned").Usage);
        Assert.Equal(ApiSchemaUsage.Excluded, AssertNode(result, "$.secret").Usage);
    }

    [Fact]
    public void Analyze_TracksRequiredAndDefaultState()
    {
        var analyzer = new ApiJsonSchemaAnalyzer();
        var requiredWithDefault = ScalarSchema(nullable: true, defaultValue: JsonValue.Create("draft"));
        var schema = ObjectSchema(
            ("status", requiredWithDefault, true),
            ("notes", ScalarSchema(), false));

        var result = analyzer.Analyze(schema, ApiSchemaContext.Request);
        var statusNode = AssertNode(result, "$.status");
        var notesNode = AssertNode(result, "$.notes");

        Assert.Equal(ApiSchemaCondition.True, statusNode.Required);
        Assert.Equal(ApiSchemaCondition.True, statusNode.HasDefault);
        Assert.Equal(ApiSchemaCondition.False, notesNode.Required);
    }

    [Fact]
    public void Analyze_CompositionMarksNodeAsAmbiguous()
    {
        var analyzer = new ApiJsonSchemaAnalyzer();
        var composedSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object
        };
        composedSchema.OneOf ??= [];
        composedSchema.OneOf.Add(ScalarSchema());
        composedSchema.OneOf.Add(ObjectSchema(("id", ScalarSchema(), false)));

        var schema = ObjectSchema(("payload", composedSchema, false));

        var result = analyzer.Analyze(schema, ApiSchemaContext.Response);
        var node = AssertNode(result, "$.payload");

        Assert.Equal(ApiSchemaCondition.Ambiguous, node.Nullable);
        Assert.NotEmpty(node.AmbiguityReasons);
        Assert.DoesNotContain(result.Nodes, static candidate => candidate.SchemaPath.Equals("$.payload.id", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_AllOfTraversesMergedPropertiesConservatively()
    {
        var analyzer = new ApiJsonSchemaAnalyzer();
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object
        };
        schema.AllOf ??= [];
        schema.AllOf.Add(ObjectSchema(("id", ScalarSchema(), true)));
        schema.AllOf.Add(ObjectSchema(("tags", ArraySchema(ObjectSchema(("name", ScalarSchema(), false))), false)));

        var result = analyzer.Analyze(schema, ApiSchemaContext.Response);

        Assert.Equal(ApiJsonSchemaNodeKind.Object, AssertNode(result, "$").Kind);
        Assert.Equal(ApiSchemaCondition.True, AssertNode(result, "$.id").Required);
        Assert.Equal(ApiJsonSchemaNodeKind.Array, AssertNode(result, "$.tags").Kind);
        Assert.Equal(ApiJsonSchemaNodeKind.Object, AssertNode(result, "$.tags[]").Kind);
        Assert.Equal(ApiJsonSchemaNodeKind.Scalar, AssertNode(result, "$.tags[].name").Kind);
        Assert.Equal(ApiSchemaCondition.Ambiguous, AssertNode(result, "$").Nullable);
        Assert.Contains(AssertNode(result, "$").AmbiguityReasons, static reason => reason.Contains("allOf", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_ReadOnlyAndWriteOnlyOnSameSchema_IsAmbiguous()
    {
        var analyzer = new ApiJsonSchemaAnalyzer();
        var schema = ObjectSchema(("token", ScalarSchema(readOnly: true, writeOnly: true), false));

        var result = analyzer.Analyze(schema, ApiSchemaContext.Request);

        Assert.Equal(ApiSchemaUsage.Ambiguous, AssertNode(result, "$.token").Usage);
    }

    [Fact]
    public async Task Analyze_LoadedFixture_TraversesFixtureSchemas()
    {
        var fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "schema-analysis.yaml"));
        var loader = new ApiSpecificationLoader();
        var loadResult = await loader.LoadAsync(fixturePath);
        var analyzer = new ApiJsonSchemaAnalyzer();

        Assert.True(loadResult.IsSuccess, loadResult.Failure?.Message);

        var document = loadResult.Specification!.Document;
        var petsPath = Assert.Contains("/pets", document.Paths);
        Assert.NotNull(petsPath.Operations);
        var postOperation = Assert.Contains(System.Net.Http.HttpMethod.Post, petsPath.Operations!);
        var getOperation = Assert.Contains(System.Net.Http.HttpMethod.Get, petsPath.Operations!);
        var requestBody = postOperation.RequestBody;
        Assert.NotNull(requestBody);
        Assert.NotNull(requestBody!.Content);
        var postJsonContent = Assert.Contains("application/json", requestBody.Content);
        Assert.NotNull(getOperation.Responses);
        var okResponse = Assert.Contains("200", getOperation.Responses!);
        Assert.NotNull(okResponse.Content);
        var getJsonContent = Assert.Contains("application/json", okResponse.Content);
        var postRequestSchema = postJsonContent.Schema;
        var getResponseSchema = getJsonContent.Schema;
        Assert.NotNull(postRequestSchema);
        Assert.NotNull(getResponseSchema);

        var requestAnalysis = analyzer.Analyze(postRequestSchema, ApiSchemaContext.Request);
        var responseAnalysis = analyzer.Analyze(getResponseSchema, ApiSchemaContext.Response);

        Assert.Equal(ApiSchemaUsage.Excluded, AssertNode(requestAnalysis, "$.pet.createdAt").Usage);
        Assert.Equal(ApiSchemaUsage.Included, AssertNode(requestAnalysis, "$.pet.secret").Usage);
        Assert.Equal(ApiSchemaCondition.True, AssertNode(requestAnalysis, "$.pet.nickname").Nullable);
        Assert.Equal(new[] { "Alpha", "alpha" }, AssertNode(requestAnalysis, "$.pet.tags[].code").EnumValues);
        Assert.Equal(ApiSchemaUsage.Excluded, AssertNode(responseAnalysis, "$.result.secret").Usage);
        Assert.Equal(ApiSchemaUsage.Included, AssertNode(responseAnalysis, "$.result.traceId").Usage);
    }

    private static ApiJsonSchemaNode AssertNode(ApiJsonSchemaAnalysis analysis, string path)
    {
        return Assert.Single(analysis.Nodes, node => node.SchemaPath.Equals(path, StringComparison.Ordinal));
    }

    private static OpenApiSchema ObjectSchema(params (string Name, OpenApiSchema Schema, bool Required)[] properties)
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, Microsoft.OpenApi.Models.Interfaces.IOpenApiSchema>(),
            Required = new HashSet<string>(StringComparer.Ordinal)
        };

        foreach (var (name, propertySchema, required) in properties)
        {
            schema.Properties[name] = propertySchema;
            if (required)
            {
                schema.Required.Add(name);
            }
        }

        return schema;
    }

    private static OpenApiSchema ArraySchema(OpenApiSchema items)
    {
        return new OpenApiSchema
        {
            Items = items
        };
    }

    private static OpenApiSchema EnumSchema(params string[] values)
    {
        var schema = new OpenApiSchema
        {
            Enum = new List<System.Text.Json.Nodes.JsonNode>()
        };

        foreach (var value in values)
        {
            schema.Enum.Add(JsonValue.Create(value));
        }

        return schema;
    }

    private static OpenApiSchema ScalarSchema(bool readOnly = false, bool writeOnly = false, bool nullable = false, JsonNode? defaultValue = null)
    {
        var schema = new OpenApiSchema
        {
            ReadOnly = readOnly,
            WriteOnly = writeOnly,
            Default = defaultValue
        };

        SetNullable(schema, nullable);
        return schema;
    }

    private static void SetNullable(OpenApiSchema schema, bool value)
    {
        var property = typeof(OpenApiSchema).GetProperty("Nullable");
        if (property is not null)
        {
            if (property.PropertyType == typeof(bool))
            {
                property.SetValue(schema, value);
                return;
            }

            if (property.PropertyType == typeof(bool?))
            {
                property.SetValue(schema, (bool?)value);
                return;
            }
        }

        if (!value)
        {
            return;
        }

        var typeProperty = typeof(OpenApiSchema).GetProperty("Type");
        if (typeProperty?.PropertyType == typeof(string))
        {
            typeProperty.SetValue(schema, "string,null");
            return;
        }

        if (typeProperty?.PropertyType.IsEnum == true)
        {
            var stringValue = Enum.Parse(typeProperty.PropertyType, "String", ignoreCase: true);
            var nullValue = Enum.Parse(typeProperty.PropertyType, "Null", ignoreCase: true);
            var combinedValue = Convert.ToUInt64(stringValue) | Convert.ToUInt64(nullValue);
            typeProperty.SetValue(schema, Enum.ToObject(typeProperty.PropertyType, combinedValue));
        }
    }
}