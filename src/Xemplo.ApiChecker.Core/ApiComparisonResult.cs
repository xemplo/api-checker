namespace Xemplo.ApiChecker.Core;

public sealed record ApiComparisonResult
{
    public static ApiComparisonResult Empty { get; } = new(Array.Empty<ApiFinding>());

    public ApiComparisonResult(IReadOnlyList<ApiFinding> findings, IReadOnlyList<ApiAcceptedChange>? unusedAcceptedChanges = null)
    {
        Findings = findings;
        UnusedAcceptedChanges = unusedAcceptedChanges ?? [];
    }

    public IReadOnlyList<ApiFinding> Findings { get; }

    /// <summary>Accepted changes that matched no finding, and can likely be removed from configuration.</summary>
    public IReadOnlyList<ApiAcceptedChange> UnusedAcceptedChanges { get; }

    public bool HasErrorFindings => Findings.Any(finding => finding.Severity == ApiSeverity.Error && !finding.IsAccepted);
}
