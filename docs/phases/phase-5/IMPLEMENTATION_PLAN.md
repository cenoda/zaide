# Phase 5: Agent Panels — Implementation Plan

## Pre-Implementation Verification
- [ ] Confirm Phase 4 is complete in live code/docs, not just roadmap wording
- [ ] Re-check `src/MainWindow.axaml.cs`, `src/ViewModels/MainWindowViewModel.cs`, `src/ViewModels/TownhallViewModel.cs`, and `src/Program.cs` for the actual composition seams
- [ ] Confirm the current shell has room for agent panels without rewriting the Townhall-centered layout

## Live Baseline

Verified against the current checkout on 2026-07-08:

- `docs/roadmap/PHASES.md` marks Phase 4 complete and defines Phase 5 as dedicated
  agent surfaces that keep Townhall primary.
- `docs/architecture/OVERVIEW.md` describes Phase 5 as "Agent Panels" and keeps
  routing explicitly in Phase 6.
- `src/MainWindow.axaml.cs` composes the live shell as nav bar | left panel slot
  | Townhall | editor, with terminal/logs bottom and status bar below.
- `src/ViewModels/MainWindowViewModel.cs` currently owns file tree, editor tabs,
  terminal host, Townhall, and source control state, but no agent-panel host or
  per-agent panel state.
- `src/ViewModels/TownhallViewModel.cs` now provides the Phase 4 shared activity
  surface (session seed data, auto-logging, filterable mixed activity history),
  which is the correct prerequisite for Phase 5.

## Scope

**Goal:** Add dedicated per-agent panel surfaces that render inside the existing
application shell, accept direct user input, and expose agent-specific output/status
without demoting Townhall from the primary shared workspace. Phase 5 also includes
the minimum real execution path required to let a panel talk to one configured
OpenAI-compatible endpoint and display the result.

**Boundaries:** This phase is about agent-specific UI surfaces, their immediate
ViewModel/state wiring, and one minimal real execution seam for direct user-to-agent
interaction. It is not the phase for multi-agent routing, debate orchestration,
provider-registry generalization, or speculative persistence architecture.

## Why This Phase Exists

Phase 4 made Townhall a real shared workspace. The next missing capability is a
place for specialized per-agent interaction when a single mixed shared feed is no
longer enough. Phase 5 should introduce those dedicated surfaces while preserving
the product direction:

- Townhall stays the shared workspace and activity ledger
- Agent panels provide focused per-agent interaction surfaces
- Routing between agents remains a later concern for Phase 6

## Design Constraints

1. Townhall remains visually and architecturally primary.
2. Agent panels must fit the existing shell before any shell rewrite is considered.
3. Phase 5 may add one minimal OpenAI-compatible provider seam, but not a broad
   provider platform.
4. Any new abstractions must solve an immediate Phase 5 problem, not a guessed
   Phase 6/7 need.
5. Tests must be budgeted alongside each new ViewModel/state seam and the new
   provider integration seam.

## Proposed Phase Split

Phase 5 is likely still too wide to implement safely as one uninterrupted pass.
Start with these sub-phases unless live implementation reveals it can stay smaller:

| Sub-phase | Scope | Status |
|-----------|-------|--------|
| 5.1 | Agent panel state/model + shell host seam | Planned |
| 5.2 | Agent panel UI surfaces (status/output/input) | Planned |
| 5.3 | Minimal direct-execution seam via one OpenAI-compatible endpoint | Planned |
| 5.4 | Townhall integration for direct-agent interactions | Planned |
| 5.5 | Docs sync + regression audit | Planned |

If implementation begins, each sub-phase should get its own
`docs/phases/phase-5.x/IMPLEMENTATION_PLAN.md`.

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate: confirm current shell/view-model seams and decide where agent panel host state lives (`MainWindowViewModel` vs dedicated host VM) | `dotnet build Zaide.slnx`, repo inspection notes |
| M1 | Introduce minimal agent-panel domain/view-model shape: panel id, agent identity, status, output items, draft input, active/visible state | New model/ViewModel unit tests |
| M2 | Add an agent-panel host seam to the shell composition so multiple panels can render without replacing Townhall | ViewModel tests + build |
| M3 | Render first dedicated agent panel UI with status area, output area, and direct input area | View tests/manual smoke |
| M4 | Add one minimal OpenAI-compatible provider path for direct panel-to-agent requests and responses | Provider/service tests + manual smoke |
| M5 | Support multiple agent panels and panel selection/focus without changing Phase 6 routing scope | Host/ViewModel tests + manual smoke |
| M6 | Mirror direct-agent interactions into Townhall at the correct abstraction level (activity/log entry, not routing engine) | ViewModel tests for Townhall logging side effects |
| M7 | Docs sync and regression sweep | `dotnet build Zaide.slnx`, `dotnet test Zaide.slnx --no-build`, manual shell smoke |

## Planned Implementation Shape

This is the recommended narrow shape for the first implementation pass.

### 1. Agent panel state

Add a focused model/ViewModel pair for a single agent panel, likely covering:

- Agent identity (`Id`, display name, avatar/resource key)
- Current status (`Idle`, `Working`, `Waiting`, `Error` or similar)
- Output history for that panel
- Draft input text
- Visibility/active selection state

Do not add routing metadata or a generalized provider registry unless a specific
Phase 5 milestone proves a tiny seam is necessary beyond one OpenAI-compatible
execution path.

### 2. Agent panel host

Add one host-level seam responsible for:

- The collection of agent panels
- Active panel selection
- Create/show/hide lifecycle
- Any shell-level coordination needed by `MainWindow`

This should be a dedicated host ViewModel/service rather than stuffing panel
collection logic directly into `MainWindow` code-behind.

### 3. Minimal execution seam

Phase 5 should include one real direct-execution path so agent panels are not
just static shells. The recommended minimal shape is:

- one OpenAI-compatible HTTP endpoint
- one focused provider/service seam for request/response handling
- one configured model per panel, or one default model shared by all panels
- direct user-to-agent request flow only

This is enough to make Phase 5 panels real without forcing a full multi-provider
architecture too early.

### 4. Shell placement

Phase 5 should reuse the current shell. The likely first move is:

- Keep Townhall in the center as the shared workspace
- Keep editor on the right as the implementation surface
- Introduce agent panels in a controlled way inside the existing right-side
  surface, or as a tabbed/stacked adjunct near the editor, without making the
  whole shell layout exploratory again

This placement decision must be explicitly recorded at M0/M1 after re-checking
the live `MainWindow` composition and `docs/DESIGN.md`.

### 5. Townhall relationship

Direct interaction with an agent panel should still produce transparent Townhall
activity at the right level, but Phase 5 should not yet implement:

- Agent-to-agent delivery
- `@mention` parsing
- debate threads
- full conversation routing semantics

Phase 5 only needs enough Townhall integration to keep the shared workspace
truthful when a user interacts with a dedicated agent surface.

## Out of Scope

- Agent-to-agent routing or `@mention` orchestration
- Broad multi-provider registry/platform work beyond one OpenAI-compatible path
- Full persistence of agent sessions or transcripts
- Automatic debate/thread branching
- Replacing Townhall as the center of gravity
- Git integration work
- Major shell redesign unless the existing shell proves unusable

## Risks To Watch

- Letting a minimal OpenAI-compatible seam expand into a full provider platform
- Mixing direct user-to-agent execution with agent-to-agent routing concerns
- Letting agent panels silently become the real primary workspace instead of
  Townhall
- Adding panel state in `MainWindow` code-behind instead of a testable host seam
- Planning tests abstractly without naming the actual test files to change
- Smuggling Phase 6 routing concerns into Phase 5 under vague "connection" wording

## Test Budget

At minimum, budget explicit tests for:

- New agent-panel ViewModel behavior
- New agent-panel host selection/lifecycle behavior
- Minimal OpenAI-compatible provider request/response behavior
- Failure states for unreachable endpoint / invalid response / cancellation
- Any `MainWindowViewModel` composition changes
- Townhall side effects when direct-agent input is sent
- Basic view rendering tests if new custom view classes are introduced

Likely files to extend or add:

- `tests/Zaide.Tests/ViewModels/MainWindowViewModelTests.cs`
- `tests/Zaide.Tests/ViewModels/` new agent-panel host/panel test files
- `tests/Zaide.Tests/Services/` new provider/service test files
- `tests/Zaide.Tests/Views/` new agent-panel view tests if custom controls are added

## Limitations (by design)

- Phase 5 may support only one OpenAI-compatible endpoint shape at first
- Agent panels may initially support only direct user-to-agent interaction
- Panel creation may start from explicit seeded agents rather than dynamic plugin
  registration
- The first version may support a fixed small number of panels before generalizing

## Exit Conditions

- [ ] At least one dedicated agent panel renders in the live shell without displacing Townhall from its primary role
- [ ] A user can type into a dedicated agent panel input surface
- [ ] Agent-specific output/status is visible in that panel
- [ ] At least one panel can send a real request to an OpenAI-compatible endpoint and render the response
- [ ] Multiple agent panels can be shown or switched in a controlled host seam
- [ ] Direct-agent interactions remain visible to the shared workspace at the intended Phase 5 level
- [ ] `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `README.md` match the implemented Phase 5 state
- [ ] Build succeeds: `dotnet build Zaide.slnx`
- [ ] Tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] Manual verification covers panel rendering, switching, input, and Townhall visibility

## Exact Next Step

Before writing Phase 5 code, create `docs/phases/phase-5.1/IMPLEMENTATION_PLAN.md`
for the first slice only:

- decide host ownership seam
- define the minimal agent-panel model/ViewModel shape
- choose the initial shell placement
- define the smallest OpenAI-compatible provider seam that supports one real request path
- avoid routing creep

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
