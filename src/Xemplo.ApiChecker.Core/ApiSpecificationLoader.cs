using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Reader;
using YamlDotNet.Serialization;

namespace Xemplo.ApiChecker.Core;

public sealed class ApiSpecificationLoader : IApiSpecificationLoader
{
    private static readonly HttpClient SharedHttpClient = new();

    private readonly HttpClient _httpClient;

    public ApiSpecificationLoader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedHttpClient;
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
            var equivalentTemplatedPathFailure = MergeEquivalentTemplatedPaths(jsonNode, source);

            if (equivalentTemplatedPathFailure is not null)
            {
                failures.Add(equivalentTemplatedPathFailure);
            }
            failures.AddRange(GetExternalReferenceFailures(jsonNode, source));
            failures.AddRange(GetMissingInternalReferenceFailures(jsonNode, source));

            var versionResult = TryGetSpecificationVersion(jsonNode);
            if (versionResult.Kind is not null)
            {
                failures.Add(
                    new ApiSpecificationLoadFailure(
                        versionResult.Kind.Value,
                        versionResult.Message!,
                        source));
            }

            if (equivalentTemplatedPathFailure is not null)
            {
                return ApiSpecificationLoadResult.Fail(failures);
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

    private static ApiSpecificationLoadFailure? MergeEquivalentTemplatedPaths(JsonNode jsonNode, string source)
    {
        if (!TryGetPathsObject(jsonNode, out var pathsObject))
        {
            return null;
        }

        var groups = pathsObject
            .Select((path, index) => new EquivalentTemplatedPathEntry(
                index,
                path.Key,
                NormalizeTemplatedPath(path.Key),
                path.Value as JsonObject,
                GetPathParameterNames(path.Key)))
            .GroupBy(static path => path.Signature, StringComparer.Ordinal)
            .Where(static group => group.Skip(1).Any())
            .OrderBy(static group => group.First().Index)
            .ToArray();

        if (groups.Length == 0)
        {
            return null;
        }

        var rewrittenPaths = new JsonObject(pathsObject.Select(path => new KeyValuePair<string, JsonNode?>(path.Key, path.Value?.DeepClone())));

        foreach (var group in groups)
        {
            var paths = group.OrderBy(static path => path.Index).ToArray();
            var canonicalPath = paths
                .OrderBy(static path => path.RawPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static path => path.RawPath, StringComparer.Ordinal)
                .First();

            if (canonicalPath.PathItem is not JsonObject canonicalPathItem)
            {
                return CreateEquivalentTemplatedPathFailure(
                    group.Key,
                    paths,
                    $"Equivalent templated paths could not be merged because '{canonicalPath.RawPath}' is not a valid Path Item object.",
                    source);
            }

            var mergedPathItem = new JsonObject();
            var failure = AppendMergedOperations(
                canonicalPath.RawPath,
                group.Key,
                paths,
                canonicalPathItem,
                BuildPathParameterRenameMap(canonicalPath.PathParameterNames, canonicalPath.PathParameterNames),
                mergedPathItem,
                source);

            if (failure is not null)
            {
                return failure;
            }

            foreach (var path in paths.Where(path => path != canonicalPath))
            {
                if (path.PathItem is not JsonObject pathItem)
                {
                    return CreateEquivalentTemplatedPathFailure(
                        group.Key,
                        paths,
                        $"Equivalent templated paths could not be merged because '{path.RawPath}' is not a valid Path Item object.",
                        source);
                }

                failure = AppendMergedOperations(
                    path.RawPath,
                    group.Key,
                    paths,
                    pathItem,
                    BuildPathParameterRenameMap(path.PathParameterNames, canonicalPath.PathParameterNames),
                    mergedPathItem,
                    source);

                if (failure is not null)
                {
                    return failure;
                }

                rewrittenPaths.Remove(path.RawPath);
            }

            rewrittenPaths[canonicalPath.RawPath] = mergedPathItem;
        }

        pathsObject.Parent!.AsObject()["paths"] = rewrittenPaths;
        return null;
    }

    private static ApiSpecificationLoadFailure? AppendMergedOperations(
        string rawPath,
        string signature,
        IReadOnlyList<EquivalentTemplatedPathEntry> paths,
        JsonObject pathItem,
        IReadOnlyDictionary<string, string> renameMap,
        JsonObject mergedPathItem,
        string source)
    {
        foreach (var property in pathItem)
        {
            if (!IsHttpMethod(property.Key))
            {
                return CreateEquivalentTemplatedPathFailure(
                    signature,
                    paths,
                    $"Equivalent templated paths could not be merged because '{rawPath}' defines the path-level field '{property.Key}'. Move that metadata onto each operation instead.",
                    source);
            }

            if (property.Value is not JsonObject operation)
            {
                return CreateEquivalentTemplatedPathFailure(
                    signature,
                    paths,
                    $"Equivalent templated paths could not be merged because the '{property.Key.ToUpperInvariant()}' operation on '{rawPath}' is not a valid Operation object.",
                    source);
            }

            if (mergedPathItem.ContainsKey(property.Key))
            {
                return CreateEquivalentTemplatedPathFailure(
                    signature,
                    paths,
                    $"Equivalent templated paths cannot both define the HTTP method '{property.Key.ToUpperInvariant()}'.",
                    source);
            }

            if (renameMap.Count > 0 && ContainsReferencedParameters(operation))
            {
                return CreateEquivalentTemplatedPathFailure(
                    signature,
                    paths,
                    $"Equivalent templated paths could not be merged because the '{property.Key.ToUpperInvariant()}' operation on '{rawPath}' uses referenced path parameters that cannot be renamed safely.",
                    source);
            }

            var normalizedOperation = (JsonObject)operation.DeepClone();
            RenameParameterArray(normalizedOperation["parameters"] as JsonArray, renameMap);
            mergedPathItem[property.Key] = normalizedOperation;
        }

        return null;
    }

    private static void RenameParameterArray(JsonArray? parameters, IReadOnlyDictionary<string, string> renameMap)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var parameterNode in parameters)
        {
            if (parameterNode is not JsonObject parameter || parameter.ContainsKey("$ref"))
            {
                continue;
            }

            if (parameter.TryGetPropertyValue("in", out var inNode)
                && string.Equals(inNode?.GetValue<string>(), "path", StringComparison.Ordinal)
                && parameter.TryGetPropertyValue("name", out var nameNode)
                && nameNode?.GetValue<string>() is { } name
                && renameMap.TryGetValue(name, out var renamed)
                && !string.Equals(name, renamed, StringComparison.Ordinal))
            {
                parameter["name"] = renamed;
            }
        }
    }

    private static bool ContainsReferencedParameters(JsonObject operation)
    {
        return operation["parameters"] is JsonArray parameters
            && parameters.Any(parameter => parameter is JsonObject parameterObject && parameterObject.ContainsKey("$ref"));
    }

    private static IReadOnlyDictionary<string, string> BuildPathParameterRenameMap(
        IReadOnlyList<string> sourceParameterNames,
        IReadOnlyList<string> targetParameterNames)
    {
        if (sourceParameterNames.Count != targetParameterNames.Count)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var renameMap = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < sourceParameterNames.Count; index++)
        {
            if (!string.Equals(sourceParameterNames[index], targetParameterNames[index], StringComparison.Ordinal))
            {
                renameMap[sourceParameterNames[index]] = targetParameterNames[index];
            }
        }

        return renameMap;
    }

    private static ApiSpecificationLoadFailure CreateEquivalentTemplatedPathFailure(
        string signature,
        IReadOnlyList<EquivalentTemplatedPathEntry> paths,
        string reason,
        string source)
    {
        var operations = paths
            .SelectMany(static path => GetPathItemMethods(path.PathItem).Count == 0
                ? [path.RawPath]
                : GetPathItemMethods(path.PathItem).Select(method => $"{method} {path.RawPath}"))
            .ToArray();

        return new ApiSpecificationLoadFailure(
            ApiSpecificationLoadFailureKind.ParseFailed,
            $"Equivalent templated paths {string.Join(", ", operations)} all normalize to the same path signature '{signature}'. {reason}",
            source,
            signature);
    }

    private static bool TryGetPathsObject(JsonNode jsonNode, out JsonObject pathsObject)
    {
        pathsObject = null!;

        if (jsonNode is not JsonObject root
            || !root.TryGetPropertyValue("paths", out var pathsNode)
            || pathsNode is not JsonObject paths)
        {
            return false;
        }

        pathsObject = paths;
        return true;
    }

    private static IReadOnlyList<string> GetPathParameterNames(string path)
    {
        return Regex.Matches(path, "\\{([^}]+)\\}")
            .Select(static match => match.Groups[1].Value)
            .ToArray();
    }

    private static string NormalizeTemplatedPath(string path)
    {
        return Regex.Replace(path, "\\{[^}]+\\}", "{}");
    }

    private static IReadOnlyList<string> GetPathItemMethods(JsonObject? pathItem)
    {
        if (pathItem is null)
        {
            return [];
        }

        return pathItem
            .Where(property => IsHttpMethod(property.Key))
            .Select(property => property.Key.ToUpperInvariant())
            .OrderBy(static method => method, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsHttpMethod(string method)
    {
        return method is "get" or "put" or "post" or "delete" or "options" or "head" or "patch" or "trace";
    }

    private static IReadOnlyList<ApiSpecificationLoadFailure> GetExternalReferenceFailures(JsonNode jsonNode, string source)
    {
        var failures = new List<ApiSpecificationLoadFailure>();
        CollectExternalReferenceFailures(jsonNode, source, "$", failures);
        return failures;
    }

    private static IReadOnlyList<ApiSpecificationLoadFailure> GetMissingInternalReferenceFailures(JsonNode jsonNode, string source)
    {
        var failures = new List<ApiSpecificationLoadFailure>();
        CollectMissingInternalReferenceFailures(jsonNode, jsonNode, source, "$", failures);
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

    private static void CollectMissingInternalReferenceFailures(
        JsonNode rootNode,
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
                    && reference.StartsWith("#", StringComparison.Ordinal)
                    && !InternalReferenceExists(rootNode, reference))
                {
                    failures.Add(
                        new ApiSpecificationLoadFailure(
                            ApiSpecificationLoadFailureKind.MissingInternalReferenceTarget,
                            $"Internal $ref target '{reference}' does not exist at {propertyPath}.",
                            source,
                            reference));
                }

                if (property.Value is not null)
                {
                    CollectMissingInternalReferenceFailures(rootNode, property.Value, source, propertyPath, failures);
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
                    CollectMissingInternalReferenceFailures(rootNode, child, source, $"{path}[{index}]", failures);
                }
            }
        }
    }

    private static bool InternalReferenceExists(JsonNode rootNode, string reference)
    {
        if (reference.Equals("#", StringComparison.Ordinal))
        {
            return true;
        }

        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            return false;
        }

        JsonNode? currentNode = rootNode;
        foreach (var rawSegment in reference[2..].Split('/', StringSplitOptions.None))
        {
            var segment = DecodeJsonPointerSegment(rawSegment);

            currentNode = currentNode switch
            {
                JsonObject currentObject when currentObject.TryGetPropertyValue(segment, out var childNode) => childNode,
                JsonArray currentArray when int.TryParse(segment, out var index) && index >= 0 && index < currentArray.Count => currentArray[index],
                _ => null
            };

            if (currentNode is null)
            {
                return false;
            }
        }

        return true;
    }

    private static string DecodeJsonPointerSegment(string segment)
    {
        return segment.Replace("~1", "/", StringComparison.Ordinal)
            .Replace("~0", "~", StringComparison.Ordinal);
    }

    private static string FormatJsonPathSegment(string propertyName)
    {
        return $"'{propertyName.Replace("'", "\\'", StringComparison.Ordinal)}'";
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

    private sealed record EquivalentTemplatedPathEntry(
        int Index,
        string RawPath,
        string Signature,
        JsonObject? PathItem,
        IReadOnlyList<string> PathParameterNames);
}