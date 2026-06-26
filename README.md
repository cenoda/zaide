# Zaide

AI-native IDE. Agents talk to each other, not just to you.

**Status:** Phase 2 (Editor) ✅ Complete — Phase 2.1 (Editor Polish) planned.

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

## Status

| Feature | Phase | Status |
|---------|-------|--------|
| 3-panel grid layout | 0 | ✅ Done |
| Semi.Avalonia dark theme | 0 | ✅ Done |
| Bottom panel toggle (Ctrl+`) | 0 | ✅ Done |
| DI container (MS DI + ReactiveUI) | 0 | ✅ Done |
| Ayaka Violet color palette | 0 | ✅ Done |
| File tree sidebar | 1 | ✅ Done |
| Townhall / Editor center | 2 | ✅ Done |
| Terminal | 3 | ⏳ Planned |
| Townhall agent logging | 4 | ⏳ Planned |
| Agent panels | 5 | ⏳ Planned |
| Agent-to-agent routing | 6 | ⏳ Planned |
| Git integration | 7 | ⏳ Planned |

## Agent-to-Agent

- Each agent panel has a **user input** — talk to any agent directly
- Use `@agent` mentions to route messages between agents
- **Townhall** is the shared transparency layer: every agent action is logged here automatically
- No agent can work in secret. You always see what they changed.

## Stack

| Layer | Technology |
|-------|-----------|
| UI Framework | **Avalonia 12** + Semi.Avalonia (dark theme) |
| Architecture | **MVVM** with ReactiveUI |
| DI | Microsoft.Extensions.DependencyInjection |
| Language | C# (nullable enabled) |
| Runtime | .NET 10.0 |
| Platform | Cross-platform (Linux, macOS, Windows) |

## Why "Zaide"

Mozart's unfinished opera. Like an IDE that grows with you.
Also "Z" + "aide" — your ultimate assistant.

---

*Built slowly. Understood completely.*
