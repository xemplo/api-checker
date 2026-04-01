namespace Xemplo.ApiChecker.Cli;

public static class CliUsage
{
        public const string Text = """
                Usage:
                    api-checker compare --old <path-or-url> --new <path-or-url> [--config <path>] [--output text|json] [--rule <rule-id>=<severity> ...]
                    api-checker validate <path-or-url>
                """;
}