using System.Text.Json;
using System.Text.Json.Serialization;
using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Cli;

public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static Task WriteAsync(TextWriter output, CompareCliOptions options, ApiComparisonResult result)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(result);

        var document = new JsonReportDocument(
            options.OldSource,
            options.NewSource,
            result.HasErrorFindings,
            result.Findings.Select(static finding => new JsonFinding(
                finding.RuleId.GetIdentifier(),
                finding.Severity.ToString().ToLowerInvariant(),
                finding.Message,
                finding.Operation is null ? null : new JsonOperation(finding.Operation.Method, finding.Operation.PathTemplate),
                finding.SchemaPath)).ToArray());

        return output.WriteAsync(JsonSerializer.Serialize(document, SerializerOptions));
    }

    private sealed record JsonReportDocument(
        string OldSource,
        string NewSource,
        bool HasErrorFindings,
        IReadOnlyList<JsonFinding> Findings);

    private sealed record JsonFinding(
        string RuleId,
        string Severity,
        string Message,
        JsonOperation? Operation,
        string? SchemaPath);

    private sealed record JsonOperation(string Method, string PathTemplate);
}