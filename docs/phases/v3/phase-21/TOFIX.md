# Phase 21: Agent Transparency, Continuity, and Memory — TOFIX

## Status

**M1 is complete and published at `4db8320293bf443b6249b70fd2c42eab8d13b7a6`.**
**M2 is complete and published.**
**Implementation commit:** `f75de2344ee57fdd59f771c7e17c9059edffcca1`
**Publication/hash-correction commit:** `474a15b015649d50aa95ae30223a56bcb1bba3e6`
**M3 is complete and published.**
**Implementation commit:** `29bfb296` (`feat(phase-21): establish M3 usage and cost evidence ledger`)
**Fixup commit:** `c1810872` (`fix(phase-21): add missing imports for M3 usage ledger`)
**Publication commit:** `ae920091eb931eba2f0e43be086cab7f7fecff6d` (`docs(phase-21): mark M3 publication gate complete`)
**M4 is complete and published at `3a415790e984fc50fc705cb0f9a77552470d3f4b`.**
**M5–M7 are not started and not authorized.**

Phase 20 remains complete, published, accepted, and unchanged. It is an
independent ACP sibling backend, not a Native Harness wrapper or fallback.

M1 delivered the backend-neutral durable record and storage foundation only.
M2 adds the redacted trace evidence capture, bounded admission, and
inspection pipeline over the M1 Trace record class.
M3 adds the usage and cost evidence ledger with provenance, zero-cost guard,
and truthful origin distinctions.
M4 adds durable interruption classification, explicit resume/terminate
boundaries, startup reconcile, and shutdown checkpoints over the M1
`SessionRecovery` record class. M4 does not introduce memory retrieval,
prompt injection, or any M5+ product behavior.

## M4 work board

- [x] Durable `SessionRecovery` checkpoints on material lifecycle transitions.
- [x] `AgentSessionContinuityCoordinator` with reconcile/resume/terminate.
- [x] Startup reconcile without automatic side-effect resume.
- [x] Explicit resume with identity/workspace/binding revalidation.
- [x] Separate termination intent and acknowledgement states.
- [x] Native Harness and ACP continuity adapters with capability matrix.
- [x] Additive `IAgentSessionService` recovery/termination boundary.
- [x] `ApplicationShutdown` and app startup composition hooks.
- [x] Townhall/Agents presentation seam for interrupted/recoverable states.
- [x] Focused continuity/restart/termination tests and architecture ratchets.
- [x] Publish `M4_RECOVERY_STATE_MACHINE.md` and `M4_RESTART_AND_TERMINATION_EVIDENCE.md`.
- [x] Run M4 verification gates with zero failures.
- [x] Publish one reviewable M4 commit and post-push audit.

## Next task

M4 post-push audit complete. M5–M7 remain not started and not authorized.
