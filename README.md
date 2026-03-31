# Xemplo API Checker

`Xemplo.ApiChecker` is a .NET command-line tool for comparing two OpenAPI specifications and reporting compatibility findings using configurable rule severities.

- OpenAPI 3.0 and 3.1 only
- exactly two required inputs: `--old` and `--new`
- local files and unauthenticated `http` or `https` URLs
- JSON request/response body comparisons only
- query-parameter comparisons only
- endpoint additions/removals and new response-code detection
- conservative evaluation when the specification is ambiguous

## Usage

```text
api-checker --old <path-or-url> --new <path-or-url> [--config <path>] [--output text|json] [--rule <rule-id>=<severity> ...]
```

Examples:

```powershell
dotnet run --project .\src\Xemplo.ApiChecker.Cli -- --old .\tests\fixtures\request-body-old.json --new .\tests\fixtures\request-body-new.json
```

```powershell
dotnet run --project .\src\Xemplo.ApiChecker.Cli -- --old https://example.com/openapi-old.yaml --new https://example.com/openapi-new.yaml --output json
```

```powershell
dotnet run --project .\src\Xemplo.ApiChecker.Cli -- --old .\old.json --new .\new.json --config .\ci-rules.json --rule endpoint:new=off --rule response:new:status-code=error
```

## Rule Configuration

Rule precedence is:

1. built-in defaults
2. `api-rules.json` in the current working directory
3. explicit `--config <path>`
4. repeated `--rule <rule-id>=<severity>` overrides

Supported severities:

- `error`
- `warning`
- `off`

Supported rule identifiers:

- `input:new:required`
- `input:new:optional`
- `output:new:nullable`
- `output:new:non-nullable`
- `output:new:enum-value`
- `query:new:required`
- `query:new:optional`
- `input:removed`
- `output:removed`
- `response:new:status-code`
- `endpoint:new`
- `endpoint:removed`

Example `api-rules.json`:

```json
{
  "rules": {
    "input:new:required": "error",
    "input:new:optional": "warning",
    "output:new:nullable": "warning",
    "output:new:non-nullable": "warning",
    "output:new:enum-value": "warning",
    "query:new:required": "error",
    "query:new:optional": "warning",
    "input:removed": "error",
    "output:removed": "error",
    "response:new:status-code": "warning",
    "endpoint:new": "warning",
    "endpoint:removed": "error"
  }
}
```

## Output And Exit Codes

Text output is the default. JSON output is available with `--output json` and includes stable fields for:

- rule id
- severity
- message
- operation identity
- schema path when applicable

Exit codes:

- `0`: no error-level findings
- `1`: one or more error-level findings
- `2`: invalid configuration, invalid input, fetch failures, parse failures, unsupported external references, or other runtime failures

## Supported And Unsupported Scope

Supported in v1:

- exact method plus case-insensitive path-template matching
- JSON media types `application/json` and `application/*+json`
- nested object and array traversal
- conservative handling of `readOnly`, `writeOnly`, nullability, defaults, and schema composition

Out of scope in v1:

- Swagger/OpenAPI 2.0
- authenticated spec downloads
- external `$ref` targets
- non-JSON body comparisons
- path, header, or cookie parameter rules
- rename heuristics
- heuristic response rematching

## Development

Run the full test suite with:

```powershell
dotnet test .\Xemplo.ApiChecker.slnx
```