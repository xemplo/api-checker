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
            Assert.NotNull(result.Failure);
            Assert.Equal(ApiSpecificationLoadFailureKind.UnsupportedSpecificationVersion, result.Failure!.Kind);
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

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}