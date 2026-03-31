namespace Xemplo.ApiChecker.Core;

internal sealed class QueryRuleEvaluator : IApiRuleEvaluator
{
    private readonly IApiJsonSchemaAnalyzer _schemaAnalyzer;

    public QueryRuleEvaluator(IApiJsonSchemaAnalyzer schemaAnalyzer)
    {
        _schemaAnalyzer = schemaAnalyzer;
    }

    public ApiRuleFamily Family => ApiRuleFamily.Query;

    public void Evaluate(ApiComparisonMap comparisonMap, ApiRuleProfile ruleProfile, ICollection<ApiFinding> findings)
    {
        foreach (var operationMatch in comparisonMap.MatchedOperations)
        {
            EvaluateQueryParameterChanges(operationMatch, ruleProfile, findings);
        }
    }

    private void EvaluateQueryParameterChanges(
        ApiOperationMatch operationMatch,
        ApiRuleProfile ruleProfile,
        ICollection<ApiFinding> findings)
    {
        var oldParameters = GetEffectiveQueryParameters(operationMatch.OldOperation);
        var newParameters = GetEffectiveQueryParameters(operationMatch.NewOperation);

        foreach (var parameter in newParameters.Values.OrderBy(static parameter => parameter.Name, StringComparer.Ordinal))
        {
            if (oldParameters.TryGetValue(parameter.Name, out var oldParameter)
                && oldParameter.Usage != ApiSchemaUsage.Excluded)
            {
                continue;
            }

            var finding = CreateAddedQueryParameterFinding(parameter, ruleProfile, operationMatch.NewOperation.Identity);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }
    }

    private IReadOnlyDictionary<string, QueryParameterAnalysis> GetEffectiveQueryParameters(ApiOperationDescription operation)
    {
        var parameters = new Dictionary<string, QueryParameterAnalysis>(StringComparer.Ordinal);

        AddQueryParameters(parameters, operation.PathItem.Parameters);
        AddQueryParameters(parameters, operation.Operation.Parameters);

        return parameters;
    }

    private void AddQueryParameters(
        IDictionary<string, QueryParameterAnalysis> destination,
        IEnumerable<Microsoft.OpenApi.Models.Interfaces.IOpenApiParameter>? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            if (parameter is null
                || parameter.In != Microsoft.OpenApi.Models.ParameterLocation.Query
                || string.IsNullOrWhiteSpace(parameter.Name))
            {
                continue;
            }

            var parameterName = parameter.Name;
            destination[parameterName] = AnalyzeQueryParameter(parameter, parameterName);
        }
    }

    private QueryParameterAnalysis AnalyzeQueryParameter(
        Microsoft.OpenApi.Models.Interfaces.IOpenApiParameter parameter,
        string parameterName)
    {
        var schemaPath = $"$query.{parameterName}";
        var analysis = _schemaAnalyzer.Analyze(parameter.Schema, ApiSchemaContext.Request, schemaPath);
        var rootNode = analysis.Nodes.FirstOrDefault(static node => node.PropertyName is null);

        return new QueryParameterAnalysis(
            parameterName,
            parameter.Required,
            rootNode?.Usage ?? ApiSchemaUsage.Included,
            rootNode?.Nullable ?? ApiSchemaCondition.Ambiguous,
            rootNode?.HasDefault ?? ApiSchemaCondition.False,
            schemaPath);
    }

    private static ApiFinding? CreateAddedQueryParameterFinding(
        QueryParameterAnalysis parameter,
        ApiRuleProfile ruleProfile,
        ApiOperationIdentity operation)
    {
        if (parameter.Usage == ApiSchemaUsage.Excluded
            || parameter.Usage == ApiSchemaUsage.Ambiguous)
        {
            return null;
        }

        if (parameter.Required == false)
        {
            return ApiRuleEvaluationHelpers.CreateFinding(
                ApiRuleId.NewOptionalQueryParam,
                ruleProfile,
                $"Query parameter '{parameter.Name}' was added as optional input.",
                operation,
                parameter.SchemaPath);
        }

        if (parameter.Nullable == ApiSchemaCondition.Ambiguous)
        {
            return null;
        }

        if (parameter.Nullable == ApiSchemaCondition.False
            && parameter.HasDefault == ApiSchemaCondition.False)
        {
            return ApiRuleEvaluationHelpers.CreateFinding(
                ApiRuleId.NewRequiredQueryParam,
                ruleProfile,
                $"Query parameter '{parameter.Name}' was added as required input.",
                operation,
                parameter.SchemaPath);
        }

        if (parameter.Nullable == ApiSchemaCondition.True
            || parameter.HasDefault == ApiSchemaCondition.True)
        {
            return ApiRuleEvaluationHelpers.CreateFinding(
                ApiRuleId.NewOptionalQueryParam,
                ruleProfile,
                $"Query parameter '{parameter.Name}' was added as optional input.",
                operation,
                parameter.SchemaPath);
        }

        return null;
    }

    private sealed record QueryParameterAnalysis(
        string Name,
        bool Required,
        ApiSchemaUsage Usage,
        ApiSchemaCondition Nullable,
        ApiSchemaCondition HasDefault,
        string SchemaPath);
}