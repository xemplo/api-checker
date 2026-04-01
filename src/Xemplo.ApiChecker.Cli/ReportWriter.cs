using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Cli;

public static class ReportWriter
{
    public static Task WriteAsync(TextWriter output, CompareCliOptions options, ApiComparisonResult result)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(result);

        return options.OutputMode switch
        {
            CliOutputMode.Json => JsonReportWriter.WriteAsync(output, options, result),
            _ => TextReportWriter.WriteAsync(output, options, result)
        };
    }
}