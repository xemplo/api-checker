namespace Xemplo.ApiChecker.Core;

public sealed class ApiSpecificationLoadResult
{
    private ApiSpecificationLoadResult(ApiSpecificationDocument? specification, IReadOnlyList<ApiSpecificationLoadFailure> failures)
    {
        Specification = specification;
        Failures = failures;
    }

    public ApiSpecificationDocument? Specification { get; }

    public IReadOnlyList<ApiSpecificationLoadFailure> Failures { get; }

    public ApiSpecificationLoadFailure? Failure => Failures.FirstOrDefault();

    public bool IsSuccess => Specification is not null && Failures.Count == 0;

    public static ApiSpecificationLoadResult Success(ApiSpecificationDocument specification)
    {
        return new ApiSpecificationLoadResult(specification, Array.Empty<ApiSpecificationLoadFailure>());
    }

    public static ApiSpecificationLoadResult Fail(ApiSpecificationLoadFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new ApiSpecificationLoadResult(null, [failure]);
    }

    public static ApiSpecificationLoadResult Fail(IEnumerable<ApiSpecificationLoadFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        var failureList = failures.ToArray();
        if (failureList.Length == 0)
        {
            throw new ArgumentException("At least one failure must be provided.", nameof(failures));
        }

        return new ApiSpecificationLoadResult(null, failureList);
    }
}