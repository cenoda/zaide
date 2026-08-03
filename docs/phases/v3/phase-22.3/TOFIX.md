# Phase 22.3: Agent-Path Enablement — TOFIX

## Status

**M0 live-seam verification complete; human M0 acceptance pending; M1 not authorized.**

Phase 22.2 is complete and its ordering dependency is satisfied. Phase 22.3
remains unimplemented. Dependency completion does not grant implementation
authorization. M1–M5, G5, and V4 have not started.

## Work Board

- [x] Confirm Phase 22.2 package-2 completion and ordering readiness.
- [x] Trace live Townhall send, typed routing, binding, session, event,
  conversation, and Townhall projection owners.
- [x] Record actionable/visible and silent outcomes, including pre-admission
  rejection, terminal states, cancellation intent, and late completion.
- [x] Verify `EndAsync`, continuity termination records, production callers,
  restart interaction, and backend acknowledgement limits.
- [x] Verify Phase 17 dispatch, policy, permission, final authorization,
  mutation/conflict, audit, and reconciliation invariants.
- [x] Verify Phase 21 checkpoint, durable partition, revalidation,
  classification, and no-silent-resume ownership.
- [x] Confirm exact existing filters with `--list-tests`.
- [x] Run focused existing filters: send/routing/projection 71/71;
  continuity/termination 12/12; broker/permission/mutation 118/118.
- [x] Define the isolated dual-backend A3 producer and evidence matrix without
  executing it.
- [x] Replace all verification placeholders and lock future test obligations,
  rollback, and migration boundaries.
- [ ] Human M0 acceptance.
- [ ] Separate Phase 22.3 implementation approval.
- [ ] M1 send/routing and outcome visibility.
- [ ] M2 explicit live-session termination.
- [ ] M3 safe mediated action path and actor attribution.
- [ ] M4 workspace-owned interrupted-run projection and explicit re-send.
- [ ] M5 owned-row dual-backend A3 re-smoke and regression closeout.

## Verified Open Product Gaps

- Pre-binding and session admission rejection has no conversation entry; the
  routed Townhall attempt clears its draft anyway.
- Direct-only router entry means a channel mention is logged as plain channel
  chat and never reaches `AgentRouter`.
- Valid routed execution belongs to the target direct conversation; the active
  source gets only target unread state and no bounded route-status row.
- `RunRejected`, `RunCancellationRequested`, `SessionEnding`, and
  `SessionEnded` are not projected. Admitted terminal failures are projected;
  accepted/running are transient panel state. No queued run state exists.
- `IAgentSessionService.EndAsync` has no production caller. Registered
  continuity/management ViewModels are not a user entry point.
- ACP cancellation currently attempts `CancelPromptAsync` with the already-
  cancelled run token; no backend acknowledgement can be claimed.
- Continuity `Terminate` writes intent/acknowledgement evidence but does not call
  live `EndAsync` or a provider deletion/termination API.
- Phase 17 early denials can return before event/audit projection; action fact
  and audit payloads lack initiating/target actor IDs.
- Phase 17 has no product multi-file transaction, change set, or rollback
  operation. Atomic replace, stale rejection, and document reconciliation are
  not rollback.
- Phase 21 durable roots are process-CWD-derived in the live session/startup
  owners. Both sibling backends currently disallow resume; explicit re-send is
  required.

## Next Task

Independent human M0 audit. Do not begin M1 until human M0 acceptance and a
separate Phase 22.3 implementation approval are both explicit.
