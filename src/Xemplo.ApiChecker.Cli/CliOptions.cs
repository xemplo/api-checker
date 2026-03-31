using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Cli;

public sealed record CliOptions
{
    public CliOptions(
        string oldSource,
        string newSource,
        CliOutputMode outputMode = CliOutputMode.Text,
        string? configPath = null,
        IReadOnlyDictionary<ApiRuleId, ApiSeverity>? ruleOverrides = null)
    {
        OldSource = oldSource;
        NewSource = newSource;
        OutputMode = outputMode;
        ConfigPath = configPath;
        RuleOverrides = ruleOverrides is null
            ? new Dictionary<ApiRuleId, ApiSeverity>()
            : new Dictionary<ApiRuleId, ApiSeverity>(ruleOverrides);
    }

    public string OldSource { get; }

    public string NewSource { get; }

    public CliOutputMode OutputMode { get; }

    public string? ConfigPath { get; }

    public IReadOnlyDictionary<ApiRuleId, ApiSeverity> RuleOverrides { get; }
}