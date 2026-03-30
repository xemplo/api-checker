namespace Xemplo.ApiChecker.Core;

public sealed class ApiComparisonEngine : IApiComparisonEngine
{
    public ApiComparisonResult Compare(ApiComparisonInput input, ApiRuleProfile ruleProfile)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(ruleProfile);

        return ApiComparisonResult.Empty;
    }
}