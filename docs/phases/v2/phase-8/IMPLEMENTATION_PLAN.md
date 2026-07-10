# Phase 8: Core Platform and Settings — Implementation Plan

## Pre-Implementation Verification

- [x] Confirm V1 is complete: `dotnet build` 0 warnings / 0 errors, `dotnet test` 817 passed (2026-07-10)
- [x] Re-read `src/Program.cs` (DI composition root), `src/Models/Workspace.cs`,
      `src/Services/AgentExecutionOptions.cs`, `src/Services/AgentExecutionService.cs`,
      `src/Views/EditorView.cs`, `src/MainWindow.axaml.cs` to verify current seams
- [x] Audit all settings, bootstrap, command, keybinding, workspace, editor, and
      agent-execution seams against live code
- [x] Verify no settings persistence, command registry, project discovery, or secret
      store exists (all greenfield)
- [x] Confirm `System.Text.Json` is available (used by `AgentExecutionService` for HTTP)
- [x] Verify `IndentGuideRenderer` and `IndentGuideMetrics` already read
      `textView.Options.IndentationSize` — will auto-adapt to settings-driven values

## Planning Status

**Revised 2026-07-10 — review findings applied.**

This revision addresses six high-priority and five medium-priority findings from
external review. All changes are marked with **[R]** annotations in the
affected decision sections.

### Live Baseline (verified 2026-07-10)

| File | Phase 8-relevant facts |
|------|------------------------|
| `src/Program.cs` | DI composition root. All registrations inline in one lambda (lines 24-78). `AgentExecutionOptions` created via factory lambda reading `AGENT_API_URL`, `AGENT_API_KEY`, `AGENT_MODEL` env vars. No settings service, no command registry, no project context service registered. |
| `src/Models/Workspace.cs` | Combines two responsibilities: (A) document/tab ownership (`_documents` dict, `OpenDocument`, `CloseDocument`, `SetActiveDocument`, `ActiveDocument`, `Documents`) and (B) folder identity (`WorkspacePath`, `ProjectName`, `SetProjectFromPath`). **`WorkspacePath` and `ProjectName` are plain auto-properties — no change notification.** No project/solution awareness. `ProjectName` is just `Path.GetFileName(folderPath)`. |
| `src/Services/AgentExecutionOptions.cs` | Simple DTO: `BaseUrl` (default `https://api.openai.com/v1`), `ApiKey` (default empty), `Model` (default `gpt-4o-mini`). Populated from env vars in `Program.cs`. Plaintext in memory for process lifetime. |
| `src/Services/AgentExecutionService.cs` | Validates `ApiKey`, `BaseUrl`, `Model` non-empty before HTTP call. Returns structured `AgentExecutionResult`. |
| `src/Models/AgentPanelState.cs` | Per-panel UI state. Deliberately excludes provider/credential configuration. Phase 8 must NOT move endpoint/model/secret settings here. |
| `src/Views/EditorView.cs` | All font/size/whitespace values hardcoded as `static readonly` literals: font `"Cascadia Code, Consolas, monospace"`, size `14`, `ShowTabs = false`, `ShowSpaces = false`. Indent size uses AvaloniaEdit default (4). |
| `src/Views/TerminalRenderControl.cs` | Terminal font `"Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace"`, size `14`. |
| `src/Styles/TextStyles.cs` | Global text style factory. Sizes 11/12/13/15 px. Not editor-specific. |
| `src/Views/IndentGuideRenderer.cs` | Reads `textView.Options.IndentationSize` at render time — auto-adapts. |
| `src/Views/IndentGuideMetrics.cs` | Reads `textView.Options.IndentationSize` — auto-adapts. |
| `src/MainWindow.axaml.cs` | 4 window-level keybindings hardcoded in `WhenActivated`: `` Ctrl+` ``, `Ctrl+J` (toggle bottom panel), `Ctrl+S` (save), `Ctrl+O` (open folder). No settings/menu surface exists. |
| `src/Views/TerminalPanel.cs` | Inline `KeyDown` handler for `Ctrl+C`, `Ctrl+Shift+C/V`, `PageUp/Down/Home/End`, search `Enter/Escape`. |
| `src/Views/FileTreeView.cs` | Inline `Ctrl+Shift+H` toggles hidden files, `Enter` opens selected file. |
| All ViewModels | Commands are `ReactiveCommand` properties created inline in constructors. No stable identifiers, no registry, no metadata. ~30 commands across 8 ViewModels. Several commands are parameterized (e.g. `ReactiveCommand<FileTreeNode, Unit>`, `ReactiveCommand<string, bool>`, `ReactiveCommand<EditorViewModel, Unit>`). |
| `src/ViewModels/MainWindowViewModel.cs` | `Activate()` subscribes to `FileTreeViewModel.RootPath` changes → calls `Workspace.SetProjectFromPath()` → refreshes Source Control. This is the ONLY workspace-switch path. |
| `src/ViewModels/EditorTabViewModel.cs` | Uses `Workspace` for document operations only. Does not use `WorkspacePath` or `ProjectName`. |
| `src/ViewModels/SourceControlViewModel.cs` | Uses `Workspace.WorkspacePath` for git discovery. |
| `src/Views/StatusBar.cs` | Binds to `MainWindowViewModel.WorkspaceProjectName` (folder name display). |

### Truthfulness Gaps Discovered

1. **No settings exist.** Every "preference" is a hardcoded literal in View code.
   There is no `SettingsService`, no settings file, no migration path, no defaults
   registry. This is entirely greenfield.

2. **`Workspace` conflates document ownership with folder identity.**
   `EditorTabViewModel` uses `Workspace` purely as a document store.
   `SourceControlViewModel` uses `Workspace.WorkspacePath` as a git discovery proxy.
   `MainWindowViewModel` uses `Workspace` for folder identity. These are three
   distinct concerns in one class.

3. **No project/solution discovery exists.** The only "project" concept is
   `Workspace.ProjectName` which is `Path.GetFileName(folderPath)`. There is no
   `.sln`/`.csproj` scanning, no project selection, no project context service.

4. **Commands have no identifiers or registry.** Each command is an anonymous
   `ReactiveCommand` property. No way to enumerate, look up, or rebind commands.
   Several commands are parameterized and cannot be executed by ID alone.

5. **Keybindings are scattered.** 4 in `MainWindow.axaml.cs`, 5+ inline `KeyDown`
   handlers in views. No central resolution, no conflict detection, no user override.

6. **API key is plaintext in memory.** `AgentExecutionOptions.ApiKey` is a plain
   `string` property on a singleton. No secret boundary exists.

7. **`Workspace.WorkspacePath` has no change notification.** It is a plain
   auto-property. `SetProjectFromPath()` mutates it without raising any event.
   Downstream consumers (currently `SourceControlViewModel`, future
   `IProjectContextService`) rely on `MainWindowViewModel`'s subscription to
   `FileTreeViewModel.RootPath` — not on `Workspace` itself — to learn about
   workspace changes.

## Sub-Phase Decision (M0)

**Phase 8 is split into three separately planned sub-phases.** Each area is a
distinct concern with its own testing surface, and later sub-phases depend on
earlier ones.

| Sub-phase | Scope | Dependency |
|-----------|-------|------------|
| **Phase 8.1** | Settings foundation: `SettingsService`, persistence, migration, atomic writes, recovery, secret store, editor settings (code + prose + terminal fonts), terminal font settings, LLM settings migration, settings UI, `WorkspaceFolderChanged` event, `IProjectContextService` registration stubs | None |
| **Phase 8.2** | Command registry + keybindings: `ICommandRegistry`, command descriptors, default keybindings, user overrides, conflict handling | Consumes `ISettingsService` for user keybinding overrides |
| **Phase 8.3** | Authoritative project context: `IProjectContextService`, solution/project discovery, selection, load/unload/reload lifecycle, observable state | Consumes `ISettingsService`; consumes `Workspace.WorkspaceFolderChanged` event (added in 8.1) |

Each sub-phase gets its own `IMPLEMENTATION_PLAN.md` under
`docs/phases/v2/phase-8/` (e.g. `phase-8.1/IMPLEMENTATION_PLAN.md`) following
the V1 convention. This umbrella plan locks the cross-cutting decisions that all
sub-phases share.

## Goal

Remove all user-facing hardcoding and establish the shared platform infrastructure
(settings, commands, project context) that Phases 9–13 consume. After Phase 8,
no font/size/whitespace value is a literal in View code, no keybinding is
hardcoded in a view, no API key is plaintext in settings, and one authoritative
project context service is the single source of project truth.

## Boundaries

- API keys must not be written as plaintext into the ordinary settings file.
- This phase does not build a broad multi-provider agent platform.
- This phase does not implement Command Palette UI, LSP, build execution, or
  debugging.
- Phase 8 owns project-context discovery and selection, but does not perform
  language-server semantic analysis or execute project targets.
- Structured Townhall or activity-history persistence is not implied by the
  application-settings store.
- Multi-cursor editing is deferred beyond V2.
- OS-native keychain integration is not required; a file-based secret boundary
  with restricted permissions satisfies the plaintext constraint.

## Key Decisions

### D1: Settings Persistence Format and Location

**Decision:** JSON file at `{XDG_CONFIG_HOME}/zaide/settings.json`
(`~/.config/zaide/settings.json` on Linux). Use `System.Text.Json` (already in
the dependency graph via `AgentExecutionService`).

**Schema versioning:** Top-level `"schemaVersion": N` integer field. The initial
schema version is **1**. Since settings are greenfield (no prior schema exists),
there is no legacy migration to write in Phase 8.1. The first migration
(v1 → v2) will be written when a future phase introduces a schema change.

| Alternative | Rejected Because |
|-------------|------------------|
| `Microsoft.Extensions.Configuration` / `IOptions<T>` | Adds a dependency tree we don't need. We want explicit load/save/migrate control, not the ASP.NET configuration pipeline. |
| SQLite for settings | Overkill for key-value settings. SQLite is already catalogued for time-series data (townhall logs). |
| INI / TOML | No standard library in .NET. Adds a dependency for no structural benefit over JSON. |
| *Chosen: `System.Text.Json` with versioned schema* | Zero new dependencies. Explicit control over serialization, migration, and atomic writes. |

### D2: Atomic Writes and Recovery

**Decision:** Write-to-temp-then-rename pattern.

1. Serialize settings to `{settingsPath}.tmp` in the same directory.
2. `File.Move(tmpPath, settingsPath, overwrite: true)` — atomic on POSIX when
   source and destination are on the same filesystem.
3. On successful load, copy `settings.json` → `settings.json.lastknowngood`.
4. On load failure (corrupt, partial write):
   - Attempt to load `settings.json.lastknowngood`.
   - If that also fails, fall back to hardcoded defaults.
   - Log the failure.

**Interrupted write recovery:** If the process crashes between writing `.tmp`
and renaming, the `.tmp` file is orphaned but `settings.json` is untouched.
Next load succeeds with the existing file. If the crash happens during
`File.Move`, POSIX guarantees the rename is atomic, so either the old or new
file is present.

### D3: Settings Migration

**Decision:** Ordered migration functions keyed by `schemaVersion`.

```csharp
// Each migration: (int fromVersion) → (int toVersion)
// Migrations run in order: 1→2, 2→3, etc.
// Unknown future version (file version > current max): refuse to load,
// do NOT overwrite. Surface error to user.
```

- Migration runs on load, before deserialization into the current model.
- Each migration is a pure function: `JsonElement → JsonElement`.
- Unknown future version → `SettingsLoadResult.UnsupportedVersion(int foundVersion)`.
  The service does NOT write to the file. The user is shown an error.
- Corrupt JSON → `SettingsLoadResult.Corrupt`. Fall back to last-known-good or
  defaults.
- Missing file → `SettingsLoadResult.Missing`. Use defaults. File is created on
  first save.

**[R] Greenfield clarification:** The initial schema is version 1. There is no
pre-existing schema to migrate from. Phase 8.1 does not include any migration
functions — the migration infrastructure (registry, runner, version check) is
built, but the migration list is empty. The first migration (v1 → v2) will be
written when a later phase changes the schema. Tests verify the migration
infrastructure by registering a synthetic test migration (v1 → v2) in the test
project, not by migrating real legacy data.

### D4: Secret-Handling Boundary

**Decision:** Separate file at `{XDG_CONFIG_HOME}/zaide/secrets.json` with
`0600` permissions (owner read/write only on Linux).

- `ISecretStore` interface: `string? Get(string key)`, `void Set(string key, string value)`,
  `void Delete(string key)`.
- `FileSecretStore` implementation: reads/writes `secrets.json`. On first write,
  creates the file with `0600` permissions via `File.SetUnixFileMode`.
- Secrets are NEVER written to `settings.json`. The settings file may contain a
  reference like `"apiKeySource": "secret-store"` but never the value itself.
- Environment-variable fallback preserved: `AGENT_API_KEY` env var takes
  precedence over the secret store (matching the existing behavior).
- Precedence order: environment variable → secret store → empty (user must
  configure).

**[R] Async/sync resolution:** The secret store is **synchronous**. File I/O for
a small JSON file (typically < 1 KB) is fast enough that async provides no
benefit for a desktop app's startup path. The existing `AgentExecutionOptions`
factory in `Program.cs` is synchronous — making `ISecretStore` async would
require restructuring DI composition for no practical gain. If OS keychain
integration is adopted later (swapping the `ISecretStore` implementation), the
interface can be made async at that point with a documented breaking change.

The `AgentExecutionOptions` factory in `Program.cs` becomes:

```csharp
services.AddSingleton<AgentExecutionOptions>(sp =>
{
    var settings = sp.GetRequiredService<ISettingsService>();
    var secrets = sp.GetRequiredService<ISecretStore>();
    var options = new AgentExecutionOptions();

    // Settings file values (lowest precedence after defaults)
    options.BaseUrl = settings.Current.Llm.BaseUrl;
    options.Model = settings.Current.Llm.Model;

    // Secret store (middle precedence)
    var storedKey = secrets.Get("llm.apiKey");
    if (!string.IsNullOrEmpty(storedKey))
        options.ApiKey = storedKey;

    // Environment variables (highest precedence — preserved from V1)
    if (Environment.GetEnvironmentVariable("AGENT_API_URL") is { Length: > 0 } url)
        options.BaseUrl = url;
    if (Environment.GetEnvironmentVariable("AGENT_API_KEY") is { Length: > 0 } key)
        options.ApiKey = key;
    if (Environment.GetEnvironmentVariable("AGENT_MODEL") is { Length: > 0 } model)
        options.Model = model;

    return options;
});
```

This preserves the existing synchronous DI composition. The `ISettingsService`
must complete its initial load synchronously before the factory runs — see D4a.

### D4a: Settings Service Initialization **[R]**

**Decision:** `ISettingsService` loads synchronously during construction. The
constructor reads the settings file (or falls back to defaults) before returning.
This ensures the service is fully initialized when resolved from DI, and the
`AgentExecutionOptions` factory can read `settings.Current` without awaiting.

- `ISettingsService.Current` returns the loaded `SettingsModel` (never null).
- `ISettingsService.SaveAsync(CancellationToken ct = default)` is async because it
  writes to disk. Accepts optional cancellation token.
- `ISettingsService.LoadResult` exposes the `SettingsLoadResult` enum so the UI
  can surface errors (unsupported version, corruption) after startup.
- The constructor performs the load. If the file is missing, defaults are used.
  If the file is corrupt, last-known-good or defaults are used. If the file has
  an unknown future version, defaults are used and `LoadResult` records the
  error. The constructor never throws.

### D5: Command Registry Architecture

**Decision:** `ICommandRegistry` singleton service. ViewModels register commands
in their constructors with stable string identifiers.

```csharp
public sealed class CommandDescriptor
{
    public string Id { get; }           // e.g. "file.save", "explorer.toggleHiddenFiles"
    public string DisplayName { get; }  // e.g. "Save", "Toggle Hidden Files"
    public string Category { get; }     // e.g. "File", "Explorer"
    public KeyGesture? DefaultKeyGesture { get; }  // nullable — not all commands have defaults
    public ICommand Command { get; }    // the live ReactiveCommand instance
}
```

**[R] Full interface contract:**

```csharp
public interface ICommandRegistry
{
    /// <summary>Register a command with a stable identifier.</summary>
    void Register(CommandDescriptor descriptor);

    /// <summary>All registered commands.</summary>
    IReadOnlyList<CommandDescriptor> GetAll();

    /// <summary>Look up a command by ID, or null if not registered.</summary>
    CommandDescriptor? GetById(string id);

    /// <summary>
    /// Execute a parameterless command by ID.
    /// Internally calls ICommand.Execute(null).
    /// Returns false if command ID is not found, or if the underlying
    /// command requires a typed parameter (CanExecute(null) returns false).
    /// Does not attempt type coercion or parameter inference.
    /// </summary>
    bool Execute(string id);

    /// <summary>
    /// Execute a parameterized command by ID with the given parameter.
    /// Returns false if command ID is not found.
    /// </summary>
    bool Execute<T>(string id, T parameter);

    /// <summary>
    /// Materialize the gesture -> command map from registered defaults
    /// and user overrides (stored in settings). Returns the resolved
    /// list of KeyBinding instances. Called once by MainWindow during
    /// activation (WhenActivated). Overrides take absolute precedence;
    /// conflicts resolved by lexicographic command ID (deterministic).
    /// </summary>
    IReadOnlyList<KeyBinding> ResolveKeyBindings(ISettingsService settings);
}
```

- Only parameterless or `Unit`-parameterized commands that make sense as global
  actions are registered (save, open folder, toggle panel, refresh, commit).
- Parameterized commands operating on specific items (stage file, copy path,
  open file per node) remain ViewModel-local and are NOT registered.

**Which commands are parameterless vs. parameterized:**

| Command | Parameter | Registry execution |
|---------|-----------|--------------------|
| `MainWindowViewModel.SaveActiveTabCommand` | `Unit` | `Execute("file.save")` |
| `MainWindowViewModel.OpenFolderCommand` | `Unit` | `Execute("workspace.openFolder")` |
| `MainWindowViewModel.ToggleBottomPanelCommand` | `Unit` | `Execute("view.toggleBottomPanel")` |
| `FileTreeViewModel.OpenFolderCommand` | `string` | Not in registry — internal to file tree |
| `FileTreeViewModel.ToggleHiddenFilesCommand` | `Unit` | `Execute("explorer.toggleHiddenFiles")` |
| `FileTreeViewModel.CopyPathCommand` | `FileTreeNode` | Not in registry — context-menu only |
| `EditorViewModel.SaveCommand` | `Unit` | Not in registry — invoked via `SaveActiveTabCommand` |
| `SourceControlViewModel.StageFileCommand` | `FileChange` | Not in registry — per-item action |
| `SourceControlViewModel.CommitCommand` | `Unit` | `Execute("sourcecontrol.commit")` |
| `SourceControlViewModel.RefreshCommand` | `Unit` | `Execute("sourcecontrol.refresh")` |

**Parameterized commands that operate on specific items** (stage file, copy path,
open file from tree) are NOT registered for global execution. They remain
ViewModel-local. Only commands that make sense as global actions (save, open
folder, toggle panel, refresh, commit) are registered with stable IDs and
keybindings.

### D6: Keybinding Resolution and Conflict Policy

**Decision:** Three-layer resolution: user override → default gesture → unbound.

1. `settings.json` may contain a `"keybindings"` section:
   `{ "file.save": "Ctrl+Shift+S" }` — user override.
2. If no user override, the command's `DefaultKeyGesture` is used.
3. If no default gesture, the command is unbound (invokable via Command Palette
   in Phase 9, but not via keyboard).

**[R] Conflict policy (deterministic):**

- **Build time:** The registry materializes a gesture → command map once, after
  all ViewModels have registered. Materialization happens when
  `ICommandRegistry.ResolveKeyBindings()` is called by `MainWindow` during
  activation.
- **User overrides take absolute precedence.** The override map is applied first.
  If two user overrides map to the same gesture, the one with the
  lexicographically earlier command ID wins. A warning is logged for the loser.
- **Default gestures fill remaining slots.** Only commands without a user
  override contribute their default gesture. If two default gestures collide,
  the one with the lexicographically earlier command ID wins. A warning is
  logged.
- **No gesture is ever assigned to two commands.** The winning command gets the
  `KeyBinding`; the losing command is unbound for that gesture.
- **No conflict-resolution UI in Phase 8.** Conflicts are logged. Phase 9
  (Command Palette) may surface them for user resolution.

**Rationale for deterministic policy:** The previous version said "later
registration wins" in one place and "first registration wins" in another. This
revision uses a single deterministic rule (lexicographic command ID) that does
not depend on registration order.

### D7: Workspace Migration Strategy

**Decision:** `Workspace` retains document/tab ownership. A new
`IProjectContextService` becomes the authoritative project/solution context.

- `Workspace` keeps: `_documents`, `OpenDocument`, `CloseDocument`,
  `SetActiveDocument`, `ActiveDocument`, `Documents`.
- `Workspace` keeps: `WorkspacePath`, `ProjectName`, `SetProjectFromPath` —
  these represent the opened folder, not the project.

**[R] Workspace change notification:** `Workspace.WorkspacePath` is currently a
plain auto-property with no change notification. `IProjectContextService` needs
to know when the workspace folder changes. Rather than converting `Workspace` to
a reactive model (which would affect all consumers), Phase 8.1 adds a single
event to `Workspace`, built and owned in Phase 8.1:

```csharp
// Added to Workspace.cs (Phase 8.1)
public event EventHandler? WorkspaceFolderChanged;
```

- `SetProjectFromPath()` raises `WorkspaceFolderChanged` after updating
  `WorkspacePath` and `ProjectName`.
- `IProjectContextService` (Phase 8.3) subscribes to this event in its
  constructor and calls `LoadAsync` in the handler.
- **Subscription disposal:** The service implements `IDisposable`. It stores the
  subscription token and unsubscribes on `Dispose()`. Since the service is a
  singleton, disposal happens when the service provider is disposed (app exit),
  matching the process-lifetime scope.
- `MainWindowViewModel`'s existing subscription to `FileTreeViewModel.RootPath`
  remains unchanged — it continues to call `Workspace.SetProjectFromPath()`,
  which now notifies all subscribers.
- This is a minimal, additive change. No existing `Workspace` consumer breaks.
- In 8.3's `IProjectContextService.ctor`, the subscription pattern is:

```csharp
public sealed class ProjectContextService : IProjectContextService, IDisposable
{
    private readonly Workspace _workspace;
    private EventHandler? _handler;

    public ProjectContextService(Workspace workspace /*, ... */)
    {
        _workspace = workspace;
        _handler = (_, _) => _ = LoadAsync(workspace.WorkspacePath!);
        _workspace.WorkspaceFolderChanged += _handler;
    }

    public void Dispose()
    {
        if (_handler != null)
            _workspace.WorkspaceFolderChanged -= _handler;
    }
}
```

Downstream consumers migrate gradually:
- `SourceControlViewModel` currently uses `Workspace.WorkspacePath` for git
  discovery. This remains valid — git discovery starts from the folder path,
  not from a project file. No migration needed in Phase 8.
- Phase 10 (LSP) and Phase 11 (Build/Run/Test) will consume
  `IProjectContextService` for project-aware operations.

### D8: Project Context Service Architecture

**Decision:** `IProjectContextService` singleton with observable state.

```csharp
public enum ProjectContextState
{
    Unloaded,       // Service initialized but no workspace opened yet
    Loading,        // Discovery in progress (async scan)
    NoProject,      // Folder open, no .sln/.slnx/.csproj found
    Unsupported,    // Folder open, files found but none are a supported type
    SingleProject,  // Exactly one supported file found — auto-selected
    Ambiguous,      // Multiple found — user must select
    Selected,       // User has selected a specific project
    Failed          // Discovery failed (IO error, permission denied, etc.)
}

public sealed class ProjectContext
{
    public ProjectContextState State { get; }
    public string? WorkspaceRoot { get; }
    public IReadOnlyList<ProjectCandidate> Candidates { get; }
    public ProjectCandidate? SelectedProject { get; }
    public string? ErrorMessage { get; }  // Set when State == Failed
}

public sealed class ProjectCandidate
{
    public string FilePath { get; }      // Full path to .sln/.slnx/.csproj
    public string DisplayName { get; }   // File name without extension
    public ProjectKind Kind { get; }     // Solution, SolutionX, or CSharpProject
}

public enum ProjectKind { Solution, SolutionX, CSharpProject }
```

**[R] Lifecycle operations with cancellation:**

```csharp
public interface IProjectContextService
{
    // Observable state
    ProjectContext Current { get; }
    IObservable<ProjectContext> WhenChanged { get; }

    // Lifecycle
    Task LoadAsync(string workspaceRoot, CancellationToken ct = default);
    Task UnloadAsync(CancellationToken ct = default);
    Task ReloadAsync(CancellationToken ct = default);

    // Selection
    void SelectProject(ProjectCandidate? candidate);  // null = clear selection
}
```

- `LoadAsync(workspaceRoot, ct)` transitions: `Unloaded`/`NoProject` → `Loading` →
  `NoProject`/`Unsupported`/`SingleProject`/`Ambiguous`/`Failed`.
- `UnloadAsync(ct)` transitions to `Unloaded`, clears candidates and selection.
- `ReloadAsync(ct)` re-scans the current workspace root. Transitions to `Loading`
  first, then to the appropriate result state.

**[R] Stale-load handling (rapid workspace switches):** When the user opens a
folder, closes it, and opens another in quick succession, multiple
`LoadAsync` calls may overlap. The service tracks a monotonically increasing
sequence number per `LoadAsync` call. When a discovery completes, the service
compares its sequence number against the current sequence. If a newer
`LoadAsync` has been issued since this discovery started, the result is
discarded — the `ProjectContext` is not updated, no `WhenChanged` event fires.
Only the most recently started discovery updates state. This prevents stale
results from overwriting fresh ones.

**Sequence-number pattern:**

```csharp
private int _discoverySeq;  // Incremented on each LoadAsync/ReloadAsync call

async Task LoadAsync(string workspaceRoot, CancellationToken ct)
{
    State = Loading;
    NotifyChanged();
    int capturedSeq = ++_discoverySeq;
    try
    {
        var result = await DiscoverAsync(workspaceRoot, ct).ConfigureAwait(true);
        if (_discoverySeq != capturedSeq) return;  // Stale — discard
        ApplyDiscoveryResult(result);
    }
    catch (OperationCanceledException)
    {
        if (_discoverySeq == capturedSeq)
            State = Failed;  // Only set Failed if this is still the active request
    }
}
```

- `Workspace.WorkspaceFolderChanged` triggers `LoadAsync` automatically.
- `Unsupported` state: the folder contains files with project-like extensions
  but none that Zaide can handle (e.g. `.vbproj`, `.fsproj`). This satisfies the
  V2 roadmap's "structured unsupported result" requirement.
- `Failed` state: IO error or cancellation during scan. `ErrorMessage` contains
  the reason.

- Discovery: scan the root folder (non-recursive for Phase 8 — only the root
  level) for files matching the classification table below.
- No-project and ambiguous results are structured, not thrown.

**[R] Extension classification for discovery:**

| Category | Extensions | State |
|----------|------------|-------|
| **Supported** | `.sln`, `.slnx`, `.csproj` | Included in `Candidates`. `State` = `SingleProject`, `Ambiguous`, or `Selected`. |
| **Unsupported** | `.vbproj`, `.fsproj`, `.vcxproj`, `.pyproj`, `.dbproj`, `.wixproj`, `.shproj` | NOT included in `Candidates`. `State` = `Unsupported`. The `ErrorMessage` says "Found {ext} projects which are not yet supported." This satisfies the V2 roadmap's "structured unsupported result" requirement. |
| **Unrelated** | All other files | Ignored. |

- If the folder contains only supported files → normal single/ambiguous result.
- If the folder contains only unsupported files → `Unsupported` state.
- If the folder contains both supported and unsupported files → supported files
  appear in `Candidates`; unsupported are reported in `ErrorMessage` but do not
  block selection. The service returns `SingleProject`/`Ambiguous` for the
  supported subset.
- If the folder contains no project-like files at all → `NoProject` state.
- `Unsupported` is distinct from `Failed` (IO error) and `NoProject` (nothing
  found). The V2 roadmap requires all three to be structured.
- Phase 8 does NOT parse solution/project contents. It discovers files and
  exposes them as candidates. Parsing belongs to Phase 10 (LSP) and Phase 11
  (Build/Run/Test).

### D9: Settings UI Scope and Integration **[R]**

**Decision:** Settings UI is part of Phase 8.1. It covers:
- Editor defaults (code font family, prose font family, code font size, tab size,
  show whitespace, indent style)
- Terminal font family and font size
- LLM configuration (endpoint URL, model, API key via secret store)
- Keybinding overrides (read-only list in 8.1; editing in 8.2 when command
  registry exists)

**[R] Font settings model:**

| Setting | Model field | Consumed by | Current hardcoded default |
|---------|-------------|-------------|--------------------------|
| `editor.codeFontFamily` | `EditorSettings.CodeFontFamily` | `EditorView` code text | `"Cascadia Code, Consolas, monospace"` |
| `editor.codeFontSize` | `EditorSettings.CodeFontSize` | `EditorView` code text | `14` |
| `editor.proseFontFamily` | `EditorSettings.ProseFontFamily` | `EditorView` prose/markdown text | `"Georgia, serif"` |
| `editor.terminalFontFamily` | `EditorSettings.TerminalFontFamily` | `TerminalRenderControl` | `"Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace"` |
| `editor.terminalFontSize` | `EditorSettings.TerminalFontSize` | `TerminalRenderControl` | `14` |

The editor and terminal share the settings file but have separate font values.
They do NOT share a single `fontFamily` setting — each surface has different
typographic requirements (code needs monospace, prose needs serif, terminal
needs a monospace with character-cell metrics).

The `EditorSettingsModel` in the JSON schema:

```json
"editor": {
  "codeFontFamily": "Cascadia Code, Consolas, monospace",
  "codeFontSize": 14,
  "proseFontFamily": "Georgia, serif",
  "terminalFontFamily": "Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace",
  "terminalFontSize": 14,
  "tabSize": 4,
  "insertSpaces": true,
  "showWhitespace": false,
  "showTabs": false,
  "showSpaces": false
}
```

**Integration point:** The current `MainWindow` has no menu bar or settings
entry point. Phase 8.1 adds a settings entry via:
- A gear icon button in the `StatusBar` (the status bar already exists and is
  always visible). Clicking it opens the settings panel.
- The settings panel is a slide-over panel (similar to the existing bottom panel
  toggle pattern) that overlays the main content.
- `MainWindow.axaml.cs` is the integration file — it hosts the settings button
  wiring and panel visibility toggle.
- No menu bar is added. A menu bar is unnecessary for a single entry point and
  would be over-engineering.

The settings UI follows existing panel patterns (C# view construction per
DESIGN.md Rule 1).

### D10: Keybinding Exception List **[R]**

Not all keyboard handling in views is a "global command keybinding." The
following are explicitly classified as **non-command keybindings** that remain
in view code and are NOT registered in the command registry:

| View | Key handling | Reason for exception |
|------|-------------|---------------------|
| `TerminalPanel` | `Ctrl+C` (copy), `Ctrl+Shift+C/V` (copy/paste) | Terminal transport keys — intercepted before command system. The terminal PTY owns these. |
| `TerminalPanel` | `PageUp/Down/Home/End` | Terminal viewport scrolling — terminal-internal navigation. |
| `TerminalPanel` | Search box `Enter`/`Escape` | Text-input context — scoped to search box focus. |
| `FileTreeView` | `Enter` on selected node | Context-dependent: opens file or confirms rename. Requires node-specific parameter. |
| `FileTreeView` | `Enter`/`Escape` during inline rename | Text-input context — scoped to rename editor focus. |
| `AgentPanelView` | `Enter` to send message | Text-input context — scoped to input field focus. |
| `TownhallInputArea` | `Enter` in input | Text-input context — scoped to input field focus. |

The following view-level keybindings ARE migrated to the command registry:

| View | Key handling | Registry command ID |
|------|-------------|---------------------|
| `FileTreeView` | `Ctrl+Shift+H` (toggle hidden files) | `explorer.toggleHiddenFiles` |

## Live Constraints To Respect

1. **`AgentExecutionOptions` env-var fallback must remain.** The V2 roadmap
   explicitly requires this. Environment variables take highest precedence.
2. **`AgentPanelState` must not gain provider/credential configuration.**
   This boundary is deliberate and must be preserved.
3. **`EditorTabViewModel`'s document operations must not break.** `Workspace`
   document ownership stays. The project context service is additive.
4. **All existing tests must continue to pass** after each sub-phase (817 as of
   2026-07-10; the exact count at closeout is recorded from `dotnet test`
   output). New services are injected via constructor — existing test helpers
   must be updated to provide mocks for new constructor parameters.
5. **DI composition is inline in `Program.cs`.** New services are registered
   inline following the existing pattern. No refactoring of the DI setup is
   in scope for Phase 8.
6. **`IndentGuideRenderer` and `IndentGuideMetrics` already read
   `textView.Options.IndentationSize`.** Editor settings must push values into
   AvaloniaEdit's options — these renderers will auto-adapt.
7. **`Workspace.WorkspacePath` is not reactive.** Phase 8 adds a
   `WorkspaceFolderChanged` event (D7). No existing consumer is broken by an
   additional event. The event is raised by `SetProjectFromPath()`.

## Milestones (Umbrella)

| Milestone | Sub-phase | Description | Verification |
|-----------|-----------|-------------|--------------|
| **M0** | Umbrella | Lock all decisions in this plan. Verify `System.Text.Json` serialization round-trip with versioned schema. Verify `File.SetUnixFileMode` works for secret file permissions. Verify atomic write pattern (write tmp → rename) on Linux. | Proof-of-concept tests in `tests/Zaide.Tests/Services/Phase8ProofOfConceptTests.cs` (5 tests: JSON round-trip with schema version, Unix file mode 0600, atomic write-rename, last-known-good fallback, future version rejection). Tests remain after M0 as regression coverage. Build + tests green. |
| **M1–M6** | 8.1 | Settings foundation: `ISettingsService`, JSON persistence, migration infrastructure, atomic writes, recovery, `ISecretStore`, editor settings (code + prose + terminal fonts), `WorkspaceFolderChanged` event, LLM settings migration, settings UI. `CancellationToken` on `SaveAsync`. | `dotnet build` + `dotnet test` green. Settings round-trip test. Migration infrastructure test (synthetic v1→v2 in test project). Atomic write test. Secret store test. Editor settings consumed by `EditorView`. Terminal font settings consumed by `TerminalRenderControl`. LLM settings consumed by `AgentExecutionOptions`. |
| **M7–M10** | 8.2 | Command registry + keybindings: `ICommandRegistry`, command descriptors, default keybindings, user overrides, window keybinding integration. | All parameterless global commands registered with stable IDs. Keybindings resolved from registry. User override test. Build + tests green. |
| **M11–M14** | 8.3 | Authoritative project context: `IProjectContextService`, discovery, selection, lifecycle, observable state, status bar integration. `CancellationToken` on `LoadAsync`/`ReloadAsync`/`UnloadAsync`. Stale-load sequence-number pattern. `IDisposable` event subscription. | Discovery finds `.sln`/`.csproj` in test fixtures. All 8 states tested (Unloaded / Loading / NoProject / Unsupported / SingleProject / Ambiguous / Selected / Failed). Stale-load sequence test (rapid LoadAsync → stale result discarded). Cancellation test. Subscription disposal test. Observable state consumed by status bar. Build + tests green. |

## Likely Implementation Shape

### New Files (across all sub-phases)

**Settings (8.1):**
- `src/Services/ISettingsService.cs` — load, save, observe settings
- `src/Services/SettingsService.cs` — JSON-backed implementation (synchronous load, async save)
- `src/Services/SettingsSchema.cs` — versioned settings model (`SettingsModel`, `EditorSettingsModel`, `LlmSettingsModel`)
- `src/Services/SettingsMigration.cs` — migration infrastructure (registry, runner; empty migration list initially)
- `src/Services/ISecretStore.cs` — secret boundary interface (synchronous)
- `src/Services/FileSecretStore.cs` — file-based implementation with `0600`
- `src/Services/EditorSettings.cs` — font, size, whitespace, indentation model (applied to AvaloniaEdit options)
- `src/ViewModels/SettingsViewModel.cs` — settings UI ViewModel
- `src/Views/SettingsView.cs` — settings UI (C# per DESIGN.md)

**Commands (8.2):**
- `src/Services/ICommandRegistry.cs` — command registry interface
- `src/Services/CommandRegistry.cs` — implementation
- `src/Services/CommandDescriptor.cs` — command metadata record

**Project Context (8.3):**
- `src/Services/IProjectContextService.cs` — project context interface
- `src/Services/ProjectContextService.cs` — implementation
- `src/Services/ProjectContext.cs` — observable state model
- `src/Services/ProjectCandidate.cs` — discovery result record
- `src/Services/ProjectDiscovery.cs` — file scanning logic

### Changes to Existing Files

| File | Sub-phase | Change |
|------|-----------|--------|
| `src/Program.cs` | All | Register new services. `AgentExecutionOptions` factory reads from settings + secret store + env vars. |
| `src/Models/Workspace.cs` | 8.1 | Add `WorkspaceFolderChanged` event. `SetProjectFromPath()` raises it after mutation. Consumed by `IProjectContextService` (8.3). |
| `src/Views/EditorView.cs` | 8.1 | Replace hardcoded font/size/whitespace literals with `ISettingsService`-driven values. |
| `src/Views/TerminalRenderControl.cs` | 8.1 | Replace hardcoded font family/size with settings-driven values. |
| `src/Services/AgentExecutionOptions.cs` | 8.1 | Factory reads from settings + secret store, env-var fallback preserved. |
| `src/MainWindow.axaml.cs` | 8.1 + 8.2 | 8.1: add settings gear button wiring + panel toggle. 8.2: replace imperative keybinding wiring with registry-driven binding. |
| `src/Views/FileTreeView.cs` | 8.2 | `Ctrl+Shift+H` handler replaced with registry command call. |
| Selectable ViewModels | 8.2 | Accept `ICommandRegistry` in constructor, register parameterless global commands with stable IDs. |
| `src/ViewModels/MainWindowViewModel.cs` | 8.3 | Inject `IProjectContextService`. `Activate()` subscribes to project context changes. |
| `src/Views/StatusBar.cs` | 8.1 + 8.3 | 8.1: add settings gear icon button. 8.3: consume project context name instead of folder name. |

### DI Registration Changes (`src/Program.cs`)

```csharp
// Phase 8.1
services.AddSingleton<ISettingsService, SettingsService>();
services.AddSingleton<ISecretStore, FileSecretStore>();

// Phase 8.2
services.AddSingleton<ICommandRegistry, CommandRegistry>();

// Phase 8.3
services.AddSingleton<IProjectContextService, ProjectContextService>();
```

### Settings JSON Shape (schema version 1)

```json
{
  "schemaVersion": 1,
  "editor": {
    "codeFontFamily": "Cascadia Code, Consolas, monospace",
    "codeFontSize": 14,
    "proseFontFamily": "Georgia, serif",
    "terminalFontFamily": "Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace",
    "terminalFontSize": 14,
    "tabSize": 4,
    "insertSpaces": true,
    "showWhitespace": false,
    "showTabs": false,
    "showSpaces": false
  },
  "llm": {
    "baseUrl": "https://api.openai.com/v1",
    "model": "gpt-4o-mini",
    "apiKeySource": "secret-store"
  },
  "keybindings": {}
}
```

### Secrets JSON Shape

```json
{
  "llm.apiKey": "sk-..."
}
```

File permissions: `0600` (owner read/write only) on Linux.

## Risk Summary

| Risk | Mitigation |
|------|------------|
| Settings file corruption crashes app on startup | Atomic writes + last-known-good fallback + defaults. Load never throws to the caller. |
| Unknown future settings version silently overwrites user config | Explicit version check: future version → refuse to load, surface error. |
| Secret file permissions not applied on non-Linux | `File.SetUnixFileMode` is Linux-only. Guard with `OperatingSystem.IsLinux()`. On Windows/macOS, create the file without restricted permissions (acceptable for V2's Linux-primary validation). Document the limitation. |
| `Workspace` split breaks existing consumers | `Workspace` does NOT split in Phase 8. Document/tab ownership and folder identity remain together. `IProjectContextService` is additive. No existing consumer is forced to change. |
| `Workspace.WorkspacePath` has no change notification | Phase 8 adds `WorkspaceFolderChanged` event (D7). Minimal additive change. No existing consumer broken. |
| Command registry injection breaks all ViewModel constructors | Only ViewModels with parameterless global commands gain `ICommandRegistry`. Parameterized commands stay ViewModel-local. Test helpers updated per sub-phase. |
| Project discovery is slow on large folders | Phase 8 discovery is non-recursive (root level only). Scanning a folder for `*.sln`/`*.csproj` is fast. Recursive discovery is not in scope. |
| `AgentExecutionOptions` env-var fallback breaks during migration | Precedence order is explicit: env var → secret store → settings default → empty. Tests cover all four cases. Synchronous DI composition preserved (D4a). |
| Settings UI has no integration point | Status bar gear icon (D9). No menu bar needed. |

## Exit Conditions

- [ ] **Settings:** Versioned settings file (schema v1) loads, saves, and recovers from corruption. Atomic writes verified. Unknown future version is refused, not overwritten. Migration infrastructure exists (empty migration list; synthetic test migration in test project). `SaveAsync` accepts `CancellationToken`.
- [ ] **Secrets:** API key is not in `settings.json`. `ISecretStore` provides get/set/delete. Env-var fallback works.
- [ ] **Editor fonts:** No font family (code or prose) or font size is a hardcoded literal in `EditorView`. Code font (`codeFontFamily`/`codeFontSize`) and prose font (`proseFontFamily`) are separate settings driven by `ISettingsService`.
- [ ] **Terminal fonts:** No font family or font size is a hardcoded literal in `TerminalRenderControl`. Terminal font (`terminalFontFamily`/`terminalFontSize`) driven by `ISettingsService`.
- [ ] **LLM:** `AgentExecutionOptions` populated from settings + secret store + env-var fallback. No plaintext API key in settings file. Synchronous DI composition preserved.
- [ ] **Commands:** All parameterless global commands registered with stable string IDs. `ICommandRegistry` provides lookup, `Execute(string id)`, and `Execute<T>(string id, T parameter)`. Parameterized commands remain ViewModel-local.
- [ ] **Keybindings:** Window keybindings driven by `ICommandRegistry.ResolveKeyBindings()`. Deterministic conflict resolution (lexicographic command ID). Exception list documented (D10).
- [ ] **Project context:** `IProjectContextService` discovers `.sln`/`.slnx`/`.csproj` in workspace root. Full 8-state lifecycle (Unloaded → Loading → NoProject/Unsupported/SingleProject/Ambiguous/Selected/Failed). Extension classification table determines supported vs. unsupported. `LoadAsync`/`ReloadAsync`/`UnloadAsync` accept `CancellationToken`. Stale-load sequence-number pattern prevents out-of-order results. `Workspace.WorkspaceFolderChanged` event drives auto-discovery with `IDisposable` subscription. Status bar consumes project context name.
- [ ] **Workspace:** `Workspace` document ownership unchanged. `WorkspaceFolderChanged` event added. Event raised by `SetProjectFromPath()`. `IProjectContextService` is additive. No parallel sources of project truth.
- [ ] **Settings UI:** Accessible via status bar gear icon. Editor and LLM settings editable.
- [ ] **Tests:** All pre-existing tests pass. New tests cover settings, secrets, commands, keybindings, and project context. Test count at closeout recorded from `dotnet test` output (baseline: 817 as of 2026-07-10).
- [ ] **Build:** `dotnet build Zaide.slnx --no-restore` — 0 warnings, 0 errors.
- [ ] **`AgentPanelState`** does not contain provider/credential configuration (grep-verified).

## Exact Next Step

Write `docs/phases/v2/phase-8/phase-8.1/IMPLEMENTATION_PLAN.md` — the detailed
plan for Settings Foundation (M1–M6). That plan locks the `ISettingsService`
interface shape, settings file path resolution, migration infrastructure,
secret store implementation, editor settings integration with `EditorView`,
the `AgentExecutionOptions` migration, and the settings UI before any
production code changes.

## Rollback Plan

- Commit hash to revert to: `fdc49fd` (Phase 8 umbrella plan committed, no code changes)
