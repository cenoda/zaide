# A3 Debugging Preflight — Negative-Path Evidence (`A1-DB-01` only)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 debugging preflight only** — negative-path
evidence for row `A1-DB-01`.
**Evidence date:** 2026-08-01
**Repo head at run:** `eebc49c2fc71f335d892f4e122883486b0901935`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime preflight evidence** (negative-path only) |
| **A3 slice** | Debugging preflight (`A1-DB-01` negative paths) |
| **A3 as a whole** | **Incomplete** — positive debugging smoke, Git, Townhall, agents, permissions, trace, memory, restart-recovery **not executed** |
| **A4 / stabilization / V4** | **Not begun** |
| Parent `A1-DB-01` from this run | **BLOCKED** (positive adapter-dependent behavior) — **not** `WORKS` |
| Real desktop UI / xdtools / screenshots / desktop pointer / manual pointer | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| Package pins / audit policy modified | **No** |
| `netcoredbg` installed / downloaded / copied / substituted | **No** |
| Fake DAP sessions or test doubles | **Not used** |
| Prior A2 / A3 debugging evidence rewritten | **No** |
| Real user `~/.config/zaide` used | **No** (disposable `HOME` + `XDG_*` only) |
| Repository tree used as workspace root | **No** |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A2_DEBUGGING_AND_OUTPUT.md](./A2_DEBUGGING_AND_OUTPUT.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)

**Explicitly out of scope this run:**

- Positive breakpoint, stepping, stack, variables, or DAP session success
- Git, Townhall, agents, permissions, trace, memory, restart
- A4, stabilization, V4 planning
- Production / tracked-test / package / policy edits

---

## 1. Classification (authoritative for this preflight)

| id / sub-path | Classification | Summary |
|---------------|----------------|---------|
| **`A1-DB-01` (parent)** | **BLOCKED** | Positive adapter-dependent smoke (breakpoint hit, step, stack/variables, live DAP) requires a real `netcoredbg` via `ZAIDE_NETCOREDBG_PATH` or `PATH`. This host has none. Parent row is **not** upgraded to `WORKS`. Missing `netcoredbg` is **not** classified as missing production wiring. |
| Missing adapter (F5 + retry) | **WORKS** | Production `debug.startOrContinue` → build → target resolve → `StartLaunchAsync` → `AdapterUnavailable`; `DebugSessionState.Failed`; status/console project locator message; F5 remains usable and retries with the same truthful failure. |
| Build failure before launch | **WORKS** | Deterministic compile error → F5 → `BuildFailed` / `"Build failed."`; session not `Running`; F5 still usable; after fix, retry progresses past build to `AdapterUnavailable` (still no false Running). |
| Unsupported launch target | **WORKS** | Disposable project overrides `TargetPath` to a missing absolute `.dll` → F5 → `UnsupportedLaunchTarget` / `"TargetPath query resolved to a missing file."`; F5 usable; after context corrected to a normal project, retry proceeds to `AdapterUnavailable`. |
| Rapid F5 / Shift+F5 | **UNVERIFIED** (sub-path **not executable**) | F5 fails to `AdapterUnavailable` without an active session; `debug.stop` / Shift+F5 `CanExecute=false`. Not fabricated. |
| Visual breakpoint margin / DebugPanel paint / keyboard focus / multi-thread picker | **UNVERIFIED-VIS** | Not deterministically observable under headless drawing; not claimed. |

Allowed classifications used: `WORKS`, `BLOCKED`, `UNVERIFIED`, `UNVERIFIED-VIS`.
Do **not** read this table as A3 debugging complete.

**Friction (observed, not a negative-path failure):** pre-launch failures publish `Failed` without entering `Starting`, so `WhenShowDebugRequested` does not switch the bottom panel to `Debug`. Build handoff still shows **Output**. `DebugPanelViewModel` remains activated and still projects status + console lines; `DebugSessionViewModel.StatusMessage` surfaces the failure. Not claimed as a missing-wiring defect in this preflight.

---

## 2. Adapter lookup preflight (exact)

Host environment was **not** modified to manufacture a positive adapter path.

```text
$ command -v netcoredbg
# empty; exit status 1

$ echo "ZAIDE_NETCOREDBG_PATH=${ZAIDE_NETCOREDBG_PATH-<unset>}"
ZAIDE_NETCOREDBG_PATH=<unset>

$ type -a netcoredbg
# bash: type: netcoredbg: not found

$ which netcoredbg
# which: no netcoredbg in ($PATH)
```

Common absolute candidates (existence check only; none installed):

| Path | Result |
|------|--------|
| `/usr/bin/netcoredbg` | absent |
| `/usr/local/bin/netcoredbg` | absent |
| `$HOME/.local/bin/netcoredbg` | absent |
| `/opt/netcoredbg/netcoredbg` | absent |

In-process locator inputs (each scenario process, after isolation):

```json
{
  "ZAIDE_NETCOREDBG_PATH": "<unset>",
  "ZAIDE_NETCOREDBG_PATH_file_exists": false,
  "netcoredbg_on_path": null,
  "locator_would_resolve": false,
  "unavailable_message": "NetCoreDbg was not found. Set ZAIDE_NETCOREDBG_PATH or add netcoredbg to PATH."
}
```

Production equivalent: `DebugAdapterLocator.Resolve()` returns `null` →
`DebugSessionOutcomeKind.AdapterUnavailable` with
`DebugAdapterLocator.UnavailableMessage`.

---

## 3. Harness construction (temporary; deleted after capture)

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-db-g3X3QS1x/` (removed after capture) |
| Project | `/tmp/zaide-a3-db-g3X3QS1x/runner/Zaide.Tests.csproj` |
| Assembly | **`Zaide.Tests`** (`InternalsVisibleTo`) |
| TFM | `net10.0` |
| Package | `Avalonia.Headless` **12.0.5** + `ReactiveUI.Avalonia.Microsoft.Extensions.DependencyInjection` **12.0.3** (runner only) |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Command path | Production `ICommandRegistry.Execute("debug.startOrContinue")` / `workspace.openFolder` |
| Folder open | LIFO production `PickFolder` Interaction → disposable workspace |
| Not used | xdtools, screenshots, desktop automation, fake DAP, service replacements for debug |

### 3.1 Isolation protocol

`HOME` and all `XDG_*` set **before** production composition. `ZAIDE_NETCOREDBG_PATH` left unset.

| Variable | Disposable value pattern |
|----------|--------------------------|
| `HOME` | `$PROFILE_ROOT/home` |
| `XDG_CONFIG_HOME` | `$PROFILE_ROOT/config` (absolute) |
| `XDG_DATA_HOME` | `$PROFILE_ROOT/data` |
| `XDG_STATE_HOME` | `$PROFILE_ROOT/state` |
| `XDG_CACHE_HOME` | `$PROFILE_ROOT/cache` |
| `DOTNET_CLI_HOME` | `$PROFILE_ROOT/dotnet-cli` |
| `NUGET_HTTP_CACHE_PATH` | `$PROFILE_ROOT/nuget-http-cache` |
| `NUGET_PACKAGES` | `/home/cenoda/.nuget/packages` (existing offline cache; not written by policy intent) |
| `ZAIDE_NETCOREDBG_PATH` | **unset** |

Preflight: `SettingsPathResolver.GetSettingsDirectory()` under `$XDG_CONFIG_HOME/zaide`, not real-user `~/.config/zaide` (`preflight_ok: true` every scenario).

### 3.2 Disposable profiles (this capture)

| Scenario | Profile root | Exit | Classification |
|----------|--------------|------|----------------|
| Missing adapter | `/tmp/zaide-a3-db-profile-29GwQVvJ` | 0 | WORKS |
| Build failed | `/tmp/zaide-a3-db-profile-D47nBXCx` | 0 | WORKS |
| Unsupported target | `/tmp/zaide-a3-db-profile-JJTi4kjx` | 0 | WORKS |
| Rapid F5 / Shift+F5 | `/tmp/zaide-a3-db-profile-nAxF5kgB` | 0 | UNVERIFIED (not executable) |

---

## 4. Disposable fixtures

Never the repository tree as workspace root.

### 4.1 Eligible single-project workspace (`DebugDemo`)

```text
workspace/
  DebugDemo.csproj    # net10.0 Exe
  Program.cs          # Console.WriteLine marker
  Broken.cs           # valid by default; mutated for build-failure
  NuGet.config        # local-cache only
```

Context after open: `ProjectContextState.SingleProject`, selected
`DebugDemo.csproj` / `CSharpProject`.

Normal `TargetPath` (fixture preflight):

```text
/tmp/zaide-a3-db-g3X3QS1x/fixtures/workspace/bin/Debug/net10.0/DebugDemo.dll
```

### 4.2 Unsupported launch-target workspace (`DebugUnsupported`)

SDK props/targets imported explicitly; `TargetPath` forced **after** SDK
targets to a missing absolute `.dll`:

```xml
<TargetPath>/tmp/zaide-a3-db-missing-target/DoesNotExist.dll</TargetPath>
```

`dotnet msbuild -getProperty:TargetPath` returned that path. `dotnet build`
succeeded (compile ok). Resolver then rejects missing file → production
`UnsupportedLaunchTarget`.

---

## 5. Negative-path observations

### 5.1 Missing adapter — **WORKS**

| Step | Action | Result |
|------|--------|--------|
| 1 | Open disposable `DebugDemo` workspace | `SingleProject` / eligible |
| 2 | `debug.startOrContinue` (F5) | executed; build + target resolve succeed |
| 3 | Adapter locate | `null` |
| 4 | Snapshot | `Failed`, kind **`AdapterUnavailable`** |
| 5 | Failure message | `NetCoreDbg was not found. Set ZAIDE_NETCOREDBG_PATH or add netcoredbg to PATH.` |
| 6 | VM / panel status | same message |
| 7 | Console lines | `Debug session failed: …` (Error) + `[error] …` (Error) |
| 8 | `AdapterProcessId` | `null`; state not `Running` / not `Starting` |
| 9 | F5 `CanExecute` after failure | **true** |
| 10 | F5 retry | executed again → same `AdapterUnavailable` / generation advanced |

Machine-readable excerpt:

```json
{
  "scenario_id": "A1-DB-01-missing-adapter",
  "exit_code": 0,
  "isolation": {
    "profile_root": "/tmp/zaide-a3-db-profile-29GwQVvJ",
    "resolved_settings_dir": "/tmp/zaide-a3-db-profile-29GwQVvJ/config/zaide",
    "zaide_netcoredbg_path": "<unset>",
    "preflight_ok": true
  },
  "adapter_lookup": {
    "locator_would_resolve": false,
    "netcoredbg_on_path": null
  },
  "observed": {
    "asserts_f5_1": {
      "failure_kind": "AdapterUnavailable",
      "failure_message": "NetCoreDbg was not found. Set ZAIDE_NETCOREDBG_PATH or add netcoredbg to PATH.",
      "state_is_failed": true,
      "not_running": true,
      "can_execute_after": true,
      "console_has_error": true
    },
    "asserts_f5_2": {
      "failure_kind_is_adapter_unavailable": true,
      "can_execute_after_retry": true,
      "retry_command_executed": true,
      "still_not_running": true
    },
    "after_f5_1": {
      "snapshot.state": "Failed",
      "snapshot.generation": 3,
      "snapshot.failure.kind": "AdapterUnavailable",
      "panel.lines": [
        {
          "text": "Debug session failed: NetCoreDbg was not found. Set ZAIDE_NETCOREDBG_PATH or add netcoredbg to PATH.",
          "kind": "Error"
        },
        {
          "text": "[error] NetCoreDbg was not found. Set ZAIDE_NETCOREDBG_PATH or add netcoredbg to PATH.",
          "kind": "Error"
        }
      ],
      "bottom_panel_mode": "Output",
      "is_debug_bottom_mode": false,
      "can.debug.startOrContinue": true,
      "can.debug.stop": false
    }
  },
  "classification_hint": "WORKS"
}
```

### 5.2 Build failure before debug launch — **WORKS**

| Step | Action | Result |
|------|--------|--------|
| 1 | Mutate `Broken.cs` → deterministic CS1002 (`int value = ;`) | — |
| 2 | F5 | **`BuildFailed`**, message `"Build failed."` |
| 3 | Snapshot | `Failed`; not `Running`; `AdapterProcessId=null` |
| 4 | Console | `Debug session failed: Build failed.` + `[error] Build failed.` |
| 5 | F5 still usable | `can.debug.startOrContinue=true` |
| 6 | Restore `Broken.cs`; F5 again | Build passes; reaches **`AdapterUnavailable`** (no false Running) |

```json
{
  "scenario_id": "A1-DB-01-build-failed",
  "exit_code": 0,
  "isolation": {
    "profile_root": "/tmp/zaide-a3-db-profile-D47nBXCx",
    "resolved_settings_dir": "/tmp/zaide-a3-db-profile-D47nBXCx/config/zaide"
  },
  "observed": {
    "asserts": {
      "failure_kind": "BuildFailed",
      "failure_message": "Build failed.",
      "state_is_failed": true,
      "not_running": true,
      "can_execute_after": true
    },
    "asserts_retry": {
      "failure_kind": "AdapterUnavailable",
      "not_running": true,
      "retry_executed": true,
      "reached_past_build": true
    }
  },
  "classification_hint": "WORKS"
}
```

### 5.3 Unsupported launch target — **WORKS**

| Step | Action | Result |
|------|--------|--------|
| 1 | Open `DebugUnsupported` workspace | `SingleProject` / `DebugUnsupported.csproj` |
| 2 | F5 | build succeeds; resolve fails |
| 3 | Snapshot | **`UnsupportedLaunchTarget`**, message `TargetPath query resolved to a missing file.` |
| 4 | Not Running; F5 usable | true |
| 5 | Open corrected normal workspace | `DebugDemo` `SingleProject` |
| 6 | F5 retry | **`AdapterUnavailable`** (build+resolve ok; still no adapter) |

```json
{
  "scenario_id": "A1-DB-01-unsupported-target",
  "exit_code": 0,
  "isolation": {
    "profile_root": "/tmp/zaide-a3-db-profile-JJTi4kjx",
    "resolved_settings_dir": "/tmp/zaide-a3-db-profile-JJTi4kjx/config/zaide"
  },
  "observed": {
    "asserts": {
      "failure_kind": "UnsupportedLaunchTarget",
      "failure_message": "TargetPath query resolved to a missing file.",
      "state_is_failed": true,
      "not_running": true,
      "can_execute_after": true
    },
    "corrected_context": {
      "state": "SingleProject",
      "selected_project": "/tmp/zaide-a3-db-profile-JJTi4kjx/workspace-normal/DebugDemo.csproj"
    },
    "asserts_retry": {
      "failure_kind": "AdapterUnavailable",
      "retry_executed": true,
      "not_running": true
    }
  },
  "classification_hint": "WORKS"
}
```

### 5.4 Rapid F5 / Shift+F5 — **UNVERIFIED** (not executable)

| Step | Action | Result |
|------|--------|--------|
| 1 | F5 | `AdapterUnavailable` / `Failed` |
| 2 | Session active? | **false** (not Starting/Running/Stopped) |
| 3 | `debug.stop` CanExecute | **false** |
| 4 | Shift+F5 | **Not executed** — recorded as not executable without fabricating a stop cycle |

```json
{
  "scenario_id": "A1-DB-01-rapid-retry-stop",
  "exit_code": 0,
  "observed": {
    "session_active": false,
    "can_stop_after_f5": false,
    "state_after_f5": "Failed",
    "failure_kind": "AdapterUnavailable",
    "shift_f5": {
      "executable": false,
      "reason": "No active debug session (Starting/Running/Stopped). Adapter absent → Failed; Shift+F5 gated off.",
      "can_execute_debug_stop": false
    }
  },
  "classification_hint": "NOT_EXECUTABLE"
}
```

---

## 6. F5 / Shift+F5 command traces (summary)

| # | Command | Gesture | Scenario | CanExecute before | Executed | After state | Failure kind |
|---|---------|---------|----------|-------------------|----------|-------------|--------------|
| 1 | `debug.startOrContinue` | F5 | missing adapter | true | true | Failed | AdapterUnavailable |
| 2 | `debug.startOrContinue` | F5 retry | missing adapter | true | true | Failed | AdapterUnavailable |
| 3 | `debug.startOrContinue` | F5 | build failed | true | true | Failed | BuildFailed |
| 4 | `debug.startOrContinue` | F5 after fix | build failed | true | true | Failed | AdapterUnavailable |
| 5 | `debug.startOrContinue` | F5 | unsupported target | true | true | Failed | UnsupportedLaunchTarget |
| 6 | `debug.startOrContinue` | F5 after correct | unsupported target | true | true | Failed | AdapterUnavailable |
| 7 | `debug.startOrContinue` | F5 | rapid retry/stop | true | true | Failed | AdapterUnavailable |
| 8 | `debug.stop` | Shift+F5 | rapid retry/stop | **false** | **not run** | — | — |

Production command registration (unchanged; not re-wired): `debug.startOrContinue` → `F5`; `debug.stop` → `Shift+F5`.

---

## 7. Positive-path blocker (explicit)

**Prerequisite for a later positive debugging smoke:**

1. Disposable isolated profile (`HOME` + all `XDG_*`) and disposable eligible single-project C# workspace (same rules as this preflight).
2. Real production **NetCoreDbg** available without fakes:
   - either `ZAIDE_NETCOREDBG_PATH` pointing at an absolute executable, **or**
   - `netcoredbg` on `PATH`.
3. Do **not** install from non-authoritative sources as part of audit evidence unless the host already supplies a known good binary. Phase 12 M7 recorded NetCoreDbg **3.2.0-1092** on Linux x64 under controlled conditions; re-measure against whatever production-authorized binary the disposable host provides.
4. Then exercise: F5 start → Debug panel → breakpoint (F9) → stop at entry / hit → F10/F11/Shift+F11 → stack/variables/current location → Shift+F5 return to Idle.

Until (2) is satisfied, positive-path rows remain **BLOCKED**. This preflight does **not** authorize positive claims, A4, stabilization, or V4 planning.

---

## 8. Machine-readable aggregate

```json
{
  "schema_version": "a3-evidence-1",
  "audit": "v1-v3-product-reality",
  "phase": "A3",
  "slice": "A3_DEBUGGING_PREFLIGHT",
  "a1_row_ids": ["A1-DB-01"],
  "overall": "INCOMPLETE",
  "repo_head": "eebc49c2fc71f335d892f4e122883486b0901935",
  "adapter_lookup": {
    "command_v_netcoredbg": null,
    "ZAIDE_NETCOREDBG_PATH": "<unset>",
    "locator_would_resolve": false
  },
  "parent_classification": {
    "A1-DB-01": "BLOCKED"
  },
  "subscenarios": {
    "missing_adapter": {
      "classification": "WORKS",
      "profile": "/tmp/zaide-a3-db-profile-29GwQVvJ",
      "failure_kind": "AdapterUnavailable",
      "retry_usable": true
    },
    "build_failed": {
      "classification": "WORKS",
      "profile": "/tmp/zaide-a3-db-profile-D47nBXCx",
      "failure_kind": "BuildFailed",
      "after_fix_kind": "AdapterUnavailable",
      "never_running": true
    },
    "unsupported_launch_target": {
      "classification": "WORKS",
      "profile": "/tmp/zaide-a3-db-profile-JJTi4kjx",
      "failure_kind": "UnsupportedLaunchTarget",
      "failure_message": "TargetPath query resolved to a missing file.",
      "after_correct_kind": "AdapterUnavailable",
      "retry_usable": true
    },
    "rapid_retry_stop": {
      "classification": "UNVERIFIED",
      "executable": false,
      "profile": "/tmp/zaide-a3-db-profile-nAxF5kgB",
      "reason": "No active session without netcoredbg; Shift+F5 gated off"
    }
  },
  "visual": {
    "breakpoint_margin": "UNVERIFIED-VIS",
    "debug_panel_paint": "UNVERIFIED-VIS",
    "keyboard_focus": "UNVERIFIED-VIS",
    "multi_thread_picker": "UNVERIFIED-VIS"
  },
  "positive_path_blocker": "Real netcoredbg via ZAIDE_NETCOREDBG_PATH or PATH on disposable host",
  "a3_overall": "INCOMPLETE"
}
```

---

## 9. Cleanup confirmation

| Artifact | Action |
|----------|--------|
| `/tmp/zaide-a3-db-g3X3QS1x/` runner, fixtures, evidence JSON working copies, obj/out | Removed after preserving this summary |
| Disposable profiles `/tmp/zaide-a3-db-profile-*` | Removed |
| Child processes | Terminated via `desktop.Shutdown` / process exit |
| `netcoredbg` processes | None started (adapter never resolved) |
| Repository tracked content | **Only this evidence file** |
| Production / tests / packages / audit policy | Unchanged |
| Real-user or repository workspace paths as open workspace | **Not used** |

### 9.1 Path isolation verification

| Check | Result |
|-------|--------|
| Settings under disposable `XDG_CONFIG_HOME/zaide` | **Yes** |
| Workspace under `$PROFILE_ROOT/workspace` | **Yes** |
| `DOTNET_CLI_HOME` under profile | **Yes** |
| Real user `~/.config/zaide` used | **No** |
| Repository tree as workspace root | **No** |
| `ZAIDE_NETCOREDBG_PATH` manufactured | **No** |

---

## 10. Closeout statement

A3 debugging preflight for **negative paths only** is recorded. Parent
`A1-DB-01` remains **BLOCKED** for positive adapter-dependent behavior.
Negative-path missing-adapter, build-failure, and unsupported-target contracts
**WORK** under production composition. Rapid F5→Shift+F5 is **not executable**
without an adapter and is **UNVERIFIED**. A3 overall remains **incomplete**.
A4 / stabilization / V4 are **not authorized** by this note.
