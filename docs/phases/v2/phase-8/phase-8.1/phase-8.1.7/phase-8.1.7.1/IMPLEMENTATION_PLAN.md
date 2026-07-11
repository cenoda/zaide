# Phase 8.1.7.1: Provider Compatibility Diagnosis — Implementation Plan

## Purpose

Diagnose the configured Cline Pass send failure and make only the smallest
confirmed compatibility or error-reporting correction. This plan is the
provider slice of the post-closeout Phase 8.1.7 follow-up.

## Dependencies

- The accepted Phase 8.1 M6 baseline is preserved.
- `AgentExecutionService` remains the single non-streaming,
  OpenAI-compatible execution path.
- A reproduction can be performed without committing or exposing credentials.

## Scope

- Reproduce the failure through the configured endpoint using a secret-safe
  diagnostic path.
- Capture only the HTTP status and redacted response shape needed to classify
  the failure as request/configuration, HTTP-error payload, or successful
  response-shape incompatibility.
- Verify the request URL, model, authorization behavior, and response contract
  against the observed endpoint without recording API-key values.
- If the endpoint is confirmed to return a supported OpenAI-compatible shape,
  improve actionable error reporting only where needed.
- If the endpoint is confirmed to use a documented successful shape that the
  current parser rejects, add the smallest parser change that supports that
  shape while retaining the existing standard response path.
- Add focused tests for the standard success path, the observed response form,
  and the relevant failure category.

## Out of Scope

- Provider registry or provider-specific abstraction layers.
- Streaming, retries, tool calling, LSP, request history, or credential UI.
- Logging request bodies, authorization headers, API keys, or raw sensitive
  response content.
- Guessing a compatibility fix before the endpoint contract is observed.
- Settings panel expansion, Phase 8.2, or Phase 8.3 behavior.

## Implementation Order

1. Add or use a secret-safe diagnostic seam and reproduce the failure.
2. Record the observed status/shape classification in the implementation
   closeout or child-plan evidence without sensitive values.
3. Add the minimal compatibility or actionable-error change only if the
   classification supports it.
4. Add focused tests and run the relevant test subset.

## Verification

- The existing OpenAI-compatible success response remains green.
- The observed provider response or failure category has a focused test.
- `dotnet build Zaide.slnx --no-restore` reports 0 warnings and 0 errors.
- `dotnet test Zaide.slnx --no-build` is green.
- `git diff --check` is clean.
- No secret value appears in source, tests, logs, diagnostics, or screenshots.

## Exit Conditions

- [ ] The provider failure is classified with secret-safe evidence.
- [ ] The standard OpenAI-compatible request and response behavior remains
      covered and passing.
- [ ] Any compatibility change is minimal, justified by the observed contract,
      and covered by focused tests; otherwise actionable error reporting is
      provided without speculative parser changes.
- [ ] No provider registry, streaming, retry, tool-calling, or unrelated
      Phase 8.1/8.2/8.3 behavior was added.

## Rollback Plan

Revert only the provider diagnostic or compatibility changes and retain the
accepted Phase 8.1 M6 baseline if the existing execution path regresses.
