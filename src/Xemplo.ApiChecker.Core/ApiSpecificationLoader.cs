using System.Text.Json.Nodes;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Reader;
using YamlDotNet.Serialization;

namespace Xemplo.ApiChecker.Core;

public sealed class ApiSpecificationLoader : IApiSpecificationLoader
{
    private readonly HttpClient _httpClient;

    public ApiSpecificationLoader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<ApiSpecificationLoadResult> LoadAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return ApiSpecificationLoadResult.Fail(
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.InvalidSource,
                    "Specification source must be provided.",
                    source));
        }

        var contentResult = await ReadSourceContentAsync(source, cancellationToken);
        if (contentResult.Failure is not null)
        {
            return ApiSpecificationLoadResult.Fail(contentResult.Failure);
        }

        var content = contentResult.Content!;

        try
        {
            var jsonNode = ParseAsJsonNode(content);
            var failures = new List<ApiSpecificationLoadFailure>();

            failures.AddRange(GetExternalReferenceFailures(jsonNode, source));

            var versionResult = TryGetSpecificationVersion(jsonNode);
            if (versionResult.Kind is not null)
            {
                failures.Add(
                    new ApiSpecificationLoadFailure(
                        versionResult.Kind.Value,
                        versionResult.Message!,
                        source));
            }

            var readerSettings = new OpenApiReaderSettings
            {
                LoadExternalRefs = false
            };
            var reader = new OpenApiJsonReader();
            var readResult = reader.Read(jsonNode, readerSettings);

            if (readResult.Diagnostic?.Errors.Count > 0)
            {
                failures.AddRange(readResult.Diagnostic.Errors.Select(error =>
                    new ApiSpecificationLoadFailure(
                        GetDiagnosticFailureKind(error.Message),
                        FormatDiagnosticFailureMessage(error.Message),
                        source)));
            }

            if (readResult.Document is null)
            {
                if (failures.Count == 0)
                {
                    failures.Add(
                        new ApiSpecificationLoadFailure(
                            ApiSpecificationLoadFailureKind.ParseFailed,
                            "Failed to parse specification: no OpenAPI document was produced.",
                            source));
                }

                return ApiSpecificationLoadResult.Fail(failures);
            }

            failures.AddRange(GetDuplicateOperationIdFailures(readResult.Document, source));

            if (failures.Count > 0)
            {
                return ApiSpecificationLoadResult.Fail(failures);
            }

            return ApiSpecificationLoadResult.Success(new ApiSpecificationDocument(readResult.Document, source));
        }
        catch (Exception exception)
        {
            return ApiSpecificationLoadResult.Fail(
                new ApiSpecificationLoadFailure(
                    GetDiagnosticFailureKind(exception.Message),
                    FormatDiagnosticFailureMessage(exception.Message),
                    source));
        }
    }

    private static JsonNode ParseAsJsonNode(string content)
    {
        var json = LooksLikeJson(content) ? content : ConvertYamlToJson(content);
        var jsonNode = JsonNode.Parse(json);
        if (jsonNode is null)
        {
            throw new InvalidOperationException("Specification content is empty after parsing.");
        }

        return jsonNode;
    }

    private static bool LooksLikeJson(string content)
    {
        foreach (var character in content)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return character == '{' || character == '[';
        }

        return false;
    }

    private static string ConvertYamlToJson(string content)
    {
        var deserializer = new DeserializerBuilder().Build();
        var yamlObject = deserializer.Deserialize(new StringReader(content));
        var serializer = new SerializerBuilder().JsonCompatible().Build();
        return serializer.Serialize(yamlObject);
    }

    private static (ApiSpecificationLoadFailureKind? Kind, string? Message) TryGetSpecificationVersion(JsonNode jsonNode)
    {
        if (jsonNode is not JsonObject root)
        {
            return (
                ApiSpecificationLoadFailureKind.ParseFailed,
                "Unable to determine OpenAPI version from the specification.");
        }

        if (root.TryGetPropertyValue("swagger", out var swaggerNode)
            && swaggerNode?.GetValue<string>() is { } swaggerVersion
            && swaggerVersion.StartsWith("2", StringComparison.Ordinal))
        {
            return (
                ApiSpecificationLoadFailureKind.UnsupportedSpecificationVersion,
                "Swagger/OpenAPI 2.0 is not supported. Only OpenAPI 3.0 and 3.1 are supported.");
        }

        if (!root.TryGetPropertyValue("openapi", out var openApiVersionNode)
            || openApiVersionNode?.GetValue<string>() is not { } version)
        {
            return (
                ApiSpecificationLoadFailureKind.ParseFailed,
                "Unable to determine OpenAPI version from the specification.");
        }

        if (!IsSupportedOpenApiVersion(version))
        {
            return (
                ApiSpecificationLoadFailureKind.UnsupportedSpecificationVersion,
                $"OpenAPI version '{version}' is not supported. Only OpenAPI 3.0 and 3.1 are supported.");
        }

        return (null, null);
    }

    private static bool IsSupportedOpenApiVersion(string version)
    {
        return version.StartsWith("3.0", StringComparison.Ordinal)
            || version.StartsWith("3.1", StringComparison.Ordinal);
    }

    private static ApiSpecificationLoadFailureKind GetDiagnosticFailureKind(string message)
    {
        if (message.Contains("specification version", StringComparison.OrdinalIgnoreCase)
            && message.Contains("not supported", StringComparison.OrdinalIgnoreCase))
        {
            return ApiSpecificationLoadFailureKind.UnsupportedSpecificationVersion;
        }

        return ApiSpecificationLoadFailureKind.ParseFailed;
    }

    private static string FormatDiagnosticFailureMessage(string message)
    {
        return GetDiagnosticFailureKind(message) switch
        {
            ApiSpecificationLoadFailureKind.UnsupportedSpecificationVersion => message,
            _ => $"Failed to parse specification: {message}"
        };
    }

    private static IReadOnlyList<ApiSpecificationLoadFailure> GetDuplicateOperationIdFailures(OpenApiDocument document, string source)
    {
        return document.Paths
            .SelectMany(static path => path.Value?.Operations?.Select(operation => new
            {
                OperationId = operation.Value?.OperationId,
                Method = ToHttpMethod(operation.Key),
                Path = path.Key
            }) ?? [])
            .Where(static operation => !string.IsNullOrWhiteSpace(operation.OperationId))
            .GroupBy(static operation => operation.OperationId!, StringComparer.Ordinal)
            .Where(static group => group.Skip(1).Any())
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(group => new ApiSpecificationLoadFailure(
                ApiSpecificationLoadFailureKind.DuplicateOperationId,
                $"Duplicate operationId '{group.Key}' is used by {string.Join(", ", group.OrderBy(static operation => operation.Path, StringComparer.OrdinalIgnoreCase).ThenBy(static operation => operation.Method, StringComparer.Ordinal).Select(static operation => $"{operation.Method} {operation.Path}"))}.",
                source,
                group.Key))
            .ToArray();
    }

    private static IReadOnlyList<ApiSpecificationLoadFailure> GetExternalReferenceFailures(JsonNode jsonNode, string source)
    {
        var failures = new List<ApiSpecificationLoadFailure>();
        CollectExternalReferenceFailures(jsonNode, source, "$", failures);
        return failures;
    }

    private static string ToHttpMethod(HttpMethod operationType)
    {
        return operationType.Method.ToUpperInvariant();
    }

    private static void CollectExternalReferenceFailures(
        JsonNode jsonNode,
        string source,
        string path,
        List<ApiSpecificationLoadFailure> failures)
    {
        if (jsonNode is JsonObject jsonObject)
        {
            foreach (var property in jsonObject)
            {
                var propertyPath = $"{path}[{FormatJsonPathSegment(property.Key)}]";

                if (property.Key.Equals("$ref", StringComparison.Ordinal)
                    && property.Value?.GetValue<string>() is { } reference
                    && !reference.StartsWith("#", StringComparison.Ordinal))
                {
                    failures.Add(
                        new ApiSpecificationLoadFailure(
                            ApiSpecificationLoadFailureKind.ExternalReferencesNotSupported,
                            $"External $ref target '{reference}' is not supported in v1 at {propertyPath}.",
                            source,
                            reference));
                }

                if (property.Value is not null)
                {
                    CollectExternalReferenceFailures(property.Value, source, propertyPath, failures);
                }
            }

            return;
        }

        if (jsonNode is JsonArray jsonArray)
        {
            for (var index = 0; index < jsonArray.Count; index++)
            {
                var child = jsonArray[index];
                if (child is not null)
                {
                    CollectExternalReferenceFailures(child, source, $"{path}[{index}]", failures);
                }
            }
        }
    }

    private static string FormatJsonPathSegment(string propertyName)
    {
        return $"'{propertyName.Replace("'", "\\'", StringComparison.Ordinal)}'";
    }

    private static bool ContainsExternalReference(JsonNode jsonNode)
    {
        if (jsonNode is JsonObject jsonObject)
        {
            foreach (var property in jsonObject)
            {
                if (property.Key.Equals("$ref", StringComparison.Ordinal)
                    && property.Value?.GetValue<string>() is { } reference
                    && !reference.StartsWith("#", StringComparison.Ordinal))
                {
                    return true;
                }

                if (property.Value is not null && ContainsExternalReference(property.Value))
                {
                    return true;
                }
            }

            return false;
        }

        if (jsonNode is JsonArray jsonArray)
        {
            foreach (var child in jsonArray)
            {
                if (child is not null && ContainsExternalReference(child))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<(string? Content, ApiSpecificationLoadFailure? Failure)> ReadSourceContentAsync(
        string source,
        CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || absoluteUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return await ReadHttpContentAsync(absoluteUri, source, cancellationToken);
            }

            if (absoluteUri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                return await ReadFileContentAsync(absoluteUri.LocalPath, source, cancellationToken);
            }
        }

        if (HasUnsupportedUriScheme(source))
        {
            return (
                null,
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.InvalidSource,
                    "Only local file paths and unauthenticated HTTP/HTTPS URLs are supported.",
                    source));
        }

        return await ReadFileContentAsync(source, source, cancellationToken);
    }

    private static async Task<(string? Content, ApiSpecificationLoadFailure? Failure)> ReadFileContentAsync(
        string filePath,
        string source,
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(filePath))
        {
            return (
                null,
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.SourceReadFailed,
                    "Specification source must be a file, not a directory.",
                    source));
        }

        if (!File.Exists(filePath))
        {
            return (
                null,
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.SourceNotFound,
                    "Specification file was not found.",
                    source));
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return (content, null);
        }
        catch (Exception exception)
        {
            return (
                null,
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.SourceReadFailed,
                    $"Failed to read specification file: {exception.Message}",
                    source));
        }
    }

    private async Task<(string? Content, ApiSpecificationLoadFailure? Failure)> ReadHttpContentAsync(
        Uri uri,
        string source,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (
                    null,
                    new ApiSpecificationLoadFailure(
                        ApiSpecificationLoadFailureKind.SourceFetchFailed,
                        $"Failed to fetch specification. Status code: {(int)response.StatusCode}.",
                        source));
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return (content, null);
        }
        catch (Exception exception)
        {
            return (
                null,
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.SourceFetchFailed,
                    $"Failed to fetch specification: {exception.Message}",
                    source));
        }
    }

    private static bool HasUnsupportedUriScheme(string source)
    {
        var separatorIndex = source.IndexOf("://", StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return false;
        }

        var scheme = source[..separatorIndex];
        return Uri.CheckSchemeName(scheme);
    }
}