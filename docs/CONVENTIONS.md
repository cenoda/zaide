# Coding Conventions

Rules so all Zaide code reads like one person wrote it.

---

## Naming

| Thing | Case | Example |
|-------|------|---------|
| Namespaces | PascalCase | `Zaide.App.Composition`, `Zaide.Features.Editor.Domain` |
| Classes / Structs | PascalCase | `AgentRouter`, `TownhallEntry` |
| Interfaces | `I` + PascalCase | `IAgent`, `IPanel` |
| Methods | PascalCase | `RouteMessage()`, `OpenDocument()` |
| Properties | PascalCase | `IsActive`, `AgentName` |
| private fields | `_camelCase` | `_agents`, `_isDisposed` |
| local vars | camelCase | `fileName`, `panelIndex` |
| Constants | PascalCase | `MaxAgentCount` |

## Files

- One class per file (exceptions: small related records/enums)
- File name = class name: `AgentRouter.cs`, `IAgent.cs`
- XAML: `Foo.axaml` + `Foo.axaml.cs` (code-behind minimal)

## Namespaces

Namespaces must match folder structure. Refactor 6.2 M1–M12 completed the
scheduled feature-first rehome inside the single `Zaide` assembly:

```
src/App/Composition/Program.cs           →  namespace Zaide.App.Composition   (6.2 M12)
src/App/Shell/MainWindowViewModel.cs     →  namespace Zaide.App.Shell         (6.2 M12)
src/UI/DesignSystem/LayoutTokens.cs      →  namespace Zaide.UI.DesignSystem   (6.2 M1)
src/Features/Settings/Domain/SettingsModel.cs
                                         →  namespace Zaide.Features.Settings.Domain  (6.2 M2)
src/Features/Workspace/Domain/Workspace.cs
                                         →  namespace Zaide.Features.Workspace.Domain  (6.2 M3)
src/Features/Editor/Domain/Document.cs
                                         →  namespace Zaide.Features.Editor.Domain  (6.2 M4)
src/Features/ProjectSystem/Domain/ProjectContext.cs
                                         →  namespace Zaide.Features.ProjectSystem.Domain  (6.2 M5a)
src/Features/Language/Application/LanguageSessionService.cs
                                         →  namespace Zaide.Features.Language.Application  (6.2 M6a)
src/Features/Language/Infrastructure/Lsp/CsharpLsSession.cs
                                         →  namespace Zaide.Features.Language.Infrastructure.Lsp  (6.2 M6b)
src/Features/Debugging/Infrastructure/Dap/NetCoreDbgAdapterSession.cs
                                         →  namespace Zaide.Features.Debugging.Infrastructure.Dap  (6.2 M7b)
src/Features/Debugging/Presentation/DebugPanelViewModel.cs
                                         →  namespace Zaide.Features.Debugging.Presentation  (6.2 M7c)
src/Features/Agents/Application/AgentRouter.cs
                                         →  namespace Zaide.Features.Agents.Application  (6.2 M11)
```

Technical-layer folders (`Models/`, `Services/`, `ViewModels/`, `Views/`) and
root composition files are empty and no longer admitted. Optional M13
(root `Infrastructure/` / `UI/Shared/`) remains unauthorized until separate
admission review. See [Architecture rules](#architecture-rules) below.

## MVVM — ReactiveUI

- **Framework:** ReactiveUI (chosen over CommunityToolkit.Mvvm for reactive pipelines).
- **ViewModels** never reference Views directly — only data binding via `WhenAnyValue`, `Bind`, `OneWayBind`.
- **Services** never reference ViewModels or Views.
- **Models** are plain data — no UI logic.
- Code-behind (`*.axaml.cs`) should be minimal — just `InitializeComponent()` if XAML exists, but prefer C# views per `DESIGN.md` Rule 1.
- **Activation:** Use `WhenActivated` for setup/teardown; dispose subscriptions with `d.Add(observable.Subscribe(...))`.
- **Commands:** All user actions via `ReactiveCommand.CreateFromTask` or `ReactiveCommand.Create`.

## Async

- Suffix async methods with `Async`: `Task OpenDocumentAsync()`
- Avoid `async void` except Avalonia event handlers
- Use `CancellationToken` on any I/O-bound method

## Nullability

- Project-wide nullable enabled (`<Nullable>enable</Nullable>`)
- Use `?` only when null is genuinely valid
- Prefer `?? throw new InvalidOperationException(...)` over `!` (null-forgiving)

## Formatting

- 4 spaces indentation
- Opening brace on new line (Allman style)
- `var` when type is obvious: `var doc = new TextDocument();`
- Explicit type when not obvious: `string path = GetPath();`

## Commit Messages

```
area: short imperative summary
```

Examples: `layout: add 3-panel grid`, `agents: implement townhall logger`, `docs: add phase-1 plan`

---

## Architecture rules

**Status:** Accepted target rules from Refactor 6.1 M0 (2026-07-16). Codified
in M1. These rules govern new design decisions and later structural work. They
do **not** authorize source movement, dependency inversion, DI cleanup, or
architecture-test code by themselves.

| Surface | Role |
|---------|------|
| This file | Canonical **detailed** enforceability rules |
| [`architecture/OVERVIEW.md`](architecture/OVERVIEW.md) | Concise architecture summary (target vs current tree) |
| [`refactor/refactor-6.1/IMPLEMENTATION_PLAN.md`](refactor/refactor-6.1/IMPLEMENTATION_PLAN.md) and [`M0_ARCHITECTURE_BASELINE.md`](refactor/refactor-6.1/M0_ARCHITECTURE_BASELINE.md) | Evidence, violation dispositions, migration record |

Do not create a separate architecture-rules document. Product-level “IDE
layer” / “Agent layer” language is not a code dependency boundary.

### Feature-first ownership taxonomy

Target source ownership (create folders only when moving currently owned types):

```text
src/
  App/
    Composition/
    Shell/
  Features/
    Editor/
    Workspace/
    Townhall/
    Agents/
    Settings/
    SourceControl/
    ProjectSystem/
    Language/
      Infrastructure/
        Lsp/
    Debugging/
      Infrastructure/
        Dap/
    Terminal/
  Infrastructure/
    FileSystem/
    Processes/
    Persistence/
  UI/
    DesignSystem/
    Shared/
```

| Module | Ownership note |
|--------|----------------|
| App / Shell | Composition root, startup, shutdown, shell layout |
| Feature folders | One truthful product owner per feature |
| `Language/Infrastructure/Lsp` | LSP transport, session, protocol, and parser types |
| `Debugging/Infrastructure/Dap` | DAP transport, adapter, session, and parser types |
| Root `Infrastructure/` | Multi-feature file-system, process, or persistence only (admission rules below) |
| `UI/DesignSystem` | Design tokens, icons, typography (`src/UI/DesignSystem`, moved in Refactor 6.2 M1) |
| `Features/Settings` | Settings domain, contracts, infrastructure, presentation (moved in Refactor 6.2 M2) |
| `Features/Workspace` | Workspace domain, file tree contracts/infrastructure/presentation (moved in Refactor 6.2 M3); `FileIconKeyResolver` parked here (R62-D04) |
| `Features/Editor` | Editor domain, contracts, infrastructure, presentation (moved in Refactor 6.2 M4); `IFileService`/`FileService` parked here (R62-D01) |
| `Features/ProjectSystem` | Project discovery, context, gate, targets, debug launch (M5a); workflow/managed process/output (M5b); build diagnostics, test results, Problems projections (M5c) |
| `Features/Language` | Language application/contracts (M6a) and LSP transport/session/parsers (M6b: `Infrastructure/Lsp`) |
| `Features/Debugging` | Debugging application/contracts (M7a) + DAP infrastructure (M7b: `Infrastructure/Dap`) + presentation (M7c) |
| `Features/SourceControl` | Source Control domain, contracts, application, infrastructure, presentation (moved in Refactor 6.2 M8); residual R61-V02/V07 edges kept allowlisted for 6.3 |
| `Features/Terminal` | Terminal contracts, application, infrastructure, presentation (moved in Refactor 6.2 M9); residual R61-V05 factory→presentation edges kept allowlisted for 6.3 |
| `Features/Townhall` | Townhall domain and presentation (moved in Refactor 6.2 M10); R61-V16 behavior preserved; conversation lifetime remains Refactor 7 |
| `Features/Agents` | Agents domain, contracts, application, infrastructure, presentation (moved in Refactor 6.2 M11); residual R61-V06 MentionParser→presentation edge kept allowlisted for 6.3; no agent-session/run types (LT02/LT03) |
| `UI/Shared` | Feature-neutral presentation primitives only (admission rules below) |

`src/Styles` has been rehomed to `src/UI/DesignSystem` (namespace
`Zaide.UI.DesignSystem`). Settings production and matching tests live under
`src/Features/Settings/` and `tests/Zaide.Tests/Features/Settings/` (namespace
`Zaide.Features.Settings.*`). Workspace production and matching tests live under
`src/Features/Workspace/` and `tests/Zaide.Tests/Features/Workspace/` (namespace
`Zaide.Features.Workspace.*`). Editor production and matching tests live under
`src/Features/Editor/` and `tests/Zaide.Tests/Features/Editor/` (namespace
`Zaide.Features.Editor.*`); `IFileService`/`FileService` remain Editor-owned
(R62-D01). ProjectSystem M5a–M5c types live under `src/Features/ProjectSystem/`
and `tests/Zaide.Tests/Features/ProjectSystem/` (namespace
`Zaide.Features.ProjectSystem.*`); `ManagedProcess*` remains ProjectSystem-owned
(R62-D02). Language application/contracts (M6a) and LSP infrastructure (M6b) live under
`src/Features/Language/{Contracts,Application,Infrastructure/Lsp}/` and
`tests/Zaide.Tests/Features/Language/` (namespace `Zaide.Features.Language.*`;
LSP types use `Zaide.Features.Language.Infrastructure.Lsp`). Debugging
application/contracts (M7a), DAP infrastructure (M7b), and presentation (M7c) live under
`src/Features/Debugging/{Contracts,Application,Infrastructure/Dap,Presentation}/` and
`tests/Zaide.Tests/Features/Debugging/` (namespace `Zaide.Features.Debugging.*`;
DAP types use `Zaide.Features.Debugging.Infrastructure.Dap`; presentation types use
`Zaide.Features.Debugging.Presentation`). Source Control (M8) lives under
`src/Features/SourceControl/{Domain,Contracts,Application,Infrastructure,Presentation}/`
and `tests/Zaide.Tests/Features/SourceControl/` (namespace
`Zaide.Features.SourceControl.*`). Terminal (M9) lives under
`src/Features/Terminal/{Contracts,Application,Infrastructure,Presentation}/`
and `tests/Zaide.Tests/Features/Terminal/` (namespace
`Zaide.Features.Terminal.*`). Townhall (M10) lives under
`src/Features/Townhall/{Domain,Presentation}/`
and `tests/Zaide.Tests/Features/Townhall/` (namespace
`Zaide.Features.Townhall.*`). Agents (M11) lives under
`src/Features/Agents/{Domain,Contracts,Application,Infrastructure,Presentation}/`
and `tests/Zaide.Tests/Features/Agents/` (namespace
`Zaide.Features.Agents.*`).
Design tokens and icons are not `UI/Shared`.
Project workflow stays under Project System even when it consumes other
features' projections. LSP is not root infrastructure; DAP is not root
infrastructure.

### Optional layers (per feature)

A feature may use a flat root or any subset of these layers. Do not create
every layer ceremonially.

| Layer | Owns |
|-------|------|
| **Domain** | Feature truth, invariants, identities, and behavior that does not require UI, storage, transport, process, or DI frameworks |
| **Application** | Use-case coordination and lifecycle-independent policies through explicit contracts |
| **Infrastructure** | Contract implementations using file systems, processes, protocols, persistence, OS APIs, or external libraries |
| **Presentation** | Views, ViewModels, reactive commands, bindings, selection, focus, drafts, filters, and other UI state |
| **Contracts** | Smallest interfaces and boundary values needed by another layer or feature — not a DTO dump |

#### Snapshots and view state

- **Snapshots** are immutable observations owned by the producing feature's
  contract or application boundary. Consumers may project them; they must not
  become the snapshot's source of truth.
- **View state** is mutable/reactive presentation state. Domain, Application,
  Infrastructure, and another feature's contracts must not consume it.

### Dependency directions

#### Allowed (within a feature)

```text
Presentation -> Application / Contracts / Domain
Infrastructure -> Application contracts / Contracts / Domain
Application -> Contracts / Domain
Contracts -> BCL and stable Domain value types only when necessary
Domain -> BCL and its own Domain only
```

#### Allowed (across features)

- A consumer may depend on the owning feature's `Contracts` or a deliberately
  exposed Application façade.
- App composition may reference concrete Infrastructure and Presentation types
  only to register, construct, activate, and shut them down.
- Shared UI may depend on feature-neutral contracts, never feature
  Infrastructure.

#### Forbidden

- Domain → Application, Infrastructure, Presentation, DI, Avalonia, ReactiveUI,
  process, protocol, or persistence implementation
- Application → Presentation or concrete Infrastructure
- Infrastructure → Presentation or ViewModels
- Models/Domain/view state consuming another technical-layer implementation
  merely to reuse a snapshot or DTO
- Any feature consuming another feature's Presentation or Infrastructure
  namespace
- Views or ViewModels resolving dependencies through `IServiceProvider`
- Production code outside App composition reading static `App.Services`
- Root shared folders accepting feature-owned types
- A dependency justified only by shared product “IDE” or “Agent” grouping

### Root-folder admission

#### `Infrastructure/`

Admit a type only when all of the following are true:

1. At least two current feature owners consume the implementation.
2. No one feature is its truthful owner.
3. The type implements a feature-neutral contract.
4. Name and dependencies describe one infrastructure capability (not generic
   helpers or “services”).
5. The architecture allowlist records consumers and approving milestone.

Feature-specific LSP and DAP code fails this test and stays under Language and
Debugging respectively.

#### `UI/Shared/`

Admit a type only when at least two current presentation owners consume it, it
owns no feature workflow or state, and it has no feature Infrastructure
dependency. `Common`, `Helpers`, `Utils`, and `Services` are not ownership
arguments. Design tokens and icons belong in `UI/DesignSystem`.

New files in either root are deny-by-default until a reviewed ownership entry
admits them.

### Visibility (public-by-exception)

- Default every production type to `internal`.
- `public` only for a proven cross-module contract, a type required by
  Avalonia/XAML activation, or an externally consumed entry point.
- Cross-feature use alone does not require `public` while features share one
  assembly; prefer `internal` plus explicit module contracts.
- Public interfaces and boundary values must be minimal and owned by
  `Contracts` or an approved application façade.
- Infrastructure implementations, parsers, policies, mappers, ViewModels, and
  Views are internal unless a framework constraint is documented.
- Do not widen visibility for tests; use existing
  `InternalsVisibleTo="Zaide.Tests"`.
- Refactor 6.1 baselines a ceiling of **348** public top-level production types
  (compiled non-nested count: **393** total / **348** public / **45** internal).
  The explicit public full-name set is frozen in
  `tests/Zaide.Tests/Architecture/PublicProductionTypeBaseline.txt` and enforced
  by architecture tests (public-by-exception). No new public type without an
  intentional baseline update in the same reviewed change; Refactor 6.3 reduces
  the explicit list.

### Lifetime vocabulary (current)

DI registration lifetime and semantic lifetime are not synonyms. Only these
current lifetimes are approved for Refactor 6.x work:

| Lifetime | Meaning | Examples |
|----------|---------|----------|
| **Application** | Root application container / window lifetime | Settings, command registry, shell composition |
| **Workspace** | Reset or replaced when the open workspace changes | `Workspace`, project context, breakpoint/repository projections |
| **Process** | One OS process or bounded process tree from start through termination/disposal | Workflow runner, LSP server process, debug adapter/debuggee |
| **Projection** | Subscribes to a source while the projection is needed | Problems, Output, Test Results, debug/status projections |
| **Editor session** | One open editor/document presentation until its tab closes | `EditorViewModel` for an opened `Document` |
| **Terminal session** | One PTY, shell process, screen, ViewModel, and tab until the tab closes | `LinuxTerminalService` + `TerminalViewModel` |

#### Explicit Refactor 7 deferrals

These V3 terms are **not** current Refactor 6.1 lifetimes. Do not introduce
them as implementation types, scopes, factories, or DI registrations in
Refactor 6.1–6.3:

| ID | Term | Gate |
|----|------|------|
| **R61-LT01** | Conversation | Refactor 7 M0 defines the owner/boundary or records a named later deferral |
| **R61-LT02** | Agent session | Refactor 7 M0 proves a concrete consumer and ownership rule first |
| **R61-LT03** | Run | Refactor 7 M0 defines the minimum existing-behavior representation or defers explicitly |

### Assembly boundary (Refactor 6.2)

Keep mechanical feature-first migration inside the existing `Zaide` assembly.
Any later assembly split needs separate evidence and approval; do not fold it
into a movement slice.

### What later refactors own

| Refactor | Owns |
|----------|------|
| **6.1** | Rules, module map, executable architecture-test baseline (M2 inventory; M3 legacy allowlist ratchet; M4 public full-name + expanded root admission; M5 documentation closeout + M0 representation matrix). Complete pending final human acceptance; next is 6.2 M0 only after acceptance. |
| **6.2** | Mechanical moves, namespaces, tests, AXAML/resources — no logic change |
| **6.3** | Dependency inversion, composition, visibility, lifetime correction |
| **7** | Agent/Conversation domain and R61-LT01–LT03 |
| **8** | Townhall/shell view extraction and UI foundation |

### Executable architecture ratchet (Refactor 6.1 M3–M4)

Tests under `tests/Zaide.Tests/Architecture/` enforce:

| Category | Milestone | Rule |
|----------|-----------|------|
| **NamespaceDirection** | M3 | Exact-file `Services → ViewModels` and `Models → Services` edges may remain only when allowlisted; no new edge file is permitted |
| **LocatorSite** | M3 | Exact production files with `IServiceProvider` / `App.Services` / resolution-call evidence may remain only when allowlisted; no new locator file (including any new View/ViewModel site) is permitted |
| **RootFolderAdmission** | M3 + M4 + 6.2 M1–M12 | **Tracked production `.cs` only** (inventory via `git ls-files` of `src/**/*.cs`). M3: tracked C# under `src/Infrastructure/` and `src/UI/Shared/` is deny-by-default (empty allowlist). Admitted top-level folders after M12: `App` (**only** `src/App/Composition/` and `src/App/Shell/`), `Features` (Settings, Workspace, Editor, ProjectSystem, Language, Debugging, SourceControl, Terminal, Townhall, Agents), `UI` (**only** `src/UI/DesignSystem/`). Technical-layer folders and `src/` root composition C# are **not** admitted. Non-C# assets (e.g. `.axaml`, `.csproj`, `app.manifest`) are **not** covered by this ratchet |
| **Public visibility** | M4 | Exact full-name baseline of 348 public types + count ceiling 393/348/45; `NEW_PUBLIC_TYPE` / `STALE_PUBLIC_BASELINE` / `VISIBILITY_BASELINE_INTEGRITY` |

**Allowlist mutation rule (M3):** add only when the entry maps to an existing M0
`R61-V##` (or a plan-documented deferred exception), live inventory already
shows the exact match key, and the same review unit updates allowlist + frozen
FindingId baseline + plan rationale; remove only in the same change that
clears the live evidence; never grow the allowlist silently to hide new debt.

**Public baseline mutation rule (M4):** update
`PublicProductionTypeBaseline.txt` only in the same reviewed change that
intentionally adds/removes/changes public production surface; tests never
auto-regenerate the file. Target feature-layout enforcement remains
Refactor 6.2. Non-C# root-asset admission policy is a separate future decision;
M4 does not invent one.

---

*Last updated: 2026-07-17 (Refactor 6.2 M12 — App Composition + Shell; scheduled migration complete)*
