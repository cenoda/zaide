# Phase 17 M9 — Final Adversarial Closeout Evidence

Date: 2026-07-25  
Baseline: M8 at `3545640b`  
Closeout scope: adversarial review, manual evidence consolidation,
non-deletion verification, architecture inventory confirmation, full-suite
verification, and documentation truth-sync. Phase 18 is not authorized.

## Authorization

- M8 received GO after corrective pass and serial verification on 2026-07-25.
- M9 closeout authorized in this session from baseline `3545640b`.

## Production change (M9)

`AgentSessionService.Dispose()` now cancels active run execution, matching
`CancelAsync` / `EndAsync`. This prevents shutdown from leaving a pending
permission review blocked indefinitely.

The applied order at all four sites (`Dispose()`, `CancelAsync`, `EndAsync`, and
workspace invalidation) is revoke-then-cancel: `RevokeRunBrokerLocked(activeRun)`
runs before `activeRun.ExecutionCancellation.Cancel()`. Revoking first
guarantees that an in-flight action request fails closed against a revoked
broker rather than racing the cancellation; the subsequent cancel then unblocks
the run.

## Adversarial automated coverage

`Phase17AdversarialCloseoutTests` (16 tests) maps and ratchets the required
adversarial categories to existing regression tests:

| Category | Representative regression |
|----------|---------------------------|
| Path traversal | `Phase17WorkspaceReadFileReaderTests.Normalize_AbsolutePath_IsRejectedAtBoundary` |
| Symlink escape | `Phase17WorkspaceReadFileReaderTests.Read_FileSymlinkEscapingWorkspace_ReturnsPathEscaped` |
| TOCTOU retarget | `Phase17WorkspaceReadFileReaderTests.Read_SymlinkRetargetedBetweenValidationAndOpen_ReturnsPathEscaped` |
| Duplicate replay | `Phase17ActionContractsBrokerTests.ContractAgentActionBroker_ReplaysDuplicateCorrelationKey` |
| Cancellation | `Phase17PermissionLifecycleTests.CancellationDuringReview_ReturnsCancelled` |
| Workspace switch | `Phase17SessionEventIntegrationTests.WorkspaceInvalidation_RevokesActiveRunBroker` |
| Redaction | `Phase17ActionContractsPolicyTests.AgentActionAuditSummary_RedactsSecretsAndBoundsText` |
| Process tree | `Phase17CommandExecutionTests.Executor_CancellationTerminatesProcessTree` |

Additional M9-only tests:

- `SessionDisposeDuringPendingAction_RevokesPendingAuthority` — shutdown during
  a blocking permission review completes with a terminal action result.
- `Phase15SessionEventFoundation_ProductionTypesPreserved` — non-deletion of
  session/event foundation production files and types.
- `LegacyOpenAiCompatibleBackend_PathPreserved` — non-deletion of the legacy
  HTTP compatibility backend and execution-service registration path.
- `ApplicationShutdown_RevokesAgentSessionBeforeExit` — ordered shutdown retains
  agent-session disposal after workflow/language teardown.
- Permission-review accessibility/layout source ratchets (accessible names,
  deny-first focus, resizable dialog, wrapped summary scroll region).
- `ArchitectureInventory_Phase17Closeout_HasNoUnexplainedWeakening` — inventory
  counts and root-admission ratchets unchanged.

Broader Phase 17 automated coverage remains in milestone-focused suites M1–M8
(300 targeted Phase 17 + Architecture tests at closeout).

## Manual evidence (consolidated)

Manual evidence is recorded in milestone documents; M9 links rather than
duplicates them.

| Scenario | Primary evidence |
|----------|------------------|
| Allow | [`M3_PERMISSION_REVIEW_EVIDENCE.md`](M3_PERMISSION_REVIEW_EVIDENCE.md) §1, §3 |
| Deny | [`M3_PERMISSION_REVIEW_EVIDENCE.md`](M3_PERMISSION_REVIEW_EVIDENCE.md) §5 |
| Dismiss | [`M3_PERMISSION_REVIEW_EVIDENCE.md`](M3_PERMISSION_REVIEW_EVIDENCE.md) §5; `Phase17PermissionReviewViewModelTests.AllowDenyAndDismiss_ResolveExactlyOnce_FirstWins` |
| Stale proposal | [`M4_PROPOSAL_PREVIEW_EVIDENCE.md`](M4_PROPOSAL_PREVIEW_EVIDENCE.md) §10; `Phase17ProposalBrokerTests` stale-base races |
| Dirty-buffer conflict | [`M6_DOCUMENT_RECONCILIATION_EVIDENCE.md`](M6_DOCUMENT_RECONCILIATION_EVIDENCE.md) |
| Cancellation | [`M3_PERMISSION_REVIEW_EVIDENCE.md`](M3_PERMISSION_REVIEW_EVIDENCE.md) §3; [`M7_COMMAND_EXECUTION_EVIDENCE.md`](M7_COMMAND_EXECUTION_EVIDENCE.md) |
| Output truncation | [`M7_COMMAND_EXECUTION_EVIDENCE.md`](M7_COMMAND_EXECUTION_EVIDENCE.md) |
| Keyboard-only permission flow | [`M3_PERMISSION_REVIEW_EVIDENCE.md`](M3_PERMISSION_REVIEW_EVIDENCE.md) §5; Deny `IsCancel="True"` and initial Deny focus in `PermissionReviewDialog` |
| Screen-reader names | [`M3_PERMISSION_REVIEW_EVIDENCE.md`](M3_PERMISSION_REVIEW_EVIDENCE.md) §5; M9 axaml ratchet for `Allow Action` / `Deny Action` |
| Narrow/wide layout | M9 axaml ratchet: `CanResize="True"`, bounded `ScrollViewer`, wrapped summary text |
| Shutdown during pending action | M9 `SessionDisposeDuringPendingAction_RevokesPendingAuthority`; `ApplicationShutdown` disposes `IAgentSessionService` after earlier teardown owners |

## Non-deletion verification

Phase 15 session/event foundation files remain present:

- `AgentSessionService`, `AgentEventStream`, `AgentConversationEventProjection`
- `IAgentBackend`, `AgentEvent`, `AgentEventKind`, `AgentCapabilitySnapshot`

Legacy OpenAI-compatible path remains present:

- `LegacyOpenAiCompatibleAgentBackend`, `AgentExecutionService`,
  `IAgentExecutionService`
- Production DI registration in `AgentsServiceCollectionExtensions`
- Focused legacy capability tests in `LegacyOpenAiCompatibleAgentBackendTests`
- Session integration proof that legacy runs still receive
  `UnavailableAgentActionBroker` and emit no action facts

## Architecture inventory and visibility

Closeout inventory ratchet (no unexplained weakening):

| Check | Result |
|-------|--------|
| Total top-level production types | 595 |
| Total production source files | 539 |
| Features source files | 494 |
| Root-folder admission (M3 detector) | 0 violations |
| Expanded root-folder admission | 0 violations |
| Legacy architecture allowlist | unchanged (2 composition locator residuals) |
| Phase 17 bypass ratchets | pass, 5/5 |

## Gate results (2026-07-25)

| Gate | Result |
|------|--------|
| `dotnet build Zaide.slnx --no-restore` | pass, 0 errors |
| `Phase17AdversarialCloseout` | pass, 16/16 |
| Phase 17 + Architecture targeted filters | pass, 300/300 |
| Full fast suite | pass, 3064/3065 (1 pre-existing parallel fd-count flake in `Restart_DoesNotLeakFileDescriptors`) |
| Serial fallback (`slow.runsettings`) | pass, 3065/3065 |
| `git diff --check` | pass, clean |

## Scope boundaries observed

M9 did not implement Phase 18 IDE-context disclosure, a production tool-using
backend, Native Harness, ACP, persistence/resume, raw traces, durable memory,
or any architecture ratchet weakening. The repository-owned
`FakeActionRequesterBackend` remains test-only.

## Linked milestone evidence

- [`M3_PERMISSION_REVIEW_EVIDENCE.md`](M3_PERMISSION_REVIEW_EVIDENCE.md)
- [`M4_PROPOSAL_PREVIEW_EVIDENCE.md`](M4_PROPOSAL_PREVIEW_EVIDENCE.md)
- [`M5_WORKSPACE_MUTATION_EVIDENCE.md`](M5_WORKSPACE_MUTATION_EVIDENCE.md)
- [`M6_DOCUMENT_RECONCILIATION_EVIDENCE.md`](M6_DOCUMENT_RECONCILIATION_EVIDENCE.md)
- [`M7_COMMAND_EXECUTION_EVIDENCE.md`](M7_COMMAND_EXECUTION_EVIDENCE.md)
- [`M8_SESSION_EVENT_INTEGRATION_EVIDENCE.md`](M8_SESSION_EVENT_INTEGRATION_EVIDENCE.md)
