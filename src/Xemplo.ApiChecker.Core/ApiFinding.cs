namespace Xemplo.ApiChecker.Core;

public sealed record ApiFinding(
    ApiRuleId RuleId,
    ApiSeverity Severity,
    string Message,
    ApiOperationIdentity? Operation = null,
    string? SchemaPath = null,
    string? StatusCode = null,
    string? AcceptedReason = null)
{
    public bool IsAccepted => AcceptedReason is not null;
}
