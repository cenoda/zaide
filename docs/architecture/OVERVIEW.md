# Zaide — Architecture Overview

Zaide is an **AI-native IDE** built around a real agent workspace foundation.
One agent codes, another reviews. They argue. You get better code.

**Roadmap status:** V1 is complete (Phase 0 through Phase 7.4).
[Roadmap V2 — IDE Core Upgrade](../roadmap/V2.md) is **complete** (Phase 8
through Phase 13, closeout 2026-07-16) with documented limitations. Sub-phase
8.1 (Settings Foundation), 8.2 (Command Registry and Keybindings), and 8.3
(Authoritative Project Context) are complete. Phase 9 delivers the registry-
backed Command Palette, active-document Search/Replace, syntax-neutral folding,
tab lifecycle/reordering, and editor status projections. Phase 10 (C# language
intelligence via LSP), Phase 11 (Project Workflow — Build / Run / Test), and
Phase 12 (DAP debugging) are complete. Phase 13 (Release Hardening) is
**complete with explicit limitations**
([M5 closeout](../phases/v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md)): locked
performance budgets, settings/workflow/LSP/DAP recovery inventories, critical-
path evidence, Linux release smoke with honest not-validated rows, and
documentation truth-sync.
[Roadmap V3 — AI-Native Orchestration](../roadmap/V3.md) is an **accepted
implementation-order roadmap**. Refactor 6.1 **M0–M5 are complete and closed**
(rules, hybrid inventory, legacy allowlist ratchet, public full-name baseline
393/348/45, expanded tracked production **C#** root admission, documentation
closeout and M0 representation proof). Refactor 6.2 **M1–M12 scheduled
mechanical migration is complete** on `master` at `72102da` (feature-first
tree under `App`, `Features`, and `UI/DesignSystem`). Optional M13 root
admissions are **declined**. Refactor 6.3 M0 is **accepted** at
[`docs/refactor/refactor-6.3/IMPLEMENTATION_PLAN.md`](../refactor/refactor-6.3/IMPLEMENTATION_PLAN.md);
**M1** complete at `e590a79`, **M2** at `d9799ad`, **M3** at `22b869e`
(manual terminal smoke not run), **M4** at `698b094` (manual agent-panel
routing smoke not run), **M5** at `273cc56` (manual verification not
required), **M6a** at `c59ad7b` (AppCore DI registration module — first
completed M6 slice; automated verification green; manual verification not
required), **M6b** at `43b8e85` (Settings DI registration module — second
completed M6 slice; automated verification green; manual verification not
required), **M6c** at `1ad3625` (Workspace DI registration module —
third completed M6 slice; automated verification green; manual verification
not required), and **M6d** at `234a38f` (Editor DI registration module —
fourth completed M6 slice; automated verification green; manual verification
not required). **Refactor 6.3 M1–M5 and M6a–M6k are complete** as individually
completed slices. **M6k** (Debugging registration module) is complete at
`df262ac` (`AddZaideDebugging`), completing the M6 series. **M7** is complete
at `554552f`: public `App.Services` is removed; the internal
`CompositionRoot.Services` store and two composition locator residuals remain.
**M8** is complete at `874aa79`: `ApplicationShutdown.Run` owns ordered,
exactly-once teardown while the App exit path remains synchronous. **M9a** is
complete at `172f2a3`: internal `AgentTownhallMirrorCoordinator` owns agent
send and Townhall mirroring. **M9b** is complete at `33a1806`: internal
`ShellPanelNavigation` owns nine panel commands while MWVM retains notifying
mode state. **M9c** is next eligible, has not started, and requires separate
authorization.
Refactors 7 / 8 have no production authorization
until their own M0 acceptances. Non-C# assets remain outside the
root-admission ratchet. No V3 production feature implementation is active.

---

## Source architecture (target vs current)

Zaide has **two distinct descriptions** that must not be collapsed:

1. **Product layers** (this document historically) — IDE shell vs agent
   workspace as a user-facing product story.
2. **Code module rules** (approved target) — feature-first ownership with
   optional Domain / Application / Infrastructure / Presentation / Contracts
   layers, enforced later by architecture tests.

Product “IDE layer” and “Agent layer” language is **not** a code dependency
boundary. Dependency direction is governed by the feature/module rules in
[`docs/CONVENTIONS.md`](../CONVENTIONS.md) (canonical detailed rules).

### Approved target (Refactor 6.1)

Feature-first ownership under one production assembly (`Zaide`):

```text
src/
  App/Composition, App/Shell
  Features/{Editor,Workspace,Townhall,Agents,Settings,SourceControl,
            ProjectSystem,Language/Infrastructure/Lsp,
            Debugging/Infrastructure/Dap,Terminal}
  Infrastructure/{FileSystem,Processes,Persistence}   # multi-feature only
  UI/{DesignSystem,Shared}                              # DesignSystem = current Styles
```

Key target rules (detail in CONVENTIONS):

- Features use only the optional layers they need; empty ceremonial layers are
  forbidden.
- Snapshots are owned by the producing feature; view state stays in
  Presentation and is not consumed as domain truth.
- Allowed dependency directions: Presentation/Infrastructure → Application
  contracts/Domain; Application → Contracts/Domain; Domain → Domain/BCL only.
- Forbidden: Application → Presentation or concrete Infrastructure;
  Infrastructure → Presentation; cross-feature Presentation/Infrastructure
  consumption; non-composition `IServiceProvider` / static `App.Services` use.
- Root `Infrastructure/` and `UI/Shared/` are deny-by-default multi-consumer
  admissions; LSP stays under Language, DAP under Debugging.
- Visibility is internal-by-default / public-by-exception (348 public-type
  full-name baseline + 393/348/45 compiled ceiling; executable in M4).
- Current lifetimes: application, workspace, process, projection, editor
  session, terminal session. Conversation, agent session, and run (R61-LT01–
  LT03) are deferred to Refactor 7.

Evidence, violation dispositions, and migration order:
[Refactor 6.1 implementation plan](../refactor/refactor-6.1/IMPLEMENTATION_PLAN.md)
and [M0 architecture baseline](../refactor/refactor-6.1/M0_ARCHITECTURE_BASELINE.md).

### Live production tree (Refactor 6.2 M1–M12 complete)

Scheduled mechanical migration is complete. The live tree matches the approved
feature-first layout (optional M13 root admissions remain unauthorized):

```text
src/
  App/Composition/     # Program, App, command registry (6.2 M12); Registration/AppCore (6.3 M6a) + Settings (6.3 M6b) + Workspace (6.3 M6c) + Editor (6.3 M6d) + Terminal (6.3 M6e) + Agents (6.3 M6f) + Townhall (6.3 M6g) + SourceControl (6.3 M6h) + ProjectSystem (6.3 M6i)
  App/Shell/           # MainWindow, shell VMs/views, chrome (6.2 M12; Animations/IconFactory shell-owned R62-D03)
  UI/DesignSystem/     # tokens, icons, typography (6.2 M1)
  Features/Settings/   # Domain, Contracts, Infrastructure, Presentation (6.2 M2)
  Features/Workspace/  # Domain, Contracts, Infrastructure, Presentation (6.2 M3)
  Features/Editor/     # Domain, Contracts, Infrastructure, Presentation (6.2 M4; FileService parked R62-D01)
  Features/ProjectSystem/  # Domain, Contracts, Infrastructure, Presentation (6.2 M5a–M5c)
  Features/Language/   # Contracts + Application (6.2 M6a) + Infrastructure/Lsp (6.2 M6b)
  Features/Debugging/  # Contracts + Application (6.2 M7a) + Infrastructure/Dap (6.2 M7b) + Presentation (6.2 M7c)
  Features/SourceControl/  # Domain, Contracts, Application, Infrastructure, Presentation (6.2 M8; R61-V02 SourceControlState cleared in 6.3 M5)
  Features/Terminal/   # Contracts, Application, Infrastructure, Presentation (6.2 M9; R61-V05 cleared in 6.3 M3)
  Features/Townhall/   # Domain, Presentation (6.2 M10; R61-V16 preserved)
  Features/Agents/     # Domain, Contracts, Application, Infrastructure, Presentation (6.2 M11; R61-V06 cleared in 6.3 M4)
```

Technical-layer folders (`Models/`, `Services/`, `ViewModels/`, `Views/`) and
root composition C# are gone. One production project (`src/Zaide.csproj`), one
assembly (`Zaide`). Architecture tests under `tests/Zaide.Tests/Architecture/`
inventory the baseline (M2), ratchet known legacy debt (M3), and enforce the
public full-name baseline plus expanded root-folder admission (M4, updated for
6.2 M1–M12 and 6.3 M1–M6d). Root-admission inventories **tracked production C#
only** (`git ls-files` of `src/**/*.cs`): exact-file service-locator sites;
NamespaceDirection edges empty after 6.3 M5; deny-by-default tracked C# under
`src/Infrastructure/` / `src/UI/Shared/`; admitted folders `App` (Composition +
Shell + Composition/Registration), `Features` (all migrated features), `UI`
(DesignSystem only); and the current **346** public type names
(`PublicProductionTypeBaseline.txt`; M5 net −1; M6a–M9b internal-only). Live
inventory after M9b: total top-level **412**, public **346**, internal **66**,
production C# **374**, App C# **35**. Composition.Registration contains eleven
internal modules (AppCore, Settings, Workspace, Editor, Terminal, Agents,
Townhall, SourceControl, ProjectSystem, Language, Debugging). FindingIds remaining:
**2** (`R61-AL-LOC-App`, `R61-AL-LOC-Program`). Non-C# assets are not governed
by the root-admission detectors. Lifetime/composition debt remains for
Refactor 6.3 (**M9c** activation host extraction next and
separately unauthorized).

| Later work | Owns |
|------------|------|
| Refactor 6.1 | Closed; rules and executable ratchets |
| Refactor 6.2 | Mechanical feature-first migration (M1–M12 scheduled complete; M13 optional admission only) |
| Refactor 6.3 | Composition, visibility reduction, lifetime, dependency inversion |
| Refactor 7 / 8 | Agent-conversation domain; Townhall/shell UI foundation |

---

## Direction

Zaide is intentionally moving away from the classic IDE shape where the editor
is the unquestioned center and AI sits in a side panel.

The target architecture is **agent-first**:

- The primary visual focus is the shared agent workspace
- The editor remains visible, but acts as the implementation surface
- The file tree and terminal support the workflow without dominating it

This is a product-direction choice, not a cosmetic layout tweak.

## Current Layout (Post-Roadmap V1)

The current app contains the agent-workspace shell delivered through V1:

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

- `ITerminalServiceFactory` / `LinuxTerminalServiceFactory` — creates one `ITerminalService` (process owner) per call; Presentation pairs each service with a `TerminalViewModel` (Refactor 6.3 M3)
- `ITerminalHost` / `TerminalHost` — owns the tab collection, active-tab switching, create/close/dispose lifecycle, and active-session error projection
- `TerminalTabViewModel` — per-tab record with title, active state, and session reference
- `TerminalTabHost` (view layer) — retains one `TerminalPanel` per tab in a `Dictionary` cache so each session keeps its own search, viewport, selection, and log-view state; shows only the active tab's panel
- `TerminalTabStrip` (view layer) — renders tab title labels, active-highlight, new-tab (+), and close-tab (×) controls; clicking `×` on the sole remaining tab hides the bottom terminal panel instead of disposing the last session
- `MainWindow` wires `TerminalTabHost` in the bottom panel; focus/startup routed through the view host seam without direct single-session calls
- 565 tests pass, 0 fail

### Phase 10 (C# language intelligence — complete, 2026-07-14)

- **Stack:** `csharp-ls` 0.25.0 (global `dotnet tool`) + `StreamJsonRpc` 2.22.23
  over stdio Content-Length JSON-RPC.
- **Ownership:** `IProjectContextService` selects the project; `ILanguageSessionService`
  owns one session per eligible context; `LanguageDocumentBridge` sends
  didOpen/didChange/didClose from `Workspace`/tabs; feature services
  (`ILanguageCompletionService`, `ILanguageHoverService`, `ILanguageNavigationService`,
  `ILanguageSymbolService`, `ILanguageFormattingService`, `ILanguageDiagnosticsService`)
  hold structured state; Views/ViewModels project results only.
- **UI:** shared `EditorView` applies version-matched updates; `ProblemsPanel`
  projects diagnostics; `StatusBarViewModel` shows `LanguageIntelligenceText`
  (`C# · Ready` / `Loading…` / etc.); commands register through `ICommandRegistry`
  (`Ctrl+Space`, `F12`, `Ctrl+Shift+O`, `Ctrl+T`, `Ctrl+Shift+I`).
- **Limitations:** C# only; on-disk URIs only; no range formatting, format-on-type,
  code actions, rename, multi-language, build/run/test, or DAP.

### Roadmap V1 agent and Git layers

| Layer | Phase | Description |
|-------|-------|-------------|
| Agent Panels | 5 | Dedicated agent surfaces with direct input/execution and Townhall mirroring |
| Agent Router | 6 | @mention routing between agents — **implemented** (M6 closeout, 2026-07-08); routed content resolves and executes on the target panel; routing-visibility gaps closed in Phase 6.1 (2026-07-09) — routing failures and routed-flow outcomes now surface in Townhall via `MainWindowViewModel`, with `AgentRouter` kept Townhall-free |
| Git Integration | 7 | Live repo-backed read seam, Source Control panel wiring, basic diff view, and stage/unstage/local-commit flow (7.1–7.4 complete) |

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
| Persistence | *(not implemented in Roadmap V1)* |
| Plugin | *(not implemented in Roadmap V1)* |

---

## Roadmap V2 Direction (delivered)

Roadmap V2 strengthened the standalone IDE core before V3 expands the existing
agent foundation into richer AI-native orchestration.

| Layer | Phase | Direction |
|---------------|-------|-----------|
| Core platform and settings | 8 | ✅ Versioned settings with migration/recovery ownership, safe credential boundary, command/keybinding infrastructure, and authoritative C# project context |
| Editor UX | 9 | ✅ Command Palette, Search/Replace, folding, and tab/status improvements; multi-cursor deferred |
| C# language intelligence | 10 | ✅ LSP lifecycle, diagnostics, completion, hover, definition, symbols, document formatting, Format on Save (M7 closeout 2026-07-14) |
| Project workflow | 11 | ✅ Execution profiles and structured Build/Run/Test orchestration over the Phase 8 project context, with Output, Problems, test results, cancellation, and error navigation |
| C# debugging | 12 | ✅ Linux-validated DAP workflow with breakpoints, stepping, call stack, variables, and debug output |
| Release hardening | 13 | ✅ Measured performance, recovery, full settings compatibility matrix, E2E coverage, platform-status documentation, and closeout (M5 2026-07-16; explicit limitations retained) |

V2 continues to require the IDE to work without full agent infrastructure.
Specific LSP server, DAP adapter, protocol libraries, secret storage, and
performance budgets are phase-plan decisions with recorded evidence, not
universal architecture guarantees.

V2 core capabilities must be exposed through UI-independent, cancellable
services with structured results and observable state. The V2 UI consumes those
services, and later agent orchestration may consume the same seams. V2 does not
implement the agent automation, tool schemas, or permission model that would
invoke them autonomously.

---

## Roadmap V3 Direction

V3 is expected to add AI-native orchestration without making the IDE dependent
on one provider or harness. The accepted direction is:

- one Townhall conversation model for public channels and direct
  conversations;
- durable Actor/Agent Identity separated from runtime, session, conversation,
  and memory scope;
- a first-party, research-driven Zaide native harness;
- ACP as an independent external-agent backend, not a wrapper around the
  native harness;
- equal product placement with truthful capability negotiation across
  backends;
- structured, user-configurable IDE/debug context and optional redacted raw
  trace;
- explicit tools, permissions, audit, workspace-mutation, memory, and trust
  boundaries;
- feature-first source/module refactoring before Phase 14.

The live V2 code remains authoritative until each refactor or phase completes.
See the V3 roadmap for observed debt, unresolved questions, and the
required Refactor 6.1–6.3, Refactor 7, Refactor 8, and Phase 14 ordering.

---

## Earlier Future Technical Considerations

The following decisions were discussed earlier and were **not implemented in
Roadmap V1 or V2**. Where this table conflicts with the accepted V3 roadmap,
V3 is the current direction-setting source. Its accepted order still does not
authorize production implementation by itself.

| Consideration | Planned Approach | Rationale |
|---------------|------------------|-----------|
| **Structured activity persistence** | SQLite remains a possible approach | V2 schedules a versioned application-settings store, not Townhall or time-series persistence. |
| **Image / Asset Storage** | Hybrid: Embedded (UI icons) + File Reference (project assets) | App icons compile in. Agent avatars stored as file refs for live replacement. |
| **Plugin Architecture** | Interface + DI manual registration | Core interfaces (`IAgent`, `IPlugin`) defined when agent layer begins. .NET 10 Keyed Services for plugin DI later. |
| **Multi-provider agent architecture** | `IAgentProvider` abstraction + `AgentRegistry` service managing N agents, each with its own provider/model configuration | Still deferred beyond the first Phase 5 execution slice. Phase 5 now plans only one minimal direct-execution path to one configured OpenAI-compatible endpoint; broad provider abstraction remains unnecessary until later multi-provider or richer agent-execution work appears. Phase 4.1 already reserved `SourceProvider`/`SourceModel`/`ThreadId` on the Townhall activity entry model so that later work does not force a breaking schema change. See `docs/phases/v1/phase-4.1/IMPLEMENTATION_PLAN.md` and `docs/phases/v1/phase-5.3/IMPLEMENTATION_PLAN.md`. |
| **Agent wire format** | Phase 5 baseline: one OpenAI-compatible, non-streaming request/response shape over built-in `HttpClient`; anything broader remains undecided | This is now intentionally decided at the Phase 5 planning level so the first direct-execution path is concrete and narrow. It is not yet a commitment to a broader provider platform, streaming protocol, or long-term multi-provider contract. |

---

*Last updated: 2026-07-18 (Refactor 6.3 M1–M9b complete; M9b at `33a1806`; ShellPanelNavigation owns nine commands while MWVM retains notifying mode state; MWVM 500 lines; automated verification green (forced build 4 pre-existing warnings / 0 errors; focused 70/70; Architecture 21/21; full suite 2284/2284), manual panel-navigation verification not run; public 346 / internal 66 / total 412 / prod C# 374 / App C# 35; FindingIds 2 unchanged; M9c next eligible and separately unauthorized)*
