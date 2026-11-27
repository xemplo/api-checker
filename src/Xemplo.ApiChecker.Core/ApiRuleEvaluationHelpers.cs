namespace Xemplo.ApiChecker.Core;

internal static class ApiRuleEvaluationHelpers
{
    public static ApiFinding? CreateFinding(
        ApiRuleId ruleId,
        ApiRuleProfile ruleProfile,
        string message,
        ApiOperationIdentity operation,
        string schemaPath,
        string? statusCode = null)
    {
        var severity = ruleProfile.GetSeverity(ruleId);
        return severity == ApiSeverity.Off
            ? null
            : new ApiFinding(ruleId, severity, message, operation, schemaPath, statusCode);
    }

    public static ApiFinding? CreateOperationFinding(
        ApiRuleId ruleId,
        ApiRuleProfile ruleProfile,
        string message,
        ApiOperationIdentity operation,
        string? statusCode = null)
    {
        var severity = ruleProfile.GetSeverity(ruleId);
        return severity == ApiSeverity.Off
            ? null
            : new ApiFinding(ruleId, severity, message, operation, StatusCode: statusCode);
    }
}