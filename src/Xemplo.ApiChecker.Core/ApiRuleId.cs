namespace Xemplo.ApiChecker.Core;

public enum ApiRuleId
{
    NewRequiredInput,
    NewOptionalInput,
    NewNullableOutput,
    NewNonNullableOutput,
    NewEnumOutput,
    NewRequiredQueryParam,
    NewOptionalQueryParam,
    RemovedInput,
    RemovedOutput,
    NewResponseCode,
    NewEndpoint,
    EndpointRemoved
}