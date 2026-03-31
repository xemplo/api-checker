namespace Xemplo.ApiChecker.Core;

public static class ApiRuleIdExtensions
{
    public static string GetIdentifier(this ApiRuleId ruleId)
    {
        return ApiRuleCatalog.GetDescriptor(ruleId).Identifier;
    }

    public static bool TryParseIdentifier(string? text, out ApiRuleId ruleId)
    {
        if (ApiRuleCatalog.TryGetDescriptor(text, out var descriptor))
        {
            ruleId = descriptor.Id;
            return true;
        }

        ruleId = default;
        return false;
    }
}