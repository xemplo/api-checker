namespace Xemplo.ApiChecker.Core;

public static class ApiRuleCatalog
{
    private static readonly ApiRuleDescriptor[] RuleDescriptors =
    [
        new(ApiRuleId.NewRequiredInput, "input:new:required", ApiRuleFamily.Input, ApiSeverity.Error),
        new(ApiRuleId.NewOptionalInput, "input:new:optional", ApiRuleFamily.Input, ApiSeverity.Warning),
        new(ApiRuleId.NewNullableOutput, "output:new:nullable", ApiRuleFamily.Output, ApiSeverity.Warning),
        new(ApiRuleId.NewNonNullableOutput, "output:new:non-nullable", ApiRuleFamily.Output, ApiSeverity.Warning),
        new(ApiRuleId.NewEnumOutput, "output:new:enum-value", ApiRuleFamily.Output, ApiSeverity.Warning),
        new(ApiRuleId.NewRequiredQueryParam, "query:new:required", ApiRuleFamily.Query, ApiSeverity.Error),
        new(ApiRuleId.NewOptionalQueryParam, "query:new:optional", ApiRuleFamily.Query, ApiSeverity.Warning),
        new(ApiRuleId.RemovedInput, "input:removed", ApiRuleFamily.Input, ApiSeverity.Error),
        new(ApiRuleId.RemovedOutput, "output:removed", ApiRuleFamily.Output, ApiSeverity.Error),
        new(ApiRuleId.NewResponseCode, "response:new:status-code", ApiRuleFamily.Response, ApiSeverity.Warning),
        new(ApiRuleId.NewEndpoint, "endpoint:new", ApiRuleFamily.Endpoint, ApiSeverity.Warning),
        new(ApiRuleId.EndpointRemoved, "endpoint:removed", ApiRuleFamily.Endpoint, ApiSeverity.Error)
    ];

    private static readonly IReadOnlyDictionary<ApiRuleId, ApiRuleDescriptor> DescriptorsById =
        RuleDescriptors.ToDictionary(static descriptor => descriptor.Id);

    private static readonly IReadOnlyDictionary<string, ApiRuleDescriptor> DescriptorsByIdentifier =
        RuleDescriptors.ToDictionary(static descriptor => descriptor.Identifier, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<ApiRuleFamily, ApiRuleDescriptor[]> DescriptorsByFamily =
        RuleDescriptors
            .GroupBy(static descriptor => descriptor.Family)
            .ToDictionary(static group => group.Key, static group => group.ToArray());

    public static IReadOnlyList<ApiRuleDescriptor> All { get; } = RuleDescriptors;

    public static ApiRuleDescriptor GetDescriptor(ApiRuleId ruleId)
    {
        return DescriptorsById.TryGetValue(ruleId, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(ruleId), ruleId, "Unsupported rule identifier.");
    }

    public static IReadOnlyList<ApiRuleDescriptor> GetDescriptors(ApiRuleFamily family)
    {
        return DescriptorsByFamily.TryGetValue(family, out var descriptors)
            ? descriptors
            : [];
    }

    public static bool TryGetDescriptor(string? identifier, out ApiRuleDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            descriptor = default!;
            return false;
        }

        return DescriptorsByIdentifier.TryGetValue(identifier.Trim(), out descriptor!);
    }

    public static IReadOnlyDictionary<ApiRuleId, ApiSeverity> CreateDefaultSeverities()
    {
        return RuleDescriptors.ToDictionary(static descriptor => descriptor.Id, static descriptor => descriptor.DefaultSeverity);
    }
}

public enum ApiRuleFamily
{
    Input,
    Query,
    Output,
    Response,
    Endpoint
}

public sealed record ApiRuleDescriptor(
    ApiRuleId Id,
    string Identifier,
    ApiRuleFamily Family,
    ApiSeverity DefaultSeverity);