using Microsoft.OpenApi;

namespace Xemplo.ApiChecker.Core;

public interface IApiJsonSchemaAnalyzer
{
    ApiJsonSchemaAnalysis Analyze(IOpenApiSchema? schema, ApiSchemaContext context, string rootPath = "$");
}