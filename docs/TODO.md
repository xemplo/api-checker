# API Checker Task Breakdown

This backlog breaks the requirements in [requirements.md](d:\src\experiments\api-checker\docs\requirements.md) into individually executable implementation tasks for the first deliverable.

All tasks implicitly include unit and fixture coverage, verification of acceptance criteria, and any documentation updates needed to keep the slice usable.

## Proposed Tasks

1. **Create the reusable solution skeleton**
   - Status: Completed (2026-03-30)
   - Blockers: None
   - User Stories: 1, 30, 31
    - Description: Create the .NET solution structure for a reusable comparison library, a thin CLI entrypoint, and a test project. All solution, project, assembly, and namespace names created in this task should use the `Xemplo.` prefix so the tool sits under a single company namespace. Define the core domain contracts for findings, rule identifiers, severities, operation identities, comparison inputs, and comparison results so later tasks can build on a stable engine boundary without leaking CLI concepts into the library.
   - Acceptance Criteria:
     - The repository contains a solution with separate projects for the comparison engine, CLI, and tests.
       - All projects and default namespaces created for the solution use the `Xemplo.` prefix.
     - The comparison engine exposes library-friendly types and APIs without dependencies on console formatting, argument parsing, or process exit codes.
     - The rule identifiers and supported severities from the requirements are represented in code.
     - The core result model can carry stable finding data needed by both text and JSON output.

2. **Implement spec source loading and OpenAPI document parsing**
   - Status: Completed (2026-03-30)
   - Blockers: 1
   - User Stories: 1, 4, 29, 30, 31
   - Description: Add the ability to load the old and new specifications from either local paths or unauthenticated HTTP or HTTPS URLs, then parse them as OpenAPI 3.0 or 3.1 documents. This task should establish the error model for invalid inputs, unsupported Swagger/OpenAPI 2.0 documents, fetch failures, and parse failures.
   - Acceptance Criteria:
     - Spec loading supports local file paths and unauthenticated HTTP or HTTPS URLs.
     - OpenAPI 3.0 and 3.1 documents are accepted.
     - Swagger/OpenAPI 2.0 inputs are rejected with a runtime failure outcome rather than being compared.
  - Specs that rely on external `$ref` targets are rejected with a runtime failure outcome rather than being compared.
     - Failures from missing files, failed downloads, malformed documents, and unsupported versions are surfaced distinctly to the caller.
     - The engine can accept already-resolved documents without forcing callers through CLI-specific source handling.

3. **Deliver an early end-to-end CLI shell**
   - Status: Completed (2026-03-30)
  - Blockers: 1, 2
  - User Stories: 1, 4, 26, 29
  - Description: Put a usable CLI in place early so the team can run the tool after each comparison slice lands. This task should provide the basic command surface, invoke the current engine capabilities, and render a simple text result so later tasks can extend an already-runnable product instead of waiting for a final integration phase.
  - Acceptance Criteria:
    - The CLI can be invoked with the two required spec inputs.
    - The CLI loads specs through the shared loading pipeline rather than duplicating logic.
    - The CLI can execute the currently available comparison flow and print a readable text result.
    - Runtime failures are surfaced through the CLI in a diagnosable way.
    - Later engine and configuration tasks can extend this CLI incrementally without reshaping the library boundary.

4. **Build endpoint and response matching foundations**
   - Status: Completed (2026-03-30)
   - Blockers: 1, 2
   - User Stories: 5, 6, 8, 18, 19, 20, 34
   - Description: Implement the deterministic matching layer that aligns operations by exact HTTP method plus case-insensitive path template and aligns responses within matched operations by exact status code plus exact JSON media type. This is the core comparison map the rule engine will consume, including treatment of method changes as one removed endpoint plus one new endpoint.
   - Acceptance Criteria:
     - Operation identity uses exact HTTP method plus exact case-insensitive path template.
     - A method change on the same path is treated as an unmatched old operation and an unmatched new operation.
  - Response comparison within a matched operation uses exact explicit status code plus exact JSON media type, with `default` acting as the fallback match for non-explicit response codes.
     - Only JSON media types relevant to v1 are selected for body comparison.
     - Unmatched operations and unmatched response codes are available to downstream rule evaluation without heuristic rematching.

5. **Add conservative JSON schema analysis primitives**
   - Status: Proposed
   - Blockers: 1, 2
   - User Stories: 7, 8, 28, 33, 35, 36
   - Description: Create the schema inspection utilities that later rule tasks will reuse to reason about JSON object properties, requiredness, nullability, defaults, enum values, and ambiguity. This task should codify the product’s conservative evaluation stance so uncertain cases are identified explicitly instead of producing speculative breaking-change findings.
   - Acceptance Criteria:
  - JSON schema traversal supports the property-level comparisons needed for the v1 rule set, including nested objects and nested arrays.
     - Property names and enum values are treated as case-sensitive.
     - Renames are not inferred and are represented as removals plus additions.
   - The analysis layer applies `readOnly` and `writeOnly` semantics conservatively so request-side and response-side rule tasks only inspect fields that are clearly in scope.
     - The analysis layer can distinguish clearly proven conditions from ambiguous ones caused by nullability, defaults, or schema composition.
     - Non-JSON bodies and unsupported parameter categories are excluded from the analysis surface.

6. **Implement request body compatibility rules**
   - Status: Proposed
   - Blockers: 3, 4, 5
   - User Stories: 9, 10, 11, 28, 32, 33, 35, 36
   - Description: Evaluate matched operations for request-side compatibility changes in JSON request bodies only, and wire those findings into the runnable CLI. This task covers `NewRequiredInput`, `NewOptionalInput`, and `RemovedInput`, creating the first partial working solution where the tool can be run end-to-end for body-rule scenarios.
   - Acceptance Criteria:
     - JSON request body changes produce findings for new required fields, new optional fields, and removed fields.
     - Required versus optional classification respects nullability and default information conservatively.
     - Ambiguous cases do not produce error-level findings unless the rule condition is clearly proven.
     - The CLI reports request body findings through its existing text output flow.
     - After this task, the tool can be run end-to-end for supported body-rule comparisons.

7. **Implement query parameter compatibility rules**
   - Status: Proposed
   - Blockers: 3, 4, 5
   - User Stories: 12, 13, 28, 32, 33, 35, 36
   - Description: Evaluate matched operations for query parameter compatibility changes only, and extend the runnable CLI so those findings appear in normal tool execution. This task covers `NewRequiredQueryParam` and `NewOptionalQueryParam` while keeping path, header, and cookie parameters out of scope for v1.
   - Acceptance Criteria:
    - Query parameter changes produce findings for new required parameters and new optional parameters.
  - Query parameter comparison uses the effective parameter set after applying standard OpenAPI path-item and operation-level override behavior.
    - Path, header, and cookie parameters are ignored in v1.
    - Required versus optional classification respects nullability and default information conservatively.
    - Ambiguous cases do not produce error-level findings unless the rule condition is clearly proven.
     - The CLI reports query parameter findings alongside any already-supported findings.
     - After this task, the tool can be run end-to-end for supported body and query-parameter scenarios.

8. **Implement response body compatibility rules**
   - Status: Proposed
   - Blockers: 3, 4, 5
   - User Stories: 14, 15, 16, 17, 28, 32, 33, 34, 35, 36
   - Description: Evaluate matched JSON responses for response-body compatibility changes, and extend the runnable CLI so those findings become part of normal tool output. This task covers `RemovedOutput`, `NewNullableOutput`, `NewNonNullableOutput`, and `NewEnumOutput`, producing structured findings without introducing heuristic rematching.
   - Acceptance Criteria:
     - Response body changes produce findings for removed fields, new nullable fields, new non-nullable fields, and added enum values.
     - Response containers are still compared even when schemas differ substantially.
     - Property and enum comparisons stay case-sensitive.
     - Ambiguous cases are handled conservatively.
     - The CLI reports response body findings alongside any already-supported findings.
     - After this task, the tool can be run end-to-end for supported request, query, and response-body scenarios.

9. **Implement endpoint and response code change rules**
   - Status: Proposed
   - Blockers: 3, 4
   - User Stories: 18, 19, 20, 28, 32, 34
   - Description: Evaluate unmatched operations and unmatched response codes to produce the surface-level compatibility findings for `NewResponseCode`, `NewEndpoint`, and `EndpointRemoved`, and wire those findings into the runnable CLI. This task completes the non-schema rule set using the established matching model.
   - Acceptance Criteria:
  - Newly introduced response codes in matched operations produce `NewResponseCode` findings unless they are already covered by a matching `default` response in the other spec.
     - Unmatched new operations produce `NewEndpoint` findings.
     - Unmatched old operations produce `EndpointRemoved` findings.
     - Method changes on the same path are represented as one removed endpoint plus one new endpoint.
     - The CLI reports endpoint and response-code findings alongside any already-supported findings.
     - After this task, the CLI exposes the full v1 rule set through its text output path.

10. **Implement configuration resolution and rule severity layering**
   - Status: Proposed
   - Blockers: 1
   - User Stories: 21, 22, 23, 24, 25, 29, 30, 31
    - Description: Add the configuration model and resolution pipeline for built-in defaults, optional local `api-rules.json`, explicit `--config` input, and partial CLI rule overrides. This task should also define and implement the repeated `--rule <RuleId>=<severity>` CLI syntax so override parsing, validation, and layering all live with the configuration behavior they affect. The task should establish the effective rule set consumed by the engine while keeping the configuration API usable from non-CLI callers.
   - Acceptance Criteria:
     - Built-in default severities match the requirements document.
  - A local `api-rules.json` file is auto-discovered only from the current working directory.
     - An explicit config path overrides local auto-discovery.
  - CLI rule overrides use repeated `--rule <RuleId>=<severity>` arguments, replace only explicitly named rules, and leave the rest unchanged.
       - Invalid `--rule <RuleId>=<severity>` inputs are rejected with a clear runtime failure outcome.
     - Invalid configuration content is rejected with a clear runtime failure outcome.

11. **Complete the CLI surface, reporters, and CI exit semantics**
   - Status: Proposed
  - Blockers: 3, 6, 7, 8, 9, 10
   - User Stories: 1, 2, 3, 4, 26, 27, 29
  - Description: Expand the early CLI shell into the full v1 product surface by adding configuration handling, structured JSON output, and the required CI exit semantics. This task finishes the command-line contract after the comparison engine capabilities are in place.
   - Acceptance Criteria:
     - The CLI requires exactly two spec inputs representing the old and new specs.
  - The CLI supports `--config`, output mode selection, and repeated `--rule <RuleId>=<severity>` overrides.
     - Text output is the default presentation.
     - JSON output includes stable fields for rule id, severity, operation identity, message, and relevant schema location details.
   - The process exits with `0` when there are no error-level findings, `1` when one or more error-level findings exist, and `2` for runtime failures.
  - Text and JSON reporters emit findings in a stable deterministic order.

## Coverage Summary

- Reusable architecture and future package boundary: 1, 2, 10, 11
- Early prototype CLI: 3
- Deterministic matching and conservative analysis: 4, 5
- Incremental CLI-backed request body rules: 6
- Incremental CLI-backed query parameter rules: 7
- Incremental CLI-backed response body rules: 8
- Incremental CLI-backed response-code and endpoint rules: 9
- Config precedence and severity overrides: 10
- Final CLI output and exit behavior: 11
