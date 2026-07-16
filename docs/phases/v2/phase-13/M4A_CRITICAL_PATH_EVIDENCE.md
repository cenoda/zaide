# Phase 13 M4a: Bounded Automated V2 Critical-Path Evidence

**Status: COMPLETE (2026-07-16)**
**Composition type:** Named focused proofs + one minimal real-fixture open-project
proof. Not a monolithic UI automation suite. Not a fake end-to-end test that
calls unrelated test methods.

**Fixture:** `tests/fixtures/workflow-console/`
- `WorkflowConsole.csproj` SHA-256 `cdd6f0b4e4b9c72196431282fc7f42508e52e0f6a10b88a948a157bbdabd220b`
- `Program.cs` SHA-256 `617a20b62997f6cbed8a0658a011ac1de1b59d68f999381866ac7ca20bee7020`
- Limitation: single thread, shallow frame (accepted for M4a; unchanged from M0).

**Environment (this evidence pass):** Linux `7.1.3-arch1-1` x86_64; .NET SDK
`10.0.109`; `csharp-ls` `0.25.0` at `/home/cenoda/.dotnet/tools/csharp-ls`.
**NetCoreDbg:** **absent** at
`/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` and
`ZAIDE_NETCOREDBG_PATH` unset. Debug/stop rows are **not re-run** this session;
they cite Phase 12 production proofs truthfully as environment-limited.

**Production code:** no `src/` change.
**New automated proof:** `Phase13M4aCriticalPathEvidenceTests` (open project +
edit/save on the workflow-console fixture via production discovery and the M0
editor command-path seam).

**Explicit non-claims:** Avalonia rendering, keyboard routing, desktop
responsiveness, interactive UX, Command Palette, and focus/status presentation
remain **M4b**.

---

## Critical-path step matrix (M4a results)

| Step | Exact test / command | Fixture | Evidence type | Environment | Max duration | Result | Limitation |
|---|---|---|---|---|---|---|---|
| Open selected C# project | `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase13M4aCriticalPathEvidenceTests.OpenSelectedCSharpProject_WorkflowConsole_LoadsSingleProjectContext'` | `tests/fixtures/workflow-console/` | Deterministic headless seam: production `FileSystemProjectFileSystem` + `ProjectDiscovery` + `ProjectContextService.LoadAsync` | Test host; no display | 10 s | **PASS** (8 ms wall in this run) | Discovers root-level `.csproj` only (production contract). Does not open the folder in the desktop shell (M4b). |
| Edit and save | `dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Phase13M4aCriticalPathEvidenceTests.EditAndSave_WorkflowConsole_OpenEditSaveRestore_Passes'` (same seam as `Phase13M0EditorMeasurementTests.OpenEditSaveRestore_Completes_AndRestoresSavedContent`) | `tests/fixtures/workflow-console/Program.cs` | Deterministic headless editor command path: `OpenFileCommand` → edit → `SaveCommand` → restore | Test host; private work copy | 10 s | **PASS** (94 ms wall in this run) | Command-path only. Not Avalonia render, keyboard, or desktop edit/save (M4b). Source fixture SHA unchanged after sample. |
| LSP result | `dotnet tools/Phase10M4CompletionHoverSmoke/bin/Debug/net10.0/Phase10M4CompletionHoverSmoke.dll <temp-copy of tools/Phase10M0LanguageIntelligenceProof/fixture>` with `csharp-ls` on `PATH` | Phase 10 language fixture (`Fixture.csproj` / `Sample.cs`); real-server production completion/hover pipeline | Real language-server process (`csharp-ls`) | `csharp-ls` 0.25.0 on PATH | 30 s | **PASS** (completion 523 items; hover `Sample.Greet`; stale dismissal OK) | Real server against the Phase 10 language fixture, not workflow-console (workflow-console has no type surface for completion). UI presentation of completion remains M4b. |
| Build | `dotnet build tests/fixtures/workflow-console/WorkflowConsole.csproj --no-restore` | workflow-console | Real `dotnet` child process | SDK 10.0.109 | 60 s | **PASS** (0.37 s) | CLI real-child process evidence per M0 matrix. Not Output-panel UI projection (M4b). Does not re-prove cancel/dispose (M3a). |
| Run or test | `dotnet run --project tests/fixtures/workflow-console/WorkflowConsole.csproj --no-restore --no-build` | workflow-console | Real `dotnet` child process | SDK 10.0.109 | 60 s | **PASS** (stdout: `workflow-console smoke` / `workflow-console breakpoint target`) | Run path only (test path remains covered by M1a/workflow-tests-pass fixture). Not Output-panel UI projection (M4b). |
| Debug to one breakpoint | `ZAIDE_NETCOREDBG_PATH=<adapter> dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName=Zaide.Tests.Services.M4DebugExecutionProofTests.ProductionProof_LaunchBreakpointStepAndStop'` | workflow-console + NetCoreDbg | Real adapter (when present) | Requires existing adapter at `ZAIDE_NETCOREDBG_PATH` or Phase 12 path; **not downloaded/bundled** | 60 s | **not re-run / environment-limited** | Adapter absent this session. Truthful Phase 12 M4 production proof remains the automated evidence when the adapter is present; silent no-op skip when absent. Not Debug UI (M4b). |
| Stop and cleanup | `ZAIDE_NETCOREDBG_PATH=<adapter> dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName=Zaide.Tests.Services.M6DebugRecoveryProofTests.ProductionProof_StopRecoverAndRestart_ClearsLiveStateAndAdapterProcess'` | workflow-console + NetCoreDbg | Real adapter stop/recover (when present) | Same adapter requirement as debug row | 30 s | **not re-run / environment-limited** | Adapter absent this session. Phase 12 M6 production proof is the named cleanup evidence when the adapter is present. Fake-adapter recovery remains covered by `DebugSessionServiceTests` (M3c). |

---

## Composition rules (honesty)

1. Each row is a separate evidence boundary. M4a does **not** claim a single
   process that walks every step through one UI session.
2. Real-server, real-`dotnet`, and real-adapter steps remain real-child proofs.
   They are not replaced by fakes for this milestone.
3. When NetCoreDbg is absent, debug/stop rows are **not** marked newly passed.
4. Desktop, keyboard, focus, and status rows stay **M4b**.

---

## Focused verification commands (sequential)

```bash
# 1–2. M4a headless open + edit/save (workflow-console)
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Phase13M4aCriticalPathEvidenceTests'

# 3. Real-server LSP (build smoke first if needed)
dotnet build tools/Phase10M4CompletionHoverSmoke/Phase10M4CompletionHoverSmoke.csproj --no-restore
export PATH="$PATH:$HOME/.dotnet/tools"
TMP=$(mktemp -d)
cp -a tools/Phase10M0LanguageIntelligenceProof/fixture "$TMP/fixture"
rm -rf "$TMP/fixture/bin" "$TMP/fixture/obj"
dotnet tools/Phase10M4CompletionHoverSmoke/bin/Debug/net10.0/Phase10M4CompletionHoverSmoke.dll "$TMP/fixture"
rm -rf "$TMP"

# 4–5. Real-child build and run
dotnet build tests/fixtures/workflow-console/WorkflowConsole.csproj --no-restore
dotnet run --project tests/fixtures/workflow-console/WorkflowConsole.csproj --no-restore --no-build

# 6–7. Real adapter only when present (do not download)
# ZAIDE_NETCOREDBG_PATH=/path/to/netcoredbg \
#   dotnet test Zaide.slnx --no-build \
#   --filter 'FullyQualifiedName=Zaide.Tests.Services.M4DebugExecutionProofTests.ProductionProof_LaunchBreakpointStepAndStop'
# ZAIDE_NETCOREDBG_PATH=/path/to/netcoredbg \
#   dotnet test Zaide.slnx --no-build \
#   --filter 'FullyQualifiedName=Zaide.Tests.Services.M6DebugRecoveryProofTests.ProductionProof_StopRecoverAndRestart_ClearsLiveStateAndAdapterProcess'
```

---

## Full sequential gate (this session)

| Command | Result |
|---|---|
| `dotnet build Zaide.slnx --no-restore` | PASS, 0 errors |
| `dotnet test Zaide.slnx --no-build` | PASS, **2172** passed / 0 failed / 0 skipped |
| `git diff --check` | PASS |

Focused M4a filter: **2** passed (`Phase13M4aCriticalPathEvidenceTests`).

---

## Adapter / environment limitation

| Item | Status |
|---|---|
| `ZAIDE_NETCOREDBG_PATH` | unset |
| Default Phase 12 proof path | `/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` — **absent** |
| M4a action | Did not download or bundle NetCoreDbg. Debug and stop/cleanup rows recorded as **not re-run / environment-limited** with citation of Phase 12 `M4DebugExecutionProofTests` and `M6DebugRecoveryProofTests`. |
| When adapter returns | Re-run the two filters above with `ZAIDE_NETCOREDBG_PATH` set; update this document and `M0_RELEASE_BASELINE_PROOF.md` §7 only if results change. |

---

## Exact next milestone

**M4b and M5 are closed.** M4b records desktop/platform/keyboard/focus/status
rows in `M4_RELEASE_SMOKE_EVIDENCE.md`. M5 closeout is in
`M5_RELEASE_CLOSEOUT_EVIDENCE.md` (Phase 13 COMPLETE with explicit limitations).
Debug/stop remain environment-limited when NetCoreDbg is absent.
