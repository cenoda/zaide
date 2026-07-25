# Phase 17: Agent Action Control Plane and Workspace Mutation — TOFIX

## Status

M0 was accepted by the user on 2026-07-24. The accepted implementation
boundary and decisions P17-D01–P17-D12 are recorded in
`IMPLEMENTATION_PLAN.md`.

M1 is authorized and complete. M2 is the next bounded implementation milestone and
remains gated by predecessor completion and the repository's automatic progression
and stop rules.

## Current work

- [x] Create, audit, amend, and accept the Phase 17 implementation plan.
- [x] Implement M1 contracts and deterministic state.

## Next task

Implement Phase 17 M2 only:

- canonical workspace capture and generation binding for action authority;
- bounded read-only file access with traversal, symlink, binary, size, and
  cancellation defenses;
- focused `Phase17WorkspaceRead` tests.

M2 must not implement permission UI, mutation, command execution, document
reconciliation, or Agent event/Townhall integration.
