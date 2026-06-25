# Zaide — Architecture Overview

Zaide is an **AI-native IDE** where agents talk to each other, not just to you.
One agent codes, another reviews. They argue. You get better code.

---

## Two-Layer Architecture

```
┌──────────────────────────────────────────────────┐
│              ZAIDE IDE (Standalone)                │
│  Editor │ Tabs │ FileTree │ Terminal │ Git │ Build │
├──────────────────────────────────────────────────┤
│          AGENT-TO-AGENT LAYER                     │
│  AgentPanel │ Townhall │ @Mention Router          │
│  Agent A ↔ Agent B ↔ Agent C (debate model)      │
└──────────────────────────────────────────────────┘
```

The IDE works without agents. Agents are a layer on top that supercharges it.

---

## Core Principles

1. **Agent-to-Agent first** — agents debate each other, user observes and intervenes
2. **Transparency** — every agent action is logged in Townhall automatically
3. **No secret changes** — all file modifications are visible in real-time
4. **IDE is standalone** — must be usable as a plain editor without agents

---

## Tech Stack

| Layer       | Technology                      |
|-------------|----------------------------------|
| UI          | Avalonia 12 (AXAML)              |
| Theme       | Semi.Avalonia (dark)             |
| Language    | C# (.NET 10.0)                    |
| Pattern     | MVVM (ReactiveUI)                  |
| DI          | Microsoft.Extensions.DependencyInjection (Keyed Services ready) |
| Persistence | SQLite (structured data) + JSON (settings) |
| Platform    | Cross-platform (Linux, macOS, Windows) |

---

## Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Backend / Persistence** | SQLite + JSON for settings | Time-series data (townhall logs, agent actions) needs queries. SQLite keeps everything in one file. JSON for simple key-value settings. |
| **Image / Asset Storage** | Hybrid: Embedded (UI icons) + File Reference (project assets) | App icons and UI assets compile into the build. Agent avatars and runtime images stored as file refs for live replacement. |
| **Plugin Architecture** | Interface + DI manual registration | Define core interfaces (`IAgent`, `IPlugin`) now. Manual registration in DI. Expand to .NET 10 Keyed Services (`services.AddKeyedSingleton`) when plugin ecosystem grows. |

---

## UI Layout

```
┌──────────┬────────────────────────┬──────────────────┐
│          │                        │   Agent A        │
│  Files   │   Townhall / Editor    │                  │
│  (Tree)  │      (tab switch)      ├──────────────────┤
│          │                        │   Agent B        │
│  Git     │                        │                  │
├──────────┴────────────────────────┴──────────────────┤
│  [Terminal | Problems | Build | Output]   Ctrl+`     │
└──────────────────────────────────────────────────────┘
```

- **Left sidebar:** File tree, git status
- **Center:** Townhall (agent activity log) or Editor (code), tab-switched
- **Right:** Agent panels, split vertically — each agent has its own panel
- **Bottom:** Terminal and standard IDE tools, toggled with Ctrl+`

---

## Subsystems (planned)

| Subsystem | Phase | Description |
|-----------|-------|-------------|
| Window & Layout | 0 | 3-panel grid with Semi theme |
| File Tree | 1 | Sidebar file browser |
| Editor | 2 | Code editor with tabs |
| Terminal | 3 | Embedded terminal with toggle |
| Townhall | 4 | Agent transparency layer |
| Agent Panels | 5 | Individual agent UIs |
| Agent Router | 6 | @mention routing between agents |
| Git Integration | 7 | Status, diff, commit from sidebar |

These phases are tentative and will be refined as work progresses.

---

*Last updated: 2025-06-25*
