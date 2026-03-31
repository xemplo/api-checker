namespace Xemplo.ApiChecker.Core;

public static class ApiRuleIdExtensions
{
    private static readonly IReadOnlyDictionary<string, ApiRuleId> RuleIdsByIdentifier =
        new Dictionary<string, ApiRuleId>(StringComparer.OrdinalIgnoreCase)
        {
            ["input:new:required"] = ApiRuleId.NewRequiredInput,
            ["input:new:optional"] = ApiRuleId.NewOptionalInput,
            ["output:new:nullable"] = ApiRuleId.NewNullableOutput,
            ["output:new:non-nullable"] = ApiRuleId.NewNonNullableOutput,
            ["output:new:enum-value"] = ApiRuleId.NewEnumOutput,
            ["query:new:required"] = ApiRuleId.NewRequiredQueryParam,
            ["query:new:optional"] = ApiRuleId.NewOptionalQueryParam,
            ["input:removed"] = ApiRuleId.RemovedInput,
            ["output:removed"] = ApiRuleId.RemovedOutput,
            ["response:new:status-code"] = ApiRuleId.NewResponseCode,
            ["endpoint:new"] = ApiRuleId.NewEndpoint,
            ["endpoint:removed"] = ApiRuleId.EndpointRemoved
        };

    public static string GetIdentifier(this ApiRuleId ruleId)
    {
        return ruleId switch
        {
            ApiRuleId.NewRequiredInput => "input:new:required",
            ApiRuleId.NewOptionalInput => "input:new:optional",
            ApiRuleId.NewNullableOutput => "output:new:nullable",
            ApiRuleId.NewNonNullableOutput => "output:new:non-nullable",
            ApiRuleId.NewEnumOutput => "output:new:enum-value",
            ApiRuleId.NewRequiredQueryParam => "query:new:required",
            ApiRuleId.NewOptionalQueryParam => "query:new:optional",
            ApiRuleId.RemovedInput => "input:removed",
            ApiRuleId.RemovedOutput => "output:removed",
            ApiRuleId.NewResponseCode => "response:new:status-code",
            ApiRuleId.NewEndpoint => "endpoint:new",
            ApiRuleId.EndpointRemoved => "endpoint:removed",
            _ => throw new ArgumentOutOfRangeException(nameof(ruleId), ruleId, "Unsupported rule identifier.")
        };
    }

    public static bool TryParseIdentifier(string? text, out ApiRuleId ruleId)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ruleId = default;
            return false;
        }

        return RuleIdsByIdentifier.TryGetValue(text.Trim(), out ruleId);
    }
}