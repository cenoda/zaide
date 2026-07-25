# Phase 17: Agent Action Control Plane and Workspace Mutation — TOFIX

## Status

M0 was accepted by the user on 2026-07-24. The accepted implementation
boundary and decisions P17-D01–P17-D12 are recorded in
`IMPLEMENTATION_PLAN.md`.

M1 received GO and is complete. M2 (canonical workspace capture and bounded
read-only file access) is authorized and implemented on 2026-07-25. M3 remains
blocked until M2 receives GO.

## Current work

- [x] Create, audit, amend, and accept the Phase 17 implementation plan.
- [x] Complete M1 contracts and deterministic state (GO).
- [x] Complete M2 canonical workspace capture and bounded read-only file access.

## M2 implementation summary

- **Canonical workspace identity/generation capture.** Added
  `WorkspaceActionScope` (`Features/Workspace/Domain`) bundling workspace
  identity, generation, and the captured absolute root, and
  `IWorkspaceActionAuthority` (`Features/Workspace/Contracts`) so the broker can
  re-resolve the live workspace state.
- **Run/action authority binding to generation.** The broker captures a scope
  at admission and re-resolves `IWorkspaceActionAuthority.IsCurrent` immediately
  before execution; a stale generation returns `Revoked` /
  `StaleWorkspace` and never touches the filesystem.
- **Bounded read-only regular-file access.** Added `IAgentFileReader`
  (`Features/Agents/Contracts`) implemented by `WorkspaceFileReader`
  (`Features/Agents/Infrastructure`) — the Zaide-owned read boundary, not the
  editor `IFileService`. Reads are bounded to the locked 1 MiB budget and return
  content plus a lowercase SHA-256 revision.
- **Defenses.** Traversal and absolute paths are rejected at
  `AgentWorkspaceRelativePath`; separator and host case behavior are covered;
  the reader canonicalizes root and target with `realpath`, enforces a
  path-boundary containment check (defeating textual-prefix siblings), rejects
  file/directory symlink escapes, re-validates the opened descriptor via
  `/proc/self/fd` to defeat retarget/TOCTOU, rejects missing paths,
  directories, special files (via `stat` file-type check), binary/invalid-UTF-8
  content, oversized files, and unreadable files, and honors cancellation.
- **Stable digest and bounded results.** Repeated reads of an unchanged file
  produce identical revisions; a growing file is rejected as `TooLarge` rather
  than silently truncated.
- **Duplicate non-reexecution.** A duplicate correlation key + fingerprint
  returns `DuplicateReplay` without invoking the reader a second time.

## Gate results (2026-07-25)

| Gate | Result |
|------|--------|
| Build | pass, 0 errors |
| `Phase17WorkspaceRead` | 29/29 pass |
| `Phase17ActionContracts` | 50/50 pass |
| Architecture | 26/26 pass |
| Full suite | 2868/2869 pass |
| Flaky failure | `Restart_DoesNotLeakFileDescriptors` (PTY fd leak) — confirmed pre-existing, passes in isolation |
| `git diff --check` | clean |

## Scope boundaries observed

M2 did not implement permission UI, mutation, command execution, document
reconciliation, or Agent event/Townhall integration. The production execution
path still uses `UnavailableAgentActionBroker`; the read executor and workspace
authority are exercised by focused tests and are wired into the live run
boundary in M8.

## Next task

When M2 receives GO, implement Phase 17 M3 only: permission classification,
decision lifecycle, revocation, exact-request fingerprints, and a minimal
visible review surface, per the plan's M3 section.
