# Phase 17: Agent Action Control Plane and Workspace Mutation — TOFIX

## Status

M0 was accepted by the user on 2026-07-24. The accepted implementation
boundary and decisions P17-D01–P17-D12 are recorded in
`IMPLEMENTATION_PLAN.md`.

M1 and M2 received GO. M2 was implemented on 2026-07-25 and completed four
corrective passes (broker admission capture, canonical root revalidation, read
result preservation, production IWorkspaceActionAuthority with event-driven
generation and mandatory root identity, fail-closed for relative paths).
M2 closeout is complete.

M3 was implemented on 2026-07-25, received NO-GO on first audit, and completed
one corrective pass (production review-surface wiring, exact path display,
cancellation preservation, atomic decision lifecycle). M3 received GO on
2026-07-25 after corrective pass #1. M3 closeout is complete.

## Current work

- [x] Create, audit, amend, and accept the Phase 17 implementation plan.
- [x] Complete M1 contracts and deterministic state (GO).
- [x] Complete M2 canonical workspace capture and bounded read-only file access.
- [x] Implement M3 permission classification, immutable decision lifecycle, revocation, exact-request fingerprint binding, and visible review surface (GO).
- [x] M3 corrective pass #1: production review-surface wiring, exact path display, cancellation preservation, atomic Published → Consumed lifecycle.
- [x] M2 corrective pass #1: broker admission capture, canonical root revalidation, read result preservation.
- [x] M2 corrective pass #2: production IWorkspaceActionAuthority, mandatory root filesystem identity, DI wiring.
- [x] M2 corrective pass #3: event-driven generation, thread-safe full-state IsCurrent, direct authority tests.
- [x] M2 corrective pass #4: fail-closed for relative paths (".", "src") before realpath/stat.

## Next task

- [x] Implement M4: immutable create/replace/delete file proposals, bounded diff/summary presentation, stale-base detection, and explicit accept/deny flow. Proposal creation remains non-mutating.
- [ ] M4 corrective pass: complete broker integration with fail-closed behavior, stale-base revalidation, and proposal/fingerprint/base-revision binding (awaiting re-audit).

Manual preview evidence recorded in `M4_PROPOSAL_PREVIEW_EVIDENCE.md`.

## M3 corrective pass #1 (2026-07-25)

Resolves the four NO-GO audit blockers. Manual review evidence is recorded in
`M3_PERMISSION_REVIEW_EVIDENCE.md`.

### 1. Production wiring of the review surface

- **DI registration.** `AddZaideAgents` registers
  `IAgentPermissionDialogPresenter → PermissionReviewDialogPresenter` and
  `IAgentPermissionReviewService → InteractiveAgentPermissionReviewService`
  as singletons (12 module registrations total).
- **Owner attachment.** `App.OnFrameworkInitializationCompleted` attaches
  `desktop.MainWindow` to the presenter singleton via `SetOwner` after the
  main window is created, making the Allow path reachable in production.
- **Fail-closed UI absence.** A missing presenter or missing owner window
  throws instead of fabricating a user denial; the broker classifies both as
  `PermissionUnavailable`. The presenter dispatch was also fixed to await the
  dialog task instead of blocking on `Task.Result` on the UI thread.
- **Tests added:** service-invokes-presenter, broker allow-path through the
  real service, no-presenter/no-owner/broker fail-closed, DI resolution and
  App-source owner-attachment assertions.

### 2. Exact path display

- `PermissionReviewViewModel.ResolvedPathText` now resolves against
  `WorkspaceActionScope.CapturedCanonicalRoot` (not `RootPath`) and
  re-validates containment beneath the captured canonical root before
  displaying the absolute path. Missing scope or unconfirmed containment
  displays an explicit fail-closed marker instead of silently falling back
  to the relative path.
- **Tests added:** exact assertions for both `NormalizedPathText` and
  `ResolvedPathText`, missing-scope marker, containment-withheld marker.

### 3. Cancellation preservation

- `InteractiveAgentPermissionReviewService` no longer catches any exception:
  `OperationCanceledException` propagates so broker results remain
  `Cancelled`; presenter failures propagate so results are
  `PermissionUnavailable`.
- `PermissionReviewDialogPresenter` registers the cancellation token while
  the dialog is open; cancellation completes the pending decision as
  cancelled before closing the dialog, so deny-on-dismiss cannot masquerade
  as a user denial.
- **Tests added:** cancellation-during-dialog at the service level and
  through the broker (`Cancelled`, not `PermissionDenied`).

### 4. Atomic decision lifecycle and classification

- `AgentPermissionDecision` status is now advanced only by the new
  `TryConsume()` — an `Interlocked.CompareExchange` transition
  Published → Consumed — so one decision authorizes at most one execution.
- The broker validates, in order: exact fingerprint binding, expected
  `RequiresUserDecision` classification, initial status (`Published` or
  `Denied` only; `Consumed`/`Revoked`/`Expired` rejected even with
  `IsAllow = true`), expiry, workspace freshness, `IsAllow`, and finally the
  atomic consume.
- `PermissionReviewViewModel` resolution is single-shot (first of
  Allow/Deny/dismiss wins), removing the duplicate click/command resolution
  paths in the dialog code-behind.
- **Tests added:** forged `Denied` + `IsAllow = true`, TryConsume
  exactly-once/non-published/concurrent-racers, broker consumes the issued
  decision, replayed consumed decision cannot authorize again.

### Gate results (M3 corrective pass #1)

| Gate | Result |
|------|--------|
| Build | pass, 0 errors |
| `Phase17Permission` | 36/36 pass |
| `Phase17ActionContracts` | 50/50 pass |
| `Phase17WorkspaceRead` | 39/39 pass |
| `Phase17WorkspaceAuthority` | 21/21 pass |
| Architecture | 26/26 pass |
| Full suite (fast) | 2936/2937; 1 fd-count flake in `Restart_DoesNotLeakFileDescriptors` under the parallel runner only (passes in isolation and serially; pre-existing, unrelated to M3) |
| Full suite (slow.runsettings) | 2936/2937; only the pre-existing Phase 16 flake `LaunchAsync_CancellationTerminatesProcessTree` failed (passes in isolation; known from M2 gates) |
| `git diff --check` | clean |

## M2 corrective pass #1 (2026-07-25)

### 1. Workspace authority capture

- **Broker admission capture.** `ContractAgentActionBroker` now captures the
  workspace scope at admission via `IWorkspaceActionAuthority.TryCaptureCurrentScope`
  instead of receiving a pre-captured `WorkspaceActionScope`. The captured scope
  is stored as nullable; when `TryCaptureCurrentScope` returns `false`, the
  broker stores `null` and rejects every action request with the new
  `AgentActionFailureKind.NoWorkspace` before composition or filesystem access.
- **Explicit no-workspace behavior.** A broker admitted without a workspace
  rejects all reads with a `NoWorkspace` denial. The check occurs in both
  `RequestAsync` (before request composition) and `ExecuteAllowedRead` (before
  execution).
- **Separated fake authority.** `FakeWorkspaceActionAuthority` now separates
  `HasWorkspace` (controls `TryCaptureCurrentScope`) from `IsStale` (controls
  `IsCurrent`), so tests can exercise no-workspace and stale-generation cases
  independently.
- **Tests added:** `NoWorkspace_RejectsReadWithNoWorkspace`,
  `StaleWorkspaceGeneration_RejectsReadBeforeAnyFileSystemAccess`.

### 2. Canonical root authority

- **Captured canonical root.** `WorkspaceActionScope` now requires a
  `capturedCanonicalRoot` parameter (absolute path resolved at capture time,
  e.g. via `realpath`) and optional `capturedRootDevice`/`capturedRootInode`
  for stat-based root identity comparison.
- **Root revalidation in reader.** `WorkspaceFileReader.Read` now re-validates
  the captured canonical root against the live filesystem before opening any file:
  1. Re-canonicalizes `scope.RootPath` via `realpath` and compares against
     `scope.CapturedCanonicalRoot` — rejects root symlink retargeting.
  2. When device/inode were captured, stats the live root and compares against
     `scope.CapturedRootDevice`/`CapturedRootInode` — rejects root directory
     replacement (same path, different filesystem object).
  3. Both checks run before any file open; failures return `PathEscaped`.
- **Tests added:** `Read_RootSymlinkRetargeted_ReturnsPathEscaped`,
  `Read_RootReplacedWithNewDirectory_ReturnsPathEscaped`,
  `Read_CapturedCanonicalRootMismatch_ReturnsPathEscaped`.

### 3. Read result preservation

- **Structured result data.** `AgentActionResult` now carries nullable
  `Content`, `Revision` (SHA-256), and `ByteLength` properties alongside the
  existing `Summary`. Successful reads populate all three; rejection results
  are bounded and redacted (`Content` null, `Revision` default, `ByteLength` 0).
- **Broker pass-through.** `ContractAgentActionBroker.ExecuteAllowedRead` now
  passes `readResult.Content`, `readResult.Revision`, and
  `readResult.ByteLength` through to the `AgentActionResult`.
- **Duplicate replay preservation.** All four duplicate-replay return paths in
  `RequestAsync` preserve `Content`, `Revision`, and `ByteLength` from the
  original terminal result.
- **Tests added:** `SuccessfulRead_PreservesContentRevisionAndByteLength`,
  `RejectedRead_HasBoundedRedactedResult`,
  `DuplicateCorrelationKey_PreservesContentRevisionAndByteLength`.

### Other supporting changes

- Added `AgentActionFailureKind.NoWorkspace` and
  `AgentActionFailureKind.WorkspaceRootChanged` to the failure-kind enum.
- Updated all `WorkspaceActionScope` construction sites (three test locations)
  for the new `capturedCanonicalRoot` parameter.
- Updated `FakeWorkspaceActionAuthority` to accept `HasWorkspace` separate from
  `IsStale`.
- Updated `ContractAgentActionBroker` constructor: removed pre-captured
  `WorkspaceActionScope` parameter; scope is captured internally via
  `TryCaptureCurrentScope`.
- Added `TryGetDeviceInode` to `WorkspaceFileReader` for stat-based root
  identity comparison.
- Added `StatDeviceInode` helper and `Stat` P/Invoke to
  `Phase17WorkspaceReadFileReaderTests`.

## M2 corrective pass #2 (2026-07-25)

### 1. Production IWorkspaceActionAuthority

- **New type:** `WorkspaceActionAuthority` in `Features/Workspace/Infrastructure`.
  Wraps the production `Workspace` singleton. Captures active workspace
  identity (deterministic SHA-256-derived from canonical path), monotonic
  generation (increments on identity change), canonical root via `realpath`,
  and root filesystem identity via `stat` (device + inode).
- **Fail-closed no-workspace:** `TryCaptureCurrentScope` returns `false` when
  `Workspace.WorkspacePath` is null or empty, or when `realpath`/`stat` fail.
- **IsCurrent:** Re-resolves the canonical root path and compares the live
  identity and generation against the captured scope's values.
- **DI registration:** Registered as singleton `IWorkspaceActionAuthority` in
  `WorkspaceServiceCollectionExtensions.AddZaideWorkspace()`.

### 2. Mandatory root filesystem identity

- **`WorkspaceActionScope`:** `capturedRootDevice` and `capturedRootInode`
  are now required (no default values). Constructor validates both are > 0;
  zero values throw `ArgumentException`.
- **`WorkspaceFileReader`:** Stat-based root identity check is unconditional
  (removed the `!= 0` guard). Every read validates the live root's (dev, inode)
  against the captured scope values before opening any file.
- **Test helper:** `FakeWorkspaceActionAuthority.CreateScopeFromDirectory`
  and `CreateScope` factory methods stat live directories to produce valid
  device/inode values. All 7 scope-construction sites updated to use these.

### 3. Tests added

- `WorkspaceActionScope_RejectsZeroDevice` — proves zero device throws
- `WorkspaceActionScope_RejectsZeroInode` — proves zero inode throws
- `Read_CapturedCanonicalRootMismatch_ReturnsPathEscaped` — stat check
  still validates while path comparison rejects
- All existing scope-construction sites updated for mandatory identity

### 4. Architecture baseline updates

- `ArchitectureInventoryReader`: M0 top-level types 557→558 (+1 internal)
- `PublicProductionTypeBaseline`: total 557→558, internal 220→221
- `ArchitectureInventoryTests`: Workspace.Infrastructure namespace (2,1,1)
- `WorkspaceRegistrationModuleTests`: updated from 2→3 planned registrations

## M2 corrective pass #3 (2026-07-25)

### 1. Event-driven generation advancement

- **Rewritten `WorkspaceActionAuthority`** subscribes to
  `Workspace.WorkspaceFolderChanged`. On every transition — open, close,
  switch, reopen — generation advances unconditionally via
  `RefreshFromCurrentPath()`. This guarantees `A → close → A` and
  `A → B → A` each produce distinct generations.
- **Identity** is derived from the canonical root path (deterministic
  SHA-256). When a workspace is closed, identity resets to `default` so
  `TryCaptureCurrentScope` returns `false`.
- **Fail-closed for unresolvable paths:** if `realpath` or `stat` fail
  during a transition, identity is cleared and capture is disabled until
  a valid workspace is opened.
- **Constructor** only initialises state if a workspace is already open
  (guards against double-generation on first open).

### 2. Thread safety

- All state mutation and reads are protected by a `lock (_gate)`. The
  folder-changed handler, `TryCaptureCurrentScope`, `IsCurrent`, and
  `Dispose` all synchronise on the same gate.
- The handler is invoked synchronously by `Workspace.SetProjectFromPath`,
  so state is consistent by the time the calling thread regains control.

### 3. Full IsCurrent validation

`IsCurrent` now validates **all five** captured fields against live
filesystem state:

| Captured field | Live validation |
|---------------|-----------------|
| `Identity` | Re-computed from `realpath(WorkspacePath)` |
| `Generation` | Compared against `_liveGeneration` |
| `CapturedCanonicalRoot` | Compared against live `realpath(WorkspacePath)` |
| `CapturedRootDevice` | Stat'd from live root directory |
| `CapturedRootInode` | Stat'd from live root directory |

A mismatch in any field returns `false`. This detects root symlink
retargeting (canonical root change) and root directory replacement
(device/inode change) without relying on generation bump.

### 4. Direct production-authority tests

New test file `Phase17WorkspaceAuthorityTests` (19 tests) covering:

- No workspace → capture returns `false`
- First capture → generation = 1, valid device/inode
- Close → capture returns `false`, prior scope stale
- Switch → generation bumps, identities differ, prior scope stale
- A → close → A → generation bumps even though identity matches
- A → B → A → strictly monotonic generations, A identity stable
- Root symlink retarget → `IsCurrent` returns `false`
- Root directory replacement → `IsCurrent` returns `false`
- Tampered canonical root → `IsCurrent` returns `false`
- Fail-closed: relative path, missing path
- Thread safety: concurrent capture + IsCurrent, concurrent capture + folder change
- DI registration: singleton via direct registration and via `AddZaideWorkspace`
- Dispose: unsubscribes from event, rejected capture

### 5. Files changed in corrective #3

| File | Change |
|------|--------|
| `WorkspaceActionAuthority.cs` | Rewritten: event-driven, thread-safe, full IsCurrent |
| `Phase17WorkspaceAuthorityTests.cs` (new) | 19 direct production-authority tests |

## M2 corrective pass #4 (2026-07-25)

### Fail-closed for relative paths

- **`RefreshFromCurrentPath`**: Added `Path.IsPathRooted(rootPath)` guard
  before `realpath`/`stat`. Relative paths including `"."` and `"src"` now
  clear all live authority state (identity → default, canonical root/device/
  inode → empty/zero) so `TryCaptureCurrentScope` returns `false`.
- **Tests added**: `TryCapture_FailsClosed_WhenPathIsDotDirectory`,
  `TryCapture_FailsClosed_WhenPathIsUnqualifiedName`.

## Gate results (2026-07-25 corrective pass #4)

| Gate | Result |
|------|--------|
| Build | pass, 0 errors |
| `Phase17WorkspaceRead` | 39/39 pass |
| `Phase17ActionContracts` | 50/50 pass |
| Architecture | 26/26 pass |
| `Phase17WorkspaceAuthority` | 21/21 pass (+2) |
| Full suite | 2899/2900 pass, 1 pre-existing Phase 16 flake |
| `git diff --check` | clean |

### Per-filter detail

| Filter | Count | Status |
|--------|-------|--------|
| `Phase17WorkspaceRead` | 39 (29 base + 10 corrective) | all pass |
| `Phase17ActionContracts` | 50 | all pass |
| Architecture | 26 | all pass |
| `Phase17WorkspaceAuthority` | 21 (new) | all pass |
| Full suite | 2900 (1 pre-existing flake) | 2899 pass |

| Gate | Result |
|------|--------|
| Build | pass, 0 errors |
| `Phase17WorkspaceRead` | 39/39 pass (+10 new corrective tests) |
| `Phase17ActionContracts` | 50/50 pass |
| Architecture | 26/26 pass |
| Full suite | 2879/2879 pass (slow.runsettings) |
| `git diff --check` | clean |

### Per-filter detail

| Filter | Count | Status |
|--------|-------|--------|
| `Phase17WorkspaceRead` | 39 (29 base + 10 corrective) | all pass |
| `Phase17ActionContracts` | 50 | all pass |
| Architecture | 26 | all pass |
| Full suite | 2879 | all pass |

## Scope boundaries observed

Both corrective passes did not implement permission UI, mutation, command
execution, document reconciliation, or Agent event/Townhall integration.
The production execution path still uses `UnavailableAgentActionBroker`; the
read executor and workspace authority are exercised by focused tests and
remain wired into the live run boundary in M8. The production
`WorkspaceActionAuthority` is registered in DI; actual broker wiring is M8.

## Scope boundaries observed (M3)

M3 and its corrective pass did not implement mutation, command execution,
document reconciliation, Agent event/Townhall integration, Native Harness,
ACP, or any Phase 16 / Phase 18 work. The production execution path still
uses `UnavailableAgentActionBroker`; live broker wiring remains M8.

## Next task

M3 corrective pass #1 is complete; request M3 re-audit. M4 (non-mutating
change proposals) remains blocked until M3 receives GO.
