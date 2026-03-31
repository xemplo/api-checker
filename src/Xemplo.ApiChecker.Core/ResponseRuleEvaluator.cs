namespace Xemplo.ApiChecker.Core;

internal sealed class ResponseRuleEvaluator : IApiRuleEvaluator
{
    public ApiRuleFamily Family => ApiRuleFamily.Response;

    public void Evaluate(ApiComparisonMap comparisonMap, ApiRuleProfile ruleProfile, ICollection<ApiFinding> findings)
    {
        foreach (var operationMatch in comparisonMap.MatchedOperations)
        {
            EvaluateResponseCodeChanges(operationMatch, ruleProfile, findings);
        }
    }

    private static void EvaluateResponseCodeChanges(
        ApiOperationMatch operationMatch,
        ApiRuleProfile ruleProfile,
        ICollection<ApiFinding> findings)
    {
        var oldExplicitStatusCodes = operationMatch.OldOperation.Operation.Responses?
            .Keys
            .Where(static statusCode => !statusCode.Equals("default", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.Ordinal)
            ?? [];

        foreach (var responseGroup in operationMatch.UnmatchedNewResponses
                     .GroupBy(static response => response.StatusCode, StringComparer.Ordinal)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            if (responseGroup.Key.Equals("default", StringComparison.OrdinalIgnoreCase)
                || oldExplicitStatusCodes.Contains(responseGroup.Key))
            {
                continue;
            }

            var finding = ApiRuleEvaluationHelpers.CreateOperationFinding(
                ApiRuleId.NewResponseCode,
                ruleProfile,
                $"Response code '{responseGroup.Key}' was added.",
                operationMatch.NewOperation.Identity);

            if (finding is not null)
            {
                findings.Add(finding);
            }
        }
    }
}