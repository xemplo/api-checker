using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Cli;

public static class TextReportWriter
{
    public static async Task WriteAsync(TextWriter output, CompareCliOptions options, ApiComparisonResult result)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(result);

        await output.WriteLineAsync($"Comparing {options.OldSource} -> {options.NewSource}");

        if (result.Findings.Count == 0)
        {
            await output.WriteLineAsync("No findings.");
        }
        else
        {
            var acceptedCount = result.Findings.Count(static finding => finding.IsAccepted);
            var acceptedSummary = acceptedCount == 0 ? string.Empty : $" ({acceptedCount} accepted)";
            await output.WriteLineAsync($"Findings: {result.Findings.Count}{acceptedSummary}");
        }

        foreach (var finding in result.Findings)
        {
            var location = FormatLocation(finding.Operation?.Method, finding.Operation?.PathTemplate, finding.StatusCode);
            var schemaPath = string.IsNullOrWhiteSpace(finding.SchemaPath) ? string.Empty : $" ({finding.SchemaPath})";
            var accepted = finding.IsAccepted ? $" Accepted: {finding.AcceptedReason}" : string.Empty;
            await output.WriteLineAsync($"- {finding.Severity}: {finding.RuleId.GetIdentifier()}{location}{schemaPath} {finding.Message}{accepted}");
        }

        foreach (var change in result.UnusedAcceptedChanges)
        {
            var location = FormatLocation(change.Operation, change.StatusCode);
            var schemaPaths = change.SchemaPaths is null ? string.Empty : $" ({string.Join(", ", change.SchemaPaths)})";
            await output.WriteLineAsync(
                $"- {ApiSeverity.Warning}: Accepted change no longer needed: {change.RuleId.GetIdentifier()}{location}{schemaPaths} {change.Reason}");
        }
    }

    private static string FormatLocation(params string?[] parts)
    {
        var text = string.Join(' ', parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
        return text.Length == 0 ? string.Empty : $" [{text}]";
    }
}
