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
            engine ??= new ApiComparisonEngine();
            ruleProfile ??= await ResolveRuleProfileAsync(parseResult.Options!, workingDirectory, cancellationToken);

            var oldResult = await loader.LoadAsync(parseResult.Options!.OldSource, cancellationToken);
            var newResult = await loader.LoadAsync(parseResult.Options.NewSource, cancellationToken);

            if (!oldResult.IsSuccess || !newResult.IsSuccess)
            {
                await WriteLoadFailuresAsync(error, "old", parseResult.Options.OldSource, oldResult);
                await WriteLoadFailuresAsync(error, "new", parseResult.Options.NewSource, newResult);
                return 2;
            }

            var comparisonInput = new ApiComparisonInput(oldResult.Specification!, newResult.Specification!);
            var comparisonResult = engine.Compare(comparisonInput, ruleProfile);

            await ReportWriter.WriteAsync(output, parseResult.Options, comparisonResult);
            return comparisonResult.HasErrorFindings ? 1 : 0;
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

    private static async Task WriteLoadFailuresAsync(TextWriter error, string label, string source, ApiSpecificationLoadResult result)
    {
        if (result.IsSuccess)
        {
            return;
        }

        await error.WriteLineAsync($"Failed to load {label} specification '{source}':");

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
        CliOptions options,
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