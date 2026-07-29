# Phase 21 M4 — Restart and Termination Evidence

**Milestone:** M4 — session continuity and explicit termination
**Depends on:** M1 `SessionRecovery` record class; M2/M3 optional evidence references
**Status:** Complete; published; verification gates pass with zero failures.
**Published commit (implementation):** `4b2af9f341df21cea11c99787a2de95b7be0e7f7` (`feat(phase-21): establish M4 session continuity and explicit termination`)
**Published commit (final publication):** `fbf5f5c31618ec4dc318873b3c8c5e3117762af1` (`docs(phase-21): mark M4 publication gate complete`)

---

## 1. Outcome and ownership

| Decision | M4 lock |
|----------|---------|
| Continuity coordinator | `AgentSessionContinuityCoordinator` over M1 `SessionRecovery` records |
| Checkpoint writer | `AgentSessionContinuityCheckpointWriter` |
| Inspector | `AgentSessionContinuityInspector` |
| Revalidation | `AgentSessionContinuityRevalidator` with binding store + backend adapters |
| Session boundary | `IAgentSessionService` additive reconcile/resume/terminate methods delegating to coordinator |
| Startup reconcile | `AgentSessionContinuityStartupReconciler` invoked from `App.axaml.cs` |
| Shutdown checkpoint | `ApplicationShutdown` calls `CheckpointActiveSessions` before session disposal |
| Event checkpoints | `AgentSessionContinuityEventSubscriber` records lifecycle transitions without blocking the event pipeline |
| Backend adapters | `NativeHarnessAgentContinuityAdapter`, `AcpAgentContinuityAdapter` |
| Presentation | `AgentSessionContinuityAvailabilityProjection`, `AgentSessionContinuityAvailabilityState`, `AgentSessionContinuityInspectionViewModel` |
| Architecture ratchet | `Phase21RecoveryRatchetTests` |

---

## 2. Verification gates

```bash
git diff --cached --check
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase21Recovery|FullyQualifiedName~Phase21Termination|FullyQualifiedName~Phase21Restart"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase17ProposalBroker|FullyQualifiedName~Phase17PermissionLifecycle"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

| Gate | Result |
|------|--------|
| M4 continuity tests | 18 discovered, 0 failures |
| Phase 17 permission regressions | pass |
| Architecture | pass after M4 baseline update (+34 internal types) |

---

## 3. Required behavior checklist

| Required behavior | M4 evidence |
|-------------------|-------------|
| Durable checkpoints before/after material lifecycle transitions | `AgentSessionService.RecordContinuityCheckpointLocked`, event subscriber, shutdown checkpoint |
| Startup reconciliation without automatic side-effect resume | `AgentSessionContinuityStartupReconciler`, `Reconcile` tests |
| Explicit user resume only | `Resume` API; no auto-resume in reconcile test |
| Revalidate Actor/workspace/conversation/session/runtime/schema/capability | `AgentSessionContinuityRevalidator` fingerprint + binding checks |
| Invalidate prior permission decisions for resumed work | New `ExecutionRunId` per resumed send; no permission replay path |
| Distinguish terminate/abandon/archive/reconnect/resume/retry/replay/new session | `AgentSessionContinuityOperationKind` taxonomy |
| Recoverable/terminal/indeterminate states | `AgentSessionContinuityClassification` + reconcile tests |
| Idempotent startup/reconcile/terminate/resume | idempotency key tests |
| Late completion and disconnect evidence | checkpoint fields + restart test |
| Separate termination intent and acknowledgement | terminate test + acknowledgement enum |
| No provider deletion claim without evidence | ACP adapter + terminate-no-claim test |
| Native Harness / ACP capability matrix | `AgentBackendContinuityCapabilityMatrix` + restart test |

---

## 4. Test files

| File | Coverage |
|------|----------|
| `tests/Zaide.Tests/Features/Agents/Continuity/Phase21ContinuityTestSupport.cs` | Fixtures |
| `tests/Zaide.Tests/Features/Agents/Continuity/Phase21RecoveryTests.cs` | Resume, reconcile, identity mismatch, idempotency |
| `tests/Zaide.Tests/Features/Agents/Continuity/Phase21TerminationTests.cs` | Terminate, abandon, acknowledgement, idempotency |
| `tests/Zaide.Tests/Features/Agents/Continuity/Phase21RestartTests.cs` | Restart reconcile, startup idempotency, capability matrix |
| `tests/Zaide.Tests/Architecture/Phase21RecoveryRatchetTests.cs` | M1 routing, feature ownership, backend isolation |

---

## 5. M4 limitations preserved

- Backend `session/resume` is not invoked for ACP or Native Harness in the accepted profiles.
- Resume prepares session identity for a new run; it does not replay in-flight tool work.
- Binding store remains in-memory; restart revalidation uses persisted fingerprint evidence.
- M5–M7 remain not started and not authorized.

---

## 6. Rollback

1. Disable startup reconcile and shutdown checkpoint in composition.
2. Revert the single M4 commit.
3. Preserve durable `SessionRecovery` records for audit; do not delete merely to silence UI.
