# Phase 8.1: Settings Foundation — Implementation Plan

## Pre-Implementation Verification

- [x] Re-read the Phase 8 umbrella plan, especially decisions D1–D4b, D7, and
      D9. This document implements only their Phase 8.1 contracts.
- [x] Confirm the current composition and ownership seams before changing them:
      `Program.cs`, `Workspace`, `MainWindowViewModel`, `FileTreeViewModel`,
      `MainWindow`, `EditorView`, `TerminalTabHost`, `TerminalPanel`,
      `TerminalRenderControl`, and `StatusBar`.
- [x] Confirm the existing `AgentExecutionService` tests and all direct
      construction call sites before changing its constructor from static options
      to live settings/secret resolution.
- [x] Confirm `FileStreamOptions.UnixCreateMode` is available on the target
      .NET version before implementing restrictive secret-file creation.
- [x] Run the baseline sequentially and record the actual output in the
      implementation closeout: `dotnet build Zaide.slnx --no-restore`, then
      `dotnet test Zaide.slnx --no-build`.

## Scope

**Goal:** Deliver the Phase 8.1 settings foundation: immutable versioned
settings, recovery-safe persistence, a file-backed secret boundary, live LLM
configuration, immediate editor and terminal settings application, a reachable
close-workspace path, and a Settings panel.

**Boundaries:**

- This is the Phase 8.1 **delivery plan**. Production implementation is split
  into the five child plans below; M6 remains the Phase 8.1 closeout gate. It
  does not reopen umbrella M0 or start Phase 8.2 or Phase 8.3 work.
- Do not add `ICommandRegistry`, command descriptors, default-gesture
  migration, user-editable keybindings, or a command palette. The local
  `CloseFolderCommand` exists only as the Phase 8.1 close-folder seam; its
  registry registration is Phase 8.2 work.
- Do not create `IProjectContextService`, scan for solutions/projects, or
  implement project selection/load/unload state. Phase 8.1 only raises
  `Workspace.WorkspaceFolderChanged` so Phase 8.3 can consume it later.
- Do not add LSP, build/run/test execution, a menu bar, broad provider support,
  OS-native keychain integration, or persistence for Townhall/activity data.
- Do not move provider or credential configuration into `AgentPanelState`.
- This planning change makes no production-code change. Production work begins
  only when an implementation milestone is explicitly authorized.

## Locked Contracts

### Settings model and service

- `SettingsModel` and all nested settings types are deeply immutable records.
  `ISettingsService.Current` is a never-null, frozen snapshot; consumers use
  `with` expressions to form candidates and cannot mutate a published snapshot.
- `ISettingsService` exposes `Current`, `WhenChanged`, `LoadResult`,
  `UpdateAsync`, `ApplyAsync`, `SaveAsync`, and `WriteErrors`. All asynchronous
  APIs accept an optional `CancellationToken`.
- `WhenChanged` and `WriteErrors` are thread-neutral: the service emits on the
  committing/writer thread and does not reference Avalonia dispatchers or Rx
  schedulers. UI subscribers apply
  `.ObserveOn(RxApp.MainThreadScheduler)` before raising UI-bound properties.
- A private async mutation gate serializes each `UpdateAsync` producer's full
  read–modify–validate–publish transaction. `ApplyAsync(expected, next)` uses
  the same gate and returns `Conflict` when its expected base snapshot is stale;
  it never overwrites a concurrent change. Invalid candidates also leave
  `Current` unchanged and return field-level validation errors.
- A valid mutation updates `Current` before publishing `WhenChanged`, increments
  a generation using `Interlocked`, and queues persistence through one
  generation-aware writer. Older queued writes complete as `Superseded`; a disk
  failure completes as a failed save result and also emits `WriteErrors`.
  Mutation tasks do not fault solely because persistence failed.
- Cancellation before the mutation gate is acquired (or before `SaveAsync`
  enqueues) cancels without changing state. Cancellation after commit/enqueue
  is ignored: the committed snapshot receives one deterministic `Saved`,
  `Superseded`, or `Failed` result and is never rolled back.

### Persistence, migration, and recovery

- Settings are JSON at `{XDG_CONFIG_HOME}/zaide/settings.json` on Linux when
  `XDG_CONFIG_HOME` is absolute; otherwise use `$HOME/.config/zaide`, then the
  platform application-data directory. Windows and macOS use platform
  application-data under `zaide` and do not interpret `XDG_CONFIG_HOME`.
- Schema v1 contains immutable editor, LLM, and keybinding-override data. The
  initial production migration list is empty, but ordered pure migration
  infrastructure is implemented and tested with a synthetic test migration.
- Save by writing a same-directory temporary file then
  `File.Move(temp, settings, overwrite: true)`. A successful load refreshes
  `settings.json.lastknowngood`.
- Missing, corrupt, invalid-schema, unsupported-old, and unknown-future files
  fall back to last-known-good or defaults as appropriate. Invalid and
  unsupported source files are never overwritten during fallback.

### Secrets and LLM configuration

- `ISecretStore` is synchronous: `Get`, `Set`, and `Delete`. Its file-backed
  implementation stores secrets separately in `secrets.json`; no API-key value
  is ever serialized to `settings.json`.
- On Linux, create secret temp files with `FileStreamOptions.UnixCreateMode`
  set to owner read/write (`0600`) before writing any bytes. Rename preserves
  that mode. On every Linux load, repair a pre-existing non-`0600` secret file
  and log a warning. Windows and macOS retain platform-default ACL behavior;
  OS-native keychain work is deferred.
- `AgentExecutionService` takes `HttpClient`, `ISettingsService`, and
  `ISecretStore`; it builds a fresh immutable `AgentExecutionOptions` for every
  `ExecuteAsync`. Precedence is `AGENT_API_URL` / `AGENT_API_KEY` /
  `AGENT_MODEL` environment values, then secret store for the API key, then
  saved settings, then empty. `AgentExecutionOptions` is no longer a DI
  singleton.

### Workspace, runtime settings, and Settings UI

- `Workspace.SetProjectFromPath()` updates `WorkspacePath` and `ProjectName`
  before raising parameterless `WorkspaceFolderChanged`. A null path is the
  supported close-workspace transition; document/tab ownership remains in
  `Workspace` and open documents are retained.
- `FileTreeViewModel.SetRootPath(string? path)` is the sole public writer for
  `RootPath`; the property setter becomes private. Closing disposes its watcher
  subscription, calls `IFileTreeService.StopWatching()`, clears `RootNodes`,
  `SelectedFile`, and status state, then publishes null. Opening validates and
  enumerates before tearing down the existing watcher.
- `MainWindowViewModel.CloseFolderCommand` is a local parameterless command
  enabled only while `RootPath` is non-null. `FileTreeViewModel` exposes
  `CloseFolderRequested`; `MainWindowViewModel.Activate()` bridges it to the
  command and always completes the interaction. Its existing RootPath
  subscription must no longer filter null transitions.
- `EditorView`, `TerminalTabHost`, `TerminalPanel`, and `TerminalRenderControl`
  receive `ISettingsService` through the concrete `MainWindow.BuildLayout()`
  construction chain. Each applies `Current` at construction and observes
  future snapshots on the UI scheduler. Terminal font changes update mutable
  typeface/font-size state, recompute cell metrics, and invalidate layout and
  rendering without reconstructing the panel.
- `StatusBarViewModel` is a DI singleton child ViewModel that forwards the
  real existing status sources from `MainWindowViewModel` and exposes
  `OpenSettingsCommand`. `MainWindowViewModel.ShowSettings` is an
  `Interaction<Unit, bool>` handled by `MainWindow`; it creates a transient
  `SettingsViewModel(ISettingsService, ISecretStore)` and a C# Settings panel
  overlay. Closing the overlay disposes that transient ViewModel.

## Milestones

| Milestone | Child plan | Description | Verification |
|-----------|------------|-------------|--------------|
| **M1** | [`phase-8.1.1`](phase-8.1.1/IMPLEMENTATION_PLAN.md) | Settings Core: immutable settings, persistence, migration infrastructure, recovery, and queued writes. | Child-plan gates pass. |
| **M2** | [`phase-8.1.2`](phase-8.1.2/IMPLEMENTATION_PLAN.md) | Secrets & Live LLM: separate secret store and per-request LLM configuration resolution. | Child-plan gates pass. |
| **M3** | [`phase-8.1.3`](phase-8.1.3/IMPLEMENTATION_PLAN.md) | Workspace Close Lifecycle: reachable close-folder flow, cleanup, and notification seam. | Child-plan gates pass. |
| **M4** | [`phase-8.1.4`](phase-8.1.4/IMPLEMENTATION_PLAN.md) | Runtime Editor & Terminal Settings: concrete settings injection and subscription disposal. | Child-plan gates pass. |
| **M5** | [`phase-8.1.5`](phase-8.1.5/IMPLEMENTATION_PLAN.md) | Settings UI: status-bar bridge, transient settings panel, editing and disposal. | Child-plan gates pass. |
| **M6** | Phase 8.1 parent | Integrate M1–M5, truth-sync documentation, and run the full regression/acceptance sweep. Do not add Phase 8.2 or 8.3 behavior. | **Complete (2026-07-11).** `dotnet build Zaide.slnx --no-restore` 0 warnings/0 errors; `dotnet test Zaide.slnx --no-build` 895/895 green; `git diff --check` clean; every blocking test present and passing. |

### Accepted milestone commits

| Milestone | Commit | Subject |
|-----------|--------|---------|
| **M1** | `3e4d0e3` (+ `60d9dcd` review fixup) | Phase 8.1.1 M1: Settings Core |
| **M2** | `6a2e3fc` | Phase 8.1.2 M2: Secrets and Live LLM Configuration |
| **M3** | `9ef6b0b` | Phase 8.1.3 M3: Workspace Close Lifecycle |
| **M4** | `7619645` | Phase 8.1.4 M4: Runtime Editor & Terminal Settings |
| **M5** | `4d438af` | Phase 8.1.5 M5: Settings UI |

M6 is a documentation-only closeout on top of the accepted `4d438af` M5
baseline; it introduced no production-code or test changes.

## Blocking Test Matrix

- `Phase8ProofOfConceptTests`: JSON/schema validation, atomic persistence,
  last-known-good recovery, migration infrastructure, and rejection without
  overwriting invalid or unsupported source files.
- Immutable snapshot tests: `with`-based whole-snapshot commits, validation
  rejection, concurrent disjoint-field updates, stale-Apply conflict behavior,
  generation-aware queued writes, cancellation before/after commit boundaries,
  write-error publication, and explicit retry success.
- `LiveLlmConfigTests`: changing saved LLM settings and secret data affects the
  next `AgentExecutionService.ExecuteAsync` without reconstruction; environment
  variables still override persisted configuration.
- Secret-mode tests: `UnixCreateMode = 0600` at creation, mode retention across
  rename, and repair of an existing loose-mode file on Linux.
- Close-workspace tests: interaction bridge completion, null root-path flow,
  watcher/tree cleanup, event ordering, Source Control reset, and retained
  documents.
- Runtime typography tests: editor options update and terminal cell metrics are
  recalculated without recreating a terminal panel.
- Settings UI/disposal tests: gear bridge opens the overlay, transient
  `SettingsViewModel` is disposed on removal, and terminal/editor subscriptions
  are released by their owners.

## Phase 8.1 Limitations (by design)

- Keybinding overrides may be represented in the schema but are read-only in
  this UI; command registration and editing are Phase 8.2.
- `WorkspaceFolderChanged` is only a notification seam in this phase. Project
  discovery and unload handling are Phase 8.3.
- The settings panel is a slide-over opened from the status bar; no menu bar or
  command palette is introduced.
- The file secret store is the Linux-primary protection boundary. Native
  keychains and non-Linux permission hardening are deferred.
- Only editor/terminal preferences defined by the Phase 8 umbrella are
  configurable. General chrome, panel, caption, and control typography remains
  governed by the existing style system.

## Exit Conditions

- [x] M1–M6 are complete and no Phase 8.2 or 8.3 production work was added.
- [x] Settings load, validate, migrate, persist atomically, surface errors, and
      recover without overwriting rejected source files.
- [x] API keys are absent from `settings.json`, and effective LLM configuration
      is resolved live for every request with environment precedence preserved.
- [x] A user can close an open folder through the file-tree affordance without
      losing open documents; all specified watcher and workspace notifications
      occur in order.
- [x] Editor and terminal settings apply at construction and immediately after
      a committed change; all subscriptions have a defined disposal owner.
- [x] The Settings panel opens through the status bar bridge and disposes its
      transient ViewModel when closed.
- [x] All blocking tests pass, `dotnet build Zaide.slnx --no-restore` reports
      0 warnings / 0 errors, and `dotnet test Zaide.slnx --no-build` is green.
- [x] The actual closeout test count and any manual verification evidence are
      recorded before this plan is marked complete.

## M6 Closeout (2026-07-11)

Phase 8.1 (Settings Foundation) is **complete**. M6 performed a read-only
integration audit of M1–M5 against the parent contracts, confirmed every
Blocking Test Matrix item still has a present, passing test, and ran the
verification sweep sequentially. No production code or test behavior was
changed in M6; only documentation was truth-synced.

### Verification results (run in order)

- `dotnet build Zaide.slnx --no-restore` — **0 warnings, 0 errors.**
- `dotnet test Zaide.slnx --no-build` — **895 passed, 0 failed, 0 skipped,
  895 total.**
- `git diff --check` — **clean** (no whitespace or conflict-marker errors).

### Integration audit (accepted)

- **Settings persistence/migration/recovery and mutation semantics:** immutable
  snapshots, `with`-based whole-snapshot commits, validation rejection, disjoint
  concurrent updates, stale-`Apply` conflict, generation-aware queued writes,
  cancellation before/after commit, write-error publication, and retry are
  covered by `SettingsCoreTests`; JSON/schema, atomic temp-then-rename,
  last-known-good recovery, migration infrastructure, and non-overwrite of
  rejected sources by `Phase8ProofOfConceptTests`.
- **Secret isolation, Linux permission behavior, and live LLM precedence:**
  `SecretStoreTests` (get/set/delete, `settings.json` isolation),
  `FileSecretStorePermissionTests` (`0600` creation, mode retention across
  rename, loose-mode repair, external-chmod repair on subsequent access), and
  `LiveLlmConfigTests` (live settings/secret pickup without recreation; env-var
  precedence for URL, model, and key).
- **Close-folder lifecycle and retained documents:** `WorkspaceTests`
  (`WorkspaceFolderChanged` on open/close, null-path ordering, retained
  documents), `FileTreeViewModelTests` (null `SetRootPath` cleanup,
  `StopWatching`, watcher-subscription disposal, failed-open preservation,
  interaction completion), and `MainWindowViewModelTests` (command → interaction
  bridge, Source Control reset to `NotARepository`, retained tabs, disabled
  command when no folder is open).
- **Editor/terminal runtime settings and subscription ownership:**
  `Phase814SettingsTests` (initial + live editor option projection, prose/code
  font selection, terminal `ApplyFontSettings` metric recompute and
  invalidation without surface recreation, exactly-once disposal on removal /
  host replacement / detach / window close) and
  `TerminalPanelSubscriptionsTests`.
- **Status-bar/settings-panel interaction, scheduler delivery, and transient
  disposal:** `M5SettingsUiTests` (real `MainWindow` `ShowSettings` bridge,
  panel open/close, injected-scheduler delivery, exactly-once transient
  disposal) and `SettingsViewModelTests` (inline field validation, apply,
  discard, stale conflict + rebase, empty-key deletion without leaking into
  settings, owned-subscription disposal).

### Blocking Test Matrix → present tests

| Matrix item | Test location |
|-------------|---------------|
| `Phase8ProofOfConceptTests` | `tests/Zaide.Tests/Services/Phase8ProofOfConceptTests.cs` |
| Immutable snapshot tests | `tests/Zaide.Tests/Services/SettingsCoreTests.cs` |
| `LiveLlmConfigTests` | `tests/Zaide.Tests/Services/LiveLlmConfigTests.cs` |
| Secret-mode tests | `tests/Zaide.Tests/Services/FileSecretStorePermissionTests.cs`, `tests/Zaide.Tests/Services/SecretStoreTests.cs` |
| Close-workspace tests | `tests/Zaide.Tests/Models/WorkspaceTests.cs`, `tests/Zaide.Tests/ViewModels/FileTreeViewModelTests.cs`, `tests/Zaide.Tests/MainWindowViewModelTests.cs` |
| Runtime typography tests | `tests/Zaide.Tests/Views/Phase814SettingsTests.cs` |
| Settings UI/disposal tests | `tests/Zaide.Tests/Views/M5SettingsUiTests.cs`, `tests/Zaide.Tests/ViewModels/SettingsViewModelTests.cs`, `tests/Zaide.Tests/Views/TerminalPanelSubscriptionsTests.cs` |

### Deferred manual desktop verification

Carried forward from M4/M5 and still deferred (not blocking for automated
closeout): visually confirm live editor font/whitespace changes, Markdown prose
versus code font rendering, terminal font-resize behavior, the settings
slide-over animation and focus behavior, and final-window-close cleanup in a
real desktop session.

## Rollback Plan

- Commit hash to revert to: the pre-M1 implementation baseline is `2a0cdb2`
  ("Harden Phase 8.1 settings contracts"), the last commit before M1
  (`3e4d0e3`). The accepted Phase 8.1 closeout baseline is `4d438af` (M5).
- If an individual milestone is unsafe, revert only that milestone's commit,
  restore the prior green baseline, and keep later milestones unstarted until
  its contract is revised.
