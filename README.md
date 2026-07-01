# Zaide

AI-native IDE. Agents talk to each other, not just to you.

**Status:** Phase 3 (Terminal) ✅ Complete — Phase 4 (Townhall) planned next.

## Philosophy

```
Cursor:  User → Agent → User → Agent   (A-to-U, single agent)
Zaide:   Agent A ↔ Agent B ↔ Agent C   (A-to-A, multi-agent debate)
         User watches, intervenes when needed
```

AI's biggest weakness is self-confirmation (hallucination).
One agent codes, another reviews. They argue. You get better code.

## Current Layout

```
┌──────────┬────────────────────────┬──────────────────┐
│          │                        │   Agent Area     │
│  Files   │        Editor          │   (placeholder)  │
│  (Tree)  │    (tabbed, syntax     │                  │
│          │     highlighting)      │                  │
├──────────┴────────────────────────┴──────────────────┤
│  [Terminal]                                Ctrl+`     │
└──────────────────────────────────────────────────────┘
```

- **Left:** File tree sidebar
- **Center:** Tabbed code editor with syntax highlighting (AvaloniaEdit)
- **Right:** Agent area (placeholder — agent panels coming in Phase 5)
- **Bottom:** Terminal panel (Linux PTY-backed shell), toggled with Ctrl+`

> **Note:** The agent-to-agent layer (Townhall, agent panels, @mention routing)
> is a future goal. The current app is an IDE foundation — usable standalone.

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
| Townhall / Agent transparency | 4 | ⏳ Planned |
| Agent panels | 5 | ⏳ Planned |
| Agent-to-agent routing | 6 | ⏳ Planned |
| Git integration | 7 | ⏳ Planned |

## Completed Refactors

| Refactor | Description | Status |
|---------|-------------|--------|
| refactor-1 | Document/Workspace extraction | ✅ Complete |
| refactor-2 | Layer boundary cleanup | ✅ Complete |

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

The planned agent-to-agent layer will add:

- **Townhall** — a scrollable, filterable log of all agent actions
- **Agent panels** — individual UIs for each agent with user input fields
- **@mention routing** — agents can request review from each other
- **Git integration** — status, diff, and commits from the sidebar

See [docs/roadmap/PHASES.md](docs/roadmap/PHASES.md) for the full plan.

## Why "Zaide"

Mozart's unfinished opera. Like an IDE that grows with you.
Also "Z" + "aide" — your ultimate assistant.

---

*Built slowly. Understood completely.*
