namespace Xemplo.ApiChecker.Core;

public sealed record ApiComparisonResult
{
    public static ApiComparisonResult Empty { get; } = new(Array.Empty<ApiFinding>());

    public ApiComparisonResult(IReadOnlyList<ApiFinding> findings)
    {
        Findings = findings;
    }

    public IReadOnlyList<ApiFinding> Findings { get; }

    public bool HasErrorFindings => Findings.Any(finding => finding.Severity == ApiSeverity.Error);
}