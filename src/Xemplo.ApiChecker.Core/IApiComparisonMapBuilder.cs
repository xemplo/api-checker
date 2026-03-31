namespace Xemplo.ApiChecker.Core;

public interface IApiComparisonMapBuilder
{
    ApiComparisonMap Build(ApiComparisonInput input);
}