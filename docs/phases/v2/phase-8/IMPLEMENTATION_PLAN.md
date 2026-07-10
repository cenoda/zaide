# Phase 8: Core Platform and Settings — Implementation Plan

## Pre-Implementation Verification

- [x] Confirm V1 is complete: `dotnet build` 0 warnings / 0 errors, `dotnet test` 817 passed
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

**Initial — 2026-07-10.**

This plan is written against the live codebase on 2026-07-10. All findings are
verified against source files, not documentation assumptions.

### Live Baseline (verified 2026-07-10)

| File | Phase 8-relevant facts |
|------|------------------------|
| `src/Program.cs` | DI composition root. All registrations inline in one lambda (lines 24-78). `AgentExecutionOptions` created via factory lambda reading `AGENT_API_URL`, `AGENT_API_KEY`, `AGENT_MODEL` env vars. No settings service, no command registry, no project context service registered. |
| `src/Models/Workspace.cs` | Combines two responsibilities: (A) document/tab ownership (`_documents` dict, `OpenDocument`, `CloseDocument`, `SetActiveDocument`, `ActiveDocument`) and (B) folder identity (`WorkspacePath`, `ProjectName`, `SetProjectFromPath`). No project/solution awareness. `ProjectName` is just `Path.GetFileName(folderPath)`. |
| `src/Services/AgentExecutionOptions.cs` | Simple DTO: `BaseUrl` (default `https://api.openai.com/v1`), `ApiKey` (default empty), `Model` (default `gpt-4o-mini`). Populated from env vars in `Program.cs`. Plaintext in memory for process lifetime. |
| `src/Services/AgentExecutionService.cs` | Validates `ApiKey`, `BaseUrl`, `Model` non-empty before HTTP call. Returns structured `AgentExecutionResult`. |
| `src/Models/AgentPanelState.cs` | Per-panel UI state. Deliberately excludes provider/credential configuration. Phase 8 must NOT move endpoint/model/secret settings here. |
| `src/Views/EditorView.cs` | All font/size/whitespace values hardcoded as `static readonly` literals: font `"Cascadia Code, Consolas, monospace"`, size `14`, `ShowTabs = false`, `ShowSpaces = false`. Indent size uses AvaloniaEdit default (4). |
| `src/Views/TerminalRenderControl.cs` | Terminal font `"Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace"`, size `14`. |
| `src/Styles/TextStyles.cs` | Global text style factory. Sizes 11/12/13/15 px. Not editor-specific. |
| `src/Views/IndentGuideRenderer.cs` | Reads `textView.Options.IndentationSize` at render time — auto-adapts. |
| `src/Views/IndentGuideMetrics.cs` | Reads `textView.Options.IndentationSize` — auto-adapts. |
| `src/MainWindow.axaml.cs` | 4 window-level keybindings hardcoded in `WhenActivated`: `` Ctrl+` ``, `Ctrl+J` (toggle bottom panel), `Ctrl+S` (save), `Ctrl+O` (open folder). |
| `src/Views/TerminalPanel.cs` | Inline `KeyDown` handler for `Ctrl+C`, `Ctrl+Shift+C/V`, `PageUp/Down/Home/End`, search `Enter/Escape`. |
| `src/Views/FileTreeView.cs` | Inline `Ctrl+Shift+H` toggles hidden files, `Enter` opens selected file. |
| All ViewModels | Commands are `ReactiveCommand` properties created inline in constructors. No stable identifiers, no registry, no metadata. ~30 commands across 8 ViewModels. |
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

5. **Keybindings are scattered.** 4 in `MainWindow.axaml.cs`, 5+ inline `KeyDown`
   handlers in views. No central resolution, no conflict detection, no user override.

6. **API key is plaintext in memory.** `AgentExecutionOptions.ApiKey` is a plain
   `string` property on a singleton. No secret boundary exists.

## Sub-Phase Decision (M0)

**Phase 8 is split into three separately planned sub-phases.** Each area is a
distinct concern with its own testing surface, and later sub-phases depend on
earlier ones.

| Sub-phase | Scope | Dependency |
|-----------|-------|------------|
| **Phase 8.1** | Settings foundation: `SettingsService`, persistence, migration, atomic writes, recovery, secret store, editor settings, LLM settings migration, settings UI | None |
| **Phase 8.2** | Command registry + keybindings: `ICommandRegistry`, command descriptors, default keybindings, user overrides, conflict handling | Consumes `ISettingsService` for user keybinding overrides |
| **Phase 8.3** | Authoritative project context: `IProjectContextService`, solution/project discovery, selection, load/unload/reload lifecycle, observable state | Consumes `ISettingsService` for project-related settings |

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

**Schema versioning:** Top-level `"schemaVersion": N` integer field.

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

### D4: Secret-Handling Boundary

**Decision:** Separate file at `{XDG_CONFIG_HOME}/zaide/secrets.json` with
`0600` permissions (owner read/write only on Linux).

- `ISecretStore` interface: `Task<string?> GetAsync(string key)`,
  `Task SetAsync(string key, string value)`, `Task DeleteAsync(string key)`.
- `FileSecretStore` implementation: reads/writes `secrets.json`. On first write,
  creates the file with `0600` permissions via `File.SetUnixFileMode`.
- Secrets are NEVER written to `settings.json`. The settings file may contain a
  reference like `"apiKeySource": "secret-store"` but never the value itself.
- Environment-variable fallback preserved: `AGENT_API_KEY` env var takes
  precedence over the secret store (matching the existing behavior).
- Precedence order: environment variable → secret store → empty (user must
  configure).

| Alternative | Rejected Because |
|-------------|------------------|
| OS keychain (libsecret / DPAPI / Keychain) | Adds platform-specific dependency. The file-based approach with `0600` satisfies the "not plaintext in settings" constraint. OS keychain can be adopted later by swapping the `ISecretStore` implementation. |
| Encrypt secrets in settings.json | Key management problem — where does the encryption key live? Separate file with restricted permissions is simpler and equally secure for a local desktop app. |
| *Chosen: separate file with restricted permissions* | Simple, no new dependencies, satisfies the constraint, swappable implementation. |

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

- `ICommandRegistry` exposes: `Register(CommandDescriptor)`, `IReadOnlyList<CommandDescriptor> GetAll()`,
  `CommandDescriptor? GetById(string id)`, `bool Execute(string id)`.
- ViewModels receive `ICommandRegistry` via constructor injection and call
  `Register()` for each command they create.
- The registry does NOT own command lifetime — ViewModels create and own the
  `ReactiveCommand` instances. The registry holds a reference for lookup and
  execution.

### D6: Keybinding Resolution and Conflict Policy

**Decision:** Three-layer resolution: user override → default gesture → unbound.

1. `settings.json` may contain a `"keybindings"` section:
   `{ "file.save": "Ctrl+Shift+S" }` — user override.
2. If no user override, the command's `DefaultKeyGesture` is used.
3. If no default gesture, the command is unbound (invokable via Command Palette
   in Phase 9, but not via keyboard).

**Conflict policy:**
- User override always wins. If a user binds two commands to the same gesture,
  the last-registered command wins and a warning is logged.
- Among default gestures, later registration wins (with warning logged).
- No conflict-resolution UI in Phase 8. Conflicts are logged and the first
  registration wins at the window keybinding level (Avalonia's behavior).
- Phase 9 (Command Palette) will surface conflicts for user resolution.

### D7: Workspace Migration Strategy

**Decision:** `Workspace` retains document/tab ownership. A new
`IProjectContextService` becomes the authoritative project/solution context.

- `Workspace` keeps: `_documents`, `OpenDocument`, `CloseDocument`,
  `SetActiveDocument`, `ActiveDocument`, `Documents`.
- `Workspace` keeps: `WorkspacePath`, `ProjectName`, `SetProjectFromPath` —
  these represent the opened folder, not the project.
- `Workspace` does NOT gain project/solution awareness.
- `IProjectContextService` observes `Workspace.WorkspacePath` changes and
  triggers discovery independently.
- Downstream consumers migrate gradually:
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
    NoWorkspace,    // No folder open
    NoProject,      // Folder open, no .sln/.slnx/.csproj found
    SingleProject,  // Exactly one found — auto-selected
    Ambiguous,      // Multiple found — user must select
    Selected        // User has selected a specific project
}

public sealed class ProjectContext
{
    public ProjectContextState State { get; }
    public string? WorkspaceRoot { get; }
    public IReadOnlyList<ProjectCandidate> Candidates { get; }
    public ProjectCandidate? SelectedProject { get; }
}

public sealed class ProjectCandidate
{
    public string FilePath { get; }      // Full path to .sln/.slnx/.csproj
    public string DisplayName { get; }   // File name without extension
    public ProjectKind Kind { get; }     // Solution, SolutionX, or CSharpProject
}

public enum ProjectKind { Solution, SolutionX, CSharpProject }
```

- `IProjectContextService` exposes `IObservable<ProjectContext>` (via
  `WhenAnyValue` on a reactive property).
- Discovery: when `Workspace.WorkspacePath` changes, scan the root folder
  (non-recursive for Phase 8 — only the root level) for `.sln`, `.slnx`,
  `.csproj` files.
- No-project and ambiguous results are structured, not thrown.
- Selection: `SelectProject(ProjectCandidate)` or `SelectProject(null)` to clear.
- Reload: `ReloadAsync()` re-scans the workspace root.
- Phase 8 does NOT parse solution/project contents. It discovers files and
  exposes them as candidates. Parsing belongs to Phase 10 (LSP) and Phase 11
  (Build/Run/Test).

### D9: Settings UI Scope

**Decision:** Settings UI is part of Phase 8.1. It covers:
- Editor defaults (font family, font size, tab size, show whitespace, indent style)
- LLM configuration (endpoint URL, model, API key via secret store)
- Keybinding overrides (read-only list in 8.1; editing in 8.2 when command
  registry exists)

The settings UI is a new panel or dialog accessible from the main menu. It
follows the existing panel patterns (C# view construction per DESIGN.md Rule 1).

## Live Constraints To Respect

1. **`AgentExecutionOptions` env-var fallback must remain.** The V2 roadmap
   explicitly requires this. Environment variables take highest precedence.
2. **`AgentPanelState` must not gain provider/credential configuration.**
   This boundary is deliberate and must be preserved.
3. **`EditorTabViewModel`'s document operations must not break.** `Workspace`
   document ownership stays. The project context service is additive.
4. **All 817 existing tests must continue to pass** after each sub-phase.
   New services are injected via constructor — existing test helpers must be
   updated to provide mocks for new constructor parameters.
5. **DI composition is inline in `Program.cs`.** New services are registered
   inline following the existing pattern. No refactoring of the DI setup is
   in scope for Phase 8.
6. **`IndentGuideRenderer` and `IndentGuideMetrics` already read
   `textView.Options.IndentationSize`.** Editor settings must push values into
   AvaloniaEdit's options — these renderers will auto-adapt.

## Milestones (Umbrella)

| Milestone | Sub-phase | Description | Verification |
|-----------|-----------|-------------|--------------|
| **M0** | Umbrella | Lock all decisions in this plan. Verify `System.Text.Json` serialization round-trip with versioned schema. Verify `File.SetUnixFileMode` works for secret file permissions. Verify atomic write pattern (write tmp → rename) on Linux. | Proof-of-concept tests pass. Directory `docs/phases/v2/phase-8/` exists with this plan. |
| **M1–M6** | 8.1 | Settings foundation: `ISettingsService`, JSON persistence, migration, atomic writes, recovery, `ISecretStore`, editor settings, LLM settings migration, settings UI. | `dotnet build` + `dotnet test` green. Settings round-trip test. Migration test (v1→v2). Atomic write test. Secret store test. Editor settings consumed by `EditorView`. LLM settings consumed by `AgentExecutionOptions`. |
| **M7–M10** | 8.2 | Command registry + keybindings: `ICommandRegistry`, command descriptors, default keybindings, user overrides, window keybinding integration. | All existing commands registered with stable IDs. Keybindings resolved from registry. User override test. Build + tests green. |
| **M11–M14** | 8.3 | Authoritative project context: `IProjectContextService`, discovery, selection, lifecycle, observable state, status bar integration. | Discovery finds `.sln`/`.csproj` in test fixtures. No-project / single / ambiguous results tested. Observable state consumed by status bar. Build + tests green. |

## Likely Implementation Shape

### New Files (across all sub-phases)

**Settings (8.1):**
- `src/Services/ISettingsService.cs` — load, save, observe settings
- `src/Services/SettingsService.cs` — JSON-backed implementation
- `src/Services/SettingsSchema.cs` — versioned settings model
- `src/Services/SettingsMigration.cs` — ordered migration functions
- `src/Services/ISecretStore.cs` — secret boundary interface
- `src/Services/FileSecretStore.cs` — file-based implementation with `0600`
- `src/Services/EditorSettings.cs` — font, size, whitespace, indentation model
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
| `src/Views/EditorView.cs` | 8.1 | Replace hardcoded font/size/whitespace literals with `ISettingsService`-driven values. |
| `src/Views/TerminalRenderControl.cs` | 8.1 | Replace hardcoded font family/size with settings-driven values. |
| `src/Services/AgentExecutionOptions.cs` | 8.1 | Factory reads from settings + secret store, env-var fallback preserved. |
| `src/MainWindow.axaml.cs` | 8.2 | Replace imperative keybinding wiring with registry-driven binding. |
| `src/Views/TerminalPanel.cs` | 8.2 | Inline `KeyDown` handlers replaced with registry command calls where applicable. |
| `src/Views/FileTreeView.cs` | 8.2 | `Ctrl+Shift+H` handler replaced with registry command call. |
| All ViewModels with commands | 8.2 | Accept `ICommandRegistry` in constructor, register commands with stable IDs. |
| `src/ViewModels/MainWindowViewModel.cs` | 8.3 | Inject `IProjectContextService`. `Activate()` subscribes to project context changes. |
| `src/Views/StatusBar.cs` | 8.3 | Consume project context name instead of folder name. |

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

### Settings JSON Shape (v1)

```json
{
  "schemaVersion": 1,
  "editor": {
    "fontFamily": "Cascadia Code, Consolas, monospace",
    "fontSize": 14,
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
| Command registry injection breaks all ViewModel constructors | Each ViewModel gains one new constructor parameter (`ICommandRegistry`). All test helpers must be updated. Mitigate by doing all ViewModels in one milestone (8.2 M8) so tests are updated atomically. |
| Project discovery is slow on large folders | Phase 8 discovery is non-recursive (root level only). Scanning a folder for `*.sln`/`*.csproj` is fast. Recursive discovery is not in scope. |
| `AgentExecutionOptions` env-var fallback breaks during migration | Precedence order is explicit: env var → secret store → settings default → empty. Tests cover all four cases. |

## Exit Conditions

- [ ] **Settings:** Versioned settings file loads, saves, migrates, and recovers from corruption. Atomic writes verified. Unknown future version is refused, not overwritten.
- [ ] **Secrets:** API key is not in `settings.json`. `ISecretStore` provides get/set/delete. Env-var fallback works.
- [ ] **Editor:** No font family, font size, tab size, or whitespace flag is a hardcoded literal in View code. All driven by `ISettingsService`.
- [ ] **LLM:** `AgentExecutionOptions` populated from settings + secret store + env-var fallback. No plaintext API key in settings file.
- [ ] **Commands:** All existing commands registered with stable string IDs. `ICommandRegistry` provides lookup and execution.
- [ ] **Keybindings:** Window keybindings driven by registry + settings overrides. No hardcoded keybindings in view code (except text-input handlers like `Enter` in search boxes).
- [ ] **Project context:** `IProjectContextService` discovers `.sln`/`.slnx`/`.csproj` in workspace root. Observable state consumed by status bar. No-project / single / ambiguous results handled.
- [ ] **Workspace:** `Workspace` document ownership unchanged. `IProjectContextService` is additive. No parallel sources of project truth.
- [ ] **Tests:** All existing 817 tests pass. New tests cover settings, secrets, commands, keybindings, and project context.
- [ ] **Build:** `dotnet build Zaide.slnx --no-restore` — 0 warnings, 0 errors.
- [ ] **`AgentPanelState`** does not contain provider/credential configuration (grep-verified).

## Exact Next Step

Write `docs/phases/v2/phase-8/phase-8.1/IMPLEMENTATION_PLAN.md` — the detailed
plan for Settings Foundation (M1–M6). That plan locks the `ISettingsService`
interface shape, settings file path resolution, migration function signatures,
secret store implementation, editor settings integration with `EditorView`, and
the `AgentExecutionOptions` migration before any production code changes.

## Rollback Plan

- Commit hash to revert to: `8ee4513` (current HEAD — V1 complete, V2 not started)
