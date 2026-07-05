# Zaide

AI-native IDE. Agents talk to each other, not just to you.

**Status:** Phase 3 (Terminal) ✅ Complete — agent-first layout transition planned next.

## Philosophy

```
Cursor:  User → Agent → User → Agent   (A-to-U, single agent)
Zaide:   Agent A ↔ Agent B ↔ Agent C   (A-to-A, multi-agent debate)
         User watches, intervenes when needed
```

AI's biggest weakness is self-confirmation (hallucination).
One agent codes, another reviews. They argue. You get better code.

## Product Direction

Zaide is moving away from the classic "editor in the middle, AI on the side"
shape.

The next UI transition makes the **agent conversation the primary workspace**
and keeps the editor visible as a focused execution surface. We are not trying
to bolt chat onto a conventional IDE. We are trying to build an IDE where the
main narrative is agent collaboration, review, and intervention.

## Current Layout (Post-Refactor-3)

```
┌──────┬──────────┬──────────────────────────────────┬────────────────────┐
│      │          │                                  │                    │
│ Nav  │ Explorer │     Townhall                    │   Editor           │
│ Bar  │  /       │     (people/channels sidebar +   │   (focused code +  │
│      │  SC      │      chat area + input)          │    status info)    │
│      │          │                                  │                    │
├──────┴──────────┴──────────────────────────────────┴────────────────────┤
│ Terminal / Logs (categorized output)                              │
├────────────────────────────────────────────────────────────────────────────┤
│ Status Bar (app info, cursor position, language, project, branch, AI model) │
└────────────────────────────────────────────────────────────────────────────┘
```

- **Far-left:** Nav bar (icon-only vertical strip for switching between Explorer and Source Control modes)
- **Left:** File tree sidebar (Explorer mode) or Source Control panel (SC mode)
- **Center:** Townhall workspace with people/channels sidebar, chat area, and input
- **Right:** Editor (tabbed, syntax highlighting) with townhall link and focused file info
- **Bottom:** Terminal / Logs panel with categorized [BUILD]/[AGENT]/[LOG] output
- **Bottom:** Status bar showing app name, cursor position, language, project, branch, and AI model

## Target Layout Direction

```
┌──────────┬────────────────────────────────────┬──────────────┐
│ Files    │ Townhall                           │ Editor       │
│ (Tree)   │ active thread, agent discussion,   │ focused file │
│          │ user intervention, task state      │ diff/edit    │
├──────────┴────────────────────────────────────┴──────────────┤
│ Terminal / Logs                                                │
└────────────────────────────────────────────────────────────────┘
```

- **Left:** File tree and project navigation
- **Center:** Townhall, the primary workspace for agent activity and user intervention
- **Right:** Editor, always available but no longer the visual center of the app
- **Bottom:** Terminal and runtime/log surface

This shift is intentional. Zaide should read as an agent-native workspace first,
not as a conventional editor with an AI sidebar.

## Status

| Feature | Phase | Status |
|---------|-------|--------|
| 3-panel grid layout | 0 | ✅ Done |
| Semi.Avalonia dark theme | 0 | ✅ Done |
| Bottom panel toggle (Ctrl+`) | 0 | ✅ Done |
| DI container (MS DI + ReactiveUI) | 0 | ✅ Done |
| Ayaka Violet color palette | 0 | ✅ Done |
| File tree sidebar | 1 | ✅ Done |
| File tree: new file/folder, rename, hidden toggle | 1.2 | ✅ Done |
| Editor with tabs, save, syntax highlighting | 2 | ✅ Done |
| Indent guides in editor | 2.1 | ✅ Done |
| Terminal (Linux PTY) | 3 | ✅ Done |
| Agent-first layout transition | 4 | ✅ Complete |
| Townhall foundations | 4 | ✅ Complete |
| Source Control panel | 4 | ✅ Complete |
| Status bar | 4 | ✅ Complete |
| Categorized logs | 4 | ✅ Complete |
| Agent panels | 5 | ⏳ Planned |
| Agent-to-agent routing | 6 | ⏳ Planned |
| Git integration | 7 | ⏳ Planned |

## Completed Refactors

| Refactor | Description | Status |
|---------|-------------|--------|
| refactor-1 | Document/Workspace extraction | ✅ Complete |
| refactor-2 | Layer boundary cleanup | ✅ Complete |
| refactor-3 | Agent-first layout transition | ✅ Complete |

### Refactor-2: Layer Boundary Cleanup (2025-01-20)

Cleaned up layer boundaries within the single-project structure:

- **M1:** Removed `SaveAsync` from `Document` model, replaced `ReactiveObject` with `INotifyPropertyChanged` in `FileTreeNode`
- **M3:** Extracted `IFileTreeService` interface, `StartWatching()` returns `IObservable<FileChangeEvent>`
- **M5:** Injected `IScheduler` into `FileTreeViewModel` (removed `AvaloniaScheduler.Instance` direct usage)
- **M6:** Created `SupportedFileTypes` static class in `Services/` (editor policy)
- **M7:** Stabilization — 340 tests pass

**Deferred:** Terminal pure logic namespace change (M2), FileTreeNode domain/UI split (M4), UI-post abstraction (M8)

## Stack Architecture

| Layer | Technology |
|-------|-----------|
| UI Framework | **Avalonia 12** + Semi.Avalonia (dark theme) |
| Architecture | **MVVM** with ReactiveUI |
| DI Container | Microsoft.Extensions.DependencyInjection |
| Language | C# (nullable enabled) |
| Runtime | .NET 10.0 |
| Platform | Cross-platform (Linux, macOS, Windows) |

## Future Direction

The next stages focus on making the center of gravity agent-first:

- **Townhall** — the primary shared workspace for agent activity, discussion, and user intervention
- **Editor** — still visible and powerful, but framed as the implementation surface
- **Agent panels** — individual UIs for each agent when dedicated surfaces are needed
- **@mention routing** — agents can request review from each other
- **Git integration** — status, diff, and commits from the sidebar

See [docs/roadmap/PHASES.md](docs/roadmap/PHASES.md) for the full plan.

## Why "Zaide"

Mozart's unfinished opera. Like an IDE that grows with you.
Also "Z" + "aide" — your ultimate assistant.

---

*Built slowly. Understood completely.*
