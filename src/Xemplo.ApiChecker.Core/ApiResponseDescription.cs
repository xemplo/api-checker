using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Models.Interfaces;

namespace Xemplo.ApiChecker.Core;

public sealed record ApiResponseDescription(
    ApiOperationIdentity OperationIdentity,
    string StatusCode,
    string MediaType,
    IOpenApiResponse Response,
    OpenApiMediaType Media);