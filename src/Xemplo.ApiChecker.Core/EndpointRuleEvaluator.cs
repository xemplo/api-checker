namespace Xemplo.ApiChecker.Core;

internal sealed class EndpointRuleEvaluator : IApiRuleEvaluator
{
    public ApiRuleFamily Family => ApiRuleFamily.Endpoint;

    public void Evaluate(ApiComparisonMap comparisonMap, ApiRuleProfile ruleProfile, ICollection<ApiFinding> findings)
    {
        foreach (var operation in comparisonMap.UnmatchedOldOperations)
        {
            var finding = ApiRuleEvaluationHelpers.CreateOperationFinding(
                ApiRuleId.EndpointRemoved,
                ruleProfile,
                $"Endpoint '{operation.Identity.Method} {operation.Identity.PathTemplate}' was removed.",
                operation.Identity);

            if (finding is not null)
            {
                findings.Add(finding);
            }
        }

        foreach (var operation in comparisonMap.UnmatchedNewOperations)
        {
            var finding = ApiRuleEvaluationHelpers.CreateOperationFinding(
                ApiRuleId.NewEndpoint,
                ruleProfile,
                $"Endpoint '{operation.Identity.Method} {operation.Identity.PathTemplate}' was added.",
                operation.Identity);

            if (finding is not null)
            {
                findings.Add(finding);
            }
        }
    }
}