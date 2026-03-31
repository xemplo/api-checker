namespace Xemplo.ApiChecker.Core;

public sealed class ApiRuleProfile
{
    private readonly IReadOnlyDictionary<ApiRuleId, ApiSeverity> _severities;

    public static ApiRuleProfile Default { get; } = new(ApiRuleCatalog.CreateDefaultSeverities());

    public ApiRuleProfile(IReadOnlyDictionary<ApiRuleId, ApiSeverity> severities)
    {
        _severities = new Dictionary<ApiRuleId, ApiSeverity>(severities);
    }

    public ApiSeverity GetSeverity(ApiRuleId ruleId)
    {
        return _severities.TryGetValue(ruleId, out var severity)
            ? severity
            : ApiSeverity.Off;
    }

    public bool HasEnabledRules(ApiRuleFamily family)
    {
        return ApiRuleCatalog.GetDescriptors(family)
            .Any(descriptor => GetSeverity(descriptor.Id) != ApiSeverity.Off);
    }
}