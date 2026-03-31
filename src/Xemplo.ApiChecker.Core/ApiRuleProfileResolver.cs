using System.Text.Json;

namespace Xemplo.ApiChecker.Core;

public static class ApiRuleProfileResolver
{
    public static async Task<ApiRuleProfile> LoadFromFileAsync(
        string filePath,
        ApiRuleProfile? baseProfile = null,
        CancellationToken cancellationToken = default)
    {
        var overrides = await LoadOverridesFromFileAsync(filePath, cancellationToken);
        return ApplyOverrides(baseProfile ?? ApiRuleProfile.Default, overrides);
    }

    public static ApiRuleProfile ApplyOverrides(
        ApiRuleProfile baseProfile,
        IReadOnlyDictionary<ApiRuleId, ApiSeverity> overrides)
    {
        ArgumentNullException.ThrowIfNull(baseProfile);
        ArgumentNullException.ThrowIfNull(overrides);

        var merged = Enum.GetValues<ApiRuleId>()
            .ToDictionary(static ruleId => ruleId, baseProfile.GetSeverity);

        foreach (var entry in overrides)
        {
            merged[entry.Key] = entry.Value;
        }

        return new ApiRuleProfile(merged);
    }

    public static async Task<IReadOnlyDictionary<ApiRuleId, ApiSeverity>> LoadOverridesFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ApiRuleProfileConfigurationException("Configuration file path must be provided.");
        }

        if (!File.Exists(filePath))
        {
            throw new ApiRuleProfileConfigurationException($"Configuration file '{filePath}' was not found.");
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(filePath, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new ApiRuleProfileConfigurationException($"Failed to read configuration file '{filePath}': {exception.Message}");
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ApiRuleProfileConfigurationException("Configuration file root must be a JSON object.");
            }

            if (!document.RootElement.TryGetProperty("rules", out var rulesElement)
                || rulesElement.ValueKind != JsonValueKind.Object)
            {
                throw new ApiRuleProfileConfigurationException("Configuration file must contain a 'rules' object.");
            }

            var overrides = new Dictionary<ApiRuleId, ApiSeverity>();
            foreach (var property in rulesElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    throw new ApiRuleProfileConfigurationException(
                        $"Rule '{property.Name}' must have a string severity value.");
                }

                if (!Enum.TryParse<ApiRuleId>(property.Name, ignoreCase: true, out var ruleId)
                    || !Enum.IsDefined(ruleId))
                {
                    throw new ApiRuleProfileConfigurationException($"Rule '{property.Name}' is not supported.");
                }

                var severityText = property.Value.GetString();
                if (!Enum.TryParse<ApiSeverity>(severityText, ignoreCase: true, out var severity)
                    || !Enum.IsDefined(severity))
                {
                    throw new ApiRuleProfileConfigurationException(
                        $"Severity '{severityText}' is not supported for rule '{property.Name}'.");
                }

                overrides[ruleId] = severity;
            }

            return overrides;
        }
        catch (JsonException exception)
        {
            throw new ApiRuleProfileConfigurationException(
                $"Configuration file '{filePath}' is not valid JSON: {exception.Message}");
        }
    }
}