namespace Xemplo.ApiChecker.Core;

public sealed record ApiFinding(
    ApiRuleId RuleId,
    ApiSeverity Severity,
    string Message,
    ApiOperationIdentity? Operation = null,
    string? SchemaPath = null);