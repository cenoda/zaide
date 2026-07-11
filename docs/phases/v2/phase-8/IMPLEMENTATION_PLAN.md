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

**Revised 2026-07-10 — second audit applied.**

This revision addresses the blocking findings from the audit of the first
revision: live LLM configuration (D4b), reachable close-workspace path (D7),
immutable settings snapshots and save concurrency (D4a), concrete view
injection and terminal runtime updates (D9), canonical command-and-gesture
table (D6a), project-context cancellation and thread ownership (D8), and a
stronger secret-file permission contract (D4). The affected decision sections
are marked with **[R]** annotations where the revised contract is material.

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

8. **Close workspace has no entry point that can produce a null `RootPath`.**
   `MainWindowViewModel.Activate()` (line 156-164) subscribes to
   `FileTreeViewModel.RootPath` but filters with `.Where(path => !string.IsNullOrEmpty(path))`,
   so a null transition never reaches `SetProjectFromPath()`. `FileTreeView`
   only ever assigns a non-null path (the folder picker returns zero folders
   when cancelled). There is therefore no user-facing command or UI that can
   produce the null close-workspace transition that D7 relies on; that branch
   is currently dead. Phase 8 must either add a reachable close-workspace path
   (see D7) or drop the null-transition claim.

9. **`AgentExecutionService` retains a single `AgentExecutionOptions` for its
   lifetime.** The constructor captures `_options` (line 19) and every
   `ExecuteAsync` call reads it. Building the options once from DI at startup
   means any later change to endpoint/model/key in the Settings UI would not
   affect subsequent requests. Phase 8 must make the effective LLM
   configuration live (see D4b).

10. **`EditorView`, `TerminalPanel`, and `TerminalRenderControl` are built with
    no settings injection.** `MainWindow.BuildLayout()` constructs
    `new EditorView()` with no arguments (line 334); `TerminalPanel` constructs
    `new TerminalRenderControl()` with no arguments (line 45); `TerminalTabHost`
    constructs `TerminalPanel`s internally. Passing `ISettingsService` to these
    surfaces requires a concrete composition path and constructor signatures,
    not the abstract "view composition passes the singleton" wording used in
    D9. `TerminalRenderControl._typeface` and `_fontSize` are `readonly`
    (lines 102-103), so runtime font updates require replacing them with
    mutable, recalculated state.

## Sub-Phase Decision (M0)

**Phase 8 is split into three separately planned sub-phases.** Each area is a
distinct concern with its own testing surface, and later sub-phases depend on
earlier ones.

| Sub-phase | Scope | Dependency |
|-----------|-------|------------|
| **Phase 8.1** | Settings foundation: `SettingsService`, persistence, migration, atomic writes, recovery, secret store, editor settings (code + prose + terminal fonts), terminal font settings, LLM settings migration, settings UI, and `WorkspaceFolderChanged` event | None |
| **Phase 8.2** | Command registry + keybindings: `ICommandRegistry`, command descriptors, default keybindings, user overrides, conflict handling | Consumes `ISettingsService` for user keybinding overrides |
| **Phase 8.3** | Authoritative project context: `IProjectContextService`, solution/project discovery, selection, load/unload/reload lifecycle, observable state | Consumes `ISettingsService`; consumes `Workspace.WorkspaceFolderChanged` event (added in 8.1) |

Each sub-phase gets its own `IMPLEMENTATION_PLAN.md` under
`docs/phases/v2/phase-8/` (e.g. `phase-8.1/IMPLEMENTATION_PLAN.md`) following
the V1 convention. This umbrella plan locks the cross-cutting decisions that all
sub-phases share.

## Goal

Remove the targeted user-facing hardcoding and establish the shared platform
infrastructure (settings, commands, project context) that Phases 9–13 consume.
After Phase 8, editor and terminal typography/whitespace settings are no longer
hardcoded in their implementation views, global keybindings are resolved through
the command infrastructure, no API key is plaintext in settings, and one
authoritative project context service is the single source of project truth.

Other view-local typography used by chrome, panels, captions, and controls is
not part of Phase 8. It remains governed by the existing style and design
system until a later, explicitly scoped visual-settings phase.

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

**[R] Path resolution:** `XDG_CONFIG_HOME` is used only when it is an absolute
path. When it is unset or invalid, Linux falls back to `$HOME/.config/zaide`;
if `HOME` is unavailable, the service uses the platform application-data
directory. Windows and macOS use their platform application-data directory
under `zaide` rather than interpreting `XDG_CONFIG_HOME`. The service creates
the directory before the first save.

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
- A missing, non-integer, zero, or negative `schemaVersion` is invalid and is
  handled as `Corrupt`; it falls back to last-known-good or defaults without
  overwriting the source file.
- A positive older version with no complete migration chain to the current
  version is `UnsupportedVersion`; it falls back to last-known-good or defaults
  and is never rewritten. This is the explicit unsupported-old behavior
  required by the V2 roadmap.
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
- `FileSecretStore` implementation: reads/writes `secrets.json`.
- Secrets are NEVER written to `settings.json`. The settings file may contain a
  reference like `"apiKeySource": "secret-store"` but never the value itself.
- Environment-variable fallback preserved: `AGENT_API_KEY` env var takes
  precedence over the secret store (matching the existing behavior).
- Precedence order: environment variable → secret store → empty (user must
  configure).

**[R] Stronger permission contract (resolves transient default-permission file
and atomic-replace mode loss):** The naive "create, then `File.SetUnixFileMode`
after first write" approach leaves a window where the file exists with default
(umask-derived, possibly group/other readable) permissions, and an
atomic temp-then-rename can replace a `0600` file with a temp whose mode was
set too late. The implementation must instead:

1. **Restrictive temp creation.** Create the temp file with a restrictive mode
   from the start. On Linux, create the temp with
   `File.Create(tmpPath, bufferSize, FileOptions.WriteThrough)` and immediately
   call `File.SetUnixFileMode(tmpPath, UnixFileMode.UserRead | UnixFileMode.UserWrite)`
   before any bytes are written. This guarantees there is no window where the
   temp is world-readable.
2. **Mode rides through the rename.** `File.Move(tmp, dest, overwrite: true)`
   preserves the temp file's inode and its `0600` mode on POSIX, so the
   destination ends up `0600` regardless of any previously-loose mode. The
   destination is therefore never briefly world-readable, and the atomic replace
   does not lose the mode.
3. **Validate/repair existing modes on load.** On every read/open, check
   `File.GetUnixFileMode(secretsPath)`. If the file exists but is not `0600`,
   re-apply `0600` (repair) and log a warning. This catches files that were
   created before the restrictive-create path existed or whose mode was changed
   by an external tool.
4. **Explicit non-Linux policy.** `File.SetUnixFileMode` / `GetUnixFileMode` are
   Linux-only and guarded with `OperatingSystem.IsLinux()`. On Windows and macOS
   the store creates the file with the platform-default ACLs (acceptable for
   V2's Linux-primary validation) and skips mode validation/repair. The
   limitation is documented; OS-native keychain integration (a future
   `ISecretStore` swap) removes the dependency on file permissions entirely.

**[R] Async/sync resolution:** The secret store is **synchronous**. File I/O for
a small JSON file (typically < 1 KB) is fast enough that async provides no
benefit for a desktop app's startup path. The existing `AgentExecutionOptions`
factory in `Program.cs` is synchronous — making `ISecretStore` async would
require restructuring DI composition for no practical gain. If OS keychain
integration is adopted later (swapping the `ISecretStore` implementation), the
interface can be made async at that point with a documented breaking change.

The `AgentExecutionOptions` factory described in earlier drafts is **removed**.
`AgentExecutionService` no longer retains a static options snapshot built once
by DI. Instead, effective LLM configuration is computed **live** on every
request from `ISettingsService` + `ISecretStore` + environment variables, so a
Settings UI change takes effect on the very next request without restart. See
**D4b** for the live-configuration decision and the required test.

`AgentExecutionOptions` remains as a plain immutable value object used only as
the per-call effective-options carrier inside `AgentExecutionService`. It is no
longer registered as a DI singleton, and DI composition stays synchronous.

### D4a: Settings Service Initialization, Snapshots, and Save Concurrency **[R]**

**Decision:** `ISettingsService` loads synchronously during construction. The
constructor reads the settings file (or falls back to defaults) before returning.
This ensures the service is fully initialized when resolved from DI. The effective
LLM options are computed live per request (see D4b), so the constructor does NOT
need to hand `settings.Current` to an options factory.

**Immutable snapshot model (revised — resolves the mutable-`Current` risk):**

- `SettingsModel` is a deeply-immutable `record` (all members `init`-only or
  `readonly`, nested record types likewise). `ISettingsService.Current` returns
  a **frozen snapshot**; consumers must not mutate it, and the service never
  mutates a snapshot it has already handed out. The only way to produce a new
  snapshot is via `with` expressions: `current with { CodeFontSize = 16 }`.
- `ISettingsService.WhenChanged` is an `IObservable<SettingsModel>` that emits a
  **new immutable snapshot** on every committed change. It is delivered on the
  UI thread: the service applies `ObserveOn(RxApp.MainThreadScheduler)` (or an
  explicit `Dispatcher` post) before emitting, so Avalonia bindings and view
  subscriptions receive updates without manual marshalling.
- `LoadResult` exposes the `SettingsLoadResult` enum so the UI can surface
  errors (unsupported version, corruption) after startup.

**Validated, atomic mutation via `Update`/`Apply` (replaces direct mutable `Current`):**

```csharp
public interface ISettingsService
{
    /// <summary>Frozen, never-null snapshot of current settings.</summary>
    SettingsModel Current { get; }

    /// <summary>Emits a NEW immutable snapshot on every committed change, on the UI thread.</summary>
    IObservable<SettingsModel> WhenChanged { get; }

    /// <summary>Result of the initial load (Missing/Corrupt/UnsupportedVersion/Loaded).</summary>
    SettingsLoadResult LoadResult { get; }

    /// <summary>
    /// Apply a validated pure transformation. The producer receives the current
    /// immutable snapshot and returns a new transformed instance (via `with`
    /// expressions on the immutable record). The service validates the result,
    /// then atomically swaps the in-memory snapshot and persists it.
    /// Returns a validation result; if invalid, nothing is committed and the
    /// error(s) are reported back to the caller for UI display.
    /// </summary>
    SettingsMutationResult Update(Func<SettingsModel, SettingsModel> producer, CancellationToken ct = default);

    /// <summary>
    /// Apply an already-constructed snapshot produced by the UI after its own
    /// validation. Same atomic-swap + persist + validation semantics as Update.
    /// The caller constructs the new `SettingsModel` via `with` expressions;
    /// the service never receives a "pre-mutated clone."
    /// </summary>
    SettingsMutationResult Apply(SettingsModel next, CancellationToken ct = default);

    /// <summary>Persist the current in-memory snapshot without mutation.</summary>
    Task<SettingsSaveResult> SaveAsync(CancellationToken ct = default);

    /// <summary>
    /// Fires on the UI thread when the async disk write triggered by
    /// Update/Apply fails. Does NOT fire for successful writes.
    /// The consumer (e.g. status bar) subscribes to surface a non-blocking
    /// error indicator.
    /// </summary>
    IObservable<SettingsSaveError> WriteErrors { get; }
}
```

- `SettingsMutationResult` carries `bool Succeeded`, `bool IsWritePending`
  (true until the async disk write completes), the committed `SettingsModel`
  (on success) or the rejected `SettingsModel` (on failure), and a
  `IReadOnlyList<SettingsValidationError>` listing field-level problems
  (e.g. non-positive font size, empty required LLM URL) for the UI to render
  inline. Validation is performed by a `SettingsValidator` over the candidate
  snapshot before any swap or write.
- The UI builds a candidate snapshot from `Current` using `with` expressions
  (no clone-and-mutate), shows validation errors live, and calls
  `Update`/`Apply` only when valid. There is no public settable `Current`,
  so unobserved or partially-saved state is impossible:
  either the whole snapshot commits or nothing does.

**Save concurrency (revised — resolves partial/overlapping-save risk):**

- The service holds a monotonically increasing `long _generation` and a
  `SemaphoreSlim _saveGate`. Each committed mutation increments `_generation`.
- `SaveAsync` / `Update` / `Apply` acquire `_saveGate` so only one disk write
  happens at a time (serialized). A write reads the current in-memory snapshot
  at write time, not a stale captured copy, so the latest committed mutation is
  always what lands on disk.
- Because saves are serialized through the gate and always write the live
  snapshot, overlapping edits cannot interleave or clobber each other. The
  atomic write-temp-then-rename (D2) makes the on-disk result always complete.
- `Update`/`Apply` perform the swap + validation synchronously under the gate,
  then trigger the async persist; the returned `SettingsMutationResult` reflects
  the committed in-memory state immediately, independent of write completion.

**`SettingsSaveError` definition:**

```csharp
public sealed record SettingsSaveError(
    Exception Exception,
    SettingsModel FailedSnapshot,  // the snapshot that failed to persist
    DateTime Timestamp
);
```

**Disk-write failure surfacing:**

Because `Update`/`Apply` return synchronously, the caller sees a successful
`SettingsMutationResult` before the async disk write completes. A failure to
persist must not be silently swallowed; the contract defines how failures are
surfaced:

1. **`SettingsMutationResult` carries a `bool IsWritePending` flag**, initially
   `true`. The in-memory snapshot is already swapped, so the UI reflects the
   new settings immediately.
2. **On write failure** (IOException, UnauthorizedAccessException, disk full):
   `ISettingsService.WriteErrors` fires a `SettingsSaveError` on the UI thread.
   The status bar or a toast subscribes to this observable and surfaces a
   non-blocking error indicator (e.g. "Settings not saved — retry?" with a
   retry button).
3. **Retry:** The consumer can call `SaveAsync()` manually after addressing
   the underlying issue (e.g. disk space freed). `SaveAsync` uses the current
   in-memory snapshot, so no state is lost.
4. **Periodic retry is not implemented in Phase 8** — the caller is responsible
   for retry. This avoids silent background loops. The unresolved write is
   always observable via `WriteErrors`.

**Construction contract:**

- The constructor performs the load. If the file is missing, defaults are used.
  If the file is corrupt, last-known-good or defaults are used. If the file has
  an unknown future version, defaults are used and `LoadResult` records the
  error. The constructor never throws.
- `Current` is never null at any point after construction, including during the
  first save.

### D4b: Live LLM Configuration **[R]**

**Decision (resolves the static-options finding):** `AgentExecutionService`
must not retain a single `AgentExecutionOptions` for its lifetime. Effective
configuration is resolved **live**, per `ExecuteAsync` call, from the current
settings snapshot, the secret store, and environment variables — in that
precedence order (env var highest, secret store middle, settings file lowest,
empty last).

- `AgentExecutionService` constructor takes `ISettingsService` and
  `ISecretStore` (and `HttpClient`) instead of a baked `AgentExecutionOptions`.
- Before each HTTP call, `ExecuteAsync` builds a fresh effective
  `AgentExecutionOptions` by reading `settings.Current.Llm` (immutable
  snapshot from D4a), `secrets.Get("llm.apiKey")`, then overlaying the
  `AGENT_API_URL` / `AGENT_API_KEY` / `AGENT_MODEL` environment variables when
  present. The existing validation of `ApiKey` / `BaseUrl` / `Model` is
  unchanged and now operates on the per-call effective options.
- Because `settings.Current` always reflects the last committed snapshot
  (D4a), any save made in the Settings UI is visible to the very next request.
  No restart, no re-injection, no options-refresh call is required.
- `AgentExecutionOptions` is demoted to a per-call value carrier (immutable
  DTO). It is no longer a DI singleton. DI composition stays synchronous.

**Explicit options-refresh contract (alternative if live resolution is
undesirable):** If a later phase prefers a pushed model, `AgentExecutionService`
subscribes to `ISettingsService.WhenChanged` and swaps an internal
`Volatile<AgentExecutionOptions>` under a lock. Either the pull (preferred) or
push model satisfies this finding; the plan commits to the **pull** model for
Phase 8 because it is simplest and needs no subscription/disposal in the
service.

**Required test (must pass before 8.1 closes):** A `LiveLlmConfigTests` case
that (1) constructs `AgentExecutionService` with a real or fake
`ISettingsService` + `ISecretStore`, (2) `Update`/`Apply`s a changed
`Llm.BaseUrl`/`Model` (or `secrets.Set("llm.apiKey", ...)`), (3) saves, and
(4) asserts the **next** `ExecuteAsync` call (captured/mock `HttpClient`) is
dispatched to the new URL with the new key — proving saved settings affect a
later request without reconstruction. A second case asserts that an env var
still overrides the saved value.

### D5: Command Registry Architecture

**Decision:** `ICommandRegistry` singleton service. ViewModels register commands
in their constructors with stable string identifiers. The service contract is
UI-framework-neutral; Avalonia objects are created only by the UI integration
layer.

```csharp
public sealed class CommandDescriptor
{
    public string Id { get; }           // e.g. "file.save", "explorer.toggleHiddenFiles"
    public string DisplayName { get; }  // e.g. "Save", "Toggle Hidden Files"
    public string Category { get; }     // e.g. "File", "Explorer"
    public IReadOnlyList<string> DefaultGestures { get; } // neutral gestures (aliases supported); empty if unbound
    public ICommand Command { get; }                // command execution abstraction
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
    /// Resolve the gesture -> command map from registered defaults and user
    /// overrides (stored in settings). Returns neutral keybinding descriptors;
    /// the UI layer converts them to framework-specific bindings. Called once
    /// by MainWindow during activation. Overrides take absolute precedence;
    /// conflicts are resolved by lexicographic command ID.
    /// </summary>
    IReadOnlyList<ResolvedKeyBinding> ResolveKeyBindings(ISettingsService settings);
}

public sealed record ResolvedKeyBinding(string Gesture, string CommandId);
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
2. If no user override, the command's neutral `DefaultGestures` are used.
   Each gesture string in the list produces its own `ResolvedKeyBinding`.
   A single command can have multiple keybindings (aliases).
3. If `DefaultGestures` is empty, the command is unbound (invokable via Command
   Palette in Phase 9, but not via keyboard).

**[R] Conflict policy (deterministic):**

- **Build time:** The registry materializes a neutral gesture → command map once, after
  all ViewModels have registered. Materialization happens when
  `ICommandRegistry.ResolveKeyBindings()` is called by `MainWindow` during
  activation.
- **User overrides take absolute precedence.** The override map is applied first.
  If two user overrides map to the same gesture, the one with the
  lexicographically earlier command ID wins. A warning is logged for the loser.
- **Default gestures fill remaining slots.** Only commands without a user
  override contribute their `DefaultGestures`. Each gesture string is resolved
  individually, so aliases on the same command are allowed and produce multiple
  `ResolvedKeyBinding` entries. If two default gestures (from different commands)
  collide, the one with the lexicographically earlier command ID wins. A warning
  is logged.
- **No gesture is ever assigned to two commands.** The winning command gets a
  `ResolvedKeyBinding`; the UI layer materializes the framework-specific
  binding and the losing command is unbound for that gesture.
- **No conflict-resolution UI in Phase 8.** Conflicts are logged. Phase 9
  (Command Palette) may surface them for user resolution.

**[R] Gesture validation and refresh:** Gestures use a documented neutral
grammar of modifier names (`Ctrl`, `Alt`, `Shift`, `Meta`) plus one key,
case-insensitively. Unknown keys, malformed strings, and overrides targeting
unregistered command IDs are ignored and logged; they never prevent startup.
`ResolveKeyBindings` is rerun when keybinding settings change and replaces the
previous UI bindings as one operation. The initial activation also performs a
full resolution after all registrations are complete.

**Rationale for deterministic policy:** The previous version said "later
registration wins" in one place and "first registration wins" in another. This
revision uses a single deterministic rule (lexicographic command ID) that does
not depend on registration order.

### D6a: Canonical Command-and-Gesture Table **[R]**

Every global command's default neutral gesture is **locked** here. The registry
is initialized with these bindings; no command may ship without an explicit
entry. Locking the table now (before 8.2) prevents the partial, order-dependent
behavior the first audit flagged.

**Neutral → Avalonia mapping (authoritative):**

- Modifiers in order: `Ctrl`, `Alt`, `Shift`, `Meta`.
- Keys are Avalonia `Key` enum names. The backtick/tilde key is **`Oem3`** —
  the neutral token `` Ctrl+` `` maps to `new KeyGesture(Key.Oem3, KeyModifiers.Control)`.
  This is the only key whose physical-code mapping is easy to get wrong; the
  table locks it explicitly.
- `Ctrl+Oem3` and `Ctrl+J` are both bound to the same command
  (`view.toggleBottomPanel`) in the live baseline; `CommandDescriptor.DefaultGestures`
  includes both as equivalent aliases. The resolution logic creates a
  `ResolvedKeyBinding` for each distinct gesture string, so both aliases are
  wired to the same command at the framework level.
- `Meta` maps to the OS command/windows key; left unbound on Linux primary
  validation.

**Locked default table:**

| Command ID | Display name | Category | Default neutral gesture(s) | Avalonia `KeyGesture`(s) | Notes |
|------------|--------------|----------|---------------------------|--------------------------|-------|
| `file.save` | Save | File | `Ctrl+S` | `Key.S`, `Ctrl` | Alias of active tab save |
| `workspace.openFolder` | Open Folder | Workspace | `Ctrl+O` | `Key.O`, `Ctrl` | Folder picker |
| `workspace.closeFolder` | Close Folder | Workspace | (unbound) | — | Added by D7; no default key yet |
| `view.toggleBottomPanel` | Toggle Bottom Panel | View | `Ctrl+Oem3`, `Ctrl+J` | `Key.Oem3`, `Ctrl` **and** `Key.J`, `Ctrl` | Two aliases, same command — both in `DefaultGestures` |
| `explorer.toggleHiddenFiles` | Toggle Hidden Files | Explorer | `Ctrl+Shift+H` | `Key.H`, `Ctrl`+`Shift` | Migrated from inline handler |
| `sourcecontrol.commit` | Commit | Source Control | (unbound) | — | Invoked from panel |
| `sourcecontrol.refresh` | Refresh | Source Control | (unbound) | — | Also fired on workspace change |

**Duplicate registration policy:** Registering the same `Id` twice is a
programming error. `ICommandRegistry` throws `InvalidOperationException` on a
duplicate `Id` at registration time (fail fast in dev). This is distinct from
gesture conflicts (D6), which are resolved by lexicographic command ID at
resolution time and only logged. No command may register without a non-empty
`Id`; `Id` and `Category` are required constructor arguments of
`CommandDescriptor`.

**Unavailable-command handling:** A command is *unavailable* when
`GetById(id)` returns null, or when the underlying `ICommand.CanExecute(parameter)`
returns false. The contract requires:
- `Execute(id)` / `Execute<T>(id, parameter)` return `false` (never throw) when
  the command is unavailable, and log a debug trace.
- The Settings UI / Command Palette must call `CanExecute` (via
  `GetById(id).Command.CanExecute`) before enabling a menu item; unavailable
  commands render disabled, not hidden.
- Keybindings whose command is currently unavailable are simply no-ops for that
  gesture (the gesture is still "consumed" so it does not fall through to view
  text input). Example: `workspace.closeFolder` is unavailable until a folder is
  open, so its (future) keybinding does nothing rather than throwing.
- `workspace.closeFolder` specifically must be enabled only when
  `FileTreeViewModel.RootPath` is non-null (see D7).

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
- A transition from a non-null path to `null` is the supported close-workspace
  operation. The `MainWindowViewModel` subscription must observe both non-null
  and null `RootPath` values, update `Workspace`, and refresh Source Control for
  either transition; it must not filter out null paths. This gives the project
  context service a real unload trigger rather than an unused null branch.
- `IProjectContextService` (Phase 8.3) subscribes to this event in its
  constructor. A non-null path starts a cancellable `LoadAsync`; a null path
  calls `UnloadAsync`. The handler observes and logs failures rather than
  silently dropping a fire-and-forget task.
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
        _handler = (_, _) => OnWorkspaceFolderChanged();
        _workspace.WorkspaceFolderChanged += _handler;
    }

    private void OnWorkspaceFolderChanged()
    {
        // The implementation owns cancellation and observes/logs failures.
        // A null workspace path unloads the context instead of being passed to
        // LoadAsync through the null-forgiving operator.
    }

   public void Dispose()
   {
       if (_handler != null)
           _workspace.WorkspaceFolderChanged -= _handler;
   }
}
```

**[R] Reachable close-workspace path (resolves the dead null-transition branch):**
The null `RootPath` transition that drives unload is currently unreachable —
`MainWindowViewModel.Activate()` filters null with
`.Where(path => !string.IsNullOrEmpty(path))` and `FileTreeView` only ever
assigns a non-null path. Phase 8 must provide a concrete, reachable path:

1. **Command:** `MainWindowViewModel` gains a parameterless
   `CloseFolderCommand` (`ReactiveCommand<Unit,Unit>`), registered with the
   command registry as `workspace.closeFolder` (D6a). It is enabled only when
   `FileTreeViewModel.RootPath` is non-null; `WhenAnyValue(x => x.FileTreeViewModel.RootPath)`
   drives `CloseFolderCommand` can-execute.
2. **UI entry point:** The file-tree header ("Open Folder...") becomes a
   split-style affordance: clicking the folder icon/path opens the folder
   picker (existing behavior), and a small close/recent button at the end of
   the header invokes `CloseFolderCommand`. This is the only required entry
   point; no menu bar is added (consistent with D9).
3. **Mechanism:** `CloseFolderCommand` executes
   `FileTreeViewModel.SetRootPath(null)` (a new public method that handles the
   full close sequence, described next), which then flows through the existing
   `MainWindowViewModel` subscription. That subscription is revised to
   **remove the `!string.IsNullOrEmpty(path)` filter** so a null transition
   reaches `Workspace.SetProjectFromPath(null)` and raises
   `WorkspaceFolderChanged`.

4. **Full close sequence in `FileTreeViewModel.SetRootPath(null):`
   Nulling the path alone is insufficient. `SetRootPath(null)` must also use the
   existing live seams — `FileTreeViewModel` owns `_watcherSubscription`
   (an `IDisposable?`) and calls `_fileTreeService.StopWatching()` on the
   injected `IFileTreeService`. It exposes `SelectedFile` (not `SelectedNode`)
   and `RootNodes` (`ObservableCollection<FileTreeNode>`). `FileTreeNode` is a
   plain `INotifyPropertyChanged` with `Name`, `FullPath`, `IsDirectory`,
   `Depth`, `IsExpanded` — it holds no disposable watcher handles. The sequence:
   - **Dispose the watcher subscription:** `_watcherSubscription?.Dispose()`
     and set the field to null. The subscription was created from
     `_fileTreeService.StartWatching(path)`.
   - **Stop the file system watcher:** `_fileTreeService.StopWatching()`.
     (The service owns `_watcher` internally; this call disposes it.)
   - **Clear tree state:** `RootNodes.Clear()`, `SelectedFile = null`,
     `StatusText = null` (or the initial "Open a folder to begin" string).
   - **No per-`FileTreeNode` disposal needed:** Nodes are plain POCOs with
     no watcher handles. `RootNodes.Clear()` is sufficient.
   After this sequence, the file tree renders as an empty (or "Open Folder")
   state. The single writer (`SetRootPath(null)`) guarantees all side effects
   happen atomically from the consumer's perspective.

5. **Source Control behavior:** On a null transition, `SourceControlViewModel`
   is refreshed; with no `WorkspacePath` it returns to its "No repository" /
   uninitialized state (it already keys off `Workspace.WorkspacePath`, which is
   now null). Open documents are **retained** — `Workspace` document ownership
   is independent of the folder identity, and open editor tabs are path-based
   and remain usable (their file paths are absolute, so the file is still
   reachable). Only the *project context* unloads (D7/D8); the document set is
   untouched. Existing tabs that happen to be inside the former workspace
   folder remain valid — they keep working until explicitly closed.

6. **Project context:** `IProjectContextService` observes the null transition
   and calls `UnloadAsync`, transitioning to `Unloaded` (D8). This makes the
   null branch a real, tested unload trigger rather than dead code.

The D7 "null is the supported close-workspace operation" claim is therefore
fully specified: there is a command, a UI affordance, a single writer
(`SetRootPath(null)`), an unfiltered subscription, defined Source Control and
document behavior, and an `UnloadAsync` outcome.

**Downstream consumers migrate gradually:**
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
- Cancellation is not represented as `Failed`. A caller-requested cancellation
  leaves the last stable context unchanged when possible, invalidates the
  cancelled request's sequence, and completes with cancellation. Explicit
  unload transitions to `Unloaded`; I/O and permission errors alone produce
  `Failed` with an error message.

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
    int capturedSeq = ++_discoverySeq;
    State = Loading;
    NotifyChanged();
    try
    {
        var result = await DiscoverAsync(workspaceRoot, ct).ConfigureAwait(true);
        if (_discoverySeq != capturedSeq) return;  // Stale — discard
        ApplyDiscoveryResult(result);
    }
    catch (OperationCanceledException)
    {
        if (_discoverySeq == capturedSeq)
            RestoreLastStableContext();
        throw;
    }
}
```

- **[R] Thread ownership of observable updates (resolves unowned dispatcher
behavior):** Discovery runs off the UI thread (e.g. `await Task.Run` /
`ConfigureAwait(false)` inside `DiscoverAsync`). All state application and
notification, however, must occur on the UI thread so Avalonia consumers
(`StatusBar`) bind without cross-thread violations:

- `ApplyDiscoveryResult`, `RestoreLastStableContext`, the `Loading` transition,
  and the `WhenChanged` emission each run via `Dispatcher.UIThread.Post` /
  `RxApp.MainThreadScheduler` (the service captures `Dispatcher.UIThread` in its
  constructor). `Current` is only ever mutated from the UI thread.
- `WhenChanged` is therefore safe to subscribe to directly from a view or
  view-model bound to Avalonia. Subscribers must not re-marshal.
- `LoadAsync`/`UnloadAsync`/`ReloadAsync` are awaitable from any thread; the
  *caller* may await on any context, but the internal state writes are owned by
  the UI thread as above.

**[R] `SelectProject` must reject out-of-snapshot candidates:** A user
selection is only valid against the candidates discovered in the *current*
`ProjectContext`. `SelectProject(candidate)`:

- If `candidate` is null → clears the selection (returns to `Ambiguous` when
  candidates exist, else `NoProject`).
- If `candidate` is non-null but is **not** contained in
  `Current.Candidates` (reference or path equality) → the call is rejected: it
  is ignored, a warning is logged, and the current state is left unchanged. This
  prevents a stale candidate (from a previous folder or a superseded discovery
  that was discarded by the sequence-number check) from being selected.
- A successful selection transitions `SingleProject`/`Ambiguous` → `Selected`
  and sets `SelectedProject`.

`Workspace.WorkspaceFolderChanged` triggers `LoadAsync` automatically.
- `Unsupported` state: the folder contains files with project-like extensions
  but none that Zaide can handle (e.g. `.vbproj`, `.fsproj`). This satisfies the
  V2 roadmap's "structured unsupported result" requirement.
- `Failed` state: an IO error, permission denied, or other unrecoverable
  discovery fault. `ErrorMessage` contains the reason. **Cancellation is NOT a
  `Failed` state** (see the lifecycle note below) — this resolves the earlier
  self-contradiction where cancellation was both "not Failed" and listed under
  `Failed`.

State invariants are explicit: exactly one supported candidate enters
`SingleProject` and exposes that candidate as `SelectedProject` for immediate
consumption; `Selected` is reserved for an explicit user selection from an
ambiguous result. Clearing a selection from an ambiguous context returns to
`Ambiguous`.

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

**Integration point (event/command bridge):** The current `MainWindow` has no
menu bar or settings entry point. Phase 8.1 adds a settings entry via:

- A gear icon button in the `StatusBar` (the status bar already exists and is
  always visible). Clicking it opens the settings panel.
- The settings panel is a slide-over panel (similar to the existing bottom panel
  toggle pattern) that overlays the main content.
- No menu bar is added. A menu bar is unnecessary for a single entry point and
  would be over-engineering.

**[R] Concrete event/command bridge (StatusBar → MainWindow):**

`StatusBar` and `MainWindow` are separate controls; `StatusBar` does not have a
reference to `MainWindow`. The bridge follows the existing `Interaction<TInput,
TOutput>` pattern from docs-rules.md §12a:

**StatusBar ViewModel conversion:** `StatusBar` currently extends
`ReactiveUserControl<MainWindowViewModel>` (line 21 of `StatusBar.cs`) and is
wired as `_statusBar.ViewModel = ViewModel;` in `MainWindow`'s constructor
(line 119). Phase 8.1 introduces `StatusBarViewModel` as a focused child
ViewModel and converts `StatusBar` to `ReactiveUserControl<StatusBarViewModel>`:

1. `StatusBarViewModel` is registered in DI as a singleton (it is a child of
   the window scope, not a transient). Its constructor receives
   `MainWindowViewModel` (already a singleton):
   ```csharp
   public class StatusBarViewModel : ReactiveObject
   {
       private readonly MainWindowViewModel _mainWindow;

       public StatusBarViewModel(MainWindowViewModel mainWindow)
       {
           _mainWindow = mainWindow;

           OpenSettingsCommand = ReactiveCommand.CreateFromTask(async () =>
               await mainWindow.ShowSettings.Handle(Unit.Default));

           // Reactive forwarding: WhenAnyValue observes the source property
           // and raises PropertyChanged on this VM so Avalonia bindings update.
           mainWindow.WhenAnyValue(x => x.CaretText)
                     .Subscribe(_ => this.RaisePropertyChanged(nameof(CaretText)));
           mainWindow.WhenAnyValue(x => x.LanguageText)
                     .Subscribe(_ => this.RaisePropertyChanged(nameof(LanguageText)));
           mainWindow.WhenAnyValue(x => x.ProjectText)
                     .Subscribe(_ => this.RaisePropertyChanged(nameof(ProjectText)));
           mainWindow.WhenAnyValue(x => x.BranchText)
                     .Subscribe(_ => this.RaisePropertyChanged(nameof(BranchText)));
       }

       public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }

       // Delegated properties — Avalonia binds here, not to MainWindowViewModel.
       // PropertyChanged is raised reactively by the subscriptions above.
       public string? CaretText => _mainWindow.CaretText;
       public string? LanguageText => _mainWindow.LanguageText;
       public string? ProjectText => _mainWindow.ProjectText;
       public string? BranchText => _mainWindow.BranchText;
   }
   ```

2. `StatusBar` converts from `ReactiveUserControl<MainWindowViewModel>` to
   `ReactiveUserControl<StatusBarViewModel>`. All existing one-way bindings
   (`CaretText`, `LanguageText`, `ProjectText`, `BranchText`) are retargeted
   to `StatusBarViewModel` properties, which forward reactively via the
   `WhenAnyValue` subscriptions above.

3. `MainWindow`'s constructor resolves `StatusBarViewModel` from DI and
   assigns it:
   ```csharp
   _statusBar.ViewModel = statusBarViewModel;  // was: ViewModel
   ```

4. `MainWindowViewModel` exposes the toggle interaction:
   ```csharp
   public Interaction<Unit, bool> ShowSettings { get; } = new();
   ```

5. `MainWindow` registers the handler in `WhenActivated` (per §12b). The
   handler resolves both dependencies from the DI container and stores the
   panel in a new `Border _settingsPanelHost` field on `MainWindow`. The panel
   overlays the full content area while leaving the status bar exposed:

   **Grid layout reference:** `MainWindow`'s root grid has 6 columns (0: nav
   bar 40px, 1: left panel 260px, 2: splitter 4px, 3: townhall 2*, 4: splitter
   4px, 5: editor 1.5*) and 4 rows (0: content star, 1: bottom splitter, 2:
   bottom panel, 3: status bar 24px). The nav bar spans rows 0–3; content
   panels span rows 0–2, leaving row 3 for the always-visible status bar.

   ```csharp
   // New field on MainWindow:
   private Border? _settingsPanelHost;

   // In WhenActivated:
   d.Add(ViewModel.ShowSettings.RegisterHandler(async ctx =>
   {
       if (_settingsPanelHost == null)
       {
           var settings = Locator.Current.GetService<ISettingsService>()!;
           var secrets = Locator.Current.GetService<ISecretStore>()!;
           var panel = new SettingsPanelView
           {
               DataContext = new SettingsViewModel(settings, secrets)
           };
           _settingsPanelHost = new Border
           {
               Child = panel,
               Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"]
           };
           // Overlay: column 0–5 (full width), rows 0–2 (content only).
           // Row 3 (status bar) is NOT covered — the status bar remains visible
           // so the user sees settings-save errors and normal indicators.
           // Z-order: added last to grid.Children, so it renders above all
           // existing panels. HorizontalAlignment/VerticalAlignment default to
           // Stretch, filling the occupied grid cells.
           Grid.SetColumn(_settingsPanelHost, 0);
           Grid.SetColumnSpan(_settingsPanelHost, 6);
           Grid.SetRow(_settingsPanelHost, 0);
           Grid.SetRowSpan(_settingsPanelHost, 3);
           ((Grid)Content!).Children.Add(_settingsPanelHost);
       }
       else
       {
           // Toggle: remove on close
           ((Grid)Content!).Children.Remove(_settingsPanelHost);
           _settingsPanelHost = null;
       }
       ctx.SetOutput(_settingsPanelHost != null);
   }));
   ```
   `SettingsPanelHost` is created on first open and destroyed on close — its
   lifetime matches the panel open duration. `HorizontalAlignment` and
   `VerticalAlignment` default to `Stretch`, so the panel fills its grid area.
   Because `_settingsPanelHost` is added to `Children` after all content
   panels, it renders at the highest Z-order automatically.

**[R] SettingsViewModel composition and registration (resolves dual-dependency inconsistency):**

- `SettingsViewModel` is not registered in DI (it is a transient per-dialog
  ViewModel, not a singleton). It is constructed at the call site inside the
  `RegisterHandler` lambda, receiving **both** `ISettingsService` and
  `ISecretStore` as resolved from the service locator:
  ```csharp
  new SettingsViewModel(settings, secrets)
  ```
- Constructor signature (both required):
  ```csharp
  public SettingsViewModel(ISettingsService settings, ISecretStore secretStore)
  ```
- `SettingsViewModel` owns:
  - A working `SettingsModel` candidate built from `Current` via `with`
    expressions.
  - `ApplyCommand` and `DiscardCommand` — the former calls
    `settings.Apply(candidate)`, the latter resets the candidate.
  - Live validation via `SettingsValidator` (inline errors).
- The settings panel is constructed in C# (per DESIGN.md Rule 1) and bound to
  `SettingsViewModel`. Its lifetime matches the panel open duration; closing the
  panel disposes the ViewModel.
- The settings UI follows existing panel patterns.

**[R] Concrete view-injection route (resolves the abstract "view composition
passes the singleton" claim):** The live construction path is
`MainWindow.BuildLayout()` → `new EditorView()` (no args), and
`TerminalTabHost` → `new TerminalPanel()` → `new TerminalRenderControl()` (no
args). Phase 8.1 changes these constructors to receive `ISettingsService`
explicitly, threading it down the existing ownership chain:

- **`EditorView`** ctor: `EditorView(ISettingsService settings)`. `MainWindow`
  resolves `ISettingsService` from DI (it is a registered singleton) and passes
  it at the `new EditorView(settings)` site in `BuildLayout()`. The EditorView
  applies `settings.Current` once in its constructor and subscribes to
  `settings.WhenChanged` for subsequent changes; the subscription is disposed
  when the EditorView is disposed (window close). EditorView owns its
  subscription; it does not own the service lifetime (service is a singleton).
- **`TerminalTabHost`** ctor: `TerminalTabHost(ISettingsService settings)`. It
  forwards `settings` into every `new TerminalPanel(settings)` it creates for a
  tab. `MainWindow` passes the resolved service into `new TerminalTabHost(settings)`.
- **`TerminalPanel`** ctor: `TerminalPanel(ISettingsService settings)`. It
  passes `settings` into `new TerminalRenderControl(settings)` and also
  subscribes itself for panel-level concerns (font-size changes drive a
  re-measure of the log list). TerminalPanel owns the TerminalRenderControl's
  lifetime and disposes its subscription when the tab closes.
- **`TerminalRenderControl`** ctor: `TerminalRenderControl(ISettingsService settings)`.
  It applies `settings.Current` once and subscribes to `WhenChanged`.

**Disposal ownership (explicit):** `ISettingsService` is a process-lifetime
singleton owned by the DI container, never by a view. Each view/panel holds
only a `WhenChanged` subscription, disposed as follows:
- `EditorView`'s subscription is disposed with the EditorView (window close,
  via `MainWindow`'s disposal chain).
- `TerminalTabHost` disposes each `TerminalPanel` (and thus its
  `TerminalRenderControl` + subscription) when its tab is closed.
- No view calls `Dispose` on `ISettingsService`.

**[R] Terminal runtime font update (replaces `readonly` fields):** Live font
changes require `TerminalRenderControl` to stop holding immutable font state.
The current `private readonly Typeface _typeface;` and
`private readonly double _fontSize = 14;` (lines 102-103) are replaced with
mutable fields plus a recalculation method:

```csharp
// TerminalRenderControl.cs (Phase 8.1)
private Typeface _typeface;
private double _fontSize;

public void ApplyFontSettings(FontFamily family, double size)
{
    _fontSize = size;
    _typeface = new Typeface(family, FontStyle.Normal, FontWeight.Normal);
    // Recompute cell metrics from the new typeface/size.
    (_cellWidth, _lineHeight) = MeasureCell(_typeface, _fontSize);
    InvalidateMeasure();
    InvalidateVisual();
}
```

The `WhenChanged` subscription calls `ApplyFontSettings(Current.Editor.TerminalFontFamily,
Current.Editor.TerminalFontSize)` and the control re-measures its character-cell
grid (cell width / line height) so the new font propagates without reconstructing
the panel. EditorView applies its code/prose font, size, tab size, insertion
mode, and whitespace options analogously through AvaloniaEdit's `TextArea.Options`.

**[R] Runtime application contract (summary):** `ISettingsService` exposes
`IObservable<SettingsModel> WhenChanged` (UI-thread-delivered, D4a) in addition
to the immutable `Current`. Each surface applies `Current` once at construction
and subscribes for subsequent changes for the lifetime of the view/panel per the
ownership chain above. Settings changes are applied immediately on the UI thread;
reopening a view is not required.

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
   additional event. The event is raised by `SetProjectFromPath()`, including
   the null close-workspace transition.

## Milestones (Umbrella)

| Milestone | Sub-phase | Description | Verification |
|-----------|-----------|-------------|--------------|
| **M0** | Umbrella | Lock all cross-cutting decisions in this plan. No production code is changed by M0. | Plan review confirms the contracts below before 8.1 begins. |
| **M1–M6** | 8.1 | Settings foundation: `ISettingsService` (immutable snapshots, `Update`/`Apply`, UI-thread `WhenChanged`, serialized saves, `WriteErrors` observable), JSON persistence, migration infrastructure, atomic writes, recovery, `ISecretStore` (restrictive create + mode repair), editor settings (code + prose + terminal fonts), `WorkspaceFolderChanged` event, live LLM config (D4b), reachable close-workspace path with full file-tree close sequence (D7), settings UI with explicit `ShowSettings` Interaction bridge (D9). `CancellationToken` on `SaveAsync`. | `dotnet build` + `dotnet test` green. Add and retain `Phase8ProofOfConceptTests` covering JSON/schema validation, Unix mode 0600, atomic write, last-known-good fallback, and future/old-version rejection. Settings round-trip, migration, secret, runtime editor/terminal application, and LLM precedence tests. **New blocking tests:** (a) `LiveLlmConfigTests` — save settings, then assert the *next* `AgentExecutionService.ExecuteAsync` uses the new endpoint/key (D4b); (b) immutable-snapshot test — `Update`/`Apply` commits a validated whole snapshot via `with` expressions and rejects invalid ones (D4a); (c) close-workspace test — `CloseFolderCommand` → null `RootPath` → watcher disposed → tree cleared → `WorkspaceFolderChanged` with null → Source Control uninitialized and open documents retained (D7); (d) terminal runtime font test — `ApplyFontSettings` recomputes cell metrics without reconstructing the panel (D9); (e) secret mode test — temp created `0600`, rename preserves mode, and a pre-existing loose-mode file is repaired on load (D4); (f) write-error surfacing test — simulate disk-write failure, assert `WriteErrors` observable fires on UI thread and retry via `SaveAsync` succeeds (D4a); (g) settings gear bridge test — gear icon click triggers `ShowSettings` Interaction and opens `SettingsPanelView` with a transient `SettingsViewModel` (D9). |
| **M7–M10** | 8.2 | Command registry + keybindings: `ICommandRegistry`, command descriptors, **canonical command-and-gesture table (D6a)**, default keybindings, user overrides, window keybinding integration (`Ctrl+Oem3`/`Ctrl+J`/`Ctrl+S`/`Ctrl+O`). | All parameterless global commands registered with stable IDs. Keybindings resolved from registry. User override test. **Duplicate-registration throws; unavailable-command handling test (D6a); canonical gesture-table coverage test (every locked default gesture resolves to the right command, including `Ctrl+Oem3` → `view.toggleBottomPanel`).** Build + tests green. |
| **M11–M14** | 8.3 | Authoritative project context: `IProjectContextService`, discovery, selection, lifecycle, observable state, status bar integration. `CancellationToken` on `LoadAsync`/`ReloadAsync`/`UnloadAsync`. Stale-load sequence-number pattern. `IDisposable` event subscription. **UI-thread ownership of `WhenChanged`/state writes (D8); `SelectProject` rejects out-of-snapshot candidates (D8).** | Discovery finds `.sln`/`.csproj` in test fixtures. All 8 states tested (Unloaded / Loading / NoProject / Unsupported / SingleProject / Ambiguous / Selected / Failed). Stale-load sequence test (rapid LoadAsync → stale result discarded). Cancellation test (cancellation is NOT `Failed`; last stable context preserved). **`SelectProject` out-of-snapshot rejection test.** **`WhenChanged` delivered on UI thread test.** Subscription disposal test. Observable state consumed by status bar. Build + tests green. |

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
- `src/ViewModels/SettingsViewModel.cs` — settings UI ViewModel (transient, constructed inside `MainWindow`'s `ShowSettings` handler, not in DI)
- `src/ViewModels/StatusBarViewModel.cs` — status bar ViewModel (receives `MainWindowViewModel`; exposes `OpenSettingsCommand` to trigger `ShowSettings` Interaction)
- `src/Views/SettingsPanelView.cs` — settings slide-over panel (C# per DESIGN.md; hosted in `MainWindow` via `_settingsPanelHost` field, toggled by `ShowSettings` handler)
- `src/Views/SettingsView.cs` — settings UI content (C# per DESIGN.md)

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
| `src/Program.cs` | All | Register new services. `AgentExecutionService` registered with `ISettingsService` + `ISecretStore` (D4b). `AgentExecutionOptions` is **no longer a DI singleton**. |
| `src/Models/Workspace.cs` | 8.1 | Add `WorkspaceFolderChanged` event. `SetProjectFromPath()` raises it after mutation (including null close-workspace transition). Consumed by `IProjectContextService` (8.3). |
| `src/Views/EditorView.cs` | 8.1 | Ctor gains `ISettingsService`. Replace hardcoded font/size/whitespace literals with settings-driven values applied at construction + on `WhenChanged`. |
| `src/Views/TerminalRenderControl.cs` | 8.1 | Ctor gains `ISettingsService`. Replace `readonly` `_typeface`/`_fontSize` with mutable fields + `ApplyFontSettings(...)` (D9). Settings-driven font applied at construction + on `WhenChanged`. |
| `src/Views/TerminalPanel.cs` | 8.1 | Ctor gains `ISettingsService`; forwards it to `TerminalRenderControl`. Owns the render control's `WhenChanged` subscription for the tab lifetime. |
| `src/Views/TerminalTabHost.cs` | 8.1 | Ctor gains `ISettingsService`; forwards it to each `TerminalPanel` it creates. |
| `src/MainWindow.axaml.cs` | 8.1 + 8.2 | 8.1: register `ShowSettings` Interaction handler (constructs `SettingsPanelView` + `SettingsViewModel(settings, secrets)`, manages `_settingsPanelHost` `Border` field); resolve `StatusBarViewModel` from DI and assign to `_statusBar.ViewModel` (replacing `ViewModel`); pass `ISettingsService` into `new EditorView(settings)` and `new TerminalTabHost(settings)`. 8.2: replace imperative keybinding wiring with registry-driven binding (`Ctrl+Oem3`/`Ctrl+J`/`Ctrl+S`/`Ctrl+O`). |
| `src/Services/AgentExecutionOptions.cs` | 8.1 | Demoted to a plain immutable per-call value object. No DI registration, no factory. |
| `src/Services/AgentExecutionService.cs` | 8.1 | Ctor gains `ISettingsService` + `ISecretStore` (replacing the static `AgentExecutionOptions`). Resolves effective LLM config live per `ExecuteAsync` (D4b). |
| `src/ViewModels/MainWindowViewModel.cs` | 8.1 + 8.3 | 8.1: add `CloseFolderCommand` (`workspace.closeFolder`, enabled only when a folder is open); add `ShowSettings` Interaction; observe both null and non-null `FileTreeViewModel.RootPath` transitions (remove the `!string.IsNullOrEmpty` filter) so close-workspace unloads and refreshes Source Control. 8.3: inject `IProjectContextService` and subscribe to project context changes. |
| `src/ViewModels/FileTreeViewModel.cs` | 8.1 | Add public `SetRootPath(string? path)` — the single writer of `RootPath`, including the full close sequence (`_watcherSubscription?.Dispose()`, `_fileTreeService.StopWatching()`, `RootNodes.Clear()`, `SelectedFile = null`, `StatusText = null`). See D7 for the complete sequence. |
| `src/Views/FileTreeView.cs` | 8.1 + 8.2 | 8.1: add a close-workspace affordance in the header that invokes `MainWindowViewModel.CloseFolderCommand`. 8.2: `Ctrl+Shift+H` handler replaced with registry command call. |
| `src/Views/StatusBar.cs` | 8.1 + 8.3 | **8.1:** convert from `ReactiveUserControl<MainWindowViewModel>` to `ReactiveUserControl<StatusBarViewModel>`; add settings gear icon button that binds to `StatusBarViewModel.OpenSettingsCommand`; forward existing one-way bindings (caret, language, project, branch) through `StatusBarViewModel` properties. 8.3: consume project context name instead of folder name. |
| `src/ViewModels/StatusBarViewModel.cs` | 8.1 | DI singleton. Ctor receives `MainWindowViewModel`. Exposes `OpenSettingsCommand` (triggers `mainWindow.ShowSettings`) and delegated properties for caret/language/project/branch forwarded from `MainWindowViewModel`. |
| `src/ViewModels/SettingsViewModel.cs` | 8.1 | Constructed transiently (not in DI) inside `MainWindow`'s `RegisterHandler` lambda. Receives `ISettingsService` + `ISecretStore` (both arguments required). Owns working candidate via `with` expressions, `ApplyCommand`/`DiscardCommand`, and live validation. |
| `src/Views/SettingsPanelView.cs` | 8.1 | Slide-over panel hosted in `MainWindow` (stored in `_settingsPanelHost` `Border` field), toggled by `ShowSettings` handler. C# view bound to transient `SettingsViewModel`. |

### DI Registration Changes (`src/Program.cs`)

```csharp
// Phase 8.1
services.AddSingleton<ISettingsService, SettingsService>();
services.AddSingleton<ISecretStore, FileSecretStore>();
services.AddSingleton<StatusBarViewModel>(); // child VM, receives MainWindowViewModel
// AgentExecutionService now resolves live config per call (D4b):
// Register via interface — existing coordinator resolves IAgentExecutionService.
services.AddSingleton<IAgentExecutionService, AgentExecutionService>(); // ctor(HttpClient, ISettingsService, ISecretStore)
// AgentExecutionOptions is NOT registered (options are live-resolved per call).

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
| Secret file permissions transiently loose or lost on rename | Restrictive temp creation (`0600` before first byte) + mode rides through `File.Move` + validate/repair existing mode on load (Linux). Non-Linux path documented. See D4. |
| Saved LLM settings don't take effect until restart | `AgentExecutionService` resolves effective config **live** per call from `ISettingsService` + `ISecretStore` + env vars (D4b). `LiveLlmConfigTests` proves the next request uses saved settings. |
| Mutable `Current` causes partial/unobserved saves | `SettingsModel` is immutable; only validated `Update`/`Apply` whole-snapshot commits are allowed; saves serialized through a gate (D4a). |
| Close workspace unreachable | `CloseFolderCommand` (`workspace.closeFolder`) + header affordance + `SetRootPath(null)` + unfiltered subscription make the null transition real and tested (D7). |
| Terminal font can't update at runtime | `readonly` typeface/size replaced with mutable fields + `ApplyFontSettings` that recomputes cell metrics; `WhenChanged` drives it (D9). |
| Project-context `WhenChanged` fires on a non-UI thread | State writes and `WhenChanged` owned by `Dispatcher.UIThread` (D8). |
| `SelectProject` accepts a stale candidate | Candidates not in `Current.Candidates` are rejected (D8). |
| `Workspace` split breaks existing consumers | `Workspace` does NOT split in Phase 8. Document/tab ownership and folder identity remain together. `IProjectContextService` is additive. No existing consumer is forced to change. |
| `Workspace.WorkspacePath` has no change notification | Phase 8 adds `WorkspaceFolderChanged` event (D7). Minimal additive change. No existing consumer broken. |
| Command registry injection breaks all ViewModel constructors | Only ViewModels with parameterless global commands gain `ICommandRegistry`. Parameterized commands stay ViewModel-local. Test helpers updated per sub-phase. |
| Project discovery is slow on large folders | Phase 8 discovery is non-recursive (root level only). Scanning a folder for `*.sln`/`*.csproj` is fast. Recursive discovery is not in scope. |
| `AgentExecutionOptions` env-var fallback breaks during migration | Precedence order is explicit: env var → secret store → settings default → empty. `AgentExecutionService` reads it **live** per call (D4b); no static snapshot. Synchronous DI composition preserved. Tests cover all four cases. |
| Settings UI has no integration point | Status bar gear icon (D9). No menu bar needed. |

## Exit Conditions

- [ ] **Settings:** Versioned settings file (schema v1) loads, saves, and recovers from corruption. Atomic writes verified. Missing/invalid schema, unsupported old versions, and unknown future versions are refused without overwriting the source file. Migration infrastructure exists (empty production migration list; synthetic test migration in test project). `SaveAsync` accepts `CancellationToken`.
- [ ] **Secrets:** API key is not in `settings.json`. `ISecretStore` provides get/set/delete. Env-var fallback works.
- [ ] **Editor fonts:** No font family (code or prose) or font size is a hardcoded literal in `EditorView`. Code font (`codeFontFamily`/`codeFontSize`) and prose font (`proseFontFamily`) are separate settings driven by `ISettingsService`.
- [ ] **Terminal fonts:** No font family or font size is a hardcoded literal in `TerminalRenderControl`. Terminal font (`terminalFontFamily`/`terminalFontSize`) driven by `ISettingsService`.
- [ ] **LLM (live):** `AgentExecutionService` resolves effective LLM config **live** per `ExecuteAsync` from `ISettingsService` + `ISecretStore` + env-var fallback (D4b). Saving endpoint/model/key in the Settings UI affects the next request without restart — verified by `LiveLlmConfigTests`. No plaintext API key in settings file. `AgentExecutionOptions` is a per-call value carrier, not a DI singleton. Synchronous DI composition preserved.
- [ ] **Settings mutation:** `ISettingsService` exposes an **immutable** `Current` snapshot and a UI-thread `WhenChanged`. Mutation happens only via validated `Update`/`Apply` (whole-snapshot commit, field-level validation errors reported, no mutable public `Current`). Saves serialized through a gate so overlapping edits cannot clobber each other (D4a).
- [ ] **Secrets:** API key is not in `settings.json`. `ISecretStore` provides get/set/delete. Temp file created `0600` before write; rename preserves mode; pre-existing loose-mode file repaired on load (Linux). Env-var fallback works. Non-Linux policy documented (D4).
- [ ] **Commands:** All parameterless global commands registered with stable string IDs. `ICommandRegistry` provides lookup, `Execute(string id)`, and `Execute<T>(string id, T parameter)`. Duplicate `Id` registration throws. Unavailable commands (`GetById` null or `CanExecute` false) make `Execute` return `false` and render disabled in UI — never throw. Parameterized commands remain ViewModel-local. **Canonical command-and-gesture table (D6a) locks every default gesture, including `Ctrl+Oem3` → `view.toggleBottomPanel`.**
- [ ] **Keybindings:** Window keybindings are materialized by the UI from
   neutral results returned by `ICommandRegistry.ResolveKeyBindings()`.
   Deterministic conflict resolution (lexicographic command ID). Exception list
   documented (D10). `Ctrl+Oem3` and `Ctrl+J` are documented aliases for
   `view.toggleBottomPanel`.
- [ ] **Close workspace (reachable):** `MainWindowViewModel.CloseFolderCommand` (`workspace.closeFolder`), enabled only when a folder is open, drives `FileTreeViewModel.SetRootPath(null)` → unfiltered `RootPath` subscription → `Workspace.SetProjectFromPath(null)` → `WorkspaceFolderChanged`(null) → `IProjectContextService.UnloadAsync` and Source Control returns to uninitialized; open documents are retained (D7).
- [ ] **Project context:** `IProjectContextService` discovers `.sln`/`.slnx`/`.csproj` in workspace root. Full 8-state lifecycle (Unloaded → Loading → NoProject/Unsupported/SingleProject/Ambiguous/Selected/Failed). Extension classification table determines supported vs. unsupported. `LoadAsync`/`ReloadAsync`/`UnloadAsync` accept `CancellationToken`. **Cancellation is NOT `Failed`** — it preserves the last stable context; only IO/permission faults are `Failed` (D8). Stale-load sequence-number pattern prevents out-of-order results. `Workspace.WorkspaceFolderChanged` event drives auto-discovery with `IDisposable` subscription. **State writes and `WhenChanged` owned by the UI thread (D8).** **`SelectProject` rejects candidates not in `Current.Candidates` (D8).** Status bar consumes project context name.
- [ ] **Workspace:** `Workspace` document ownership unchanged. `WorkspaceFolderChanged` event added. Event raised by `SetProjectFromPath()`. `IProjectContextService` is additive. No parallel sources of project truth.
- [ ] **Settings UI:** Accessible via status bar gear icon. Editor and LLM settings editable.
- [ ] **Tests:** All pre-existing tests pass. New tests cover settings, secrets, commands, keybindings, and project context. Test count at closeout recorded from `dotnet test` output (baseline: 817 as of 2026-07-10).
- [ ] **Build:** `dotnet build Zaide.slnx --no-restore` — 0 warnings, 0 errors.
- [ ] **`AgentPanelState`** does not contain provider/credential configuration (grep-verified).

## Exact Next Step

Write `docs/phases/v2/phase-8/phase-8.1/IMPLEMENTATION_PLAN.md` — the detailed
plan for Settings Foundation (M1–M6). That plan locks the `ISettingsService`
interface shape (immutable `Current`, UI-thread `WhenChanged`, validated
`Update`/`Apply`, serialized `SaveAsync` — D4a), settings file path resolution,
migration infrastructure, secret store implementation (restrictive create +
mode repair — D4), editor/terminal settings integration with the concrete
constructor-injection path and terminal runtime font update (D9), the
**live** `AgentExecutionService` LLM resolution (D4b), the reachable
close-workspace path (D7), and the settings UI before any production code
changes. The 8.1 plan must include the blocking tests from M1
(`LiveLlmConfigTests`, immutable-snapshot, close-workspace, terminal font,
secret mode).

## Rollback Plan

- Commit hash to revert to: `fdc49fd` (Phase 8 umbrella plan committed, no code changes)
