namespace Xemplo.ApiChecker.Core;

/// <summary>
/// A known, agreed finding that is still reported but never fails a comparison.
/// Null filters (or <c>*</c> for operation and status) match anything.
/// </summary>
public sealed record ApiAcceptedChange(
    ApiRuleId RuleId,
    string Reason,
    string? Operation = null,
    string? StatusCode = null,
    IReadOnlyList<string>? SchemaPaths = null)
{
    public bool Matches(ApiFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return finding.RuleId == RuleId
            && OperationMatches(finding.Operation)
            && (StatusCode is null or "*" || string.Equals(StatusCode, finding.StatusCode, StringComparison.OrdinalIgnoreCase))
            && (SchemaPaths is null || SchemaPaths.Contains(finding.SchemaPath, StringComparer.Ordinal));
    }

    private bool OperationMatches(ApiOperationIdentity? operation)
    {
        if (Operation is null or "*")
        {
            return true;
        }

        var parts = Operation.Split(' ');
        return operation is not null
            && parts[0].Equals(operation.Method, StringComparison.OrdinalIgnoreCase)
            && parts[1].Equals(operation.PathTemplate, StringComparison.Ordinal);
    }
}
