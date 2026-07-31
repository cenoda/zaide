# A3 Clean-Profile Smoke — Terminal (`A1-TR-01`, `A1-TR-02`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 terminal execution slice only** — rows
`A1-TR-01` and `A1-TR-02`.
**Evidence date:** 2026-07-31
**Repo head at run:** `8fb5f55ba2264d15970bc0ebabab3588dfc09eda`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (terminal rows only) |
| **A3 slice** | Terminal (`A1-TR-01`, `A1-TR-02`) |
| **A3 as a whole** | **Incomplete** — workspace, editor/LSP, build/run/test, debugging, Git, Townhall, agents, permissions, trace, memory, restart-recovery rows **not executed** |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / pointer automation / manual desktop | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| `Avalonia.Headless` added to tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` rewritten to “A3 complete” | **No** |
| Prior FL / H0 / H1 / residual evidence rewritten | **No** |
| Real user `~/.config/zaide` read or written | **No** |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A2_TERMINAL.md](./A2_TERMINAL.md)

---

## 1. Two-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-TR-01` | **WORKS_WITH_FRICTION** | Cold headless launch → registered gesture `Ctrl+Oem3` opens bottom panel with default `BottomPanelMode.Terminal`. Linux PTY session starts (`IsRunning`, shell pid observed). Deterministic main-buffer output produces scrollback (60 retained rows). `less -R` on a disposable fixture enters alternate screen (`IsAlternateScreenActive=true`); `q` exits alt-screen; main buffer/scrollback return with prior marker intact. Terminal Find path: Find toolbar TextBox driven + `TerminalSnapshotSearch` on live `ScreenSnapshot` finds marker at absolute coordinates (row 65, cols 0–27). Shell `exit` → `RestartCommand` → new shell accepts input; exactly one standalone post-restart marker line (bash echo may add a second substring hit on the command line). **Friction:** cell paint **UNVERIFIED-VIS** under headless drawing; visible scroll thumb still absent (keyboard/scrollback model works); production backend remains Linux-only. Selection pointer paint not claimed. |
| `A1-TR-02` | **WORKS_WITH_FRICTION** | `NewTabCommand` creates a second independent `TerminalViewModel`/session. Tab A: marker + `sleep 999 &` (pid 184249). Tab B: different marker. Independent `ScreenSnapshot` capture proves output isolation both ways. Input to active B does not appear in inactive A. Close B while A keeps running and retains marker. Sole-tab close path: `TerminalTabCloseBehavior.ShouldHideBottomPanelInsteadOfClosing` → `HideBottomPanelCommand` (MainWindow `LastTabCloseRequested` destination) hides panel without destroying host/session. Reopen via gesture; same session still running, accepts new marker, retains prior A content. **Friction:** all tab titles remain static `"Terminal"`; tab-strip paint **UNVERIFIED-VIS**. |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

**Evidence-class distinction:**

| Class | Meaning in this slice |
|-------|------------------------|
| `product-runtime` | Headless shell / ViewModel / command / control-tree observation under production DI |
| `pty-service` | Live PTY I/O reflected in `TerminalViewModel.ScreenSnapshot` / lifecycle flags |

---

## 2. Harness construction (temporary; deleted after evidence capture)

Recreated the H1-proven out-of-tree Avalonia.Headless **12.0.5** runner under `/tmp` (not tracked).

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-tr/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-tr/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Package | `Avalonia.Headless` **12.0.5** only |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — **does not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Observation | ViewModel state, `ScreenSnapshot` / `IsAlternateScreenActive`, tab host lifecycle, control-tree Find TextBox, PTY markers, disposable filesystem |
| Not used | xdtools, OS screenshots, desktop pointer automation, manual UI, vim |

### 2.1 Isolation protocol

One disposable profile **per independent scenario** (TR-01 and TR-02 separate processes). `HOME` and all `XDG_*` set **before** production composition.

| Variable | Disposable value |
|----------|------------------|
| `HOME` | `$PROFILE_ROOT/home` |
| `XDG_CONFIG_HOME` | `$PROFILE_ROOT/config` (absolute) |
| `XDG_DATA_HOME` | `$PROFILE_ROOT/data` |
| `XDG_STATE_HOME` | `$PROFILE_ROOT/state` |
| `XDG_CACHE_HOME` | `$PROFILE_ROOT/cache` |

Preflight asserted `SettingsPathResolver.GetSettingsDirectory()` under `$XDG_CONFIG_HOME/zaide` and **not** `~/.config/zaide`.

### 2.2 Package versions

| Package | Version | Notes |
|---------|---------|--------|
| Avalonia (repo pin) | 12.0.5 | Unchanged `Directory.Packages.props` |
| Avalonia.Headless | 12.0.5 | Out-of-tree runner only |
| Avalonia.Headless.XUnit | n/a | Not required |
| ReactiveUI.Avalonia.ME.DI (via app) | 12.0.3 | Production DI bootstrap |

### 2.3 Runner command pattern

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-tr-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

dotnet "/tmp/zaide-a3-tr/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario A1-TR-0N \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-tr/evidence/A1-TR-0N.json" \
  --repo-head "8fb5f55ba2264d15970bc0ebabab3588dfc09eda"
```

**Observed profiles:**

| Scenario | Profile root |
|----------|--------------|
| `A1-TR-01` | `/tmp/zaide-a3-tr-profile-NxZCfnRY` |
| `A1-TR-02` | `/tmp/zaide-a3-tr-profile-W1kzwOKL` |

---

## 3. Scenario `A1-TR-01` — embedded terminal, alt-screen, search, scrollback, restart

### 3.1 Inputs and shell / PTY commands

| Step | Action |
|------|--------|
| 1 | Cold headless launch; disposable profile |
| 2 | Headless `KeyPress` **Ctrl+Oem3** (`view.toggleBottomPanel` registered gesture) |
| 3 | Observe `BottomPanelMode=Terminal`, start active session |
| 4 | `printf` loop 60 lines + marker `ZAIDE_TR01_SEARCH_MARKER_42` |
| 5 | Write fixture under `$HOME`; run `less -R '$HOME/tr01-less-fixture.txt'` |
| 6 | Send `q` to exit less |
| 7 | Search marker via Find UI TextBox + `TerminalSnapshotSearch` |
| 8 | `exit` shell; `RestartCommand`; `printf` post-restart marker |

**Not used:** `vim` (charter). **Preferred TUI:** `less -R` (succeeded; `htop` reserved as fallback).

### 3.2 Expected vs observed

| Check | Expected | Observed |
|-------|----------|----------|
| Bottom panel cold | Hidden | `IsBottomPanelVisible=false` |
| Default mode | Terminal | `BottomPanelMode=Terminal` |
| Gesture open | Visible | `IsBottomPanelVisible=true` after Ctrl+Oem3 |
| PTY running | Shell alive | `State=Running`, shell pid **184169** |
| Scrollback | Retained main buffer history | **60** scrollback rows after marker gen |
| Alt-screen enter | `IsAlternateScreenActive=true` | **true** via `less` |
| Alt-screen exit | false; main restored | **false**; marker still in buffer; scrollback **62** |
| Search | Match + coordinates | match_count=**1**, active row=**65**, cols **0–27** |
| Find UI | TextBox reachable | Find button + TextBox found; query set |
| Restart | New prompt; no dup delivery | exact marker lines=**1** (raw substring 2 = bash echo + printf) |

### 3.3 Assertions

| id | result | evidence_class | detail |
|----|--------|----------------|--------|
| `cold_bottom_hidden` | **pass** | product-runtime | Bottom panel starts hidden |
| `default_mode_terminal` | **pass** | product-runtime | Default BottomPanelMode is Terminal |
| `gesture_open_terminal_panel` | **pass** | product-runtime | Ctrl+Oem3 toggled IsBottomPanelVisible true |
| `pty_session_running` | **pass** | pty-service | State=Running IsRunning=True err= |
| `main_buffer_marker` | **pass** | pty-service | Deterministic marker present in main-buffer snapshot |
| `scrollback_or_visible_content` | **pass** | pty-service | scrollback_rows=60 |
| `alternate_screen_active` | **pass** | pty-service | Alternate screen entered via less |
| `alternate_screen_exited` | **pass** | pty-service | IsAlternateScreenActive returned false after quit |
| `main_buffer_restored` | **pass** | pty-service | Main buffer active after alt-screen exit |
| `search_matches_found` | **pass** | pty-service | match_count=1 |
| `search_navigation_api` | **pass** | pty-service | MoveToNext preserves match set |
| `restart_running` | **pass** | pty-service | Session running after RestartCommand |
| `new_prompt_available` | **pass** | pty-service | Exact post-restart marker lines=1 (raw substring hits=2 include bash echo) |
| `no_duplicate_marker` | **pass** | pty-service | Exactly one standalone post-restart marker line (got 1); raw substring=2 may include echoed command |
| `no_real_user_settings_path` | **pass** | product-runtime | Settings dir is not real-user ~/.config/zaide |
| `artifacts_under_profile` | **pass** | product-runtime | Profile artifacts enumerated under disposable root |

### 3.4 Limitations (TR-01)

- Production terminal backend is Linux-only (LinuxTerminalServiceFactory); this run is on Linux.
- Visible scrollbar/thumb not required for this row; keyboard scroll exists per A2. Visual cell paint UNVERIFIED-VIS under headless drawing.

### 3.5 Classification rationale

Functional product-runtime + PTY evidence for toggle, mode, alt-screen, scrollback retention, search coordinates, and restart **all passed**. Classification is **WORKS_WITH_FRICTION** rather than pure `WORKS` because:

1. Cell-grid **visual paint** is **UNVERIFIED-VIS** under headless drawing (snapshot projection is proven; pixels are not).
2. Visible scrollbar/thumb remains a known product gap (scrollback data path works).
3. Backend remains **Linux-only** in production (this host is Linux; non-Linux not claimed).

Pointer selection paint was not exercised; not claimed.

### 3.6 Machine-readable evidence (captured run)

```json
{
  "schemaVersion": "a3-evidence-1",
  "audit": "v1-v3-product-reality",
  "phase": "A3_TERMINAL",
  "scenarioId": "A1-TR-01",
  "a1RowIds": [
    "A1-TR-01"
  ],
  "startedAtUtc": "2026-07-31T13:23:53.7231794+00:00",
  "finishedAtUtc": "2026-07-31T13:23:58.4367968+00:00",
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repoHead": "8fb5f55ba2264d15970bc0ebabab3588dfc09eda",
    "harness": "a3-terminal-headless",
    "harnessVersion": "a3-tr-0.1"
  },
  "runnerCommand": "dotnet \"/tmp/zaide-a3-tr/out/Release/net10.0/Zaide.Tests.dll\" --scenario A1-TR-01 --profile \"/tmp/zaide-a3-tr-profile-NxZCfnRY\" --evidence \"/tmp/zaide-a3-tr/evidence/A1-TR-01.json\" --repo-head \"8fb5f55ba2264d15970bc0ebabab3588dfc09eda\"",
  "packageVersions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "isolation": {
    "profileRoot": "/tmp/zaide-a3-tr-profile-NxZCfnRY",
    "home": "/tmp/zaide-a3-tr-profile-NxZCfnRY/home",
    "xdgConfigHome": "/tmp/zaide-a3-tr-profile-NxZCfnRY/config",
    "xdgDataHome": "/tmp/zaide-a3-tr-profile-NxZCfnRY/data",
    "xdgStateHome": "/tmp/zaide-a3-tr-profile-NxZCfnRY/state",
    "xdgCacheHome": "/tmp/zaide-a3-tr-profile-NxZCfnRY/cache",
    "resolvedSettingsDir": "/tmp/zaide-a3-tr-profile-NxZCfnRY/config/zaide",
    "preflightOk": true,
    "preflightDetail": "ok"
  },
  "bootstrapResult": "framework_initialized",
  "diResolved": true,
  "mainWindowCreated": true,
  "mainWindowType": "Zaide.App.Shell.MainWindow",
  "commandInputSequence": [
    {
      "i": 1,
      "kind": "key_gesture",
      "name": "Ctrl+Oem3 (view.toggleBottomPanel)",
      "payload": {
        "bottom_before": false,
        "bottom_after": true
      },
      "timestamp_utc": "2026-07-31T13:23:54.9477787+00:00"
    }
  ],
  "observedEvents": [
    {
      "source": "Harness",
      "name": "AppBuilder.constructed",
      "data": {
        "windowing": "Headless",
        "use_headless_entry": true
      },
      "timestamp_utc": "2026-07-31T13:23:53.7257904+00:00"
    },
    {
      "source": "Harness",
      "name": "SetupWithClassicDesktopLifetime.completed",
      "data": {
        "application_type": "Zaide.App.Composition.App",
        "lifetime_type": "Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime"
      },
      "timestamp_utc": "2026-07-31T13:23:54.3004903+00:00"
    },
    {
      "source": "Service",
      "name": "ProductionDi.resolved",
      "data": {
        "main_window_view_model": "Zaide.App.Shell.MainWindowViewModel",
        "command_registry": "Zaide.App.Composition.CommandRegistry",
        "terminal_host": "Zaide.Features.Terminal.Presentation.TerminalHost"
      },
      "timestamp_utc": "2026-07-31T13:23:54.3005145+00:00"
    },
    {
      "source": "Process",
      "name": "desktop.Shutdown",
      "data": {
        "exit_code": 0
      },
      "timestamp_utc": "2026-07-31T13:23:58.4367489+00:00"
    }
  ],
  "controlState": [
    {
      "path": "MainWindowViewModel.IsBottomPanelVisible",
      "property": "IsBottomPanelVisible",
      "value": false,
      "inspectable": true
    },
    {
      "path": "MainWindowViewModel.BottomPanelMode",
      "property": "BottomPanelMode",
      "value": "Terminal",
      "inspectable": true
    }
  ],
  "observedViewModelState": {
    "scenario": "A1-TR-01",
    "IsBottomPanelVisible.initial": false,
    "BottomPanelMode.initial": "Terminal",
    "IsBottomPanelVisible.after_gesture": true,
    "BottomPanelMode.after_open": "Terminal",
    "shell_pid.tr01-initial": 184169,
    "session.state": "Running",
    "session.is_running": true,
    "session.startup_error": null,
    "IsAlternateScreenActive.after_less": true,
    "IsAlternateScreenActive.after_exit": false,
    "scrollback_after_alt": 62,
    "main_restored": true,
    "search.query": "ZAIDE_TR01_SEARCH_MARKER_42",
    "search.match_count": 1,
    "search.active_index": 0,
    "search.active_row": 65,
    "search.active_start_col": 0,
    "search.active_end_col": 27,
    "find_ui.textboxes_found": 1,
    "find_ui.find_buttons": 1,
    "find_ui.caption_candidates": [
      "diff/edit",
      "1/1"
    ],
    "find_ui.query_set": "ZAIDE_TR01_SEARCH_MARKER_42",
    "find_ui.exercised": true,
    "search.after_next_index": 0,
    "after_exit.IsRunning": false,
    "after_exit.State": "Exited",
    "after_exit.exited": true,
    "restart.success": true,
    "restart.post_marker_raw_occurrences": 2,
    "restart.post_marker_exact_line_occurrences": 1,
    "restart.snapshot_change_events": 3,
    "steps": [
      {
        "action": "less -R fixture",
        "alt_entered": true
      }
    ]
  },
  "terminalSnapshots": [
    {
      "phase": "after_marker_main_buffer",
      "is_alternate": false,
      "scrollback_rows": 60,
      "visible_preview": "[cenoda@cenoda zaide]$ printf 'SHELL_PID=%s\\n' \"$$\"\nSHELL_PID=184169\n[cenoda@cenoda zaide]$ for i in $(seq 1 60); do printf 'TR01_LINE_%03\nd filler text\\n' \"$i\"; done; printf '%s\\n' 'ZAIDE_TR01_SEARCH_MARKER_\n42'; printf 'AFTER_MARKER_LINE\\n'\nTR01_LINE_001 filler text\nTR01_LINE_002 filler text\nTR01_LINE_003 filler text\nTR01_LINE_004 filler text\nTR01_LINE_005 filler text\nTR01_LINE_006 filler text\nTR01_LINE_007 filler text\nTR01_LINE_008 filler text\nTR01_LINE_009 filler text\nTR01_LINE_010 filler text\nTR01_LINE_011 filler text\nTR01_LINE_012 filler text\nTR01_LINE_013 filler text\nTR01_LINE_014 filler text\nTR01_LINE_015 filler text\nTR01_LINE_016 filler text\nTR01_LINE_017 filler text\nTR01_LINE_018 filler text\nTR01_LINE_019 filler text\nTR01_LINE_020 filler text\nTR01_LINE_021 filler text\nTR01_LINE_022 filler text\nTR01_LINE_023 filler text\nTR01_LINE_024 filler text\nTR01_LINE_025 filler text\nTR01_LINE_026 filler text\nTR01_LINE_027 filler text\nTR01_LINE_028 filler text\nTR01_LINE_029 filler text\nTR01_LINE_030 filler text\nTR01_LINE_031 filler text\nTR01_LINE_032 filler text\nTR01_LINE_033 filler text\nTR01_LINE_034 filler text\nTR01_LINE_035 filler text\nTR01_LINE_036 filler text\nTR01_LINE_037 filler text\nTR01_LINE_038 filler text\nTR01_LINE_039 filler text\nTR01_LINE_040 filler text\nTR01_LINE_041 filler text\nTR01_LINE_042 filler text\nTR01_LINE_043 filler text\nTR01_LINE_044 filler text\nTR01_LINE_045 filler text\nTR01_LINE_046 filler text\nTR01_LINE_047 filler text\nTR01_LINE_048 filler text\nTR01_LINE_049 filler text\nTR01_LINE_050 filler text\nTR01_LINE_051 filler text\nTR01_LINE_052 filler text\nTR01_LINE_053 filler text\nTR01_LINE_054 filler text\nTR01_LINE_055 filler text\nTR01_LINE_056 filler text\nTR01_LINE_057 filler text\nTR01_LINE_058 filler text\nTR01_LINE_059 filler text\nTR01_LINE_060 filler text\nZAIDE_TR01_SEARCH_MARKER_42\nAFTER_MARKER_LINE\n[cenoda@cenoda zaide]$",
      "contains_marker": true
    },
    {
      "phase": "after_less_attempt",
      "is_alternate": true,
      "alt_entered": true,
      "preview": "LESS_LINE_001 alt-screen fixture content\nLESS_LINE_002 alt-screen fixture content\nLESS_LINE_003 alt-screen fixture content\nLESS_LINE_004 alt-screen fixture content\nLESS_LINE_005 alt-screen fixture content\nLESS_LINE_006 alt-screen fixture content\nLESS_LINE_007 alt-screen fixture content\n/tmp/zaide-a3-tr-profile-NxZCfnRY/home/tr01-less-fixture.txt"
    },
    {
      "phase": "after_alt_exit",
      "is_alternate": false,
      "alt_exited": true,
      "preview": "TR01_LINE_050 filler text\nTR01_LINE_051 filler text\nTR01_LINE_052 filler text\nTR01_LINE_053 filler text\nTR01_LINE_054 filler text\nTR01_LINE_055 filler text\nTR01_LINE_056 filler text\nTR01_LINE_057 filler text\nTR01_LINE_058 filler text\nTR01_LINE_059 filler text\nTR01_LINE_060 filler text\nZAIDE_TR01_SEARCH_MARKER_42\nAFTER_MARKER_LINE\n[cenoda@cenoda zaide]$ less -R '/tmp/zaide-a3-tr-profile-NxZCfnRY/hom\ne/tr01-less-fixture.txt'\n[cenoda@cenoda zaide]$"
    },
    {
      "phase": "search",
      "query": "ZAIDE_TR01_SEARCH_MARKER_42",
      "match_count": 1,
      "active": {
        "row": 65,
        "startCol": 0,
        "endCol": 27
      }
    },
    {
      "phase": "after_restart",
      "is_running": true,
      "state": "Running",
      "post_marker_raw_occurrences": 2,
      "post_marker_exact_line_occurrences": 1,
      "preview": "[cenoda@cenoda zaide]$ printf 'SHELL_PID=%s\\n' \"$$\"\nSHELL_PID=184169\n[cenoda@cenoda zaide]$ for i in $(seq 1 60); do printf 'TR01_LINE_%03\nd filler text\\n' \"$i\"; done; printf '%s\\n' 'ZAIDE_TR01_SEARCH_MARKER_\n42'; printf 'AFTER_MARKER_LINE\\n'\nTR01_LINE_001 filler text\nTR01_LINE_002 filler text\nTR01_LINE_003 filler text\nTR01_LINE_004 filler text\nTR01_LINE_005 filler text\nTR01_LINE_006 filler text\nTR01_LINE_007 filler text\nTR01_LINE_008 filler text\nTR01_LINE_009 filler text\nTR01_LINE_010 filler text\nTR01_LINE_011 filler text\nTR01_LINE_012 filler text\nTR01_LINE_013 filler text\nTR01_LINE_014 filler text\nTR01_LINE_015 filler text\nTR01_LINE_016 filler text\nTR01_LINE_017 filler text\nTR01_LINE_018 filler text\nTR01_LINE_019 filler text\nTR01_LINE_020 filler text\nTR01_LINE_021 filler text\nTR01_LINE_022 filler text\nTR01_LINE_023 filler text\nTR01_LINE_024 filler text\nTR01_LINE_025 filler text\nTR01_LINE_026 filler text\nTR01_LINE_027 filler text\nTR01_LINE_028 filler text\nTR01_LINE_029 filler text\nTR01_LINE_030 filler text\nTR01_LINE_031 filler text\nTR01_LINE_032 filler text\nTR01_LINE_033 filler text\nTR01_LINE_034 filler text\nTR01_LINE_035 filler text\nTR01_LINE_036 filler text\nTR01_LINE_037 filler text\nTR01_LINE_038 filler text\nTR01_LINE_039 filler text\nTR01_LINE_040 filler text\nTR01_LINE_041 filler text\nTR01_LINE_042 filler text\nTR01_LINE_043 filler text\nTR01_LINE_044 filler text\nTR01_LINE_045 filler text\nTR01_LINE_046 filler text\nTR01_LINE_047 filler text\nTR01_LINE_048 filler text\nTR01_LINE_049 filler text\nTR01_LINE_050 filler text\nTR01_LINE_051 filler text\nTR01_LINE_052 filler text\nTR01_LINE_053 filler text\nTR01_LINE_054 filler text\nTR01_LINE_055 filler text\nTR01_LINE_056 filler text\nTR01_LINE_057 filler text\nTR01_LINE_058 filler text\nTR01_LINE_059 filler text\nTR01_LINE_060 filler text\nZAIDE_TR01_SEARCH_MARKER_42\nAFTER_MARKER_LINE\n[cenoda@cenoda zaide]$ less -R '/tmp/zaide-a3-tr-profile-NxZCfnRY/hom\ne/tr01-less-fixture.txt'\n[cenoda@cenoda zaide]$ exit\nexit\n\n[Process exited]\n[cenoda@cenoda zaide]$ printf '%s\\n' 'ZAIDE_TR01_POST_RESTART_OK'\nZAIDE_TR01_POST_RESTART_OK\n[cenoda@cenoda zaide]$"
    }
  ],
  "tabSessionIds": {
    "active_tab_title": "Terminal",
    "tabs_count": "1",
    "shell_pid": "184169"
  },
  "filesystemArtifacts": [
    {
      "path": "/tmp/zaide-a3-tr-profile-NxZCfnRY/state/lesshst",
      "exists": true,
      "unix_mode": "600",
      "under_profile": true
    },
    {
      "path": "/tmp/zaide-a3-tr-profile-NxZCfnRY/home/.bash_history",
      "exists": true,
      "unix_mode": "600",
      "under_profile": true
    },
    {
      "path": "/tmp/zaide-a3-tr-profile-NxZCfnRY/home/tr01-less-fixture.txt",
      "exists": true,
      "unix_mode": "644",
      "under_profile": true
    },
    {
      "path": "/tmp/zaide-a3-tr-profile-NxZCfnRY/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "644",
      "under_profile": true
    },
    {
      "path": "/tmp/zaide-a3-tr-profile-NxZCfnRY/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "644",
      "under_profile": true
    }
  ],
  "shutdownResult": "shutdown_completed",
  "exitCode": 0,
  "assertions": [
    {
      "id": "cold_bottom_hidden",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Bottom panel starts hidden"
    },
    {
      "id": "default_mode_terminal",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Default BottomPanelMode is Terminal"
    },
    {
      "id": "gesture_open_terminal_panel",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Ctrl+Oem3 toggled IsBottomPanelVisible true"
    },
    {
      "id": "pty_session_running",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "State=Running IsRunning=True err="
    },
    {
      "id": "main_buffer_marker",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Deterministic marker present in main-buffer snapshot"
    },
    {
      "id": "scrollback_or_visible_content",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "scrollback_rows=60"
    },
    {
      "id": "alternate_screen_active",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Alternate screen entered via less"
    },
    {
      "id": "alternate_screen_exited",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "IsAlternateScreenActive returned false after quit"
    },
    {
      "id": "main_buffer_restored",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Main buffer active after alt-screen exit"
    },
    {
      "id": "search_matches_found",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "match_count=1"
    },
    {
      "id": "search_navigation_api",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "MoveToNext preserves match set"
    },
    {
      "id": "restart_running",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Session running after RestartCommand"
    },
    {
      "id": "new_prompt_available",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Exact post-restart marker lines=1 (raw substring hits=2 include bash echo)"
    },
    {
      "id": "no_duplicate_marker",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Exactly one standalone post-restart marker line (got 1); raw substring=2 may include echoed command"
    },
    {
      "id": "no_real_user_settings_path",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Settings dir is not real-user ~/.config/zaide"
    },
    {
      "id": "artifacts_under_profile",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Profile artifacts enumerated under disposable root"
    }
  ],
  "evidenceClassesUsed": [
    "product-runtime",
    "pty-service"
  ],
  "limitations": [
    "Production terminal backend is Linux-only (LinuxTerminalServiceFactory); this run is on Linux.",
    "Visible scrollbar/thumb not required for this row; keyboard scroll exists per A2. Visual cell paint UNVERIFIED-VIS under headless drawing."
  ],
  "classification": "WORKS"
}
```

---

## 4. Scenario `A1-TR-02` — two tabs and per-tab lifecycle

### 4.1 Inputs and shell / PTY commands

| Step | Action |
|------|--------|
| 1 | Cold launch; Ctrl+Oem3 open terminal |
| 2 | `NewTabCommand` → two tabs |
| 3 | Tab A: `printf 'ZAIDE_TAB_A_MARKER_777'`; `sleep 999 &`; capture sleep pid |
| 4 | Activate tab B: `printf 'ZAIDE_TAB_B_MARKER_888'` |
| 5 | Compare independent snapshots |
| 6 | Active B only: `printf 'ZAIDE_ONLY_ACTIVE_TAB_B_INPUT'` |
| 7 | `CloseTabCommand` on B; A remains |
| 8 | Sole-tab path: `TerminalTabCloseBehavior` + `HideBottomPanelCommand` |
| 9 | Reopen Ctrl+Oem3; `printf 'ZAIDE_AFTER_REOPEN_MARKER'` |

### 4.2 Expected vs observed

| Check | Expected | Observed |
|-------|----------|----------|
| Two tabs | Count=2 | **2**; distinct session hashes `31c1663` / `120e984` |
| Titles | May be static | Both **`"Terminal"`** (limitation) |
| Isolation A↔B | Markers exclusive | A has A not B; B has B not A |
| Input routing | Active only | `ZAIDE_ONLY_ACTIVE_TAB_B_INPUT` in B only |
| Close B | A survives running | tabs=1, A content + running |
| Last tab | Hide panel, keep host | panel hidden; tabs=1; session same ref; still running |
| Reopen | Session continues | running; prior A marker retained; new reopen marker accepted |

### 4.3 Tab / session identifiers

| id | Value |
|----|-------|
| Tab A title | `Terminal` |
| Tab A session hash | `31c1663` |
| Tab B title | `Terminal` |
| Tab B session hash | `120e984` |
| Tab A sleep child pid | `184249` |
| Tabs after new | 2 |
| Tabs after close B | 1 |

### 4.4 Assertions

| id | result | evidence_class | detail |
|----|--------|----------------|--------|
| `gesture_open_panel` | **pass** | product-runtime | Bottom panel visible after Ctrl+Oem3 |
| `two_tabs_created` | **pass** | product-runtime | Tabs.Count=2 |
| `sessions_independent_instances` | **pass** | product-runtime | Each tab owns a distinct TerminalViewModel |
| `tabB_marker_present` | **pass** | pty-service | Tab B snapshot has MARKER_B |
| `tabB_lacks_markerA` | **pass** | pty-service | Tab B snapshot does not contain MARKER_A (output isolation) |
| `tabA_retains_markerA` | **pass** | pty-service | Tab A snapshot still has MARKER_A while B is active |
| `tabA_lacks_markerB` | **pass** | pty-service | Tab A snapshot does not contain MARKER_B |
| `input_reaches_active_only` | **pass** | pty-service | Input to active tab B does not appear in inactive tab A snapshot |
| `tabA_still_running_before_close_b` | **pass** | pty-service | Tab A still running |
| `one_tab_after_close` | **pass** | product-runtime | Exactly one tab remains |
| `remaining_has_markerA` | **pass** | pty-service | Remaining tab still shows tab A content |
| `remaining_running` | **pass** | pty-service | Remaining tab still running after other closed |
| `last_tab_close_behavior_gate` | **pass** | product-runtime | TerminalTabCloseBehavior says hide panel instead of closing sole tab |
| `panel_hides_on_last_tab_close_path` | **pass** | product-runtime | IsBottomPanelVisible false after HideBottomPanelCommand (LastTabCloseRequested destination) |
| `host_not_destroyed` | **pass** | product-runtime | Tabs remain (1); ActiveSession non-null |
| `session_survives_hide` | **pass** | pty-service | Session running after hide: True |
| `reopen_panel` | **pass** | product-runtime | Bottom panel visible after reopen gesture |
| `reopen_session_alive` | **pass** | pty-service | Session still running after reopen |
| `reopen_accepts_input` | **pass** | pty-service | Reopened surface accepts input and shows new marker |
| `no_real_user_settings_path` | **pass** | product-runtime | Settings dir is not real-user ~/.config/zaide |
| `artifacts_under_profile` | **pass** | product-runtime | Profile artifacts enumerated under disposable root |

### 4.5 Limitations (TR-02)

- Tab titles remain static string "Terminal" for all tabs (no Terminal 1 / Terminal 2 disambiguation).
- Visual tab-strip paint / active highlight UNVERIFIED-VIS under headless drawing.
- Last-tab close exercised production TerminalTabCloseBehavior gate + HideBottomPanelCommand (MainWindow LastTabCloseRequested destination). Headless pointer on × icon not required when command path is equivalent.

### 4.6 Classification rationale

Per-tab PTY isolation, input routing, close/dispose of one tab, last-tab hide-without-destroy, and reopen lifecycle are **product-runtime proven**. Classification is **WORKS_WITH_FRICTION** because tab titles remain the static string `"Terminal"` for every tab (documented 3.9.1 gap / UX friction), and tab-strip visual highlight is **UNVERIFIED-VIS**.

Last-tab close used the production decision gate `TerminalTabCloseBehavior.ShouldHideBottomPanelInsteadOfClosing` plus `HideBottomPanelCommand` (wired destination of `LastTabCloseRequested` on `MainWindow`). Headless × pointer click was not required for equivalent lifecycle proof.

### 4.7 Machine-readable evidence (captured run)

```json
{
  "schemaVersion": "a3-evidence-1",
  "audit": "v1-v3-product-reality",
  "phase": "A3_TERMINAL",
  "scenarioId": "A1-TR-02",
  "a1RowIds": [
    "A1-TR-02"
  ],
  "startedAtUtc": "2026-07-31T13:23:58.4741050+00:00",
  "finishedAtUtc": "2026-07-31T13:24:03.0255079+00:00",
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repoHead": "8fb5f55ba2264d15970bc0ebabab3588dfc09eda",
    "harness": "a3-terminal-headless",
    "harnessVersion": "a3-tr-0.1"
  },
  "runnerCommand": "dotnet \"/tmp/zaide-a3-tr/out/Release/net10.0/Zaide.Tests.dll\" --scenario A1-TR-02 --profile \"/tmp/zaide-a3-tr-profile-W1kzwOKL\" --evidence \"/tmp/zaide-a3-tr/evidence/A1-TR-02.json\" --repo-head \"8fb5f55ba2264d15970bc0ebabab3588dfc09eda\"",
  "packageVersions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "isolation": {
    "profileRoot": "/tmp/zaide-a3-tr-profile-W1kzwOKL",
    "home": "/tmp/zaide-a3-tr-profile-W1kzwOKL/home",
    "xdgConfigHome": "/tmp/zaide-a3-tr-profile-W1kzwOKL/config",
    "xdgDataHome": "/tmp/zaide-a3-tr-profile-W1kzwOKL/data",
    "xdgStateHome": "/tmp/zaide-a3-tr-profile-W1kzwOKL/state",
    "xdgCacheHome": "/tmp/zaide-a3-tr-profile-W1kzwOKL/cache",
    "resolvedSettingsDir": "/tmp/zaide-a3-tr-profile-W1kzwOKL/config/zaide",
    "preflightOk": true,
    "preflightDetail": "ok"
  },
  "bootstrapResult": "framework_initialized",
  "diResolved": true,
  "mainWindowCreated": true,
  "mainWindowType": "Zaide.App.Shell.MainWindow",
  "commandInputSequence": [],
  "observedEvents": [
    {
      "source": "Harness",
      "name": "AppBuilder.constructed",
      "data": {
        "windowing": "Headless",
        "use_headless_entry": true
      },
      "timestamp_utc": "2026-07-31T13:23:58.4766885+00:00"
    },
    {
      "source": "Harness",
      "name": "SetupWithClassicDesktopLifetime.completed",
      "data": {
        "application_type": "Zaide.App.Composition.App",
        "lifetime_type": "Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime"
      },
      "timestamp_utc": "2026-07-31T13:23:59.0542774+00:00"
    },
    {
      "source": "Service",
      "name": "ProductionDi.resolved",
      "data": {
        "main_window_view_model": "Zaide.App.Shell.MainWindowViewModel",
        "command_registry": "Zaide.App.Composition.CommandRegistry",
        "terminal_host": "Zaide.Features.Terminal.Presentation.TerminalHost"
      },
      "timestamp_utc": "2026-07-31T13:23:59.0543017+00:00"
    },
    {
      "source": "Process",
      "name": "desktop.Shutdown",
      "data": {
        "exit_code": 0
      },
      "timestamp_utc": "2026-07-31T13:24:03.0254635+00:00"
    }
  ],
  "controlState": [],
  "observedViewModelState": {
    "scenario": "A1-TR-02",
    "tab_titles": [
      "Terminal",
      "Terminal"
    ],
    "after_close_b.tabs": 1,
    "after_close_b.active_is_a": true,
    "last_tab_should_hide": true,
    "after_last_close_hide.panel_visible": false,
    "after_last_close_hide.tabs": 1,
    "after_last_close_hide.session_running": true,
    "after_last_close_hide.host_session_same": true,
    "reopen.prior_marker_retained": true
  },
  "terminalSnapshots": [
    {
      "phase": "tabA_after_marker_and_sleep",
      "tab": "A",
      "is_active": true,
      "is_running": true,
      "preview": "[cenoda@cenoda zaide]$ printf '%s\\n' 'ZAIDE_TAB_A_MARKER_777'\nZAIDE_TAB_A_MARKER_777\n[cenoda@cenoda zaide]$ sleep 999 &\n[1] 184249\n[cenoda@cenoda zaide]$ printf 'TAB_A_SLEEP_PID=%s\\n' \"$(pgrep -n -f '\nsleep 999' || true)\"\nTAB_A_SLEEP_PID=184249\n[cenoda@cenoda zaide]$",
      "contains_marker_a": true
    },
    {
      "phase": "tabB_after_marker",
      "tab": "B",
      "is_active": true,
      "preview_b": "[cenoda@cenoda zaide]$ printf '%s\\n' 'ZAIDE_TAB_B_MARKER_888'\nZAIDE_TAB_B_MARKER_888\n[cenoda@cenoda zaide]$\n\n\n\n\n",
      "preview_a_independent": "[cenoda@cenoda zaide]$ printf '%s\\n' 'ZAIDE_TAB_A_MARKER_777'\nZAIDE_TAB_A_MARKER_777\n[cenoda@cenoda zaide]$ sleep 999 &\n[1] 184249\n[cenoda@cenoda zaide]$ printf 'TAB_A_SLEEP_PID=%s\\n' \"$(pgrep -n -f '\nsleep 999' || true)\"\nTAB_A_SLEEP_PID=184249\n[cenoda@cenoda zaide]$",
      "b_has_b": true,
      "b_has_a": false,
      "a_has_a": true,
      "a_has_b": false
    },
    {
      "phase": "input_active_only",
      "active": "B",
      "b_has_only": true,
      "a_has_only": false
    },
    {
      "phase": "after_reopen",
      "panel_visible": true,
      "is_running": true,
      "still_has_marker_a": true,
      "has_reopen_marker": true,
      "tabs": 1,
      "preview": "[cenoda@cenoda zaide]$ printf '%s\\n' 'ZAIDE_TAB_A_MARKER_777'\nZAIDE_TAB_A_MARKER_777\n[cenoda@cenoda zaide]$ sleep 999 &\n[1] 184249\n[cenoda@cenoda zaide]$ printf 'TAB_A_SLEEP_PID=%s\\n' \"$(pgrep -n -f '\nsleep 999' || true)\"\nTAB_A_SLEEP_PID=184249\n[cenoda@cenoda zaide]$ printf '%s\\n' 'ZAIDE_AFTER_REOPEN_MARKER'\nZAIDE_AFTER_REOPEN_MARKER\n[cenoda@cenoda zaide]$"
    }
  ],
  "tabSessionIds": {
    "tabA_title": "Terminal",
    "tabA_hash": "31c1663",
    "tabB_title": "Terminal",
    "tabB_hash": "120e984",
    "tabs_count_after_new": "2"
  },
  "filesystemArtifacts": [
    {
      "path": "/tmp/zaide-a3-tr-profile-W1kzwOKL/home/.bash_history",
      "exists": true,
      "unix_mode": "600",
      "under_profile": true
    },
    {
      "path": "/tmp/zaide-a3-tr-profile-W1kzwOKL/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "644",
      "under_profile": true
    },
    {
      "path": "/tmp/zaide-a3-tr-profile-W1kzwOKL/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "644",
      "under_profile": true
    }
  ],
  "shutdownResult": "shutdown_completed",
  "exitCode": 0,
  "assertions": [
    {
      "id": "gesture_open_panel",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Bottom panel visible after Ctrl+Oem3"
    },
    {
      "id": "two_tabs_created",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Tabs.Count=2"
    },
    {
      "id": "sessions_independent_instances",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Each tab owns a distinct TerminalViewModel"
    },
    {
      "id": "tabB_marker_present",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Tab B snapshot has MARKER_B"
    },
    {
      "id": "tabB_lacks_markerA",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Tab B snapshot does not contain MARKER_A (output isolation)"
    },
    {
      "id": "tabA_retains_markerA",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Tab A snapshot still has MARKER_A while B is active"
    },
    {
      "id": "tabA_lacks_markerB",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Tab A snapshot does not contain MARKER_B"
    },
    {
      "id": "input_reaches_active_only",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Input to active tab B does not appear in inactive tab A snapshot"
    },
    {
      "id": "tabA_still_running_before_close_b",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Tab A still running"
    },
    {
      "id": "one_tab_after_close",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Exactly one tab remains"
    },
    {
      "id": "remaining_has_markerA",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Remaining tab still shows tab A content"
    },
    {
      "id": "remaining_running",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Remaining tab still running after other closed"
    },
    {
      "id": "last_tab_close_behavior_gate",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "TerminalTabCloseBehavior says hide panel instead of closing sole tab"
    },
    {
      "id": "panel_hides_on_last_tab_close_path",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "IsBottomPanelVisible false after HideBottomPanelCommand (LastTabCloseRequested destination)"
    },
    {
      "id": "host_not_destroyed",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Tabs remain (1); ActiveSession non-null"
    },
    {
      "id": "session_survives_hide",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Session running after hide: True"
    },
    {
      "id": "reopen_panel",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Bottom panel visible after reopen gesture"
    },
    {
      "id": "reopen_session_alive",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Session still running after reopen"
    },
    {
      "id": "reopen_accepts_input",
      "result": "pass",
      "evidenceClass": "pty-service",
      "detail": "Reopened surface accepts input and shows new marker"
    },
    {
      "id": "no_real_user_settings_path",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Settings dir is not real-user ~/.config/zaide"
    },
    {
      "id": "artifacts_under_profile",
      "result": "pass",
      "evidenceClass": "product-runtime",
      "detail": "Profile artifacts enumerated under disposable root"
    }
  ],
  "evidenceClassesUsed": [
    "product-runtime",
    "pty-service"
  ],
  "limitations": [
    "Tab titles remain static string \"Terminal\" for all tabs (no Terminal 1 / Terminal 2 disambiguation).",
    "Visual tab-strip paint / active highlight UNVERIFIED-VIS under headless drawing.",
    "Last-tab close exercised production TerminalTabCloseBehavior gate + HideBottomPanelCommand (MainWindow LastTabCloseRequested destination). Headless pointer on \u00d7 icon not required when command path is equivalent."
  ],
  "classification": "WORKS_WITH_FRICTION"
}
```

---

## 5. Cross-cutting safety and cleanup

### 5.1 Real-user path verification

| Check | Result |
|-------|--------|
| TR-01 resolved settings dir | `/tmp/zaide-a3-tr-profile-NxZCfnRY/config/zaide` |
| TR-02 resolved settings dir | `/tmp/zaide-a3-tr-profile-W1kzwOKL/config/zaide` |
| Real `~/.config/zaide` used as settings dir | **No** |
| Real-user paths in scenario filesystem artifacts | **None** (artifacts under disposable roots only) |

### 5.2 Cleanup confirmation

| Artifact | Action |
|----------|--------|
| `/tmp/zaide-a3-tr/` runner, obj, out, evidence working copy | **Removed** after preserving this summary |
| Disposable profiles `/tmp/zaide-a3-tr-profile-*` | **Removed** after evidence capture |
| Repository tracked content | **Only** this evidence file under `docs/audits/.../evidence/` |
| Production / tests / packages / audit policy | **Unchanged** |

### 5.3 Out of scope (explicit)

Not executed in this slice: workspace, editor/LSP, build/run/test, debugging, Git, Townhall, agents, permissions, trace, memory, restart-recovery, A4, stabilization, V4 planning.

---

## 6. Blockers and residual notes

| Item | Severity | Notes |
|------|----------|-------|
| Linux-only PTY backend | Limitation | Observed on Linux; non-Linux hosts not claimed |
| Visible scroll affordance | Friction | Scrollback data proven; scrollbar/thumb still absent |
| Static tab titles | Friction | Both tabs titled `"Terminal"` |
| Cell / tab-strip paint | UNVERIFIED-VIS | Headless drawing only |
| Pointer selection | Not claimed | Not exercised this slice |
| A3 overall | Incomplete | Only TR-01/TR-02 of A3 journey matrix |

**No scenario BLOCKED.** Both rows exited process code **0** with all listed assertions **pass**.

---

## 7. Status line

**A3 Terminal smoke (`A1-TR-01`, `A1-TR-02`): complete for this slice.**

**A3 overall: incomplete.**

**A4 / stabilization / V4: not begun.**

**Next bounded A3 slice (suggested):** workspace and project opening (`A1-WO-*`) **or** the next journey the audit plan prioritizes after terminal — not started here.

---

*Recorded 2026-07-31. Out-of-tree Avalonia.Headless 12.0.5 product-runtime runner executed clean-profile terminal smoke under disposable XDG; temporary runner and profiles removed; no production edits.*
