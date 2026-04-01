using Microsoft.OpenApi.Models;
using NSubstitute;
using System.Text.Json;
using Xemplo.ApiChecker.Cli;
using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Core.Tests;

public class CliApplicationTests
{
    [Fact]
    public void Parse_WithCompareCommandAndValidArguments_ReturnsOptions()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json"]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        Assert.Equal(CliCommand.Compare, result.Options!.Command);
        Assert.Equal("old.json", result.Options!.OldSource);
        Assert.Equal("new.json", result.Options.NewSource);
        Assert.Null(result.Options.SpecificationSource);
        Assert.Equal(CliOutputMode.Text, result.Options.OutputMode);
    }

    [Fact]
    public void Parse_WithValidateCommandAndValidArgument_ReturnsOptions()
    {
        var result = CliArgumentParser.Parse(["validate", "spec.json"]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        Assert.Equal(CliCommand.Validate, result.Options!.Command);
        Assert.Equal("spec.json", result.Options.SpecificationSource);
        Assert.Null(result.Options.OldSource);
        Assert.Null(result.Options.NewSource);
    }

    [Fact]
    public void Parse_WithoutCommand_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse([]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing command. Use 'compare' or 'validate'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithUnknownCommand_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["diff", "spec.json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unknown command 'diff'. Use 'compare' or 'validate'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithUnknownCompareArgument_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json", "--verbose"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unknown argument '--verbose'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithMissingValueForOld_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --old.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithOptionTokenInsteadOfOldValue_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "--new", "new.json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --old.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithMissingValueForNew_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --new.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithDuplicateOldArgument_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--old", "other.json", "--new", "new.json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Duplicate argument '--old'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithDuplicateNewArgument_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json", "--new", "other.json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Duplicate argument '--new'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithConfigAndRuleOverrides_ReturnsExpandedOptions()
    {
        var result = CliArgumentParser.Parse([
            "compare",
            "--old", "old.json",
            "--new", "new.json",
            "--output", "json",
            "--config", "rules.json",
            "--rule", "input:new:required=off",
            "--rule", "input:removed=warning"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(CliOutputMode.Json, result.Options!.OutputMode);
        Assert.Equal("rules.json", result.Options!.ConfigPath);
        Assert.Equal(ApiSeverity.Off, result.Options.RuleOverrides[ApiRuleId.NewRequiredInput]);
        Assert.Equal(ApiSeverity.Warning, result.Options.RuleOverrides[ApiRuleId.RemovedInput]);
    }

    [Fact]
    public void Parse_CompareWithMissingValueForOutput_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json", "--output"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --output.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithDuplicateOutputArgument_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json", "--output", "json", "--output", "text"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Duplicate argument '--output'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithUnsupportedOutputMode_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json", "--output", "xml"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Output mode 'xml' is not supported. Use 'text' or 'json'.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithMissingValueForConfig_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json", "--config"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --config.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithMissingValueForRule_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json", "--rule"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --rule.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithOptionTokenInsteadOfRuleValue_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json", "--rule", "--output", "json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing value for --rule.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithInvalidRuleFormat_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json", "--rule", "input:new:required"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid rule override 'input:new:required'. Expected <rule-id>=<severity>.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithUnsupportedRule_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json", "--rule", "UnknownRule=warning"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Rule 'UnknownRule' is not supported.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_CompareWithUnsupportedSeverity_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["compare", "--old", "old.json", "--new", "new.json", "--rule", "input:new:required=critical"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Severity 'critical' is not supported.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_ValidateWithoutSpec_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["validate"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("The validate command requires <spec>.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_ValidateWithOptionTokenInsteadOfSpec_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["validate", "--output"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("The validate command requires <spec>.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_ValidateWithUnexpectedArgument_ReturnsFailure()
    {
        var result = CliArgumentParser.Parse(["validate", "spec.json", "extra.json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unknown argument 'extra.json'.", result.ErrorMessage);
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
            ["compare", "--old", "old.json", "--new", "new.json"],
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
    public async Task RunAsync_WithValidateCommandAndValidInput_PrintsValidationSuccess()
    {
        var loader = Substitute.For<IApiSpecificationLoader>();
        var engine = Substitute.For<IApiComparisonEngine>();
        var document = new ApiSpecificationDocument(new OpenApiDocument(), "spec.json");
        var output = new StringWriter();
        var error = new StringWriter();

        loader.LoadAsync("spec.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(document));

        var exitCode = await CliApplication.RunAsync(
            ["validate", "spec.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(0, exitCode);
        Assert.Contains("Validated spec.json", output.ToString());
        Assert.Contains("Specification is valid.", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
        await loader.Received(1).LoadAsync("spec.json", Arg.Any<CancellationToken>());
        engine.DidNotReceive().Compare(Arg.Any<ApiComparisonInput>(), Arg.Any<ApiRuleProfile>());
    }

    [Fact]
    public async Task RunAsync_WithValidateCommandAndLoadFailure_ReturnsRuntimeFailure()
    {
        var loader = Substitute.For<IApiSpecificationLoader>();
        var engine = Substitute.For<IApiComparisonEngine>();
        var output = new StringWriter();
        var error = new StringWriter();

        loader.LoadAsync("spec.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Fail(
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.ParseFailed,
                    "Bad input.",
                    "spec.json")));

        var exitCode = await CliApplication.RunAsync(
            ["validate", "spec.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(2, exitCode);
        Assert.Contains("Failed to load specification 'spec.json':", error.ToString());
        Assert.Contains("- \u001b[31mERROR\u001b[0m Bad input.", error.ToString());
        Assert.Equal(string.Empty, output.ToString());
        engine.DidNotReceive().Compare(Arg.Any<ApiComparisonInput>(), Arg.Any<ApiRuleProfile>());
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
            var exitCode = await CliApplication.RunAsync(["compare", "--old", oldPath, "--new", newPath], output, error);

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
                                "input:new:required": "off",
                                "input:new:optional": "off",
                                "input:removed": "off"
              }
            }
            """);

        try
        {
            var exitCode = await CliApplication.RunAsync(["compare", "--old", oldPath, "--new", newPath], output, error, workingDirectory: directory);

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
                                "input:new:required": "off",
                                "input:new:optional": "off",
                                "input:removed": "off"
              }
            }
            """);
        await File.WriteAllTextAsync(explicitConfigPath, """
            {
              "rules": {
                                "input:new:required": "error"
              }
            }
            """);

        try
        {
            var exitCode = await CliApplication.RunAsync(
                ["compare", "--old", oldPath, "--new", newPath, "--config", "pipeline-rules.json"],
                output,
                error,
                workingDirectory: directory);

            Assert.Equal(1, exitCode);
            Assert.Contains("input:new:required", output.ToString());
            Assert.DoesNotContain("input:new:optional", output.ToString());
            Assert.DoesNotContain("input:removed", output.ToString());
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
                                "input:new:required": "off",
                                "input:new:optional": "off",
                                "input:removed": "off"
              }
            }
            """);

        try
        {
            var exitCode = await CliApplication.RunAsync(
                ["compare", "--old", oldPath, "--new", newPath, "--config", "pipeline-rules.json", "--rule", "input:new:required=error"],
                output,
                error,
                workingDirectory: directory);

            Assert.Equal(1, exitCode);
            Assert.Contains("input:new:required", output.ToString());
            Assert.DoesNotContain("input:new:optional", output.ToString());
            Assert.DoesNotContain("input:removed", output.ToString());
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
                ["compare", "--old", oldPath, "--new", newPath, "--config", "bad-rules.json"],
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

        var exitCode = await CliApplication.RunAsync(["compare", "--old", "old.json"], output, error);

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
            ["compare", "--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(2, exitCode);
        Assert.Contains("Failed to load old specification 'old.json':", error.ToString());
        Assert.Contains("- \u001b[31mERROR\u001b[0m Specification file was not found.", error.ToString());
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
            ["compare", "--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(2, exitCode);
        Assert.Contains("Failed to load new specification 'new.json':", error.ToString());
        Assert.Contains("- \u001b[31mERROR\u001b[0m Bad input.", error.ToString());
        Assert.Equal(string.Empty, output.ToString());
        engine.DidNotReceive().Compare(Arg.Any<ApiComparisonInput>(), Arg.Any<ApiRuleProfile>());
    }

    [Fact]
    public async Task RunAsync_WithDuplicateOperationIdsInNewSpec_ReturnsLoadFailure()
    {
        var oldPath = Path.GetTempFileName();
        var newPath = Path.GetTempFileName();
        var output = new StringWriter();
        var error = new StringWriter();

        await File.WriteAllTextAsync(oldPath, "{\"openapi\":\"3.0.3\",\"info\":{\"title\":\"Old\",\"version\":\"1.0.0\"},\"paths\":{}}");
        await File.WriteAllTextAsync(newPath, "{\"openapi\":\"3.0.3\",\"info\":{\"title\":\"New\",\"version\":\"1.0.0\"},\"paths\":{\"/pets\":{\"get\":{\"operationId\":\"listPets\",\"responses\":{\"200\":{\"description\":\"ok\"}}}},\"/pets/search\":{\"post\":{\"operationId\":\"listPets\",\"responses\":{\"200\":{\"description\":\"ok\"}}}}}}");

        try
        {
            var exitCode = await CliApplication.RunAsync(["compare", "--old", oldPath, "--new", newPath], output, error);

            Assert.Equal(2, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("Failed to load new specification", error.ToString());
            Assert.Contains("- \u001b[31mERROR\u001b[0m Duplicate operationId '\u001b[38;5;208mlistPets\u001b[0m' is used by GET /pets, POST /pets/search.", error.ToString());
            Assert.Contains("listPets", error.ToString());
        }
        finally
        {
            File.Delete(oldPath);
            File.Delete(newPath);
        }
    }

    [Fact]
    public async Task RunAsync_WithLoadFailuresInBothSpecs_ReportsAllValidationErrors()
    {
        var loader = Substitute.For<IApiSpecificationLoader>();
        var engine = Substitute.For<IApiComparisonEngine>();
        var output = new StringWriter();
        var error = new StringWriter();

        loader.LoadAsync("old.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Fail(
            [
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.SourceNotFound,
                    "Specification file was not found.",
                    "old.json"),
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.ParseFailed,
                    "Failed to parse specification: extra trailing content.",
                    "old.json")
            ]));
        loader.LoadAsync("new.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Fail(
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.DuplicateOperationId,
                    "Duplicate operationId 'listPets' is used by GET /pets, POST /pets/search.",
                    "new.json",
                    "listPets")));

        var exitCode = await CliApplication.RunAsync(
            ["compare", "--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Failed to load old specification 'old.json':", error.ToString());
        Assert.Contains("Failed to load new specification 'new.json':", error.ToString());
        Assert.Contains("- \u001b[31mERROR\u001b[0m Specification file was not found.", error.ToString());
        Assert.Contains("- \u001b[31mERROR\u001b[0m Failed to parse specification: extra trailing content.", error.ToString());
        Assert.Contains("- \u001b[31mERROR\u001b[0m Duplicate operationId '\u001b[38;5;208mlistPets\u001b[0m' is used by GET /pets, POST /pets/search.", error.ToString());
        engine.DidNotReceive().Compare(Arg.Any<ApiComparisonInput>(), Arg.Any<ApiRuleProfile>());
    }

    [Fact]
    public async Task RunAsync_WithHighlightedValidationSubject_ColorsOnlyTheSubject()
    {
        var loader = Substitute.For<IApiSpecificationLoader>();
        var engine = Substitute.For<IApiComparisonEngine>();
        var output = new StringWriter();
        var error = new StringWriter();

        loader.LoadAsync("old.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Fail(
                new ApiSpecificationLoadFailure(
                    ApiSpecificationLoadFailureKind.DuplicateOperationId,
                    "Duplicate operationId 'listPets' is used by GET /pets, POST /pets/search.",
                    "old.json",
                    "listPets")));
        loader.LoadAsync("new.json", Arg.Any<CancellationToken>())
            .Returns(ApiSpecificationLoadResult.Success(new ApiSpecificationDocument(new OpenApiDocument(), "new.json")));

        var exitCode = await CliApplication.RunAsync(
            ["compare", "--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(2, exitCode);
        Assert.Contains("\u001b[38;5;208mlistPets\u001b[0m", error.ToString());
        Assert.DoesNotContain("\u001b[38;5;208mGET /pets\u001b[0m", error.ToString());
        engine.DidNotReceive().Compare(Arg.Any<ApiComparisonInput>(), Arg.Any<ApiRuleProfile>());
    }

    [Fact]
    public async Task RunAsync_WithMissingInternalReference_ColorsMissingReferenceTarget()
    {
        var oldPath = Path.GetTempFileName();
        var newPath = Path.GetTempFileName();
        var output = new StringWriter();
        var error = new StringWriter();

        await File.WriteAllTextAsync(oldPath, "{\"openapi\":\"3.0.3\",\"info\":{\"title\":\"Old\",\"version\":\"1.0.0\"},\"paths\":{}}");
        await File.WriteAllTextAsync(newPath, "{\"openapi\":\"3.0.3\",\"info\":{\"title\":\"New\",\"version\":\"1.0.0\"},\"paths\":{\"/pets\":{\"get\":{\"responses\":{\"200\":{\"description\":\"ok\",\"content\":{\"application/json\":{\"schema\":{\"$ref\":\"#/components/schemas/MissingPet\"}}}}}}}},\"components\":{\"schemas\":{\"Pet\":{\"type\":\"object\"}}}}");

        try
        {
            var exitCode = await CliApplication.RunAsync(["compare", "--old", oldPath, "--new", newPath], output, error);

            Assert.Equal(2, exitCode);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Contains("Failed to load new specification", error.ToString());
            Assert.Contains("Internal $ref target '\u001b[38;5;208m#/components/schemas/MissingPet\u001b[0m' does not exist", error.ToString());
        }
        finally
        {
            File.Delete(oldPath);
            File.Delete(newPath);
        }
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
            ["compare", "--old", "old.json", "--new", "new.json"],
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
            ["compare", "--old", "old.json", "--new", "new.json"],
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
            ["compare", "--old", "old.json", "--new", "new.json"],
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
            ["compare", "--old", "old.json", "--new", "new.json", "--output", "json"],
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
        Assert.Equal("endpoint:removed", firstFinding.GetProperty("ruleId").GetString());
        Assert.Equal("error", firstFinding.GetProperty("severity").GetString());
        Assert.Equal("Endpoint removed", firstFinding.GetProperty("message").GetString());
        Assert.False(firstFinding.TryGetProperty("schemaPath", out _));
        Assert.Equal("GET", firstFinding.GetProperty("operation").GetProperty("method").GetString());
        Assert.Equal("/alpha", firstFinding.GetProperty("operation").GetProperty("pathTemplate").GetString());

        var secondFinding = findings[1];
        Assert.Equal("output:new:nullable", secondFinding.GetProperty("ruleId").GetString());
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

        var exitCode = await CliApplication.RunAsync(["compare", "--old", oldPath, "--new", newPath, "--output", "json"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, error.ToString());

        using var document = JsonDocument.Parse(output.ToString());
        var findings = document.RootElement.GetProperty("findings");

        Assert.Equal(3, findings.GetArrayLength());
        Assert.Equal("endpoint:removed", findings[0].GetProperty("ruleId").GetString());
        Assert.Equal("GET", findings[0].GetProperty("operation").GetProperty("method").GetString());
        Assert.Equal("/orders", findings[0].GetProperty("operation").GetProperty("pathTemplate").GetString());

        Assert.Equal("response:new:status-code", findings[1].GetProperty("ruleId").GetString());
        Assert.Equal("GET", findings[1].GetProperty("operation").GetProperty("method").GetString());
        Assert.Equal("/pets", findings[1].GetProperty("operation").GetProperty("pathTemplate").GetString());

        Assert.Equal("endpoint:new", findings[2].GetProperty("ruleId").GetString());
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
                [new ApiFinding(ApiRuleId.NewResponseCode, ApiSeverity.Warning, "Response code added")]));

        var exitCode = await CliApplication.RunAsync(
            ["compare", "--old", "old.json", "--new", "new.json"],
            output,
            error,
            loader,
            engine);

        Assert.Equal(0, exitCode);
        Assert.Contains("response:new:status-code", output.ToString());
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

        var exitCode = await CliApplication.RunAsync(["compare", "--old", oldPath, "--new", newPath], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("input:new:required", output.ToString());
        Assert.Contains("input:new:optional", output.ToString());
        Assert.Contains("input:removed", output.ToString());
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

        var exitCode = await CliApplication.RunAsync(["compare", "--old", oldPath, "--new", newPath], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("query:new:required", output.ToString());
        Assert.Contains("query:new:optional", output.ToString());
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

        var exitCode = await CliApplication.RunAsync(["compare", "--old", oldPath, "--new", newPath], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("output:removed", output.ToString());
        Assert.Contains("output:new:nullable", output.ToString());
        Assert.Contains("output:new:non-nullable", output.ToString());
        Assert.Contains("output:new:enum-value", output.ToString());
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

        var exitCode = await CliApplication.RunAsync(["compare", "--old", oldPath, "--new", newPath], output, error);

        Assert.Equal(1, exitCode);
        Assert.Contains("endpoint:removed", output.ToString());
        Assert.Contains("endpoint:new", output.ToString());
        Assert.Contains("response:new:status-code", output.ToString());
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