using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Models.Interfaces;

namespace Xemplo.ApiChecker.Core;

public sealed record ApiOperationDescription(
    ApiOperationIdentity Identity,
    IOpenApiPathItem PathItem,
    OpenApiOperation Operation);