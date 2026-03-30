using Microsoft.OpenApi.Models;
using NSubstitute;
using Xemplo.ApiChecker.Cli;
using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Core.Tests;

public class CliApplicationTests
{
    [Fact]
    public void Parse_WithValidArguments_ReturnsOptions()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new", "new.json"]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        Assert.Equal("old.json", result.Options!.OldSource);
        Assert.Equal("new.json", result.Options.NewSource);
    }

    [Fact]
    public void Parse_WithUnknownArgument_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new", "new.json", "--verbose"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unknown argument '--verbose'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithMissingValueForOld_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --old.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithMissingValueForNew_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --new.", result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_WithValidInputs_LoadsSpecsAndPrintsReadableResult()
    {
        var loader = Substitute.For<IApiSpecificationLoader>();
        var engine = Substitute.For<IApiComparisonEngine>();
        var oldDocument = new ApiSpecificationDocument(new OpenApiDocument(), "old.json");
        var newDocument = new ApiSpecificationDocument(new OpenApiDocument(), "new.json");
        var output = new StringWriter();
        var error = new StringWriter();

        loader.LoadAsync("old.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(oldDocument));
        loader.LoadAsync("new.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(newDocument));
        engine.Compare(Arg.Any<ApiComparisonInput>(), ApiRuleProfile.Default)
            .Returns(ApiComparisonResult.Empty);

        var exitCode = await CliApplication.RunAsync(
            ["--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(0, exitCode);
        Assert.Contains("Comparing old.json -> new.json", output.ToString());
        Assert.Contains("No findings.", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
        await loader.Received(1).LoadAsync("old.json", Arg.Any<CancellationToken>());
        await loader.Received(1).LoadAsync("new.json", Arg.Any<CancellationToken>());
        engine.Received(1).Compare(
            Arg.Is<ApiComparisonInput>(input => ReferenceEquals(input.OldSpecification, oldDocument)
                && ReferenceEquals(input.NewSpecification, newDocument)),
            ApiRuleProfile.Default);
    }

    [Fact]
    public async Task RunAsync_WithoutInjectedDependencies_UsesDefaultPipeline()
    {
        var oldPath = Path.GetTempFileName();
        var newPath = Path.GetTempFileName();
        var output = new StringWriter();
        var error = new StringWriter();

        await File.WriteAllTextAsync(oldPath, "{\"openapi\":\"3.0.3\",\"info\":{\"title\":\"Old\",\"version\":\"1.0.0\"},\"paths\":{}}");
        await File.WriteAllTextAsync(newPath, "{\"openapi\":\"3.0.3\",\"info\":{\"title\":\"New\",\"version\":\"1.0.0\"},\"paths\":{}}");

        try
        {
            var exitCode = await CliApplication.RunAsync(["--old", oldPath, "--new", newPath], output, error);

            Assert.Equal(0, exitCode);
            Assert.Contains("No findings.", output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            File.Delete(oldPath);
            File.Delete(newPath);
        }
    }

    [Fact]
    public async Task RunAsync_WithMissingArguments_ReturnsRuntimeFailureAndUsage()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["--old", "old.json"], output, error);

        Assert.Equal(2, exitCode);
        Assert.Contains("Both --old and --new are required.", error.ToString());
        Assert.Contains(CliUsage.Text, error.ToString());
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public async Task RunAsync_WithOldSpecLoadFailure_ReturnsRuntimeFailure()
    {
        var loader = Substitute.For<IApiSpecificationLoader>();
        var engine = Substitute.For<IApiComparisonEngine>();
        var output = new StringWriter();
        var error = new StringWriter();

        loader.LoadAsync("old.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Fail(
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.SourceNotFound,
                    "Specification file was not found.",
                    "old.json")));

        var exitCode = await CliApplication.RunAsync(
            ["--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(2, exitCode);
        Assert.Contains("Failed to load old specification 'old.json'", error.ToString());
        Assert.Equal(string.Empty, output.ToString());
        engine.DidNotReceive().Compare(Arg.Any<ApiComparisonInput>(), Arg.Any<ApiRuleProfile>());
    }

    [Fact]
    public async Task RunAsync_WithNewSpecLoadFailure_ReturnsRuntimeFailure()
    {
        var loader = Substitute.For<IApiSpecificationLoader>();
        var engine = Substitute.For<IApiComparisonEngine>();
        var oldDocument = new ApiSpecificationDocument(new OpenApiDocument(), "old.json");
        var output = new StringWriter();
        var error = new StringWriter();

        loader.LoadAsync("old.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(oldDocument));
        loader.LoadAsync("new.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Fail(
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.ParseFailed,
                    "Bad input.",
                    "new.json")));

        var exitCode = await CliApplication.RunAsync(
            ["--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(2, exitCode);
        Assert.Contains("Failed to load new specification 'new.json'", error.ToString());
        Assert.Equal(string.Empty, output.ToString());
        engine.DidNotReceive().Compare(Arg.Any<ApiComparisonInput>(), Arg.Any<ApiRuleProfile>());
    }

    [Fact]
    public async Task RunAsync_WhenEngineThrows_ReturnsRuntimeFailure()
    {
        var loader = Substitute.For<IApiSpecificationLoader>();
        var engine = Substitute.For<IApiComparisonEngine>();
        var oldDocument = new ApiSpecificationDocument(new OpenApiDocument(), "old.json");
        var newDocument = new ApiSpecificationDocument(new OpenApiDocument(), "new.json");
        var output = new StringWriter();
        var error = new StringWriter();

        loader.LoadAsync("old.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(oldDocument));
        loader.LoadAsync("new.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(newDocument));
        engine.When(x => x.Compare(Arg.Any<ApiComparisonInput>(), Arg.Any<ApiRuleProfile>()))
            .Do(_ => throw new InvalidOperationException("boom"));

        var exitCode = await CliApplication.RunAsync(
            ["--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(2, exitCode);
        Assert.Contains("Runtime failure: boom", error.ToString());
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public async Task RunAsync_WithErrorLevelFindings_ReturnsFailingExitCode()
    {
        var loader = Substitute.For<IApiSpecificationLoader>();
        var engine = Substitute.For<IApiComparisonEngine>();
        var oldDocument = new ApiSpecificationDocument(new OpenApiDocument(), "old.json");
        var newDocument = new ApiSpecificationDocument(new OpenApiDocument(), "new.json");
        var output = new StringWriter();
        var error = new StringWriter();

        loader.LoadAsync("old.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(oldDocument));
        loader.LoadAsync("new.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(newDocument));
        engine.Compare(Arg.Any<ApiComparisonInput>(), ApiRuleProfile.Default)
            .Returns(new ApiComparisonResult(
                [new ApiFinding(ApiRuleId.EndpointRemoved, ApiSeverity.Error, "Endpoint removed", new ApiOperationIdentity("GET", "/pets"))]));

        var exitCode = await CliApplication.RunAsync(
            ["--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(1, exitCode);
        Assert.Contains("Findings: 1", output.ToString());
        Assert.Contains("Endpoint removed", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_WithWarningFindings_ReturnsSuccessAndWritesLocation()
    {
        var loader = Substitute.For<IApiSpecificationLoader>();
        var engine = Substitute.For<IApiComparisonEngine>();
        var oldDocument = new ApiSpecificationDocument(new OpenApiDocument(), "old.json");
        var newDocument = new ApiSpecificationDocument(new OpenApiDocument(), "new.json");
        var output = new StringWriter();
        var error = new StringWriter();

        loader.LoadAsync("old.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(oldDocument));
        loader.LoadAsync("new.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(newDocument));
        engine.Compare(Arg.Any<ApiComparisonInput>(), ApiRuleProfile.Default)
            .Returns(new ApiComparisonResult(
                [new ApiFinding(ApiRuleId.NewEndpoint, ApiSeverity.Warning, "Endpoint added", new ApiOperationIdentity("POST", "/pets"))]));

        var exitCode = await CliApplication.RunAsync(
            ["--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(0, exitCode);
        Assert.Contains("Findings: 1", output.ToString());
        Assert.Contains("[POST /pets]", output.ToString());
        Assert.Contains("Endpoint added", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_WithFindingWithoutOperation_WritesReadableLine()
    {
        var loader = Substitute.For<IApiSpecificationLoader>();
        var engine = Substitute.For<IApiComparisonEngine>();
        var oldDocument = new ApiSpecificationDocument(new OpenApiDocument(), "old.json");
        var newDocument = new ApiSpecificationDocument(new OpenApiDocument(), "new.json");
        var output = new StringWriter();
        var error = new StringWriter();

        loader.LoadAsync("old.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(oldDocument));
        loader.LoadAsync("new.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(newDocument));
        engine.Compare(Arg.Any<ApiComparisonInput>(), ApiRuleProfile.Default)
            .Returns(new ApiComparisonResult(
                [new ApiFinding(ApiRuleId.NewResponseCode, ApiSeverity.Warning, "Response code added") ]));

        var exitCode = await CliApplication.RunAsync(
            ["--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(0, exitCode);
        Assert.Contains("NewResponseCode", output.ToString());
        Assert.Contains("Response code added", output.ToString());
        Assert.DoesNotContain("[]", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_WithRequestBodyFixtureInputs_ReportsBodyFindings()
    {
        var oldPath = GetFixturePath("request-body-old.yaml");
        var newPath = GetFixturePath("request-body-new.yaml");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["--old", oldPath, "--new", newPath], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("NewRequiredInput", output.ToString());
        Assert.Contains("NewOptionalInput", output.ToString());
        Assert.Contains("RemovedInput", output.ToString());
        Assert.Contains("$.pet.age", output.ToString());
        Assert.Contains("$.pet.tags[].code", output.ToString());
        Assert.Contains("$.pet.legacy", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    private static string GetFixturePath(string fileName)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", fileName));
    }
}