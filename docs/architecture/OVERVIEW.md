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
| **Far-left** | Nav bar (icon-only vertical strip) | ✅ Done |
| **Left** | File tree sidebar (Explorer mode) | ✅ Done |
| **Left** | Source Control panel (SC mode) | ✅ Done |
| **Center** | Townhall workspace (people/channels sidebar + chat area + input) | ✅ Done |
| **Right** | Editor (tabbed, syntax highlighting) | ✅ Done |
| **Bottom** | Terminal / Logs (Linux PTY-backed shell with categorized output) | ✅ Done |
| **Bottom** | Status bar (app info, cursor position, language, project, branch, AI model) | ✅ Done |

### Current layout (post-Refactor-3):

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

### Implemented layers (Refactor 3):

| Layer | Component | Description |
|-------|-----------|-------------|
| Agent-first layout transition | ✅ Complete | Townhall is now the visual center, editor repositioned as focused implementation surface |
| Townhall | ✅ Complete | Shared activity thread with people/channels sidebar, chat area, and input |
| Source Control panel | ✅ Complete | Real panel with branch selector, change list, staging area, and commit input |
| Status Bar | ✅ Complete | Shows app name, cursor position, language, project, branch, and AI model |
| Categorized Logs | ✅ Complete | Terminal output categorized as [BUILD], [AGENT], [LOG] with colored indicators |

### Planned layers (future):

| Layer | Phase | Description |
|-------|-------|-------------|
| Agent Panels | 5 | Dedicated agent surfaces when specialized views are needed |
| Agent Router | 6 | @mention routing between agents |
| Git Integration | 7 | Real git operations (branching, staging, committing with actual repo) |

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
