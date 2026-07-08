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

## Service Boundary Rule

The execution service added in this slice must not reference Views or ViewModels.
It returns execution results/errors to the ViewModel layer; Townhall mirroring is
explicitly deferred to `phase-5.4`.

## Orchestration Rule

This phase may add a coordinator seam above the service layer, but it must stay
narrow and explicit:

- Services do HTTP request/response work only.
- The coordinator owns panel send orchestration only.
- `MainWindow` may wire the view event into the coordinator, but must not become
  the place that owns execution state or business logic.
- Townhall mirroring remains deferred to `phase-5.4`.

If the implementation adds a coordinator, it should be treated as the app/ViewModel
seam for direct panel execution rather than as an ad hoc view-only callback chain.

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

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Record the minimal execution decision, configuration source, and reactive-state approach | Design note + `dotnet build Zaide.slnx` |
| M1 | Implement one focused execution service using built-in `HttpClient` and one OpenAI-compatible request/response shape | Service tests |
| M2 | Implement the narrow orchestration seam (`IAgentExecutionCoordinator` if retained) and wire one panel send flow to it | ViewModel tests |
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
- composition wiring only if `MainWindowViewModel` constructor or DI changes

Likely files to extend or add:

- `tests/Zaide.Tests/Services/` new execution-service test files
- `tests/Zaide.Tests/ViewModels/` new direct-execution panel tests
- `tests/Zaide.Tests/MainWindowViewModelTests.cs` only if composition behavior changes

Likely implementation files:

- `src/Services/IAgentExecutionService.cs`
- `src/Services/AgentExecutionService.cs`
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

## Audit Notes Incorporated

This plan intentionally incorporates two constraints discovered during live-code
audit of the current checkout:

- The proposed service/coordinator split is directionally sound and matches the
  existing host/service precedent, but the app must avoid turning `MainWindow`
  code-behind into the real execution owner.
- The current `AgentPanelState` shape is too plain for reliable status-driven UI
  updates, so reactive-state work is a prerequisite for the visible busy/error
  behavior this phase promises.

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
