# Phase 5.4: Townhall Integration for Direct-Agent Interaction — Implementation Plan

## Pre-Implementation Verification

These items must be verified before implementation begins — treated as hard gates:

- [ ] Confirm Phase 5.1.1 through 5.3 are complete (agent panels, coordinator, host)
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] Confirm `AgentExecutionCoordinator` has no Townhall references in `src/ViewModels/AgentExecutionCoordinator.cs`
- [ ] Confirm `MainWindowViewModel.SendAgentMessageAsync` is the current composition point in `src/ViewModels/MainWindowViewModel.cs`
- [ ] Confirm `TownhallViewModel.LogActivity` is private and the surface method must be added in `src/ViewModels/TownhallViewModel.cs`
- [ ] Confirm `IAgentExecutionCoordinator.SendAsync` returns `Task` (no result payload) in `src/ViewModels/IAgentExecutionCoordinator.cs`

## Scope

**Goal:** Keep Townhall truthful when a user interacts with a dedicated agent panel, without introducing routing semantics.

**In scope:**

- Log direct user-to-agent requests into Townhall at a useful activity level
- Log direct agent responses and visible failures into Townhall at a useful activity level
- Keep panel activity and Townhall activity aligned for direct interactions
- Use only the existing app/ViewModel seam required to mirror these events cleanly

**Out of scope:**

- Agent-to-agent routing
- Mention parsing
- Debate/thread orchestration
- Full transcript synchronization semantics
- Provider-service awareness of Townhall

## Boundary Rule

Townhall mirroring in this slice must happen through the app/ViewModel orchestration
layer, not by having provider services reference `TownhallViewModel` directly.

For the current codebase, the intended seam is the existing
`MainWindowViewModel.SendAgentMessageAsync(...)` flow:

- `MainWindow.axaml.cs` raises the panel send request
- `MainWindowViewModel.SendAgentMessageAsync(...)` is the composition point
- `IAgentExecutionCoordinator.SendAsync(...)` performs panel execution/state mutation
- `MainWindowViewModel` mirrors the direct-interaction result into `TownhallViewModel`

Phase 5.4 should not add Townhall knowledge to `AgentExecutionCoordinator` or
provider services.

This preserves the current MVVM/service boundaries:

- Services do execution work only.
- ViewModels/app-layer seams decide what Townhall should record.
- Views only render the resulting state.

## Locked Decisions

### Orchestration approach

Use the existing `MainWindowViewModel.SendAgentMessageAsync(...)` seam.

- Log the user request into Townhall before awaiting the coordinator call.
- Await `IAgentExecutionCoordinator.SendAsync(...)`.
- After the await, inspect the target `AgentPanelState` to mirror the visible
  result into Townhall.

No reactive subscription, coordinator event, or new interface member is required
for this phase. The coordinator already completes only after it has appended the
panel-visible user/assistant/error output and updated the panel status.

### Response/failure capture

After `SendAsync(...)` completes, derive the Townhall mirror entry from the
final panel-visible state:

- Resolve the panel by `panelId` from `IAgentPanelHost.Panels`
- Read the latest relevant `OutputHistory` entry
- Read the final `Status`

Phase 5.4 mirrors only the visible final result already shown in the panel. It
does not introduce transcript replay, streaming, or provider-detail capture.

### Townhall write surface

`TownhallViewModel` currently exposes only private `LogActivity(...)` plumbing.
Phase 5.4 should add a narrow public method for mirrored activity insertion
rather than exposing the private helper directly.

Preferred shape:

- Add a small public method on `TownhallViewModel` for appending an activity
  entry to the active channel
- Keep channel/message-list invariants inside `TownhallViewModel`

### DI registration

No new DI registrations are required for Phase 5.4. `TownhallViewModel` is
already registered as a singleton in `Program.cs` (line 36) and is injected
into `MainWindowViewModel` (line 101). `IAgentExecutionCoordinator` is already
registered at line 53.

If implementation introduces any new interface or class, update
`src/Program.cs` accordingly.

### Message-kind mapping

Use the existing `TownhallMessageKind` semantics:

- Direct user request: `Chat`
- Direct agent text response: `Chat`
- Direct visible agent/request failure: `AgentError`

`AgentAction` remains reserved for visible non-chat actions such as file edits,
tool execution, or command-running activity when Phase 5/6 exposes those.

### Channel targeting

Mirror entries into the currently active Townhall channel via
`TownhallViewModel.ActiveChannelId`.

Phase 5.4 does not create per-agent channels or routing-specific destinations.

### Identity mapping

Use existing live identities:

- Direct user request: `senderId = "user-1"`, `senderName = "User"`
- Direct agent response: `senderId = panel.AgentId`, `senderName = panel.AgentName`
- Direct agent failure: `senderId = panel.AgentId`, `senderName = panel.AgentName`

## Logging Decision Constraints

Phase 5.4 should log only what is necessary to keep the shared workspace honest:

- user sent direct message to agent X
- agent X responded
- agent X failed / request failed visibly

Do not log speculative routing concepts, invisible internal retries, or detailed
provider internals unless Phase 5 already exposes them to the user.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Verify pre-implementation gates and confirm live seam behavior matches lock decisions | Pre-impl checklist + targeted debug of panel state transitions |
| M1 | Add the narrow Townhall append surface (public method on `TownhallViewModel`) | Focused `TownhallViewModel` tests |
| M2 | Mirror direct request/response/error flow from `MainWindowViewModel.SendAgentMessageAsync(...)` into Townhall | Focused ViewModel/orchestration tests |
| M3 | Verify panel-visible state and Townhall-visible state remain aligned for the direct interaction flow | ViewModel tests + manual smoke |

## Test Budget

At minimum, budget tests for:

- Townhall entry creation when a direct-agent request is sent
- Townhall entry creation when a direct-agent response arrives
- Townhall entry creation when the direct request fails visibly
- No Townhall reference being introduced into provider services or `AgentExecutionCoordinator`
- Alignment between panel-visible and Townhall-visible state transitions

Likely files to extend or add:

- `tests/Zaide.Tests/ViewModels/MainWindowViewModelTests.cs`
- `tests/Zaide.Tests/ViewModels/` new Townhall-integration tests
- `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs` if direct assertions are added there
- `tests/Zaide.Tests/ViewModels/` panel/Townhall coordination tests

## Limitations (by design)

- Townhall integration may remain summary-level rather than full transcript mirroring
- No agent-to-agent delivery semantics
- No mention parsing or thread branching
- No persistence of mirrored events beyond whatever Phase 4 already supports in memory

## Exit Conditions

- [x] Direct user-to-agent interactions appear in Townhall
- [x] Direct agent responses and visible failures appear in Townhall at the intended Phase 5 level
- [x] No provider service directly references `TownhallViewModel`
- [x] `AgentExecutionCoordinator` remains free of Townhall references
- [x] No routing behavior is introduced
- [x] `dotnet build Zaide.slnx` passes
- [x] Focused ViewModel/orchestration tests pass
- [x] Manual smoke confirms panel activity and Townhall activity remain aligned

## Rollback Plan

- Revert to commit `8651d016e6e9b8c59cb00c228ccdadf19c3c9ad4` if implementation diverges from the locked decisions
