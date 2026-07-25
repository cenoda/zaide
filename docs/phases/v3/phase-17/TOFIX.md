# Phase 17: Agent Action Control Plane and Workspace Mutation — TOFIX

## Status

M0 was accepted by the user on 2026-07-24. The accepted implementation
boundary and decisions P17-D01–P17-D12 are recorded in
`IMPLEMENTATION_PLAN.md`.

M1 is authorized and the corrective pass is complete. M2 remains blocked
until corrected M1 receives GO during re-audit.

## Current work

- [x] Create, audit, amend, and accept the Phase 17 implementation plan.
- [x] Complete corrected M1 contracts and deterministic state.

## Blockers closed in corrective pass (commit 4d8b459)

- Thread-safe `AgentActionRunSlotTracker` and `AgentActionCorrelationRegistry`
  admission with parallel race tests.
- Canonical resolved-command fingerprint and display binding; unresolved PATH
  tokens cannot become permission-ready command requests.
- Unbounded `Monitor.Wait` replaced with 100ms bounded polling that observes
  `CancellationToken` and registry revocation.
- Raw agent command requests separated from Zaide-resolved executable
  identity via `IAgentCommandResolver` infrastructure contract.
- `AgentCommandResolutionSource` and symlink chain metadata bound before
  permission approval; denylist classification applied to canonical target.
- Cancellation, revocation, symlink-to-shell, and PATH-retarget tests (13 new
  tests across broker + fingerprint suites).

## Gate results (commit 4d8b459)

| Gate | Result |
|------|--------|
| Focused (Phase 17) | 50/50 pass |
| Architecture | 26/26 pass |
| Full suite | 2839/2840 pass |
| Flaky failure | `Restart_DoesNotLeakFileDescriptors` (PTY fd leak) — confirmed pre-existing, passes in isolation |

## Re-audit request

M1 corrective pass is complete. Requesting re-audit for GO. The two contract
blockers (unbounded waits, unresolved command identity) are resolved.

## Next task

When M1 receives GO, implement Phase 17 M2 only:

- canonical workspace capture and generation binding for action authority;
- bounded read-only file access with traversal, symlink, binary, size, and
  cancellation defenses;
- focused `Phase17WorkspaceRead` tests.

M2 must not implement permission UI, mutation, command execution, document
reconciliation, or Agent event/Townhall integration.
