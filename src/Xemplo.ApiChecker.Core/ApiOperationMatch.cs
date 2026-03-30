namespace Xemplo.ApiChecker.Core;

public sealed class ApiOperationMatch
{
    public ApiOperationMatch(
        ApiOperationDescription oldOperation,
        ApiOperationDescription newOperation,
        IReadOnlyList<ApiResponseMatch> matchedResponses,
        IReadOnlyList<ApiResponseDescription> unmatchedOldResponses,
        IReadOnlyList<ApiResponseDescription> unmatchedNewResponses)
    {
        OldOperation = oldOperation;
        NewOperation = newOperation;
        MatchedResponses = matchedResponses;
        UnmatchedOldResponses = unmatchedOldResponses;
        UnmatchedNewResponses = unmatchedNewResponses;
    }

    public ApiOperationDescription OldOperation { get; }

    public ApiOperationDescription NewOperation { get; }

    public IReadOnlyList<ApiResponseMatch> MatchedResponses { get; }

    public IReadOnlyList<ApiResponseDescription> UnmatchedOldResponses { get; }

    public IReadOnlyList<ApiResponseDescription> UnmatchedNewResponses { get; }
}