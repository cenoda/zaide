# Phase 3.9.1: Terminal Tabs — Implementation Plan

## Pre-Implementation Verification

- [x] Read `docs/phases/phase-3.9.1/BRIEF.md`
- [x] Re-read `docs/phases/phase-3.9/IMPLEMENTATION_PLAN.md` and `TOFIX.md`
- [x] Re-read `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `docs/CONVENTIONS.md`
- [x] Verify current build succeeds: `dotnet build Zaide.slnx` — 0 warnings, 0 errors (verified 2026-07-07)
- [x] Verify current tests pass: `dotnet test Zaide.slnx --no-build` — 536 passed, 0 failed (verified 2026-07-07)
- [x] Verify current Phase 3.9 UX baseline is complete before widening into tabs
- [x] Verify current bottom-panel composition against live code (`MainWindow` hosts one `TerminalPanel` bound to one `TerminalViewModel`)
- [x] Verify current DI/session seam against live code (`ITerminalService` and `TerminalViewModel` are singleton registrations today)
- [ ] Manual baseline on Linux still feels stable before session-multiplexing work:
  - [ ] existing single terminal still starts and accepts input
  - [ ] scrollback/search/selection from Phase 3.9 still work
  - [ ] alternate-screen isolation still holds in `less` / `vim`
  - [ ] restart and clear still work after a shell exits
- [x] Decide and document the per-tab log ownership rule before M2 implementation — **per-tab logs** for Phase 3.9.1
- [x] Decide and document the per-tab UI-state ownership rule before M3 implementation — **each tab retains its own `TerminalPanel` instance** so search/viewport/selection state stay session-local
- [x] Decide and document shell feedback ownership before M2 implementation — status-bar terminal errors reflect the **active session** only; host-level creation failures surface separately

## Planning Status

**M1: Complete.** Phase 3.9 introduced the full terminal UX (render control, search, scrollback, selection, logs) but intentionally stopped before terminal tabs because the app architecture still assumed exactly one terminal session. M1 removes that blocker by introducing `ITerminalSessionFactory` and `ITerminalHost` so `ITerminalService` and `TerminalViewModel` are instantiable per session. The existing single-session behavior is preserved as the first consumer of the new seam.

Current architecture (post-M1):

Verified live seams on 2026-07-07:

- `src/Program.cs`
  - now registers `ITerminalSessionFactory` → `TerminalSessionFactory` and
    `ITerminalHost` → `TerminalHost` (instead of the old singleton
    `ITerminalService`/`TerminalViewModel`). Sessions are created per tab via
    the factory, not as app-wide singletons.
  - this is the DI seam that unblocks one-terminal-per-tab behavior
- `src/ViewModels/TerminalViewModel.cs`
  - models one terminal session and owns lifecycle, parser/screen state, logs,
    search-visible snapshot projection, and restart/clear commands
  - constructor already accepts `ITerminalService` and has a seam-friendly
    internal test constructor, which makes per-session instantiation feasible
- `src/Views/TerminalPanel.cs`
  - currently expects one `TerminalViewModel` and already encapsulates the full
    terminal UI surface (toolbar, render control, logs, search, input forwarding)
  - importantly, it also owns meaningful per-session **view-layer state** today:
    search query/results live in `TerminalPanel`, and viewport/selection/search-
    highlight state live in `TerminalRenderControl`
  - this means a single shared panel rebound across tabs would smear or discard
    session-local UI state unless that state were migrated elsewhere
- `src/MainWindow.axaml.cs`
  - currently creates exactly one `TerminalPanel` and directly performs
    `_terminalPanel.FocusTerminal()` when the bottom panel opens; startup is
    routed through `TerminalHost.EnsureActiveSessionStartedAsync()`
  - bottom-panel composition will need a tab strip / host surface **and** a
    host-level focus seam wired through to the active tab's panel
  - this is the natural place for a view-layer `TerminalPanel` cache or a
    dedicated view-host, so retained panels stay out of ViewModels
- `src/ViewModels/MainWindowViewModel.cs`
  - now exposes `ITerminalHost` instead of a concrete `TerminalViewModel`.
    `TerminalHost.StartupError` is subscribed for status-bar surfacing.
  - shell-level commands and bottom-panel visibility already live here, making
    it the likely host location for terminal-tab coordination
- `src/Services/ITerminalService.cs` and `src/Services/LinuxTerminalService.cs`
  - current service contract is already per-session by shape
    (`StartAsync`, `WriteAsync`, `StopAsync`, `Resize`, events)
  - backend rewrite is not needed; the missing piece is lifecycle/factory
    ownership, not PTY capability
- Current focused tests already exist in:
  - `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`
  - `tests/Zaide.Tests/Services/LinuxTerminalServiceTests.cs`
  - `tests/Zaide.Tests/Views/TerminalRenderControlTests.cs`
  - `tests/Zaide.Tests/Views/TerminalGeometryTests.cs`

## Scope

**Goal:** Add lightweight terminal tabs to the bottom panel so users can run multiple independent shell sessions, each with its own terminal state, while preserving the Phase 3.8 correctness contract and the Phase 3.9 UX polish.

**Boundaries:**

- Do not rewrite the PTY backend or terminal parser/screen architecture
- Do not add terminal splits, pane layouts, detached windows, or drag-reorder
- Do not persist terminal tabs/sessions across app restarts
- Do not build a generalized workspace/session subsystem beyond what terminal tabs require now
- Do not start Phase 4 Townhall/activity work here
- Keep the design Linux-focused like the existing terminal backend; no Windows/macOS backend work
- Prefer explicit documented limitations over speculative abstractions

## Known Gaps This Phase Targets

These are the concrete gaps implied by the current code and docs:

- The app currently owns exactly one terminal session because both `ITerminalService` and `TerminalViewModel` are registered as singletons
- `MainWindow` currently hard-wires one `TerminalPanel`, so the bottom panel cannot switch among multiple sessions
- The shell currently has no host object for tab creation, activation, closing, or disposal
- The current terminal/logs UI assumes one active session, so log ownership must be made explicit before implementation

## Phase 3.9.1 Design Decisions

These decisions keep the phase narrow and verifiable:

1. **One tab = one `TerminalViewModel` + one `ITerminalService` instance.**
   Do not share a single backend across tabs. Independent session ownership is the point of the feature.

2. **Reuse `TerminalPanel` as the per-session surface.**
   Do not split the polished terminal UI back apart. The tab host should switch among whole terminal surfaces or whole session view-model bindings.

3. **Introduce a small host/factory seam instead of broad DI redesign.**
   The preferred shape is:
   - `TerminalViewModel` becomes a per-session type
   - `ITerminalService`/`LinuxTerminalService` become per-session creations
   - a small host (for example `TerminalTabsViewModel` and/or `ITerminalSessionFactory`) owns creation, activation, and disposal

4. **Focus correctness beats feature breadth.**
   Tab close/disposal, session isolation, and active-tab input focus are more important than extras such as reordering, persistence, or session metadata history.

5. **Log ownership is per tab.**
   Phase 3.9.1 will keep `LogEntries` session-local. Each terminal tab owns its own logs, matching the existing one-session-one-log-list design and avoiding cross-session ambiguity.

6. **UI state ownership is per tab.**
   Because `TerminalPanel` and `TerminalRenderControl` currently own search/viewport/selection state, each tab will retain its own `TerminalPanel` instance in Phase 3.9.1. That retained panel cache must live in the **view layer** (for example, `MainWindow` or a dedicated view/host object), not in any ViewModel. Do not rebind one shared panel across multiple sessions unless that UI state is first moved into an explicitly session-owned layer.

7. **Terminal error feedback follows the active session.**
   Status-bar terminal messages should reflect the active tab's session-level `StartupError` / terminal status. Host-level creation failures (for example, failure to create a new session at all) should surface separately and must not be confused with per-session shell errors.

## Recommended Architecture Shape

This is the narrowest architecture that appears consistent with the live code:

### Per-session objects

For each terminal tab, create:

- one `ITerminalService` / `LinuxTerminalService`
- one `TerminalViewModel`
- one tab record/view-model containing title, session id, active state, and close command

### Host layer

Add a small terminal-tab host that owns:

- collection of open terminal tabs
- active tab selection
- `NewTab`, `CloseTab`, and `ActivateTab` commands
- session disposal when a tab closes
- initial tab creation policy (lazy first tab vs create-on-open)

### UI layer

Replace the single hard-wired terminal control in the bottom panel with:

- a small tab strip for terminal sessions
- a view-layer cache of one retained `TerminalPanel` instance per tab/session
- an active-session host that shows only the selected tab's panel

This cache must live in the view layer, not in a ViewModel, to preserve the project MVVM rule that ViewModels never reference views. `MainWindow` or a dedicated view host is the expected owner.

This is the lowest-risk fit for the current code because `TerminalPanel` and `TerminalRenderControl` already own meaningful per-session UI state (search query/results, viewport, selection, active highlights). Rebinding one shared panel would require additional state migration work that this phase is explicitly avoiding.

## Milestones (Incremental)

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate: current build/tests/live seams verified and architecture choice pinned down | `dotnet build`, `dotnet test`, code audit, focused Linux smoke | ⬜ Build/test/code-audit ready; manual Linux smoke pending |
| M1 | Per-session creation seam: make terminal service/view-model instantiable per tab without regressing single-session behavior | targeted VM/service tests + build/test | ✅ Complete (`ITerminalSessionFactory` + `ITerminalHost` wired, 549 tests pass) |
| M2 | Terminal tab host: collection, active-tab switching, create/close/dispose lifecycle | host-viewmodel tests for session isolation and disposal | ⬜ |
| M3 | Bottom-panel UI integration: tab strip + active session surface + focus/start behavior | focused UI/view tests + manual tab switching smoke | ⬜ |
| M4 | Docs sync and exit audit | `dotnet build`, `dotnet test`, roadmap/doc sync, Linux smoke, TOFIX update | ⬜ |

## Detailed Milestone Plans

### M1: Per-Session Creation Seam

**Why this belongs first:**

The current singleton registrations are the root blocker. Until one `TerminalViewModel` can own one independently-created `ITerminalService`, the app cannot support real terminal tabs.

**Files likely touched:**

- `src/Program.cs`
- `src/ViewModels/TerminalViewModel.cs`
- `src/Services/ITerminalService.cs` (only if a tiny factory interface is introduced nearby)
- `src/Services/LinuxTerminalService.cs` (implementation likely unchanged)
- `tests/Zaide.Tests/ViewModels/TerminalViewModelTests.cs`
- `tests/Zaide.Tests/Services/LinuxTerminalServiceTests.cs`

**Planned change shape:**

1. Stop treating `TerminalViewModel` as a singleton app-wide shell object
2. Introduce the smallest creation seam needed for multiple sessions:
   - either register `ITerminalService`/`TerminalViewModel` as transient and compose them through a small factory
   - or add an explicit terminal session factory that constructs both together
3. Preserve the existing single-session behavior as the first consumer of the new seam before adding tabs
4. Keep `LinuxTerminalService` itself per-instance, not multi-session-aware
5. Define a host-level session-start seam before UI integration begins:
   - a host object, not `MainWindow`, becomes responsible for `EnsureActiveSessionStartedAsync()`
   - `MainWindow` should no longer call `EnsureStartedAsync()` on a single concrete `TerminalViewModel`
6. Ensure disposal rules are explicit:
   - disposing a terminal tab disposes its `TerminalViewModel`
   - disposing `TerminalViewModel` disposes its owned `ITerminalService`
7. Do not add persistence or generalized keyed-service infrastructure unless the narrow factory seam proves insufficient

**Tests (M1):**

- prove two independently-created `TerminalViewModel` instances can exist without shared state
- prove disposing one session does not affect another session's service contract
- existing restart/startup tests still pass under the new creation model
- focused construction tests for any new factory/host helper

### M2: Terminal Tab Host and Session Lifecycle

**Why this belongs in 3.9.1:**

Once per-session creation is possible, the app needs a small coordinator that owns tabs as product-level state: open, activate, close, and dispose.

**Files likely touched:**

- new host types in `src/ViewModels/` (for example `TerminalTabViewModel`, `TerminalTabsViewModel`, small session-factory abstractions)
- `src/ViewModels/MainWindowViewModel.cs`
- tests in `tests/Zaide.Tests/ViewModels/`

**Planned change shape:**

1. Add a small terminal-tab host with:
   - collection of terminal tabs
   - active tab
   - `NewTab` command
   - `CloseTab` command
   - tab activation command
   - `EnsureActiveSessionStartedAsync()` (or equivalent) so shell startup is no longer hard-wired in `MainWindow`
   - active-session terminal status/error projection for the status bar
2. Choose a minimal creation policy:
   - preferred: create the first tab lazily when the bottom panel is first shown or when the user explicitly opens a new tab
3. Define close behavior explicitly:
   - closing the active tab activates a neighbor if one exists
   - closing the last tab either hides the terminal surface or leaves an empty-state/new-tab affordance
4. Keep logs per session (fixed phase decision)
5. Define status-bar behavior explicitly:
   - active session startup/runtime terminal errors surface in the main status text
   - host-level failures to create/open a new tab surface as host errors
   - inactive-session errors do not steal focus/status unless that tab becomes active
6. Keep bottom-panel visibility separate from tab lifetime; toggling the panel should not destroy sessions

**Tests (M2):**

- `NewTerminalTab_CreatesIndependentSession`
- `CloseTerminalTab_DisposesItsSession`
- `SwitchTerminalTab_PreservesEachSessionSnapshot`
- active tab fallback when the current tab closes
- toggling bottom-panel visibility does not destroy existing sessions
- per-tab log ownership behaves as chosen

### M3: Bottom-Panel UI Integration

**Why this belongs after M2:**

The host behavior should be proven in tests first. Then the UI layer only has to bind to that host and route focus/input to the active session surface.

**Files likely touched:**

- `src/MainWindow.axaml.cs`
- `src/Views/TerminalPanel.cs` (likely adapted to work as an active-session surface inside a host)
- possibly a new small terminal-tab-strip view or dedicated view host in `src/Views/`
- tests in `tests/Zaide.Tests/Views/`

**Planned change shape:**

1. Replace the single hard-coded terminal surface in the bottom panel with a small terminal-tab host UI
2. Preserve per-session view-layer state by retaining one `TerminalPanel` instance per tab, shown/hidden by the active-session host
3. Add a minimal tab strip with:
   - active tab title
   - new-tab action
   - close-tab action
4. Define tab title policy narrowly:
   - acceptable first pass: `Terminal 1`, `Terminal 2`, ...
   - optional improvement: append state when exited, but do not over-design titles
5. Own the retained panel cache in the view host (`MainWindow` or a dedicated view) so ViewModels never store or reference `TerminalPanel` instances
6. Preserve focus/start behavior through an explicit host seam:
   - opening the bottom panel should call host-level active-session start/focus behavior, not direct single-session calls from `MainWindow`
   - switching tabs should focus the active terminal surface
   - full-screen TUI input should still go only to the active session

**Tests (M3):**

- active session surface receives focus/input bindings
- switching tabs does not leak snapshot/log/search/viewport/selection state between sessions
- retained `TerminalPanel` instances are owned by the view host, not any ViewModel
- tab UI operations do not break the existing bottom-panel toggle behavior
- alternate-screen isolation still applies per active session
- active-session terminal errors/status projection behaves as documented

**Manual smoke for M3:**

- open two or more tabs
- run different commands in each and confirm outputs remain isolated
- switch tabs and confirm input/focus follows the active tab only
- close a running/exited tab and confirm remaining tabs still work
- verify `less`/`vim` in one tab does not affect another tab's main buffer/search/selection state

### M4: Docs Sync and Exit Audit

**Why this belongs in 3.9.1:**

Terminal tabs are the first terminal feature in this repo that require a host/session layer, so the docs need to record the final architecture boundary clearly.

**Files likely touched:**

- `docs/phases/phase-3.9.1/IMPLEMENTATION_PLAN.md`
- `docs/phases/phase-3.9.1/TOFIX.md`
- `docs/roadmap/PHASES.md`
- `docs/architecture/OVERVIEW.md` (if the session-host seam becomes part of the architecture truth)
- `docs/LIBRARIES.md` only if a new package is added (not currently planned)

**Planned change shape:**

1. Re-run the phase gates after M1–M3 complete
2. Update roadmap/docs so terminal tabs are marked complete without overlapping Phase 4 claims
3. Record any remaining polish or lifecycle issues in `TOFIX.md`
4. Keep the architecture notes explicit: terminal tabs add a small session host, not a broad workspace/session subsystem

## Limitations (by design)

- No persisted terminal sessions across app restarts
- No split panes, detached windows, or drag-reorder
- Tab titles may remain generic in the first pass
- Session host is terminal-specific, not a generalized app-wide session framework
- Manual Linux smoke remains required because multi-session PTY behavior and focus feel are not fully capturable by unit tests

## Exit Conditions

- [ ] `dotnet build Zaide.slnx` succeeds with 0 warnings, 0 errors
- [ ] `dotnet test Zaide.slnx --no-build` passes
- [ ] Multiple terminal tabs can be created, activated, and closed
- [ ] Each tab owns an independent shell session and independent terminal state
- [ ] Closing one tab disposes only that session
- [ ] Toggling the bottom panel does not destroy surviving terminal sessions
- [ ] Active-tab focus/input behavior works correctly
- [ ] Active-session startup/focus behavior works through the host seam without direct single-session calls in `MainWindow`
- [ ] Active-session terminal errors/status surface correctly in the shell
- [ ] Phase 3.8 alternate-screen isolation still holds per session
- [ ] Phase 3.9 search/scrollback/selection polish still works within each session, with no cross-tab state smearing
- [ ] `docs/roadmap/PHASES.md` and any touched architecture docs remain in sync

## Rollback Plan

- Commit hash to revert to: `adc95e6`
- If multi-session ownership proves architecturally too invasive, revert to the single-terminal host and re-plan in a dedicated refactor phase rather than widening Phase 3.9.1 beyond lightweight tabs
