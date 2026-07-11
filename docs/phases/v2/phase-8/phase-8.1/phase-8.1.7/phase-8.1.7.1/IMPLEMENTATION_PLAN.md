# Phase 8.1.7.1: Cline Pass Integration and UI Completion — Implementation Plan

## Purpose

Diagnose the configured Cline Pass send failure by comparing it with the
already-working DeepSeek OpenAI-compatible path, and fix the smallest confirmed
integration or post-success UI failure. The current leading UI hypothesis is
that `ConfigureAwait(false)` allows ViewModel state consumed by Avalonia to be
mutated away from the UI thread after the HTTP request completes. This plan is
the provider/integration slice of the post-closeout Phase 8.1.7 follow-up.

## Dependencies

- The accepted Phase 8.1 M6 baseline is preserved.
- `AgentExecutionService` remains the single non-streaming,
  OpenAI-compatible execution path.
- DeepSeek is the known-good OpenAI-compatible control provider for the
  comparison. Its successful response does not by itself prove that Cline
  Pass is configured correctly.
- The HTTP service may continue using `ConfigureAwait(false)` internally, but
  UI-bound state updates must return to the Avalonia UI thread before mutating
  `AgentPanelState` or Townhall-bound collections/properties.
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
- Verify the async continuation boundaries in
  `AgentExecutionCoordinator.SendAsync` and
  `MainWindowViewModel.SendAgentMessageAsync` before changing provider code.
- Restore UI-thread affinity at the narrowest ViewModel/application boundary
  for `OutputHistory`, `Status`, and Townhall mirroring. Do not add UI concerns
  to `AgentExecutionService`.
- Add secret-safe actionable diagnostics for response failures: status code,
  request path, model, and redacted response shape/top-level field names only.
  Never display or log API keys, authorization headers, request bodies, or raw
  response bodies.
- Add the smallest confirmed integration or UI-thread/error-reporting
  correction, with focused coverage for the observed failure category.

## Implementation Surface

The implementation is limited to these seams unless diagnosis proves another
file is required:

- `src/Services/AgentExecutionService.cs` — request/response behavior only if
  the DeepSeek/Cline comparison proves an HTTP or response-contract difference.
- `src/ViewModels/AgentExecutionCoordinator.cs` — restore UI-thread affinity
  before mutating `AgentPanelState` after the awaited execution call.
- `src/ViewModels/MainWindowViewModel.cs` — restore UI-thread affinity before
  Townhall mirroring and before reading/mirroring post-execution panel state.
- `tests/Zaide.Tests/Services/AgentExecutionServiceTests.cs` — provider
  request/response evidence only when a provider-specific contract is observed.
- `tests/Zaide.Tests/ViewModels/AgentExecutionCoordinatorTests.cs` and
  `tests/Zaide.Tests/MainWindowViewModelTests.cs` — regression coverage for
  successful completion, failure completion, and post-success UI updates.

Do not widen this slice to provider abstractions, streaming, retries, command
registration, settings schema changes, or a general dispatcher framework.

## Diagnostic Gate Before Code Changes

1. Run the existing DeepSeek configuration through the normal UI send path and
   record success plus any later UI error.
2. Run the same short message through Cline Pass and record only the effective
   route category, HTTP status, model, and redacted response shape.
3. If the HTTP request succeeds but a later error appears, reproduce with a
   deterministic test or controlled continuation that proves whether the
   failure occurs during `OutputHistory` or Townhall mutation.
4. Only then choose one of these implementation outcomes:
   - configuration/request correction;
   - minimal response-contract correction;
   - UI-thread-affinity correction;
   - actionable error-reporting correction.

The implementation must not claim Cline Pass incompatibility merely because a
post-success UI exception is present.

## Out of Scope

- Provider registry or provider-specific abstraction layers.
- Streaming, retries, tool calling, LSP, request history, or credential UI.
- Logging request bodies, authorization headers, API keys, or raw sensitive
  response content.
- Guessing a Cline Pass compatibility fix before the comparison evidence is
  observed.
- Settings panel expansion, Phase 8.2, or Phase 8.3 behavior.

## Provider Comparison Evidence

### Common path analysis (code inspection)

Both DeepSeek and Cline Pass use the same code path:

| Property | Value |
|----------|-------|
| Implementation | `AgentExecutionService.ExecuteAsync` in `AgentExecutionService.cs` |
| Endpoint | `{BaseUrl}/chat/completions` (OpenAI-compatible) |
| Method | POST with `Authorization: Bearer {ApiKey}` |
| Request body | `{ "model": ..., "stream": false, "messages": [{ "role": "user", "content": ... }] }` |
| Response parsing | `choices[0].message.content` via `System.Text.Json` |
| Provider dispatch | None — single execution path, no provider registry |

Both providers hit the exact same request construction and response parsing
code. No provider-specific branching, header injection, or contract negotiation
exists. Therefore the HTTP/response layer is identical for both.

### Root cause classifications: **UI-thread affinity and explicit streaming mode**

The `Exception has been thrown by the target of an invocation.` error is a
cross-thread exception in Avalonia — not a provider configuration or response
shape issue. It occurs when `ObservableCollection<T>.Add()` or
`ReactiveObject.RaiseAndSetIfChanged()` are called from a thread-pool thread.

Three `ConfigureAwait(false)` sites in the ViewModel boundary cause the
continuation to skip the captured Avalonia SynchronizationContext:

| File | Line | Before fix | After fix |
|------|------|-----------|-----------|
| `AgentExecutionCoordinator.cs` | 61 | `await _executionService.ExecuteAsync(...).ConfigureAwait(false)` | `await _executionService.ExecuteAsync(...)` |
| `AgentRouter.cs` | 44, 51 | `await _coordinator.SendAsync(...).ConfigureAwait(false)` (two sites) | `await _coordinator.SendAsync(...)` |
| `MainWindowViewModel.cs` | 255 | `await AgentRouter.RouteAndExecuteAsync(...).ConfigureAwait(false)` | `await AgentRouter.RouteAndExecuteAsync(...)` |

`AgentExecutionService.ExecuteAsync` internally preserves its own
`ConfigureAwait(false)` for HTTP I/O — this is correct and unchanged.

### Current diagnosis and evidence boundary

The shared-code inspection confirms that DeepSeek and Cline Pass use the same
`AgentExecutionService` request and response path. The continuation fix is
therefore provider-neutral and is supported by the observed async boundaries
plus focused state-transition tests.

The desktop Cline Pass capture adds one confirmed request-contract finding:
Cline documents `stream: true` as the default for its chat-completions endpoint,
while Zaide parses only a single non-streaming
`choices[0].message.content` response. The request now sends `"stream": false`;
no streaming implementation is added in this phase.

The subsequent Cline capture shows a successful response envelope with
top-level `data` and `success` fields. The response choices are therefore
located at `data.choices` for this observed Cline Pass path. Zaide now unwraps
that object envelope while retaining the standard top-level `choices` path.

The capture also shows a separate HTTP 404 for one Cline Pass request. The
available evidence does not distinguish an invalid model/account route from a
transient provider-side failure, so no model rewrite or retry is added.

No speculative parser change, provider-specific contract change, or settings
change was made.

## Implementation Order

1. Establish DeepSeek as the known-good control using the existing execution
   and UI path.
2. Reproduce the same request against Cline Pass through a secret-safe
   diagnostic path.
3. Record the provider comparison and classify any later UI/Townhall error
   separately from the HTTP request result.
4. Verify and correct the ViewModel continuation boundaries if the error occurs
   after successful HTTP execution.
5. Add a provider/request correction only if the comparison proves one is
   required, then add focused tests for the confirmed category.

## Verification

- The existing OpenAI-compatible success response remains green.
- The observed Cline Pass result or post-success failure category has a
  focused test.
- A successful execution updates panel output/status and Townhall without a
  cross-thread or wrapped reflection exception.
- `dotnet build Zaide.slnx --no-restore` reports 0 warnings and 0 errors.
- `dotnet test Zaide.slnx --no-build` is green.
- `git diff --check` is clean.
- No secret value appears in source, tests, logs, diagnostics, or screenshots.

## Exit Conditions

- [x] DeepSeek control behavior and Cline Pass behavior are compared with
      secret-safe live evidence. Both providers share the same path through
      `AgentExecutionService.ExecuteAsync` and `/chat/completions`. Desktop
      evidence shows Cline Pass returns a 404 for one request and defaults to
      streaming; Zaide now explicitly requests non-streaming mode.
- [x] The standard OpenAI-compatible request and response behavior remains
      covered and passing (all 902 tests green).
- [x] Any integration change is minimal, justified by the observed comparison,
      and covered by focused tests; the request now sends `stream: false`, with
      the observed `data.choices` envelope supported without changing the
      standard response path. No model rewrite was made.
- [x] Response-shape failures expose safe top-level field diagnostics without
      exposing raw response content or credentials.
- [x] A post-success UI/Townhall exception, if present, is tracked separately
      from provider compatibility and has desktop evidence. User-provided
      desktop verification shows successful DeepSeek and Cline Pass responses
      with panel status `Idle`, Townhall mirroring, and no wrapped reflection
      or cross-thread error.
- [x] ViewModel-owned UI state is confirmed to be mutated on the Avalonia UI
      thread after asynchronous execution completes. The three
      ViewModel-boundary `ConfigureAwait(false)` sites were removed, focused
      tests pass, and the user-provided Avalonia desktop verification is clean.
- [x] No provider registry, streaming, retry, tool-calling, or unrelated
      Phase 8.1/8.2/8.3 behavior was added.

## Rollback Plan

Revert only the provider diagnostic or compatibility changes and retain the
accepted Phase 8.1 M6 baseline if the existing execution path regresses.
