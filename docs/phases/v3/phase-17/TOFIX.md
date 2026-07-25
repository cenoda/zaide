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

M4 received GO on 2026-07-25 after corrective passes #1–#3. M4 closeout is
complete.

M5 was implemented on 2026-07-25 with a constrained mutation executor for
accepted create/replace/delete proposals, immediate pre-apply revalidation, safe
temporary-file and atomic replacement behavior, and truthful terminal results.
M5 completed corrective pass #1 on 2026-07-25 (architecture inventory ratchets
for the four mutation production files) and received GO on 2026-07-25.

M6 was implemented on 2026-07-25 with a Workspace/Editor application
reconciliation contract consumed by the action broker after confirmed disk
mutation; dirty buffers are never silently overwritten. M6 received GO on
2026-07-25.

M7 was implemented on 2026-07-25 with constrained non-shell command execution
through `IAgentCommandExecutor` / `WorkspaceCommandExecutor`, production
`DefaultAgentCommandResolver`, locked environment construction, and broker
integration preserving bounded stdout/stderr results. M7 received GO on
2026-07-25.

M8 was implemented on 2026-07-25 with session/event integration, in-memory
audit snapshots, fake action requester integration tests, projection ownership,
and bypass-prevention architecture ratchets.

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

- [x] M4 received GO on 2026-07-25 after corrective passes #1–#3.
- [x] Implement M5: safe workspace mutation behind accepted immutable proposals.
- [x] M5 corrective pass #1: ratchet architecture inventory for `IAgentFileMutator`, `AgentFileMutationOutcome`, `AgentFileMutationResult`, and `WorkspaceFileMutator` (source-file counts 516, Features 471, namespace rollups, baseline comments).
- [x] M5 received GO on 2026-07-25.
- [x] Implement M6: document reconciliation after confirmed disk mutation.
- [x] M6 received GO on 2026-07-25.
- [x] Implement M7: constrained command execution behind approved resolved commands.
- [x] M7 received GO on 2026-07-25.
- [x] Implement M8: session/event integration, audit snapshots, and bypass ratchets.
- [ ] M9 remains gated. Do not authorize M9 prematurely.

Manual mutation evidence recorded in `M5_WORKSPACE_MUTATION_EVIDENCE.md`.
Manual reconciliation evidence recorded in `M6_DOCUMENT_RECONCILIATION_EVIDENCE.md`.
Manual command execution evidence recorded in `M7_COMMAND_EXECUTION_EVIDENCE.md`.
Manual session/event integration evidence recorded in
`M8_SESSION_EVENT_INTEGRATION_EVIDENCE.md`.
Manual preview evidence for M4 remains in `M4_PROPOSAL_PREVIEW_EVIDENCE.md`.

## M8 (2026-07-25)

Implemented session/event integration and bypass ratchets:

- `AgentSessionService` creates run-scoped brokers for
  `IAgentActionRequestCapableBackend` backends; legacy backend keeps
  `UnavailableAgentActionBroker`.
- Typed action facts (`ActionRequested` … `ActionRevoked`) publish through
  `RunScopedAgentActionEventPublisher` and `AgentActionAuditStore`.
- `AgentConversationEventProjection` appends bounded `SystemNotification`
  summaries for terminal action results.
- Revocation propagates on run cancel/end, workspace invalidation, and shutdown.
- Repository-owned `FakeActionRequesterBackend` exercises full integration in
  `Phase17SessionEventIntegrationTests`.
- `Phase17BypassRatchetTests` ratchet forbidden bypass paths.

### Gate results (M8)

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-restore` | pass, 0 errors |
| `Phase17SessionEventIntegration` | pass, 7/7 |
| `Phase17BypassRatchet` | pass, 5/5 |
| `Phase17` + `Architecture` targeted filters | pass, 281/281 |
| Full fast suite | pass, 3045/3046 (1 pre-existing parallel fd-count flake) |
| Serial fallback | pass, 3045/3046 (1 pre-existing Phase 16 flake) |
| `git diff --check` | pass, clean |

## M7 (2026-07-25)

Implemented constrained command execution:

- `IAgentCommandExecutor` (`Agents.Contracts`) and `WorkspaceCommandExecutor`
  (`Agents.Infrastructure`) run one approved resolved command without shell
  parsing or ProjectSystem workflow runners.
- `DefaultAgentCommandResolver` resolves PATH, symlinks, and denylist targets
  before permission review.
- `AgentCommandEnvironmentBuilder` constructs the locked environment and
  redacts secret values.
- `ContractAgentActionBroker` executes approved commands, revalidates identity
  before start, and preserves `AgentCommandExecutionResult` on terminal
  results and duplicate replay.
- `PermissionReviewViewModel.ContainmentDisclosureText` states that
  working-directory scope is not filesystem or network sandboxing.

### Gate results (M7)

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-restore` | pass, 0 errors |
| `Phase17CommandExecution` | pass, 22/22 |
| `Phase17` (all filters) | pass |
| `Architecture` | pass |
| Full fast suite | pass, 3034/3034 |
| `git diff --check` | pass, clean |

## M5 corrective pass #1 (2026-07-25)

Ratchets the four M5 mutation production files in the architecture inventory:

- `IAgentFileMutator` (`Agents.Contracts`)
- `AgentFileMutationOutcome`, `AgentFileMutationResult` (`Agents.Domain`)
- `WorkspaceFileMutator` (`Agents.Infrastructure`)

Updates:

- Total production source files: 512 → 516
- Features folder source files: 467 → 471
- Namespace rollup comments in `ArchitectureInventoryTests` and
  `ArchitectureVisibilityTests`
- `PublicProductionTypeBaseline` M5 type-count comments (572 total / 235
  internal unchanged; all four types are internal)

### Gate results (M5 corrective pass #1)

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-restore` | pass, 0 errors |
| Phase 17/Architecture targeted filters | pass, 244/244 |
| Full fast suite | pass, 3002/3002 |
| Serial fallback | pass, 3002/3002 |
| `git diff --check` | pass, clean |

## M6 (2026-07-25)

Implemented document reconciliation after confirmed disk mutation:

- `IAgentDocumentReconciler` (`Agents.Contracts`) is consumed by
  `ContractAgentActionBroker` after successful file mutations.
- `WorkspaceEditorDocumentReconciler` (`Editor.Application`) reconciles open
  `Workspace` documents without referencing Editor Presentation types.
- `IEditorUiDispatcher` / `AvaloniaEditorUiDispatcher` marshal document
  updates on the UI thread with observer-failure isolation.
- `Document` exposes `ReloadCleanContent`, `FlagDiskAbsent`, and
  `IsDiskAbsent`; dirty buffers are never silently overwritten.

### Gate results (M6)

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-restore` | pass, 0 errors |
| `Phase17DocumentReconciliation` | pass, 10/10 |
| `Phase17WorkspaceMutation` | pass, 17/17 |
| `Phase17Proposal` | pass, 48/48 |
| `Phase17ProposalBroker` | pass, 27/27 |
| `Phase17Permission` | pass, 36/36 |
| `Phase17ActionContracts` | pass, 50/50 |
| `Phase17WorkspaceRead` | pass, 39/39 |
| `Phase17WorkspaceAuthority` | pass, 21/21 |
| Architecture | pass |
| Full fast suite | pass, 3012/3012 |
| Serial fallback | pass (1 pre-existing flake isolated: `LaunchAsync_CancellationTerminatesProcessTree`) |
| `git diff --check` | pass, clean |

## M4 corrective pass #3 (2026-07-25)

Corrects the permission lifecycle ordering in `ContractAgentActionBroker`.

- File proposal validation still checks fingerprint, classification, expiry,
  workspace freshness, and proposal/base binding before authorization.
- File create/replace/delete stale-base revalidation now runs immediately before
  `AgentPermissionDecision.TryConsume()`.
- `TryConsume()` is the final authorization step. A stale proposal returns
  `Revoked` with `StaleBaseRevision` and leaves the published decision
  unconsumed.
- Create admission remains `NotFound`-only and all indeterminate target states
  remain fail-closed.
- Regression coverage proves stale create, replace, and delete rejection does
  not consume; fresh proposals consume exactly once; and a concurrent
  stale/allow race cannot consume the stale proposal.

### Gate results (M4 corrective pass #3)

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-restore` | pass, 0 errors |
| Phase 17/Architecture targeted filters | pass, 220/220 |
| Full fast suite | pass, 2985/2985 |
| Serial fallback | not needed |
| `git diff --check` | pass, clean |

## M4 corrective pass #2 (2026-07-25)

Restores predecessor `Phase17Permission` and `Phase17ActionContracts` broker
behavior while preserving M4 fail-closed proposal semantics.

### 1. Create target inspection (fail closed)

- `AgentFileProposalGenerator` accepts create proposals only when the live
  target read returns `AgentFileReadOutcome.NotFound`.
- Existing, binary, special, escaped, unreadable, cancelled, and all other
  indeterminate inspection outcomes reject proposal generation before permission
  review begins.

### 2. Stale-base revalidation before decision consumption

- `ContractAgentActionBroker.IsFileProposalStaleBeforeConsumption` re-reads the
  proposal target immediately before `TryConsume()`.
- Create operations require a confirmed `NotFound` at consumption time; any other
  outcome (including non-regular and unreadable targets) revokes with
  `StaleBaseRevision`.
- Replace/delete operations preserve revision comparison and fail closed when
  the base cannot be read successfully.

### 3. Broker/test seam restoration

- `CountingAgentFileReader` is path-aware with a default of confirmed absence
  (`NotFound`), per-path overrides, and queued read sequences for race tests.
- Predecessor permission, correlation, cancellation, and revocation broker
  tests use the synthetic reader without false create rejection.

### 4. Regression tests added

- Synthetic reader default does not false-reject create proposals.
- Create accepted only on confirmed `NotFound`.
- Create rejected on every indeterminate inspection outcome.
- Create target appearing or becoming non-regular/unreadable before decision
  consumption revokes as stale base.

### Gate results (M4 corrective pass #2)

| Gate | Result |
|------|--------|
| Build | pass, 0 errors |
| `Phase17Proposal` | pass |
| `Phase17ProposalBroker` | pass |
| `Phase17Permission` | pass |
| `Phase17ActionContracts` | pass |
| `Phase17WorkspaceRead` | pass |
| `Phase17WorkspaceAuthority` | pass |
| Architecture | pass |
| Full suite (fast) | 2978/2980 pass; 2 pre-existing parallel-runner flakes (`Restart_DoesNotLeakFileDescriptors`, `LaunchAsync_EnforcesWallTimeoutAndOrphanAbsence`) |
| Full suite (slow.runsettings) | 2980/2980 pass |
| `git diff --check` | clean |

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

## Scope boundaries observed (M5)

M5 did not implement document reconciliation, command execution, Agent
event/Townhall integration, Native Harness, ACP, or any Phase 16 / Phase 18
work. The production execution path still uses `UnavailableAgentActionBroker`;
live broker wiring remains M8.

## Scope boundaries observed (M8)

M8 did not implement M9 closeout, Native Harness, ACP, persistence/resume,
raw traces, Phase 18 context disclosure, or a production tool-using backend.
The fake action requester is test-only and not registered in production DI.
M9 remains gated by M8 GO.

## Scope boundaries observed (M7)

M7 did not implement M8 session/event integration, Agent/Townhall projection,
production DI broker wiring, Native Harness, ACP, or any Phase 16 / Phase 18
work. Command execution is exercised through focused tests and the broker seam
only. M8 remains gated by M7 GO.

## Scope boundaries observed (M6)

M6 did not implement command execution, Agent/Townhall event integration,
Native Harness, ACP, or any Phase 16 / Phase 18 work. Reconciliation consumes
confirmed M5 mutation results only. M7 remains gated by M6 GO.
