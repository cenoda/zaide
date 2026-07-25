# Phase 17: Agent Action Control Plane and Workspace Mutation — TOFIX

## Status

M0 was accepted by the user on 2026-07-24. The accepted implementation
boundary and decisions P17-D01–P17-D12 are recorded in
`IMPLEMENTATION_PLAN.md`.

M1 is authorized but incomplete. A corrective pass is closing audit blockers for
thread-safe single-action admission and canonical command fingerprints. M2
remains blocked until corrected M1 receives GO.

## Current work

- [x] Create, audit, amend, and accept the Phase 17 implementation plan.
- [ ] Complete corrected M1 contracts and deterministic state.

## Blockers closed in corrective pass

- Thread-safe `AgentActionRunSlotTracker` and `AgentActionCorrelationRegistry`
  admission with parallel race tests.
- Canonical resolved-command fingerprint and display binding; unresolved PATH
  tokens cannot become permission-ready command requests.

## Next task

Finish corrected M1 verification and re-audit. Do not begin M2 until M1 receives
GO.

When M1 is accepted, implement Phase 17 M2 only:

- canonical workspace capture and generation binding for action authority;
- bounded read-only file access with traversal, symlink, binary, size, and
  cancellation defenses;
- focused `Phase17WorkspaceRead` tests.

M2 must not implement permission UI, mutation, command execution, document
reconciliation, or Agent event/Townhall integration.
