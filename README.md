# Zaide

AI-native IDE. Agents talk to each other, not just to you.

## Philosophy

```
Cursor:  User → Agent → User → Agent   (A-to-U, single agent)
Zaide:   Agent A ↔ Agent B ↔ Agent C   (A-to-A, multi-agent debate)
         User watches, intervenes when needed
```

AI's biggest weakness is self-confirmation (hallucination).
One agent codes, another reviews. They argue. You get better code.

## Layout

```
┌──────────┬────────────────────────┬──────────────────┐
│          │                        │   Agent A        │
│  Files   │   Townhall / Editor    │                  │
│  (Tree)  │      (tab switch)       ├──────────────────┤
│          │                        │   Agent B        │
│  Git     │                        │                  │
├──────────┴────────────────────────┴──────────────────┤
│  [Terminal | Problems | Build | Output]   Ctrl+`     │
└──────────────────────────────────────────────────────┘
```

- **Left:** Classic IDE sidebar (files, git status)
- **Center:** Your main stage. Townhall to watch agents work, Editor to code yourself.
- **Right:** Each agent has their own panel. They work independently, report to Townhall.
- **Bottom:** Terminal and standard IDE tools. Toggle with Ctrl+`.

## Agent-to-Agent

- Each agent panel has a **user input** — talk to any agent directly
- Use `@agent` mentions to route messages between agents
- **Townhall** is the shared transparency layer: every agent action is logged here automatically
- No agent can work in secret. You always see what they changed.

## Stack

- **Avalonia 12** + **Semi.Avalonia** (dark theme)
- .NET 9
- Cross-platform desktop (Linux, macOS, Windows)

## MVP

- [ ] 3-panel grid layout with Semi theme
- [ ] File tree sidebar
- [ ] Townhall / Editor center with tab switching
- [ ] 2 agent panels (right, split top/bottom)
- [ ] Bottom panel with Terminal (Ctrl+` toggle)
- [ ] Git status in sidebar
- [ ] Agent-to-agent `@`mention routing
- [ ] Townhall auto-log: all agent file changes appear in shared view

## Why "Zaide"

Mozart's unfinished opera. Like an IDE that grows with you.
Also "Z" + "aide" — your ultimate assistant.

---

*Built slowly. Understood completely.*
