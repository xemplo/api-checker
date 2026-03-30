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
            nameof(ApiRuleId.NewRequiredInput),
            nameof(ApiRuleId.NewOptionalInput),
            nameof(ApiRuleId.NewNullableOutput),
            nameof(ApiRuleId.NewNonNullableOutput),
            nameof(ApiRuleId.NewEnumOutput),
            nameof(ApiRuleId.NewRequiredQueryParam),
            nameof(ApiRuleId.NewOptionalQueryParam),
            nameof(ApiRuleId.RemovedInput),
            nameof(ApiRuleId.RemovedOutput),
            nameof(ApiRuleId.NewResponseCode),
            nameof(ApiRuleId.NewEndpoint),
            nameof(ApiRuleId.EndpointRemoved)
        };

        var actual = Enum.GetNames<ApiRuleId>();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DefaultProfile_UsesExpectedSeverities()
    {
        var profile = ApiRuleProfile.Default;

        Assert.Equal(ApiSeverity.Error, profile.GetSeverity(ApiRuleId.NewRequiredInput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewOptionalInput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewNullableOutput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewNonNullableOutput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewEnumOutput));
        Assert.Equal(ApiSeverity.Error, profile.GetSeverity(ApiRuleId.NewRequiredQueryParam));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewOptionalQueryParam));
        Assert.Equal(ApiSeverity.Error, profile.GetSeverity(ApiRuleId.RemovedInput));
        Assert.Equal(ApiSeverity.Error, profile.GetSeverity(ApiRuleId.RemovedOutput));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewResponseCode));
        Assert.Equal(ApiSeverity.Warning, profile.GetSeverity(ApiRuleId.NewEndpoint));
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
}