# Zaide — Architecture Overview

Zaide is an **AI-native IDE** that is moving toward a real agent workspace.
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

## Current Layout Scaffold (Post-Refactor-4)

The current app contains the visual shell for the future agent workspace:

```
┌──────┬──────────┬──────────────────────────────────┬────────────────────┐
│      │          │                                  │                    │
│ Nav  │ Explorer │     Townhall                    │   Editor           │
│ Bar  │  /       │     (people/channels sidebar +  │   (focused code +  │
│      │  SC      │      chat area + input)         │    status info)    │
│      │          │                                  │                    │
├──────┴──────────┴──────────────────────────────────┴────────────────────┤
│ Terminal / Logs (categorized output)                                   │
├─────────────────────────────────────────────────────────────────────────┤
│ Status Bar (app info, cursor position, language, project, branch, AI model) │
└─────────────────────────────────────────────────────────────────────────┘
```

### Current layers

| Layer | Component | Phase | Status |
|-------|-----------|-------|--------|
| **Far-left** | Nav bar (icon-only vertical strip) | 0 | ✅ Done |
| **Left** | File tree sidebar (Explorer mode) | 1 | ✅ Done |
| **Left** | Source Control panel (SC mode) | 1 | ✅ Done |
| **Center** | Townhall — activity surface with 8-kind entry taxonomy, auto-logged entries on send/switch, kind-based visual rendering, and All/Chat/Activity filter toggle; explicit in-memory session seed data creates channels, agents, and empty per-channel message collections | 4 | ✅ Done |
| **Right** | Editor (tabbed, syntax highlighting) | 2 | ✅ Done |
| **Bottom** | Terminal / Logs — full-screen TUI support via alternate screen buffer, saved-cursor state, alt-screen scrollback isolation, and categorized output | 3 | ✅ Done |
| **Bottom** | Status bar (app info, cursor position, language, project, branch, AI model) | 3 | ✅ Done |

### Refactor-delivered surfaces

| Layer | Component | Description |
|-------|-----------|-------------|
| Shell remap | ✅ Complete | Townhall is now the visual center, editor repositioned as focused implementation surface |
| Townhall UI scaffold | ✅ Complete | People/channels sidebar, chat area, and input are present as layout scaffolding |
| Source Control panel | ✅ Complete | Panel with branch selector, change list, staging area, and commit input |
| Status Bar | ✅ Complete | Shows app name, cursor position, language, project, branch, and AI model |
| Categorized Logs | ✅ Complete | Terminal output categorized as [BUILD], [AGENT], [LOG] with colored indicators |

### Phase 3.8 (TUI compatibility — complete, 2026-07-07)

- Dual-buffer (main + alternate) terminal screen model with saved-cursor state
- Parser-level support for DEC private modes `?1047`/`?1048`/`?1049` and ESC 7/8 save/restore cursor
- Transcript-tested compatibility with `less` and `vim` open-exit flows
- Log-entry suppression during full-screen TUI sessions to avoid redraw-noise pollution
- View-layer suppression of main-buffer selection and scrollback during full-screen apps
- 510 tests pass, 0 fail

### Refactor 4 (Visual Polish — complete)

- Typography scale via `TextStyles`, animation helpers, spacing tokens, elevation contrast, file tree polish, chat rebuild, status bar, glass/fallback
- All regression gates (build, test, luminance VC-3, animations, VC-4/VC-11 audits) passed at M7

### Phase 3.9.1 (Terminal Tabs — complete, 2026-07-07)

- `ITerminalSessionFactory` / `TerminalSessionFactory` — creates one `ITerminalService` + `TerminalViewModel` pair per call, enabling independent per-tab shell sessions
- `ITerminalHost` / `TerminalHost` — owns the tab collection, active-tab switching, create/close/dispose lifecycle, and active-session error projection
- `TerminalTabViewModel` — per-tab record with title, active state, and session reference
- `TerminalTabHost` (view layer) — retains one `TerminalPanel` per tab in a `Dictionary` cache so each session keeps its own search, viewport, selection, and log-view state; shows only the active tab's panel
- `TerminalTabStrip` (view layer) — renders tab title labels, active-highlight, new-tab (+), and close-tab (×) controls; clicking `×` on the sole remaining tab hides the bottom terminal panel instead of disposing the last session
- `MainWindow` wires `TerminalTabHost` in the bottom panel; focus/startup routed through the view host seam without direct single-session calls
- 565 tests pass, 0 fail

### Planned layers

| Layer | Phase | Description |
|-------|-------|-------------|
| Agent Panels | 5 | Dedicated agent surfaces when specialized views are needed |
| Agent Router | 6 | @mention routing between agents |
| Git Integration | 7 | Real git operations (branching, staging, committing with actual repo) |

Zaide should still work without full agent infrastructure. The current layout
already points toward the agent-first direction, and Phase 4 (Agent Workspace
Foundations) is now complete — Townhall is a real activity surface with
auto-logging, kind-based rendering, filtering, and explicit in-memory session
seed data.

---

## Core Principles

1. **Agent-first workspace** — the main screen should foreground agent activity and user intervention
2. **Transparency** — every agent action should be logged in Townhall automatically
3. **No secret changes** — all file modifications are visible in real-time
4. **Editor as execution surface** — code editing remains first-class, but not the sole center of attention
5. **IDE is standalone** — must remain usable even before the full agent system exists

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| UI | Avalonia 12 (C# construction, no AXAML for custom views) |
| Theme | Semi.Avalonia (dark) |
| Language | C# (.NET 10.0, nullable enabled) |
| Pattern | MVVM (ReactiveUI) |
| DI | Microsoft.Extensions.DependencyInjection |
| Platform | Cross-platform (Linux, macOS, Windows) |
| Persistence | *(none yet — likely starts when Phase 4 needs durable activity history)* |
| Plugin | *(none yet — deferred to Phase 6+)* |

---

## Future Technical Considerations

The following decisions have been discussed but are **not yet implemented**.
They will be revisited when their respective phases begin.

| Consideration | Planned Approach | Rationale |
|---------------|------------------|-----------|
| **Persistence** | SQLite (structured data) + JSON (settings) | Time-series activity history needs queries. JSON for simple key-value settings. |
| **Image / Asset Storage** | Hybrid: Embedded (UI icons) + File Reference (project assets) | App icons compile in. Agent avatars stored as file refs for live replacement. |
| **Plugin Architecture** | Interface + DI manual registration | Core interfaces (`IAgent`, `IPlugin`) defined when agent layer begins. .NET 10 Keyed Services for plugin DI later. |
| **Multi-provider agent architecture** | `IAgentProvider` abstraction + `AgentRegistry` service managing N agents, each with its own provider/model configuration | Still deferred beyond the first Phase 5 execution slice. Phase 5 now plans only one minimal direct-execution path to one configured OpenAI-compatible endpoint; broad provider abstraction remains unnecessary until later multi-provider or richer agent-execution work appears. Phase 4.1 already reserved `SourceProvider`/`SourceModel`/`ThreadId` on the Townhall activity entry model so that later work does not force a breaking schema change. See `docs/phases/phase-4.1/IMPLEMENTATION_PLAN.md` and `docs/phases/phase-5.3/IMPLEMENTATION_PLAN.md`. |
| **Agent wire format** | Phase 5 baseline: one OpenAI-compatible, non-streaming request/response shape over built-in `HttpClient`; anything broader remains undecided | This is now intentionally decided at the Phase 5 planning level so the first direct-execution path is concrete and narrow. It is not yet a commitment to a broader provider platform, streaming protocol, or long-term multi-provider contract. |

---

*Last updated: 2026-07-08*
