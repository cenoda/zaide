# Phase 5.3: Minimal OpenAI-Compatible Execution — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 5.1.1 through 5.2 are complete
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
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

## Planned Execution Shape

The current intended implementation shape for this slice is:

- `src/Services/IAgentExecutionService.cs` — narrow service interface for one
  OpenAI-compatible request path
- `src/Services/AgentExecutionService.cs` — built-in `HttpClient` implementation
  using manual JSON against `/v1/chat/completions`
- `src/Services/AgentExecutionOptions.cs` — narrow shared configuration shape
  for base URL, API key, and model
- `src/Services/AgentExecutionResult.cs` — narrow result type for success/failure
- `src/ViewModels/IAgentExecutionCoordinator.cs` — orchestration seam for panel send flow
- `src/ViewModels/AgentExecutionCoordinator.cs` — composes `IAgentPanelHost` and
  `IAgentExecutionService` to update panel-visible state

Configuration for the first version is intentionally shared:

- `AGENT_API_URL` — defaults to `https://api.openai.com/v1`
- `AGENT_API_KEY` — required; missing key must produce explicit visible failure
- `AGENT_MODEL` — defaults to `gpt-4o-mini`

This preserves the "one endpoint shape only" rule while keeping provider/model
configuration outside panel state.

## Input Trigger Constraint

The current live panel UI has a draft input box but no send trigger:

- `AgentPanelView` does not expose a send button.
- `AgentPanelView` does not handle Enter/Return for send.
- `AgentPanelHostView` does not expose a panel-send seam.

Phase 5.3 therefore cannot stop at service/coordinator work. It must also add
one explicit user action that starts a request.

For this slice, the send trigger should stay minimal:

- a send button next to the input box is preferred
- Enter-to-send is acceptable if implemented narrowly
- execution logic must not live in the view event handler

The trigger should route into the orchestration seam rather than teaching the
view about provider execution.

## Service Boundary Rule

The execution service added in this slice must not reference Views or ViewModels.
It returns execution results/errors to the ViewModel layer; Townhall mirroring is
explicitly deferred to `phase-5.4`.

## Orchestration Rule

This phase may add a coordinator seam above the service layer, but it must stay
narrow and explicit:

- Services do HTTP request/response work only.
- The coordinator owns panel send orchestration only.
- The coordinator owns per-panel one-in-flight enforcement for this slice.
- `MainWindow` may wire the view event into the coordinator, but must not become
  the place that owns execution state or business logic.
- Townhall mirroring remains deferred to `phase-5.4`.

If the implementation adds a coordinator, it should be treated as the app/ViewModel
seam for direct panel execution rather than as an ad hoc view-only callback chain.

`MainWindowViewModel` may expose a thin command or delegating method if that
keeps the view binding clean, but execution state mutation should still remain
inside the coordinator seam.

## Configuration Decision

For the minimal first slice, shared execution configuration should come from
environment variables and be captured in a narrow options type:

- `AGENT_API_URL`
- `AGENT_API_KEY`
- `AGENT_MODEL`

Recommended implementation shape:

- read the environment variables in `Program.cs`
- populate an `AgentExecutionOptions` instance at startup
- inject that options object into `AgentExecutionService`

This keeps secrets out of panel state and avoids introducing app settings or a
provider registry in Phase 5.3.

## Reactive State Constraint

The live code currently binds `AgentPanelView` directly to `AgentPanelState`,
while `AgentPanelState` still uses plain auto-properties for `Status` and
`DraftInput`.

That means Phase 5.3 cannot safely rely on live updates such as:

- `Status = "Thinking"` / `"Idle"` / `"Error"`
- disabling or re-enabling the input surface while a request is in flight
- clearing draft text after a successful send

until the panel state seam is made observable.

Before implementation begins, this slice must choose one explicit path:

1. Make `AgentPanelState` observable enough for Phase 5.3 UI updates.
2. Introduce a dedicated panel ViewModel seam and stop binding the panel view
   directly to a plain model.

Phase 5.3 should choose the smaller of those two options, but it must choose
one up front. Without that decision, the busy/error UX in this plan is not
reliably implementable.

Recommendation for this slice: prefer the smaller path unless live implementation
proves otherwise. Making `AgentPanelState` reactive for the coordinator-mutated
properties is likely narrower than introducing a whole new panel ViewModel layer.

## Testability Rule

The service and orchestration seams in this slice must stay trivially testable:

- `AgentExecutionService` should accept `HttpClient` via constructor
- tests should inject a fake or mocked `HttpMessageHandler`
- JSON should remain manual with the built-in stack
- tests must not require live network access

This is the expected path for covering request construction, endpoint failure,
invalid response handling, and explicit missing-config behavior.

## Phase 5.4 Compatibility Note

Townhall mirroring remains out of scope for 5.3, but 5.3 should avoid painting
Phase 5.4 into a corner.

That means:

- do not let `AgentExecutionCoordinator` reference `TownhallViewModel`
- consider exposing a narrow execution event seam that a later phase can
  subscribe to without rewriting the coordinator's core logic

This is a forward-compatibility guideline, not a reason to widen 5.3 into full
Townhall integration now.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Record the minimal execution decision, configuration source, and reactive-state approach | Design note + `dotnet build Zaide.slnx` |
| M1 | Implement one focused execution service using built-in `HttpClient` and one OpenAI-compatible request/response shape | Service tests |
| M2 | Add one explicit panel send trigger and implement the narrow orchestration seam (`IAgentExecutionCoordinator` if retained) that receives it | View/ViewModel tests |
| M3 | Expose visible busy/success/error behavior in the panel without widening provider or Townhall scope | ViewModel tests + manual smoke |
| M4 | Verify failure handling for missing config, endpoint failure, invalid response, and the one-in-flight policy | Service/ViewModel tests |

## Test Budget

At minimum, budget tests for:

- request construction for the chosen endpoint shape
- response parsing for a valid success case
- missing API key / missing base URL / missing model behavior
- endpoint failure handling
- invalid response handling
- one-in-flight-request panel behavior
- panel busy/error state transitions
- coordinator behavior for success/failure updates to the targeted panel
- draft clearing / output history append behavior after send
- send-trigger behavior in the panel view
- explicit missing-config behavior from injected options/environment-derived config
- composition wiring only if `MainWindowViewModel` constructor or DI changes

Likely files to extend or add:

- `tests/Zaide.Tests/Services/` new execution-service test files
- `tests/Zaide.Tests/ViewModels/` new direct-execution panel tests
- `tests/Zaide.Tests/Views/` panel send-trigger tests if the control behavior is non-trivial
- `tests/Zaide.Tests/MainWindowViewModelTests.cs` only if composition behavior changes

Likely implementation files:

- `src/Services/IAgentExecutionService.cs`
- `src/Services/AgentExecutionService.cs`
- `src/Services/AgentExecutionOptions.cs`
- `src/Services/AgentExecutionResult.cs`
- `src/ViewModels/IAgentExecutionCoordinator.cs`
- `src/ViewModels/AgentExecutionCoordinator.cs`
- `src/Views/AgentPanelView.cs`
- `src/Views/AgentPanelHostView.cs`
- `src/MainWindow.axaml.cs`
- `src/Program.cs`

## Limitations (by design)

- One endpoint shape only
- Non-streaming only
- One in-flight request per panel
- Shared default model/configuration is acceptable for this slice
- No tool results, routing semantics, or transcript storage
- Busy/error UI is only in scope once the panel state seam is observably reactive
- Configuration remains process-level/shared rather than per-panel in this slice

## Audit Notes Incorporated

This plan intentionally incorporates constraints discovered during live-code
audits of the current checkout:

- The proposed service/coordinator split is directionally sound and matches the
  existing host/service precedent, but the app must avoid turning `MainWindow`
  code-behind into the real execution owner.
- The current `AgentPanelState` shape is too plain for reliable status-driven UI
  updates, so reactive-state work is a prerequisite for the visible busy/error
  behavior this phase promises.
- The live panel UI currently has no send trigger, so Phase 5.3 must include a
  minimal input-action seam rather than assuming execution can already start.
- Configuration and `HttpClient` testability should be decided explicitly before
  implementation to keep the first provider slice deterministic and narrow.

## Exit Conditions

- [ ] A panel can send one real request to one configured OpenAI-compatible endpoint
- [ ] A response is shown in the panel output surface
- [ ] Busy and failure states are visible in the panel
- [ ] Missing/invalid configuration produces explicit failure behavior
- [ ] The chosen reactive-state path is implemented and covered by tests
- [ ] No provider registry/platform abstraction was added
- [ ] `dotnet build Zaide.slnx` passes
- [ ] Focused service and ViewModel tests pass
- [ ] Manual smoke verifies one successful request path when valid configuration is available

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
