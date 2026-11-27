using System.Text.Json;

namespace Xemplo.ApiChecker.Core;

public static class ApiRuleProfileResolver
{
    public static async Task<ApiRuleProfile> LoadFromFileAsync(
        string filePath,
        ApiRuleProfile? baseProfile = null,
        CancellationToken cancellationToken = default)
    {
        baseProfile ??= ApiRuleProfile.Default;
        var content = await ReadFileAsync(filePath, cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ApiRuleProfileConfigurationException("Configuration file root must be a JSON object.");
            }

            return Merge(baseProfile, ParseRules(root), ParseAcceptedChanges(root));
        }
        catch (JsonException exception)
        {
            throw new ApiRuleProfileConfigurationException(
                $"Configuration file '{filePath}' is not valid JSON: {exception.Message}");
        }
    }

    public static ApiRuleProfile ApplyOverrides(
        ApiRuleProfile baseProfile,
        IReadOnlyDictionary<ApiRuleId, ApiSeverity> overrides)
    {
        ArgumentNullException.ThrowIfNull(baseProfile);
        ArgumentNullException.ThrowIfNull(overrides);

        return Merge(baseProfile, overrides, []);
    }

    private static ApiRuleProfile Merge(
        ApiRuleProfile baseProfile,
        IReadOnlyDictionary<ApiRuleId, ApiSeverity> overrides,
        IReadOnlyList<ApiAcceptedChange> acceptedChanges)
    {
        var merged = Enum.GetValues<ApiRuleId>()
            .ToDictionary(static ruleId => ruleId, baseProfile.GetSeverity);

        foreach (var entry in overrides)
        {
            merged[entry.Key] = entry.Value;
        }

        return new ApiRuleProfile(merged, [.. baseProfile.AcceptedChanges, .. acceptedChanges]);
    }

    private static async Task<string> ReadFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ApiRuleProfileConfigurationException("Configuration file path must be provided.");
        }

        if (!File.Exists(filePath))
        {
            throw new ApiRuleProfileConfigurationException($"Configuration file '{filePath}' was not found.");
        }

        try
        {
            return await File.ReadAllTextAsync(filePath, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new ApiRuleProfileConfigurationException($"Failed to read configuration file '{filePath}': {exception.Message}");
        }
    }

    private static Dictionary<ApiRuleId, ApiSeverity> ParseRules(JsonElement root)
    {
        if (!root.TryGetProperty("rules", out var rulesElement)
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

            if (!ApiRuleIdExtensions.TryParseIdentifier(property.Name, out var ruleId))
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

    private static ApiAcceptedChange[] ParseAcceptedChanges(JsonElement root)
    {
        if (!root.TryGetProperty("accepted", out var acceptedElement))
        {
            return [];
        }

        if (acceptedElement.ValueKind != JsonValueKind.Array)
        {
            throw new ApiRuleProfileConfigurationException("Configuration 'accepted' must be an array.");
        }

        return acceptedElement.EnumerateArray().Select(ParseAcceptedChange).ToArray();
    }

    private static ApiAcceptedChange ParseAcceptedChange(JsonElement element, int index)
    {
        var label = $"accepted[{index}]";
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ApiRuleProfileConfigurationException($"{label} must be a JSON object.");
        }

        string? rule = null, reason = null, operation = null, status = null;
        IReadOnlyList<string>? paths = null;

        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "rule": rule = ReadString(property, label); break;
                case "reason": reason = ReadString(property, label); break;
                case "operation": operation = ReadString(property, label); break;
                case "status":
                    status = property.Value.ValueKind == JsonValueKind.Number
                        ? property.Value.GetRawText()
                        : ReadString(property, label);
                    break;
                case "path": paths = ReadPaths(property, label); break;
                default:
                    throw new ApiRuleProfileConfigurationException($"{label} has unsupported property '{property.Name}'.");
            }
        }

        if (rule is null)
        {
            throw new ApiRuleProfileConfigurationException($"{label} must have a 'rule'.");
        }

        if (!ApiRuleIdExtensions.TryParseIdentifier(rule, out var ruleId))
        {
            throw new ApiRuleProfileConfigurationException($"{label} rule '{rule}' is not supported.");
        }

        if (reason is null)
        {
            throw new ApiRuleProfileConfigurationException($"{label} must have a 'reason'.");
        }

        if (operation is not null && operation != "*" && !IsOperation(operation))
        {
            throw new ApiRuleProfileConfigurationException(
                $"{label} operation '{operation}' must be '*' or 'METHOD /path'.");
        }

        return new ApiAcceptedChange(ruleId, reason, operation, status, paths);
    }

    private static string ReadString(JsonProperty property, string label)
    {
        var value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
        return string.IsNullOrWhiteSpace(value)
            ? throw new ApiRuleProfileConfigurationException($"{label} '{property.Name}' must be a non-empty string.")
            : value;
    }

    private static string[] ReadPaths(JsonProperty property, string label)
    {
        var value = property.Value;
        string?[] paths = value.ValueKind switch
        {
            JsonValueKind.String => [value.GetString()],
            JsonValueKind.Array => value.EnumerateArray()
                .Select(static item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
                .ToArray(),
            _ => []
        };

        return paths.Length == 0 || paths.Any(string.IsNullOrWhiteSpace)
            ? throw new ApiRuleProfileConfigurationException(
                $"{label} 'path' must be a non-empty string or a non-empty list of non-empty strings.")
            : paths.OfType<string>().ToArray();
    }

    private static bool IsOperation(string operation)
    {
        var parts = operation.Split(' ');
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].StartsWith('/');
    }
}
