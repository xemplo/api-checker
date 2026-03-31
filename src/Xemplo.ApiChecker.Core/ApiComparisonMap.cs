namespace Xemplo.ApiChecker.Core;

public sealed class ApiComparisonMap
{
    public ApiComparisonMap(
        IReadOnlyList<ApiOperationMatch> matchedOperations,
        IReadOnlyList<ApiOperationDescription> unmatchedOldOperations,
        IReadOnlyList<ApiOperationDescription> unmatchedNewOperations)
    {
        MatchedOperations = matchedOperations;
        UnmatchedOldOperations = unmatchedOldOperations;
        UnmatchedNewOperations = unmatchedNewOperations;
    }

    public IReadOnlyList<ApiOperationMatch> MatchedOperations { get; }

    public IReadOnlyList<ApiOperationDescription> UnmatchedOldOperations { get; }

    public IReadOnlyList<ApiOperationDescription> UnmatchedNewOperations { get; }
}