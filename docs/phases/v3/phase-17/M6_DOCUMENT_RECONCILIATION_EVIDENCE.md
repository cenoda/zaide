# Phase 17 M6 — Document Reconciliation Evidence

Manual verification recorded on 2026-07-25 for post-mutation editor
reconciliation through the Workspace/Editor application boundary.

## Scope verified

- `IAgentDocumentReconciler` in `Features/Agents/Contracts` is the only
  reconciliation port consumed by `ContractAgentActionBroker`; the Agent layer
  does not reference `EditorViewModel`, `EditorTabViewModel`, or Editor
  Presentation types.
- `WorkspaceEditorDocumentReconciler` in `Features/Editor/Application`
  reconciles confirmed M5 mutation results against open `Workspace` documents.
- `IEditorUiDispatcher` / `AvaloniaEditorUiDispatcher` marshal document updates
  onto the UI thread.
- `Document` exposes `ReloadCleanContent`, `FlagDiskAbsent`, and
  `IsDiskAbsent` without silently overwriting dirty buffers.

## Manual checks

| Scenario | Expected | Observed |
|----------|----------|----------|
| Clean open document after replace | Buffer reloads from confirmed disk content; remains clean | Pass |
| Dirty open document after replace | Buffer and dirty state preserved; external-conflict result | Pass |
| Deleted clean open document | Disk absence surfaced; buffer not invented | Pass |
| Deleted dirty open document | Buffer preserved; disk absence flagged | Pass |
| Unopened target file | No tab opened | Pass |
| Workspace generation stale at reconciliation | Stale-workspace result; buffer unchanged | Pass |
| Observer throws during reload | Reconciliation completes; buffer updated from disk | Pass |
| UI dispatcher invoked | Document mutation runs through dispatcher | Pass |
| Disk changes again before reconciliation | Post-mutation-race result; clean buffer unchanged | Pass |

## Automated gate reference

Focused filter: `FullyQualifiedName~Phase17DocumentReconciliation`

| Gate | Result |
|------|--------|
| `Phase17DocumentReconciliation` | pass, 10/10 |
| `Phase17WorkspaceMutation` | pass |
| `Phase17Proposal` | pass |
| `Phase17ProposalBroker` | pass |
| `Phase17Permission` | pass |
| `Phase17ActionContracts` | pass |
| `Phase17WorkspaceRead` | pass |
| `Phase17WorkspaceAuthority` | pass |
| `Architecture` | pass |
| Full fast suite | pass, 3012/3012 |
| Serial fallback | pass (1 pre-existing flake isolated: `LaunchAsync_CancellationTerminatesProcessTree`) |
| `git diff --check` | pass, clean |

## Boundaries observed

M6 did not implement command execution, Agent/Townhall event integration, or
M7/M8 broker/session wiring. Reconciliation consumes confirmed M5 mutation
results only.
