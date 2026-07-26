# Phase 18: Live IDE Context for Agent Runs — TOFIX

## Status

M0 accepted. M1 implementation landed, failed NO-GO review, and the corrective pass
is complete as of 2026-07-26. M2 policy evaluation and context assembly landed
2026-07-26 and is now complete. M3/M4 are not started.

## Current work

- [x] M0 review and acceptance by user.
- [x] M1 initial delivery.
- [x] M1 corrective pass for NO-GO findings (2026-07-26).
- [x] M2 policy evaluation and context assembly service (2026-07-26).

## M2 delivery (2026-07-26) - COMPLETE

Production (`src/Features/Agents/`):

- **Policy evaluation:** `AgentContextPolicyEvaluationService` resolves application
default and session override, applies the locked Off / Minimal / Standard / Detailed
levels exclusively through `AgentContextSourcePolicyMatrix`, and records policy
exclusion decisions. Custom policy remains unsupported.
- **Hard exclusions:** `AgentContextManifestBuilder` enforces
`AgentContextHardExclusionRegistry` before budget accounting (binary active file,
environment-line filtering in workflow output, fail-closed redaction drops with
`RedactionPatternMatch`). No bypass path.
- **Snapshot consumption:** `LiveAgentContextSnapshotSources` reads contract-level
services only (`IEditorStateSnapshotService`, `ISourceControlSnapshotService`,
`ILanguageDiagnosticsService`, `IBuildDiagnosticsService`, `IProjectWorkflowService`,
`ITestResultsService`, `IDebugSessionService`, `IProjectContextService`). No
Presentation or cross-feature Infrastructure references.
- **Assembly:** `AgentContextContentComposer` serializes snapshots deterministically;
`AgentContextRedactionProcessor` redacts before `AgentContextTokenEstimator` counts;
`AgentContextBudgetEnforcer` applies locked priority order with atomic drop/truncate;
`AgentContextManifestBuilder` produces `AgentContextManifest` with provenance,
exclusion, and truncation decisions.
- **Domain helpers:** `AgentContextSourcePriority`, `AgentContextTokenEstimator`,
`AgentContextPolicyEvaluationResult`, `AgentContextManifestCandidate`.

Tests:

- `Phase18ContextAssemblyTests` — policy precedence, matrix levels, hard exclusions,
  unavailable capabilities, deterministic ordering, redaction-before-counting, budget
  boundaries/overflow, atomic truncation, fail-closed redaction, provenance, and
  repeated-input determinism.

Architecture ratchets updated for M2 (+15 internal production types + 11 internal
context assembly service types). Phase 18 bypass ratchets pass with
`AgentContextManifestBuilder` naming (no forbidden `ContextAssembly` / `ContextService`
type names).

## M1 corrective findings addressed (2026-07-26)

- **Hard-exclusion invariant restored in `AgentContextExclusionDecision`**
- **Strengthened bypass ratchet** (`ContextAssemblyService_RequiresPolicyMatrixRegistration`)
- **Complete snapshot immutability** (Editor, SourceControl nested clones)
- **Contract invariant tests** in `Phase18ContextContractTests`
- **Regression fix** for nested `FileChange` cloning in SourceControl tests

## Verification status (M2 2026-07-26) - COMPLETE

- `dotnet build Zaide.slnx --no-restore` — 0 errors, 0 warnings
- `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase18'` — 51/51 passed
- `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Architecture'` — 36/36 passed
- `dotnet test Zaide.slnx --no-build` — 3116/3116 passed
- `git diff --check` — clean

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

Legacy backend keeps `IdeContext` capability as `Unavailable`.

## Next task

- [ ] M3 run integration and consumption boundary (not started).

## Scope boundaries observed

M2 does not implement run wiring, backend consumption, `ContextDisclosed` event
emission, disclosure UI, session policy selector UI, custom policy support, viewport
state, language identification, terminal scrollback, telemetry, or Phase 17 contract
changes. Legacy backend remains free of `AgentContextManifest` consumption.

M3/M4 boundaries preserved:
- no run wiring
- no backend manifest consumption  
- no ContextDisclosed event emission
- no UI
- no custom policy
- no viewport/language-ID/terminal-scrollback work
