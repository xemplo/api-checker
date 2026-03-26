## Overview

The product is a .NET command-line tool that compares two OpenAPI specifications and reports whether the newer specification introduces API changes that should be treated as success, warning, or error conditions according to configurable compatibility rules.

From the user's perspective, the tool is intended to answer a practical CI/CD question: "Did this API contract change in a way that should block my build, or at least be surfaced for review?" The first deliverable focuses on a conservative baseline that avoids aggressive guesses when the specification is ambiguous.

### Solution

The solution is a reusable OpenAPI comparison engine exposed through a thin .NET tool CLI.

The CLI accepts an old and a new OpenAPI specification from either local file paths or unauthenticated URLs. It loads an optional JSON configuration file, applies any CLI rule overrides, evaluates the requested compatibility rules against matched operations and responses, prints findings in either human-readable text or JSON, and exits with a status code suitable for CI use.

The engine compares operations by exact HTTP method plus exact case-insensitive OpenAPI path template. Within matched operations, it evaluates JSON request bodies, JSON response bodies, query parameters, and response code additions according to the configured rules. Unmatched operations can also produce endpoint-level findings.

## User Stories

1. As an API maintainer, I want to compare an old and new OpenAPI specification, so that I can detect contract changes before publishing them.
2. As a build engineer, I want the tool to return a failing exit code when error-level findings are present, so that CI can automatically block breaking changes.
3. As a build engineer, I want warnings to remain non-failing by default, so that additive or lower-risk changes are visible without blocking delivery.
4. As an API maintainer, I want to provide the old and new specs as either local files or URLs, so that the tool works in both local development and automated pipelines.
5. As an API maintainer, I want endpoint matching to use exact method plus path template identity, so that comparison behavior is deterministic and easy to reason about.
6. As an API maintainer, I want path-template matching to be case-insensitive, so that harmless path casing changes do not create duplicate endpoint identities.
7. As an API maintainer, I want request and response schema property names to remain case-sensitive, so that actual payload field changes are treated accurately.
8. As an API maintainer, I want renamed fields to be treated as one removal plus one addition, so that the tool avoids fragile rename heuristics.
9. As an API maintainer, I want the tool to detect a new required request-body field, so that I can identify likely breaking input changes.
10. As an API maintainer, I want the tool to detect a new optional request-body field, so that I can review additive input changes.
11. As an API maintainer, I want the tool to detect removed request-body fields, so that I can identify contract reductions that may affect callers.
12. As an API maintainer, I want the tool to detect new required query parameters, so that I can catch likely breaking URL contract changes.
13. As an API maintainer, I want the tool to detect new optional query parameters, so that I can review additive query contract changes.
14. As an API maintainer, I want the tool to detect removed response-body fields, so that I can catch likely breaking response changes for consumers.
15. As an API maintainer, I want the tool to detect new nullable response fields, so that I can understand additive response changes with lower compatibility risk.
16. As an API maintainer, I want the tool to detect new non-nullable response fields, so that I can review response expansions that may affect strict clients.
17. As an API maintainer, I want the tool to detect new enum values in responses, so that I can identify places where consumer switch statements or validation may break.
18. As an API maintainer, I want the tool to detect newly introduced response codes, so that I can review behavioral changes in the API surface.
19. As an API maintainer, I want the tool to detect entirely new endpoints, so that I can track API surface growth.
20. As an API maintainer, I want the tool to detect removed endpoints, so that I can catch endpoint deletions that may be breaking.
21. As an API maintainer, I want to configure the severity of each rule independently, so that the tool reflects my team's compatibility policy.
22. As an API maintainer, I want sensible default severities when no config is provided, so that the tool is useful immediately.
23. As a developer, I want a local `api-rules.json` file to be picked up automatically, so that routine usage is simple.
24. As a developer, I want an explicit `--config` option to take precedence, so that pipeline jobs can use environment-specific rules.
25. As a developer, I want CLI rule overrides to replace only the rules I specify, so that I can make small changes without redefining the full config.
26. As a developer, I want text output by default, so that the tool is easy to read during local runs.
27. As a platform engineer, I want an optional JSON output mode, so that other tools can consume findings programmatically.
28. As an API maintainer, I want conservative rule evaluation when the spec is ambiguous, so that CI failures are not caused by speculative inferences.
29. As a developer, I want invalid configuration or malformed input to produce a distinct non-zero exit code, so that build failures are diagnosable.
30. As a future library consumer, I want the comparison engine to be reusable outside the CLI, so that the same logic can later be used from unit tests or other .NET code.
31. As a test author, I want the future package boundary to accept already-resolved specs and effective rule settings, so that tests can call the engine directly without emulating CLI concerns.
32. As a maintainer, I want fixture-based tests for each rule, so that later rule refinements do not introduce regressions.
33. As a maintainer, I want explicit coverage for ambiguous schema cases, so that conservative behavior stays consistent over time.
34. As a maintainer, I want consistent operation and response matching rules, so that findings remain stable across versions of the tool.
35. As a maintainer, I want the tool to focus on JSON body semantics in v1, so that the first deliverable stays narrow enough to implement and validate well.
36. As a maintainer, I want non-JSON bodies and unsupported parameter types excluded from v1, so that the initial rules are reliable rather than overly broad.

## Technical Requirements

1. The implementation must target a reusable architecture consisting of a core comparison library, a thin .NET tool CLI, and automated tests.
2. The core comparison engine must not depend on CLI-specific concepts such as console formatting, argument parsing, or process exit codes.
3. The CLI must accept exactly two required spec inputs representing the old and new contract versions.
4. The CLI input parameters must support both local file paths and unauthenticated HTTP or HTTPS URLs.
5. The CLI must support OpenAPI 3.0 and OpenAPI 3.1 documents in v1.
6. The implementation must not support Swagger/OpenAPI 2.0 in v1.
7. Input specifications must be self-contained in v1; external `$ref` targets are not supported and must produce a runtime failure outcome.
8. Operation identity must be defined as exact HTTP method plus exact case-insensitive path template.
9. A method change on the same path must be treated as an endpoint removal plus a new endpoint.
10. Schema property names and enum values must be treated as case-sensitive.
11. Request and response body rule evaluation must be limited to JSON media types in v1, specifically `application/json` and `application/*+json`.
12. Non-JSON media types, including multipart, form, and XML payloads, must be ignored in v1.
13. Parameter rule evaluation must be limited to query parameters in v1.
14. Path, header, and cookie parameter compatibility rules must be excluded from v1.
15. Response matching within a matched operation must first use exact explicit status-code equality plus exact JSON media type.
16. A `default` response must participate as a fallback match for response codes that are not explicitly declared in the other spec.
17. Response containers must still be compared even when the old and new schemas are radically different; schema differences must produce findings rather than rematching heuristics.
18. The engine must apply conservative evaluation when nullability, defaults, or schema composition make a rule uncertain.
19. The engine should only produce error-level findings when the specification clearly proves the rule condition.
20. Renames must not be inferred; a rename is represented as a removal plus an addition.
21. Schema traversal for the v1 rule set must include nested objects and nested arrays so findings can be attributed to stable schema paths.
22. Request-side rule evaluation must honor OpenAPI `readOnly` and `writeOnly` semantics conservatively, excluding `readOnly`-only fields from request compatibility checks unless the spec clearly proves otherwise.
23. Response-side rule evaluation must honor OpenAPI `readOnly` and `writeOnly` semantics conservatively, excluding `writeOnly`-only fields from response compatibility checks unless the spec clearly proves otherwise.
24. Unmatched new operations must produce `NewEndpoint` findings.
25. Unmatched old operations must produce `EndpointRemoved` findings.
26. Unmatched newly introduced response codes within matched operations must produce `NewResponseCode` findings unless they are already covered by a matching `default` response in the other spec.
27. Configuration precedence must be built-in defaults, then `api-rules.json` in the current working directory, then explicit `--config`, then partial CLI rule overrides.
28. CLI rule overrides must replace only explicitly named rules and leave all other effective rule values unchanged.
29. The CLI must support a repeated `--rule <RuleId>=<severity>` override syntax for per-rule severity changes.
30. The CLI must return exit code `0` when no error-level findings exist.
31. The CLI must return exit code `1` when one or more error-level findings exist.
32. The CLI must use exit code `2` for invalid configuration, invalid input, fetch failures, parse failures, unsupported external references, and similar runtime failures.
33. The CLI must support human-readable text output by default.
34. The CLI must support structured JSON output in v1.
35. The JSON output contract should include stable fields for rule id, severity, operation identity, message, and any relevant schema location details.
36. The implementation must include fixture-based automated tests covering each rule, matching behavior, config precedence, output behavior, nested-array traversal, external-reference failures, `readOnly` and `writeOnly` semantics, and representative ambiguity cases.

## Technical Decisions

- The first deliverable will be implemented as a reusable core library plus a thin .NET tool CLI plus tests.
- The CLI will compare exactly two specifications: an old spec and a new spec.
- The CLI input surface will support both local file paths and unauthenticated URLs.
- Input specifications must be self-contained in v1; external `$ref` targets will be rejected as runtime failures.
- Configuration will live in a dedicated JSON file format.
- Configuration discovery preference order will be: explicit `--config <path>`, then `api-rules.json` in the current working directory, then built-in defaults if no file exists.
- CLI rule overrides will use repeated `--rule <RuleId>=<severity>` arguments layered on top of the effective configuration.
- The first deliverable will support OpenAPI 3.0 and 3.1 only.
- Operation matching will use exact method plus exact case-insensitive path template.
- Request and response body comparison will be limited to JSON payloads.
- Parameter comparison will be limited to query parameters.
- Response comparison will match exact explicit status code plus exact JSON media type, with `default` acting as the fallback match for non-explicit response codes.
- The rule engine will use conservative evaluation and avoid speculative inference.
- Schema traversal will include nested arrays in addition to nested objects.
- Request and response analysis will honor `readOnly` and `writeOnly` semantics conservatively.
- Endpoint additions and removals are explicit first-class rules in v1.
- Output will default to text with an optional JSON mode.
- Runtime failures will use process exit code `2`.
- The engine boundary will be designed so a future NuGet package can expose the same functionality for unit-test usage.

### CLI Input Contract

The requirements-level CLI contract for v1 is:

- Required parameter: old specification source
- Required parameter: new specification source
- Optional parameter: `--config <path>`
- Optional parameter: output mode selection, including JSON output
- Optional parameter: one or more `--rule <RuleId>=<severity>` overrides

A representative invocation shape is:

```text
api-checker --old <path-or-url> --new <path-or-url> [--config <path>] [--output text|json] [--rule <RuleId>=<severity> ...]
```

The following requirements are fixed:

- Two spec inputs are required.
- Those inputs must accept path or URL values.
- `--config` must override local auto-discovery.
- Auto-discovery must look only for `api-rules.json` in the current working directory.
- Output mode must support at least `text` and `json`.
- CLI overrides must use repeated `--rule <RuleId>=<severity>` arguments.
- CLI overrides must allow setting severity per rule without redefining the entire rule set.

### Configuration File Structure

The configuration file must be a JSON document that expresses per-rule severities. The minimum required structure is a mapping from rule identifier to severity value.

A representative v1 structure is:

```json
{
  "rules": {
    "NewRequiredInput": "error",
    "NewOptionalInput": "warning",
    "NewNullableOutput": "warning",
    "NewNonNullableOutput": "warning",
    "NewEnumOutput": "warning",
    "NewRequiredQueryParam": "error",
    "NewOptionalQueryParam": "warning",
    "RemovedInput": "error",
    "RemovedOutput": "error",
    "NewResponseCode": "warning",
    "NewEndpoint": "warning",
    "EndpointRemoved": "error"
  }
}
```

Supported rule identifiers for v1 are:

- `NewRequiredInput`
- `NewOptionalInput`
- `NewNullableOutput`
- `NewNonNullableOutput`
- `NewEnumOutput`
- `NewRequiredQueryParam`
- `NewOptionalQueryParam`
- `RemovedInput`
- `RemovedOutput`
- `NewResponseCode`
- `NewEndpoint`
- `EndpointRemoved`

Supported severity values for v1 should be:

- `error`
- `warning`
- `off`

If neither config nor CLI overrides are supplied, the built-in defaults are:

- `Error`: `NewRequiredInput`, `NewRequiredQueryParam`, `RemovedInput`, `RemovedOutput`, `EndpointRemoved`
- `Warning`: `NewOptionalInput`, `NewNullableOutput`, `NewNonNullableOutput`, `NewEnumOutput`, `NewOptionalQueryParam`, `NewResponseCode`, `NewEndpoint`

### Rule Semantics Summary

- `NewRequiredInput`: a new request-body field exists and is not nullable and has no default.
- `NewOptionalInput`: a new request-body field exists and is nullable or has a default.
- `NewNullableOutput`: a new response-body field exists and is nullable.
- `NewNonNullableOutput`: a new response-body field exists and is not nullable.
- `NewEnumOutput`: a new enum value was added in a response-body field that clients may observe.
- `NewRequiredQueryParam`: a new query parameter exists and is not nullable and has no default.
- `NewOptionalQueryParam`: a new query parameter exists and is nullable or has a default.
- `RemovedInput`: a previously documented request-body field no longer exists.
- `RemovedOutput`: a previously documented response-body field no longer exists.
- `NewResponseCode`: a newly possible response code was added to a matched operation.
- If one side declares `default` and the other side declares a non-explicit response code, those responses should be compared as a matched pair before deciding whether `NewResponseCode` applies.
- `NewEndpoint`: a new operation exists in the new spec with no old-spec match by method and case-insensitive path template.
- `EndpointRemoved`: an old-spec operation no longer exists in the new spec by method and case-insensitive path template.

## Out of Scope

The following items are out of scope for the first deliverable:

- Swagger/OpenAPI 2.0 support
- Authenticated URL fetching or custom request-header support for spec downloads
- External `$ref` targets spanning multiple files or remote documents
- Rename detection heuristics for endpoints, parameters, or schema fields
- Non-JSON request or response body comparison
- Path, header, or cookie parameter compatibility rules
- Heuristic response rematching based on schema similarity
- Full semantic interpretation of all ambiguous OpenAPI constructs
- The future NuGet test-consumption package itself
- Integration with ASP.NET projects for live spec extraction in v1
- Any rule not explicitly listed in the v1 rule set

## Further Notes

The intended behavior is conservative by design. If the specification does not clearly prove that a rule condition is met, the tool should avoid raising an error-level finding based on inference alone. This is a deliberate product choice to create a trustworthy baseline before broadening coverage.

The future package scenario should influence implementation boundaries now, even though it is not a v1 deliverable. The comparison engine should expose a clean, reusable API that accepts already-resolved spec content and effective rule settings, then returns structured findings and summary status. This keeps the path open for a later NuGet package that can be consumed directly from unit tests.

The requirements document intentionally includes concrete CLI and configuration expectations because those are part of the product contract, even though implementation-level class names and file organization may change during delivery.
