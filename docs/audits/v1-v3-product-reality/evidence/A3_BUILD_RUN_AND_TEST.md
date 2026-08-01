# A3 Clean-Profile Smoke — Build / Run / Test (`A1-BR-01` … `A1-BR-04`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 build/run/test execution slice only** — rows
`A1-BR-01` through `A1-BR-04`.
**Evidence date:** 2026-08-01
**Repo head at run:** `37a0e5f4477efbd5af71d9d31b2ba6d374d7780d`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (BR-01…BR-04 only) |
| **A3 slice** | Build / Run / Test (`A1-BR-01`…`A1-BR-04`) |
| **A3 as a whole** | **Incomplete** — debugging, Git, Townhall, agents, permissions, trace, memory, restart-recovery **not executed** |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / desktop pointer automation / manual desktop | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| `Avalonia.Headless` added to tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` rewritten to “A3 complete” | **No** |
| Prior A2 / A3 evidence rewritten | **No** |
| Real user `~/.config/zaide` read or written | **No** (disposable `HOME` + `XDG_*` only) |
| Packages installed into repository | **No** |
| Real user NuGet state mutated | **No** (read-only `NUGET_PACKAGES=$HOME/.nuget/packages` offline assets) |
| Unit/parser tests used as A3 proof | **No** |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A2_BUILD_RUN_AND_TEST.md](./A2_BUILD_RUN_AND_TEST.md)

**Out of scope (explicit):** debugging, Git, Townhall, agents, permissions, trace, memory, restart, A4/V4, production/test/package/policy edits.

---

## 1. Four-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-BR-01` | **WORKS_WITH_FRICTION** | Disposable single-project workspace → `SingleProject` eligible; `project.build` / `run` / `test` / `cancel` via registered commands; target = context `.csproj`; show-on-start **Output** (build/run) and **Test Results** (test); concurrent second start → **`RejectedConcurrent`**; CanExecute false while active; long run cancel → **`Cancelled`** (pid observed); build output lines projected; test summary **Passed: 2 Failed: 1 Skipped: 1 Total: 4**. **Friction:** production `OutputPanel`/`TestResultsPanel` item templates NRE on virtualization recycle (`item!` null) under headless; harness applied **null-safe ItemTemplate patch only** (no production edit) so workflow could complete. Scroll/paint of Output list **UNVERIFIED-VIS**. |
| `A1-BR-02` | **WORKS** | Deterministic CS1002 in disposable `Broken.cs` → build **Failed**; Problems lists **build** diagnostic with file/line/col/severity/message/source=`build`; navigate opens `Broken.cs`; language count not cleared by build (0→0 without csharp-ls); fix + rebuild **Succeeded**, build-generation 1→2, stale build diags **0**. |
| `A1-BR-03` | **WORKS** | Offline packages present; `project.test` → summary counts pass/fail/skip; status “One or more tests failed.”; failed case navigable to `UnitTests.cs:12`; long test (`ZAIDE_BR_SLOW_TEST=1`) cancel → outcome **Cancelled**, status **“Tests cancelled. See Output for raw log.”**, `IsPartial=true` (not false success). Console-first parser projects failed case detail; pass/skip mainly via summary counts. |
| `A1-BR-04` | **WORKS** | Interactive PTY started first (marker + pid); Build → Output exclusive; Test → Test Results exclusive; Terminal mode exclusive; redirected build lines present; PTY marker retained and same pid **748885** before/after (workflow does not destroy PTY). |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

**Evidence-class distinction:** product-runtime (DI + MainWindow + workflow services + commands). Local restore/tooling was available offline — **not** a blocker this run. Parser/unit tests alone were not used as proof.

---

## 2. Harness construction (temporary; deleted after capture)

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-br/` (removed after capture) |
| Project | `/tmp/zaide-a3-br/runner/Zaide.Tests.csproj` |
| Assembly | **`Zaide.Tests`** (`InternalsVisibleTo`) |
| TFM | `net10.0` |
| Package | `Avalonia.Headless` **12.0.5** only |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Isolation | One disposable profile + process per scenario; `HOME`, all `XDG_*`, `DOTNET_CLI_HOME`, `NUGET_HTTP_CACHE_PATH` under profile; `NUGET_PACKAGES` read-only global cache for offline assets |
| Headless friction mitigation | Null-safe `ItemTemplate` on Output/TestResults list boxes only (product template NRE on recycle); **not** a production code change |
| Not used | xdtools, screenshots, desktop automation, manual UI, network package install, unit tests as proof |

### 2.1 Isolation protocol

| Variable | Disposable value |
|----------|------------------|
| `HOME` | `$PROFILE_ROOT/home` |
| `XDG_CONFIG_HOME` | `$PROFILE_ROOT/config` (absolute) |
| `XDG_DATA_HOME` | `$PROFILE_ROOT/data` |
| `XDG_STATE_HOME` | `$PROFILE_ROOT/state` |
| `XDG_CACHE_HOME` | `$PROFILE_ROOT/cache` |
| `DOTNET_CLI_HOME` | `$PROFILE_ROOT/dotnet-cli` |
| `NUGET_HTTP_CACHE_PATH` | `$PROFILE_ROOT/nuget-http-cache` |
| `NUGET_PACKAGES` | `/home/cenoda/.nuget/packages` (existing offline cache; not written by policy intent) |

Preflight: `SettingsPathResolver.GetSettingsDirectory()` under `$XDG_CONFIG_HOME/zaide`, not real-user `~/.config/zaide`.

### 2.2 Package / SDK availability

| Asset | Version / path | Offline |
|-------|----------------|---------|
| .NET SDK | 10.0.110 | Yes |
| Microsoft.NET.Test.Sdk | 17.14.1 (local cache) | Yes |
| xunit | 2.9.3 | Yes |
| xunit.runner.visualstudio | 3.0.2 | Yes |
| Avalonia.Headless (runner only) | 12.0.5 | Yes |

**Restore command (per profile workspace copy):**

```bash
dotnet restore --source /home/cenoda/.nuget/packages
```

Fixture `NuGet.config` clears public feeds and points only at the local packages folder.

### 2.3 Runner command pattern

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-br-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
export DOTNET_CLI_HOME="${PROFILE_ROOT}/dotnet-cli"
export NUGET_PACKAGES="/home/cenoda/.nuget/packages"
export NUGET_HTTP_CACHE_PATH="${PROFILE_ROOT}/nuget-http-cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME" \
  "$DOTNET_CLI_HOME" "$NUGET_HTTP_CACHE_PATH"
cp -a /tmp/zaide-a3-br/fixtures/workspace "$PROFILE_ROOT/workspace"
(cd "$PROFILE_ROOT/workspace" && dotnet restore --source /home/cenoda/.nuget/packages)

dotnet "/tmp/zaide-a3-br/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario A1-BR-0N \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-br/evidence/A1-BR-0N.json" \
  --repo-head "37a0e5f4477efbd5af71d9d31b2ba6d374d7780d" \
  --workspace "$PROFILE_ROOT/workspace"
```

### 2.4 Disposable profiles (final capture)

| Scenario | Profile root | Exit | Assertions |
|----------|--------------|------|------------|
| `A1-BR-01` | `/tmp/zaide-a3-br-profile-rAoQML1b` | **0** | 25/25 pass |
| `A1-BR-02` | `/tmp/zaide-a3-br-profile-gIGfFOoL` | **0** | 13/13 pass |
| `A1-BR-03` | `/tmp/zaide-a3-br-profile-2friHvfO` | **0** | 9/9 pass |
| `A1-BR-04` | `/tmp/zaide-a3-br-profile-9V8Deomz` | **0** | 10/10 pass |

**Total:** 57 product-runtime assertions, all pass on final capture.

---

## 3. Disposable fixture

Single hybrid C# project (Exe + xunit; `GenerateProgramFile=false`) so discovery stays **`SingleProject`** while Build, Run, and Test all target the same context file.

```text
workspace/
  DemoApp.csproj
  NuGet.config          # local-cache only
  Program.cs            # run markers + ZAIDE_BR_LONG_RUN loop
  Broken.cs             # valid by default; BR-02 mutates for CS1002
  UnitTests.cs          # pass / fail / skip / optional slow
```

### 3.1 File hashes (SHA-256, template)

| File | SHA-256 |
|------|---------|
| `Program.cs` | `57c85127494c959d606db7b40c94bd32bde86a487dfef0f08e210c908c53eeb1` |
| `Broken.cs` | `8e4fd27b2da44388183ae7dd8a0343e0ce6850649b28af03c34253c5ffe5050e` |
| `UnitTests.cs` | `34d8543a3028318a303b7cd59e84bb894ef0f1447e16fa9d95a26c91f9d205bf` |
| `DemoApp.csproj` | `8ced7512377064c4897ebad4fda4939fd61365945c3384babf4465634a236e0b` |
| `NuGet.config` | `b4762ac8afc51a28b8fbc27a8076715a7c8386de8444abfd0c7285195f93bd1f` |

### 3.2 Execution profiles (production locked argv)

| Operation | Command |
|-----------|---------|
| Build | `dotnet build "<csproj>"` |
| Run | `dotnet run --project "<csproj>"` |
| Test | `dotnet test "<csproj>"` |

---

## 4. `A1-BR-01` — target, commands, cancel, one-at-a-time, Output

### 4.1 Sequence

| Step | Action | Result |
|------|--------|--------|
| 1 | Open disposable workspace | `SingleProject` → `DemoApp.csproj` |
| 2 | `project.build` | Succeeded; Output mode; show-on-start true |
| 3 | Second build while active | `RejectedConcurrent`; CanExecute false |
| 4 | `project.run` (short) | Succeeded; markers in redirected stdout |
| 5 | Long run (`ZAIDE_BR_LONG_RUN=1`) | pid 748199 Running |
| 6 | Concurrent test while run | `RejectedConcurrent` |
| 7 | `project.cancel` | **Cancelled** |
| 8 | `project.test` | Failed (expected failing test); Test Results mode; summary 2/1/1/4 |

### 4.2 Machine-readable excerpt

```json
{
  "scenario_id": "A1-BR-01",
  "exit_code": 0,
  "isolation": {
    "profile_root": "/tmp/zaide-a3-br-profile-rAoQML1b",
    "resolved_settings_dir": "/tmp/zaide-a3-br-profile-rAoQML1b/config/zaide",
    "dotnet_cli_home": "/tmp/zaide-a3-br-profile-rAoQML1b/dotnet-cli",
    "nuget_packages": "/home/cenoda/.nuget/packages"
  },
  "observed_view_model_state": {
    "context.state": "SingleProject",
    "context.target": "/tmp/zaide-a3-br-profile-rAoQML1b/workspace/DemoApp.csproj",
    "build.last_outcome": "Succeeded",
    "build.show_output_requested": true,
    "build.bottom_mode": "Output",
    "build.output_line_count": 9,
    "build.status": "Build succeeded.",
    "concurrent.while_build": "RejectedConcurrent",
    "run.output_sample": ["ZAIDE_BR_RUN_MARKER_START", "ZAIDE_BR_RUN_MARKER_DONE"],
    "run_long.pid": 748199,
    "cancel.last_outcome": "Cancelled",
    "test.bottom_mode": "TestResults",
    "test.summary": "Passed: 2  Failed: 1  Skipped: 1  Total: 4",
    "panel_template_patch": "applied"
  },
  "classification_hint": "WORKS"
}
```

### 4.3 Classification rationale — **WORKS_WITH_FRICTION**

All workflow admission, generation, cancel, show-on-start, and output/test projection checks passed. Friction is the production Output/TestResults list template **null-item NRE** under headless virtualization recycle, mitigated only in the temporary harness.

---

## 5. `A1-BR-02` — build diagnostics → Problems + navigation

### 5.1 Sequence

| Step | Action | Result |
|------|--------|--------|
| 1 | Mutate `Broken.cs` → missing `;` (CS1002) | — |
| 2 | `project.build` | **Failed** |
| 3 | Problems build item | file `Broken.cs`, line **6**, col **33**, Error, “; expected”, source **`build`** |
| 4 | Navigate | Active tab **Broken.cs** |
| 5 | Language retention | before=0 after=0 (no csharp-ls Ready required; non-regression) |
| 6 | Restore file + rebuild | **Succeeded**; build generation **1→2**; build diags **0** |

### 5.2 Machine-readable excerpt

```json
{
  "scenario_id": "A1-BR-02",
  "exit_code": 0,
  "observed_view_model_state": {
    "build.outcome": "Failed",
    "problem.sample": {
      "kind": "Build",
      "FileName": "Broken.cs",
      "Line": 6,
      "Column": 33,
      "severity": "Error",
      "Message": "; expected",
      "Source": "build"
    },
    "nav.active_name": "Broken.cs",
    "rebuild.outcome": "Succeeded",
    "rebuild.build_diags.generation": 2,
    "rebuild.build_diags.count": 0
  },
  "classification_hint": "WORKS"
}
```

---

## 6. `A1-BR-03` — Test Results parse / project / navigate / cancel

### 6.1 Package preflight

| Package | Present offline |
|---------|-----------------|
| Microsoft.NET.Test.Sdk 17.14.1 | **Yes** |
| xunit 2.9.3 | **Yes** |

**Not BLOCKED** this run.

### 6.2 Observations

| Check | Observed |
|-------|----------|
| Show-on-start | Test Results |
| Summary | `Passed: 2  Failed: 1  Skipped: 1  Total: 4` |
| Status | `One or more tests failed.` |
| Case list | Failed case `DemoApp.UnitTests.FailingTest` with file/line |
| Navigate | `UnitTests.cs` |
| Cancel mid-run | `Cancelled`; status `Tests cancelled. See Output for raw log.`; `IsPartial=true` |

### 6.3 Machine-readable excerpt

```json
{
  "scenario_id": "A1-BR-03",
  "exit_code": 0,
  "observed_view_model_state": {
    "test.summary": "Passed: 2  Failed: 1  Skipped: 1  Total: 4",
    "test.service.summary": { "Passed": 2, "Failed": 1, "Skipped": 1, "Total": 4 },
    "nav.case": {
      "DisplayName": "DemoApp.UnitTests.FailingTest",
      "FilePath": "/tmp/zaide-a3-br-profile-2friHvfO/workspace/UnitTests.cs",
      "Line": 12,
      "CanNavigate": true
    },
    "cancel.outcome": "Cancelled",
    "cancel.service_partial": true,
    "cancel.test_status": "Tests cancelled. See Output for raw log."
  },
  "classification_hint": "WORKS"
}
```

---

## 7. `A1-BR-04` — redirected output vs PTY; bottom-panel exclusivity

### 7.1 Sequence

| Step | Action | Result |
|------|--------|--------|
| 1 | Open Terminal bottom mode; start PTY | Running; marker present; pid **748885** |
| 2 | `project.build` | Mode **Output**; only Output child visible |
| 3 | Redirected lines | 9 lines; contains DemoApp/build stream |
| 4 | `project.test` | Mode **TestResults**; only Test Results visible |
| 5 | PTY after workflow | Still Running; marker retained; **same pid 748885** |
| 6 | Switch to Terminal mode | Only Terminal child visible |

### 7.2 Machine-readable excerpt

```json
{
  "scenario_id": "A1-BR-04",
  "exit_code": 0,
  "observed_view_model_state": {
    "pty.is_running_before": true,
    "pty.is_running_after": true,
    "pty.pid_before": 748885,
    "pty.pid_after": 748885,
    "pty.marker_present": true,
    "pty.marker_after": true,
    "after_build.visible.output": true,
    "after_build.visible.terminal": false,
    "after_test.visible.test_results": true,
    "after_test.visible.output": false,
    "terminal_mode.visible.terminal": true,
    "build.output_line_count": 9
  },
  "classification_hint": "WORKS"
}
```

---

## 8. Cross-cutting limitations

1. **A3 overall incomplete** — BR-01…BR-04 only.
2. Headless Output/TestResults list virtualization: production templates NRE on null recycle; temporary harness null-safe template patch recorded as **friction**, not production fix.
3. Output list scroll-follow / visual paint **UNVERIFIED-VIS** under headless drawing.
4. Language diagnostics retention exercised only as non-regression without Ready `csharp-ls` (counts 0).
5. Console-first test parser surfaces failed cases with locations; pass/skip primarily in summary counts (product design F7/U4).
6. Offline NuGet used existing user package cache for package **bytes** only; no network restore required; no packages added to the repo.
7. Temporary runner, profiles, fixtures, caches, and logs removed after capture.

---

## 9. Cleanup performed

| Artifact | Action |
|----------|--------|
| `/tmp/zaide-a3-br/` runner, fixtures, evidence working copy, obj/out | Removed after preserving this summary |
| Disposable profiles `/tmp/zaide-a3-br-profile-*` | Removed |
| Managed child processes / PTY sessions | Terminated via process shutdown / cancel before exit |
| Repository tracked content | Only this evidence file |
| Production / tests / packages | Unchanged |

---

## 10. Path isolation verification

| Check | Result |
|-------|--------|
| Settings under disposable `XDG_CONFIG_HOME/zaide` | **Yes** |
| Workspace under `$PROFILE_ROOT/workspace` | **Yes** |
| `DOTNET_CLI_HOME` under profile | **Yes** |
| Real user `~/.config/zaide` used | **No** |
| Repository tree as workspace root | **No** |

---

## 11. Next bounded A3 slice

**A3 remains incomplete.** Recommended next journey (not begun): **Debugging** (`A1-DB-*`), or **Git**, **Townhall**, agents, permissions, or restart/recovery — one pack at a time under the same headless disposable-profile model. **A4 / V4 not authorized.**

---

## 12. Status line

**A3 Build / Run / Test (`A1-BR-01`…`A1-BR-04`): executed (product-runtime smoke).**

| Row | Classification |
|-----|----------------|
| `A1-BR-01` | **WORKS_WITH_FRICTION** |
| `A1-BR-02` | **WORKS** |
| `A1-BR-03` | **WORKS** |
| `A1-BR-04` | **WORKS** |

**A3 as a whole: incomplete.**

**A4 / stabilization / V4: not begun.**

---

*Recorded 2026-08-01. Out-of-tree Avalonia.Headless 12.0.5 product-runtime runner executed clean-profile build/run/test smoke under disposable XDG + offline NuGet assets; temporary runner and profiles removed; no production edits.*
