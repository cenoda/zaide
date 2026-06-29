# Zaide — Architecture Overview

Zaide is an **AI-native IDE** where agents talk to each other, not just to you.
One agent codes, another reviews. They argue. You get better code.

---

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

### Planned layers (future):

| Layer | Phase | Description |
|-------|-------|-------------|
| Townhall | 4 | Agent activity log in center area |
| Agent Panels | 5 | Individual agent UIs replacing the placeholder |
| Agent Router | 6 | @mention routing between agents |
| Git Integration | 7 | Status, diff, commit from sidebar |

The IDE works without agents. The agent layer is built on top.

---

## Core Principles

1. **Agent-to-Agent first** — agents debate each other, user observes and intervenes (planned for Phase 4–6)
2. **Transparency** — every agent action is logged in Townhall automatically (planned)
3. **No secret changes** — all file modifications are visible in real-time
4. **IDE is standalone** — must be usable as a plain editor without agents ✅

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

*Last updated: 2026-06-29*
