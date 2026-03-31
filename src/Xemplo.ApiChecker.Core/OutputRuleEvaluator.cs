namespace Xemplo.ApiChecker.Core;

internal sealed class OutputRuleEvaluator : IApiRuleEvaluator
{
    private readonly IApiJsonSchemaAnalyzer _schemaAnalyzer;

    public OutputRuleEvaluator(IApiJsonSchemaAnalyzer schemaAnalyzer)
    {
        _schemaAnalyzer = schemaAnalyzer;
    }

    public ApiRuleFamily Family => ApiRuleFamily.Output;

    public void Evaluate(ApiComparisonMap comparisonMap, ApiRuleProfile ruleProfile, ICollection<ApiFinding> findings)
    {
        foreach (var operationMatch in comparisonMap.MatchedOperations)
        {
            EvaluateResponseBodyChanges(operationMatch, ruleProfile, findings);
        }
    }

    private void EvaluateResponseBodyChanges(
        ApiOperationMatch operationMatch,
        ApiRuleProfile ruleProfile,
        ICollection<ApiFinding> findings)
    {
        foreach (var responseMatch in operationMatch.MatchedResponses)
        {
            var oldAnalysis = _schemaAnalyzer.Analyze(responseMatch.OldResponse.Media.Schema, ApiSchemaContext.Response);
            var newAnalysis = _schemaAnalyzer.Analyze(responseMatch.NewResponse.Media.Schema, ApiSchemaContext.Response);
            var oldNodes = oldAnalysis.Nodes.ToDictionary(static node => node.SchemaPath, StringComparer.Ordinal);
            var newNodes = newAnalysis.Nodes.ToDictionary(static node => node.SchemaPath, StringComparer.Ordinal);

            foreach (var newNode in newAnalysis.Nodes)
            {
                if (!ShouldEvaluateResponseField(newNode))
                {
                    continue;
                }

                if (oldNodes.TryGetValue(newNode.SchemaPath, out var oldNode)
                    && oldNode.Usage != ApiSchemaUsage.Excluded)
                {
                    AddNewEnumOutputFindings(oldNode, newNode, ruleProfile, responseMatch.NewResponse.OperationIdentity, findings);
                    continue;
                }

                var finding = CreateAddedOutputFinding(newNode, ruleProfile, responseMatch.NewResponse.OperationIdentity);
                if (finding is not null)
                {
                    findings.Add(finding);
                }
            }

            foreach (var oldNode in oldAnalysis.Nodes)
            {
                if (!ShouldEvaluateResponseField(oldNode))
                {
                    continue;
                }

                if (!newNodes.TryGetValue(oldNode.SchemaPath, out var newNode))
                {
                    AddRemovedOutputFinding(oldNode, ruleProfile, responseMatch.NewResponse.OperationIdentity, findings);
                    continue;
                }

                if (newNode.Usage == ApiSchemaUsage.Excluded)
                {
                    AddRemovedOutputFinding(oldNode, ruleProfile, responseMatch.NewResponse.OperationIdentity, findings);
                }
            }
        }
    }

    private static bool ShouldEvaluateResponseField(ApiJsonSchemaNode node)
    {
        return node.PropertyName is not null
            && node.Usage == ApiSchemaUsage.Included;
    }

    private static ApiFinding? CreateAddedOutputFinding(
        ApiJsonSchemaNode node,
        ApiRuleProfile ruleProfile,
        ApiOperationIdentity operation)
    {
        if (node.Nullable == ApiSchemaCondition.Ambiguous
            || node.Usage == ApiSchemaUsage.Ambiguous)
        {
            return null;
        }

        if (node.Nullable == ApiSchemaCondition.True)
        {
            return ApiRuleEvaluationHelpers.CreateFinding(
                ApiRuleId.NewNullableOutput,
                ruleProfile,
                $"Response field '{node.SchemaPath}' was added as nullable output.",
                operation,
                node.SchemaPath);
        }

        if (node.Nullable == ApiSchemaCondition.False)
        {
            return ApiRuleEvaluationHelpers.CreateFinding(
                ApiRuleId.NewNonNullableOutput,
                ruleProfile,
                $"Response field '{node.SchemaPath}' was added as non-nullable output.",
                operation,
                node.SchemaPath);
        }

        return null;
    }

    private static void AddRemovedOutputFinding(
        ApiJsonSchemaNode node,
        ApiRuleProfile ruleProfile,
        ApiOperationIdentity operation,
        ICollection<ApiFinding> findings)
    {
        if (node.Usage == ApiSchemaUsage.Ambiguous)
        {
            return;
        }

        var finding = ApiRuleEvaluationHelpers.CreateFinding(
            ApiRuleId.RemovedOutput,
            ruleProfile,
            $"Response field '{node.SchemaPath}' was removed.",
            operation,
            node.SchemaPath);

        if (finding is not null)
        {
            findings.Add(finding);
        }
    }

    private static void AddNewEnumOutputFindings(
        ApiJsonSchemaNode oldNode,
        ApiJsonSchemaNode newNode,
        ApiRuleProfile ruleProfile,
        ApiOperationIdentity operation,
        ICollection<ApiFinding> findings)
    {
        if (oldNode.Usage != ApiSchemaUsage.Included
            || newNode.Usage != ApiSchemaUsage.Included)
        {
            return;
        }

        if (oldNode.EnumValues.Count == 0 || newNode.EnumValues.Count == 0)
        {
            return;
        }

        var oldEnumValues = oldNode.EnumValues.ToHashSet(StringComparer.Ordinal);
        foreach (var enumValue in newNode.EnumValues)
        {
            if (oldEnumValues.Contains(enumValue))
            {
                continue;
            }

            var finding = ApiRuleEvaluationHelpers.CreateFinding(
                ApiRuleId.NewEnumOutput,
                ruleProfile,
                $"Response field '{newNode.SchemaPath}' added enum value '{enumValue}'.",
                operation,
                newNode.SchemaPath);

            if (finding is not null)
            {
                findings.Add(finding);
            }
        }
    }
}