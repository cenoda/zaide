# Refactor 6.3 — Lifetime ownership map (V11)

**Status:** M12 artifact staged against `93d8626` (docs-only; commit pending).
**Scope:** Exactly the 67 production DI registrations in `Program.ConfigureServices` →
`AddZaide*` registration modules.
**Verified live registration count:** **67** (all `AddSingleton`; no `AddTransient` /
`AddScoped` production registrations).

---

## DI Singleton vs semantic lifetime

Microsoft.Extensions.DependencyInjection **lifetime** (`Singleton`, `Scoped`,
`Transient`) describes **how the container caches and resolves** a registration.
Zaide registers every production service as a **DI Singleton**: one shared instance
per root `IServiceProvider` for the desktop process.

**Semantic lifetime** describes **who owns the instance in product terms** and what
event invalidates or tears down that ownership. A DI Singleton may therefore represent:

- an application-wide coordinator that lives for the process,
- workspace-scoped state that is **logically** reset when the open workspace changes
  (while the same DI instance remains),
- a projection surface that subscribes to upstream observables,
- or a **factory/host** that creates shorter-lived **Editor session** or
  **Terminal session** objects that are **not** separately registered in DI.

All 67 rows below are **DI Singleton**. Semantic lifetime may be narrower than DI
lifetime. Do not infer disposal from DI lifetime alone; use the **Owner / dispose
trigger** column, which records live-code truth only.

---

## Non-registered session types

### EditorViewModel (Editor session)

`EditorViewModel` is **not registered in DI** after Refactor 6.3 M1. Each instance
is created by `IEditorSessionFactory` (`EditorSessionFactory.Create`) and owned by
`EditorTabViewModel.OpenTabs` until `CloseTabCommand` removes the tab and calls
`Workspace.CloseDocument`. `EditorViewModel` does not implement `IDisposable`; tab
teardown is collection removal plus workspace document eviction. `EditorView`
disposes view-local subscriptions when deactivated or when the control is disposed
(`MainWindow` `FinalWindowCleanup` disposes the shared `EditorView` on window
`Closed`).

### Terminal session (not a DI registration)

`ITerminalServiceFactory` (`LinuxTerminalServiceFactory`) creates a fresh
`LinuxTerminalService` per call. `ITerminalHost` (`TerminalHost`) wraps each session
in a `TerminalViewModel` inside `TerminalTabViewModel` tabs. `TerminalHost.CloseTab`
calls `tab.Session.Dispose()`. `TerminalHost.Dispose` (invoked from
`MainWindow` `FinalWindowCleanup` on `Closed` and from `ApplicationShutdown.Run` on
desktop `Exit`) disposes every open terminal session.

---

## Application shutdown

`App.axaml.cs` wires `desktop.Exit` → `ApplicationShutdown.Run(CompositionRoot.Services)`.
`ApplicationShutdown` disposes a **fixed ordered subset** of `IDisposable` /
`IAsyncDisposable` registrations (debug session, workflow/process tree, language
session/LSP process, project context, file-tree watcher, terminal host). Registrations
not listed there rely on process exit or view/window teardown paths documented per
row. `ApplicationShutdown` is not registered in DI.

`MainWindowViewModel.Dispose` runs on `MainWindow` `WhenActivated` deactivation cleanup.
`FinalWindowCleanup` disposes the shared `EditorView` and `TerminalTabHost` on window
`Closed`.

---

## Registration inventory (67 rows)

| # | Registration (service key) | DI lifetime | Semantic lifetime | Owner / dispose trigger |
|--:|----------------------------|-------------|-------------------|-------------------------|
| 1 | `Workspace` | Singleton | Workspace | Application DI singleton (`AddZaideAppCore`). `SetProjectFromPath` updates `WorkspacePath` / `ProjectName` and raises `WorkspaceFolderChanged`. Not `IDisposable`; instance lives until process exit. |
| 2 | `ICommandRegistry` → `CommandRegistry` | Singleton | Application | Application DI singleton. Not `IDisposable`; lives until process exit. |
| 3 | `ISettingsService` → `SettingsService` | Singleton | Application | Application DI singleton. `IDisposable` drains the background writer loop on `Dispose`; `ApplicationShutdown` does not resolve this registration. Lives until process exit unless an explicit dispose path is added later. |
| 4 | `ISettingsPanelFactory` → `SettingsPanelFactory` | Singleton | Application | Application DI singleton factory. Each settings open constructs a new `SettingsViewModel` + `SettingsPanelView` (not DI-registered). Factory instance not `IDisposable`. |
| 5 | `ISecretStore` → `FileSecretStore` | Singleton | Application | Application DI singleton. `IDisposable.Dispose()` is empty; lives until process exit. |
| 6 | `StatusBarViewModel` | Singleton | Application | Application DI singleton shell projection. `IDisposable` disposes its subscriptions, but no live `ApplicationShutdown` or window-cleanup path invokes it; wired from `MainWindow` and otherwise lives until process exit. |
| 7 | `IFileService` → `FileService` | Singleton | Application | Application DI singleton stateless file I/O. Not `IDisposable`. |
| 8 | `IEditorSessionFactory` → `EditorSessionFactory` | Singleton | Application | Application DI singleton factory. `Create(Document)` constructs **Editor session** `EditorViewModel` instances (not registered in DI). Factory not `IDisposable`. |
| 9 | `IEditorReadOnlyTabService` → `EditorReadOnlyTabService` | Singleton | Application | Application DI singleton editor gateway. Opens/updates read-only tabs via `IEditorSessionFactory` + `EditorTabViewModel`; not `IDisposable`. |
| 10 | `EditorSearchViewModel` | Singleton | Application | Application DI singleton search/replace chrome. `MainWindow` sets `ActiveDocument` on tab switch. Not `IDisposable`. |
| 11 | `EditorTabViewModel` | Singleton | Application | Application DI singleton tab manager. Owns `OpenTabs`; `CloseTabCommand` → `CloseTabAsync` removes tab and `Workspace.CloseDocument`. Not `IDisposable`. |
| 12 | `EditorLanguageInputViewModel` | Singleton | Application | Application DI singleton language-input router (completion/hover/navigation commands). Not `IDisposable`. |
| 13 | `ITerminalServiceFactory` → `LinuxTerminalServiceFactory` | Singleton | Application | Application DI singleton factory. `Create()` returns a new **Terminal session** `LinuxTerminalService` per call (not DI-registered). Factory not `IDisposable`. |
| 14 | `ITerminalHost` → `TerminalHost` | Singleton | Application | Application DI singleton terminal tab host. Owns `TerminalTabViewModel` tabs; `CloseTab` disposes `TerminalViewModel` session; `Dispose` disposes all sessions. Also disposed via `MainWindow` `FinalWindowCleanup` (`Closed`) and `ApplicationShutdown.Run`. |
| 15 | `IAgentPanelHost` → `AgentPanelHost` | Singleton | Application | Application DI singleton agent panel host. `MainWindow` calls `DetachHost` on `WhenActivated` cleanup. Not `IDisposable`. |
| 16 | `IAgentExecutionService` → `AgentExecutionService` | Singleton | Application | Application DI singleton HTTP agent execution. Not `IDisposable`. |
| 17 | `IAgentExecutionCoordinator` → `AgentExecutionCoordinator` | Singleton | Application | Application DI singleton coordinator. Not `IDisposable`. |
| 18 | `MentionParser` | Singleton | Application | Application DI singleton stateless parser. Not `IDisposable`. |
| 19 | `IAgentRouter` → `AgentRouter` | Singleton | Application | Application DI singleton routing façade. Not `IDisposable`. |
| 20 | `HttpClient` | Singleton | Application | Application DI singleton shared client (`AddZaideAgents`). No explicit dispose on desktop `Exit`; lives until process exit. |
| 21 | `IFileTreeService` → `FileTreeService` | Singleton | Workspace | Application DI singleton. `StartWatching` / `StopWatching` follow the opened folder; `Dispose` stops watcher and completes `WhenWatcherRestarted`. Disposed via `ApplicationShutdown.Run`. |
| 22 | `IScheduler` → AvaloniaScheduler | Singleton | Application | Application DI singleton (`ReactiveUI.Avalonia.AvaloniaScheduler.Instance`). Not `IDisposable`. |
| 23 | `FileTreeViewModel` | Singleton | Projection | Application DI singleton projecting `IFileTreeService` into the explorer tree. `IDisposable` disposes watcher/restart subscriptions; not listed in `ApplicationShutdown`. |
| 24 | `MainWindowViewModel` | Singleton | Application | Application DI singleton shell hub. `Dispose` on `MainWindow` `WhenActivated` cleanup disposes `CompositeDisposable` activation subscriptions. |
| 25 | `CommandPaletteViewModel` | Singleton | Application | Application DI singleton palette state. Not `IDisposable`. |
| 26 | `TownhallState` | Singleton | Application | Application DI singleton in-memory townhall state bag. Not `IDisposable`. |
| 27 | `TownhallViewModel` | Singleton | Projection | Application DI singleton projecting `TownhallState` into the townhall surface. Not `IDisposable`. |
| 28 | `SourceControlViewModel` | Singleton | Projection | Application DI singleton projecting git snapshots from `ISourceControlSnapshotOrchestrator` / mutation seams. Not `IDisposable`. |
| 29 | `IGitRepositoryService` → `GitRepositoryService` | Singleton | Application | Application DI singleton stateless LibGit2Sharp read seam. Not `IDisposable`. |
| 30 | `ISourceControlSnapshotOrchestrator` → orchestrator | Singleton | Application | Application DI singleton (`SourceControlSnapshotOrchestrator`). Stateless refresh orchestration; not `IDisposable`. |
| 31 | `IFileDiffService` → `FileDiffService` | Singleton | Application | Application DI singleton diff computation. Not `IDisposable`. |
| 32 | `ISourceControlDiffTabService` → `SourceControlDiffTabService` | Singleton | Application | Application DI singleton; opens diff tabs through `IEditorReadOnlyTabService` / editor gateway. Not `IDisposable`. |
| 33 | `IGitMutationService` → `GitMutationService` | Singleton | Application | Application DI singleton git mutation seam. Not `IDisposable`. |
| 34 | `IProjectFileSystem` → `FileSystemProjectFileSystem` | Singleton | Application | Application DI singleton project file-system abstraction. Not `IDisposable`. |
| 35 | `IProjectDiscovery` → `ProjectDiscovery` | Singleton | Application | Application DI singleton discovery. Not `IDisposable`. |
| 36 | `IProjectContextService` → `ProjectContextService` | Singleton | Workspace | Application DI singleton authoritative `ProjectContext`. Subscribes `Workspace.WorkspaceFolderChanged`; `LoadAsync` / `UnloadAsync` / `ReloadAsync` publish context snapshots. `IDisposable` unsubscribes and completes subject; disposed via `ApplicationShutdown.Run`. |
| 37 | `IProjectOperationGate` → `ProjectOperationGate` | Singleton | Application | Application DI singleton admission/critical-section gate. `IDisposable` releases mutexes; not listed in `ApplicationShutdown`. |
| 38 | `IProjectDebugTargetResolver` → `ProjectDebugTargetResolver` | Singleton | Application | Application DI singleton debug-target resolution. Not `IDisposable`. |
| 39 | `IProjectDebugLaunchService` → `ProjectDebugLaunchService` | Singleton | Application | Application DI singleton launch handoff into debug session. Not `IDisposable`. |
| 40 | `IManagedProcessRunner` → `ManagedProcessRunner` | Singleton | Process | Application DI singleton OS process runner (build/run/test). Kills process tree on cancel; `IDisposable` via `IProjectWorkflowService.Dispose` during `ApplicationShutdown.Run` (before language teardown). |
| 41 | `IProjectWorkflowService` → `ProjectWorkflowService` | Singleton | Application | Application DI singleton workflow coordinator (owns runner admission and operation cancellation). `IDisposable` disposes context subscription and `IManagedProcessRunner`; disposed via `ApplicationShutdown.Run`. |
| 42 | `IProjectOutputService` → `ProjectOutputService` | Singleton | Projection | Application DI singleton structured output subject for the Output surface. `IDisposable` completes subscription/subject; disposed via `ApplicationShutdown.Run`. |
| 43 | `ProjectWorkflowViewModel` | Singleton | Projection | Application DI singleton Output-surface projection over `IProjectWorkflowService` / `IProjectOutputService`. `IDisposable` disposes subscriptions and `ShowOutputRequested` subject; not listed in `ApplicationShutdown`. |
| 44 | `IBuildDiagnosticsService` → `BuildDiagnosticsService` | Singleton | Projection | Application DI singleton build-diagnostics projection feeding Problems. `IDisposable`; disposed via `ApplicationShutdown.Run`. |
| 45 | `ITestResultsService` → `TestResultsService` | Singleton | Projection | Application DI singleton test-results projection. `IDisposable`; disposed via `ApplicationShutdown.Run`. |
| 46 | `TestResultsViewModel` | Singleton | Projection | Application DI singleton Test Results surface projection. `IDisposable` disposes subscriptions; not listed in `ApplicationShutdown`. |
| 47 | `ProblemsViewModel` | Singleton | Projection | Application DI singleton Problems surface projection over language/build diagnostics. `IDisposable` disposes subscriptions; not listed in `ApplicationShutdown`. |
| 48 | `ILanguageServerBinaryLocator` → `LanguageServerBinaryLocator` | Singleton | Application | Application DI singleton csharp-ls binary discovery. Not `IDisposable`. |
| 49 | `ILanguageServerSessionFactory` → `CsharpLsSessionFactory` | Singleton | Application | Application DI singleton factory for LSP server **Process** sessions (`CsharpLsSession`). Factory not `IDisposable`; sessions torn down by `ILanguageSessionService`. |
| 50 | `ILanguageSessionService` → `LanguageSessionService` | Singleton | Application | Application DI singleton LSP session owner. Reacts to `IProjectContextService.WhenChanged`; `TearDownSessionLockedAsync` shuts down LSP process; `Dispose` / `DisposeAsync` via `ApplicationShutdown.Run`. |
| 51 | `ILanguageDocumentBridge` → `LanguageDocumentBridge` | Singleton | Application | Application DI singleton document↔LSP sync bridge. `IDisposable`; disposed via `ApplicationShutdown.Run` (after language feature services, before session service). |
| 52 | `ILanguageDiagnosticsService` → `LanguageDiagnosticsService` | Singleton | Application | Application DI singleton language diagnostics publisher. `IDisposable`; disposed via `ApplicationShutdown.Run`. |
| 53 | `ILanguageCompletionService` → `LanguageCompletionService` | Singleton | Application | Application DI singleton completion feature. `IDisposable`; disposed via `ApplicationShutdown.Run`. |
| 54 | `ILanguageHoverService` → `LanguageHoverService` | Singleton | Application | Application DI singleton hover feature. `IDisposable`; disposed via `ApplicationShutdown.Run`. |
| 55 | `ILanguageNavigationService` → `LanguageNavigationService` | Singleton | Application | Application DI singleton navigation feature. `IDisposable`; disposed via `ApplicationShutdown.Run`. |
| 56 | `ILanguageSymbolService` → `LanguageSymbolService` | Singleton | Application | Application DI singleton symbol feature. `IDisposable`; disposed via `ApplicationShutdown.Run`. |
| 57 | `ILanguageFormattingService` → `LanguageFormattingService` | Singleton | Application | Application DI singleton formatting feature. `IDisposable`; disposed via `ApplicationShutdown.Run`. |
| 58 | `IDebugAdapterLocator` → `DebugAdapterLocator` | Singleton | Application | Application DI singleton netcoredbg locator. Not `IDisposable`. |
| 59 | `IDebugAdapterSessionFactory` → `DebugAdapterSessionFactory` | Singleton | Application | Application DI singleton factory for DAP adapter **Process** sessions. Factory not `IDisposable`; sessions owned by `IDebugSessionService`. |
| 60 | `DebugSessionTimeoutPolicy` | Singleton | Application | Application DI singleton timeout policy object. Not `IDisposable`. |
| 61 | `IDebugSessionService` → `DebugSessionService` | Singleton | Application | Application DI singleton debug-session owner (adapter/debuggee process tree). `IDisposable` tears down active DAP session; disposed first in `ApplicationShutdown.Run`. |
| 62 | `IBreakpointService` → `BreakpointService` | Singleton | Workspace | Application DI singleton workspace-scoped breakpoint persistence via `ISettingsService` keyed by workspace root. Not `IDisposable`; logical scope follows `IProjectContextService` / open workspace. |
| 63 | `DebugSessionViewModel` | Singleton | Projection | Application DI singleton debug command projection over `IDebugSessionService`. `IDisposable`; disposed via `ApplicationShutdown.Run`. |
| 64 | `DebugStackProjectionViewModel` | Singleton | Projection | Application DI singleton call-stack projection. `IDisposable`; disposed via `DebugPanelViewModel.Dispose` (which `ApplicationShutdown` invokes). |
| 65 | `DebugCurrentLocationViewModel` | Singleton | Projection | Application DI singleton current-location projection. `IDisposable`; disposed via `ApplicationShutdown.Run`. |
| 66 | `DebugPanelViewModel` | Singleton | Projection | Application DI singleton Debug bottom-panel projection. `IDisposable` disposes subscriptions and `StackProjection`; disposed via `ApplicationShutdown.Run`. |
| 67 | `EditorBreakpointViewModel` | Singleton | Projection | Application DI singleton editor breakpoint gutter projection. `IDisposable`; disposed via `ApplicationShutdown.Run`. |

---

## Semantic lifetime distribution

| Semantic lifetime | Count | Registration numbers |
|-------------------|------:|----------------------|
| Application | 48 | 2–12, 13–20, 22, 24–26, 29–35, 37–39, 41, 48–57, 58–61 |
| Workspace | 4 | 1, 21, 36, 62 |
| Process | 1 | 40 |
| Projection | 14 | 23, 27–28, 42–47, 63–67 |
| Editor session | 0 | *(not DI-registered; see above)* |
| Terminal session | 0 | *(not DI-registered; see above)* |

**DI lifetime:** all **67** registrations are **Singleton**.

---

## Live verification (M12 audit at `93d8626`)

| Check | Result |
|-------|--------|
| Branch | `master` |
| `HEAD` = `origin/master` | `93d8626` |
| Working tree | clean at audit start |
| M11d implementation / closeout | `133a3c1` / `93d8626` present |
| Production registration modules | 11 `AddZaide*` extensions + `Program.ConfigureServices` composition path |
| Live `AddSingleton` count (excl. logging) | **67** |
| Match to locked post-M10 inventory | **exact** (service keys, mappings, DI lifetimes) |
| `EditorViewModel` in DI | **absent** — only `IEditorSessionFactory` + `EditorTabViewModel` ownership |
| Production / test / ratchet / baseline edits | **none** (docs-only milestone) |
