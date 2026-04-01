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
api-checker --old ./tests/fixtures/request-body-old.json --new ./tests/fixtures/request-body-new.json

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
- `input:new:required` - A new required field has been added to the request body
- `input:new:optional` - A new optional field has been added to the request body
- `input:updated:required` - An existing property has changed from optional to required in the request body
- `input:updated:optional` - An existing property has changed from required to optional in the request body
- `output:new:nullable` - A new nullable field has been added to the response body
- `output:new:non-nullable` - A new non-nullable field has been added to the response body
- `output:updated:nullable` - An existing property has changed from non-nullable to nullable in the response body
- `output:updated:non-nullable` - An existing property has changed from nullable to non-nullable in the response body
- `output:new:enum-value` - An existing enum used in the response body has gained a new value
- `query:new:required` - A new required query parameter has been added
- `query:new:optional` - A new optional query parameter has been added
- `input:removed` - A property in the request body has been removed
- `output:removed` - A property in the response body has been removed
- `response:new:status-code` - An endpoint now returns a new HTTP status code
- `endpoint:new` - A new endpoint has been added
- `endpoint:updated:id` - An existing endpoint operationId has changed
- `endpoint:removed` - An endpoint has been removed

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

### Builds and Versioning

GitHub Actions will:
- Build and test all PRs targeting `main`
- Build, test and publish every commit to `main` as an `alpha` pre-release version such as `0.1.0-alpha.3`
- Build, test and publish `v<major>.<minor>.<patch>` tags as stable release of `<major>.<minor>.<patch>`

You can also control version increments via :
  - Because this repo uses squash merges, put the `+semver:` marker in the PR title so it flows into the squashed commit on `main`
  - If you edit the squash commit message while merging, keep the `+semver:` marker in the final commit subject or body
  - Use `+semver: patch` for fixes
  - Use `+semver: minor` for backwards-compatible features
  - Use `+semver: major` or `+semver: breaking` for breaking changes
  - Use `+semver: none` or `+semver: skip` when a merged change should not advance the release line

### Recommended Release Flow

1. Merge new features to `main`; each push to `main` publishes the next `alpha` package automatically
2. Set the intended release size on the PR you merge by putting the `+semver:` marker in the PR's commit message
3. Branch `release/<target-version>` from `main` when you want an `rc` stabilization lane
4. Tag the release commit with `v<version>` when you want the stable package published for that line
