# Phase 17 M5 — Workspace Mutation Evidence

Manual verification recorded on 2026-07-25 for the safe workspace mutation
executor behind accepted immutable proposals.

## Scope verified

- `WorkspaceFileMutator` applies create, replace, and delete only after
  revalidating captured workspace root identity, canonical root, device/inode,
  path containment, proposal binding, and base revision.
- `ContractAgentActionBroker` executes approved file mutations after atomic
  decision consumption and maps apply-time stale bases to `Conflict`.
- Same-directory temporary files are cleaned up on success, failure, and
  cancellation; success is reported only after on-disk confirmation.

## Manual checks

| Scenario | Expected | Observed |
|----------|----------|----------|
| Approved create on absent target | File appears with proposed UTF-8 bytes and revision digest | Pass |
| Approved replace with unchanged base | Target content replaced atomically | Pass |
| Approved delete with unchanged base | Target removed; no orphan temp files | Pass |
| Create race after permission (target appears) | `Conflict`; original external content preserved | Pass (automated broker/mutator tests) |
| Workspace generation change before apply | `Revoked`; no mutation | Pass (automated) |
| Duplicate correlation replay | `DuplicateReplay`; mutator invoked once | Pass (automated) |

## Automated gate reference

Focused filter: `FullyQualifiedName~Phase17WorkspaceMutation`
