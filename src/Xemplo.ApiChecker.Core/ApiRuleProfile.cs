namespace Xemplo.ApiChecker.Core;

public sealed class ApiRuleProfile
{
    private readonly IReadOnlyDictionary<ApiRuleId, ApiSeverity> _severities;

    public static ApiRuleProfile Default { get; } = new(new Dictionary<ApiRuleId, ApiSeverity>
    {
        [ApiRuleId.NewRequiredInput] = ApiSeverity.Error,
        [ApiRuleId.NewOptionalInput] = ApiSeverity.Warning,
        [ApiRuleId.NewNullableOutput] = ApiSeverity.Warning,
        [ApiRuleId.NewNonNullableOutput] = ApiSeverity.Warning,
        [ApiRuleId.NewEnumOutput] = ApiSeverity.Warning,
        [ApiRuleId.NewRequiredQueryParam] = ApiSeverity.Error,
        [ApiRuleId.NewOptionalQueryParam] = ApiSeverity.Warning,
        [ApiRuleId.RemovedInput] = ApiSeverity.Error,
        [ApiRuleId.RemovedOutput] = ApiSeverity.Error,
        [ApiRuleId.NewResponseCode] = ApiSeverity.Warning,
        [ApiRuleId.NewEndpoint] = ApiSeverity.Warning,
        [ApiRuleId.EndpointRemoved] = ApiSeverity.Error
    });

    public ApiRuleProfile(IReadOnlyDictionary<ApiRuleId, ApiSeverity> severities)
    {
        _severities = severities;
    }

    public ApiSeverity GetSeverity(ApiRuleId ruleId)
    {
        return _severities.TryGetValue(ruleId, out var severity)
            ? severity
            : ApiSeverity.Off;
    }
}