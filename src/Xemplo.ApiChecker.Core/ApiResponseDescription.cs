using Microsoft.OpenApi;

namespace Xemplo.ApiChecker.Core;

public sealed record ApiResponseDescription(
    ApiOperationIdentity OperationIdentity,
    string StatusCode,
    string MediaType,
    IOpenApiResponse Response,
    OpenApiMediaType Media);