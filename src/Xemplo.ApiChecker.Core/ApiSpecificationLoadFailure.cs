namespace Xemplo.ApiChecker.Core;

public sealed record ApiSpecificationLoadFailure(
    ApiSpecificationLoadFailureKind Kind,
    string Message,
    string Source);