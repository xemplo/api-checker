using Xemplo.ApiChecker.Core;

namespace Xemplo.ApiChecker.Cli;

public sealed record CliOptions
{
    private CliOptions(
        CliCommand command,
        string? compareOldSource,
        string? compareNewSource,
        string? specificationSource,
        CliOutputMode outputMode = CliOutputMode.Text,
        string? configPath = null,
        IReadOnlyDictionary<ApiRuleId, ApiSeverity>? ruleOverrides = null)
    {
        Command = command;
        OldSource = compareOldSource;
        NewSource = compareNewSource;
        SpecificationSource = specificationSource;
        OutputMode = outputMode;
        ConfigPath = configPath;
        RuleOverrides = ruleOverrides is null
            ? new Dictionary<ApiRuleId, ApiSeverity>()
            : new Dictionary<ApiRuleId, ApiSeverity>(ruleOverrides);
    }

    public static CliOptions Compare(
        string oldSource,
        string newSource,
        CliOutputMode outputMode = CliOutputMode.Text,
        string? configPath = null,
        IReadOnlyDictionary<ApiRuleId, ApiSeverity>? ruleOverrides = null)
    {
        return new CliOptions(CliCommand.Compare, oldSource, newSource, null, outputMode, configPath, ruleOverrides);
    }

    public static CliOptions Validate(string specificationSource)
    {
        return new CliOptions(CliCommand.Validate, null, null, specificationSource);
    }

    public CliCommand Command { get; }

    public string? OldSource { get; }

    public string? NewSource { get; }

    public string? SpecificationSource { get; }

    public CliOutputMode OutputMode { get; }

    public string? ConfigPath { get; }

    public IReadOnlyDictionary<ApiRuleId, ApiSeverity> RuleOverrides { get; }
}