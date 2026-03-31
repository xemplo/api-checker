namespace Xemplo.ApiChecker.Core;

internal interface IApiRuleEvaluator
{
    ApiRuleFamily Family { get; }

    void Evaluate(ApiComparisonMap comparisonMap, ApiRuleProfile ruleProfile, ICollection<ApiFinding> findings);
}