# Phase 5.3: Minimal OpenAI-Compatible Execution — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 5.1.1 through 5.2 are complete
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [ ] Re-check `src/Program.cs` and current service-registration seams
- [ ] Decide the concrete request path for this slice: built-in `HttpClient` + manual JSON over a single OpenAI-compatible endpoint
- [ ] Confirm where base URL, API key, and default model will come from before coding starts
- [ ] Confirm no new library is required for the first request path

## Scope

**Goal:** Add one real direct user-to-agent execution path so a panel can send one request to one configured OpenAI-compatible endpoint and render the response.

**In scope:**

- One focused execution service seam for request/response handling
- One non-streaming OpenAI-compatible request path
- One configured endpoint and one default model
- Wiring a panel's direct input/output to that path
- Visible success/failure state for the panel
- Cancellation support only if it stays narrow and testable

**Out of scope:**

- Multi-provider architecture
- Provider registry or provider picker UI
- Streaming responses if they widen the slice materially
- Tool calling
- Agent-to-agent routing
- Transcript persistence

## Minimal Execution Decision

Phase 5.3 should stay intentionally small:

- Use the built-in .NET HTTP stack rather than introducing a provider SDK.
- Use one OpenAI-compatible chat-completion-style request path only.
- Use non-streaming responses only.
- Allow only one in-flight request per panel.
- Treat provider/model configuration as shared default configuration for this phase unless a concrete per-panel need appears during implementation.

This is enough to make a panel real without accidentally building a provider platform.

## Configuration Constraints

Before implementation begins, the plan owner must decide and record:

- where the base URL comes from
- where the API key comes from
- where the default model name comes from
- what visible error state is shown for missing or invalid configuration

Do not hardcode secrets. If the app cannot run the real request path without user-supplied configuration, that is acceptable as long as the failure is explicit and testable.

## Service Boundary Rule

The execution service added in this slice must not reference Views or ViewModels.
It returns execution results/errors to the ViewModel layer; Townhall mirroring is
explicitly deferred to `phase-5.4`.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Record the minimal execution decision and configuration source | Design note + `dotnet build Zaide.slnx` |
| M1 | Implement one focused execution service using built-in `HttpClient` and one OpenAI-compatible request/response shape | Service tests |
| M2 | Wire a panel's send flow to the execution seam with visible busy/success/error states | ViewModel tests + manual smoke |
| M3 | Verify failure handling for missing config, endpoint failure, invalid response, and cancellation/no-cancellation policy | Service/ViewModel tests |

## Test Budget

At minimum, budget tests for:

- request construction for the chosen endpoint shape
- response parsing for a valid success case
- missing API key / missing base URL / missing model behavior
- endpoint failure handling
- invalid response handling
- one-in-flight-request panel behavior
- panel busy/error state transitions

Likely files to extend or add:

- `tests/Zaide.Tests/Services/` new execution-service test files
- `tests/Zaide.Tests/ViewModels/` new direct-execution panel tests
- `tests/Zaide.Tests/MainWindowViewModelTests.cs` only if composition behavior changes

## Limitations (by design)

- One endpoint shape only
- Non-streaming only
- One in-flight request per panel
- Shared default model/configuration is acceptable for this slice
- No tool results, routing semantics, or transcript storage

## Exit Conditions

- [ ] A panel can send one real request to one configured OpenAI-compatible endpoint
- [ ] A response is shown in the panel output surface
- [ ] Busy and failure states are visible in the panel
- [ ] Missing/invalid configuration produces explicit failure behavior
- [ ] No provider registry/platform abstraction was added
- [ ] `dotnet build Zaide.slnx` passes
- [ ] Focused service and ViewModel tests pass
- [ ] Manual smoke verifies one successful request path when valid configuration is available

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
