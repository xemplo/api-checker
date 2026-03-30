using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Cli;

public static class TextReportWriter
{
    public static async Task WriteAsync(TextWriter output, CliOptions options, ApiComparisonResult result)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(result);

        await output.WriteLineAsync($"Comparing {options.OldSource} -> {options.NewSource}");

        if (result.Findings.Count == 0)
        {
            await output.WriteLineAsync("No findings.");
            return;
        }

        await output.WriteLineAsync($"Findings: {result.Findings.Count}");

        foreach (var finding in result.Findings)
        {
            var location = finding.Operation is null ? string.Empty : $" [{finding.Operation.Method} {finding.Operation.PathTemplate}]";
            await output.WriteLineAsync($"- {finding.Severity}: {finding.RuleId}{location} {finding.Message}");
        }
    }
}