using Microsoft.OpenApi.Models;
using NSubstitute;
using System.Text.Json;
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
        Assert.Equal(CliOutputMode.Text, result.Options.OutputMode);
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
    public void Parse_WithDuplicateOldArgument_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--old", "other.json", "--new", "new.json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Duplicate argument '--old'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithDuplicateNewArgument_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new", "new.json", "--new", "other.json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Duplicate argument '--new'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithConfigAndRuleOverrides_ReturnsExpandedOptions()
    {
        var result = CliArgumentParser.Parse([
            "--old", "old.json",
            "--new", "new.json",
            "--output", "json",
            "--config", "rules.json",
            "--rule", "NewRequiredInput=off",
            "--rule", "RemovedInput=warning"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(CliOutputMode.Json, result.Options!.OutputMode);
        Assert.Equal("rules.json", result.Options!.ConfigPath);
        Assert.Equal(ApiSeverity.Off, result.Options.RuleOverrides[ApiRuleId.NewRequiredInput]);
        Assert.Equal(ApiSeverity.Warning, result.Options.RuleOverrides[ApiRuleId.RemovedInput]);
    }

    [Fact]
    public void Parse_WithMissingValueForOutput_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new", "new.json", "--output"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --output.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithDuplicateOutputArgument_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new", "new.json", "--output", "json", "--output", "text"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Duplicate argument '--output'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithUnsupportedOutputMode_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new", "new.json", "--output", "xml"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Output mode 'xml' is not supported. Use 'text' or 'json'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithMissingValueForConfig_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new", "new.json", "--config"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --config.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithMissingValueForRule_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new", "new.json", "--rule"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --rule.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithInvalidRuleFormat_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new", "new.json", "--rule", "NewRequiredInput"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid rule override 'NewRequiredInput'. Expected <RuleId>=<severity>.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithUnsupportedRule_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new", "new.json", "--rule", "UnknownRule=warning"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Rule 'UnknownRule' is not supported.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithUnsupportedSeverity_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["--old", "old.json", "--new", "new.json", "--rule", "NewRequiredInput=critical"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Severity 'critical' is not supported.", result.ErrorMessage);
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
    public async Task RunAsync_AutoDiscoversLocalConfigFromWorkingDirectory()
    {
        var directory = CreateTempDirectory();
        var oldPath = GetFixturePath("request-body-old.json");
        var newPath = GetFixturePath("request-body-new.json");
        var output = new StringWriter();
        var error = new StringWriter();

        await File.WriteAllTextAsync(Path.Combine(directory, "api-rules.json"), """
            {
              "rules": {
                "NewRequiredInput": "off",
                "NewOptionalInput": "off",
                "RemovedInput": "off"
              }
            }
            """);

        try
        {
            var exitCode = await CliApplication.RunAsync(["--old", oldPath, "--new", newPath], output, error, workingDirectory: directory);

            Assert.Equal(0, exitCode);
            Assert.Contains("No findings.", output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ExplicitConfigOverridesAutoDiscoveredConfig()
    {
        var directory = CreateTempDirectory();
        var oldPath = GetFixturePath("request-body-old.json");
        var newPath = GetFixturePath("request-body-new.json");
        var explicitConfigPath = Path.Combine(directory, "pipeline-rules.json");
        var output = new StringWriter();
        var error = new StringWriter();

        await File.WriteAllTextAsync(Path.Combine(directory, "api-rules.json"), """
            {
              "rules": {
                "NewRequiredInput": "off",
                "NewOptionalInput": "off",
                "RemovedInput": "off"
              }
            }
            """);
        await File.WriteAllTextAsync(explicitConfigPath, """
            {
              "rules": {
                "NewRequiredInput": "error"
              }
            }
            """);

        try
        {
            var exitCode = await CliApplication.RunAsync(
                ["--old", oldPath, "--new", newPath, "--config", "pipeline-rules.json"],
                output,
                error,
                workingDirectory: directory);

            Assert.Equal(1, exitCode);
            Assert.Contains("NewRequiredInput", output.ToString());
            Assert.DoesNotContain("NewOptionalInput", output.ToString());
            Assert.DoesNotContain("RemovedInput", output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_RuleOverridesApplyOnTopOfFileConfiguration()
    {
        var directory = CreateTempDirectory();
        var oldPath = GetFixturePath("request-body-old.json");
        var newPath = GetFixturePath("request-body-new.json");
        var explicitConfigPath = Path.Combine(directory, "pipeline-rules.json");
        var output = new StringWriter();
        var error = new StringWriter();

        await File.WriteAllTextAsync(explicitConfigPath, """
            {
              "rules": {
                "NewRequiredInput": "off",
                "NewOptionalInput": "off",
                "RemovedInput": "off"
              }
            }
            """);

        try
        {
            var exitCode = await CliApplication.RunAsync(
                ["--old", oldPath, "--new", newPath, "--config", "pipeline-rules.json", "--rule", "NewRequiredInput=error"],
                output,
                error,
                workingDirectory: directory);

            Assert.Equal(1, exitCode);
            Assert.Contains("NewRequiredInput", output.ToString());
            Assert.DoesNotContain("NewOptionalInput", output.ToString());
            Assert.DoesNotContain("RemovedInput", output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WithInvalidConfigContent_ReturnsRuntimeFailure()
    {
        var directory = CreateTempDirectory();
        var oldPath = GetFixturePath("request-body-old.json");
        var newPath = GetFixturePath("request-body-new.json");
        var output = new StringWriter();
        var error = new StringWriter();

        await File.WriteAllTextAsync(Path.Combine(directory, "bad-rules.json"), "{ not-json }");

        try
        {
            var exitCode = await CliApplication.RunAsync(
                ["--old", oldPath, "--new", newPath, "--config", "bad-rules.json"],
                output,
                error,
                workingDirectory: directory);

            Assert.Equal(2, exitCode);
            Assert.Contains("Invalid configuration:", error.ToString());
            Assert.Equal(string.Empty, output.ToString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
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
    public async Task RunAsync_WithJsonOutput_WritesStructuredJsonInStableOrder()
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
            [
                new ApiFinding(
                    ApiRuleId.EndpointRemoved,
                    ApiSeverity.Error,
                    "Endpoint removed",
                    new ApiOperationIdentity("GET", "/alpha")),
                new ApiFinding(
                    ApiRuleId.NewNullableOutput,
                    ApiSeverity.Warning,
                    "Nullable field added",
                    new ApiOperationIdentity("POST", "/beta"),
                    "$.result.summary")
            ]));

        var exitCode = await CliApplication.RunAsync(
            ["--old", "old.json", "--new", "new.json", "--output", "json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.DoesNotContain("Comparing old.json -> new.json", output.ToString());

        using var document = JsonDocument.Parse(output.ToString());
        var root = document.RootElement;
        Assert.Equal("old.json", root.GetProperty("oldSource").GetString());
        Assert.Equal("new.json", root.GetProperty("newSource").GetString());
        Assert.True(root.GetProperty("hasErrorFindings").GetBoolean());

        var findings = root.GetProperty("findings");
        Assert.Equal(2, findings.GetArrayLength());

        var firstFinding = findings[0];
        Assert.Equal("EndpointRemoved", firstFinding.GetProperty("ruleId").GetString());
        Assert.Equal("error", firstFinding.GetProperty("severity").GetString());
        Assert.Equal("Endpoint removed", firstFinding.GetProperty("message").GetString());
        Assert.False(firstFinding.TryGetProperty("schemaPath", out _));
        Assert.Equal("GET", firstFinding.GetProperty("operation").GetProperty("method").GetString());
        Assert.Equal("/alpha", firstFinding.GetProperty("operation").GetProperty("pathTemplate").GetString());

        var secondFinding = findings[1];
        Assert.Equal("NewNullableOutput", secondFinding.GetProperty("ruleId").GetString());
        Assert.Equal("warning", secondFinding.GetProperty("severity").GetString());
        Assert.Equal("$.result.summary", secondFinding.GetProperty("schemaPath").GetString());
        Assert.Equal("POST", secondFinding.GetProperty("operation").GetProperty("method").GetString());
        Assert.Equal("/beta", secondFinding.GetProperty("operation").GetProperty("pathTemplate").GetString());
    }

    [Fact]
    public async Task RunAsync_WithFixtureInputsAndJsonOutput_ReportsDeterministicFindingOrder()
    {
        var oldPath = GetFixturePath("endpoint-responsecode-old.json");
        var newPath = GetFixturePath("endpoint-responsecode-new.json");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["--old", oldPath, "--new", newPath, "--output", "json"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, error.ToString());

        using var document = JsonDocument.Parse(output.ToString());
        var findings = document.RootElement.GetProperty("findings");

        Assert.Equal(3, findings.GetArrayLength());
        Assert.Equal("EndpointRemoved", findings[0].GetProperty("ruleId").GetString());
        Assert.Equal("GET", findings[0].GetProperty("operation").GetProperty("method").GetString());
        Assert.Equal("/orders", findings[0].GetProperty("operation").GetProperty("pathTemplate").GetString());

        Assert.Equal("NewResponseCode", findings[1].GetProperty("ruleId").GetString());
        Assert.Equal("GET", findings[1].GetProperty("operation").GetProperty("method").GetString());
        Assert.Equal("/pets", findings[1].GetProperty("operation").GetProperty("pathTemplate").GetString());

        Assert.Equal("NewEndpoint", findings[2].GetProperty("ruleId").GetString());
        Assert.Equal("POST", findings[2].GetProperty("operation").GetProperty("method").GetString());
        Assert.Equal("/pets", findings[2].GetProperty("operation").GetProperty("pathTemplate").GetString());
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
        var oldPath = GetFixturePath("request-body-old.json");
        var newPath = GetFixturePath("request-body-new.json");
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

    [Fact]
    public async Task RunAsync_WithQueryParameterFixtureInputs_ReportsQueryFindings()
    {
        var oldPath = GetFixturePath("query-params-old.json");
        var newPath = GetFixturePath("query-params-new.json");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["--old", oldPath, "--new", newPath], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("NewRequiredQueryParam", output.ToString());
        Assert.Contains("NewOptionalQueryParam", output.ToString());
        Assert.Contains("$query.filter", output.ToString());
        Assert.Contains("$query.includeDetails", output.ToString());
        Assert.Contains("$query.limit", output.ToString());
        Assert.DoesNotContain("traceId", output.ToString());
        Assert.DoesNotContain("polymorphic", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_WithResponseBodyFixtureInputs_ReportsResponseFindings()
    {
        var oldPath = GetFixturePath("response-body-old.json");
        var newPath = GetFixturePath("response-body-new.json");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["--old", oldPath, "--new", newPath], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("RemovedOutput", output.ToString());
        Assert.Contains("NewNullableOutput", output.ToString());
        Assert.Contains("NewNonNullableOutput", output.ToString());
        Assert.Contains("NewEnumOutput", output.ToString());
        Assert.Contains("$.result.legacy", output.ToString());
        Assert.Contains("$.result.summary", output.ToString());
        Assert.Contains("$.result.count", output.ToString());
        Assert.Contains("$.result.status", output.ToString());
        Assert.DoesNotContain("internalNote", output.ToString());
        Assert.DoesNotContain("polymorphic", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_WithEndpointAndResponseCodeFixtureInputs_ReportsSurfaceFindings()
    {
        var oldPath = GetFixturePath("endpoint-responsecode-old.json");
        var newPath = GetFixturePath("endpoint-responsecode-new.json");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["--old", oldPath, "--new", newPath], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("EndpointRemoved", output.ToString());
        Assert.Contains("NewEndpoint", output.ToString());
        Assert.Contains("NewResponseCode", output.ToString());
        Assert.Contains("[GET /orders]", output.ToString());
        Assert.Contains("[POST /pets]", output.ToString());
        Assert.Contains("Response code '201' was added.", output.ToString());
        Assert.DoesNotContain("404", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    private static string GetFixturePath(string fileName)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", fileName));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"api-checker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}