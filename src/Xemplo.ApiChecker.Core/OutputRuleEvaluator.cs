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
            var response = responseMatch.NewResponse.StatusCode.Equals("default", StringComparison.OrdinalIgnoreCase)
                ? responseMatch.NewResponse with { StatusCode = responseMatch.OldResponse.StatusCode }
                : responseMatch.NewResponse;
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
                    AddUpdatedOutputFinding(oldNode, newNode, ruleProfile, response, findings);
                    AddNewEnumOutputFindings(oldNode, newNode, ruleProfile, response, findings);
                    continue;
                }

                var finding = CreateAddedOutputFinding(newNode, ruleProfile, response);
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
                    AddRemovedOutputFinding(oldNode, ruleProfile, response, findings);
                    continue;
                }

                if (newNode.Usage == ApiSchemaUsage.Excluded)
                {
                    AddRemovedOutputFinding(oldNode, ruleProfile, response, findings);
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
        ApiResponseDescription response)
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
                response.OperationIdentity,
                node.SchemaPath,
                response.StatusCode);
        }

        if (node.Nullable == ApiSchemaCondition.False)
        {
            return ApiRuleEvaluationHelpers.CreateFinding(
                ApiRuleId.NewNonNullableOutput,
                ruleProfile,
                $"Response field '{node.SchemaPath}' was added as non-nullable output.",
                response.OperationIdentity,
                node.SchemaPath,
                response.StatusCode);
        }

        return null;
    }

    private static void AddRemovedOutputFinding(
        ApiJsonSchemaNode node,
        ApiRuleProfile ruleProfile,
        ApiResponseDescription response,
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
            response.OperationIdentity,
            node.SchemaPath,
            response.StatusCode);

        if (finding is not null)
        {
            findings.Add(finding);
        }
    }

    private static void AddNewEnumOutputFindings(
        ApiJsonSchemaNode oldNode,
        ApiJsonSchemaNode newNode,
        ApiRuleProfile ruleProfile,
        ApiResponseDescription response,
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
                response.OperationIdentity,
                newNode.SchemaPath,
                response.StatusCode);

            if (finding is not null)
            {
                findings.Add(finding);
            }
        }
    }

    private static void AddUpdatedOutputFinding(
        ApiJsonSchemaNode oldNode,
        ApiJsonSchemaNode newNode,
        ApiRuleProfile ruleProfile,
        ApiResponseDescription response,
        ICollection<ApiFinding> findings)
    {
        if (oldNode.Usage != ApiSchemaUsage.Included
            || newNode.Usage != ApiSchemaUsage.Included
            || oldNode.Nullable == ApiSchemaCondition.Ambiguous
            || newNode.Nullable == ApiSchemaCondition.Ambiguous
            || oldNode.Nullable == newNode.Nullable)
        {
            return;
        }

        var finding = newNode.Nullable switch
        {
            ApiSchemaCondition.True => ApiRuleEvaluationHelpers.CreateFinding(
                ApiRuleId.UpdatedNullableOutput,
                ruleProfile,
                $"Response field '{newNode.SchemaPath}' changed from non-nullable output to nullable output.",
                response.OperationIdentity,
                newNode.SchemaPath,
                response.StatusCode),
            ApiSchemaCondition.False => ApiRuleEvaluationHelpers.CreateFinding(
                ApiRuleId.UpdatedNonNullableOutput,
                ruleProfile,
                $"Response field '{newNode.SchemaPath}' changed from nullable output to non-nullable output.",
                response.OperationIdentity,
                newNode.SchemaPath,
                response.StatusCode),
            _ => null
        };

        if (finding is not null)
        {
            findings.Add(finding);
        }
    }
}