using System.Net;
using System.Net.Http;
using Microsoft.OpenApi.Models;
using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Core.Tests;

public class ApiSpecificationLoaderTests
{
    [Fact]
    public async Task LoadAsync_LocalFileWithOpenApi30_ReturnsSpecification()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, OpenApi30Json);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Specification);
            Assert.Null(result.Failure);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_LocalFileWithMinifiedOpenApi30Json_ReturnsSpecification()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, MinifiedOpenApi30Json);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Specification);
            Assert.Null(result.Failure);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_FileUriWithOpenApi30_ReturnsSpecification()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, OpenApi30Json);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(new Uri(path).AbsoluteUri);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Specification);
            Assert.Null(result.Failure);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_HttpWithOpenApi31_ReturnsSpecification()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OpenApi31Yaml)
            }));
        var loader = new ApiSpecificationLoader(httpClient);

        var result = await loader.LoadAsync("https://example.com/openapi.yaml");

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.NotNull(result.Specification);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsSourceNotFound()
    {
        var loader = new ApiSpecificationLoader();

        var result = await loader.LoadAsync($"{Guid.NewGuid():N}.yaml");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(ApiSpecificationLoadFailureKind.SourceNotFound, result.Failure!.Kind);
    }

    [Fact]
    public async Task LoadAsync_BlankSource_ReturnsInvalidSource()
    {
        var loader = new ApiSpecificationLoader();

        var result = await loader.LoadAsync("   ");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(ApiSpecificationLoadFailureKind.InvalidSource, result.Failure!.Kind);
    }

    [Fact]
    public async Task LoadAsync_DirectoryPath_ReturnsSourceReadFailed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"api-checker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(ApiSpecificationLoadFailureKind.SourceReadFailed, result.Failure!.Kind);
        }
        finally
        {
            Directory.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_HttpFailure_ReturnsSourceFetchFailed()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)));
        var loader = new ApiSpecificationLoader(httpClient);

        var result = await loader.LoadAsync("https://example.com/missing.yaml");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(ApiSpecificationLoadFailureKind.SourceFetchFailed, result.Failure!.Kind);
    }

    [Fact]
    public async Task LoadAsync_HttpException_ReturnsSourceFetchFailed()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("boom")));
        var loader = new ApiSpecificationLoader(httpClient);

        var result = await loader.LoadAsync("https://example.com/error.yaml");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(ApiSpecificationLoadFailureKind.SourceFetchFailed, result.Failure!.Kind);
    }

    [Fact]
    public async Task LoadAsync_MalformedDocument_ReturnsParseFailed()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, MalformedOpenApi);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(ApiSpecificationLoadFailureKind.ParseFailed, result.Failure!.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_NullJsonLiteral_ReturnsParseFailed()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "null");

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(ApiSpecificationLoadFailureKind.ParseFailed, result.Failure!.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_InvalidTopLevelShape_ReturnsParseFailed()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, InvalidTopLevelJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(ApiSpecificationLoadFailureKind.ParseFailed, result.Failure!.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_MissingOpenApiVersion_ReturnsParseFailed()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, MissingOpenApiVersionJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(ApiSpecificationLoadFailureKind.ParseFailed, result.Failure!.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_Swagger2Document_ReturnsUnsupportedVersion()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, Swagger2Yaml);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(ApiSpecificationLoadFailureKind.UnsupportedSpecificationVersion, result.Failure!.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_OpenApi32Document_ReturnsUnsupportedVersion()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, UnsupportedOpenApiYaml);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Failures, failure => failure.Kind == ApiSpecificationLoadFailureKind.UnsupportedSpecificationVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_ExternalReference_ReturnsExternalReferenceFailure()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, ExternalReferenceYaml);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(ApiSpecificationLoadFailureKind.ExternalReferencesNotSupported, result.Failure!.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_ExternalReferenceInMinifiedJson_ReturnsExternalReferenceFailure()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, MinifiedExternalReferenceJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Failure);
            Assert.Equal(ApiSpecificationLoadFailureKind.ExternalReferencesNotSupported, result.Failure!.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_InternalReference_IsAllowed()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, InternalReferenceJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.True(result.IsSuccess, result.Failure?.Message);
            Assert.NotNull(result.Specification);
            Assert.Null(result.Failure);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_MissingInternalReference_ReturnsMissingInternalReferenceFailure()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, MissingInternalReferenceJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Failures, failure => failure.Kind == ApiSpecificationLoadFailureKind.MissingInternalReferenceTarget);
            var failure = Assert.Single(result.Failures, failure => failure.Kind == ApiSpecificationLoadFailureKind.MissingInternalReferenceTarget);
            Assert.Contains("#/components/schemas/MissingPet", failure.Message);
            Assert.Equal("#/components/schemas/MissingPet", failure.HighlightedSubject);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_DuplicateOperationIds_ReturnsDuplicateOperationIdFailure()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, DuplicateOperationIdJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.Single(result.Failures);
            Assert.Equal(ApiSpecificationLoadFailureKind.DuplicateOperationId, result.Failures[0].Kind);
            Assert.Contains("listPets", result.Failures[0].Message);
            Assert.Contains("GET /pets", result.Failures[0].Message);
            Assert.Contains("POST /pets/search", result.Failures[0].Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_SameTemplatedPathWithDifferentMethods_ReturnsSpecification()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, SameTemplatedPathDifferentMethodsJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Failures.Select(static failure => failure.Message)));
            Assert.NotNull(result.Specification);
            Assert.Empty(result.Failures);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_EquivalentTemplatedPathsWithDistinctMethods_ReturnsMergedSpecification()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, EquivalentTemplatedPathsJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Failures.Select(static failure => failure.Message)));
            Assert.NotNull(result.Specification);
            var specification = result.Specification!;
            var pathItem = Assert.Single(specification.Document.Paths);
            Assert.Matches("^/api/v1/instances/\\{[^}]+\\}$", pathItem.Key);

            var canonicalParameterName = pathItem.Key[(pathItem.Key.LastIndexOf('{') + 1)..^1];
            Assert.NotNull(pathItem.Value);
            Assert.NotNull(pathItem.Value!.Operations);
            var operations = pathItem.Value.Operations!;

            Assert.True(operations.ContainsKey(System.Net.Http.HttpMethod.Get));
            Assert.True(operations.ContainsKey(System.Net.Http.HttpMethod.Put));
            Assert.True(operations.ContainsKey(System.Net.Http.HttpMethod.Delete));

            var getOperation = operations[System.Net.Http.HttpMethod.Get];
            var putOperation = operations[System.Net.Http.HttpMethod.Put];
            Assert.NotNull(getOperation);
            Assert.NotNull(putOperation);
            Assert.Contains(getOperation!.Parameters ?? [], parameter => parameter.Name == canonicalParameterName);
            Assert.Contains(putOperation!.Parameters ?? [], parameter => parameter.Name == canonicalParameterName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_EquivalentTemplatedPathsWithConflictingMethods_ReturnsParseFailure()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, EquivalentTemplatedPathsConflictingMethodsJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            var failure = Assert.Single(result.Failures);
            Assert.Equal(ApiSpecificationLoadFailureKind.ParseFailed, failure.Kind);
            Assert.Contains("GET /api/v1/instances/{id}", failure.Message);
            Assert.Contains("GET /api/v1/instances/{idOrCode}", failure.Message);
            Assert.Contains("cannot both define the HTTP method 'GET'", failure.Message);
            Assert.Equal("/api/v1/instances/{}", failure.HighlightedSubject);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_EquivalentTemplatedPathsWithConflictingNonMethodFields_ReturnsParseFailure()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, EquivalentTemplatedPathsConflictingMetadataJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            var failure = Assert.Single(result.Failures);
            Assert.Equal(ApiSpecificationLoadFailureKind.ParseFailed, failure.Kind);
            Assert.Contains("conflicting values for the non-method field 'summary'", failure.Message);
            Assert.Equal("/api/v1/instances/{}", failure.HighlightedSubject);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_EquivalentTemplatedPaths_DeduplicatesPromotedReferencedPathParameters()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, EquivalentTemplatedPathsWithReferencedPathParametersJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Failures.Select(static failure => failure.Message)));
            Assert.NotNull(result.Specification);
            var specification = result.Specification!;
            var pathItem = Assert.Single(specification.Document.Paths);
            Assert.NotNull(pathItem.Value);
            Assert.NotNull(pathItem.Value!.Operations);

            var getOperation = pathItem.Value.Operations![System.Net.Http.HttpMethod.Get];
            Assert.NotNull(getOperation);
            var pathParameters = (getOperation!.Parameters ?? []).Where(static parameter => parameter.In == Microsoft.OpenApi.Models.ParameterLocation.Path).ToArray();
            Assert.Single(pathParameters);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_WithMultipleValidationIssues_ReturnsAllFailures()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, MultiValidationIssueJson);

        try
        {
            var loader = new ApiSpecificationLoader();

            var result = await loader.LoadAsync(path);

            Assert.False(result.IsSuccess);
            Assert.Equal(3, result.Failures.Count);
            Assert.Contains(result.Failures, failure => failure.Kind == ApiSpecificationLoadFailureKind.ExternalReferencesNotSupported);
            Assert.Contains(result.Failures, failure => failure.Kind == ApiSpecificationLoadFailureKind.DuplicateOperationId);
            Assert.Contains(result.Failures, failure => failure.Kind == ApiSpecificationLoadFailureKind.MissingInternalReferenceTarget);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_UnsupportedScheme_ReturnsInvalidSource()
    {
        var loader = new ApiSpecificationLoader();

        var result = await loader.LoadAsync("ftp://example.com/openapi.yaml");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(ApiSpecificationLoadFailureKind.InvalidSource, result.Failure!.Kind);
    }

    [Fact]
    public void ComparisonInput_CanAcceptResolvedDocuments()
    {
        var oldSpecification = new ApiSpecificationDocument(new OpenApiDocument(), "old");
        var newSpecification = new ApiSpecificationDocument(new OpenApiDocument(), "new");

        var input = new ApiComparisonInput(oldSpecification, newSpecification);

        Assert.Same(oldSpecification, input.OldSpecification);
        Assert.Same(newSpecification, input.NewSpecification);
    }

    private const string OpenApi30Json = """
        {
          "openapi": "3.0.3",
          "info": {
            "title": "Pets",
            "version": "1.0.0"
          },
          "paths": {}
        }
        """;

    private const string MinifiedOpenApi30Json = "{" +
        "\"openapi\":\"3.0.3\"," +
        "\"info\":{\"title\":\"Pets\",\"version\":\"1.0.0\"}," +
        "\"paths\":{}" +
        "}";

    private const string OpenApi31Yaml = """
        openapi: 3.1.0
        info:
          title: Pets
          version: 1.0.0
        paths: {}
        """;

    private const string MalformedOpenApi = """
        openapi: 3.0.3
        info:
          title: Pets
          version: [
        paths: {}
        """;

    private const string InvalidTopLevelJson = """
                []
                """;

    private const string DuplicateOperationIdJson = "{" +
        "\"openapi\":\"3.0.3\"," +
        "\"info\":{\"title\":\"Pets\",\"version\":\"1.0.0\"}," +
        "\"paths\":{\"/pets\":{\"get\":{\"operationId\":\"listPets\",\"responses\":{\"200\":{\"description\":\"ok\"}}}},\"/pets/search\":{\"post\":{\"operationId\":\"listPets\",\"responses\":{\"200\":{\"description\":\"ok\"}}}}}" +
        "}";

    private const string SameTemplatedPathDifferentMethodsJson = "{" +
        "\"openapi\":\"3.0.3\"," +
        "\"info\":{\"title\":\"Pets\",\"version\":\"1.0.0\"}," +
        "\"paths\":{\"/pets/{id}\":{\"get\":{\"responses\":{\"200\":{\"description\":\"ok\"}}},\"delete\":{\"responses\":{\"204\":{\"description\":\"deleted\"}}}}}" +
        "}";

    private const string EquivalentTemplatedPathsJson = "{" +
        "\"openapi\":\"3.0.3\"," +
        "\"info\":{\"title\":\"Instances\",\"version\":\"1.0.0\"}," +
        "\"paths\":{" +
        "\"/api/v1/instances/{id}\":{\"put\":{\"parameters\":[{\"name\":\"id\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"integer\"}}],\"responses\":{\"200\":{\"description\":\"ok\"}}},\"delete\":{\"parameters\":[{\"name\":\"id\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"integer\"}}],\"responses\":{\"200\":{\"description\":\"ok\"}}}}," +
        "\"/api/v1/instances/{idOrCode}\":{\"get\":{\"parameters\":[{\"name\":\"idOrCode\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"ok\"}}}}" +
        "}" +
        "}";

    private const string EquivalentTemplatedPathsConflictingMethodsJson = "{" +
        "\"openapi\":\"3.0.3\"," +
        "\"info\":{\"title\":\"Instances\",\"version\":\"1.0.0\"}," +
        "\"paths\":{" +
        "\"/api/v1/instances/{id}\":{\"get\":{\"parameters\":[{\"name\":\"id\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"integer\"}}],\"responses\":{\"200\":{\"description\":\"ok\"}}}}," +
        "\"/api/v1/instances/{idOrCode}\":{\"get\":{\"parameters\":[{\"name\":\"idOrCode\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"ok\"}}}}" +
        "}" +
        "}";

    private const string EquivalentTemplatedPathsConflictingMetadataJson = "{" +
        "\"openapi\":\"3.0.3\"," +
        "\"info\":{\"title\":\"Instances\",\"version\":\"1.0.0\"}," +
        "\"paths\":{" +
        "\"/api/v1/instances/{id}\":{\"summary\":\"By integer id\",\"put\":{\"parameters\":[{\"name\":\"id\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"integer\"}}],\"responses\":{\"200\":{\"description\":\"ok\"}}}}," +
        "\"/api/v1/instances/{idOrCode}\":{\"summary\":\"By code\",\"get\":{\"parameters\":[{\"name\":\"idOrCode\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"ok\"}}}}" +
        "}" +
        "}";

    private const string EquivalentTemplatedPathsWithReferencedPathParametersJson = "{" +
        "\"openapi\":\"3.0.3\"," +
        "\"info\":{\"title\":\"Instances\",\"version\":\"1.0.0\"}," +
        "\"paths\":{" +
        "\"/api/v1/instances/{alphaId}\":{\"parameters\":[{\"$ref\":\"#/components/parameters/AlphaId\"}],\"get\":{\"parameters\":[{\"name\":\"alphaId\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"ok\"}}}}," +
        "\"/api/v1/instances/{betaId}\":{\"put\":{\"parameters\":[{\"name\":\"betaId\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"ok\"}}}}" +
        "}," +
        "\"components\":{\"parameters\":{\"AlphaId\":{\"name\":\"alphaId\",\"in\":\"path\",\"required\":true,\"schema\":{\"type\":\"integer\"}}}}" +
        "}";

    private const string MultiValidationIssueJson = "{" +
        "\"openapi\":\"3.0.3\"," +
        "\"info\":{\"title\":\"Pets\",\"version\":\"1.0.0\"}," +
        "\"paths\":{\"/pets\":{\"get\":{\"operationId\":\"listPets\",\"responses\":{\"200\":{\"description\":\"ok\",\"content\":{\"application/json\":{\"schema\":{\"$ref\":\"./schemas/pet.json#/Pet\"}}}}}}},\"/pets/search\":{\"post\":{\"operationId\":\"listPets\",\"responses\":{\"200\":{\"description\":\"ok\",\"content\":{\"application/json\":{\"schema\":{\"$ref\":\"#/components/schemas/MissingPet\"}}}}}}}}" +
        "}";

    private const string MissingOpenApiVersionJson = """
                {
                    "info": {
                        "title": "Pets",
                        "version": "1.0.0"
                    },
                    "paths": {}
                }
                """;

    private const string Swagger2Yaml = """
        swagger: "2.0"
        info:
          title: Pets
          version: 1.0.0
        paths: {}
        """;

    private const string UnsupportedOpenApiYaml = """
                openapi: 3.2.0
                info:
                    title: Pets
                    version: 1.0.0
                paths: {}
                """;

    private const string ExternalReferenceYaml = """
        openapi: 3.0.3
        info:
          title: Pets
          version: 1.0.0
        paths:
          /pets:
            get:
              responses:
                '200':
                  description: ok
                  content:
                    application/json:
                      schema:
                        $ref: './schemas/pet.yaml#/components/schemas/Pet'
        """;

    private const string MinifiedExternalReferenceJson = "{" +
        "\"openapi\":\"3.0.3\"," +
        "\"info\":{\"title\":\"Pets\",\"version\":\"1.0.0\"}," +
        "\"paths\":{\"/pets\":{\"get\":{\"responses\":{\"200\":{\"description\":\"ok\",\"content\":{\"application/json\":{\"schema\":{\"$ref\":\"./schemas/pet.json#/Pet\"}}}}}}}}" +
        "}";

    private const string InternalReferenceJson = """
                {
                    "openapi": "3.0.3",
                    "info": {
                        "title": "Pets",
                        "version": "1.0.0"
                    },
                    "paths": {
                        "/pets": {
                            "get": {
                                "responses": {
                                    "200": {
                                        "description": "ok",
                                        "content": {
                                            "application/json": {
                                                "schema": {
                                                    "$ref": "#/components/schemas/Pet"
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    "components": {
                        "schemas": {
                            "Pet": {
                                "type": "object"
                            }
                        }
                    }
                }
                """;

    private const string MissingInternalReferenceJson = """
                {
                    "openapi": "3.0.3",
                    "info": {
                        "title": "Pets",
                        "version": "1.0.0"
                    },
                    "paths": {
                        "/pets": {
                            "get": {
                                "responses": {
                                    "200": {
                                        "description": "ok",
                                        "content": {
                                            "application/json": {
                                                "schema": {
                                                    "$ref": "#/components/schemas/MissingPet"
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    "components": {
                        "schemas": {
                            "Pet": {
                                "type": "object"
                            }
                        }
                    }
                }
                """;

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}