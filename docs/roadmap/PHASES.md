# Zaide — Development Roadmap

Build the IDE layer first (usable standalone), then add the agent-to-agent layer.
Each phase must be complete before moving to the next.

---

## Technical Decisions (Resolved)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Backend** | SQLite + JSON (settings) | Time-series data needs queries. One-file management. |
| **Image Storage** | Hybrid: Embedded (UI) + File Ref (project) | Icons build in, agent avatars swap at runtime. |
| **Plugin** | Interface + DI manual registration | Core interfaces now, .NET 10 Keyed Services later. |

See `docs/architecture/OVERVIEW.md` for full details.

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
- [ ] File tree view in left sidebar
- [ ] Open folder dialog
- [ ] Click file → placeholder "opened" state
- [ ] Ignore list (node_modules, bin, obj, .git)
- [ ] File system watcher for live updates

## Phase 2: Editor
- [ ] Text editor widget (AvaloniaEdit or alternative)
- [ ] Tabbed editor — open/close/switch tabs
- [ ] Open file from tree → editor tab
- [ ] File save (Ctrl+S)
- [ ] Dirty flag indicator on tabs
- [ ] Syntax highlighting (TextMate grammars)

## Phase 3: Terminal
- [ ] Embedded terminal in bottom panel
- [ ] Ctrl+` toggle visibility
- [ ] Process execution (run commands)
- [ ] VT100 escape code rendering

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
| 4 | Phase 3 terminal works |
| 5 | Phase 4 townhall displays entries |
| 6 | Phase 5 agent panels render and accept input |
| 7 | Phase 6 agent routing works |

---

*Last updated: 2025-06-25*
