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
            && FilterMatches(Operation, finding.Operation is null ? null : $"{finding.Operation.Method} {finding.Operation.PathTemplate}")
            && FilterMatches(StatusCode, finding.StatusCode)
            && (SchemaPaths is null || SchemaPaths.Contains(finding.SchemaPath, StringComparer.Ordinal));
    }

    private static bool FilterMatches(string? filter, string? value)
    {
        return filter is null or "*" || string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);
    }
}
