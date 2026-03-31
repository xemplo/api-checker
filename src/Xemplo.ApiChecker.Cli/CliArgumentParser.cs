using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Cli;

public static class CliArgumentParser
{
    public static CliParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? oldSource = null;
        string? newSource = null;
        var outputMode = CliOutputMode.Text;
        var hasOutputMode = false;
        string? configPath = null;
        var ruleOverrides = new Dictionary<ApiRuleId, ApiSeverity>();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            switch (arg)
            {
                case "--old":
                    if (!string.IsNullOrWhiteSpace(oldSource))
                    {
                        return CliParseResult.Fail("Duplicate argument '--old'.");
                    }

                    if (!TryReadValue(args, ref index, out oldSource))
                    {
                        return CliParseResult.Fail("Missing value for --old.");
                    }

                    break;

                case "--new":
                    if (!string.IsNullOrWhiteSpace(newSource))
                    {
                        return CliParseResult.Fail("Duplicate argument '--new'.");
                    }

                    if (!TryReadValue(args, ref index, out newSource))
                    {
                        return CliParseResult.Fail("Missing value for --new.");
                    }

                    break;

                case "--config":
                    if (!string.IsNullOrWhiteSpace(configPath))
                    {
                        return CliParseResult.Fail("Duplicate argument '--config'.");
                    }

                    if (!TryReadValue(args, ref index, out configPath))
                    {
                        return CliParseResult.Fail("Missing value for --config.");
                    }

                    break;

                case "--output":
                    if (hasOutputMode)
                    {
                        return CliParseResult.Fail("Duplicate argument '--output'.");
                    }

                    if (!TryReadValue(args, ref index, out var outputModeText))
                    {
                        return CliParseResult.Fail("Missing value for --output.");
                    }

                    if (!TryParseOutputMode(outputModeText!, out outputMode, out var outputErrorMessage))
                    {
                        return CliParseResult.Fail(outputErrorMessage!);
                    }

                    hasOutputMode = true;

                    break;

                case "--rule":
                    if (!TryReadValue(args, ref index, out var ruleOverrideText))
                    {
                        return CliParseResult.Fail("Missing value for --rule.");
                    }

                    if (!TryParseRuleOverride(ruleOverrideText!, out var ruleId, out var severity, out var errorMessage))
                    {
                        return CliParseResult.Fail(errorMessage!);
                    }

                    ruleOverrides[ruleId] = severity;
                    break;

                default:
                    return CliParseResult.Fail($"Unknown argument '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(oldSource) || string.IsNullOrWhiteSpace(newSource))
        {
            return CliParseResult.Fail("Both --old and --new are required.");
        }

        return CliParseResult.Success(new CliOptions(oldSource, newSource, outputMode, configPath, ruleOverrides));
    }

    private static bool TryReadValue(string[] args, ref int index, out string? value)
    {
        var nextIndex = index + 1;
        if (nextIndex >= args.Length)
        {
            value = null;
            return false;
        }

        var nextValue = args[nextIndex];
        if (string.IsNullOrWhiteSpace(nextValue) || IsOptionToken(nextValue))
        {
            value = null;
            return false;
        }

        value = nextValue;
        index = nextIndex;
        return true;
    }

    private static bool IsOptionToken(string value)
    {
        return value.StartsWith("--", StringComparison.Ordinal);
    }

    private static bool TryParseRuleOverride(
        string text,
        out ApiRuleId ruleId,
        out ApiSeverity severity,
        out string? errorMessage)
    {
        ruleId = default;
        severity = default;
        errorMessage = null;

        var separatorIndex = text.IndexOf('=');
        if (separatorIndex <= 0 || separatorIndex == text.Length - 1)
        {
            errorMessage = $"Invalid rule override '{text}'. Expected <rule-id>=<severity>.";
            return false;
        }

        var ruleText = text[..separatorIndex].Trim();
        var severityText = text[(separatorIndex + 1)..].Trim();

        if (!ApiRuleIdExtensions.TryParseIdentifier(ruleText, out ruleId))
        {
            errorMessage = $"Rule '{ruleText}' is not supported.";
            return false;
        }

        if (!Enum.TryParse<ApiSeverity>(severityText, ignoreCase: true, out severity)
            || !Enum.IsDefined(severity))
        {
            errorMessage = $"Severity '{severityText}' is not supported.";
            return false;
        }

        return true;
    }

    private static bool TryParseOutputMode(
        string text,
        out CliOutputMode outputMode,
        out string? errorMessage)
    {
        outputMode = default;
        errorMessage = null;

        if (!Enum.TryParse<CliOutputMode>(text, ignoreCase: true, out outputMode)
            || !Enum.IsDefined(outputMode))
        {
            errorMessage = $"Output mode '{text}' is not supported. Use 'text' or 'json'.";
            return false;
        }

        return true;
    }
}