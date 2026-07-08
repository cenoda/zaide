# Phase 6.1: Routing Visibility Follow-up — Implementation Plan

## Pre-Implementation Verification (M0 — Locked)

- [x] Confirm Phase 6 is complete (build + tests pass)
- [x] Re-check `src/ViewModels/MainWindowViewModel.cs` line 191-233 (SendAgentMessageAsync)
- [x] Re-check `src/ViewModels/AgentRouter.cs` for RouteAndExecuteAsync behavior
- [x] Re-check `src/Services/MentionParser.cs` for failure case returns
- [x] Confirm no dedicated router test file exists under `tests/Zaide.Tests/ViewModels/`

## Planning Status

**M0 locked (2026-07-08).**

All 4 planning decisions verified against live code. Build: 0 errors / 0 warnings.
Tests: 724 passed / 0 failed. Rollback hash recorded for M3:
`67a393d6757d285c567db1633b0edd693c43e5dd`.

Doc correction: `MainWindowViewModelTests.cs` lives at `tests/Zaide.Tests/`
(not `tests/Zaide.Tests/ViewModels/`), which is where the existing mirroring
tests and `CreateMirrorTestViewModel` helper (lines 285-339) are defined.

**Result: Plan is locked ✓ — proceed to M1.**

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
   - `MentionParser` returns explicit `RouteResult` failure (known gaps, Phase 6 plan)
   - `MainWindowViewModel.SendAgentMessageAsync` captures `RouteResult` as `var routeResult` (line 199) but never reads it — dead variable
   - No Townhall or panel-visible error entry is generated

2. **Routed responses are not surfaced in Townhall**
   - `AgentRouter` executes routed content on target panel (known gaps, Phase 6 plan)
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
(Phase 6 boundary, `src/ViewModels/AgentRouter.cs` — `MentionParser` + `IAgentPanelHost`
+ `IAgentExecutionCoordinator` only). All Townhall mirroring belongs in
`MainWindowViewModel.SendAgentMessageAsync`, which already owns the thin
composition seam. Panel/status feedback is out of scope.

### 2. Routed-success mirroring reads from target panel, not RouteResult

**Decision:** `RouteResult` carries only `Success`, `Request`, and `FailureReason`
(`src/Models/RouteResult.cs`). It does **not** carry execution outcome, resolved
target panel id, or mirrored output content. Routed-success mirroring must read
post-execution state from the resolved target panel via
`Request.TargetAgentName` + `IAgentPanelHost.Panels`.

**Locked flow for routed success:**
1. `SendAgentMessageAsync` receives `RouteResult` with `Success = true` and `Request.IsDirectSend = false`
2. After `RouteAndExecuteAsync` completes, look up the target panel by `Request.TargetAgentName`
3. If target panel is found, read its post-execution `OutputHistory` and `Status`
4. If target panel is not found (closed/disposed between routing and mirroring), skip — no Townhall entry
5. Mirror the target panel's last assistant/error entry using the same guard pattern as existing direct-send mirroring (`MainWindowViewModel.cs` lines 219-232)

### 3. Townhall entry format

**Decision:** All new Townhall entries use the same `AddMirroredActivity` pattern
as existing mirroring.

**Failure entries:**
- `kind`: `TownhallMessageKind.AgentError`
- `senderId`: source panel's `AgentId`
- `senderName`: source panel's `AgentName`
- `content`: `"Routing failed: {FailureReason}"`
  - `FailureReason` is the exact string from `MentionParser`: `"Unknown target"`,
    `"Multiple mentions"`, `"Ambiguous target"`, `"Empty input"`,
    `"Empty content after stripping"`, `"Empty mention target"`

**Routed-success entries (from target panel):**
- Same exact logic as direct-send mirroring (`MainWindowViewModel.cs` lines 219-232)
- If target panel's last output starts with `"Assistant: "` → `TownhallMessageKind.Chat`, target panel's identity
- If target panel status is `"Error"` and last output starts with `"Error: "` → `TownhallMessageKind.AgentError`, target panel's identity
- Otherwise → skip (no entry to mirror), consistent with existing guard

**Edge cases:**
- `"Empty input"` / `"Empty content after stripping"` / `"Empty mention target"`: user's raw message is already mirrored at line 192 before routing. M1 only adds the error entry after that — no duplication or modification of the user entry.
- Target panel vanished between routing and mirroring: skip gracefully.
- `"Multiple mentions"`: no specific mention target. Content = `"Routing failed: Multiple mentions"`, sender = source panel.

### 4. Test ownership split

**Decision:** `AgentRouterTests` covers routing resolution and target execution
only. Townhall visibility assertions belong in `MainWindowViewModelTests`.

- `AgentRouterTests`: parse resolution, direct-send vs routed-send dispatch,
  all failure cases (unknown/ambiguous/multi/empty), no Townhall dependency
- `MainWindowViewModelTests`: unknown-target Townhall error entry, routed-success
  Townhall mirroring from target panel, target-panel-vanished, direct-send mirroring (existing)

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Lock the 6.1 visibility decisions (Townhall-only, entry formats, RouteResult shape, edge cases, test ownership split) — **DONE** | Plan truth-sync |
| M1 | Consume `RouteResult` in `MainWindowViewModel.SendAgentMessageAsync`: on parse failure mirror a Townhall error entry; on routed success read target panel output and mirror into Townhall | `MainWindowViewModelTests` for unknown target, routed success, ambiguous target, vanished target panel |
| M2 | Add dedicated `AgentRouterTests.cs` covering routing resolution and target execution only (no Townhall assertions) | Router tests for direct-send dispatch, routed-send dispatch, all failure cases (unknown/ambiguous/multi/empty) |
| M3 | Verify build/tests green; no UI changes beyond Townhall | Build + test verification |

## Likely Implementation Shape

- `MainWindowViewModel.SendAgentMessageAsync` changes:
  - Inspect `RouteResult` returned by `RouteAndExecuteAsync` (currently captured as `var routeResult` at line 199 but never read — dead variable)
  - On parse failure (`result.Success == false`): mirror `TownhallMessageKind.AgentError` with source panel identity and `"Routing failed: {FailureReason}"`
  - On routed success (`result.Request.IsDirectSend == false`): look up target panel via `Request.TargetAgentName` + `IAgentPanelHost`, read post-execution `OutputHistory`, apply same `"Assistant: "` / `"Error: "` guard as direct-send mirroring (lines 219-232), mirror with target panel identity
  - On target panel not found: skip mirroring silently
- `tests/Zaide.Tests/ViewModels/AgentRouterTests.cs` new file — routing resolution and target execution only
- No changes to `MentionParser` (already correct)
- No changes to `AgentRouter` (already correct — no Townhall dependency)

## Out of Scope

- Multi-mention support beyond existing failure behavior
- History/log persistence for routed messages
- Provider/registry abstraction
- Any git-related work
- Panel/status bar UI changes

## Limitations (by design)

- Failure entries use `"Routing failed: {reason}"` — no per-failure-type UI customization
- Routed response mirrors only the last assistant/error entry from target panel's `OutputHistory`
- No change to mention parsing or matching rules
- Target panel vanishing between routing and mirroring results in no Townhall entry (silent skip)

## Exit Conditions

- [ ] Unknown-target failures produce visible Townhall error entries (`AgentError` kind, source panel identity, `"Routing failed: Unknown target"`)
- [ ] Routed-flow outcomes produce visible Townhall entries (target panel's last assistant output mirrored with target panel identity)
- [ ] Vanished target panel between routing and mirroring is handled gracefully (no crash, no entry)
- [ ] All 6 `MentionParser` failure reasons are mapped to Townhall entries
- [ ] Dedicated `AgentRouter` test file exists with passing tests (routing resolution + target execution only, no Townhall)
- [ ] `AgentRouter` remains free of Townhall dependency
- [ ] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] Tests pass: `dotnet test Zaide.slnx --no-build`

## Exact Next Step

After 6.1 is complete, Phase 7 can begin with clean routing visibility.

## Rollback Plan

- Commit hash to revert to: TBD when implementation begins
