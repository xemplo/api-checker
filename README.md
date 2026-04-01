# Xemplo API Checker

`Xemplo.ApiChecker` is a .NET command-line tool for validating and comparing OpenAPI specifications and reporting compatibility findings using configurable rule severities.

There is nothing more frustrating to the user of your API than to have breaking changes unexpectedly arise. This tool helps to try and combat as many of those as possible.

Currently supports:
- OpenAPI 3.0 and 3.1
- Local and http(s) hosted API spec files
- JSON request/response body comparisons
- Query-parameter comparisons
- Endpoint additions/removals, operationId updates, and new response-code detection

## Install

Currently, the easiest way to use it is as a dotnet CLI tool:

```bash
dotnet tool install --global Xemplo.ApiChecker
api-checker --old <path-or-url> --new <path-or-url>
```

Or, more simply, using new `dnx` syntax:
```bash
dnx api-checker --old <path-or-url> --new <path-or-url>
```

## Detailed Usage

```text
api-checker --old <path-or-url> --new <path-or-url> [--config <path>] [--output text|json] [--rule <rule-id>=<severity> ...]
```

### Examples

```bash
# Compare local specs using default compatibility rules
api-checker --old .\tests\fixtures\request-body-old.json --new .\tests\fixtures\request-body-new.json

# Compare online specs using default compatibility rules and JSON output
api-checker --old https://example.com/openapi-old.yaml --new https://example.com/openapi-new.yaml --output json

# Compare sources using a custom set of comparison rules
api-checker --old old.json --new new.json --config ci-rules.json --rule endpoint:new=off --rule response:new:status-code=error
```

## Rule Configuration

Rule precedence (from lowest to highest) is:
1. Built-in defaults
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
- `input:updated:required`
- `input:updated:optional`
- `output:new:nullable`
- `output:new:non-nullable`
- `output:updated:nullable`
- `output:updated:non-nullable`
- `output:new:enum-value`
- `query:new:required`
- `query:new:optional`
- `input:removed`
- `output:removed`
- `response:new:status-code`
- `endpoint:new`
- `endpoint:updated:id`
- `endpoint:removed`

Example `api-rules.json`:

```json
{
  "rules": {
    "input:new:required": "error",
    "input:new:optional": "warning",
    "input:updated:required": "error",
    "input:updated:optional": "warning",
    "output:new:nullable": "warning",
    "output:new:non-nullable": "warning",
    "output:updated:nullable": "warning",
    "output:updated:non-nullable": "warning",
    "output:new:enum-value": "warning",
    "query:new:required": "error",
    "query:new:optional": "warning",
    "input:removed": "error",
    "output:removed": "error",
    "response:new:status-code": "warning",
    "endpoint:new": "warning",
    "endpoint:updated:id": "warning",
    "endpoint:removed": "error"
  }
}
```

## Output And Exit Codes

Text output is the default. JSON output is available with `--output json` and includes stable fields for:

- Rule id
- Severity
- Message
- Operation identity
- Schema path when applicable

Exit codes:
- `0`: no error-level findings
- `1`: one or more error-level findings
- `2`: invalid configuration, invalid input, fetch failures, parse failures, unsupported external references, or other runtime failures

## Contributing

This is currently an early-stage, internal Xemplo tool. Issues may be submitted, but third-party pull requests are not currently being reviewed.
