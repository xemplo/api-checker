namespace Xemplo.ApiChecker.Core;

public interface IApiSpecificationLoader
{
    Task<ApiSpecificationLoadResult> LoadAsync(string source, CancellationToken cancellationToken = default);
}