# Phase 11 M3: Build Diagnostics in Problems — Manual Evidence

## Environment

| Field | Value |
|---|---|
| Date | 2026-07-14 |
| Host OS | Linux (Arch) |
| `dotnet` | 10.0.109 |
| Fixture (fail build) | `tests/fixtures/workflow-fail-build/WorkflowFailBuild.csproj` |
| Fixture (pass build) | `tests/fixtures/workflow-console/WorkflowConsole.csproj` |

## Automated verification (this session)

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~BuildDiagnostic
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProblemsBuild
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProblemsViewModel
dotnet test Zaide.slnx --no-build --filter FullyQualifiedName~ProjectWorkflow
dotnet test Zaide.slnx --no-build
git diff --check
```

| Gate | Result |
|---|---|
| Build | Pass |
| BuildDiagnostic* tests | 11 passed |
| ProblemsBuild* tests | 4 passed |
| ProblemsViewModel* tests | 10 passed |
| ProjectWorkflow* tests | 16 passed |
| Full suite | 1758 passed |
| `git diff --check` | Clean |

## Fixture smoke (CLI)

Deliberate compile error fixture:

```bash
dotnet build tests/fixtures/workflow-fail-build/WorkflowFailBuild.csproj
```

Observed MSBuild line (parser input):

```text
tests/fixtures/workflow-fail-build/CompileError.cs(2,43): error CS1002: ; expected [...]
```

Parser unit tests confirm this form is accepted and normalized to an absolute path.

## Product smoke checklist (operator)

Record when exercising the Zaide UI on Linux:

1. Open workspace containing `tests/fixtures/workflow-fail-build/`.
2. Select `WorkflowFailBuild.csproj` as project context.
3. **Ctrl+Shift+B** — Output shows build failure lines.
4. Problems lists a **build**-sourced item (`[build]` tag) for `CompileError.cs`.
5. Activate the problem — editor opens at line 2, column 43.
6. With an unrelated LSP diagnostic on another file, rebuild — LSP item remains.
7. Fix `CompileError.cs`, rebuild — build problems clear or update; LSP unchanged.

## U2 / U3 choices

| Decision | M3 choice |
|---|---|
| U2 Incremental vs end-of-build parse | **End-of-build** — diagnostics set only on build terminal workflow snapshot |
| U3 Partial diagnostics after cancel | **Keep partial** — `BuildDiagnosticsSnapshot.IsPartial = true` on `Cancelled` |

## Parser limitations (Phase 11)

- MSBuild / Roslyn CLI `path(line,col): error|warning CODE: message` only.
- Optional `[project.csproj]` suffix stripped; duplicate summary lines deduplicated.
- Relative paths resolve against target file parent directory (`TargetFilePath` dirname).
- No TRX, no live incremental parse while build is `Running`.
- Unsupported lines remain in Output only.
