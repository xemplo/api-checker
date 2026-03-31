using Microsoft.OpenApi.Models;

namespace Xemplo.ApiChecker.Core;

public sealed record ApiComparisonInput(ApiSpecificationDocument OldSpecification, ApiSpecificationDocument NewSpecification)
{
    public static ApiComparisonInput Empty { get; } = new(
        new ApiSpecificationDocument(new OpenApiDocument()),
        new ApiSpecificationDocument(new OpenApiDocument()));
}