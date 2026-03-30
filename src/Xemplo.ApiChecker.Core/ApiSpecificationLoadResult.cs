namespace Xemplo.ApiChecker.Core;

public sealed class ApiSpecificationLoadResult
{
    private ApiSpecificationLoadResult(ApiSpecificationDocument? specification, ApiSpecificationLoadFailure? failure)
    {
        Specification = specification;
        Failure = failure;
    }

    public ApiSpecificationDocument? Specification { get; }

    public ApiSpecificationLoadFailure? Failure { get; }

    public bool IsSuccess => Specification is not null && Failure is null;

    public static ApiSpecificationLoadResult Success(ApiSpecificationDocument specification)
    {
        return new ApiSpecificationLoadResult(specification, null);
    }

    public static ApiSpecificationLoadResult Fail(ApiSpecificationLoadFailure failure)
    {
        return new ApiSpecificationLoadResult(null, failure);
    }
}