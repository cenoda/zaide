# Phase 8.2: Command Registry and Keybindings — Implementation Plan

## Scope

**Goal:** Implement the Phase 8.2 command registry and keybinding foundation:
stable command identifiers, command metadata, deterministic gesture resolution,
user overrides from `ISettingsService`, and registry-driven window keybindings.

**Dependencies:** Phase 8.1 and its post-closeout follow-up are complete. The
accepted current baseline is the 2026-07-11 verification snapshot:

- `dotnet build Zaide.slnx --no-restore` — 0 warnings, 0 errors.
- `dotnet test Zaide.slnx --no-build` — 935 passed, 0 failed, 0 skipped.
- `git diff --check` — clean.

**Out of scope:** Project/solution discovery and project context (Phase 8.3),
Command Palette UI (Phase 9), menu-bar work, parameterized item commands,
provider/tool/LSP work, and conflict-resolution UI.

## M0 Entry Gate and Live-Code Findings

M0 is documentation-only. No production code or test behavior changes are
authorized by M0.

The live-code audit for this plan confirmed:

- `src/Services/ICommandRegistry.cs`, `CommandRegistry.cs`, and
  `CommandDescriptor.cs` do not yet exist.
- `src/MainWindow.axaml.cs` currently wires `Ctrl+Oem3`, `Ctrl+J`, `Ctrl+S`,
  and `Ctrl+O` imperatively through Avalonia `KeyBindings`.
- `src/Views/FileTreeView.cs` currently handles `Ctrl+Shift+H` directly in its
  `KeyDown` handler.
- `MainWindowViewModel` already owns the parameterless command seams needed by
  this phase, including save, open-folder, toggle-bottom-panel, and close-folder
  commands. `FileTreeViewModel.ToggleHiddenFilesCommand` is the existing hidden
  files seam.
- `SettingsModel.Keybindings` is currently an empty immutable placeholder, and
  `ISettingsService.WhenChanged` is the existing settings-change notification
  seam. Phase 8.2 must extend this contract without moving secrets or changing
  settings ownership.
- `Program.cs` is the DI registration seam. The registry is a singleton and must
  be available before `MainWindow` performs initial command registration and
  keybinding resolution.

M0 exit gate:

- [x] Phase 8.1 baseline and current live seams verified.
- [x] The Phase 8 umbrella decisions D5, D6, D6a, and D10 are the governing
      contracts for this plan.
- [x] Phase 8.3 and Phase 9 boundaries are explicit.
- [x] Milestones M7–M10 and their verification gates are locked below.

## Implementation Contract

### Command descriptors and registry

Add the following new files under `src/Services/`:

- `ICommandRegistry.cs` — command registry interface (D5).
- `CommandRegistry.cs` — singleton implementation.
- `CommandDescriptor.cs` — command metadata (D5).
- `ResolvedKeyBinding.cs` — neutral gesture→command resolution record (D5):

```csharp
public sealed record ResolvedKeyBinding(string Gesture, string CommandId);
```

`CommandDescriptor` must match the umbrella D5 contract exactly:

```csharp
public sealed class CommandDescriptor
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public IReadOnlyList<string> DefaultGestures { get; }
    public ICommand Command { get; }

    public CommandDescriptor(
        string id,
        string displayName,
        string category,
        IReadOnlyList<string> defaultGestures,
        ICommand command);
}
```

The constructor validates non-empty `Id` and `Category`. `DefaultGestures` may
be empty for an intentionally unbound command. The registry must:

- reject duplicate command IDs with `InvalidOperationException`;
- expose registration-order-independent `GetAll()` and `GetById(string)`
  behavior without inventing a second command source;
- execute parameterless commands through `Execute(string)` by calling
  `CanExecute(null)` before `Execute(null)`;
- return `false` and write a debug trace for unknown or unavailable commands;
- expose `Execute<T>(string, T)` for explicitly parameterized callers. When the
  underlying `ICommand` cannot accept `T` (`CanExecute(parameter)` returns false
  because the parameter type doesn't match), `Execute<T>` returns `false` and
  logs a debug trace — it never throws, coerces types, or infers parameters;
- remain UI-framework neutral. Avalonia `KeyBinding` objects are created only
  by the window integration layer.

### Canonical command table

The following table is authoritative for Phase 8.2. Every registered global
command must have an explicit row; no implicit default gesture is allowed.

| Command ID | Display name | Category | Default gesture(s) | Existing command seam |
|---|---|---|---|---|
| `file.save` | Save | File | `Ctrl+S` | `MainWindowViewModel.SaveActiveTabCommand` |
| `workspace.openFolder` | Open Folder | Workspace | `Ctrl+O` | `MainWindowViewModel.OpenFolderCommand` |
| `workspace.closeFolder` | Close Folder | Workspace | unbound | `MainWindowViewModel.CloseFolderCommand` |
| `view.toggleBottomPanel` | Toggle Bottom Panel | View | `Ctrl+Oem3`, `Ctrl+J` | `MainWindowViewModel.ToggleBottomPanelCommand` |
| `explorer.toggleHiddenFiles` | Toggle Hidden Files | Explorer | `Ctrl+Shift+H` | `FileTreeViewModel.ToggleHiddenFilesCommand` |
| `sourcecontrol.commit` | Commit | Source Control | unbound | `SourceControlViewModel.CommitCommand` |
| `sourcecontrol.refresh` | Refresh | Source Control | unbound | `SourceControlViewModel.RefreshCommand` |

`Ctrl+Oem3` is the canonical neutral token for the backtick/tilde key and must
map to Avalonia `Key.Oem3` with `KeyModifiers.Control`. `Ctrl+J` is an alias for
the same command, not a separate command.

The following remain ViewModel-local and must not be registered: terminal
internal commands, file-tree expand/collapse and item commands, stage/copy/open
commands that require a specific item, one-way panel hiding, sidebar navigation,
and Townhall text-input send.

### Gesture resolution

Implement the three-layer resolution policy:

1. A valid user override wins over defaults for that command.
2. If there is no override, each valid default gesture contributes a binding.
3. Empty defaults produce an unbound command.

Use the neutral grammar `Ctrl`, `Alt`, `Shift`, `Meta` plus one Avalonia `Key`
enum name, case-insensitively. Unknown keys, malformed gestures, overrides for
unregistered command IDs, and invalid override values are ignored and logged;
they must not prevent startup.

Gesture conflicts are deterministic and registration-order independent:

- user-override conflicts are resolved by lexicographically earlier command ID;
- default-gesture conflicts are resolved by lexicographically earlier command
  ID after overridden commands have been applied;
- no gesture may resolve to two command IDs;
- the losing command remains registered but is unbound for that gesture;
- Phase 8 has no conflict-resolution UI.

`ResolveKeyBindings(ISettingsService settings)` returns neutral
`ResolvedKeyBinding` records. It must be safe to call again after a settings
change and must return a complete replacement set rather than incremental
duplicates.

### Window integration and refresh

Register `ICommandRegistry`/`CommandRegistry` as a singleton in `Program.cs`.
The owning ViewModels register their commands in their constructors, as required
by D5: `MainWindowViewModel` owns the window commands, `FileTreeViewModel` owns
the explorer command, and `SourceControlViewModel` owns the source-control
commands. `MainWindow` — which is `ReactiveWindow<MainWindowViewModel>` — only
resolves the full set during `WhenActivated`, after those ViewModels have
registered, converts neutral gestures to Avalonia `KeyBinding` instances, and
removes/replaces the previous generated bindings as one operation. It must not
become a second command-registration owner.

Registry-owned `KeyBinding` instances are tracked in a dedicated
`List<KeyBinding>` field on `MainWindow`. Before each materialization pass the
list is cleared from `Window.KeyBindings` and then repopulated from scratch.
This isolates them from any view-local or unrelated bindings and guarantees
clean replacement without enumeration-during-modification hazards.

The imperative command-specific binding blocks in `MainWindow.axaml.cs` must be
replaced by registry-driven materialization. The direct `Ctrl+Shift+H` handling
in `FileTreeView.cs` must be removed; the registry-owned window binding invokes
the command registered by `FileTreeViewModel`, so there is no second global
keybinding source. The view-local Enter/open-file behavior remains local.

When `ISettingsService.WhenChanged` emits a new snapshot, the window captures
the snapshot value synchronously, then reruns resolution and replaces only the
registry-owned framework bindings. If multiple snapshots arrive during a
resolution pass, only the latest one's bindings are kept — the intermediate
ones are skipped. Existing unrelated or view-local input behavior must not be
removed.

Unavailable commands, including `workspace.closeFolder` while no folder is
open, are no-ops and do not throw. Their resolved gesture remains owned by the
command layer if a future override assigns one, so the gesture does not fall
through to text input.

### Settings schema prerequisite (M7, before M8)

`KeybindingOverrides` is currently an empty placeholder record:

```csharp
public sealed record KeybindingOverrides
{
    public static readonly KeybindingOverrides Empty = new();
}
```

Before M8 resolution can read user overrides from `ISettingsService`, the
placeholder type must be removed and the settings model must hold a
`commandId → neutralGesture` map. The target JSON shape is flat for user
readability:

```json
"keybindings": {
    "file.save": "Ctrl+Shift+S",
    "explorer.toggleHiddenFiles": ""
}
```

Each key is a registered command ID; an empty-string value unbinds the command.
To make this flat shape work with `SettingsModel`, change the `Keybindings`
property on `SettingsModel` from `KeybindingOverrides` to:

```csharp
IReadOnlyDictionary<string, string> Keybindings
```

And remove the `KeybindingOverrides` type entirely. The default is
`new Dictionary<string, string>().AsReadOnly()`. Because Phase 8.1 requires
deeply immutable settings snapshots, deserialization and every candidate
publication must defensively copy the incoming dictionary into a read-only
wrapper; exposing it as `IReadOnlyDictionary` alone is not sufficient. The
`SettingsSerializer` already rejects a null `Keybindings` section; the combined
null-guard on L79 (`result.Keybindings is null`) must be updated to check the
new type and normalize it before returning the model. This expansion ships in
M7 so M8 resolution has a complete immutable settings contract to consume. A
custom `JsonConverter` is **not** needed — `System.Text.Json` serializes the
flat dictionary shape natively; normalization is a snapshot-boundary concern.

## Milestones

| Milestone | Description | Verification |
|---|---|---|
| **M7** | Command registry core: `CommandDescriptor`, `ResolvedKeyBinding`, `ICommandRegistry`/`CommandRegistry`, stable IDs, registration, lookup, execution, unavailable-command behavior, `SettingsModel.Keybindings` changed from `KeybindingOverrides` to `IReadOnlyDictionary<string,string>`, `KeybindingOverrides` type removed, and DI registration. | Focused registry tests cover duplicate IDs, lookup, parameterless execution, typed execution, unknown IDs, unavailable commands, keybindings dictionary round-trip serialization, and singleton resolution. |
| **M8** | Canonical command registration from the owning ViewModel constructors and neutral gesture resolution: locked table, parser/validation, aliases, user overrides, deterministic conflicts, and invalid-input logging. `MainWindow` only resolves after all constructor registrations are complete. | Focused resolution tests cover every locked default, especially `Ctrl+Oem3` → `view.toggleBottomPanel`, aliases, override precedence, lexicographic conflict winners, malformed gestures, and unknown command overrides. |
| **M9** | Window integration: replace imperative global bindings with registry materialization, keep the View-local Enter/open-file behavior local, remove the duplicate `FileTreeView` global handler, and refresh generated bindings after settings changes. | Integration tests or focused seam tests prove generated binding replacement, settings-driven refresh, no duplicate bindings after repeated resolution, and `Ctrl+Shift+H` registry execution. **Manual desktop smoke pass/fail criteria:** (a) `Ctrl+Oem3` and `Ctrl+J` toggle the bottom panel, (b) `Ctrl+S` saves the active tab, (c) `Ctrl+O` opens the folder picker, (d) `Ctrl+Shift+H` toggles hidden files in the file tree, (e) after a settings change that rebinds a gesture, the old binding is removed and only the new binding fires, (f) no duplicate bindings appear in the running application after repeated resolution or settings changes. |
| **M10** | Phase 8.2 closeout: audit scope, truth-sync affected docs, run the sequential full verification, and record manual evidence and any explicit limitations. | `dotnet build Zaide.slnx --no-restore`, then `dotnet test Zaide.slnx --no-build`, then `git diff --check`; all canonical gesture coverage and registry tests green. |

## Required Test Matrix

- Descriptor validation rejects empty IDs/categories and preserves immutable
  metadata.
- Duplicate registration throws and does not replace the original command.
- `GetAll`, `GetById`, `Execute`, and `Execute<T>` cover registered, unknown,
  unavailable, and wrong-parameter cases.
- Every canonical table row is registered exactly once.
- Every locked default gesture resolves to the expected command, including both
  `Ctrl+Oem3` and `Ctrl+J` for `view.toggleBottomPanel`.
- User overrides take precedence over defaults and support unbound commands.
- User and default gesture conflicts select the lexicographically earlier ID and
  emit a warning for the loser.
- Invalid gesture strings and overrides for missing command IDs are ignored and
  logged without startup failure.
- Repeated resolution returns no duplicate gesture bindings.
- Settings changes replace generated bindings rather than accumulating them.
- An unavailable command is a no-op and does not throw.
- Existing local parameterized commands remain outside the registry.

## Limitations by Design

- Keybinding override editing UI is not part of Phase 8.2; the registry consumes
  the persisted settings contract and later UI may edit it.
- Conflicts are logged only. User-facing conflict resolution is deferred to the
  Command Palette work in Phase 9.
- The registry does not discover commands dynamically and does not register
  parameterized item actions.
- Project context, solution discovery, and unload/reload state remain Phase 8.3.
- Meta-key behavior is defined by the neutral grammar but is not required for
  Linux-primary manual acceptance.
- Gesture parsing is implemented as an internal helper inside `CommandRegistry`;
  a separate `GestureParser` type is deferred until complexity warrants it.
- `SourceControlViewModel.RefreshCommand` always reports `CanExecute == true`
  even when no workspace is open. Since `sourcecontrol.refresh` is unbound (no
  keybinding) in Phase 8.2, this is harmless. A `canExecute` guard
  (`_workspace.WorkspacePath is not null`) is deferred to the phase that first
  binds a gesture or surfaces this command in the Command Palette.

## Exit Conditions

- [ ] M7–M9 are complete with isolated commits and focused tests.
- [ ] `SettingsModel.Keybindings` is `IReadOnlyDictionary<string, string>` (flat
      JSON) and round-trips through serialization; `KeybindingOverrides` type is
      removed.
- [ ] All canonical global commands are registered with stable IDs exactly once.
- [ ] Canonical commands are registered by their owning ViewModel constructors;
      `MainWindow` performs resolution/materialization only.
- [ ] Resolution follows D6/D6a deterministically and uses the settings service
      for overrides.
- [ ] Main-window global keybindings are registry-driven; no duplicate imperative
      keybinding source remains for the canonical gestures.
- [ ] Phase 8.3, Phase 9, and unrelated parameterized commands remain untouched.
- [ ] `dotnet build Zaide.slnx --no-restore` reports 0 warnings / 0 errors.
- [ ] `dotnet test Zaide.slnx --no-build` is green.
- [ ] `git diff --check` is clean and manual desktop evidence is recorded.

## Rollback Plan

Revert only the isolated Phase 8.2 milestone commit that introduced the unsafe
behavior. Restore the Phase 8.1.7 green baseline (`c357172` and its accepted
ancestors), then leave later milestones unstarted until the registry or binding
seam is corrected. Do not revert Phase 8.1 settings or provider work as part of
an 8.2 rollback.
