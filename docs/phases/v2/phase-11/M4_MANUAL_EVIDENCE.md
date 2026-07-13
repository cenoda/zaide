# Phase 11 M4: Run Command — Manual Evidence

## Status: M4 complete (command registration + tests + smoke)

**Date:** 2026-07-14  
**Host:** Linux, `dotnet` 10.0.109 (`/usr/bin/dotnet`)  
**Fixture:** `tests/fixtures/workflow-console/WorkflowConsole.csproj`

## Automated verification

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectRun
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectBuild
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectWorkflow
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectOutput
dotnet test Zaide.slnx --no-build
git diff --check
```

## Verification checklist

| Check | Result |
|---|---|
| `project.run` registered; display name Run, category Project, default Ctrl+F5 | pass (`RunCommand_IsRegisteredWithMetadata`) |
| CanExecute: eligible CSharpProject + idle → true | pass (`Run_CanExecute_ReflectsEligibilityForCSharpProject`) |
| CanExecute: solution / solutionX → false (U1a) | pass (`Run_CanExecute_IsFalseForSolutionTarget`) |
| CanExecute: busy → false | pass (`Run_CanExecute_IsFalseWhileOperationActive`) |
| Execute invokes `StartRunAsync` | pass (`Run_InvokesStartRunAsync`) |
| Registry respects CanExecute for RejectedConcurrent | pass (`RegistryExecute_RespectsCanExecute_ForRejectedConcurrent`) |
| Cancel while running invokes `CancelAsync` | pass (`Cancel_InvokesCancelAsyncWhileRunning`) |
| Show Output on Run Starting (before completion) | pass (`Run_ShowOutputRequested_WhenStartingBeforeCompletion`) |
| Show Output not raised on RejectedContext | pass (`Run_ShowOutputRequested_NotRaisedOnRejectedContext`) |
| Solution run → RejectedContext via CanExecute+registry | pass (`Run_RejectedContext_ForSolutionTarget`) |
| `ProjectWorkflowOperation.Run` handled by `ProjectOutputService` (reuses all operation kinds) | pass (compile + existing ProjectOutputServiceTests) |
| Complete full gate suite | pass |

## Fixture smoke

```bash
dotnet build tests/fixtures/workflow-console/WorkflowConsole.csproj
```

**Result:** pass — fixture builds successfully.

## Policy compliance

| Locked contract | Status |
|---|---|
| U1a: Run ineligible for Solution/SolutionX → RejectedContext / CanExecute false | Verified |
| U7: No OutputType probe; class libraries remain eligible; non-zero exit → Failed | Verified (Resolve does not check OutputType; dotnet run non-zero → Failed) |
| Target: only `IProjectContextService` / `ProjectContext.SelectedProject.FilePath` | Verified (same path as Build) |
| Terminal: never attach Run to PTY | Verified (reuses existing redirected process) |
| Problems: Run does not own build-diagnostics lifecycle | Verified (no new diagnostics path) |
| Navigation / editor: no new open-file seams | Verified (no new navigation code) |
| API dual path: UI CanExecute and programmatic StartRunAsync both reject concurrent/ineligible | Verified (CanExecute + Resolve) |

## Next milestone

M5 only — Test command + ITestResultsService + Test Results surface.
