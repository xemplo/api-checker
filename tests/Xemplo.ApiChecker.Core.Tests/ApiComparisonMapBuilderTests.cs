using System.Net.Http;
using Microsoft.OpenApi.Models;
using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Core.Tests;

public class ApiComparisonMapBuilderTests
{
    [Fact]
    public void Build_MatchesOperationsByExactMethodAndCaseInsensitivePath()
    {
        var oldDocument = CreateDocument(Operation("/pets", HttpMethod.Get, ("200", "application/json")));
        var newDocument = CreateDocument(Operation("/PETS", HttpMethod.Get, ("200", "application/json")));
        var builder = new ApiComparisonMapBuilder();

        var map = builder.Build(new ApiComparisonInput(new ApiSpecificationDocument(oldDocument), new ApiSpecificationDocument(newDocument)));

        Assert.Single(map.MatchedOperations);
        Assert.Empty(map.UnmatchedOldOperations);
        Assert.Empty(map.UnmatchedNewOperations);
        Assert.Equal("GET", map.MatchedOperations[0].OldOperation.Identity.Method);
        Assert.Equal("/pets", map.MatchedOperations[0].OldOperation.Identity.PathTemplate);
    }

    [Fact]
    public void Build_TreatsMethodChangeAsUnmatchedOldAndNewOperation()
    {
        var oldDocument = CreateDocument(Operation("/pets", HttpMethod.Get, ("200", "application/json")));
        var newDocument = CreateDocument(Operation("/pets", HttpMethod.Post, ("200", "application/json")));
        var builder = new ApiComparisonMapBuilder();

        var map = builder.Build(new ApiComparisonInput(new ApiSpecificationDocument(oldDocument), new ApiSpecificationDocument(newDocument)));

        Assert.Empty(map.MatchedOperations);
        Assert.Single(map.UnmatchedOldOperations);
        Assert.Single(map.UnmatchedNewOperations);
        Assert.Equal("GET", map.UnmatchedOldOperations[0].Identity.Method);
        Assert.Equal("POST", map.UnmatchedNewOperations[0].Identity.Method);
    }

    [Fact]
    public void Build_MatchesResponsesByExactStatusAndExactJsonMediaType()
    {
        var oldDocument = CreateDocument(Operation(
            "/pets",
            HttpMethod.Get,
            ("200", "application/json"),
            ("200", "application/xml"),
            ("default", "application/json")));
        var newDocument = CreateDocument(Operation(
            "/pets",
            HttpMethod.Get,
            ("200", "application/json"),
            ("200", "application/problem+json"),
            ("201", "application/json")));
        var builder = new ApiComparisonMapBuilder();

        var map = builder.Build(new ApiComparisonInput(new ApiSpecificationDocument(oldDocument), new ApiSpecificationDocument(newDocument)));
        var operation = Assert.Single(map.MatchedOperations);

        Assert.Collection(
            operation.MatchedResponses,
            match =>
            {
                Assert.Equal(ApiResponseMatchKind.Exact, match.MatchKind);
                Assert.Equal("200", match.OldResponse.StatusCode);
                Assert.Equal("application/json", match.OldResponse.MediaType);
            },
            match =>
            {
                Assert.Equal(ApiResponseMatchKind.DefaultFallback, match.MatchKind);
                Assert.Equal("default", match.OldResponse.StatusCode);
                Assert.Equal("201", match.NewResponse.StatusCode);
            });

        Assert.Single(operation.UnmatchedNewResponses);
        Assert.Equal("application/problem+json", operation.UnmatchedNewResponses[0].MediaType);
        Assert.Empty(operation.UnmatchedOldResponses);
    }

    [Fact]
    public void Build_UsesDefaultFallbackWhenOtherSideLacksExplicitStatusCode()
    {
        var oldDocument = CreateDocument(Operation("/pets", HttpMethod.Get, ("default", "application/json")));
        var newDocument = CreateDocument(Operation("/pets", HttpMethod.Get, ("404", "application/json")));
        var builder = new ApiComparisonMapBuilder();

        var map = builder.Build(new ApiComparisonInput(new ApiSpecificationDocument(oldDocument), new ApiSpecificationDocument(newDocument)));
        var operation = Assert.Single(map.MatchedOperations);
        var responseMatch = Assert.Single(operation.MatchedResponses);

        Assert.Equal(ApiResponseMatchKind.DefaultFallback, responseMatch.MatchKind);
        Assert.Equal("default", responseMatch.OldResponse.StatusCode);
        Assert.Equal("404", responseMatch.NewResponse.StatusCode);
        Assert.Empty(operation.UnmatchedOldResponses);
        Assert.Empty(operation.UnmatchedNewResponses);
    }

    [Fact]
    public void Build_AllowsSingleDefaultResponseToMatchMultipleNewExplicitStatusCodes()
    {
        var oldDocument = CreateDocument(Operation("/pets", HttpMethod.Get, ("default", "application/json")));
        var newDocument = CreateDocument(Operation(
            "/pets",
            HttpMethod.Get,
            ("404", "application/json"),
            ("500", "application/json")));
        var builder = new ApiComparisonMapBuilder();

        var map = builder.Build(new ApiComparisonInput(new ApiSpecificationDocument(oldDocument), new ApiSpecificationDocument(newDocument)));
        var operation = Assert.Single(map.MatchedOperations);

        Assert.Equal(2, operation.MatchedResponses.Count);
        Assert.All(operation.MatchedResponses, match => Assert.Equal(ApiResponseMatchKind.DefaultFallback, match.MatchKind));
        Assert.Contains(operation.MatchedResponses, static match => match.NewResponse.StatusCode == "404");
        Assert.Contains(operation.MatchedResponses, static match => match.NewResponse.StatusCode == "500");
        Assert.Empty(operation.UnmatchedOldResponses);
        Assert.Empty(operation.UnmatchedNewResponses);
    }

    [Fact]
    public void Build_AllowsSingleDefaultResponseToMatchMultipleOldExplicitStatusCodes()
    {
        var oldDocument = CreateDocument(Operation(
            "/pets",
            HttpMethod.Get,
            ("404", "application/json"),
            ("500", "application/json")));
        var newDocument = CreateDocument(Operation("/pets", HttpMethod.Get, ("default", "application/json")));
        var builder = new ApiComparisonMapBuilder();

        var map = builder.Build(new ApiComparisonInput(new ApiSpecificationDocument(oldDocument), new ApiSpecificationDocument(newDocument)));
        var operation = Assert.Single(map.MatchedOperations);

        Assert.Equal(2, operation.MatchedResponses.Count);
        Assert.All(operation.MatchedResponses, match => Assert.Equal(ApiResponseMatchKind.DefaultFallback, match.MatchKind));
        Assert.Contains(operation.MatchedResponses, static match => match.OldResponse.StatusCode == "404");
        Assert.Contains(operation.MatchedResponses, static match => match.OldResponse.StatusCode == "500");
        Assert.Empty(operation.UnmatchedOldResponses);
        Assert.Empty(operation.UnmatchedNewResponses);
    }

    [Fact]
    public void Build_DoesNotUseDefaultFallbackWhenExplicitStatusExistsOnOtherSide()
    {
        var oldDocument = CreateDocument(Operation("/pets", HttpMethod.Get, ("404", "application/json")));
        var newDocument = CreateDocument(Operation(
            "/pets",
            HttpMethod.Get,
            ("404", "application/problem+json"),
            ("default", "application/json")));
        var builder = new ApiComparisonMapBuilder();

        var map = builder.Build(new ApiComparisonInput(new ApiSpecificationDocument(oldDocument), new ApiSpecificationDocument(newDocument)));
        var operation = Assert.Single(map.MatchedOperations);

        Assert.Empty(operation.MatchedResponses);
        Assert.Single(operation.UnmatchedOldResponses);
        Assert.Equal("404", operation.UnmatchedOldResponses[0].StatusCode);
        Assert.Equal("application/json", operation.UnmatchedOldResponses[0].MediaType);
        Assert.Equal(2, operation.UnmatchedNewResponses.Count);
    }

    [Fact]
    public void Build_IgnoresNonJsonMediaTypesForResponseMatching()
    {
        var oldDocument = CreateDocument(Operation("/pets", HttpMethod.Get, ("200", "application/xml")));
        var newDocument = CreateDocument(Operation("/pets", HttpMethod.Get, ("200", "text/plain")));
        var builder = new ApiComparisonMapBuilder();

        var map = builder.Build(new ApiComparisonInput(new ApiSpecificationDocument(oldDocument), new ApiSpecificationDocument(newDocument)));
        var operation = Assert.Single(map.MatchedOperations);

        Assert.Empty(operation.MatchedResponses);
        Assert.Empty(operation.UnmatchedOldResponses);
        Assert.Empty(operation.UnmatchedNewResponses);
    }

    [Fact]
    public void Build_MatchesDefaultResponsesExactlyWhenBothSidesDeclareDefault()
    {
        var oldDocument = CreateDocument(Operation("/pets", HttpMethod.Get, ("default", "application/problem+json")));
        var newDocument = CreateDocument(Operation("/pets", HttpMethod.Get, ("default", "application/problem+json")));
        var builder = new ApiComparisonMapBuilder();

        var map = builder.Build(new ApiComparisonInput(new ApiSpecificationDocument(oldDocument), new ApiSpecificationDocument(newDocument)));
        var operation = Assert.Single(map.MatchedOperations);
        var responseMatch = Assert.Single(operation.MatchedResponses);

        Assert.Equal(ApiResponseMatchKind.Exact, responseMatch.MatchKind);
        Assert.Equal("default", responseMatch.OldResponse.StatusCode);
        Assert.Equal("application/problem+json", responseMatch.OldResponse.MediaType);
    }

    private static OpenApiDocument CreateDocument(params (string Path, HttpMethod Method, OpenApiResponses Responses)[] operations)
    {
        var paths = new OpenApiPaths();

        foreach (var group in operations.GroupBy(static operation => operation.Path, StringComparer.Ordinal))
        {
            var pathItem = new OpenApiPathItem();

            foreach (var operation in group)
            {
                pathItem.AddOperation(operation.Method, new OpenApiOperation
                {
                    Responses = operation.Responses
                });
            }

            paths.Add(group.Key, pathItem);
        }

        return new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test", Version = "1.0.0" },
            Paths = paths
        };
    }

    private static (string Path, HttpMethod Method, OpenApiResponses Responses) Operation(
        string path,
        HttpMethod method,
        params (string StatusCode, string MediaType)[] responses)
    {
        var openApiResponses = new OpenApiResponses();

        foreach (var responseGroup in responses.GroupBy(static response => response.StatusCode, StringComparer.Ordinal))
        {
            var content = new Dictionary<string, OpenApiMediaType>(StringComparer.OrdinalIgnoreCase);
            var response = new OpenApiResponse
            {
                Description = responseGroup.Key,
                Content = content
            };

            foreach (var mediaType in responseGroup)
            {
                content[mediaType.MediaType] = new OpenApiMediaType();
            }

            openApiResponses[responseGroup.Key] = response;
        }

        return (path, method, openApiResponses);
    }
}