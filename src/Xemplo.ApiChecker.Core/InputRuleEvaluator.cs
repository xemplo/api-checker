namespace Xemplo.ApiChecker.Core;

internal sealed class InputRuleEvaluator : IApiRuleEvaluator
{
    private readonly IApiJsonSchemaAnalyzer _schemaAnalyzer;

    public InputRuleEvaluator(IApiJsonSchemaAnalyzer schemaAnalyzer)
    {
        _schemaAnalyzer = schemaAnalyzer;
    }

    public ApiRuleFamily Family => ApiRuleFamily.Input;

    public void Evaluate(ApiComparisonMap comparisonMap, ApiRuleProfile ruleProfile, ICollection<ApiFinding> findings)
    {
        foreach (var operationMatch in comparisonMap.MatchedOperations)
        {
            EvaluateRequestBodyChanges(operationMatch, ruleProfile, findings);
        }
    }

    private void EvaluateRequestBodyChanges(
        ApiOperationMatch operationMatch,
        ApiRuleProfile ruleProfile,
        ICollection<ApiFinding> findings)
    {
        var oldSchema = GetComparableRequestSchema(operationMatch.OldOperation.Operation);
        var newSchema = GetComparableRequestSchema(operationMatch.NewOperation.Operation);

        var oldAnalysis = _schemaAnalyzer.Analyze(oldSchema, ApiSchemaContext.Request);
        var newAnalysis = _schemaAnalyzer.Analyze(newSchema, ApiSchemaContext.Request);
        var oldNodes = oldAnalysis.Nodes.ToDictionary(static node => node.SchemaPath, StringComparer.Ordinal);
        var newNodes = newAnalysis.Nodes.ToDictionary(static node => node.SchemaPath, StringComparer.Ordinal);

        foreach (var newNode in newAnalysis.Nodes)
        {
            if (!ShouldEvaluateRequestField(newNode))
            {
                continue;
            }

            if (oldNodes.TryGetValue(newNode.SchemaPath, out var oldNode)
                && oldNode.Usage != ApiSchemaUsage.Excluded)
            {
                continue;
            }

            var finding = CreateAddedInputFinding(newNode, ruleProfile, operationMatch.NewOperation.Identity);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }

        foreach (var oldNode in oldAnalysis.Nodes)
        {
            if (!ShouldEvaluateRequestField(oldNode))
            {
                continue;
            }

            if (!newNodes.TryGetValue(oldNode.SchemaPath, out var newNode))
            {
                AddRemovedInputFinding(oldNode, ruleProfile, operationMatch.NewOperation.Identity, findings);
                continue;
            }

            if (newNode.Usage == ApiSchemaUsage.Excluded)
            {
                AddRemovedInputFinding(oldNode, ruleProfile, operationMatch.NewOperation.Identity, findings);
            }
        }
    }

    private static bool ShouldEvaluateRequestField(ApiJsonSchemaNode node)
    {
        return node.PropertyName is not null
            && node.Usage == ApiSchemaUsage.Included;
    }

    private static ApiFinding? CreateAddedInputFinding(
        ApiJsonSchemaNode node,
        ApiRuleProfile ruleProfile,
        ApiOperationIdentity operation)
    {
        if (node.Required == ApiSchemaCondition.Ambiguous
            || node.Nullable == ApiSchemaCondition.Ambiguous
            || node.Usage == ApiSchemaUsage.Ambiguous)
        {
            return null;
        }

        if (node.Required == ApiSchemaCondition.True
            && node.Nullable == ApiSchemaCondition.False
            && node.HasDefault == ApiSchemaCondition.False)
        {
            return ApiRuleEvaluationHelpers.CreateFinding(
                ApiRuleId.NewRequiredInput,
                ruleProfile,
                $"Request field '{node.SchemaPath}' was added as required input.",
                operation,
                node.SchemaPath);
        }

        if (node.Required == ApiSchemaCondition.False
            || node.Nullable == ApiSchemaCondition.True
            || node.HasDefault == ApiSchemaCondition.True)
        {
            return ApiRuleEvaluationHelpers.CreateFinding(
                ApiRuleId.NewOptionalInput,
                ruleProfile,
                $"Request field '{node.SchemaPath}' was added as optional input.",
                operation,
                node.SchemaPath);
        }

        return null;
    }

    private static void AddRemovedInputFinding(
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
            ApiRuleId.RemovedInput,
            ruleProfile,
            $"Request field '{node.SchemaPath}' was removed.",
            operation,
            node.SchemaPath);

        if (finding is not null)
        {
            findings.Add(finding);
        }
    }

    private static Microsoft.OpenApi.Models.Interfaces.IOpenApiSchema? GetComparableRequestSchema(Microsoft.OpenApi.Models.OpenApiOperation operation)
    {
        if (operation.RequestBody?.Content is null)
        {
            return null;
        }

        var jsonEntries = operation.RequestBody.Content
            .Where(static content => content.Value?.Schema is not null && IsJsonMediaType(content.Key))
            .OrderBy(static content => GetRequestBodyMediaTypePriority(content.Key))
            .ThenBy(static content => content.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (jsonEntries.Length == 0)
        {
            return null;
        }

        if (jsonEntries.Length == 1)
        {
            return jsonEntries[0].Value!.Schema;
        }

        if (jsonEntries[0].Key.Equals("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return jsonEntries[0].Value!.Schema;
        }

        var firstSchema = jsonEntries[0].Value!.Schema;
        return jsonEntries.All(entry => ReferenceEquals(entry.Value!.Schema, firstSchema))
            ? firstSchema
            : null;
    }

    private static int GetRequestBodyMediaTypePriority(string mediaType)
    {
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private static bool IsJsonMediaType(string mediaType)
    {
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || (mediaType.StartsWith("application/", StringComparison.OrdinalIgnoreCase)
                && mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
    }
}