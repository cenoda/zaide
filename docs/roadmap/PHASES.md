# Zaide — Development Roadmap

Build a strong IDE foundation first, then turn the UI scaffold into a real
agent workspace. Each phase must be complete before moving to the next.

---

## Technical Decisions (Resolved So Far)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **UI Framework** | Avalonia 12 + Semi.Avalonia (dark) | Cross-platform, MVVM-friendly |
| **Architecture** | MVVM with ReactiveUI | Standard for Avalonia, testable |
| **DI** | Microsoft.Extensions.DependencyInjection | Standard .NET, no magic |
| **Language** | C# (nullable enabled) | Type safety, .NET 10 |
| **Editor** | AvaloniaEdit + TextMate grammars | Syntax highlighting, proven widget |
| **Terminal** | Linux native PTY via P/Invoke | Real TTY for shell interactivity |

### Future Considerations (not yet implemented)

| Decision | Planned Approach | Rationale |
|----------|-----------------|-----------|
| **Persistence** | SQLite (structured data) + JSON (settings) | Time-series data needs queries. One-file management. |
| **Image Storage** | Hybrid: Embedded (UI) + File Ref (project) | Icons build in, agent avatars swap at runtime. |
| **Plugin** | Interface + DI manual registration | Core interfaces later, .NET 10 Keyed Services when needed. |

See `docs/architecture/OVERVIEW.md` for extended discussion.

---

## Phase 0: Foundation & Layout
- [x] Avalonia 12 project scaffold
- [x] Semi.Avalonia theme applied
- [x] .NET 10.0 LTS upgrade
- [x] Central Package Management (CPM)
- [x] Microsoft DI + ReactiveUI bootstrap
- [x] "Ayaka Violet" palette defined
- [x] Directory structure: src/{Models,Services,ViewModels,Views}
- [x] 3-panel grid layout (left sidebar, center, right agent area)
- [x] Bottom panel placeholder (terminal area) with Ctrl+` toggle
- [x] Basic window chrome (title, minimize, maximize, close)

## Phase 1: File Tree Sidebar
- [x] File tree view in left sidebar
- [x] Open folder dialog
- [x] Click file → placeholder "opened" state
- [x] Ignore list (node_modules, bin, obj, .git)
- [x] File system watcher for live updates

### Phase 1.1: File Tree Polish
- [x] Directory rename cascade fix (descendant paths update)
- [x] OpenFolderCommand error handling (4 exception types)
- [x] GridSplitter (sidebar 180–500px)
- [x] Right-click context menu (Open, Expand All, Collapse All)
- [x] IsExpanded binding to TreeViewItem
- [x] Single open pathway (RequestOpenFileCommand)
- [x] Keyboard: Ctrl+O folder picker, Enter + Double-click to open
- [x] Sort-order enforcement (.OrderBy on enumerations)
- [x] Subscription disposal ($11b violations: 0)

### Phase 1.2: File Tree Essentials
- [x] New File / New Folder (context menu + modal prompt)
- [x] Show Hidden Files toggle (Ctrl+Shift+H + checkable menu)
- [x] Copy Path / Copy Relative Path (context menu → clipboard)
- [x] Create watcher now stays alive on failed folder open
- [x] Open file request uses payload, not SelectedFile

## Phase 2: Editor
- [x] Text editor widget (AvaloniaEdit)
- [x] Tabbed editor — open/close/switch tabs
- [x] Open file from tree → editor tab
- [x] File save (Ctrl+S)
- [x] Dirty flag indicator on tabs
- [x] Syntax highlighting (TextMate grammars)

## Phase 3: Terminal
- [x] Embedded terminal in bottom panel
- [x] Ctrl+` toggle visibility
- [x] Process execution (run commands)
- [x] Raw text output (no VT100 parsing — deferred to future polish phase)
- [x] Wire terminal resize to PTY rows/columns
- [x] Expand common shell key forwarding
- [x] Add visible terminal controls (clear/restart/state)
- [x] Make terminal restart safe across service and ViewModel layers
- [x] Improve raw output experience within MVP bounds
- [x] **3.6:** Terminal renderer foundation (ANSI/CSI parser, screen buffer, custom render control) — ✅ Complete
- [x] **3.7:** Interactive shell quality (clear, prompts, resize stability) — ✅ Complete
- [x] **3.8:** TUI compatibility (alternate screen, richer screen-control behavior) — ✅ Complete (2026-07-07)
- [x] **3.9:** Terminal UX polish (selection, scrollback, search) — ✅ Complete (2026-07-07); all exit conditions met including interactive Linux smoke; terminal tabs deferred to 3.9.1
- [x] **3.9.1:** Terminal tabs (multi-session bottom-panel workflow) — ✅ Complete (2026-07-07); lightweight terminal tabs with per-tab sessions, session host/factory seam, view-layer panel caching, and tab strip UI

## Phase 4: Agent Workspace Foundations
- [x] Convert the current Townhall-centered UI scaffold into a real shared workspace
- [x] Auto-log timestamped entries for user and agent actions
- [x] Clear distinction between chat messages and action/log events
- [x] Scrollable, filterable activity history
- [x] Preserve fast file-tree, editor, and terminal workflows while real agent-workspace behavior lands

Completed 2026-07-08 via sub-phases 4.1–4.4 (activity data model, auto-logging
and session-state initialization, activity history UI, docs sync and exit audit).
Townhall now has an 8-kind entry taxonomy, `SourceProvider`/`SourceModel`/`ThreadId`
fields reserved for Phase 5/6, auto-logged entries on send/switch, explicit
in-memory session seed data (channels, agents, empty per-channel message collections
are created by `InitializeSessionState()`), kind-based visual rendering, and a
working filter toggle (All/Chat/Activity).

Phase 5 entry condition reads: "Phase 4 Townhall activity model works as a real
shared workspace" — this is now accurate and requires no adjustment.

## Phase 5: Agent Panels
- [x] Add dedicated agent surfaces without demoting Townhall from the primary workspace
- [x] Establish the panel state/host/composition seam first (`phase-5.1` umbrella, split into `5.1.1`–`5.1.3`)
- [x] Each agent panel: name, status indicator, output area, and direct input surface
- [x] Support one minimal real direct-execution path to one configured OpenAI-compatible endpoint
- [x] Mirror direct-agent interactions into Townhall at the intended Phase 5 level

**Completed via sub-phases 5.1–5.5 (2026-07-08).** All exit conditions verified against live code, build/tests pass, manual smoke confirmed.

---

## Completed Refactors

| Refactor | Description | Status |
|---------|-------------|--------|
| refactor-1 | Document/Workspace extraction | ✅ Complete |
| refactor-2 | Layer boundary cleanup | ✅ Complete |
| refactor-3 | Townhall/editor shell remap and UI scaffold | ✅ Complete |
| refactor-4 | Visual polish pass on the remapped UI surfaces | ✅ Complete |

### refactor-2: Layer Boundary Cleanup

Cleaned up layer boundaries within the single-project structure (preparation for future project split):

- **M1:** Removed `SaveAsync` from `Document` model, replaced `ReactiveObject` with `INotifyPropertyChanged` in `FileTreeNode`
- **M3:** Extracted `IFileTreeService` interface, `StartWatching()` returns `IObservable<FileChangeEvent>`
- **M5:** Injected `IScheduler` into `FileTreeViewModel` (removed `AvaloniaScheduler.Instance` direct usage)
- **M6:** Created `SupportedFileTypes` static class in `Services/` (editor policy)
- **M7:** Stabilization — 340 tests pass

**Deferred:** Terminal pure logic namespace change (M2), FileTreeNode domain/UI split (M4), UI-post abstraction (M8)

### refactor-3: Townhall/Editor Shell Remap

Remapped the shell visually so later agent-workspace phases have a place to land:

- **M0:** Baseline verification — confirmed build/tests green and current layout
- **M0.5:** Palette update to match concept colors (Ayaka Violet → concept.png palette)
- **M1:** Main window layout transition with nav bar and left-panel mode switching
- **M2:** Townhall domain/view-model foundation with channels, messages, and agents
- **M3:** Townhall view integration with people/channels sidebar and chat area
- **M4:** Editor adaptation with townhall link and focused file info bar
- **M5:** Terminal/logs categorization with [BUILD]/[AGENT]/[LOG] tags
- **M6:** Source Control panel and status bar implementation
- **M7:** Regression sweep and documentation sync

**Result:** The UI scaffold now exposes a Townhall-centered shell, but this is
still preparatory layout work rather than completed Phase 4 behavior.

### refactor-4: Visual Polish Pass
M7 regression sweep + doc sync complete:
- All gates passed: `dotnet build` (0 warnings), `dotnet test` (480 pass), luminance ΔL*=10.72 (VC-3), `check-animations.sh` (0), VC-4 limited to documented intentional exceptions, VC-11 limited to token-reference-only grep hits.
- Docs updated: DESIGN.md (typography scale + animation mark), OVERVIEW.md (chronology), PHASES.md.
- No new behavior; polish only.

## Phase 6: Agent-to-Agent Router
- [x] @mention syntax parsing (narrow: zero or one `@AgentName`, case-insensitive exact match; `MentionParser`)
- [x] Route messages between agent panels (mechanical: routed content executes on the resolved target panel via `AgentRouter`)
- [x] Agent A can request review from Agent B (via `@mention` routed send to another visible panel)
- [ ] Debate model: disagreements surfaced in Townhall — **not implemented as a specialized feature**; Townhall mirrors generic chat/error entries only (see plan Known Gaps)

**Completed 2026-07-08.** Smoke test performed: all TOFIX items (agent panel
tab strip scrolling, live Townhall refresh on agent send, input cleared after
send, Enter vs Shift+Enter behavior, channel highlight switching, input width
stability) were fixed, verified by automated tests (724 passed, 0 failed), and
confirmed by manual visual smoke. Two known routing-visibility gaps remained
(unknown-mention visible failure, routed-flow Townhall surfacing) at Phase 6
close — **both closed in Phase 6.1** (see below). Phase 7 entry
condition ("Phase 6 agent routing works — mechanical routing + identity + parser
shipped") is satisfied. See `docs/phases/phase-6/TOFIX.md` for the full smoke
test record.

### Phase 6.1: Routing Visibility Follow-up
- [x] Surface `@mention` routing failures in Townhall (`AgentError` under source panel identity, `"Routing failed: {reason}"`)
- [x] Mirror routed-flow outcomes into Townhall from the resolved target panel (last assistant/error output, target identity)
- [x] Handle vanished target panel gracefully (no crash, no entry)
- [x] Dedicated `AgentRouter` tests (`tests/Zaide.Tests/ViewModels/AgentRouterTests.cs`) — routing resolution + dispatch only, no Townhall dependency

**Completed 2026-07-09.** `MainWindowViewModel.SendAgentMessageAsync` now consumes
`RouteResult` to surface routing failures and routed-flow outcomes in Townhall
(Townhall-only visibility; `AgentRouter` remains Townhall-free). Added 7 router
tests plus 5 view-model mirroring tests. All audit TOFIX items resolved
(`docs/phases/phase-6.1/TOFIX.md`). Build clean, 736 tests pass, no UI changes
beyond Townhall. The Phase 6 routing-visibility gaps are now closed.

## Phase 7: Git Integration
- [x] Git status in left sidebar (7.1+7.2 M1/M2 live)
- [x] Basic diff view (7.3 ✅ done)
- [x] Commit from IDE (7.4 ✅ done)
- [x] Branch display (7.2 M1/M2 live)

Implementation split:

- `docs/phases/phase-7/IMPLEMENTATION_PLAN.md` (umbrella)
- `docs/phases/phase-7.1/IMPLEMENTATION_PLAN.md` — repository discovery + status seam
- `docs/phases/phase-7.2/IMPLEMENTATION_PLAN.md` — live Source Control wiring
- `docs/phases/phase-7.3/IMPLEMENTATION_PLAN.md` — basic diff view
- `docs/phases/phase-7.4/IMPLEMENTATION_PLAN.md` — stage/unstage + local commit (complete)

---

## Entry Conditions

| Phase | Entry Condition |
|-------|-----------------|
| 0 | — |
| 1 | Phase 0 layout renders correctly |
| 2 | Phase 1 file tree works, can select files |
| 3 | Phase 2 editor works, can edit and save |
| 4 | Phase 3 terminal UI normalization complete |
| 5 | Phase 4 Townhall activity model works as a real shared workspace |
| 6 | Phase 5 agent panels render and accept input, support one minimal real direct-execution path, and keep Townhall truthful for direct-agent interactions |
| 7 | Phase 6 agent routing works (mechanical routing + identity + parser shipped; routing-visibility gaps closed in Phase 6.1) |

---

*Last updated: 2026-07-10 (Phase 7.4 closed — stage/unstage/local-commit flow shipped via M1/M2 and verified/stabilized in M3; automated end-to-end mutation-flow coverage added, Source Control panel confirmed truthful across repeated mutation-refresh cycles)*
