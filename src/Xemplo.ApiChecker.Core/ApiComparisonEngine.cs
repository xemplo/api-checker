namespace Xemplo.ApiChecker.Core;

public sealed class ApiComparisonEngine : IApiComparisonEngine
{
    private readonly IApiComparisonMapBuilder _mapBuilder;
    private readonly IApiJsonSchemaAnalyzer _schemaAnalyzer;

    public ApiComparisonEngine(IApiComparisonMapBuilder? mapBuilder = null, IApiJsonSchemaAnalyzer? schemaAnalyzer = null)
    {
        _mapBuilder = mapBuilder ?? new ApiComparisonMapBuilder();
        _schemaAnalyzer = schemaAnalyzer ?? new ApiJsonSchemaAnalyzer();
    }

    public ApiComparisonResult Compare(ApiComparisonInput input, ApiRuleProfile ruleProfile)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(ruleProfile);

        var comparisonMap = _mapBuilder.Build(input);
        var findings = new List<ApiFinding>();

        foreach (var operationMatch in comparisonMap.MatchedOperations)
        {
            EvaluateRequestBodyChanges(operationMatch, ruleProfile, findings);
            EvaluateQueryParameterChanges(operationMatch, ruleProfile, findings);
        }

        if (findings.Count == 0)
        {
            return ApiComparisonResult.Empty;
        }

        return new ApiComparisonResult(OrderFindings(findings));
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
            return CreateFinding(
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
            return CreateFinding(
                ApiRuleId.NewOptionalInput,
                ruleProfile,
                $"Request field '{node.SchemaPath}' was added as optional input.",
                operation,
                node.SchemaPath);
        }

        return null;
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
            return CreateFinding(
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
            return CreateFinding(
                ApiRuleId.NewRequiredQueryParam,
                ruleProfile,
                $"Query parameter '{parameter.Name}' was added as required input.",
                operation,
                parameter.SchemaPath);
        }

        if (parameter.Nullable == ApiSchemaCondition.True
            || parameter.HasDefault == ApiSchemaCondition.True)
        {
            return CreateFinding(
                ApiRuleId.NewOptionalQueryParam,
                ruleProfile,
                $"Query parameter '{parameter.Name}' was added as optional input.",
                operation,
                parameter.SchemaPath);
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

        var finding = CreateFinding(
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

    private static ApiFinding? CreateFinding(
        ApiRuleId ruleId,
        ApiRuleProfile ruleProfile,
        string message,
        ApiOperationIdentity operation,
        string schemaPath)
    {
        var severity = ruleProfile.GetSeverity(ruleId);
        return severity == ApiSeverity.Off
            ? null
            : new ApiFinding(ruleId, severity, message, operation, schemaPath);
    }

    private static IReadOnlyList<ApiFinding> OrderFindings(IEnumerable<ApiFinding> findings)
    {
        return findings.OrderBy(static finding => finding.Operation?.PathTemplate, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static finding => finding.Operation?.Method, StringComparer.Ordinal)
            .ThenBy(static finding => finding.SchemaPath, StringComparer.Ordinal)
            .ThenBy(static finding => finding.RuleId)
            .ThenBy(static finding => finding.Message, StringComparer.Ordinal)
            .ToArray();
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

            destination[parameter.Name] = AnalyzeQueryParameter(parameter);
        }
    }

    private QueryParameterAnalysis AnalyzeQueryParameter(Microsoft.OpenApi.Models.Interfaces.IOpenApiParameter parameter)
    {
        var schemaPath = $"$query.{parameter.Name}";
        var analysis = _schemaAnalyzer.Analyze(parameter.Schema, ApiSchemaContext.Request, schemaPath);
        var rootNode = analysis.Nodes.FirstOrDefault(static node => node.PropertyName is null);

        return new QueryParameterAnalysis(
            parameter.Name,
            parameter.Required,
            rootNode?.Usage ?? ApiSchemaUsage.Included,
            rootNode?.Nullable ?? ApiSchemaCondition.Ambiguous,
            rootNode?.HasDefault ?? ApiSchemaCondition.False,
            schemaPath);
    }

    private sealed record QueryParameterAnalysis(
        string Name,
        bool Required,
        ApiSchemaUsage Usage,
        ApiSchemaCondition Nullable,
        ApiSchemaCondition HasDefault,
        string SchemaPath);
}