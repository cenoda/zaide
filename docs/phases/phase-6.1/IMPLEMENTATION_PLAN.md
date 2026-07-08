# Phase 6.1: Routing Visibility Follow-up — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 6 is complete (build + tests pass)
- [ ] Re-check `src/ViewModels/MainWindowViewModel.cs` line 191-233 (SendAgentMessageAsync)
- [ ] Re-check `src/ViewModels/AgentRouter.cs` for RouteAndExecuteAsync behavior
- [ ] Re-check `src/Services/MentionParser.cs` for failure case returns
- [ ] Confirm no dedicated router test file exists under `tests/Zaide.Tests/ViewModels/`

## Planning Status

**Planned (2026-07-08).**

This is a small follow-up phase to close the routing-visibility gaps documented in
the Phase 6 Known Gaps section. It does **not** introduce git integration, multi-hop
routing, or any feature expansion.

## Goal

Make Phase 6 routing failures and routed-flow outcomes visible in Townhall,
and add focused test coverage for the router seam. All Townhall mirroring
changes belong in `MainWindowViewModel`; `AgentRouter` itself does not gain
a Townhall dependency.

## Scope

This phase covers only:

- Consuming `RouteResult` in `SendAgentMessageAsync(...)` to surface routing failures in Townhall
- Mirroring unknown-target and routed-flow outcomes into Townhall truthfully
- Adding dedicated `AgentRouter` orchestration tests (routing resolution and target execution only — no Townhall assertions at the router layer)

This phase does **not** cover:

- Multi-mention support beyond existing failure behavior
- History/log persistence for routed messages
- Provider/registry abstraction
- Any git-related work

## Live Gaps To Fix

The following gaps are documented in Phase 6 Known Gaps and remain unfixed:

1. **Unknown/ambiguous/multi-mention failures are detected but not surfaced**
   - `MentionParser` returns explicit `RouteResult` failure (lines 483-493)
   - `MainWindowViewModel.SendAgentMessageAsync` discards `RouteResult` (`_ = ...`)
   - No Townhall or panel-visible error entry is generated

2. **Routed responses are not surfaced in Townhall**
   - `AgentRouter` executes routed content on target panel (line 495-502)
   - `SendAgentMessageAsync` mirrors only source panel output afterward
   - Target panel's executed response lands nowhere visible in Townhall

3. **No dedicated AgentRouter test file**
   - Router behavior covered indirectly via MentionParserTests and MainWindowViewModelTests
   - `RouteAndExecuteAsync` path lacks focused unit tests

## M0 Locked Decisions

### 1. Visibility surface: Townhall-only

**Decision:** Phase 6.1 uses Townhall as the sole visibility surface for routing
failures and routed-flow outcomes. No panel/status bar UI changes are required.

**Rationale:** `AgentRouter` has no Townhall dependency and must not gain one
(Phase 6 boundary, `docs/phases/phase-6/IMPLEMENTATION_PLAN.md` line 530).
All Townhall mirroring belongs in `MainWindowViewModel.SendAgentMessageAsync`,
which already owns the thin composition seam. Panel/status feedback is out of
scope for this phase.

### 2. Routed-success mirroring reads from target panel, not RouteResult

**Decision:** `RouteResult` carries only `Success`, `Request`, and `FailureReason`
(`src/Models/RouteResult.cs`). It does **not** carry execution outcome, resolved
target panel id, or mirrored output content. Routed-success mirroring must read
post-execution state from the resolved target panel via
`Request.TargetAgentName` + `IAgentPanelHost.Panels`.

**Locked flow for routed success:**
1. `SendAgentMessageAsync` receives `RouteResult` with `Success = true` and `Request.IsDirectSend = false`
2. After `RouteAndExecuteAsync` completes, look up the target panel by `Request.TargetAgentName`
3. Read the target panel's post-execution `OutputHistory` and `Status`
4. Mirror the target panel's response into Townhall (same pattern as direct-send mirroring)

### 3. Test ownership split

**Decision:** `AgentRouterTests` covers routing resolution and target execution
only. Townhall visibility assertions belong in `MainWindowViewModelTests`.

- `AgentRouterTests`: parse resolution, direct-send vs routed-send dispatch,
  unknown/ambiguous target failure, no Townhall dependency
- `MainWindowViewModelTests`: unknown-target Townhall error entry, routed-success
  Townhall mirroring from target panel, direct-send mirroring (existing)

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Lock the 6.1 visibility decisions (Townhall-only, no panel/status UI change, RouteResult shape constraints, test ownership split) | Plan truth-sync |
| M1 | Consume `RouteResult` in `MainWindowViewModel.SendAgentMessageAsync`: on parse failure mirror a Townhall error entry; on routed success read target panel output and mirror into Townhall | `MainWindowViewModelTests` for unknown target, routed success, ambiguous target |
| M2 | Add dedicated `AgentRouterTests.cs` covering routing resolution and target execution only (no Townhall assertions) | Router tests for direct-send dispatch, routed-send dispatch, unknown/ambiguous failure |
| M3 | Verify build/tests green; no UI changes beyond Townhall | Build + test verification |

## Likely Implementation Shape

- `MainWindowViewModel.SendAgentMessageAsync` changes:
  - Inspect `RouteResult` returned by `RouteAndExecuteAsync` (currently discarded via `_ = ...`)
  - On parse failure (`result.Success == false`): mirror a visible error entry into Townhall
  - On routed success (`result.Request.IsDirectSend == false`): look up target panel via `Request.TargetAgentName` + `IAgentPanelHost`, read its post-execution `OutputHistory`, mirror into Townhall
- `tests/Zaide.Tests/ViewModels/AgentRouterTests.cs` new file — routing resolution and target execution only
- No changes to `MentionParser` (already correct)
- No changes to `AgentRouter` (already correct — no Townhall dependency)

## Out of Scope

- Multi-mention support
- History persistence
- Provider abstraction
- Git integration
- Branch management
- Rich debate visualization

## Limitations (by design)

- Single unknown-target error message (no elaborate UI)
- Routed response mirrors only the final target output
- No change to mention parsing or matching rules

## Exit Conditions

- [ ] Unknown-target failures produce visible Townhall error entries (via `MainWindowViewModel`)
- [ ] Routed-flow outcomes produce visible Townhall entries (target panel output mirrored via `MainWindowViewModel`)
- [ ] Dedicated `AgentRouter` test file exists with passing tests (routing resolution + target execution only)
- [ ] `AgentRouter` remains free of Townhall dependency
- [ ] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] Tests pass: `dotnet test Zaide.slnx --no-build`

## Exact Next Step

After 6.1 is complete, Phase 7 can begin with clean routing visibility.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins