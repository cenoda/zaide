# Zaide

AI-native IDE. Agents talk to each other, not just to you.

**Status:** Roadmap V1 is complete (Phase 0 through Phase 7.4).
[Roadmap V2 — IDE Core Upgrade](docs/roadmap/V2.md) is **complete** (Phase 8
through Phase 13, closeout 2026-07-16) with documented limitations. Phase 9
delivers the Command Palette, active-document Search/Replace, syntax-neutral
folding, tab lifecycle/reordering, and truthful editor/status-bar feedback.
Phase 10 adds C# LSP support with csharp-ls. Phase 11 delivers Build / Run /
Test over Phase 8.3 project context, structured Output, build diagnostics in
Problems, Test Results surface, and cancel/one-at-a-time workflow execution.
Phase 12 (DAP debugging) is complete: NetCoreDbg adapter, breakpoints,
execution controls, Debug Console, call stack, variables, and error/recovery
hardening. Phase 13 (Release Hardening) is complete with explicit limitations
([M5 closeout](docs/phases/v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md)):
measured budgets, recovery inventories, critical-path evidence, Linux release
smoke, and doc truth-sync. Zaide has the IDE foundation, Townhall workspace,
direct agent panels, `@mention` routing, and local Git
status/diff/stage/commit workflows delivered by V1, plus the V2 IDE core.
Completed plans are archived under [`docs/phases/`](docs/phases/).
[Roadmap V3 — AI-Native Orchestration](docs/roadmap/V3.md) is now a
**user-accepted implementation-order roadmap**. Refactor 6.1 is closed and
Refactor 6.2's scheduled M1–M12 feature-first migration is accepted closed.
Optional M13 root admissions are declined. Refactor 6.3 M0 is accepted; **M1**
is complete at `e590a79`, **M2** at `d9799ad`, **M3** at `22b869e` (manual
terminal smoke not run), **M4** at `698b094` (manual agent-panel routing smoke
not run), **M5** at `273cc56` (manual verification not required), **M6a**
at `c59ad7b` (AppCore DI registration module; first completed M6 slice;
automated verification green; manual verification not required), **M6b**
at `43b8e85` (Settings DI registration module; second completed M6 slice;
automated verification green; manual verification not required), **M6c**
at `1ad3625` (Workspace DI registration module; third completed M6 slice;
automated verification green; manual verification not required), and **M6d**
at `234a38f` (Editor DI registration module; fourth completed M6 slice;
automated verification green; manual verification not required). **Refactor
6.3 M1–M5 and M6a–M6k are complete** as individually completed slices. **M6k**
(Debugging registration module) is complete at `df262ac`
(`AddZaideDebugging`), completing the M6 registration-module series. **M7**
(composition-root store / removal of public `App.Services`) is complete at
`554552f`. **M8** (ordered shutdown owner) is complete at `874aa79`;
`ApplicationShutdown.Run` now owns exactly-once ordered teardown. **M9a**
(Agent send / Townhall mirror extraction) is complete at `172f2a3`. **M9b**
(panel navigation extraction) is complete at `33a1806`. **M9c** (activation
host extraction) is complete at `bcb1e97`, completing the M9 series. **M10** is
next eligible, has not started, and requires separate explicit authorization.
Completing M9 does not authorize M10.

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
- **Phase 12:** C# debugging through DAP ✅ complete
- **Phase 13:** Release hardening ✅ complete with explicit limitations
  ([M5 evidence](docs/phases/v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md))

V2 is **complete** (2026-07-16). Phase plans live under `docs/phases/v2/`.
Documented limitations include Linux-first validation, environment-dependent
real NetCoreDbg proofs, and desktop keyboard/focus rows that remain not
validated without a non-synthetic input path. Multi-cursor editing and broader
AI-native orchestration are outside V2.

### V2 by the Numbers

From the V1 closeout (`84b5246`) to the V2 closeout (`f56711a`), Zaide grew
from an agent-workspace foundation into a credible standalone IDE core:

- **186 commits** across Phase 8 through Phase 13
- **523 files changed**: 465 added and 58 modified
- **83,523 net new lines**: 84,292 insertions and 769 deletions
- **2,172 passing tests**, up from 817 at the V1 closeout

Six days of focused work turned the roadmap into a project that now feels like
an IDE, not merely an editor. V2 is done.

## Roadmap V3

[Roadmap V3](docs/roadmap/V3.md) defines the accepted next agent-native layer
while preserving the completed V2 IDE core. Its direction is:

- unify Townhall channels and Agent direct conversations;
- build a research-driven native Zaide harness with license-compliant
  open-source reuse;
- support ACP as an independent, equally presented external-agent backend;
- add configurable live IDE/debug context, optional redacted raw traces,
  memory scopes, tools, permissions, and attributable workspace changes;
- begin with bounded architecture refactors (`6.1`–`6.3`, then Refactors 7
  and 8) before Phase 14.

The implementation order is accepted. Refactor 6.3 **M6a** is complete at
`c59ad7b` (AppCore DI registration module — first completed M6 slice). **M6b**
is complete at `43b8e85` (Settings DI registration module — second completed
M6 slice). **M6c** is complete at `1ad3625` (Workspace DI registration module —
third completed M6 slice). **M6d** is complete at `234a38f` (Editor DI
registration module — fourth completed M6 slice). **M6e** is complete at
`8ab50c0` (Terminal DI registration module — fifth completed M6 slice). **M6f**
is complete at `cd809d2` (Agents DI registration module — sixth completed M6
slice). **M6g** is complete at `1f18e49` (Townhall DI registration module —
seventh completed M6 slice). **M6h** is complete at `9f514cd` (SourceControl
DI registration module — eighth completed M6 slice). **M6i** is complete at
`e6f9fb8` (ProjectSystem DI registration module — ninth completed M6 slice).
**M6j** is complete at `e7785b4` (Language DI registration module — tenth
completed M6 slice). **M6k** is complete at `df262ac` (Debugging DI
registration module — eleventh and final M6 slice). The M6 series is complete.
**M7** is complete at `554552f`; public `App.Services` is removed and the
internal `CompositionRoot.Services` residual remains. **M8** is complete at
`874aa79` (ordered shutdown owner). **M9a** is complete at `172f2a3` (Agent
send / Townhall mirror extraction). **M9b** is complete at `33a1806` (panel
navigation extraction). **M9c** is complete at `bcb1e97` (activation host
extraction), completing the M9 series. **M10** is next eligible and requires
separate explicit authorization. Phase 14 and
every preceding refactor still require their own live-code-verified M0
acceptance before production implementation.

## Why "Zaide"

Mozart's unfinished opera. Like an IDE that grows with you.
Also "Z" + "aide" — your ultimate assistant.

---

*Built slowly. Understood completely.*
