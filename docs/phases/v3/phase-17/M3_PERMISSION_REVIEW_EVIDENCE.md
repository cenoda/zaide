# Phase 17 M3: Permission Review Evidence

This document records the manual review evidence and test validation for
Phase 17 Milestone 3 (permission policy and visible decisions), including the
corrective pass that resolved the M3 NO-GO audit blockers.

The manual evidence below is a code-path review of the production wiring,
tracing each hop by file, with the focused automated test that pins each
behavior. No GUI screenshot evidence is included; the review surface is a
production-wired Avalonia dialog exercised through its presenter seam.

## 1. Production wiring of the visible review surface

Traced production path (each hop verified against live code):

1. `Program.ConfigureServices` calls `AddZaideAgents()`
   (`src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs`),
   which registers as singletons:
   - `IAgentPermissionDialogPresenter` → `PermissionReviewDialogPresenter`
   - `IAgentPermissionReviewService` → `InteractiveAgentPermissionReviewService`
2. `App.OnFrameworkInitializationCompleted`
   (`src/App/Composition/App.axaml.cs`) resolves the presenter singleton after
   `desktop.MainWindow` is created and attaches the owner via
   `SetOwner(desktop.MainWindow)`. The Allow path is therefore reachable in
   production: broker → `InteractiveAgentPermissionReviewService` →
   `PermissionReviewDialogPresenter` → owned modal `PermissionReviewDialog`.
3. Until the owner is attached (or in headless composition),
   `PermissionReviewDialogPresenter.ShowAsync` throws, and a service
   constructed without a presenter throws; the broker maps both to a denial
   with `AgentActionFailureKind.PermissionUnavailable`. UI absence fails
   closed and is truthfully classified as unavailability, not as a user
   denial.

Evidence:

- `AgentsRegistrationModuleTests.AddZaideAgents_RegistersExactlyThePlannedServices`
  and `AgentsModuleSource_ContainsExactlyThePlannedRegistrations` pin the DI
  membership (12 singletons).
- `AgentsRegistrationModuleTests.ProgramConfigureServices_ResolvesAgentsServicesAsSingletons`
  resolves the production provider and proves `IAgentPermissionReviewService`
  is `InteractiveAgentPermissionReviewService` and
  `IAgentPermissionDialogPresenter` is the `PermissionReviewDialogPresenter`
  singleton.
- `AgentsRegistrationModuleTests.AppSource_AttachesPermissionPresenterOwnerToMainWindow`
  proves the shell attaches `desktop.MainWindow` as the presenter owner.
- `Phase17PermissionReviewServiceTests.RequestDecision_InvokesVisibleReviewPath_AndAllowFlowsThrough`
  proves the service invokes the presenter (visible review path) with the
  exact request, display summary, and captured workspace scope, and that an
  explicit Allow flows through as a `Published` allow decision.
- `Phase17PermissionReviewServiceTests.Broker_UserAllowThroughVisibleReviewPath_Succeeds`
  proves the full broker → interactive service → presenter allow path.
- `Phase17PermissionReviewServiceTests.RequestDecision_NoPresenter_FailsClosedByThrowing`,
  `ProductionPresenter_WithoutOwnerWindow_FailsClosedByThrowing`, and
  `Broker_UiAbsence_FailsClosedAsPermissionUnavailable` prove UI absence
  fails closed as `PermissionUnavailable` at the service, presenter, and
  broker layers respectively.

## 2. Exact path display

`PermissionReviewViewModel` receives the captured `WorkspaceActionScope` from
the broker (passed through `IAgentPermissionReviewService.RequestDecisionAsync`
and `IAgentPermissionDialogPresenter.ShowAsync`) and displays:

- `NormalizedPathText` — the normalized workspace-relative path from the
  immutable request payload;
- `ResolvedPathText` — the absolute path built from the captured canonical
  root (`WorkspaceActionScope.CapturedCanonicalRoot`), re-validated for
  containment beneath that root (ordinal prefix check against the
  root-with-separator) before display. When the scope is unavailable or
  containment cannot be confirmed, an explicit fail-closed marker
  (`NoWorkspaceScopeText` / `EscapedPathText`) is displayed instead of a
  fabricated or relative path.

Both fields are bound in `PermissionReviewDialog.axaml`
("Workspace-Relative Path" and "Resolved Absolute Path" rows).

Evidence (`Phase17PermissionReviewViewModelTests`):

- `DisplaysBothNormalizedRelativePathAndResolvedAbsolutePath` asserts the
  exact workspace-relative value and the exact rooted absolute value beneath
  the captured canonical root.
- `ResolvedPath_WithoutWorkspaceScope_ShowsFailClosedMarker` asserts the
  missing-scope marker and that no rooted path is fabricated.
- `ResolvedPath_ContainmentNotConfirmed_WithholdsAbsolutePath` asserts a
  non-canonical captured root fails the containment re-validation and the
  absolute path is withheld.
- `Phase17PermissionLifecycleTests.WorkspaceScope_PassedToReviewService`
  proves the broker provides the captured scope (root and canonical root) to
  the review layer.

## 3. Cancellation semantics

`InteractiveAgentPermissionReviewService` does not catch
`OperationCanceledException`; it propagates to the broker, which returns
`AgentActionResultKind.Cancelled` (never `PermissionDenied`).
`PermissionReviewDialogPresenter` registers the cancellation token while the
dialog is open; cancellation completes the pending decision as cancelled
*before* closing the dialog, so deny-on-dismiss cannot record a user denial
that never happened.

Evidence:

- `Phase17PermissionReviewServiceTests.RequestDecision_CancellationDuringDialog_RethrowsOperationCanceled`
  cancels while the presenter is showing and asserts the exception
  propagates.
- `Phase17PermissionReviewServiceTests.Broker_CancellationDuringDialog_ReturnsCancelledNotPermissionDenied`
  drives the broker through the real interactive service and asserts the
  terminal result is `Cancelled`, not `PermissionDenied`.
- `Phase17PermissionLifecycleTests.CancellationDuringReview_ReturnsCancelled`
  pins the broker mapping for a cancelled review service.

## 4. Decision lifecycle and classification

`ContractAgentActionBroker` validates every decision returned by the review
service, in order, before authorizing:

1. Decision fingerprint must equal the exact immutable request fingerprint.
2. Classification must be `RequiresUserDecision`.
3. Status must be `Published` (allow) or `Denied` (deny/dismiss); `Consumed`,
   `Revoked`, and `Expired` are rejected even when `IsAllow` is true.
4. The decision must not be expired.
5. The workspace scope must still be current (generation and root).
6. `IsAllow` must be true and status must not be `Denied`.
7. `AgentPermissionDecision.TryConsume()` must succeed: an atomic
   compare-exchange transition Published → Consumed on the decision itself,
   so one decision authorizes at most one execution and no forged terminal
   status can be consumed.

Evidence (`Phase17PermissionLifecycleTests`):

- `MismatchedFingerprint_Rejected` — forged fingerprint binding.
- `ForgedClassification_WrongClassification_Rejected` — forged
  `AllowedByLockedPolicy` classification.
- `ForgedStatus_Consumed_Rejected`, `ForgedStatus_Revoked_Rejected`,
  `ForgedStatus_Expired_RejectedEvenIfAllowTrue`,
  `ForgedStatus_Denied_RejectedEvenIfAllowTrue` — forged status/`IsAllow`
  combinations.
- `TryConsume_TransitionsPublishedToConsumed_ExactlyOnce`,
  `TryConsume_RejectsNonPublishedStatuses`,
  `TryConsume_ConcurrentRacers_ExactlyOneWins` — atomic lifecycle at the
  domain level.
- `AllowedDecision_IsConsumedAfterAuthorization` — the broker consumes the
  issued decision (Published → Consumed) on the authorized path.
- `ReplayedConsumedDecision_CannotAuthorizeSecondRequest` — an already
  consumed decision replayed by an adversarial review service cannot
  authorize again.
- `ExpiredDecision_Rejected`, `StaleWorkspace_Revokes`,
  `BackendSelfApproval_Rejected`, `ObserverFailure_FailsClosed` — expiry,
  workspace invalidation, self-approval, and observer failure remain
  enforced.

## 5. Review surface content, dismissal, and accessibility

`PermissionReviewDialog.axaml` displays: initiating actor, target actor,
backend, correlated run, action kind, normalized target, workspace-relative
path, resolved absolute path, the bounded display summary, and the fixed
scope line "Scope: this exact request only.". Allow and Deny are explicit
buttons with `Focusable="True"` and `AutomationProperties.Name` ("Allow
Action" / "Deny Action"); Deny is `IsCancel="True"` (Escape denies) and
receives initial keyboard focus so the fail-safe choice is the default.
Closing the window without an explicit choice resolves as a denial;
resolution is single-shot (first resolution wins), so a close following an
explicit Allow cannot be overwritten and a dismiss cannot double-fire.

Evidence:

- `Phase17PermissionLifecycleTests.DisplaySummary_ContainsAllRequiredFields`
  — bounded display summary content.
- `Phase17PermissionReviewViewModelTests.DisplaysFixedScopeAndRequestIdentityFields`
  — fixed scope text and request identity fields.
- `Phase17PermissionReviewViewModelTests.AllowDenyAndDismiss_ResolveExactlyOnce_FirstWins`
  — deny-on-dismiss and single-resolution semantics.
- `Phase17PermissionLifecycleTests.NonReadRequest_DeniedByUser` and
  `CorrectLifecycle_DeniedStatus_ReturnsPermissionDenied` — user denial and
  dismissal map to `PermissionDenied`.

## 6. Read policy and scope boundaries

- `Phase17PermissionLifecycleTests.ReadRequest_AutoAllowedWithoutPrompt`
  proves reads remain auto-allowed under the locked read policy without
  triggering the review surface.
- No mutation executor, command executor, document reconciliation, or
  Agent/Townhall integration was added; the authorized allow path terminates
  in an attributable `Succeeded` result without disk mutation (mutation
  executors are M5+). The production run boundary still uses
  `UnavailableAgentActionBroker` until M8.

## Gate results (2026-07-25 corrective pass)

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-restore` | pass, 0 errors |
| `Phase17Permission` | 36/36 pass |
| `Phase17ActionContracts` | 50/50 pass |
| `Phase17WorkspaceRead` | 39/39 pass |
| `Phase17WorkspaceAuthority` | 21/21 pass |
| `Architecture` | 26/26 pass |
| Full suite (fast) | 2936/2937; 1 pre-existing fd-count flake under the parallel runner only (passes in isolation and serially) |
| Full suite (slow.runsettings) | 2936/2937; only the pre-existing Phase 16 flake failed (passes in isolation) |
| `git diff --check` | clean |
