namespace Xemplo.ApiChecker.Core;

public enum ApiRuleId
{
    NewRequiredInput,
    NewOptionalInput,
    UpdatedRequiredInput,
    UpdatedOptionalInput,
    NewNullableOutput,
    NewNonNullableOutput,
    UpdatedNullableOutput,
    UpdatedNonNullableOutput,
    NewEnumOutput,
    NewRequiredQueryParam,
    NewOptionalQueryParam,
    RemovedInput,
    RemovedOutput,
    NewResponseCode,
    NewEndpoint,
    UpdatedEndpointId,
    EndpointRemoved
}