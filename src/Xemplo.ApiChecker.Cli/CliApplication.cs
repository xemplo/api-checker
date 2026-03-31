using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Cli;

public static class CliApplication
{
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
            if (!oldResult.IsSuccess)
            {
                await error.WriteLineAsync($"Failed to load old specification '{parseResult.Options.OldSource}': {oldResult.Failure!.Message}");
                return 2;
            }

            var newResult = await loader.LoadAsync(parseResult.Options.NewSource, cancellationToken);
            if (!newResult.IsSuccess)
            {
                await error.WriteLineAsync($"Failed to load new specification '{parseResult.Options.NewSource}': {newResult.Failure!.Message}");
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