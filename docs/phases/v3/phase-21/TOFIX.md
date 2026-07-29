# Phase 21: Agent Transparency, Continuity, and Memory — TOFIX

## Status

**M0 planning is the only active and authorized work. M1 and all later
milestones are not started. No production implementation is authorized.**

Phase 20 remains complete, published, accepted, and unchanged at
`597b7b5151de8ab8f6091adb69809a531dd49357`. It is an independent ACP sibling
backend, not a Native Harness wrapper or fallback.

This M0 is documentation-only. It does not authorize production code, tests,
tools, dependencies, UI, persistence, memory, trace, recovery, backend
behavior, external candidate activity, network/provider execution,
credentials, paid services, destructive actions, or Phase 16 evaluation
campaigns.

## M0 work board

- [x] Read `AGENTS.md`, `docs-rules.md`, `docs/CONVENTIONS.md`,
      `docs/DESIGN.md`, and `docs/roadmap/V3.md` completely.
- [x] Read Phase 20 `IMPLEMENTATION_PLAN.md`, `TOFIX.md`, and
      `M6_CLOSEOUT_EVIDENCE.md` completely.
- [x] Verify clean `master` planning baseline at
      `597b7b5151de8ab8f6091adb69809a531dd49357`, synchronized with
      `origin/master`.
- [x] Verify the accepted Phase 20 baseline and preserve its history and
      acceptance wording.
- [x] Inspect live Agent Session, run, normalized event, action audit,
      capability, backend binding, Native Harness, ACP, process-shutdown, and
      cancellation seams.
- [x] Inspect live Conversation, schema-v1 persistence, last-known-good
      recovery, Townhall projection, drafts/read state, and startup hydration
      seams.
- [x] Inspect relevant production code, focused tests, architecture ratchets,
      and Phase 20 adversarial exclusions.
- [x] Confirm no existing Phase 21 plan, production code, tests, tools,
      dependency, UI, persistence, memory, trace, recovery, or backend
      behavior exists.
- [x] Record the backend-neutral ownership and scope model.
- [x] Record mandatory secret-safety, usage/cost truthfulness, recovery,
      termination, memory-integrity, and durable-record invariants.
- [x] Record retention, export, deletion, backup, migration, ordering,
      idempotency, replay, schema-versioning, workspace-isolation, and
      restart-state questions.
- [x] Keep implementation choices that require evidence explicitly open.
- [x] Define M1–M7 goals, dependencies, allowed surfaces, required artifacts,
      exact gates, rollback points, limitations, stop conditions, and exit
      conditions without starting them.

## M0 publication gate

After the document content is frozen:

1. Stage exactly the two Phase 21 M0 planning documents.
2. Run the required documentation-only M0 verification gates.
3. If every gate passes, publish one reviewable commit:
   `docs(phase-21): establish M0 plan`.
4. Verify clean synchronized post-push state, no `src`/`tests`/`tools`
   changes, no Phase 21 implementation, and M1+ not started.

This procedure is part of the M0 boundary, not a self-referential publication
status field. The final commit hash and post-push results are reported after
push because a commit cannot truthfully contain its own hash.

## Live M0 findings

- `AgentSessionService` is the backend-neutral in-memory lifecycle owner. It
  keeps one session per conversation, creates session/run IDs, serializes event
  order, and clears live ownership on end/disposal. It has no durable recovery
  record or restart reconstruction.
- `AgentEvent` schema v1 carries typed lifecycle/evidence/correlation data, but
  `AgentEventStream` is in-memory only and has no durable replay contract.
- `AgentConversationEventProjection` is the sole normalized event-to-
  conversation writer. That boundary remains locked for Phase 21.
- Conversation schema v1 persists conversations/entries, channels, active
  selection, drafts, and read cursors with temp-file and last-known-good
  handling. It does not persist Agent Sessions, runs, backend bindings,
  capability snapshots, normalized events, audit, usage/cost, traces, or
  memory.
- `AgentActionAuditStore` is bounded to the current application lifetime and
  is not durable. Its summary redaction is not sufficient for arbitrary raw
  trace payloads.
- ACP usage is currently a generic backend-reported observation that changes a
  capability row; numeric usage/cost evidence is not preserved.
- ACP `Resume` and `RawTrace` are explicitly not supported/currently usable in
  the accepted Phase 20 profile. Current production continuity methods are not
  exposed through `IAcpSessionClient`.
- Backend bindings and ACP session bindings are in-memory. Restart validity,
  persistence, and recovery remain open decisions.
- Application shutdown cancels/revokes live session authority and terminates
  owned ACP process trees, but it does not persist a Phase 21 interruption or
  termination acknowledgement.
- Durable memory, trace storage, memory retrieval/injection, usage/cost ledger,
  and session recovery are absent.

## Locked M0 boundaries

- Identity, session, conversation, run, runtime, trace, usage/cost, and memory
  scopes remain distinct.
- Conversations remain authoritative communication history.
- Agent lifecycle/events/audit/trace/usage/recovery/memory remain backend-
  neutral Agents concerns with separate record semantics.
- Native Harness and ACP remain independent sibling backends.
- Accepted Phase 20 capability facts are inputs, not assumptions.
- Missing usage/cost/trace/recovery evidence is unavailable, not zero or
  success.
- Unredacted secret retention is forbidden.
- Side-effecting work is never resumed silently.
- A resumed action must be reproposed and reauthorized;
  `AgentPermissionDecision.TryConsume()` remains final.
- Human-to-Human messaging, public agent API, unredacted secret retention,
  silent side-effect resumption, and Phase 16 evaluation campaigns remain
  excluded.

## Open decisions

- Storage engine/format/layout, encryption, transaction model, and physical
  separation of record classes.
- Durable workspace identity and multi-window/process coordination.
- Trace capture default, retention/quota/rotation, redaction rules, and exact
  backend-exposed layers.
- Usage taxonomy, pricing source/version, currency, estimation, and invoice
  reconciliation.
- Recovery state machine, persisted backend binding fields, and supported
  Native Harness/ACP continuity subset.
- Memory representation, creation/edit workflow, conflict/supersession,
  retrieval/index strategy, and influence record.
- Export formats, deletion cascades/tombstones, backup/restore package, and
  migration/downgrade policy.
- Whether conversation schema v1 changes and the exact accessible UI design.

These stay open until their owning milestone resolves them with live evidence
and explicit authorization.

## Next task

After the M0 publication gate, stop for review/acceptance. Do not begin M1 or
any later milestone.
