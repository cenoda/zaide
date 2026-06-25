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
| Platform    | Cross-platform (Linux, macOS, Windows) |

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
