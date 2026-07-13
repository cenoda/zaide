# Phase 11 M5: Test Command + Test Results — Manual Evidence

## Status: M5 complete

| Field | Value |
|---|---|
| Date | 2026-07-14 |
| Host | Linux `cenoda` 7.1.3-arch1-1, x86_64 |
| `dotnet` | 10.0.109 (`/usr/bin/dotnet`) |
| Parse strategy | Console-first (`TestResultsParser`); no TRX |
| Commit | `a4f9727` |

## Fixture paths

| Fixture | Path |
|---|---|
| Passing tests | `tests/fixtures/workflow-tests-pass/WorkflowTestsPass.csproj` |
| Failing tests | `tests/fixtures/workflow-tests-fail/WorkflowTestsFail.csproj` |

## Fixture smoke (`dotnet test`)

```bash
dotnet test tests/fixtures/workflow-tests-pass/WorkflowTestsPass.csproj
dotnet test tests/fixtures/workflow-tests-fail/WorkflowTestsFail.csproj
```

| Command | Result |
|---|---|
| `workflow-tests-pass` | pass — `Passed: 1, Failed: 0, Total: 1` |
| `workflow-tests-fail` | pass (expected fail) — `Failed: 1, Passed: 0, Total: 1` |

## Automated verification

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

| Filter | Count |
|---|---|
| `FullyQualifiedName~ProjectTest` | 19 passed |
| `FullyQualifiedName~TestResults` | 15 passed |
| Full suite | 1810 passed |

## Acceptance checklist

| Item | Result |
|---|---|
| `project.test` registered: Run Tests, Project, no default gesture | pass |
| CanExecute: eligible context + idle; solution and project eligible | pass |
| Execute → `StartTestAsync` → `dotnet test <path>`; Output streams | pass (M1/M2 reuse) |
| `ITestResultsService` + parser + Test Results panel + `BottomPanelMode.TestResults` | pass |
| U4: parse fail → outcome + Output only; no invented passes | pass (`TestResultsServiceTests`) |
| Cancel mid-test; one-slot shared with Build/Run | pass |
| No PTY; no DAP; M6 not started | pass |
| M6 scope not started | pass |

## CanExecute matrix (Test)

| Context | Idle | Busy |
|---|---|---|
| `SingleProject` / `Selected` + `CSharpProject` | true | false |
| `SingleProject` / `Selected` + `Solution` / `SolutionX` | true | false |
| `Unloaded`, `Loading`, `NoProject`, `Unsupported`, `Ambiguous`, `Failed` | false | false |

Run (`project.run`) remains `CSharpProject`-only per M4 (U1a); Test does not share that restriction.

## Notes

- Test Results panel is a distinct bottom-mode surface (not Terminal, not Output-only).
- Show-on-test-start reveals Test Results (and Output via existing show-on-start path).
- TRX deferred; console parse sufficient for pass/fail fixtures within M5 budget.

## Next step

M6 closeout only — see [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).
