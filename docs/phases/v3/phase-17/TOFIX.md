# Phase 17: Agent Action Control Plane and Workspace Mutation — TOFIX

## Status

M0 was accepted by the user on 2026-07-24. The accepted implementation
boundary and decisions P17-D01–P17-D12 are recorded in
`IMPLEMENTATION_PLAN.md`.

M1 is authorized. A second corrective pass closed the remaining re-audit
blockers. M2 remains blocked until corrected M1 receives GO.

## Current work

- [x] Create, audit, amend, and accept the Phase 17 implementation plan.
- [x] Complete corrected M1 contracts and deterministic state.

## Blockers closed in second corrective pass

- **Resolver ownership:** `DefaultAgentCommandResolver` moved from
  `Application/` to `Infrastructure/` per the plan's command-adapter
  assignment. Infrastructure is the approved layer for filesystem-aware
  resolution; Application is now free of `System.IO`.
- **Fail-closed default resolver:** The production
  `DefaultAgentCommandResolver.TryResolve` always returns `false`. No
  command action is permission-ready until a trusted filesystem-aware
  infrastructure resolver is plugged in. Test-only
  `FakeTrustedCommandResolver` exercises fingerprint, display, and
  denylist binding under controlled conditions.
- **`BackendProvided` removed** from `AgentCommandResolutionSource` —
  backend claims cannot establish executable identity.
- **Cancellation-less `TryWaitForInFlightReplay` overload removed.**
  All replay-wait paths require a `CancellationToken`.
- **Architecture ratchets corrected** after the file move: 495 total
  source files, 450 Features, Agents.Infrastructure (3,0,3),
  Agents.Application back to (25,7,18).

## Gate results

| Gate | Result |
|------|--------|
| Build | pass, 0 errors, 4 existing warnings |
| Focused (Phase 17) | 50/50 pass |
| Architecture | 26/26 pass |
| Full suite | 2839/2840 pass |
| Flaky failure | `Restart_DoesNotLeakFileDescriptors` (PTY fd leak) — confirmed pre-existing, passes in isolation |

## Re-audit request

M1 second corrective pass is complete. Requesting re-audit for GO.

## Next task

When M1 receives GO, implement Phase 17 M2 only:

- canonical workspace capture and generation binding for action authority;
- bounded read-only file access with traversal, symlink, binary, size, and
  cancellation defenses;
- focused `Phase17WorkspaceRead` tests.

M2 must not implement permission UI, mutation, command execution, document
reconciliation, or Agent event/Townhall integration.
