# Phase 18 M1 Corrective Pass — Completion Summary

## Status: GO (M1 corrective pass complete)

Verified on 2026-07-26 against the live repository. No commit or push was performed.

## Corrective work completed

### 1. Hard-exclusion invariant restored

`AgentContextExclusionDecision` now rejects:

- `isHardExclusion: true` without `hardExclusionId`
- `isHardExclusion: true` with `sourceId`
- `isHardExclusion: false` with `hardExclusionId`

The prior test incorrectly accepted the inconsistent state; it now asserts rejection.

### 2. Snapshot immutability hardened

- `EditorStateSnapshot` copies `OpenFilePaths` defensively
- `RepositoryStatusSnapshot` copies branch/change collections and nested
  `FileChange` values on init and in `CloneDefensively()`
- `SourceControlStatusSnapshot` stores `RepositoryStatus?.CloneDefensively()`
- Focused mutation tests live in `Phase18SnapshotSeamTests`

### 3. Contract invariant coverage expanded

`Phase18ContextContractTests` now covers manifest, item, exclusion-decision, and
disclosure-payload invariants required by the M1 corrective audit.

### 4. Architecture ratchets corrected

- Vacuous "no ContextAssembly class exists" assertion removed
- `ContextAssemblyService_RequiresPolicyMatrixRegistration` scans all Agents
  production files
- Legacy backend isolation and cross-feature dependency checks preserved

### 5. Regression fix

`RepositoryStatusSnapshot` nested cloning changed diff-tab refresh behavior in one
source-control presentation test. The mock now matches `GetDiff` by file path.

## Verification results (executed)

```text
dotnet build Zaide.slnx --no-restore
  0 errors, 0 warnings

dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase18'
  Passed: 28, Failed: 0, Total: 28

dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Architecture'
  Passed: 36, Failed: 0, Total: 36

dotnet test Zaide.slnx --no-build
  Passed: 3093, Failed: 0, Total: 3093

git diff --check
git diff --cached --check
  clean
```

Serial fallback was not required after the source-control test regression fix.

## M2 scope enforcement

No M2 implementation exists:

- no context assembly service
- no policy evaluation runtime
- no redaction detector
- no token counter
- no run integration
- no event emission
- no UI

## Changed files (working tree)

**New production contracts and seams (staged):**

- `src/Features/Agents/Domain/AgentContext*.cs` (18 files)
- `src/Features/Editor/Application/EditorStateSnapshot.cs`
- `src/Features/Editor/Contracts/IEditorStateSnapshotService.cs`
- `src/Features/SourceControl/Application/SourceControlSnapshotAvailability.cs`
- `src/Features/SourceControl/Application/SourceControlStatusSnapshot.cs`
- `src/Features/SourceControl/Contracts/ISourceControlSnapshotService.cs`
- `src/Features/Terminal/Application/TerminalSurfaceSnapshot.cs`
- `src/Features/Terminal/Contracts/ITerminalSurfaceSnapshotService.cs`

**Modified production / integration files:**

- `src/Features/Agents/Domain/AgentCapabilityId.cs`
- `src/Features/Agents/Domain/AgentEvent.cs`
- `src/Features/Agents/Domain/AgentEventKind.cs`
- `src/Features/Agents/Infrastructure/LegacyOpenAiCompatibleAgentBackend.cs`
- `src/Features/SourceControl/Application/RepositoryStatusSnapshot.cs`

**New tests (untracked):**

- `tests/Zaide.Tests/Architecture/Phase18ContextBypassRatchetTests.cs`
- `tests/Zaide.Tests/Features/Agents/Domain/Phase18ContextContractTests.cs`
- `tests/Zaide.Tests/Features/Agents/Domain/Phase18SnapshotSeamTests.cs`

**Modified tests / architecture inventory:**

- `tests/Zaide.Tests/Architecture/*` (inventory ratchet updates)
- `tests/Zaide.Tests/Features/Agents/Application/Phase17AdversarialCloseoutTests.cs`
- `tests/Zaide.Tests/Features/Agents/Infrastructure/LegacyOpenAiCompatibleAgentBackendTests.cs`
- `tests/Zaide.Tests/Features/SourceControl/Presentation/SourceControlViewModelTests.cs`

**Documentation:**

- `docs/phases/v3/phase-18/TOFIX.md`
- `PHASE18_M1_CORRECTIVE_COMPLETION_SUMMARY.md`

## Remaining risks / blockers

- None for M1. M2 remains unstarted by design.
- Working tree is uncommitted; commit/push is intentionally deferred.

## M1 verdict

**GO** — corrective invariants restored, focused and architecture tests pass, full
fast suite passes, no M2 leakage detected, whitespace checks clean.
