# Phase 11 M2: Build + Structured Output — Manual Evidence

## Status: M2 smoke recorded (Linux CLI + automated tests)

**Date:** 2026-07-14  
**Host:** Linux, `dotnet` 10.0.109 (`/usr/bin/dotnet`)  
**Fixture:** `tests/fixtures/workflow-console/WorkflowConsole.csproj`

## Automated verification (this session)

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectBuild
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectOutput
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectWorkflow
dotnet test Zaide.slnx --no-build
git diff --check
```

All gates passed (1741 tests green).

## Fixture build smoke

```bash
dotnet build tests/fixtures/workflow-console/WorkflowConsole.csproj
```

**Result:** pass — `WorkflowConsole.dll` produced under
`tests/fixtures/workflow-console/bin/Debug/net10.0/`.

## Product behavior (verified via unit/VM tests; UI manual checklist)

| Check | Result |
|---|---|
| `project.build` registered; default `Ctrl+Shift+B` | pass (`ProjectBuildCommandTests`) |
| `project.cancel` registered; no default gesture | pass |
| CanExecute: build when eligible + idle; cancel when active | pass |
| RejectedConcurrent hidden from registry Execute while busy | pass |
| Structured Output lines (stdout/stderr, timestamp) | pass (`ProjectOutputServiceTests`) |
| Clear/replace on new generation | pass |
| Cancelled outcome status | pass |
| `BottomPanelMode.Output` distinct from Terminal | pass (compile + panel host wiring) |
| Show Output on build `Starting` (before completion) | pass (`Build_ShowOutputRequested_WhenStartingBeforeCompletion`) |
| Show Output not raised on `RejectedContext` | pass (`Build_ShowOutputRequested_NotRaisedOnRejectedContext`) |
| Terminal tab host cache unchanged | pass (visibility-only mode switch) |

## Interactive UI smoke (operator)

Not run headless in this session. Recommended manual pass:

1. Open folder containing `tests/fixtures/workflow-console/`
2. Ensure project context `SingleProject` or `Selected`
3. `Ctrl+Shift+B` or palette **Build**
4. Output panel shows structured lines; ends **Succeeded**
5. Terminal tab still works independently

## Next milestone

M3 only — build diagnostics → Problems merge (LSP retained).
