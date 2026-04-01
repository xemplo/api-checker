using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Cli;

public static class CliApplication
{
    private const string RedErrorLabel = "\u001b[31mERROR\u001b[0m";
    private const string OrangeSubjectFormat = "\u001b[38;5;208m{0}\u001b[0m";

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        IApiSpecificationLoader? loader = null,
        IApiComparisonEngine? engine = null,
        ApiRuleProfile? ruleProfile = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var parseResult = CliArgumentParser.Parse(args);
        if (!parseResult.IsSuccess)
        {
            await error.WriteLineAsync(parseResult.ErrorMessage);
            await error.WriteLineAsync(CliUsage.Text);
            return 2;
        }

        try
        {
            loader ??= new ApiSpecificationLoader();

            return parseResult.Options switch
            {
                CompareCliOptions compareOptions => await RunCompareAsync(compareOptions, output, error, loader, engine, ruleProfile, workingDirectory, cancellationToken),
                ValidateCliOptions validateOptions => await RunValidateAsync(validateOptions, output, error, loader, cancellationToken),
                _ => 2
            };
        }
        catch (ApiRuleProfileConfigurationException exception)
        {
            await error.WriteLineAsync($"Invalid configuration: {exception.Message}");
            return 2;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync($"Runtime failure: {exception.Message}");
            return 2;
        }
    }

    private static async Task<int> RunCompareAsync(
        CompareCliOptions options,
        TextWriter output,
        TextWriter error,
        IApiSpecificationLoader loader,
        IApiComparisonEngine? engine,
        ApiRuleProfile? ruleProfile,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        engine ??= new ApiComparisonEngine();
        ruleProfile ??= await ResolveRuleProfileAsync(options, workingDirectory, cancellationToken);

        var oldResult = await loader.LoadAsync(options.OldSource, cancellationToken);
        var newResult = await loader.LoadAsync(options.NewSource, cancellationToken);

        if (!oldResult.IsSuccess || !newResult.IsSuccess)
        {
            await WriteLoadFailuresAsync(error, "old", options.OldSource, oldResult);
            await WriteLoadFailuresAsync(error, "new", options.NewSource, newResult);
            return 2;
        }

        var comparisonInput = new ApiComparisonInput(oldResult.Specification!, newResult.Specification!);
        var comparisonResult = engine.Compare(comparisonInput, ruleProfile);

        await ReportWriter.WriteAsync(output, options, comparisonResult);
        return comparisonResult.HasErrorFindings ? 1 : 0;
    }

    private static async Task<int> RunValidateAsync(
        ValidateCliOptions options,
        TextWriter output,
        TextWriter error,
        IApiSpecificationLoader loader,
        CancellationToken cancellationToken)
    {
        var source = options.SpecificationSource;
        var result = await loader.LoadAsync(source, cancellationToken);

        if (!result.IsSuccess)
        {
            await WriteLoadFailuresAsync(error, null, source, result);
            return 2;
        }

        await output.WriteLineAsync($"Validated {source}");
        await output.WriteLineAsync("Specification is valid.");
        return 0;
    }

    private static async Task WriteLoadFailuresAsync(TextWriter error, string? label, string source, ApiSpecificationLoadResult result)
    {
        if (result.IsSuccess)
        {
            return;
        }

        var descriptor = string.IsNullOrWhiteSpace(label)
            ? "specification"
            : $"{label} specification";

        await error.WriteLineAsync($"Failed to load {descriptor} '{source}':");

        foreach (var failure in result.Failures)
        {
            await error.WriteLineAsync($"- {RedErrorLabel} {FormatFailureMessage(failure)}");
        }
    }

    private static string FormatFailureMessage(ApiSpecificationLoadFailure failure)
    {
        if (string.IsNullOrWhiteSpace(failure.HighlightedSubject))
        {
            return failure.Message;
        }

        var subjectIndex = failure.Message.IndexOf(failure.HighlightedSubject, StringComparison.Ordinal);
        if (subjectIndex < 0)
        {
            return failure.Message;
        }

        return string.Concat(
            failure.Message.AsSpan(0, subjectIndex),
            string.Format(OrangeSubjectFormat, failure.HighlightedSubject),
            failure.Message.AsSpan(subjectIndex + failure.HighlightedSubject.Length));
    }

    private static async Task<ApiRuleProfile> ResolveRuleProfileAsync(
        CompareCliOptions options,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        var currentDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Environment.CurrentDirectory
            : workingDirectory;

        var profile = ApiRuleProfile.Default;
        var discoveredConfigPath = Path.Combine(currentDirectory, "api-rules.json");

        if (File.Exists(discoveredConfigPath))
        {
            profile = await ApiRuleProfileResolver.LoadFromFileAsync(discoveredConfigPath, profile, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(options.ConfigPath))
        {
            var explicitConfigPath = Path.IsPathRooted(options.ConfigPath)
                ? options.ConfigPath
                : Path.Combine(currentDirectory, options.ConfigPath);

            profile = await ApiRuleProfileResolver.LoadFromFileAsync(explicitConfigPath, profile, cancellationToken);
        }

        return options.RuleOverrides.Count == 0
            ? profile
            : ApiRuleProfileResolver.ApplyOverrides(profile, options.RuleOverrides);
    }
}