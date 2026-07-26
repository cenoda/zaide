# Phase 18: Live IDE Context for Agent Runs — TOFIX

## Status

M0 accepted. M1 implementation landed, failed NO-GO review, and the corrective pass
is complete as of 2026-07-26. M2 context assembly is not started.

## Current work

- [x] M0 review and acceptance by user.
- [x] M1 initial delivery.
- [x] M1 corrective pass for NO-GO findings (2026-07-26).

## M1 corrective findings addressed (2026-07-26)

- **Hard-exclusion invariant restored in `AgentContextExclusionDecision`:**
  - `isHardExclusion: true` requires `hardExclusionId` and forbids `sourceId`
  - `isHardExclusion: false` forbids `hardExclusionId`
  - `sourceId` and `hardExclusionId` remain mutually exclusive
  - either `sourceId` or `hardExclusionId` must be supplied
  - `AgentContextExclusionDecision_RejectsInconsistentHardExclusionState` now
    verifies rejection of the inconsistent state

- **Strengthened bypass ratchet:**
  - Removed vacuous "no ContextAssembly class exists" gate from
    `ContextAssembly_DoesNotBypassPolicyBoundary`
  - Added `ContextAssemblyService_RequiresPolicyMatrixRegistration` structural
    ratchet for future assembly-service types
  - Scan covers all `src/Features/Agents` production files
  - Legacy backend isolation and cross-feature Presentation/Infrastructure checks
    preserved

- **Complete snapshot immutability:**
  - `EditorStateSnapshot` defensively copies `OpenFilePaths`
  - `RepositoryStatusSnapshot` defensively copies `Branches` and `Changes`,
    including nested `FileChange` values, on init and via `CloneDefensively()`
  - `SourceControlStatusSnapshot` acquires defensive copies via
    `RepositoryStatus?.CloneDefensively()`
  - Focused mutation tests added in `Phase18SnapshotSeamTests`

- **Contract invariant tests in `Phase18ContextContractTests`:**
  - Manifest null collections/elements, UTC timestamp, invalid policy, checked
    token-sum overflow, and read-only item exposure
  - Item null content, processing-failed content rejection, redacted-state reason
    requirement
  - Metadata-only `AgentContextDisclosurePayload` structural guard and
    `AgentEventKind.ContextDisclosed` payload matching

- **Regression fix from nested `FileChange` cloning:**
  - `SourceControlViewModelTests.Refresh_SamePathAfterRefresh_ReselectsAndPreservesDiff`
    now matches diff requests by file path instead of object identity

## Verification status (corrective pass 2026-07-26)

- `dotnet build Zaide.slnx --no-restore` — 0 errors, 0 warnings
- `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase18'` — 28/28 passed
- `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Architecture'` — 36/36 passed
- `dotnet test Zaide.slnx --no-build` — 3093/3093 passed
- `git diff --check` — clean
- `git diff --cached --check` — clean

## M1 delivery scope (unchanged)

Implemented under `src/Features/Agents/Domain/`:

- Context source identifier, item, manifest, provenance, redaction state/reason,
  exclusion decision, token budget, truncation decision
- Four policy levels (Off, Minimal, Standard, Detailed) with locked source matrix
- Application default (`Standard`) and session override contracts
- Hard exclusion registry with no Phase 18 escape hatch
- Metadata-only `AgentContextDisclosurePayload` plus redaction/boundary summaries
- `AgentCapabilityId.IdeContext` and `AgentEventKind.ContextDisclosed` taxonomy

Contract-level passive snapshot seams:

- `IEditorStateSnapshotService` / `EditorStateSnapshot`
- `ITerminalSurfaceSnapshotService` / `TerminalSurfaceSnapshot` (no scrollback shape)
- `ISourceControlSnapshotService` / `SourceControlStatusSnapshot`

Legacy backend keeps `IdeContext` capability as `Unavailable`. No assembly service,
run integration, redaction detection, token counting, or disclosure UI were added.

## Next task

- [ ] M2 policy evaluation and context assembly service.

## Scope boundaries observed

M1 and the corrective pass do not implement context assembly, redaction
detection, token counting or truncation execution, run integration, disclosure UI,
session policy selector, Native Harness or ACP, persistence, memory, raw traces,
prompt engineering, or Phase 17 contract changes.
