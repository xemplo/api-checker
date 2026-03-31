using Microsoft.OpenApi.Models;

namespace Xemplo.ApiChecker.Core;

public sealed class ApiSpecificationDocument
{
    public ApiSpecificationDocument(OpenApiDocument document, string? source = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Source = source;
    }

    public OpenApiDocument Document { get; }

    public string? Source { get; }
}