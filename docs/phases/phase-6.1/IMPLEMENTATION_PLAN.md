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

Make Phase 6 routing failures and routed-flow outcomes visible in Townhall and
the UI, and add focused test coverage for the router seam.

## Scope

This phase covers only:

- Consuming `RouteResult` in `SendAgentMessageAsync(...)` to surface routing failures
- Mirroring unknown-target and routed-flow outcomes into Townhall truthfully
- Adding dedicated `AgentRouter` orchestration tests
- Optional: visible panel/status feedback for routing failure

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

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Lock the 6.1 visibility seam: define how unknown-target errors, ambiguous-target errors, and routed-flow outcomes appear in Townhall; decide whether `RouteResult` drives both panel and Townhall feedback | Plan truth-sync + minimal POC |
| M1 | Consume `RouteResult` in `MainWindowViewModel.SendAgentMessageAsync` to mirror visible failures and routed outcomes into Townhall | ViewModel tests for unknown target, routed success, ambiguous target |
| M2 | Add dedicated `AgentRouterTests.cs` with coverage for parse resolution, target execution, and Townhall mirroring behavior | Focused router/orchestration tests |
| M3 | Ensure status/panel feedback for routing failures (if any UI change is needed beyond Townhall) and verify build/tests green | Build + test verification |

## Likely Implementation Shape

- `MainWindowViewModel.SendAgentMessageAsync` inspection of `RouteResult`
  - On parse failure: mirror a visible error entry into Townhall
  - On routed success: mirror target panel response into Townhall
- `tests/Zaide.Tests/ViewModels/AgentRouterTests.cs` new file
- No changes to `MentionParser` (already correct)
- No changes to `AgentRouter` (already correct)

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

- [ ] Unknown-target failures produce visible Townhall error entries
- [ ] Routed-flow outcomes produce visible Townhall entries
- [ ] Dedicated `AgentRouter` test file exists with passing tests
- [ ] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] Tests pass: `dotnet test Zaide.slnx --no-build`

## Exact Next Step

After 6.1 is complete, Phase 7 can begin with clean routing visibility.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins