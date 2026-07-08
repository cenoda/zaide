# Phase 5: Agent Panels — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 4 is complete in live code/docs, not just roadmap wording
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] Re-check `src/MainWindow.axaml.cs`, `src/ViewModels/MainWindowViewModel.cs`, `src/ViewModels/TownhallViewModel.cs`, and `src/Program.cs` for the actual composition seams
- [ ] Re-check `docs/DESIGN.md` before making shell-placement claims
- [ ] Confirm the current shell can expose agent panels without rewriting the Townhall-centered layout
- [ ] Confirm Phase 5 will use one minimal OpenAI-compatible request path only, not a provider platform
- [ ] Confirm Phase 5 root docs remain aligned with the Phase 5.1 umbrella split

## Planning Status

**Planned (2026-07-08).**

Phase 5 is intentionally split. The first draft was directionally correct but too
open-ended for implementation because it left host ownership, shell placement,
and the execution seam too vague.

This document is the umbrella only. It defines the phase boundary, the required
decisions, and the sub-phase order. The actual implementation details belong in:

- `docs/phases/phase-5.1/IMPLEMENTATION_PLAN.md` (umbrella for 5.1.1–5.1.3)
- `docs/phases/phase-5.2/IMPLEMENTATION_PLAN.md`
- `docs/phases/phase-5.3/IMPLEMENTATION_PLAN.md`
- `docs/phases/phase-5.4/IMPLEMENTATION_PLAN.md`
- `docs/phases/phase-5.5/IMPLEMENTATION_PLAN.md`

## Live Baseline

Verified against the current checkout on 2026-07-08:

- `docs/roadmap/PHASES.md` marks Phase 4 complete and defines Phase 5 as dedicated agent surfaces that keep Townhall primary.
- `docs/architecture/OVERVIEW.md` describes Phase 5 as "Agent Panels" and keeps routing explicitly in Phase 6.
- `src/MainWindow.axaml.cs` composes the live shell as nav bar | left panel slot | Townhall | editor, with terminal/logs bottom and status bar below.
- `src/ViewModels/MainWindowViewModel.cs` currently owns file tree, editor tabs, terminal host, Townhall, and source control state, but no agent-panel host or per-agent panel state.
- `src/ViewModels/TownhallViewModel.cs` now provides the Phase 4 shared activity surface (session seed data, auto-logging, filterable mixed activity history), which is the correct prerequisite for Phase 5.
- The existing terminal architecture already provides the clearest host-pattern precedent: dedicated host seam in the ViewModel layer, retained long-lived control state in the view layer.

## Scope

**Goal:** Add dedicated per-agent panel surfaces that live inside the existing shell, allow direct user input, expose agent-specific status/output, and support one minimal real direct-execution path to one configured OpenAI-compatible endpoint, while keeping Townhall as the primary shared workspace.

**Boundaries:** Phase 5 covers agent-panel state, host seams, UI surfaces, one deliberately narrow direct user-to-agent execution seam, and the minimum Townhall mirroring needed to keep the shared workspace truthful. It does **not** cover multi-agent routing, debate orchestration, provider-platform generalization, transcript persistence, or shell redesign.

## Phase-Level Decisions

These decisions apply across the whole phase unless a later sub-phase records a
specific blocker with live-code evidence.

### 1. Townhall remains primary

Phase 5 adds focused agent surfaces, but Townhall remains:

- the shared workspace
- the activity ledger
- the visual center of gravity

No Phase 5 sub-phase should let agent panels silently become the real primary workspace.

### 2. Host logic is composed, not embedded

Agent-panel collection/selection/lifecycle logic belongs in a dedicated host seam,
not in `MainWindow` code-behind and not as ad-hoc state directly inside
`MainWindowViewModel`.

`MainWindowViewModel` may compose the host seam; it should not become the host.

### 3. Shell reuse is mandatory

Phase 5 must work within the current shell unless live implementation proves a
concrete blocker. The placement decision for the first implementation pass is:

- Townhall remains unchanged in the center.
- No new top-level shell column is added.
- Agent panels are introduced inside the existing right-side column (`MainWindow`
  column 5), through an internal composition seam owned by later Phase 5 work.
- That means Phase 5.2 may split or compose within the existing right column,
  but it must not reopen the outer shell grid or replace Townhall as the center
  of gravity.

Phase 5 is not permission to reopen whole-shell exploration.

### 4. Execution stays deliberately tiny

Phase 5 includes one real execution path, but only in the smallest useful form:

- one OpenAI-compatible endpoint shape
- one focused execution service seam
- one shared default configuration/model is acceptable
- non-streaming is the default
- one in-flight request per panel is acceptable

Phase 5 must not accidentally become a provider platform.

### 5. Townhall mirroring happens above the service layer

Execution services must not reference `TownhallViewModel` directly.
Townhall mirroring belongs in the ViewModel/app orchestration layer and is
explicitly scoped to direct user-to-agent interactions only.

## Why This Phase Exists

Phase 4 made Townhall a real shared workspace. The next missing capability is a
focused surface for per-agent interaction when the shared mixed feed is no longer
enough by itself.

Phase 5 exists to add that focused surface without breaking the product direction:

- Townhall stays shared and primary
- agent panels provide focused direct interaction
- routing between agents remains Phase 6 work

## Proposed Phase Split

Phase 5 is intentionally split into narrow slices:

| Sub-phase | Scope | Status |
|-----------|-------|--------|
| 5.1 | Agent panel state/model + host/composition seam umbrella (`5.1.1`–`5.1.3`) | Implemented |
| 5.2 | Agent panel UI surfaces (status/output/input) | Planned |
| 5.3 | Minimal direct-execution seam via one OpenAI-compatible endpoint | Planned |
| 5.4 | Townhall integration for direct-agent interactions | Planned |
| 5.5 | Docs sync + regression audit | Planned |

Phase 5.1 is itself split because it still carried too much decision weight:

| Sub-phase | Scope | Status |
|-----------|-------|--------|
| 5.1.1 | Single-panel state shape + host ownership decision | Implemented |
| 5.1.2 | Multi-panel host seam | Implemented |
| 5.1.3 | `MainWindowViewModel` / DI composition seam + 5.1 exit audit | Implemented |

## Phase Map

Treat the sub-phases as a dependency chain, not as parallel implementation work:

| Order | Sub-phase | Primary outcome |
|------:|-----------|-----------------|
| 1 | 5.1.1 | Single-panel shape is defined narrowly and ownership is decided |
| 2 | 5.1.2 | Multi-panel host seam exists and is testable |
| 3 | 5.1.3 | App composition exposes the host seam cleanly |
| 4 | 5.2 | User can see and interact with panel UI surfaces |
| 5 | 5.3 | One real direct-execution path works |
| 6 | 5.4 | Townhall truthfully reflects direct-agent interactions |
| 7 | 5.5 | Docs and exit audit reflect the real final state |

## Phase-Level Risks To Watch

- Letting the execution seam expand into a provider platform
- Mixing direct user-to-agent execution with Phase 6 routing concerns
- Reopening whole-shell layout exploration during 5.2
- Stuffing host state into `MainWindow` code-behind or directly into `MainWindowViewModel`
- Letting provider services take a dependency on Townhall
- Planning tests abstractly instead of naming the likely files/seams to cover

## Phase-Level Test Budget

At minimum, the whole phase should budget explicit tests for:

- single-panel state behavior
- multi-panel host selection/lifecycle behavior
- `MainWindowViewModel`/DI composition changes
- panel UI binding/rendering behavior
- execution-service request/response behavior
- failure states for missing config / endpoint failure / invalid response / cancellation policy
- Townhall side effects for direct-agent interactions

Likely files to extend or add across the phase:

- `tests/Zaide.Tests/MainWindowViewModelTests.cs`
- `tests/Zaide.Tests/ViewModels/` new agent-panel state/host/execution coordination tests
- `tests/Zaide.Tests/Services/` new execution-service test files
- `tests/Zaide.Tests/Views/` new agent-panel view tests

## Out of Scope

- Agent-to-agent routing or `@mention` orchestration
- Debate threads or routing semantics beyond direct user-to-agent interaction
- Broad provider registry/platform work beyond one minimal OpenAI-compatible path
- Tool calling
- Full persistence of agent sessions or transcripts
- Replacing Townhall as the center of gravity
- Git integration work
- Major shell redesign unless the existing shell proves unusable with concrete evidence

## Limitations (by design)

- Phase 5 may support only one OpenAI-compatible endpoint shape at first
- Execution may remain non-streaming in the first version
- Agent panels may initially support only direct user-to-agent interaction
- Panel creation may start from explicit seeded agents rather than dynamic registration
- The first version may support a fixed small number of panels before generalizing
- Townhall mirroring may remain summary-level rather than full transcript sync

## Exit Conditions

The phase is complete only when all sub-phases are complete and these conditions
are true in live code:

- [x] A dedicated agent-panel state/host/composition seam exists and is covered by tests
- [ ] At least one dedicated agent panel renders in the live shell without displacing Townhall from its primary role
- [ ] A user can type into a dedicated agent panel input surface
- [ ] Agent-specific output/status is visible in that panel
- [ ] At least one panel can send one real request to one configured OpenAI-compatible endpoint and render the response
- [ ] Multiple agent panels can be shown or switched through a controlled host seam
- [ ] Direct-agent interactions remain visible to the shared workspace at the intended Phase 5 level
- [ ] No provider registry/platform abstraction was added prematurely
- [ ] `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `README.md` match the implemented Phase 5 state
- [ ] Build succeeds: `dotnet build Zaide.slnx`
- [ ] Tests pass: `dotnet test Zaide.slnx --no-build`
- [ ] Manual verification covers panel rendering, switching, input, real request path (when valid config is available), visible failure behavior, and Townhall visibility

## Exact Next Step

Before writing Phase 5 code, start with `docs/phases/phase-5.1.1/IMPLEMENTATION_PLAN.md` only.

That first slice should do exactly this:

- record the host ownership decision
- define the minimal single-panel model/ViewModel shape
- keep the slice narrower than host implementation or shell wiring
- leave provider execution, Townhall mirroring, and routing concerns for later sub-phases

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
