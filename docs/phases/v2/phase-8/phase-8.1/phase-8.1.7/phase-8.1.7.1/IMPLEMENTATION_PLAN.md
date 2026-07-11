# Phase 8.1.7.1: Cline Pass Integration Diagnosis — Implementation Plan

## Purpose

Diagnose the configured Cline Pass send failure by comparing it with the
already-working DeepSeek OpenAI-compatible path, then make only the smallest
confirmed integration or error-reporting correction. This plan is the
provider slice of the post-closeout Phase 8.1.7 follow-up.

## Dependencies

- The accepted Phase 8.1 M6 baseline is preserved.
- `AgentExecutionService` remains the single non-streaming,
  OpenAI-compatible execution path.
- DeepSeek is the known-good OpenAI-compatible control provider for the
  comparison. Its successful response does not by itself prove that Cline
  Pass is configured correctly.
- A reproduction can be performed without committing or exposing credentials.

## Scope

- Reproduce a short request through DeepSeek and Cline Pass using the same
  application path and secret-safe diagnostics.
- Compare only the effective base URL, final request path, model, HTTP status,
  and redacted response shape. Never record API-key values.
- Classify the Cline Pass result as configuration/request failure, HTTP error,
  response-shape issue, or post-success UI/Townhall processing failure.
- If both providers return valid assistant text but the UI adds a later error,
  keep that as a separate post-success application failure rather than calling
  it provider incompatibility.
- Add the smallest confirmed integration or actionable-error correction, with
  focused coverage for the observed failure category.

## Out of Scope

- Provider registry or provider-specific abstraction layers.
- Streaming, retries, tool calling, LSP, request history, or credential UI.
- Logging request bodies, authorization headers, API keys, or raw sensitive
  response content.
- Guessing a Cline Pass compatibility fix before the comparison evidence is
  observed.
- Settings panel expansion, Phase 8.2, or Phase 8.3 behavior.

## Implementation Order

1. Establish DeepSeek as the known-good control using the existing execution
   and UI path.
2. Reproduce the same request against Cline Pass through a secret-safe
   diagnostic path.
3. Record the provider comparison and classify any later UI/Townhall error
   separately from the HTTP request result.
4. Add the minimal confirmed correction and focused tests only after the
   classification is known.

## Verification

- The existing OpenAI-compatible success response remains green.
- The observed Cline Pass result or post-success failure category has a
  focused test.
- `dotnet build Zaide.slnx --no-restore` reports 0 warnings and 0 errors.
- `dotnet test Zaide.slnx --no-build` is green.
- `git diff --check` is clean.
- No secret value appears in source, tests, logs, diagnostics, or screenshots.

## Exit Conditions

- [ ] DeepSeek control behavior and Cline Pass behavior are compared with
      secret-safe evidence.
- [ ] The standard OpenAI-compatible request and response behavior remains
      covered and passing.
- [ ] Any integration change is minimal, justified by the observed comparison,
      and covered by focused tests; no speculative parser change is added.
- [ ] A post-success UI/Townhall exception, if present, is tracked separately
      from provider compatibility and has its own focused evidence.
- [ ] No provider registry, streaming, retry, tool-calling, or unrelated
      Phase 8.1/8.2/8.3 behavior was added.

## Rollback Plan

Revert only the provider diagnostic or compatibility changes and retain the
accepted Phase 8.1 M6 baseline if the existing execution path regresses.
