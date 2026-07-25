# Phase 17 M7 — Constrained Command Execution Evidence

Manual verification recorded on 2026-07-25 for Zaide-owned command
execution through the Phase 17 action-control boundary.

## Scope verified

- `IAgentCommandExecutor` (`Agents.Contracts`) and `WorkspaceCommandExecutor`
  (`Agents.Infrastructure`) execute one approved resolved command without shell
  invocation or ProjectSystem workflow runners.
- `DefaultAgentCommandResolver` binds canonical executable identity, PATH
  resolution, symlink metadata, and denylist results before permission review.
- `AgentCommandEnvironmentBuilder` constructs the locked environment only;
  request-local variables are rejected and secret values are not copied.
- `ContractAgentActionBroker` executes approved commands through the executor,
  preserves bounded stdout/stderr on `AgentActionResult.CommandExecution`, and
  revalidates command identity before start.
- `PermissionReviewViewModel.ContainmentDisclosureText` and command display
  summaries state that working-directory scope is not filesystem or network
  sandboxing.

## Manual checks

| Scenario | Expected | Observed |
|----------|----------|----------|
| Approved non-shell command with argument vector | Process starts without shell parsing; stdout/stderr captured separately | Pass |
| Denied shell interpreter (`bash -c`) | Policy denial before execution | Pass |
| Symlink to shell interpreter | Denylist bound at resolution | Pass |
| Working-directory symlink escape | Path-escaped terminal result | Pass |
| Locked environment (`NO_COLOR`, no request vars) | Only enumerated baseline present | Pass |
| Non-zero exit | Failed result with exit code | Pass |
| Output line budget exceeded | Truncated result; process tree killed | Pass |
| Cancellation during run | Cancelled result; child PID absent after cleanup | Pass |
| Concurrent command for one run | Second request denied (`ConcurrentActionRejected`) | Pass |
| Duplicate correlation key | Duplicate replay without re-execution | Pass |

## Automated gate reference

Focused filter: `FullyQualifiedName~Phase17CommandExecution`

| Gate | Result |
|------|--------|
| `Phase17CommandExecution` | pass, 22/22 |
| `Phase17` (all) | pass |
| `Architecture` | pass |
| Full fast suite | pass, 3034/3034 |
| `git diff --check` | pass, clean |

## Boundaries observed

M7 did not implement M8 session/event integration, Agent/Townhall projection,
production broker wiring in DI, or any real tool-using backend. Command execution
is exercised through focused tests and the broker seam only.
