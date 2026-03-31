namespace Xemplo.ApiChecker.Core;

internal sealed class EndpointRuleEvaluator : IApiRuleEvaluator
{
    public ApiRuleFamily Family => ApiRuleFamily.Endpoint;

    public void Evaluate(ApiComparisonMap comparisonMap, ApiRuleProfile ruleProfile, ICollection<ApiFinding> findings)
    {
        foreach (var operationMatch in comparisonMap.MatchedOperations)
        {
            AddUpdatedEndpointIdFinding(operationMatch, ruleProfile, findings);
        }

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

    private static void AddUpdatedEndpointIdFinding(
        ApiOperationMatch operationMatch,
        ApiRuleProfile ruleProfile,
        ICollection<ApiFinding> findings)
    {
        var oldOperationId = operationMatch.OldOperation.Operation.OperationId;
        var newOperationId = operationMatch.NewOperation.Operation.OperationId;

        if (string.IsNullOrWhiteSpace(oldOperationId)
            || string.IsNullOrWhiteSpace(newOperationId)
            || string.Equals(oldOperationId, newOperationId, StringComparison.Ordinal))
        {
            return;
        }

        var finding = ApiRuleEvaluationHelpers.CreateOperationFinding(
            ApiRuleId.UpdatedEndpointId,
            ruleProfile,
            $"Endpoint operationId changed from '{oldOperationId}' to '{newOperationId}'.",
            operationMatch.NewOperation.Identity);

        if (finding is not null)
        {
            findings.Add(finding);
        }
    }
}