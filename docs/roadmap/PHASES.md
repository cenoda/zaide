# Zaide — Development Roadmap

Build a strong IDE foundation first, then pivot the product toward an
agent-first workspace. Each phase must be complete before moving to the next.

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
- [ ] **3.8:** TUI compatibility (alternate screen, richer screen-control behavior)
- [ ] **3.9:** Terminal UX polish (selection, scrollback, search, terminal tabs)

## Phase 4: Agent-First Layout Transition ✅ Complete
- [x] Move Townhall into the visual center of the app
- [x] Reposition the editor as a focused implementation surface, still always visible
- [x] Preserve fast file-tree, editor, and terminal workflows during the layout pivot
- [x] Establish Townhall as the default attention surface for agent activity and user intervention

### Phase 4.1: Townhall Foundations ✅ Complete
- [x] Townhall thread renders in the center workspace
- [x] Channel switching and message sending
- [x] People panel with agent status indicators
- [ ] Auto-log: timestamped entries for agent actions (not yet implemented)
- [ ] Scrollable, filterable log (not yet implemented)
- [ ] Clear distinction between agent actions and user actions

## Phase 5: Agent Panels
- [ ] Add dedicated agent surfaces without demoting Townhall from the primary workspace
- [ ] Each agent panel: name, status indicator, output area
- [ ] User input field per agent panel (talk to agent directly)

---

## Completed Refactors

| Refactor | Description | Status |
|---------|-------------|--------|
| refactor-1 | Document/Workspace extraction | ✅ Complete |
| refactor-2 | Layer boundary cleanup | ✅ Complete |
| refactor-3 | Agent-first layout transition | ✅ Complete |

### refactor-2: Layer Boundary Cleanup

Cleaned up layer boundaries within the single-project structure (preparation for future project split):

- **M1:** Removed `SaveAsync` from `Document` model, replaced `ReactiveObject` with `INotifyPropertyChanged` in `FileTreeNode`
- **M3:** Extracted `IFileTreeService` interface, `StartWatching()` returns `IObservable<FileChangeEvent>`
- **M5:** Injected `IScheduler` into `FileTreeViewModel` (removed `AvaloniaScheduler.Instance` direct usage)
- **M6:** Created `SupportedFileTypes` static class in `Services/` (editor policy)
- **M7:** Stabilization — 340 tests pass

**Deferred:** Terminal pure logic namespace change (M2), FileTreeNode domain/UI split (M4), UI-post abstraction (M8)

### refactor-3: Agent-First Layout Transition

Transformed Zaide from an editor-first IDE layout to an agent-first workspace layout:

- **M0:** Baseline verification — confirmed build/tests green and current layout
- **M0.5:** Palette update to match concept colors (Ayaka Violet → concept.png palette)
- **M1:** Main window layout transition with nav bar and left-panel mode switching
- **M2:** Townhall domain/view-model foundation with channels, messages, and agents
- **M3:** Townhall view integration with people/channels sidebar and chat area
- **M4:** Editor adaptation with townhall link and focused file info bar
- **M5:** Terminal/logs categorization with [BUILD]/[AGENT]/[LOG] tags
- **M6:** Source Control panel and status bar implementation
- **M7:** Regression sweep and documentation sync

**Result:** Townhall is now the visual center, editor remains always visible as focused implementation surface, all existing workflows preserved and stable.
- [ ] Agent panel ↔ Townhall: actions appear in both

## Phase 6: Agent-to-Agent Router
- [ ] @mention syntax parsing
- [ ] Route messages between agent panels
- [ ] Agent A can request review from Agent B
- [ ] Debate model: disagreements surfaced in Townhall

## Phase 7: Git Integration
- [ ] Git status in left sidebar
- [ ] Basic diff view
- [ ] Commit from IDE
- [ ] Branch display

---

## Entry Conditions

| Phase | Entry Condition |
|-------|-----------------|
| 0 | — |
| 1 | Phase 0 layout renders correctly |
| 2 | Phase 1 file tree works, can select files |
| 3 | Phase 2 editor works, can edit and save |
| 4 | Phase 3 terminal UI normalization complete |
| 5 | Phase 4 layout transition and Townhall foundations render correctly |
| 6 | Phase 5 agent panels render and accept input |
| 7 | Phase 6 agent routing works |

---

*Last updated: 2026-07-01*
