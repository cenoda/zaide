# A2 Wiring Audit — `A2_TERMINAL`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_TERMINAL` (fourteenth A2 slice; prior:
`A2_AGENT_SEND`, `A2_MULTI_AGENT_ROUTING`,
`A2_TRACE_MEMORY_USAGE_TERMINATION`,
`A2_RESTART_RECOVERY_AND_CONTEXT`,
`A2_TOOLS_PERMISSIONS`,
`A2_AGENT_CREATION_AND_BACKEND_ONBOARDING`,
`A2_TOWNHALL_AND_CONVERSATIONS`,
`A2_FIRST_LAUNCH_AND_SETTINGS`,
`A2_WORKSPACE_AND_PROJECT_OPENING`,
`A2_FILE_NAVIGATION_AND_EDITING`,
`A2_SEARCH_AND_COMMAND_DISCOVERY`,
`A2_BUILD_RUN_AND_TEST`,
`A2_DEBUGGING_AND_OUTPUT`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`f328993385245125282fec40d6e1f34fcaba138d` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `f328993385245125282fec40d6e1f34fcaba138d` |
| `git rev-parse origin/master` | `f328993385245125282fec40d6e1f34fcaba138d` |
| Working tree at audit start | Clean (`git status --short` empty) |
| Thirteen published A2 evidence files | Present (Agent Send through Debugging/Output) |
| This slice evidence file before write | Absent |
| A1 acceptance authority | [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md) (2026-07-30) |
| Production code modified | No |
| Tests modified | No |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` edited | No |
| Earlier evidence edited | No |
| Issues / deferred findings edited | No |
| Real user profile / settings / secrets / workspace read or written | No |
| App launched | No |
| Build or tests run | No |
| A3 executed | No |
| Commit / push | No |

**Safety boundary:** this slice is A2 wiring inspection only. Production
source is verdict authority. Phase 3 / 3.6–3.9 / 3.9.1 closeout materials
and unit/VM tests are corroboration only. Live Avalonia rendering, real PTY
sessions, TUI programs (`less`, `vim`, `htop`), and disposable-profile
terminal smoke are not claimed from source alone. **No real user profile,
settings, secrets, or opened workspace path was accessed.**

**Verdict rows (this slice only):** `A1-TR-01` and `A1-TR-02`. No new
verdicts for AS, MR, TC, TP, AC, TH, FL, WO, FN, SC, BR, DB, GT, or other
TR rows. Shared seam overlap with `A2_FIRST_LAUNCH_AND_SETTINGS`
(`view.toggleBottomPanel`, terminal font settings), `A2_BUILD_RUN_AND_TEST`
(`BottomPanelMode` exclusivity, PTY vs redirected Output), and
`A2_SEARCH_AND_COMMAND_DISCOVERY` (`ICommandRegistry` registration of
`view.toggleBottomPanel`) is called out under the rows but is **not**
re-verdicted in this slice.

---

## 2. Sources inspected

### 2.1 Documentation

- [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md) (§4 journey 7 Terminal; §5 schema;
  §17.8 A2 progress)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§7 Terminal rows `A1-TR-01`,
  `A1-TR-02`; §17.8 progress table)
- [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- V1 roadmap: [PHASES.md §"Phase 3: Terminal"](../../../roadmap/PHASES.md#phase-3-terminal)
- Phase 3 family plans and closeout:
  [phase-3/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-3/IMPLEMENTATION_PLAN.md),
  [phase-3/TOFIX.md](../../../phases/v1/phase-3/TOFIX.md),
  [phase-3.6/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-3.6/IMPLEMENTATION_PLAN.md),
  [phase-3.7/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-3.7/IMPLEMENTATION_PLAN.md),
  [phase-3.8/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-3.8/IMPLEMENTATION_PLAN.md),
  [phase-3.8/TOFIX.md](../../../phases/v1/phase-3.8/TOFIX.md),
  [phase-3.9/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-3.9/IMPLEMENTATION_PLAN.md),
  [phase-3.9/TOFIX.md](../../../phases/v1/phase-3.9/TOFIX.md),
  [phase-3.9.1/IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-3.9.1/IMPLEMENTATION_PLAN.md),
  [phase-3.9.1/TOFIX.md](../../../phases/v1/phase-3.9.1/TOFIX.md),
  [phase-3.9.1/BRIEF.md](../../../phases/v1/phase-3.9.1/BRIEF.md)
- Published A2 evidence with shared seam overlap:
  [A2_FIRST_LAUNCH_AND_SETTINGS.md](./A2_FIRST_LAUNCH_AND_SETTINGS.md)
  (`A1-FL-01` bottom-panel toggle, `A1-FL-05` terminal font settings);
  [A2_BUILD_RUN_AND_TEST.md](./A2_BUILD_RUN_AND_TEST.md)
  (`A1-BR-04` Output vs PTY separation, `BottomPanelMode` routing);
  [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./A2_SEARCH_AND_COMMAND_DISCOVERY.md)
  (`view.toggleBottomPanel` registry entry)

### 2.2 Production source (minimum required + supporting)

**Shell reachability and bottom-panel composition**

- [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs)
  (`TerminalTabHost` from `BottomPanelHost`, `SetHost(ViewModel.TerminalHost)`,
  `LastTabCloseRequested` → `HideBottomPanelCommand`)
- [MainLayoutBuilder.cs](../../../../src/App/Shell/MainLayoutBuilder.cs)
  (constructs `BottomPanelHost`)
- [BottomPanelHost.cs](../../../../src/App/Shell/BottomPanelHost.cs)
  (mode strip, `TerminalTabHost` visibility, `FocusAndStartActiveTerminalSession`)
- [MainWindowViewModel.cs](../../../../src/App/Shell/MainWindowViewModel.cs)
  (`ITerminalHost TerminalHost`, default `BottomPanelMode.Terminal`,
  `SwitchToTerminalBottomCommand`, `ToggleBottomPanelCommand`)
- [ShellPanelNavigation.cs](../../../../src/App/Shell/ShellPanelNavigation.cs)
  (toggle vs mode-switch commands)
- [MainWindowActivationHost.cs](../../../../src/App/Shell/MainWindowActivationHost.cs)
  (active-session `StartupError` → status bar)

**DI and PTY backend**

- [TerminalServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TerminalServiceCollectionExtensions.cs)
- [LinuxTerminalServiceFactory.cs](../../../../src/Features/Terminal/Infrastructure/LinuxTerminalServiceFactory.cs)
- [LinuxTerminalService.cs](../../../../src/Features/Terminal/Infrastructure/LinuxTerminalService.cs)
- [LinuxPtyInterop.cs](../../../../src/Features/Terminal/Infrastructure/LinuxPtyInterop.cs)
- [ITerminalService.cs](../../../../src/Features/Terminal/Contracts/ITerminalService.cs)
- [ITerminalServiceFactory.cs](../../../../src/Features/Terminal/Contracts/ITerminalServiceFactory.cs)

**Session / tab host (3.9.1)**

- [TerminalHost.cs](../../../../src/Features/Terminal/Presentation/TerminalHost.cs)
- [ITerminalHost.cs](../../../../src/Features/Terminal/Presentation/ITerminalHost.cs)
- [TerminalTabViewModel.cs](../../../../src/Features/Terminal/Presentation/TerminalTabViewModel.cs)
- [TerminalTabHost.cs](../../../../src/Features/Terminal/Presentation/TerminalTabHost.cs)
- [TerminalTabStrip.cs](../../../../src/Features/Terminal/Presentation/TerminalTabStrip.cs)
- [TerminalTabCloseBehavior.cs](../../../../src/Features/Terminal/Presentation/TerminalTabCloseBehavior.cs)

**Parser, screen, render, search (3.6–3.9)**

- [TerminalViewModel.cs](../../../../src/Features/Terminal/Presentation/TerminalViewModel.cs)
- [AnsiParser.cs](../../../../src/Features/Terminal/Presentation/AnsiParser.cs)
- [TerminalScreen.cs](../../../../src/Features/Terminal/Presentation/TerminalScreen.cs)
- [TerminalSnapshot.cs](../../../../src/Features/Terminal/Presentation/TerminalSnapshot.cs)
- [TerminalRenderControl.cs](../../../../src/Features/Terminal/Presentation/TerminalRenderControl.cs)
- [TerminalPanel.cs](../../../../src/Features/Terminal/Presentation/TerminalPanel.cs)
- [TerminalKeyMapper.cs](../../../../src/Features/Terminal/Presentation/TerminalKeyMapper.cs)
- [TerminalSnapshotSearch.cs](../../../../src/Features/Terminal/Presentation/TerminalSnapshotSearch.cs)
- [TerminalGeometry.cs](../../../../src/Features/Terminal/Presentation/TerminalGeometry.cs)
- [TerminalState.cs](../../../../src/Features/Terminal/Presentation/TerminalState.cs)

**Settings injection (terminal font — corroborates FL-05 only)**

- [SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs)
  (`TerminalFontFamily`, `TerminalFontSize`)

---

## 3. Two-row verdict table

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-TR-01` | **Wired-with-gap** | The embedded terminal is production-composed in the bottom panel via `BottomPanelHost` → `TerminalTabHost` → per-tab `TerminalPanel` → `TerminalRenderControl`, with default `BottomPanelMode.Terminal` and a user-reachable mode-strip **Terminal** button (`SwitchToTerminalBottomCommand`). `view.toggleBottomPanel` (`Ctrl+Oem3` / `` Ctrl+` ``, `Ctrl+J`) toggles bottom-panel **visibility only** and does not force `BottomPanelMode.Terminal`, so a user on Output/Debug/Test Results can show the panel without seeing the PTY surface. `LinuxTerminalService` provides a Linux PTY backend (`posix_openpt`, `posix_spawn`, `TIOCSWINSZ`, reader thread, restart-safe fd reaping). `TerminalViewModel` owns UTF-8 decode → `AnsiParser` → `TerminalScreen` → `TerminalSnapshot` projection, alternate-screen dispatch (DEC 1047/1049), scrollback retention, restart/clear lifecycle, and bracketed paste. `TerminalRenderControl` implements cell-grid rendering, pointer selection (single/double/triple-click), manual scrollback (PageUp/PageDown/Home/End when not on alternate screen), and copy/paste affordances. `TerminalPanel` owns in-panel Find (substring search over snapshot + scrollback via `TerminalSnapshotSearch`, suppressed during alternate screen). Startup failures surface through `TerminalHost.StartupError` → status bar. Gaps: Linux-only factory (no Windows/macOS backend in production tree); `Ctrl+\`` does not guarantee terminal mode; Phase 3.9 TOFIX still lists unchecked manual Linux smoke and a missing visible scroll affordance; live TUI/selection/search/restart behavior remains **A3-unproven**. |
| `A1-TR-02` | **Wired-with-gap** | Phase 3.9.1 multi-tab wiring is present: `ITerminalServiceFactory` / `LinuxTerminalServiceFactory` creates one `ITerminalService` per tab; `TerminalHost` (singleton `ITerminalHost`) owns `Tabs`, `ActiveTab`, `NewTabCommand`, `CloseTabCommand`, `ActivateTabCommand`, and disposes `TerminalViewModel` (which disposes its service) on tab close; each new/activated tab calls `EnsureActiveSessionStartedAsync`. `TerminalTabHost` retains one `TerminalPanel` per `TerminalTabViewModel` in a view-layer `_panels` cache so search/viewport/selection/log-view state stay session-local; `TerminalTabStrip` renders titles, active highlight, **+** (new tab), and **×** (close). Closing the sole remaining tab invokes `LastTabCloseRequested` → `HideBottomPanelCommand` instead of destroying the session (`TerminalTabCloseBehavior`). Toggling bottom-panel visibility does not destroy tab sessions (`BottomPanelHost` only changes row height / `IsVisible`). Gaps: every tab title is the static string `"Terminal"` (no `Terminal 1` / `Terminal 2` disambiguation); sessions are explicitly not persisted across app restart (documented 3.9.1 limitation, not a success-condition violation); per-tab PTY isolation and focus routing remain **A3-unproven**. |

Verdict definitions: [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

---

## 4. End-to-end production wiring trace

Legend per seam: **T** = type/method exists · **R** = registered in
production DI · **C** = called by production path · **U** = reachable
from user-visible entry point · **P** = result projected back to UI · **A3** =
clean-profile smoke evidence.

### 4.1 Bottom-panel reachability and mode routing (`A1-TR-01`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `BottomPanelHost` terminal surface | ✓ | — | ✓ | ✓ | ✓ | [BottomPanelHost.cs](../../../../src/App/Shell/BottomPanelHost.cs) hosts `TerminalTabHost` in bottom content grid |
| Default mode `Terminal` | ✓ | — | ✓ | ✓ | ✓ | [MainWindowViewModel.cs](../../../../src/App/Shell/MainWindowViewModel.cs) `_bottomPanelMode = BottomPanelMode.Terminal` |
| Mode strip **Terminal** button | ✓ | — | ✓ | ✓ | ✓ | `CreateModeButton("Terminal", … SwitchToTerminalBottomCommand)` |
| `SwitchToTerminalBottomCommand` | ✓ | — | ✓ | ✓ | ✓ | [ShellPanelNavigation.cs](../../../../src/App/Shell/ShellPanelNavigation.cs) sets `BottomPanelMode.Terminal` + visible |
| `ToggleBottomPanelCommand` | ✓ | ✓ | ✓ | ✓ | ✓ | Registered as `view.toggleBottomPanel` with `Ctrl+Oem3`, `Ctrl+J`; toggles `IsBottomPanelVisible` only |
| `ApplyBottomPanelMode` | ✓ | — | ✓ | ✓ | ✓ | `TerminalTabHost.IsVisible = mode == BottomPanelMode.Terminal`; mutual exclusion with Problems/Output/TestResults/Debug |
| Show + focus + start on terminal reveal | ✓ | — | ✓ | ✓ | ✓ | `ApplyBottomPanelVisibility` / `ApplyBottomPanelMode` → `FocusAndStartActiveTerminalSession` → `TerminalTabHost.FocusActiveSession` + `TerminalHost.EnsureActiveSessionStartedAsync` |
| `MainWindow` host binding | ✓ | — | ✓ | — | ✓ | `_terminalTabHost.SetHost(ViewModel!.TerminalHost)` in `WhenActivated` |

**Source-proven gap:** `ToggleBottomPanelCommand` does not call
`SwitchToTerminalBottomCommand`. If the user last viewed Output or Debug,
`` Ctrl+` `` shows that mode again, not the PTY terminal. Terminal mode
requires the mode-strip **Terminal** button (or starting from default
`BottomPanelMode.Terminal` on cold launch).

**Non-re-verdict overlap:** [A2_BUILD_RUN_AND_TEST.md §4.8](./A2_BUILD_RUN_AND_TEST.md)
already verdicted `A1-BR-04` **Wired** for Output/Test Results vs PTY
separation via `BottomPanelMode`.

### 4.2 PTY process execution (`A1-TR-01`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `ITerminalService` contract | ✓ | — | ✓ | — | — | `StartAsync`, `WriteAsync`, `StopAsync`, `Resize`, events |
| `LinuxTerminalService` PTY alloc/spawn | ✓ | ✓ (via factory) | ✓ | ✓ (on panel reveal) | ✓ | `posix_openpt` → `grantpt`/`unlockpt` → `SpawnShell` via `posix_spawn` + `POSIX_SPAWN_SETSID`; default shell `/bin/bash` |
| Output reader thread | ✓ | — | ✓ | — | ✓ | `ReadLoop` raises `OutputReceived`; `SignalExit` reaps child once |
| Resize ioctl | ✓ | — | ✓ | ✓ (on panel resize) | — | `LinuxPtyInterop.TIOCSWINSZ`; `TerminalViewModel.Resize` + post-start `ApplyPendingResize` |
| Input forwarding | ✓ | — | ✓ | ✓ | — | `TerminalPanel` `KeyDown`/`TextInput` → `TerminalKeyMapper` / UTF-8 → `SendInputAsync` → `WriteAsync` |
| Factory per session | ✓ | ✓ | ✓ | ✓ (new tab) | — | [LinuxTerminalServiceFactory.cs](../../../../src/Features/Terminal/Infrastructure/LinuxTerminalServiceFactory.cs) `Create()` → new instance |
| Linux-only backend | ✓ | ✓ | ✓ | — | — | No non-Linux `ITerminalServiceFactory` implementation in production tree |

**A3-unproven:** real shell prompt, interactive readline, SIGINT delivery,
and multi-chunk UTF-8 behavior under live PTY I/O.

### 4.3 ANSI/CSI parsing and screen buffer (`A1-TR-01`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `AnsiParser` state machine | ✓ | — | ✓ | — | ✓ | Parses print, C0 execute, CSI (`A/B/C/D/H/J/K/m`), DECSET/DECRST (`?2004`, `?1047`, `?1048`, `?1049`), cursor save/restore |
| `TerminalScreen` dual buffer | ✓ | — | ✓ | — | ✓ | Main + alternate buffers; scrollback on main only; `EnterAlternateScreen` / `ExitAlternateScreen` |
| `TerminalViewModel.Append` dispatch | ✓ | — | ✓ | — | ✓ | Applies parser actions; coalesces DEC 1049 cursor save/restore with alt-screen switch |
| Snapshot projection | ✓ | — | ✓ | — | ✓ | `UpdateSnapshot` builds `TerminalSnapshot` with visible rows + scrollback rows/cells |
| `TerminalRenderControl` draw | ✓ | — | ✓ | ✓ | ✓ | Cell grid via `DrawingContext`; SGR colors; cursor block |

**A3-unproven:** full `vim` / `htop` / `less` compatibility under real TUI
traffic (Phase 3.8 scope claims common full-screen apps; runtime not
executed here).

### 4.4 Alternate-screen handling (`A1-TR-01`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| Parser emits `AlternateScreenAction` | ✓ | — | ✓ | — | — | [AnsiParser.cs](../../../../src/Features/Terminal/Presentation/AnsiParser.cs) modes 1047/1049 |
| Screen switch + isolation | ✓ | — | ✓ | — | ✓ | `TerminalScreen.EnterAlternateScreen` clears alt buffer; main scrollback preserved |
| View-model exposure | ✓ | — | ✓ | — | ✓ | `TerminalViewModel.IsAlternateScreenActive` |
| Render control gate | ✓ | — | ✓ | ✓ | ✓ | `IsAlternateScreenActiveProperty`; suppresses main-buffer selection/scrollback/search |
| Panel search suppression | ✓ | — | ✓ | ✓ | ✓ | `EffectiveSearchResult()` returns null during alt screen |
| Exit on process death | ✓ | — | ✓ | — | ✓ | `OnProcessExited` calls `_screen.ExitAlternateScreen()` before exit message |
| Restart reset | ✓ | — | ✓ | ✓ (Restart toolbar) | ✓ | `PrepareForRestart` → `_screen.ResetForRestart()` |

### 4.5 Selection, scrollback, search (`A1-TR-01`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| Pointer selection | ✓ | — | ✓ | ✓ | ✓ | [TerminalRenderControl.cs](../../../../src/Features/Terminal/Presentation/TerminalRenderControl.cs) drag + double-click word + triple-click line |
| Selection copy | ✓ | — | ✓ | ✓ | ✓ | `Ctrl+C` / `Ctrl+Shift+C` when selection non-empty; context menu Copy |
| Copy Visible | ✓ | — | ✓ | ✓ | ✓ | Separate explicit action (no selection fallback on Copy) |
| Bracketed paste | ✓ | — | ✓ | ✓ | — | DEC mode 2004 in `HandleDecSetReset`; `PasteAsync` wraps `\x1B[200~…\x1B[201~` |
| Manual scrollback | ✓ | — | ✓ | ✓ | ✓ | `ScrollPageUp`/`ScrollPageDown`/`ScrollToTop`/`ScrollToBottom`; wheel handler; disabled on alternate screen |
| Keyboard scroll (main buffer) | ✓ | — | ✓ | ✓ | ✓ | [TerminalPanel.cs](../../../../src/Features/Terminal/Presentation/TerminalPanel.cs) PageUp/PageDown/Home/End consumed when not alt-screen |
| Find toolbar | ✓ | — | ✓ | ✓ | ✓ | `_searchToggleButton`, query box, Prev/Next, match count |
| `TerminalSnapshotSearch` | ✓ | — | ✓ | ✓ | ✓ | Case-insensitive substring over scrollback + visible rows |
| Jump to match | ✓ | — | ✓ | ✓ | ✓ | `BringSearchMatchIntoView` on active match |
| Visible scroll affordance | ✗ | — | — | — | — | Phase 3.9 TOFIX: scrollbar/thumb not implemented; keyboard scroll only |

### 4.6 Terminal restart safety (`A1-TR-01`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `RestartCommand` | ✓ | — | ✓ | ✓ | ✓ | Disabled while running (`ReactiveCommand` from `RestartAsync`) |
| Running restart path | ✓ | — | ✓ | ✓ | ✓ | `StopAsync` → `OnProcessExited` → `PrepareForRestart` → `EnsureStartedAsync` |
| Service restart fd hygiene | ✓ | — | ✓ | — | — | [LinuxTerminalService.cs](../../../../src/Features/Terminal/Infrastructure/LinuxTerminalService.cs) closes stale master fd, joins reader, resets `_exitSignaled` before respawn |
| ViewModel start gate | ✓ | — | ✓ | — | ✓ | `_startRequested` cleared in `PrepareForRestart`; failed start resets gate for retry |
| Clear when exited | ✓ | — | ✓ | ✓ | ✓ | `ClearAsync` erases local screen + log entries when not running |
| Clear when running | ✓ | — | ✓ | ✓ | ✓ | Sends `\x0C` to PTY |
| Status projection | ✓ | — | ✓ | ✓ | ✓ | `StatusLabel`, toolbar `_statusText`, `StartupError` → status bar |

**A3-unproven:** restart after `vim`/`less` crash, rapid restart under live
load, and zombie-free reaping under repeated cycles (unit tests corroborate
contracts only).

### 4.7 Per-tab session ownership and tab lifecycle (`A1-TR-02`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `ITerminalHost` singleton | ✓ | ✓ | ✓ | — | ✓ | [TerminalServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TerminalServiceCollectionExtensions.cs) |
| Initial tab at host construction | ✓ | — | ✓ | ✓ | ✓ | `TerminalHost` ctor creates first `TerminalViewModel` + `TerminalTabViewModel` |
| `NewTabCommand` | ✓ | — | ✓ | ✓ (+ button) | ✓ | New `TerminalViewModel(_serviceFactory.Create())`; activates; auto-starts |
| `ActivateTabCommand` | ✓ | — | ✓ | ✓ (tab click) | ✓ | Switches `ActiveTab`; auto-starts if needed |
| `CloseTabCommand` | ✓ | — | ✓ | ✓ (× button) | ✓ | Removes tab; `tab.Session.Dispose()` |
| Active-tab fallback on close | ✓ | — | ✓ | ✓ | ✓ | Selects neighbor at same index or last tab |
| Last-tab close → hide panel | ✓ | — | ✓ | ✓ | ✓ | `TerminalTabCloseBehavior` + `LastTabCloseRequested` → `HideBottomPanelCommand` |
| Independent parser/screen state | ✓ | — | ✓ | — | ✓ | Each `TerminalViewModel` owns its own `_parser`, `_screen`, `_decoder` |
| Active error projection | ✓ | — | ✓ | ✓ | ✓ | `TerminalHost.StartupError` switches on `ActiveTab.Session.StartupError` |
| No cross-tab persistence | ✓ | — | ✓ | — | — | Documented 3.9.1 limitation; no snapshot/store on disk |

### 4.8 `TerminalTabHost` / `TerminalPanel` caching and tab strip UI (`A1-TR-02`)

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| View-layer panel cache | ✓ | — | ✓ | — | ✓ | [TerminalTabHost.cs](../../../../src/Features/Terminal/Presentation/TerminalTabHost.cs) `_panels` dictionary |
| One panel per tab | ✓ | — | ✓ | — | ✓ | `EnsurePanel` creates via factory; `panel.ViewModel = tab.Session` |
| Panel removal on tab close | ✓ | — | ✓ | — | ✓ | `RemovePanel` clears binding and dictionary entry |
| Active panel swap | ✓ | — | ✓ | ✓ | ✓ | `ShowActivePanel` sets `_content.Content` |
| Focus seam | ✓ | — | ✓ | ✓ | ✓ | `FocusActiveSession` → `ActivePanel.FocusTerminal()` (view only; `ITerminalHost.FocusActiveSession` is intentionally empty) |
| Tab strip UI | ✓ | — | ✓ | ✓ | ✓ | [TerminalTabStrip.cs](../../../../src/Features/Terminal/Presentation/TerminalTabStrip.cs) titles, highlight, +/× |
| Tab title policy | ✓ | — | ✓ | ✓ | **gap** | [TerminalTabViewModel.cs](../../../../src/Features/Terminal/Presentation/TerminalTabViewModel.cs) sets `Title = "Terminal"` for every tab |
| Bottom-panel toggle preserves sessions | ✓ | — | ✓ | ✓ | ✓ | Visibility toggle does not call `CloseTab`; host/tabs remain |

**A3-unproven:** switching tabs while two shells run different commands;
confirming search/viewport/selection do not leak between cached panels under
live input.

---

## 5. Row notes — source-proven vs A3-unproven

### 5.1 `A1-TR-01`

**Source-proven wiring**

- Embedded terminal lives in the bottom panel composition tree, not a
  separate window or editor tab.
- PTY-backed execution is implemented for Linux via native interop; the
  service boundary keeps platform code out of views/view models.
- The full renderer pipeline documented in Phase 3.6–3.9 is present:
  parser → screen → snapshot → custom render control.
- Alternate-screen, selection, scrollback navigation, and in-panel search
  are implemented with explicit gates so main-buffer state does not leak
  during full-screen TUIs.
- Restart clears service/view-model state deliberately (`PrepareForRestart`,
  fd reaping, alt-screen reset).

**Documented / structural gaps (still Wired-with-gap)**

- **Toggle vs mode:** `` Ctrl+` `` / `Ctrl+J` flip visibility only
  ([ShellPanelNavigation.cs](../../../../src/App/Shell/ShellPanelNavigation.cs)).
  Terminal surface reachability when another bottom mode is selected requires
  the **Terminal** mode-strip button.
- **Linux-only backend:** production registers only
  `LinuxTerminalServiceFactory`. V1 Phase 3 scoped Linux MVP; non-Linux hosts
  have no alternate factory in-tree.
- **Scroll affordance:** Phase 3.9 TOFIX records keyboard/viewport scroll
  without a visible scrollbar/thumb.
- **Manual smoke debt:** [phase-3.9/TOFIX.md](../../../phases/v1/phase-3.9/TOFIX.md)
  still lists unchecked interactive Linux smoke items even though
  [PHASES.md](../../../roadmap/PHASES.md) marks 3.9 complete with smoke —
  runtime verification remains open for A3.

**A3-unproven (not counted as Missing)**

- TUI programs (`htop`, `less`, `vim`) render and accept input correctly.
- Selection/copy/search behave as documented under real pointer/keyboard use.
- Restart safety under repeated user-driven restart after shell exit.
- Startup failure messages for missing `/bin/bash` or PTY exhaustion.

### 5.2 `A1-TR-02`

**Source-proven wiring**

- Factory/host/tab-host architecture from Phase 3.9.1 is implemented:
  one service + one view model per tab, singleton host coordinator, view-layer
  panel cache, tab strip commands.
- Tab creation, activation, close/dispose, and active-tab fallback are wired
  in `TerminalHost` with production UI bindings in `TerminalTabStrip` /
  `TerminalTabHost`.
- Closing the last tab hides the bottom panel without disposing the sole
  session (by design).

**Documented / structural gaps (still Wired-with-gap)**

- **Tab titles:** all tabs display `"Terminal"`; Phase 3.9.1 allowed generic
  titles but suggested numbered labels for disambiguation — not implemented.
- **No session persistence:** explicit 3.9.1 limitation; not required by
  `A1-TR-02` success condition.

**A3-unproven**

- Two tabs running different commands stay isolated under live PTY use.
- Focus and input follow the active tab only when switching during concurrent
  sessions.

---

## 6. Planned A3 scenarios (not executed)

From [GOAL_MATRIX.md §7](../GOAL_MATRIX.md#7-terminal):

| id | Scenario (disposable profile) | A2 status |
|----|------------------------------|-----------|
| `A1-TR-01` | Open terminal, run `htop` (alt-screen), exercise search | **Not executed** — blocked on A3 phase; preconditions lighter than Debug (no NetCoreDbg / C# project required) |
| `A1-TR-02` | Add two terminal tabs, run different commands, switch tabs | **Not executed** |

Suggested A3 checks derived from gaps above:

1. Cold launch → `` Ctrl+` `` → confirm terminal visible when
   `BottomPanelMode` is Terminal; switch to Output → `` Ctrl+` `` hide/show
   → confirm terminal is **not** shown until **Terminal** mode strip clicked.
2. Run `htop` or `less -R` in one tab; confirm alternate screen, then exit;
   confirm main-buffer scrollback/search restore.
3. Open two tabs, run `sleep 999` in tab A and `echo TAB_B` in tab B;
   switch tabs and confirm output/input isolation.
4. Close one tab while the other runs; close last tab → panel hides; reopen
   → prior session still alive.
5. Exit shell → **Restart** → new prompt without duplicated event handlers.

A3 is **not begun** in this session.

---

## 7. Corroborating tests (non-proof)

Tests below corroborate contracts; A2 does not promote them to production
reachability or live PTY proof.

| Area | Representative tests | Prove | Do **not** prove |
|------|----------------------|-------|-------------------|
| PTY service | `LinuxTerminalServiceTests`, `LinuxTerminalServiceFactoryTests` | Start/write/resize/exit/restart fd hygiene | Live Avalonia focus; real bash readline |
| Parser / screen | `AnsiParserTests`, `TerminalScreenTests`, `TerminalSnapshotTests` | CSI/SGR/alt-screen/scrollback semantics | Real `vim` byte streams |
| View model | `TerminalViewModelTests` | Lifecycle, restart, snapshot projection, alt-screen log suppression | UI rendering |
| Render / search | `TerminalRenderControlTests`, `TerminalSnapshotSearchTests`, `TerminalGeometryTests` | Selection math, search coordinates, viewport scroll | Pointer delivery in live window |
| Key map | `TerminalKeyMapperTests` | Byte sequences for common keys | PageUp scroll (handled in panel, not mapper) |
| Tab host | `TerminalHostTests` (in `TerminalHostTests.cs` / host lifecycle tests) | Independent sessions, close disposal, active-tab fallback | Multi-tab PTY isolation smoke |
| Tab host view | `TerminalPanelSubscriptionsTests`, host-level tests referenced in 3.9.1 plan | Panel cache ownership, restart subscription cleanup | Visual tab strip |
| DI | `TerminalRegistrationModuleTests` | Factory + host singleton registration | End-to-end shell wiring |
| Isolation collection | `LinuxTerminalProcessIsolationCollection` | Test harness grouping for PTY tests | Product isolation |

**Distinction from production composition:** tests construct
`TerminalViewModel` with test doubles and synchronous `_uiPost`; production
uses `Dispatcher.UIThread.Post`. Reachability is established by §4 traces.

---

## 8. Issue / deferred-finding relationships (read-only)

No issue or deferred-finding files were edited.

| Artifact | Relationship to this slice |
|----------|----------------------------|
| [A2_FIRST_LAUNCH_AND_SETTINGS.md](./A2_FIRST_LAUNCH_AND_SETTINGS.md) | `A1-FL-01` already verdicted bottom-panel toggle **Wired-with-gap**; `A1-FL-05` **Wired** for terminal font settings applied via `TerminalPanel` settings binding — not re-verdicted here |
| [A2_BUILD_RUN_AND_TEST.md](./A2_BUILD_RUN_AND_TEST.md) | `A1-BR-04` **Wired** for Output vs PTY separation; `BottomPanelMode` exclusivity shared — not re-verdicted |
| [A2_SEARCH_AND_COMMAND_DISCOVERY.md](./A2_SEARCH_AND_COMMAND_DISCOVERY.md) | `view.toggleBottomPanel` registry entry; not re-verdicted |
| [phase-3.9/TOFIX.md](../../../phases/v1/phase-3.9/TOFIX.md) | Manual Linux smoke pending; scroll affordance deferred |
| [phase-3.9.1/IMPLEMENTATION_PLAN.md §Limitations](../../../phases/v1/phase-3.9.1/IMPLEMENTATION_PLAN.md) | No persisted sessions; no splits/detached windows |

---

## 9. Exact next recommended A2 slice (explicitly not started)

**Next recommended A2 slice (not begun in this session):**
`A2_GIT`.

**Suggested goal rows:**

- `A1-GT-01` — repo-backed Source Control panel and status bar git state
- `A1-GT-02` — unified diff view for modified files
- `A1-GT-03` — stage/unstage and local commit with validation
- `A1-GT-04` — truthful branch display in status bar

**Why this next:** Terminal was the last IDE shell journey without A2
evidence. Git workflow (`A1-GT-01`–`A1-GT-04`) is the remaining
user-journey block in the goal matrix with no published wiring slice.
Agent, audit, trace, and townhall journeys already have A2 evidence.

**Explicitly not started here:** `A2_GIT`, A3, A4, stabilization, V4,
corrective implementation, or any other A2 evidence file.

---

## 10. Verification and working-tree closeout

### Pre-closeout checks (to be re-run after writing)

| Check | Expected |
|-------|----------|
| Exactly one new untracked file | `docs/audits/v1-v3-product-reality/evidence/A2_TERMINAL.md` |
| No tracked files modified | Clean aside from that untracked evidence file |
| Whitespace | `git diff --no-index --check /dev/null <evidence-file>` (exit 1 from diff vs `/dev/null` is expected; no whitespace diagnostics) |
| Relative Markdown links | Repository-relative paths under `docs/` and `src/` resolve |
| Fragment links | Headings in this file and cited docs resolve |
| Primary verdict table | Exactly one verdict row per `A1-TR-01` and `A1-TR-02` |
| Next slice | `A2_GIT` named and **not** begun |
| Commit / push | Not performed |

### Closeout verdicts (repeat)

| id | verdict |
|----|---------|
| `A1-TR-01` | **Wired-with-gap** |
| `A1-TR-02` | **Wired-with-gap** |

**Stop for re-audit.** No next slice started. No commit or push.

---

*A2_TERMINAL complete. Read-only audit; no fixes, A3 work, commits, or pushes.*
