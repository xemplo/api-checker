using Microsoft.OpenApi;

namespace Xemplo.ApiChecker.Core;

public sealed record ApiOperationDescription(
    ApiOperationIdentity Identity,
    IOpenApiPathItem PathItem,
    OpenApiOperation Operation);