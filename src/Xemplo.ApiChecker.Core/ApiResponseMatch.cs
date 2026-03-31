namespace Xemplo.ApiChecker.Core;

public sealed record ApiResponseMatch(
    ApiResponseDescription OldResponse,
    ApiResponseDescription NewResponse,
    ApiResponseMatchKind MatchKind);