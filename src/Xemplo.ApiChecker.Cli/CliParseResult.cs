namespace Xemplo.ApiChecker.Cli;

public sealed record CliParseResult(CliOptions? Options, string? ErrorMessage)
{
    public bool IsSuccess => Options is not null && string.IsNullOrWhiteSpace(ErrorMessage);

    public static CliParseResult Success(CliOptions options)
    {
        return new CliParseResult(options, null);
    }

    public static CliParseResult Fail(string errorMessage)
    {
        return new CliParseResult(null, errorMessage);
    }
}