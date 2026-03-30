namespace Xemplo.ApiChecker.Core;

public interface IApiComparisonEngine
{
    ApiComparisonResult Compare(ApiComparisonInput input, ApiRuleProfile ruleProfile);
}