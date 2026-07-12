# Zaide

AI-native IDE. Agents talk to each other, not just to you.

**Status:** Roadmap V1 is complete (Phase 0 through Phase 7.4).
[Roadmap V2 — IDE Core Upgrade](docs/roadmap/V2.md) is in progress — Phase 8
umbrella plan is locked and Phase 8.1 (Settings Foundation) is complete;
Phase 8.2 is complete, and Phase 8.3 implementation is complete through M4.
Phase 8 closeout verification is complete; Phases 9 through 13 remain ahead. Zaide currently has the IDE
foundation, Townhall workspace, direct agent panels, `@mention` routing, and
local Git status/diff/stage/commit workflows delivered by V1. The completed V1
plans are archived under [`docs/phases/v1/`](docs/phases/v1/).

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

Townhall now has explicit in-memory session seed data (channels, agents, empty
per-channel message collections), auto-logging, kind-based visual rendering, and
filtering (Phase 4 complete — 2026-07-08). The
layout work from refactor-3/4 is now backed by behavioral features.

## Current Layout (UI Scaffold From Refactor-3/4)

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
- **Center:** Townhall UI scaffold with people/channels sidebar, chat area, and input
- **Right:** Editor (tabbed, syntax highlighting) with townhall link and focused file info; agent-panel host lives below the editor in the same column
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

This shift is intentional. Zaide should eventually read as an agent-native
workspace first, not as a conventional editor with an AI sidebar.

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
| Phase 3.8: TUI compatibility | 3.8 | ✅ Done |
| Phase 3.9: Terminal UX polish | 3.9 | ✅ Done |
| Phase 3.9.1: Terminal tabs | 3.9.1 | ✅ Done |
| Phase 4: Agent workspace foundations | 4 | ✅ Done |
| Agent panels | 5 | ✅ Done |
| Agent-to-agent routing | 6 | 🟡 Implemented (with documented limitations) |
| Git integration | 7 | 🟢 Complete (7.1–7.4: read seam, live wiring, diff view, stage/unstage, local commit) |

## Completed Refactors

| Refactor | Description | Status |
|---------|-------------|--------|
| refactor-1 | Document/Workspace extraction | ✅ Complete |
| refactor-2 | Layer boundary cleanup | ✅ Complete |
| refactor-3 | Townhall/editor shell remap and UI scaffold | ✅ Complete |
| refactor-4 | Visual polish pass on the remapped UI | ✅ Complete |

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

## Roadmap V1 Outcome

Roadmap V1 established the following agent-workspace foundation:

- **Agent panels** — individual UIs for each agent (Phase 5 complete: render + direct input + one minimal execution path + Townhall mirroring)
- **@mention routing** — agents can request review from each other (Phase 6 complete: mechanical routing + Townhall visibility for failures and routed-flow outcomes)
- **Git integration** — repository status, diff, staging, and local commits from the sidebar (Phase 7 complete)
- **Editor** — still visible and powerful, but framed as the implementation surface

See the completed [Roadmap V1](docs/roadmap/PHASES.md) and its
[versioned phase-plan archive](docs/phases/README.md) for the implementation
record.

## Roadmap V2 Direction

[Roadmap V2](docs/roadmap/V2.md) upgrades the standalone IDE core before deeper
AI-native orchestration work:

- **Phase 8:** Core platform, settings, commands, and authoritative C# project
  context
- **Phase 9:** Editor UX
- **Phase 10:** C# language intelligence and document formatting
- **Phase 11:** Build, run, and test workflow
- **Phase 12:** C# debugging through DAP
- **Phase 13:** Release hardening

V2 is in progress. Phase 8 umbrella plan is live-code-verified at
`docs/phases/v2/phase-8/IMPLEMENTATION_PLAN.md`. Sub-phase 8.1 (Settings
Foundation) is complete across its five implementation slices (M1–M6 closeout
2026-07-11, full suite green); Phase 8.2 (command registry and keybindings) has
closed on 2026-07-12; Phase 8.3 is implemented through M4 with automated
verification green, including the manual `Failed` → `Project error` GUI smoke
check.
Multi-cursor editing and broader AI-native orchestration are outside V2.

## Why "Zaide"

Mozart's unfinished opera. Like an IDE that grows with you.
Also "Z" + "aide" — your ultimate assistant.

---

*Built slowly. Understood completely.*
