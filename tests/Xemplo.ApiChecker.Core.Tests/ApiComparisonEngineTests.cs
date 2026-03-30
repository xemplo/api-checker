using Xemplo.ApiChecker.Core;
using Microsoft.OpenApi.Models;

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

    private static async Task<ApiComparisonInput> LoadRequestBodyFixturePairAsync()
    {
        var loader = new ApiSpecificationLoader();
        var oldPath = GetFixturePath("request-body-old.yaml");
        var newPath = GetFixturePath("request-body-new.yaml");
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

    private static ApiSpecificationDocument CreateSpecification(OpenApiOperation operation)
    {
        var pathItem = new OpenApiPathItem();
        pathItem.AddOperation(System.Net.Http.HttpMethod.Post, operation);

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

    private static OpenApiSchema CreateScalarSchema(bool readOnly = false)
    {
        return new OpenApiSchema
        {
            ReadOnly = readOnly,
            Type = JsonSchemaType.String
        };
    }
}