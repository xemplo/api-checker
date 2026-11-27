namespace Xemplo.ApiChecker.Core;

public sealed class ApiComparisonEngine : IApiComparisonEngine
{
    private readonly IApiComparisonMapBuilder _mapBuilder;
    private readonly IReadOnlyList<IApiRuleEvaluator> _ruleEvaluators;

    public ApiComparisonEngine(IApiComparisonMapBuilder? mapBuilder = null, IApiJsonSchemaAnalyzer? schemaAnalyzer = null)
    {
        _mapBuilder = mapBuilder ?? new ApiComparisonMapBuilder();
        var effectiveSchemaAnalyzer = schemaAnalyzer ?? new ApiJsonSchemaAnalyzer();
        _ruleEvaluators =
        [
            new InputRuleEvaluator(effectiveSchemaAnalyzer),
            new QueryRuleEvaluator(effectiveSchemaAnalyzer),
            new OutputRuleEvaluator(effectiveSchemaAnalyzer),
            new ResponseRuleEvaluator(),
            new EndpointRuleEvaluator()
        ];
    }

    public ApiComparisonResult Compare(ApiComparisonInput input, ApiRuleProfile ruleProfile)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(ruleProfile);

        var comparisonMap = _mapBuilder.Build(input);
        var findings = new List<ApiFinding>();

        foreach (var ruleEvaluator in _ruleEvaluators)
        {
            if (!ruleProfile.HasEnabledRules(ruleEvaluator.Family))
            {
                continue;
            }

            ruleEvaluator.Evaluate(comparisonMap, ruleProfile, findings);
        }

        if (findings.Count == 0 && ruleProfile.AcceptedChanges.Count == 0)
        {
            return ApiComparisonResult.Empty;
        }

        return ApplyAcceptedChanges(OrderFindings(findings), ruleProfile.AcceptedChanges);
    }

    private static ApiComparisonResult ApplyAcceptedChanges(
        IReadOnlyList<ApiFinding> findings,
        IReadOnlyList<ApiAcceptedChange> acceptedChanges)
    {
        var used = new HashSet<ApiAcceptedChange>();
        var marked = findings.Select(finding =>
        {
            var matches = acceptedChanges.Where(change => change.Matches(finding)).ToArray();
            used.UnionWith(matches);
            return matches.Length == 0 ? finding : finding with { AcceptedReason = matches[0].Reason };
        }).ToArray();

        return new ApiComparisonResult(marked, acceptedChanges.Where(change => !used.Contains(change)).ToArray());
    }

    private static IReadOnlyList<ApiFinding> OrderFindings(IEnumerable<ApiFinding> findings)
    {
        return findings.OrderBy(static finding => finding.Operation?.PathTemplate, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static finding => finding.Operation?.Method, StringComparer.Ordinal)
            .ThenBy(static finding => finding.StatusCode, StringComparer.Ordinal)
            .ThenBy(static finding => finding.SchemaPath, StringComparer.Ordinal)
            .ThenBy(static finding => finding.RuleId)
            .ThenBy(static finding => finding.Message, StringComparer.Ordinal)
            .ToArray();
    }
}