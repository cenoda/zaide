# Phase 17: Agent Action Control Plane and Workspace Mutation — TOFIX

## Status

M0 was accepted by the user on 2026-07-24. The accepted implementation
boundary and decisions P17-D01–P17-D12 are recorded in
`IMPLEMENTATION_PLAN.md`.

M1 is authorized and has not started. M2 and later milestones remain gated by
predecessor completion and the repository's automatic progression and stop
rules.

## Current work

- [x] Create, audit, amend, and accept the Phase 17 implementation plan.
- [ ] Implement M1 contracts and deterministic state.

## Next task

Implement Phase 17 M1 only:

- action, attempt, request, decision, proposal, and result identities/contracts;
- the run-scoped `IAgentActionBroker` and `AgentBackendExecutionContext` shape;
- deterministic lifecycle, fingerprint, idempotency, policy, budget, and
  redaction rules;
- display-ready immutable summaries for every action kind;
- focused `Phase17ActionContracts` tests.

M1 must not perform file I/O, process execution, permission UI, workspace
mutation, document reconciliation, or Agent event/Townhall integration.
