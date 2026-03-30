using Xemplo.ApiChecker.Core;

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
}