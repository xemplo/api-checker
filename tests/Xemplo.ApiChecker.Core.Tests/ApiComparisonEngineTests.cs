using Xemplo.ApiChecker.Core;
using Microsoft.OpenApi.Models;
using NSubstitute;

namespace Xemplo.ApiChecker.Core.Tests;

public class ApiComparisonEngineTests
{
    [Fact]
    public async Task Compare_RequestBodyFixtures_ProducesExpectedFindings()
    {
        var engine = new ApiComparisonEngine();
        var input = await LoadRequestBodyFixturePairAsync();

        var result = engine.Compare(input, ApiRuleProfile.Default);

        Assert.Collection(
            result.Findings,
            finding =>
            {
                Assert.Equal(ApiRuleId.NewRequiredInput, finding.RuleId);
                Assert.Equal(ApiSeverity.Error, finding.Severity);
                Assert.Equal("$.pet.age", finding.SchemaPath);
            },
            finding =>
            {
                Assert.Equal(ApiRuleId.RemovedInput, finding.RuleId);
                Assert.Equal(ApiSeverity.Error, finding.Severity);
                Assert.Equal("$.pet.legacy", finding.SchemaPath);
            },
            finding =>
            {
                Assert.Equal(ApiRuleId.NewOptionalInput, finding.RuleId);
                Assert.Equal(ApiSeverity.Warning, finding.Severity);
                Assert.Equal("$.pet.tags[].code", finding.SchemaPath);
            });
    }

    [Fact]
    public async Task Compare_RequestBodyFixtures_SkipsReadOnlyAndAmbiguousFields()
    {
        var engine = new ApiComparisonEngine();
        var input = await LoadRequestBodyFixturePairAsync();

        var result = engine.Compare(input, ApiRuleProfile.Default);

        Assert.DoesNotContain(result.Findings, static finding => finding.SchemaPath == "$.pet.serverAssigned");
        Assert.DoesNotContain(result.Findings, static finding => finding.SchemaPath == "$.pet.polymorphic");
    }

    [Fact]
    public async Task Compare_WhenRuleSeverityIsOff_SuppressesMatchingFindings()
    {
        var engine = new ApiComparisonEngine();
        var input = await LoadRequestBodyFixturePairAsync();
        var profile = new ApiRuleProfile(new Dictionary<ApiRuleId, ApiSeverity>
        {
            [ApiRuleId.NewRequiredInput] = ApiSeverity.Off,
            [ApiRuleId.NewOptionalInput] = ApiSeverity.Warning,
            [ApiRuleId.RemovedInput] = ApiSeverity.Error
        });

        var result = engine.Compare(input, profile);

        Assert.DoesNotContain(result.Findings, static finding => finding.RuleId == ApiRuleId.NewRequiredInput);
        Assert.Contains(result.Findings, static finding => finding.RuleId == ApiRuleId.NewOptionalInput);
        Assert.Contains(result.Findings, static finding => finding.RuleId == ApiRuleId.RemovedInput);
    }

    [Fact]
    public void Compare_WhenRequestFieldChangesFromReadOnlyToWritable_ReportsAddedInput()
    {
        var engine = new ApiComparisonEngine();
        var input = new ApiComparisonInput(
            CreateSpecification(CreateRequestBodyOperation(CreateObjectSchema(("serverAssigned", CreateScalarSchema(readOnly: true), true)))),
            CreateSpecification(CreateRequestBodyOperation(CreateObjectSchema(("serverAssigned", CreateScalarSchema(), true)))));

        var result = engine.Compare(input, ApiRuleProfile.Default);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(ApiRuleId.NewRequiredInput, finding.RuleId);
        Assert.Equal(ApiSeverity.Error, finding.Severity);
        Assert.Equal("$.serverAssigned", finding.SchemaPath);
    }

    [Fact]
    public void Compare_WhenExistingRequestFieldBecomesRequired_ReportsUpdatedRequiredInput()
    {
        var engine = new ApiComparisonEngine();
        var input = new ApiComparisonInput(
            CreateSpecification(CreateRequestBodyOperation(CreateObjectSchema(("name", CreateScalarSchema(), false)))),
            CreateSpecification(CreateRequestBodyOperation(CreateObjectSchema(("name", CreateScalarSchema(), true)))));

        var finding = Assert.Single(engine.Compare(input, ApiRuleProfile.Default).Findings);

        Assert.Equal(ApiRuleId.UpdatedRequiredInput, finding.RuleId);
        Assert.Equal(ApiSeverity.Error, finding.Severity);
        Assert.Equal("$.name", finding.SchemaPath);
        Assert.Contains("optional input to required input", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_WhenExistingRequestFieldBecomesOptional_ReportsUpdatedOptionalInput()
    {
        var engine = new ApiComparisonEngine();
        var input = new ApiComparisonInput(
            CreateSpecification(CreateRequestBodyOperation(CreateObjectSchema(("name", CreateScalarSchema(), true)))),
            CreateSpecification(CreateRequestBodyOperation(CreateObjectSchema(("name", CreateScalarSchema(), false)))));

        var finding = Assert.Single(engine.Compare(input, ApiRuleProfile.Default).Findings);

        Assert.Equal(ApiRuleId.UpdatedOptionalInput, finding.RuleId);
        Assert.Equal(ApiSeverity.Warning, finding.Severity);
        Assert.Equal("$.name", finding.SchemaPath);
        Assert.Contains("required input to optional input", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compare_QueryParameterFixtures_ProducesExpectedFindings()
    {
        var engine = new ApiComparisonEngine();
        var input = await LoadFixturePairAsync("query-params-old.json", "query-params-new.json");

        var result = engine.Compare(input, ApiRuleProfile.Default);

        Assert.Collection(
            result.Findings,
            finding =>
            {
                Assert.Equal(ApiRuleId.NewRequiredQueryParam, finding.RuleId);
                Assert.Equal(ApiSeverity.Error, finding.Severity);
                Assert.Equal("$query.filter", finding.SchemaPath);
            },
            finding =>
            {
                Assert.Equal(ApiRuleId.NewOptionalQueryParam, finding.RuleId);
                Assert.Equal(ApiSeverity.Warning, finding.Severity);
                Assert.Equal("$query.includeDetails", finding.SchemaPath);
            },
            finding =>
            {
                Assert.Equal(ApiRuleId.NewOptionalQueryParam, finding.RuleId);
                Assert.Equal(ApiSeverity.Warning, finding.Severity);
                Assert.Equal("$query.limit", finding.SchemaPath);
            });
    }

    [Fact]
    public void Compare_QueryParameters_UseOperationOverridesOverPathItemParameters()
    {
        var engine = new ApiComparisonEngine();
        var oldInput = CreateSpecification(CreateGetOperation(), pathItemParameters: [], method: System.Net.Http.HttpMethod.Get);
        var newInput = CreateSpecification(
            CreateGetOperation(operationParameters:
            [
                CreateQueryParameter("filter", required: false, schema: CreateScalarSchema())
            ]),
            pathItemParameters:
            [
                CreateQueryParameter("filter", required: true, schema: CreateScalarSchema())
            ],
            method: System.Net.Http.HttpMethod.Get);

        var result = engine.Compare(new ApiComparisonInput(oldInput, newInput), ApiRuleProfile.Default);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(ApiRuleId.NewOptionalQueryParam, finding.RuleId);
        Assert.Equal("$query.filter", finding.SchemaPath);
    }

    [Fact]
    public void Compare_QueryParameters_IgnoresNonQueryAndAmbiguousRequiredParameters()
    {
        var engine = new ApiComparisonEngine();
        var oldSpecification = CreateSpecification(CreateGetOperation(), pathItemParameters: [], method: System.Net.Http.HttpMethod.Get);
        var newSpecification = CreateSpecification(
            CreateGetOperation(operationParameters:
            [
                CreateHeaderParameter("traceId", required: true, schema: CreateScalarSchema()),
                CreateQueryParameter("polymorphic", required: true, schema: CreateOneOfScalarSchema())
            ]),
            pathItemParameters: [],
            method: System.Net.Http.HttpMethod.Get);

        var result = engine.Compare(new ApiComparisonInput(oldSpecification, newSpecification), ApiRuleProfile.Default);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task Compare_ResponseBodyFixtures_ProducesExpectedFindings()
    {
        var engine = new ApiComparisonEngine();
        var input = await LoadFixturePairAsync("response-body-old.json", "response-body-new.json");

        var result = engine.Compare(input, ApiRuleProfile.Default);

        Assert.Collection(
            result.Findings,
            finding =>
            {
                Assert.Equal(ApiRuleId.NewNonNullableOutput, finding.RuleId);
                Assert.Equal(ApiSeverity.Warning, finding.Severity);
                Assert.Equal("$.result.count", finding.SchemaPath);
            },
            finding =>
            {
                Assert.Equal(ApiRuleId.RemovedOutput, finding.RuleId);
                Assert.Equal(ApiSeverity.Error, finding.Severity);
                Assert.Equal("$.result.legacy", finding.SchemaPath);
            },
            finding =>
            {
                Assert.Equal(ApiRuleId.NewEnumOutput, finding.RuleId);
                Assert.Equal(ApiSeverity.Warning, finding.Severity);
                Assert.Equal("$.result.status", finding.SchemaPath);
                Assert.Contains("Archived", finding.Message, StringComparison.Ordinal);
            },
            finding =>
            {
                Assert.Equal(ApiRuleId.NewNullableOutput, finding.RuleId);
                Assert.Equal(ApiSeverity.Warning, finding.Severity);
                Assert.Equal("$.result.summary", finding.SchemaPath);
            },
            finding =>
            {
                Assert.Equal(ApiRuleId.NewNonNullableOutput, finding.RuleId);
                Assert.Equal(ApiSeverity.Warning, finding.Severity);
                Assert.Equal("$.result.traceId", finding.SchemaPath);
            });
    }

    [Fact]
    public async Task Compare_ResponseBodyFixtures_SkipsWriteOnlyAndAmbiguousFields()
    {
        var engine = new ApiComparisonEngine();
        var input = await LoadFixturePairAsync("response-body-old.json", "response-body-new.json");

        var result = engine.Compare(input, ApiRuleProfile.Default);

        Assert.DoesNotContain(result.Findings, static finding => finding.SchemaPath == "$.result.internalNote");
        Assert.DoesNotContain(result.Findings, static finding => finding.SchemaPath == "$.result.polymorphic");
    }

    [Fact]
    public void Compare_WhenExistingResponseFieldBecomesNullable_ReportsUpdatedNullableOutput()
    {
        var oldSchema = CreateObjectSchema(("summary", CreateScalarSchema(), false));
        var newSchema = CreateObjectSchema(("summary", CreateScalarSchema(), false));
        var analyzer = CreateSchemaAnalyzer(
            (oldSchema, ApiSchemaContext.Response, CreateAnalysis(("$.summary", ApiSchemaCondition.False, ApiSchemaCondition.False))),
            (newSchema, ApiSchemaContext.Response, CreateAnalysis(("$.summary", ApiSchemaCondition.False, ApiSchemaCondition.True))));
        var engine = new ApiComparisonEngine(schemaAnalyzer: analyzer);
        var input = new ApiComparisonInput(
            CreateSpecification(CreateResponseBodyOperation(oldSchema)),
            CreateSpecification(CreateResponseBodyOperation(newSchema)));

        var finding = Assert.Single(engine.Compare(input, ApiRuleProfile.Default).Findings);

        Assert.Equal(ApiRuleId.UpdatedNullableOutput, finding.RuleId);
        Assert.Equal(ApiSeverity.Warning, finding.Severity);
        Assert.Equal("$.summary", finding.SchemaPath);
        Assert.Contains("non-nullable output to nullable output", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_WhenExistingResponseFieldBecomesNonNullable_ReportsUpdatedNonNullableOutput()
    {
        var oldSchema = CreateObjectSchema(("summary", CreateScalarSchema(), false));
        var newSchema = CreateObjectSchema(("summary", CreateScalarSchema(), false));
        var analyzer = CreateSchemaAnalyzer(
            (oldSchema, ApiSchemaContext.Response, CreateAnalysis(("$.summary", ApiSchemaCondition.False, ApiSchemaCondition.True))),
            (newSchema, ApiSchemaContext.Response, CreateAnalysis(("$.summary", ApiSchemaCondition.False, ApiSchemaCondition.False))));
        var engine = new ApiComparisonEngine(schemaAnalyzer: analyzer);
        var input = new ApiComparisonInput(
            CreateSpecification(CreateResponseBodyOperation(oldSchema)),
            CreateSpecification(CreateResponseBodyOperation(newSchema)));

        var finding = Assert.Single(engine.Compare(input, ApiRuleProfile.Default).Findings);

        Assert.Equal(ApiRuleId.UpdatedNonNullableOutput, finding.RuleId);
        Assert.Equal(ApiSeverity.Warning, finding.Severity);
        Assert.Equal("$.summary", finding.SchemaPath);
        Assert.Contains("nullable output to non-nullable output", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compare_EndpointAndResponseCodeFixtures_ProducesExpectedFindings()
    {
        var engine = new ApiComparisonEngine();
        var input = await LoadFixturePairAsync("endpoint-responsecode-old.json", "endpoint-responsecode-new.json");

        var result = engine.Compare(input, ApiRuleProfile.Default);

        Assert.Collection(
            result.Findings,
            finding =>
            {
                Assert.Equal(ApiRuleId.EndpointRemoved, finding.RuleId);
                Assert.Equal(ApiSeverity.Error, finding.Severity);
                Assert.Equal("GET", finding.Operation!.Method);
                Assert.Equal("/orders", finding.Operation.PathTemplate);
            },
            finding =>
            {
                Assert.Equal(ApiRuleId.NewResponseCode, finding.RuleId);
                Assert.Equal(ApiSeverity.Warning, finding.Severity);
                Assert.Equal("GET", finding.Operation!.Method);
                Assert.Equal("/pets", finding.Operation.PathTemplate);
                Assert.Contains("201", finding.Message, StringComparison.Ordinal);
            },
            finding =>
            {
                Assert.Equal(ApiRuleId.NewEndpoint, finding.RuleId);
                Assert.Equal(ApiSeverity.Warning, finding.Severity);
                Assert.Equal("POST", finding.Operation!.Method);
                Assert.Equal("/pets", finding.Operation.PathTemplate);
            });
    }

    [Fact]
    public void Compare_MethodChangeOnSamePath_ProducesRemovedAndNewEndpointFindings()
    {
        var engine = new ApiComparisonEngine();
        var oldSpecification = CreateSpecification(CreateGetOperation(), method: System.Net.Http.HttpMethod.Get);
        var newSpecification = CreateSpecification(CreateGetOperation(), method: System.Net.Http.HttpMethod.Post);

        var result = engine.Compare(new ApiComparisonInput(oldSpecification, newSpecification), ApiRuleProfile.Default);

        Assert.Collection(
            result.Findings,
            finding =>
            {
                Assert.Equal(ApiRuleId.EndpointRemoved, finding.RuleId);
                Assert.Equal("GET", finding.Operation!.Method);
                Assert.Equal("/pets", finding.Operation.PathTemplate);
            },
            finding =>
            {
                Assert.Equal(ApiRuleId.NewEndpoint, finding.RuleId);
                Assert.Equal("POST", finding.Operation!.Method);
                Assert.Equal("/pets", finding.Operation.PathTemplate);
            });
    }

    [Fact]
    public void Compare_WhenOperationIdChanges_ReportsUpdatedEndpointId()
    {
        var engine = new ApiComparisonEngine();
        var oldSpecification = CreateSpecification(CreateGetOperation(operationId: "listPets"), method: System.Net.Http.HttpMethod.Get);
        var newSpecification = CreateSpecification(CreateGetOperation(operationId: "getPets"), method: System.Net.Http.HttpMethod.Get);

        var finding = Assert.Single(engine.Compare(new ApiComparisonInput(oldSpecification, newSpecification), ApiRuleProfile.Default).Findings);

        Assert.Equal(ApiRuleId.UpdatedEndpointId, finding.RuleId);
        Assert.Equal(ApiSeverity.Warning, finding.Severity);
        Assert.Equal("GET", finding.Operation!.Method);
        Assert.Equal("/pets", finding.Operation.PathTemplate);
        Assert.Contains("listPets", finding.Message, StringComparison.Ordinal);
        Assert.Contains("getPets", finding.Message, StringComparison.Ordinal);
    }

    private static async Task<ApiComparisonInput> LoadRequestBodyFixturePairAsync()
    {
        return await LoadFixturePairAsync("request-body-old.json", "request-body-new.json");
    }

    private static async Task<ApiComparisonInput> LoadFixturePairAsync(string oldFileName, string newFileName)
    {
        var loader = new ApiSpecificationLoader();
        var oldPath = GetFixturePath(oldFileName);
        var newPath = GetFixturePath(newFileName);
        var oldResult = await loader.LoadAsync(oldPath);
        var newResult = await loader.LoadAsync(newPath);

        Assert.True(oldResult.IsSuccess, oldResult.Failure?.Message);
        Assert.True(newResult.IsSuccess, newResult.Failure?.Message);

        return new ApiComparisonInput(oldResult.Specification!, newResult.Specification!);
    }

    private static string GetFixturePath(string fileName)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", fileName));
    }

    private static ApiSpecificationDocument CreateSpecification(
        OpenApiOperation operation,
        IReadOnlyList<Microsoft.OpenApi.Models.Interfaces.IOpenApiParameter>? pathItemParameters = null,
        System.Net.Http.HttpMethod? method = null)
    {
        var pathItem = new OpenApiPathItem
        {
            Parameters = pathItemParameters is null ? [] : [.. pathItemParameters]
        };
        pathItem.AddOperation(method ?? System.Net.Http.HttpMethod.Post, operation);

        return new ApiSpecificationDocument(new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test", Version = "1.0.0" },
            Paths = new OpenApiPaths
            {
                ["/pets"] = pathItem
            }
        });
    }

    private static OpenApiOperation CreateRequestBodyOperation(OpenApiSchema requestSchema)
    {
        return new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = requestSchema }
                }
            },
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "ok" }
            }
        };
    }

    private static OpenApiOperation CreateResponseBodyOperation(OpenApiSchema responseSchema)
    {
        return new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Description = "ok",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new() { Schema = responseSchema }
                    }
                }
            }
        };
    }

    private static OpenApiOperation CreateGetOperation(
        IReadOnlyList<Microsoft.OpenApi.Models.Interfaces.IOpenApiParameter>? operationParameters = null,
        string? operationId = null)
    {
        return new OpenApiOperation
        {
            OperationId = operationId,
            Parameters = operationParameters is null ? [] : [.. operationParameters],
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = "ok" }
            }
        };
    }

    private static OpenApiSchema CreateObjectSchema(params (string Name, OpenApiSchema Schema, bool Required)[] properties)
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

    private static OpenApiSchema CreateScalarSchema(bool readOnly = false, bool nullable = false)
    {
        var schema = new OpenApiSchema
        {
            ReadOnly = readOnly,
            Type = JsonSchemaType.String
        };

        SetNullable(schema, nullable);
        return schema;
    }

    private static OpenApiSchema CreateOneOfScalarSchema()
    {
        var schema = new OpenApiSchema();
        schema.OneOf ??= [];
        schema.OneOf.Add(new OpenApiSchema { Type = JsonSchemaType.String });
        schema.OneOf.Add(new OpenApiSchema { Type = JsonSchemaType.Integer });
        return schema;
    }

    private static OpenApiParameter CreateQueryParameter(string name, bool required, OpenApiSchema? schema = null)
    {
        return new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Query,
            Required = required,
            Schema = schema
        };
    }

    private static OpenApiParameter CreateHeaderParameter(string name, bool required, OpenApiSchema? schema = null)
    {
        return new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = required,
            Schema = schema
        };
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

    private static IApiJsonSchemaAnalyzer CreateSchemaAnalyzer(
        params (OpenApiSchema Schema, ApiSchemaContext Context, ApiJsonSchemaAnalysis Analysis)[] mappings)
    {
        var analyzer = Substitute.For<IApiJsonSchemaAnalyzer>();

        analyzer.Analyze(Arg.Any<Microsoft.OpenApi.Models.Interfaces.IOpenApiSchema?>(), Arg.Any<ApiSchemaContext>(), Arg.Any<string>())
            .Returns(ApiJsonSchemaAnalysis.Empty);

        foreach (var (schema, context, analysis) in mappings)
        {
            analyzer.Analyze(
                Arg.Is<Microsoft.OpenApi.Models.Interfaces.IOpenApiSchema?>(candidate => ReferenceEquals(candidate, schema)),
                context,
                Arg.Any<string>())
                .Returns(analysis);
        }

        return analyzer;
    }

    private static ApiJsonSchemaAnalysis CreateAnalysis(params (string Path, ApiSchemaCondition Required, ApiSchemaCondition Nullable)[] nodes)
    {
        return new ApiJsonSchemaAnalysis(nodes.Select(node => new ApiJsonSchemaNode(
            node.Path,
            node.Path.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(),
            ApiJsonSchemaNodeKind.Scalar,
            ApiSchemaUsage.Included,
            node.Required,
            node.Nullable,
            ApiSchemaCondition.False,
            Array.Empty<string>(),
            Array.Empty<string>())).ToArray());
    }
}