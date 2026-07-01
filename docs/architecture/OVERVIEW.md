# Zaide — Architecture Overview

Zaide is an **AI-native IDE** where agents talk to each other, not just to you.
One agent codes, another reviews. They argue. You get better code.

---

## Direction

Zaide is intentionally moving away from the classic IDE shape where the editor
is the unquestioned center and AI sits in a side panel.

The target architecture is **agent-first**:

- The primary visual focus is the shared agent workspace
- The editor remains visible, but acts as the implementation surface
- The file tree and terminal support the workflow without dominating it

This is a product-direction choice, not a cosmetic layout tweak.

## Current Architecture

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

### Current layers (implemented):

| Layer | Component | Status |
|-------|-----------|--------|
| **Left** | File tree sidebar | ✅ Done |
| **Center** | Tabbed editor with syntax highlighting (AvaloniaEdit) | ✅ Done |
| **Right** | Agent area (placeholder panel) | ✅ Done (shell only) |
| **Bottom** | Terminal (Linux PTY-backed shell) | ✅ Done |

### Target layout direction:

```
┌──────────┬────────────────────────────────────┬──────────────┐
│ Files    │ Townhall                           │ Editor       │
│ (Tree)   │ active thread, agent discussion,   │ focused file │
│          │ user intervention, task state      │ diff/edit    │
├──────────┴────────────────────────────────────┴──────────────┤
│ Terminal / Logs                                                │
└────────────────────────────────────────────────────────────────┘
```

### Planned layers (future):

| Layer | Phase | Description |
|-------|-------|-------------|
| Agent-first layout transition | 4 | Move Townhall into the visual center and reposition the editor as a focused implementation surface |
| Townhall | 4 | Shared activity thread for agent work, review, and user intervention |
| Agent Panels | 5 | Dedicated agent surfaces when specialized views are needed |
| Agent Router | 6 | @mention routing between agents |
| Git Integration | 7 | Status, diff, commit from sidebar |

Zaide should still work without full agent infrastructure, but the product is no
longer editor-first. The agent layer is becoming the main stage, not an add-on.

---

## Core Principles

1. **Agent-first workspace** — the main screen should foreground agent activity and user intervention
2. **Transparency** — every agent action is logged in Townhall automatically (planned)
3. **No secret changes** — all file modifications are visible in real-time
4. **Editor as execution surface** — code editing remains first-class, but not the sole center of attention
5. **IDE is standalone** — must remain usable even before the full agent system exists ✅

---

## Tech Stack

| Layer       | Technology                      |
|-------------|----------------------------------|
| UI          | Avalonia 12 (C# construction, no AXAML for custom views) |
| Theme       | Semi.Avalonia (dark)             |
| Language    | C# (.NET 10.0, nullable enabled) |
| Pattern     | MVVM (ReactiveUI)                |
| DI          | Microsoft.Extensions.DependencyInjection |
| Platform    | Cross-platform (Linux, macOS, Windows) |
| Persistence | *(none yet — deferred to Phase 4+)* |
| Plugin      | *(none yet — deferred to Phase 6+)* |

---

## Future Technical Considerations

The following decisions have been discussed but are **not yet implemented**.
They will be revisited when their respective phases begin.

| Consideration | Planned Approach | Rationale |
|---------------|------------------|-----------|
| **Persistence** | SQLite (structured data) + JSON (settings) | Time-series data (townhall logs) needs queries. JSON for simple key-value settings. |
| **Image / Asset Storage** | Hybrid: Embedded (UI icons) + File Reference (project assets) | App icons compile in. Agent avatars stored as file refs for live replacement. |
| **Plugin Architecture** | Interface + DI manual registration | Core interfaces (`IAgent`, `IPlugin`) defined when agent layer begins. .NET 10 Keyed Services for plugin DI later. |

---

*Last updated: 2026-07-01*
