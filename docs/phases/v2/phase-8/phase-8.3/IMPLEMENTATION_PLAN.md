# Phase 8.3: Authoritative Project Context — Implementation Plan

## Pre-Implementation Verification

- [x] Phase 8 umbrella decisions D7 and D8 reviewed against the live checkout.
- [x] `Workspace.WorkspaceFolderChanged` is present and raised after
      `WorkspacePath`/`ProjectName` updates, including the null close transition.
- [x] `FileTreeViewModel.SetRootPath(string?)` is the sole `RootPath` writer and
      preserves validate-before-teardown behavior for failed opens.
- [x] `MainWindowViewModel.Activate()` observes both open and close transitions
      and remains the existing workspace/Source Control ownership seam.
- [x] `ISettingsService` exposes immutable `Current`; project context will
      consume settings without adding a second persistence model.
- [x] `StatusBarViewModel` currently projects `WorkspaceProjectName` from the
      folder-oriented `Workspace` seam; 8.3 must replace that project label with
      the selected project-context name without moving project ownership into the
      status bar.
- [x] M0 must prove the selected discovery/file-classification approach with
      the live target framework, publish `M0_DISCOVERY_PROOF.md`, and define
      the exact test seams before M1.

## Scope

**Goal:** Add one UI-independent, cancellable, observable project-context
service that discovers supported C# solution/project inputs in the opened
workspace, exposes structured selection and lifecycle state, and supplies the
authoritative project name to the status bar.

**Boundaries:**

- Scan only the opened workspace root; do not recursively discover projects.
- Support `.sln`, `.slnx`, and `.csproj`. Classify known unsupported project
  extensions separately from no-project and I/O-failure results.
- Do not parse solution/project contents or create a second `Workspace` model.
- Do not implement LSP, Build/Run/Test, debugging, project loading by an SDK,
  or multi-project orchestration.
- Keep the service free of Avalonia and Rx scheduler dependencies. UI-bound
  consumers marshal `WhenChanged` at their subscription site.

## Milestones (Incremental)

| Milestone | Description | Test |
|---|---|---|
| **M0** | ✅ Entry gate: verify the live `Workspace`, `FileTreeViewModel`, `MainWindowViewModel`, `Program`, and `StatusBarViewModel` seams; lock the field-level state contract, transition/emission rules, discovery classification, injectable discovery seam, cancellation/stale-load policy, selection rules, ownership, logger seam, and exact verification commands. No production implementation. Produce `M0_DISCOVERY_PROOF.md` as the review artifact. | Add a test-only `Phase83M0DiscoveryProofTests` seam and run `dotnet test Zaide.slnx --filter FullyQualifiedName~Phase83M0DiscoveryProofTests`; acceptance output is exactly `1 passed, 0 failed`, followed by `git diff --check` with no output. Then confirm the sequential full gates: `dotnet build Zaide.slnx --no-restore`, `dotnet test Zaide.slnx --no-build`. |
| **M1** | ✅ Project-context contracts and root-level discovery: add immutable `ProjectContext`, `ProjectCandidate`, `ProjectKind`, `ProjectContextState`, `IProjectContextService`, and framework-neutral discovery. Implement supported/unsupported/unrelated classification, deterministic ordering, `NoProject`, `Unsupported`, `SingleProject`, `Ambiguous`, and `Failed` results. | Focused discovery tests cover every extension class, mixed supported/unsupported files, empty roots, deterministic candidate ordering, missing/permission-failure behavior, and structured results. |
| **M2** | ✅ Lifecycle and cancellation: implement singleton `ProjectContextService` with `LoadAsync`, `ReloadAsync`, `UnloadAsync`, immutable `Current`, thread-neutral `WhenChanged`, cancellation that is not `Failed`, and sequence protection so stale loads cannot publish. | Service tests cover the exact transition/emission matrix below: initial-load cancellation before `Loading`, cancellation after `Loading`, reload cancellation, overlapping loads, stale-result suppression, explicit unload, and thread-neutral `WhenChanged`. |
| **M3** | ✅ Workspace, ownership, selection, and logging integration: subscribe to `Workspace.WorkspaceFolderChanged`, load on non-null paths, unload on null, observe/log background failures, dispose the event subscription, and implement `SelectProject` with current-snapshot candidate validation. Register the service in `Program.cs`; inject it into `MainWindowViewModel`; let DI construct the singleton, its constructor perform startup reconciliation, and the application exit path dispose it. | Integration tests cover open/close event routing, startup reconciliation, disposal/unsubscription, reload, valid selection, clearing selection, stale/foreign candidate rejection, and logger events. DI tests prove one singleton and no Avalonia dependency. Logger tests use an injectable `ILogger<ProjectContextService>` test provider and assert warning/error event IDs. |
| **M4** | ✅ UI projection and closeout: `MainWindowViewModel` owns the UI-thread projection of `IProjectContextService.Current`; `StatusBarViewModel` projects that property instead of `WorkspaceProjectName`. Display a selected/single-project name and truthful state text for every non-project state. Truth-sync affected docs and record limitations/evidence. | View-model/status-bar tests cover every display state (19 focused tests, 0 failed), selected-project updates, unload/close, and non-UI service emissions. Full suite: 1185 passed, 0 failed. Build: 0 warnings, 0 errors. `git diff --check`: clean. |

## Project Context Contract

`IProjectContextService.Current` is one immutable `ProjectContext` snapshot with
exactly these fields:

```csharp
public sealed record ProjectContext(
    ProjectContextState State,
    string? WorkspaceRoot,
    IReadOnlyList<ProjectCandidate> Candidates,
    ProjectCandidate? SelectedProject,
    IReadOnlyList<string> UnsupportedFiles,
    string? ErrorMessage);
```

`ProjectCandidate` is also immutable and has exact identity/display rules:

```csharp
public sealed record ProjectCandidate(
    string FilePath,
    string DisplayName,
    ProjectKind Kind);

public enum ProjectKind
{
    Solution,
    SolutionX,
    CSharpProject
}
```

`FilePath` is the normalized, absolute path returned by discovery. Candidate
identity is ordinal-equal `FilePath` comparison after normalization; `Kind` is
derived from the case-insensitive extension (`.sln` → `Solution`, `.slnx` →
`SolutionX`, `.csproj` → `CSharpProject`) and is not independently selected by
callers. `DisplayName` is `Path.GetFileNameWithoutExtension(FilePath)` using the
original file name's casing. It is presentation-only and never used for
identity or ordering. `Candidates` are sorted by ordinal `FilePath`.

`WorkspaceRoot` is the canonical full path being discovered, or `null` only in
`Unloaded`. `Candidates` contains only supported files, sorted by ordinal full
path. `SelectedProject` is either `null` or one of the exact candidate objects
in `Candidates`. `UnsupportedFiles` contains the full paths of known
unsupported project files, sorted by ordinal, and is empty for `Unloaded` and
`NoProject`. `ErrorMessage` is non-null only for `Failed`; cancellation never
writes a cancellation message into this field.

The service owns these immutable states:

- `Unloaded` — no workspace is open.
- `Loading` — discovery is in progress.
- `NoProject` — the root contains no project-like file.
- `Unsupported` — project-like files exist, but none are supported.
- `SingleProject` — exactly one supported candidate exists and is immediately
  selected.
- `Ambiguous` — multiple supported candidates exist and require selection.
- `Selected` — the user selected a candidate from the current snapshot.
- `Failed` — discovery failed because of I/O or permission errors.

Supported candidates are `.sln`, `.slnx`, and `.csproj`, matched
case-insensitively. The explicit known unsupported-extension set is
`.vbproj`, `.fsproj`, `.vcxproj`, `.pyproj`, `.dbproj`, `.wixproj`, and
`.shproj`, also matched case-insensitively. Unknown extensions, extensionless
files, and unrelated files are ignored and do not produce `Unsupported`.
Mixed roots retain supported candidates and report known unsupported files
without blocking selection. Candidate ordering is deterministic.
`SelectProject` rejects candidates not in the current snapshot, logs the
rejection, and leaves state unchanged.

The discovery seam returns this exact immutable result:

```csharp
public sealed record ProjectDiscoveryResult(
    IReadOnlyList<ProjectCandidate> SupportedCandidates,
    IReadOnlyList<string> UnsupportedFiles,
    ProjectDiscoveryFailure? Failure);

public sealed record ProjectDiscoveryFailure(
    ProjectDiscoveryFailureKind Kind,
    string Message);

public enum ProjectDiscoveryFailureKind
{
    InvalidRoot,
    NotFound,
    Unauthorized,
    Io
}
```

Both collections are non-null, contain normalized absolute paths, are sorted
by ordinal path, and contain no duplicates. `SupportedCandidates` contains
only the three supported kinds; `UnsupportedFiles` contains only files with
the explicit known unsupported extensions. The service maps the result as
follows when `Failure` is null: both collections empty → `NoProject`; supported
empty and unsupported non-empty → `Unsupported`; one supported candidate →
`SingleProject` with that candidate selected; multiple supported candidates →
`Ambiguous` with no selection. If both collections are non-empty, supported
candidates control the state and unsupported paths remain in
`UnsupportedFiles`. A non-null `Failure` requires both collections to be empty
and maps to `Failed` with `Failure.Message` as `ErrorMessage`.

The exact service contract is:

```csharp
public interface IProjectContextService : IDisposable
{
    ProjectContext Current { get; }
    IObservable<ProjectContext> WhenChanged { get; }

    Task LoadAsync(string workspaceRoot, CancellationToken cancellationToken = default);
    Task ReloadAsync(CancellationToken cancellationToken = default);
    Task UnloadAsync(CancellationToken cancellationToken = default);
    void SelectProject(ProjectCandidate? candidate);
}
```

`LoadAsync` accepts any string path. After the cancellation check it emits
`Loading`; an empty/whitespace, non-existent, non-directory, or inaccessible
root is converted by `ProjectDiscovery` to a non-null `Failure` and therefore
to a terminal `Failed` snapshot. It does not throw argument exceptions.
`ReloadAsync` requires a non-null current root; when no root is loaded it emits
a terminal `Failed` snapshot with `InvalidRoot`. `UnloadAsync` is awaitable for
API symmetry but commits synchronously after its pre-commit cancellation check.

### Transition and emission contract

The initial `Current` is `Unloaded, null, [], null, [], null`.

| Operation | Required `WhenChanged` sequence | Cancellation/result rule |
|---|---|---|
| `LoadAsync(root)` with an already-cancelled token | No emission | Throw `OperationCanceledException`; `Current` remains unchanged. |
| Initial `LoadAsync(root)` | `Loading(root, [], null, [], null)` then exactly one terminal `NoProject`, `Unsupported`, `SingleProject`, `Ambiguous`, or `Failed` snapshot | `SingleProject` sets `SelectedProject` to its sole candidate. `Ambiguous` leaves it null. |
| Initial load cancelled after `Loading` | `Loading` then `Unloaded` restoration | Throw cancellation; no `Failed` snapshot and no cancellation error. |
| `ReloadAsync` with an active stable snapshot | `Loading` for the same root with empty candidates/selection/unsupported/error, then one terminal snapshot | Cancellation after `Loading` emits the exact prior stable snapshot once and throws. |
| Overlapping loads | Each accepted request emits its own `Loading`; only the newest request may emit a terminal or restoration snapshot | Older completion or cancellation emits nothing after it becomes stale. The newest request owns the final state; if it is cancelled after its `Loading`, it restores and emits exactly the last stable snapshot from before the overlapping load sequence (or `Unloaded` if none exists), then throws cancellation. An older in-flight request remains stale and cannot publish afterward. |
| `UnloadAsync` | Exactly one `Unloaded, null, [], null, [], null` snapshot | Invalidate all in-flight sequences before publishing. Cancellation is checked before the synchronous commit; after commit it cannot undo unload. |
| `SelectProject(candidate)` | One `Selected` snapshot on valid current candidate; clearing an ambiguous selection emits `Ambiguous` | Foreign/stale candidate is ignored, logged, and emits nothing. A null clear with one candidate preserves `SingleProject` and its automatic selection; with zero candidates it preserves `NoProject` or `Unsupported`. |

`Loading` is the only transient state. A discovery I/O/permission exception
emits `Failed` with the exception message. Cancellation restores the last
stable snapshot when one exists; an initial cancelled load restores `Unloaded`.
State mutation and `WhenChanged` emission are thread-neutral and occur on the
service's calling/continuation thread; UI consumers marshal separately.

### Discovery and test seams

`ProjectContextService` must not call `Directory`, `File`, or
`DirectoryInfo` directly. Inject this framework-neutral seam:

```csharp
public interface IProjectDiscovery
{
    Task<ProjectDiscoveryResult> DiscoverAsync(
        string workspaceRoot, CancellationToken cancellationToken);
}
```

`ProjectDiscoveryResult` contains sorted supported candidates, sorted
`UnsupportedFiles`, an optional expected `Failure`, and no UI types.
`ProjectDiscovery` uses an injected `IProjectFileSystem` adapter whose
production implementation calls `Directory.EnumerateFiles(root, "*",
TopDirectoryOnly)`. It catches the expected filesystem exceptions and returns
the corresponding `ProjectDiscoveryFailure`; it does not catch cancellation or
unexpected exceptions. Tests inject a deterministic fake filesystem or fake
discovery, controlling delays, exceptions, and cancellation checkpoints without
machine permissions or timing.

The M0 proof must create `M0_DISCOVERY_PROOF.md` with: (1) the `net10.0`
target-framework confirmation; (2) the exact planned files
`src/Services/IProjectFileSystem.cs`, `FileSystemProjectFileSystem.cs`,
`IProjectDiscovery.cs`, `ProjectDiscovery.cs`, `ProjectDiscoveryResult.cs`,
`ProjectDiscoveryFailure.cs`, `ProjectCandidate.cs`, `ProjectKind.cs`,
`ProjectContext.cs`, `ProjectContextState.cs`, `IProjectContextService.cs`,
and `ProjectContextService.cs`, plus
`tests/Zaide.Tests/Services/Phase83M0DiscoveryProofTests.cs`; (3) the adapter
choice and root-level enumeration/classification algorithm; and (4) proof
assertions for case-insensitive `.SLN`/`.CsProj`, all seven known unsupported
extensions, unknown-extension ignoring, deterministic ordinal ordering,
empty/unsupported collection mapping, and the expected failure contract. The
artifact must record the filtered command, its exact `1 passed, 0 failed`
output, and `git diff --check` output.

### Construction, activation, disposal, and startup reconciliation

- `Program.cs` registers `IProjectDiscovery`/`ProjectDiscovery` and
  `IProjectContextService`/`ProjectContextService` as singletons.
- DI constructs `ProjectContextService` when resolving the singleton dependency
  of `MainWindowViewModel`; no view constructs it manually.
- The service constructor subscribes directly to
  `Workspace.WorkspaceFolderChanged`, stores the exact handler delegate, and
  immediately reconciles a non-null `Workspace.WorkspacePath` by starting one
  load. A null startup path remains `Unloaded`.
- The event handler starts an observed async operation. It catches cancellation
  as Debug, logs unexpected discovery failures through the injected logger, and
  never leaves an unobserved fire-and-forget task.
- `ProjectContextService : IDisposable` unsubscribes the stored handler and
  invalidates active sequences. The concrete ownership decision is: `App` owns
  shutdown and explicitly calls
  `Services.GetRequiredService<IProjectContextService>().Dispose()` from the
  existing `desktop.Exit` handler, alongside the existing terminal disposal.
  `Program` does not rely on implicit root-provider disposal. Disposal is
  tested by raising the workspace event after exit cleanup and asserting no
  discovery occurs.

### Logger contract

Inject `ILogger<ProjectContextService>`. Use stable event IDs: `8301` for
background discovery failure at Error, `8302` for cancellation at Debug, and
`8303` for rejected stale/foreign selection at Warning. The logger message must
include the workspace root or candidate path as applicable. The test logger
provider captures category, level, event ID, and message; tests assert that
background failures are logged once and cancellation is not logged as Error.

Discovery exception ownership is explicit. `ProjectDiscovery` converts
`DirectoryNotFoundException`, `UnauthorizedAccessException`, and `IOException`
from the filesystem seam become the corresponding `ProjectDiscoveryFailure`
and then a `Failed` `LoadAsync`/`ReloadAsync` result; these expected
operational failures are not re-thrown by the lifecycle API.
`OperationCanceledException` is never converted to `Failed`: it follows the
cancellation matrix above. Empty/whitespace, non-directory, and invalid roots
become `InvalidRoot` failures rather than argument exceptions. Any other
exception indicates a programming or unexpected provider failure: the
workspace-event handler logs it once at Error with event ID 8301, the task is
observed, and the exception is re-thrown to direct callers rather than silently
converted into a user-facing discovery state.

### DI and UI projection path

Change `MainWindowViewModel`'s constructor to require
`IProjectContextService projectContextService` and store it as a readonly
dependency. Add an immutable `ProjectContext CurrentProjectContext` property
initialized from `projectContextService.Current`. In `Activate()`, subscribe
to `projectContextService.WhenChanged`, apply
`ObserveOn(RxApp.MainThreadScheduler)`, and assign the property. This is the
single UI-thread projection seam.

Keep `StatusBarViewModel` dependent on `MainWindowViewModel` and
`ISettingsService`, but replace its `WorkspaceProjectName` subscription with
`mainWindow.WhenAnyValue(x => x.CurrentProjectContext)`. Its `ProjectText`
mapping is: selected candidate display name for `SingleProject`/`Selected`,
`Loading…`, `No project`, `Unsupported project`, `Project error`, or `Zaide`
for `Unloaded`. `StatusBar` remains unchanged as a view of
`StatusBarViewModel.ProjectText`. `WorkspaceProjectName` remains available for
legacy folder-name consumers but is no longer the status-bar project source.

## Limitations (by design)

- Discovery is root-level and file-based; project and solution contents are not
  parsed in Phase 8.3.
- A single supported candidate is auto-selected; only ambiguous results require
  explicit selection.
- The service does not start language servers or project processes.
- Status-bar text is a projection of project-context state, not a new source of
  project ownership.
- Linux is the primary manual validation platform for this roadmap.

## Exit Conditions

- [x] M0 entry gate is reviewed and the discovery proof passes.
- [x] All M1–M4 focused tests pass.
      M1–M3 focused tests accepted from prior milestones.
      M4 focused: 19 passed, 0 failed.
- [x] `dotnet build Zaide.slnx --no-restore` succeeds (0 warnings, 0 errors).
- [x] `dotnet test Zaide.slnx --no-build` passes in full (1185 passed, 0 failed).
- [x] `git diff --check` is clean.
- [ ] Manual verification covers open, close, single-project, ambiguous,
      unsupported, no-project, failed, and project-name status-bar states.
      **Partially verified:** open, close, single-project, ambiguous,
      unsupported, no-project, and project-name states were manually checked.
      The `Failed` → `Project error` GUI state remains unverified because no
      GUI environment was available; deterministic failure behavior is covered
      by the M1–M3 tests.
- [x] Phase 8.3 docs and the V2 roadmap agree; no Phase 9–13 scope is added.

## Rollback Plan

Revert only the isolated Phase 8.3 milestone commit that introduced the
incorrect behavior. Restore the accepted Phase 8.2 baseline (commit `4fa2dcc`)
and leave later V2 phases untouched. Do not revert Phase 8.1 settings or Phase
8.2 command/keybinding work as part of an 8.3 rollback.
