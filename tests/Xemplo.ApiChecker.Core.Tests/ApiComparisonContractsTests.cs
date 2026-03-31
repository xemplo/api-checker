using NSubstitute;
using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Core.Tests;

public class ApiComparisonContractsTests
{
    [Fact]
    public void RuleIdentifiers_AreDefinedForV1Requirements()
    {
        var expected = new[]
        {
            "input:new:required",
            "input:new:optional",
            "input:updated:required",
            "input:updated:optional",
            "output:new:nullable",
            "output:new:non-nullable",
            "output:updated:nullable",
            "output:updated:non-nullable",
            "output:new:enum-value",
            "query:new:required",
            "query:new:optional",
            "input:removed",
            "output:removed",
            "response:new:status-code",
            "endpoint:new",
            "endpoint:updated:id",
            "endpoint:removed"
        };

        var actual = Enum.GetValues<ApiRuleId>()
            .Select(static ruleId => ruleId.GetIdentifier())
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RuleCatalog_MapsCanonicalIdentifiersToMetadata()
    {
        foreach (var descriptor in ApiRuleCatalog.All)
        {
            var resolved = ApiRuleCatalog.GetDescriptor(descriptor.Id);

            Assert.Equal(descriptor, resolved);
            Assert.True(ApiRuleCatalog.TryGetDescriptor(descriptor.Identifier, out var parsed));
            Assert.Equal(descriptor, parsed);
        }
    }

    [Fact]
    public void RuleCatalog_CollectionsAreExposedAsReadOnlyViews()
    {
        Assert.False(ApiRuleCatalog.All is ApiRuleDescriptor[]);
        Assert.False(ApiRuleCatalog.GetDescriptors(ApiRuleFamily.Input) is ApiRuleDescriptor[]);
    }

    [Fact]
    public void DefaultProfile_UsesExpectedSeverities()
    {
        var profile = ApiRuleProfile.Default;

        Assert.Equal(ApiSeverity.Error, profile.GetSeverity(ApiRuleId.NewRequiredInput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewOptionalInput));
        Assert.Equal(ApiSeverity.Error, profile.GetSeverity(ApiRuleId.UpdatedRequiredInput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.UpdatedOptionalInput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewNullableOutput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewNonNullableOutput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.UpdatedNullableOutput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.UpdatedNonNullableOutput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewEnumOutput));
        Assert.Equal(ApiSeverity.Error, profile.GetSeverity(ApiRuleId.NewRequiredQueryParam));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewOptionalQueryParam));
        Assert.Equal(ApiSeverity.Error, profile.GetSeverity(ApiRuleId.RemovedInput));
        Assert.Equal(ApiSeverity.Error, profile.GetSeverity(ApiRuleId.RemovedOutput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewResponseCode));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewEndpoint));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.UpdatedEndpointId));
        Assert.Equal(ApiSeverity.Error, profile.GetSeverity(ApiRuleId.EndpointRemoved));
    }

    [Fact]
    public void ComparisonResult_DetectsErrorFindings()
    {
        var result = new ApiComparisonResult(
            new[]
            {
                new ApiFinding(ApiRuleId.NewOptionalInput, ApiSeverity.Warning, "optional"),
                new ApiFinding(ApiRuleId.RemovedOutput, ApiSeverity.Error, "removed")
            });

        Assert.True(result.HasErrorFindings);
    }

    [Fact]
    public void ComparisonResult_WithoutErrors_DoesNotReportErrorFindings()
    {
        var result = new ApiComparisonResult(
            new[]
            {
                new ApiFinding(ApiRuleId.NewOptionalInput, ApiSeverity.Warning, "optional")
            });

        Assert.False(result.HasErrorFindings);
    }

    [Fact]
    public void EmptyComparisonResult_HasNoFindingsOrErrors()
    {
        Assert.Empty(ApiComparisonResult.Empty.Findings);
        Assert.False(ApiComparisonResult.Empty.HasErrorFindings);
    }

    [Fact]
    public void UnknownRuleSeverity_DefaultsToOff()
    {
        var profile = new ApiRuleProfile(new Dictionary<ApiRuleId, ApiSeverity>());

        Assert.Equal(ApiSeverity.Off, profile.GetSeverity(ApiRuleId.NewEndpoint));
    }

    [Fact]
    public void RuleProfile_ThrowsForNullSeverities()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ApiRuleProfile(null!));

        Assert.Equal("severities", exception.ParamName);
    }

    [Fact]
    public void RuleProfile_CanDetectWhenAFamilyIsDisabled()
    {
        var profile = new ApiRuleProfile(new Dictionary<ApiRuleId, ApiSeverity>
        {
            [ApiRuleId.NewRequiredInput] = ApiSeverity.Off,
            [ApiRuleId.NewOptionalInput] = ApiSeverity.Off,
            [ApiRuleId.RemovedInput] = ApiSeverity.Off,
            [ApiRuleId.NewEndpoint] = ApiSeverity.Warning
        });

        Assert.False(profile.HasEnabledRules(ApiRuleFamily.Input));
        Assert.True(profile.HasEnabledRules(ApiRuleFamily.Endpoint));
    }

    [Fact]
    public void Engine_ImplementsLibraryBoundaryContract()
    {
        var engine = new ApiComparisonEngine();

        var result = engine.Compare(ApiComparisonInput.Empty, ApiRuleProfile.Default);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Engine_ThrowsForNullInput()
    {
        var engine = new ApiComparisonEngine();

        Assert.Throws<ArgumentNullException>(() => engine.Compare(null!, ApiRuleProfile.Default));
    }

    [Fact]
    public void Engine_ThrowsForNullRuleProfile()
    {
        var engine = new ApiComparisonEngine();

        Assert.Throws<ArgumentNullException>(() => engine.Compare(ApiComparisonInput.Empty, null!));
    }

    [Fact]
    public void EngineContract_CanBeSubstituted_ForNonCliConsumers()
    {
        var engine = Substitute.For<IApiComparisonEngine>();
        var expected = new ApiComparisonResult(
            new[]
            {
                new ApiFinding(
                    ApiRuleId.NewEndpoint,
                    ApiSeverity.Warning,
                    "Endpoint added",
                    new ApiOperationIdentity("GET", "/pets"),
                    "paths./pets.get")
            });

        engine.Compare(ApiComparisonInput.Empty, ApiRuleProfile.Default).Returns(expected);

        var actual = engine.Compare(ApiComparisonInput.Empty, ApiRuleProfile.Default);

        Assert.Single(actual.Findings);
        Assert.Equal(ApiRuleId.NewEndpoint, actual.Findings[0].RuleId);
    }

    [Fact]
    public async Task RuleProfileResolver_LoadFromFile_CanBeUsedOutsideCli()
    {
        var directory = CreateTempDirectory();
        var configPath = Path.Combine(directory, "rules.json");
        await File.WriteAllTextAsync(configPath, """
            {
              "rules": {
                                "input:new:required": "off",
                                "input:removed": "warning"
              }
            }
            """);

        try
        {
            var profile = await ApiRuleProfileResolver.LoadFromFileAsync(configPath);

            Assert.Equal(ApiSeverity.Off, profile.GetSeverity(ApiRuleId.NewRequiredInput));
            Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.RemovedInput));
            Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewOptionalInput));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"api-checker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}