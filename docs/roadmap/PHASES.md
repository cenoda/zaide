# Zaide — Development Roadmap

Build the IDE layer first (usable standalone), then add the agent-to-agent layer.
Each phase must be complete before moving to the next.

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

### Phase 3.5: Terminal UI Normalization
- [x] Wire terminal resize to PTY rows/columns
- [x] Expand common shell key forwarding
- [x] Add visible terminal controls/state
- [x] Make terminal restart safe across service and ViewModel layers
- [x] Improve raw output experience within MVP bounds

## Phase 4: Townhall (Agent Transparency)
- [ ] Townhall view in center area (tab alongside editor)
- [ ] Auto-log: timestamped entries for agent actions
- [ ] Scrollable, filterable log
- [ ] Clear distinction between agent actions and user actions

## Phase 5: Agent Panels
- [ ] Right panel split into 2 agent slots (top/bottom)
- [ ] Each agent panel: name, status indicator, output area
- [ ] User input field per agent panel (talk to agent directly)
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
| 4 | Phase 3.5 terminal UI normalization complete |
| 5 | Phase 4 townhall displays entries |
| 6 | Phase 5 agent panels render and accept input |
| 7 | Phase 6 agent routing works |

---

*Last updated: 2026-06-29*
