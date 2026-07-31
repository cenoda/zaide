# A3 Clean-Profile Smoke — First Launch and Settings (`A1-FL-01` … `A1-FL-05`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 first authorized execution slice** — first-launch
and settings journey only (`A1-FL-01` through `A1-FL-05`).
**Evidence date:** 2026-07-31
**Repo head at run:** `5bed4be2f1eb4b7f8833b0a10e9e0c369832c76d`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (not harness POC) |
| **A3 slice** | First Launch and Settings (`A1-FL-01`…`A1-FL-05`) |
| **A3 as a whole** | **Not complete** — terminal, workspace, editor, build/run/test, debugging, Git, Townhall, agent, permission, trace, memory, restart-mid-run rows **not executed** |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / pointer / manual desktop | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| `Avalonia.Headless` added to tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` rewritten to “A3 complete” | **No** |
| H1 POC rows reclassified from this run | **No** (H1 remains harness-only) |
| Real user `~/.config/zaide` read/written | **No** |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A2_FIRST_LAUNCH_AND_SETTINGS.md](./A2_FIRST_LAUNCH_AND_SETTINGS.md)

---

## 1. Five-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-FL-01` | **WORKS_WITH_FRICTION** | Cold headless launch constructs production `MainWindow` with multi-column shell (nav \| left Explorer/SC \| Townhall \| editor \| status + bottom host). Bottom panel starts hidden; registered gesture `Ctrl+Oem3` toggles visibility via headless `KeyPress` (`IsBottomPanelVisible` false→true). Command path `view.toggleBottomPanel` also works. **Friction:** historical Phase 0 three-panel / right-agent layout is **not** observed (explicit mismatch recorded). |
| `A1-FL-02` | **WORKS_WITH_FRICTION** | `RequestedThemeVariant`/`ActualThemeVariant` = **Dark**. Navy IDE palette Color resources match `App.axaml` hex (`#066ADB`, `#0A0F19`, …). Fluent theme style root present; multi-style composition present (Semi include path in production XAML; style tree observable under headless). **Friction:** historical “Ayaka Violet” name absent; no user theme switcher. **Pixel/visual-color acceptance:** **UNVERIFIED-VIS** (headless drawing; no frame/pixel claim). |
| `A1-FL-03` | **WORKS_WITH_FRICTION** | User-reachable settings open (status-bar path) → change `CodeFontSize` to 18 → `ApplyAsync` (awaits queued writer) → graceful shutdown. Second process same disposable profile: `LoadResult=Loaded`, font size 18 restored. Primary `settings.json` corrupted with LKG preserved: third process `LoadResult=Corrupt`, in-memory value restored from LKG (18), primary remains corrupt on disk. **Friction:** recovery is silent (no user-visible `LoadResult` / recovery status UI). |
| `A1-FL-04` | **WORKS** | User-reachable credential surface exists (Settings LLM API Key via settings overlay). Synthetic sentinel applied through `SettingsViewModel.ApiKey` + `ApplyAsync`. Sentinel **absent** from ordinary `settings.json`; present in `secrets.json` / `ISecretStore`. Linux mode **`0600`**. `Llm.ApiKeySource` remains `secret-store` (not a plaintext key field). |
| `A1-FL-05` | **WORKS** | User-reachable editor defaults (`TabSize=8`, `ShowWhitespace`/`ShowTabs`/`ShowSpaces`, `CodeFontSize=16`) applied via settings surface. Values persisted to disposable `settings.json`. Disposable file opened via `EditorTabs.OpenFileCommand`; live `TextEditor` properties match (FontSize 16, IndentationSize 8, ShowTabs/ShowSpaces true). Close + reopen preserves live properties. |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

---

## 2. Harness construction (temporary; deleted after evidence capture)

Recreated the H1-proven out-of-tree Avalonia.Headless 12.0.5 runner under `/tmp` (not tracked).

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-fl/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-fl/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Package | `Avalonia.Headless` **12.0.5** only |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — **does not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Observation | ViewModel state, control tree / layout grid, `TextEditor` options, filesystem under disposable profile, headless keyboard |

### 2.1 Isolation protocol

Before any production type construction (process env + runner re-apply):

| Variable | Disposable value |
|----------|------------------|
| `HOME` | `$PROFILE_ROOT/home` |
| `XDG_CONFIG_HOME` | `$PROFILE_ROOT/config` (absolute) |
| `XDG_DATA_HOME` | `$PROFILE_ROOT/data` |
| `XDG_STATE_HOME` | `$PROFILE_ROOT/state` |
| `XDG_CACHE_HOME` | `$PROFILE_ROOT/cache` |

Preflight asserted `SettingsPathResolver.GetSettingsDirectory()` under `$XDG_CONFIG_HOME/zaide` and not the real user config path.
**One OS process per scenario** (FL-03: ordered process A write → B verify → C recover on **one** disposable profile).

### 2.2 Package versions

| Package | Version | Notes |
|---------|---------|--------|
| Avalonia (repo pin) | 12.0.5 | Unchanged `Directory.Packages.props` |
| Avalonia.Headless | 12.0.5 | Out-of-tree runner only |
| Avalonia.Headless.XUnit | n/a | Not required |
| ReactiveUI.Avalonia.ME.DI (via app) | 12.0.3 | Production DI bootstrap |

### 2.3 Runner command pattern

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-fl-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

dotnet "/tmp/zaide-a3-fl/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario A1-FL-0N \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-fl/evidence/A1-FL-0N.json" \
  --repo-head "5bed4be2f1eb4b7f8833b0a10e9e0c369832c76d"
# FL-03 also: --phase write|verify|recover (same PROFILE_ROOT)
```

---

## 3. Scenario evidence

### 3.1 `A1-FL-01` — cold launch and bottom-panel toggle

| Field | Value |
|-------|--------|
| Profile | `/tmp/zaide-a3-fl-profile-N3LHkpmv` |
| Process exit | **0** |
| Classification | **WORKS_WITH_FRICTION** |

**Inputs:** cold headless launch; no prior settings.
**Expected:** shell presents; bottom panel toggle via registered gesture; historical 3-panel mismatch recorded if present.
**Observed:**

- `MainWindow` title `Zaide`, 1280×800, min 960×600, visible.
- Layout columns: `40,260,4,2*,4,1.5*` (nav \| left \| splitter \| townhall \| splitter \| editor) — **not** historical 3-panel/right-agent.
- `IsBottomPanelVisible` initial **false**.
- Headless `KeyPress` **Ctrl+Oem3** → `IsBottomPanelVisible` **true** (registered gesture coverage).
- `ICommandRegistry` `view.toggleBottomPanel` also toggles (command path).
- Historical three-panel/right-agent: **false** (mismatch note recorded).

```json
{
  "schema_version": "a3-evidence-1",
  "scenario_id": "A1-FL-01",
  "scenario_phase": "run",
  "exit_code": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "5bed4be2f1eb4b7f8833b0a10e9e0c369832c76d",
    "harness": "a3-first-launch-settings-headless",
    "harness_version": "a3-fl-0.1"
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-fl-profile-N3LHkpmv",
    "home": "/tmp/zaide-a3-fl-profile-N3LHkpmv/home",
    "xdg_config_home": "/tmp/zaide-a3-fl-profile-N3LHkpmv/config",
    "xdg_data_home": "/tmp/zaide-a3-fl-profile-N3LHkpmv/data",
    "xdg_state_home": "/tmp/zaide-a3-fl-profile-N3LHkpmv/state",
    "xdg_cache_home": "/tmp/zaide-a3-fl-profile-N3LHkpmv/cache",
    "resolved_settings_dir": "/tmp/zaide-a3-fl-profile-N3LHkpmv/config/zaide",
    "preflight_ok": true,
    "preflight_detail": "ok"
  },
  "bootstrap_result": "framework_initialized",
  "observed_view_model_state": {
    "IsBottomPanelVisible.initial": false,
    "IsBottomPanelVisible.after_gesture": true,
    "IsBottomPanelVisible.after_command": false,
    "key_gesture_worked": true,
    "command_worked": true,
    "historical_three_panel_right_agent": false,
    "layout_summary": "40,260,4,2*,4,1.5*"
  },
  "assertions": [
    {
      "id": "cold_launch_mainwindow",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "MainWindow visible under headless"
    },
    {
      "id": "bottom_panel_initially_hidden",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "IsBottomPanelVisible starts false"
    },
    {
      "id": "multi_column_shell",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "nav|left|townhall|editor multi-column shell present"
    },
    {
      "id": "historical_three_panel_mismatch_recorded",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Historical Phase 0 three-panel/right-agent layout NOT observed. Current multi-column shell: Nav + Left(Explorer/SC) + Townhall center + Editor right + bottom multi-panel + status bar. Agent Panel chrome retired; right column is editor."
    },
    {
      "id": "key_gesture_or_command_toggle",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Ctrl+Oem3 gesture toggled bottom panel"
    },
    {
      "id": "key_gesture_coverage",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Registered gesture delivered via headless KeyPress"
    },
    {
      "id": "command_path_coverage",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "ICommandRegistry view.toggleBottomPanel works"
    }
  ],
  "shutdown_result": "shutdown_completed",
  "limitations": [
    "Headless drawing only; visual layout paint proportions not asserted.",
    "Registered key gesture delivered successfully."
  ],
  "control_state_selected": [
    {
      "path": "MainWindow.Title",
      "property": "Title",
      "value": "Zaide",
      "inspectable": true
    },
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
    },
    {
      "path": "Layout.ColumnCount",
      "property": "ColumnCount",
      "value": 6,
      "inspectable": true
    },
    {
      "path": "Layout.RowCount",
      "property": "RowCount",
      "value": 4,
      "inspectable": true
    },
    {
      "path": "Layout.ColumnSummary",
      "property": "ColumnSummary",
      "value": "40,260,4,2*,4,1.5*",
      "inspectable": true
    },
    {
      "path": "Layout.ChildTypeSummary",
      "property": "ChildTypeSummary",
      "value": "NavBar,FileTreeView,SourceControlPanel,GridSplitter,TownhallView,Grid,Border,StatusBar,CommandPaletteOverlay",
      "inspectable": true
    },
    {
      "path": "Layout.HasNavBar",
      "property": "HasNavBar",
      "value": true,
      "inspectable": true
    },
    {
      "path": "Layout.HasFileTree",
      "property": "HasFileTree",
      "value": true,
      "inspectable": true
    },
    {
      "path": "Layout.HasTownhall",
      "property": "HasTownhall",
      "value": true,
      "inspectable": true
    },
    {
      "path": "Layout.HasEditorColumn",
      "property": "HasEditorColumn",
      "value": true,
      "inspectable": true
    },
    {
      "path": "Layout.HasStatusBar",
      "property": "HasStatusBar",
      "value": true,
      "inspectable": true
    },
    {
      "path": "Layout.HasBottomPanelHost",
      "property": "HasBottomPanelHost",
      "value": true,
      "inspectable": true
    },
    {
      "path": "Layout.HistoricalThreePanelRightAgent",
      "property": "HistoricalThreePanelRightAgent",
      "value": false,
      "inspectable": true
    },
    {
      "path": "Layout.HistoricalMismatchNote",
      "property": "HistoricalMismatchNote",
      "value": "Historical Phase 0 three-panel/right-agent layout NOT observed. Current multi-column shell: Nav + Left(Explorer/SC) + Townhall center + Editor right + bottom multi-panel + status bar. Agent Panel chrome retired; right column is editor.",
      "inspectable": true
    },
    {
      "path": "Gesture.CtrlOem3.Worked",
      "property": "Worked",
      "value": true,
      "inspectable": true
    },
    {
      "path": "BottomPanel.IsBottomPanelVisible",
      "property": "IsBottomPanelVisible",
      "value": false,
      "inspectable": true
    },
    {
      "path": "BottomPanel.SplitterRowHeight",
      "property": "SplitterRowHeight",
      "value": "0",
      "inspectable": true
    },
    {
      "path": "BottomPanel.PanelRowHeight",
      "property": "PanelRowHeight",
      "value": "0",
      "inspectable": true
    },
    {
      "path": "BottomPanel.PanelBorderIsVisible",
      "property": "PanelBorderIsVisible",
      "value": false,
      "inspectable": true
    }
  ],
  "command_input_sequence": [
    {
      "i": 1,
      "kind": "key_gesture",
      "name": "Ctrl+Oem3",
      "payload": {
        "bottom_before": false,
        "bottom_after": true,
        "worked": true
      },
      "timestamp_utc": "2026-07-31T12:50:01.6477168+00:00"
    },
    {
      "i": 2,
      "kind": "command",
      "name": "view.toggleBottomPanel",
      "payload": {
        "bottom_before": true,
        "bottom_after": false,
        "worked": true
      },
      "timestamp_utc": "2026-07-31T12:50:01.6941926+00:00"
    }
  ],
  "filesystem_artifacts": [
    {
      "path": "/tmp/zaide-a3-fl-profile-N3LHkpmv/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-N3LHkpmv/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-N3LHkpmv/config/zaide/settings.json",
      "exists": false,
      "notes": "expected_product_path"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-N3LHkpmv/config/zaide/settings.json.lastknowngood",
      "exists": false,
      "notes": "expected_product_path"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-N3LHkpmv/config/zaide/secrets.json",
      "exists": false,
      "notes": "expected_product_path"
    }
  ]
}
```

---

### 3.2 `A1-FL-02` — theme / palette composition (non-pixel)

| Field | Value |
|-------|--------|
| Profile | `/tmp/zaide-a3-fl-profile-MfAF1mmV` |
| Process exit | **0** |
| Classification | **WORKS_WITH_FRICTION** (+ pixel claims **UNVERIFIED-VIS**) |

**Inputs:** cold headless launch.
**Expected:** Dark / Semi+Fluent composition and palette resources; no pixel acceptance.
**Observed:**

- `RequestedThemeVariant` = `Dark`, `ActualThemeVariant` = `Dark`.
- Navy palette Color keys all match expected hex (PrimaryAccent `#066ADB`, SurfaceBase `#0A0F19`, …).
- Application styles present (Fluent theme type among style roots).
- Pixel/frame visual-color acceptance **not claimed** → **UNVERIFIED-VIS**.
- “Ayaka Violet” not a live resource key.

```json
{
  "schema_version": "a3-evidence-1",
  "scenario_id": "A1-FL-02",
  "scenario_phase": "run",
  "exit_code": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "5bed4be2f1eb4b7f8833b0a10e9e0c369832c76d",
    "harness": "a3-first-launch-settings-headless",
    "harness_version": "a3-fl-0.1"
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-fl-profile-MfAF1mmV",
    "home": "/tmp/zaide-a3-fl-profile-MfAF1mmV/home",
    "xdg_config_home": "/tmp/zaide-a3-fl-profile-MfAF1mmV/config",
    "xdg_data_home": "/tmp/zaide-a3-fl-profile-MfAF1mmV/data",
    "xdg_state_home": "/tmp/zaide-a3-fl-profile-MfAF1mmV/state",
    "xdg_cache_home": "/tmp/zaide-a3-fl-profile-MfAF1mmV/cache",
    "resolved_settings_dir": "/tmp/zaide-a3-fl-profile-MfAF1mmV/config/zaide",
    "preflight_ok": true,
    "preflight_detail": "ok"
  },
  "bootstrap_result": "framework_initialized",
  "observed_view_model_state": {
    "RequestedThemeVariant": "Dark",
    "ActualThemeVariant": "Dark",
    "palette_resource_matches": true,
    "resource_hits": {
      "PrimaryAccentBrushColor": {
        "expected": "#066ADB",
        "observed": "#066ADB",
        "match": true
      },
      "SecondaryAccentBrushColor": {
        "expected": "#3ED3E4",
        "observed": "#3ED3E4",
        "match": true
      },
      "SurfaceBaseBrushColor": {
        "expected": "#0A0F19",
        "observed": "#0A0F19",
        "match": true
      },
      "SurfacePanelBrushColor": {
        "expected": "#1A2540",
        "observed": "#1A2540",
        "match": true
      },
      "TextPrimaryBrushColor": {
        "expected": "#E3E4F4",
        "observed": "#E3E4F4",
        "match": true
      },
      "TextSecondaryBrushColor": {
        "expected": "#8B95A5",
        "observed": "#8B95A5",
        "match": true
      },
      "PanelDeepBrushColor": {
        "expected": "#101A2A",
        "observed": "#101A2A",
        "match": true
      },
      "SurfaceRaisedBrushColor": {
        "expected": "#243352",
        "observed": "#243352",
        "match": true
      },
      "WarningBrushColor": {
        "expected": "#FCBB47",
        "observed": "#FCBB47",
        "match": true
      },
      "SuccessBrushColor": {
        "expected": "#28A745",
        "observed": "#28A745",
        "match": true
      }
    },
    "pixel_claims": "UNVERIFIED-VIS"
  },
  "assertions": [
    {
      "id": "requested_theme_dark",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "RequestedThemeVariant=Dark"
    },
    {
      "id": "navy_palette_resources",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Navy IDE palette Color keys match App.axaml hex values"
    },
    {
      "id": "fluent_theme_composed",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Fluent/theme styles present in Application.Styles"
    },
    {
      "id": "semi_or_styleinclude_present",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Semi.Avalonia style include or multiple style roots present (headless property; not pixel proof)"
    },
    {
      "id": "pixel_color_acceptance",
      "result": "skip",
      "evidence_class": "product-runtime",
      "detail": "UNVERIFIED-VIS: no pixel/frame visual-color acceptance claimed under headless drawing"
    },
    {
      "id": "ayaka_violet_name_absent",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Historical 'Ayaka Violet' is not a resource key; live palette is Navy IDE"
    },
    {
      "id": "user_theme_switcher",
      "result": "skip",
      "evidence_class": "product-runtime",
      "detail": "No user-reachable theme switcher in settings (composition-only theme)"
    }
  ],
  "shutdown_result": "shutdown_completed",
  "limitations": [
    "Pixel / visual-color / Semi chrome paint not verified (UNVERIFIED-VIS).",
    "Headless UseHeadlessDrawing=true; CaptureRenderedFrame not used as acceptance."
  ],
  "control_state_selected": [
    {
      "path": "Application.RequestedThemeVariant",
      "property": "RequestedThemeVariant",
      "value": "Dark",
      "inspectable": true
    },
    {
      "path": "Application.ActualThemeVariant",
      "property": "ActualThemeVariant",
      "value": "Dark",
      "inspectable": true
    },
    {
      "path": "Resources.PrimaryAccentBrushColor",
      "property": "PrimaryAccentBrushColor",
      "value": "#066ADB",
      "inspectable": true
    },
    {
      "path": "Resources.SecondaryAccentBrushColor",
      "property": "SecondaryAccentBrushColor",
      "value": "#3ED3E4",
      "inspectable": true
    },
    {
      "path": "Resources.SurfaceBaseBrushColor",
      "property": "SurfaceBaseBrushColor",
      "value": "#0A0F19",
      "inspectable": true
    },
    {
      "path": "Resources.SurfacePanelBrushColor",
      "property": "SurfacePanelBrushColor",
      "value": "#1A2540",
      "inspectable": true
    },
    {
      "path": "Resources.TextPrimaryBrushColor",
      "property": "TextPrimaryBrushColor",
      "value": "#E3E4F4",
      "inspectable": true
    },
    {
      "path": "Resources.TextSecondaryBrushColor",
      "property": "TextSecondaryBrushColor",
      "value": "#8B95A5",
      "inspectable": true
    },
    {
      "path": "Resources.PanelDeepBrushColor",
      "property": "PanelDeepBrushColor",
      "value": "#101A2A",
      "inspectable": true
    },
    {
      "path": "Resources.SurfaceRaisedBrushColor",
      "property": "SurfaceRaisedBrushColor",
      "value": "#243352",
      "inspectable": true
    },
    {
      "path": "Resources.WarningBrushColor",
      "property": "WarningBrushColor",
      "value": "#FCBB47",
      "inspectable": true
    },
    {
      "path": "Resources.SuccessBrushColor",
      "property": "SuccessBrushColor",
      "value": "#28A745",
      "inspectable": true
    }
  ],
  "command_input_sequence": null,
  "filesystem_artifacts": [
    {
      "path": "/tmp/zaide-a3-fl-profile-MfAF1mmV/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-MfAF1mmV/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-MfAF1mmV/config/zaide/settings.json",
      "exists": false,
      "notes": "expected_product_path"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-MfAF1mmV/config/zaide/settings.json.lastknowngood",
      "exists": false,
      "notes": "expected_product_path"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-MfAF1mmV/config/zaide/secrets.json",
      "exists": false,
      "notes": "expected_product_path"
    }
  ]
}
```

---

### 3.3 `A1-FL-03` — settings persistence and corrupt/LKG recovery

| Field | Value |
|-------|--------|
| Profile (shared A→B→C) | `/tmp/zaide-a3-fl-profile-NfMqJ9gm` |
| Process A exit (write) | **0** |
| Process B exit (verify) | **0** |
| Process C exit (recover) | **0** |
| Classification | **WORKS_WITH_FRICTION** |

**Inputs:** disposable profile; change `Editor.CodeFontSize` → **18** via settings surface; graceful exit after writer completion; restart; corrupt primary JSON while keeping LKG; restart again.
**Expected:** persistence across process boundary; LKG recovery on corrupt primary.
**Observed:**

| Phase | LoadResult | CodeFontSize | Disk |
|-------|------------|--------------|------|
| write (A) | Missing→apply | 18 after apply | `settings.json` + `.lastknowngood` written |
| verify (B) | **Loaded** | **18** | restored from primary |
| recover (C) | **Corrupt** | **18** (from LKG) | primary still invalid JSON; LKG preserved |

Settings opened via status-bar `OpenSettingsCommand` path (user-reachable).
No user-visible recovery status UI (silent) — friction.

#### Process A (write)

```json
{
  "schema_version": "a3-evidence-1",
  "scenario_id": "A1-FL-03",
  "scenario_phase": "write",
  "exit_code": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "5bed4be2f1eb4b7f8833b0a10e9e0c369832c76d",
    "harness": "a3-first-launch-settings-headless",
    "harness_version": "a3-fl-0.1"
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-fl-profile-NfMqJ9gm",
    "home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/home",
    "xdg_config_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config",
    "xdg_data_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/data",
    "xdg_state_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/state",
    "xdg_cache_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/cache",
    "resolved_settings_dir": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide",
    "preflight_ok": true,
    "preflight_detail": "ok"
  },
  "bootstrap_result": "framework_initialized",
  "observed_view_model_state": {
    "LoadResult": "Missing",
    "CodeFontSize": 18,
    "applyOk": true
  },
  "assertions": [
    {
      "id": "settings_surface_opened",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Settings overlay open via status-bar command"
    },
    {
      "id": "apply_succeeded",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "SettingsViewModel.ApplyAsync returned true"
    },
    {
      "id": "in_memory_font_size",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Current.Editor.CodeFontSize=18"
    },
    {
      "id": "settings_json_written",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/settings.json"
    },
    {
      "id": "lkg_written",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/settings.json.lastknowngood"
    },
    {
      "id": "settings_json_contains_font_size",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "settings.json includes CodeFontSize value"
    }
  ],
  "shutdown_result": "shutdown_completed",
  "limitations": null,
  "control_state_selected": [
    {
      "path": "Settings.LoadResult",
      "property": "LoadResult",
      "value": "Missing",
      "inspectable": true
    },
    {
      "path": "Settings.CodeFontSize.before",
      "property": "before",
      "value": 14,
      "inspectable": true
    }
  ],
  "command_input_sequence": [
    {
      "i": 1,
      "kind": "command",
      "name": "OpenSettingsCommand",
      "payload": {
        "openOk": true
      },
      "timestamp_utc": "2026-07-31T12:57:09.4756936+00:00"
    },
    {
      "i": 2,
      "kind": "viewmodel",
      "name": "SetCodeFontSize",
      "payload": {
        "value": 18
      },
      "timestamp_utc": "2026-07-31T12:57:09.4769048+00:00"
    },
    {
      "i": 3,
      "kind": "viewmodel",
      "name": "ApplyAsync",
      "payload": {
        "applyOk": true
      },
      "timestamp_utc": "2026-07-31T12:57:09.5031148+00:00"
    }
  ],
  "filesystem_artifacts": [
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/settings.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/settings.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/secrets.json",
      "exists": false,
      "notes": "expected_product_path"
    }
  ]
}
```

#### Process B (verify)

```json
{
  "schema_version": "a3-evidence-1",
  "scenario_id": "A1-FL-03",
  "scenario_phase": "verify",
  "exit_code": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "5bed4be2f1eb4b7f8833b0a10e9e0c369832c76d",
    "harness": "a3-first-launch-settings-headless",
    "harness_version": "a3-fl-0.1"
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-fl-profile-NfMqJ9gm",
    "home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/home",
    "xdg_config_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config",
    "xdg_data_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/data",
    "xdg_state_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/state",
    "xdg_cache_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/cache",
    "resolved_settings_dir": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide",
    "preflight_ok": true,
    "preflight_detail": "ok"
  },
  "bootstrap_result": "framework_initialized",
  "observed_view_model_state": {
    "LoadResult": "Loaded",
    "CodeFontSize": 18
  },
  "assertions": [
    {
      "id": "load_result_loaded",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "LoadResult=Loaded"
    },
    {
      "id": "font_size_restored",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "CodeFontSize=18 expected 18"
    }
  ],
  "shutdown_result": "shutdown_completed",
  "limitations": null,
  "control_state_selected": [
    {
      "path": "Settings.LoadResult",
      "property": "LoadResult",
      "value": "Loaded",
      "inspectable": true
    },
    {
      "path": "Settings.CodeFontSize",
      "property": "CodeFontSize",
      "value": 18,
      "inspectable": true
    },
    {
      "path": "Settings.SchemaVersion",
      "property": "SchemaVersion",
      "value": 3,
      "inspectable": true
    }
  ],
  "command_input_sequence": null,
  "filesystem_artifacts": [
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/settings.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/settings.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/secrets.json",
      "exists": false,
      "notes": "expected_product_path"
    }
  ]
}
```

#### Process C (recover)

```json
{
  "schema_version": "a3-evidence-1",
  "scenario_id": "A1-FL-03",
  "scenario_phase": "recover",
  "exit_code": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "5bed4be2f1eb4b7f8833b0a10e9e0c369832c76d",
    "harness": "a3-first-launch-settings-headless",
    "harness_version": "a3-fl-0.1"
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-fl-profile-NfMqJ9gm",
    "home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/home",
    "xdg_config_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config",
    "xdg_data_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/data",
    "xdg_state_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/state",
    "xdg_cache_home": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/cache",
    "resolved_settings_dir": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide",
    "preflight_ok": true,
    "preflight_detail": "ok"
  },
  "bootstrap_result": "framework_initialized",
  "observed_view_model_state": {
    "LoadResult": "Corrupt",
    "CodeFontSize": 18,
    "recoveredFromLkg": true,
    "silent_recovery_no_ui": true
  },
  "assertions": [
    {
      "id": "load_result_corrupt",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "LoadResult=Corrupt (expected Corrupt when primary unparseable)"
    },
    {
      "id": "recovered_font_size_from_lkg",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "CodeFontSize=18 (LKG had 18)"
    },
    {
      "id": "primary_still_corrupt_on_disk",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Primary file not overwritten on corrupt load"
    },
    {
      "id": "lkg_still_present",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/settings.json.lastknowngood"
    },
    {
      "id": "user_visible_recovery_status",
      "result": "skip",
      "evidence_class": "product-runtime",
      "detail": "No production UI projects LoadResult (silent recovery) \u2014 friction noted"
    }
  ],
  "shutdown_result": "shutdown_completed",
  "limitations": null,
  "control_state_selected": [
    {
      "path": "Settings.LoadResult",
      "property": "LoadResult",
      "value": "Corrupt",
      "inspectable": true
    },
    {
      "path": "Settings.CodeFontSize",
      "property": "CodeFontSize",
      "value": 18,
      "inspectable": true
    },
    {
      "path": "Settings.IsDefaults",
      "property": "IsDefaults",
      "value": false,
      "inspectable": true
    }
  ],
  "command_input_sequence": null,
  "filesystem_artifacts": [
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/settings.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/settings.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-NfMqJ9gm/config/zaide/secrets.json",
      "exists": false,
      "notes": "expected_product_path"
    }
  ]
}
```

---

### 3.4 `A1-FL-04` — secret boundary

| Field | Value |
|-------|--------|
| Profile | `/tmp/zaide-a3-fl-profile-SxmHwRtk` |
| Process exit | **0** |
| Classification | **WORKS** |

**Inputs:** synthetic sentinel only (`ZAIDE_A3_SYNTHETIC_SENTINEL_KEY_NOT_REAL_7f3a9c`); never real credentials.
**Expected:** user-reachable credential surface or UNDISCOVERABLE; sentinel absent from `settings.json`; secret file path + restricted mode.
**Observed:**

- Credential surface **user-reachable** (settings overlay + `SettingsViewModel.ApiKey`).
- Sentinel **absent** from ordinary `settings.json`.
- Sentinel present in `secrets.json` / `ISecretStore.Get("llm.apiKey")`.
- `secrets.json` path: `$XDG_CONFIG_HOME/zaide/secrets.json`.
- Linux unix mode: **`0600`**.
- `Llm.ApiKeySource` = `secret-store` (no plaintext key field in settings model).

```json
{
  "schema_version": "a3-evidence-1",
  "scenario_id": "A1-FL-04",
  "scenario_phase": "run",
  "exit_code": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "5bed4be2f1eb4b7f8833b0a10e9e0c369832c76d",
    "harness": "a3-first-launch-settings-headless",
    "harness_version": "a3-fl-0.1"
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-fl-profile-SxmHwRtk",
    "home": "/tmp/zaide-a3-fl-profile-SxmHwRtk/home",
    "xdg_config_home": "/tmp/zaide-a3-fl-profile-SxmHwRtk/config",
    "xdg_data_home": "/tmp/zaide-a3-fl-profile-SxmHwRtk/data",
    "xdg_state_home": "/tmp/zaide-a3-fl-profile-SxmHwRtk/state",
    "xdg_cache_home": "/tmp/zaide-a3-fl-profile-SxmHwRtk/cache",
    "resolved_settings_dir": "/tmp/zaide-a3-fl-profile-SxmHwRtk/config/zaide",
    "preflight_ok": true,
    "preflight_detail": "ok"
  },
  "bootstrap_result": "framework_initialized",
  "observed_view_model_state": {
    "settings_open": false,
    "applyOk": true,
    "sentinel_in_settings_json": false,
    "secrets_exists": true,
    "secrets_unix_mode": "0600",
    "secret_store_matches": true
  },
  "assertions": [
    {
      "id": "user_reachable_credential_surface",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Settings opened via status-bar OpenSettingsCommand; ApiKey field on SettingsViewModel"
    },
    {
      "id": "apply_ok",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "ApplyAsync after setting ApiKey"
    },
    {
      "id": "sentinel_absent_from_settings_json",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "sentinel absent from ordinary settings.json"
    },
    {
      "id": "secrets_file_exists",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "/tmp/zaide-a3-fl-profile-SxmHwRtk/config/zaide/secrets.json"
    },
    {
      "id": "sentinel_present_in_secrets_store",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Synthetic key present in secrets store / secrets.json"
    },
    {
      "id": "secrets_file_mode_0600",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "unix_mode=0600"
    },
    {
      "id": "api_key_source_not_plaintext_field",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "ApiKeySource=secret-store"
    },
    {
      "id": "settings_json_no_api_key_value_field",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "No synthetic credential value in settings.json"
    }
  ],
  "shutdown_result": "shutdown_completed",
  "limitations": [
    "Synthetic sentinel only; no real credentials used.",
    "Sentinel value not embedded in evidence beyond presence flags."
  ],
  "control_state_selected": [
    {
      "path": "credential_surface.settings_open",
      "property": "settings_open",
      "value": true,
      "inspectable": true
    },
    {
      "path": "credential_surface.SettingsViewModel.ApiKey_property",
      "property": "ApiKey_property",
      "value": true,
      "inspectable": true
    },
    {
      "path": "credential_surface.user_reachable",
      "property": "user_reachable",
      "value": true,
      "inspectable": true
    },
    {
      "path": "secrets.json.path",
      "property": "path",
      "value": "/tmp/zaide-a3-fl-profile-SxmHwRtk/config/zaide/secrets.json",
      "inspectable": true
    },
    {
      "path": "secrets.json.exists",
      "property": "exists",
      "value": true,
      "inspectable": true
    },
    {
      "path": "sentinel_in_settings.json",
      "property": "json",
      "value": false,
      "inspectable": true
    },
    {
      "path": "sentinel_in_secrets.json",
      "property": "json",
      "value": true,
      "inspectable": true
    },
    {
      "path": "secrets.json.unix_mode",
      "property": "unix_mode",
      "value": "0600",
      "inspectable": true
    },
    {
      "path": "secrets.json.contains_key_name",
      "property": "contains_key_name",
      "value": true,
      "inspectable": true
    },
    {
      "path": "ISecretStore.Get.matches_sentinel",
      "property": "matches_sentinel",
      "value": true,
      "inspectable": true
    }
  ],
  "command_input_sequence": [
    {
      "i": 1,
      "kind": "viewmodel",
      "name": "ApiKey+ApplyAsync",
      "payload": {
        "applyOk": true
      },
      "timestamp_utc": "2026-07-31T12:57:21.4560425+00:00"
    }
  ],
  "filesystem_artifacts": [
    {
      "path": "/tmp/zaide-a3-fl-profile-SxmHwRtk/config/zaide/secrets.json",
      "exists": true,
      "unix_mode": "0600",
      "notes": "secrets.json (content redacted)"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-SxmHwRtk/config/zaide/settings.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-SxmHwRtk/config/zaide/settings.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-SxmHwRtk/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-SxmHwRtk/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json"
    }
  ]
}
```

---

### 3.5 `A1-FL-05` — editor defaults configure, persist, live-apply

| Field | Value |
|-------|--------|
| Profile | `/tmp/zaide-a3-fl-profile-XFo3mQ1p` |
| Disposable file | `$PROFILE/workspaces/fl05/sample.txt` |
| Process exit | **0** |
| Classification | **WORKS** |

**Inputs:** change user-reachable editor defaults; open disposable document; reopen.
**Expected:** persisted settings; live editor properties.
**Observed:**

| Setting | Applied | Live TextEditor | After reopen |
|---------|---------|----------------|--------------|
| CodeFontSize 16 | yes | FontSize=16 | FontSize=16 |
| TabSize 8 | yes | IndentationSize=8 | IndentationSize=8 |
| ShowWhitespace + tabs/spaces | yes | ShowTabs/ShowSpaces true | preserved |

`settings.json` contains `tabSize` 8. Open via production `EditorTabs.OpenFileCommand`.

```json
{
  "schema_version": "a3-evidence-1",
  "scenario_id": "A1-FL-05",
  "scenario_phase": "run",
  "exit_code": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "5bed4be2f1eb4b7f8833b0a10e9e0c369832c76d",
    "harness": "a3-first-launch-settings-headless",
    "harness_version": "a3-fl-0.1"
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-fl-profile-XFo3mQ1p",
    "home": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/home",
    "xdg_config_home": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/config",
    "xdg_data_home": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/data",
    "xdg_state_home": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/state",
    "xdg_cache_home": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/cache",
    "resolved_settings_dir": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/config/zaide",
    "preflight_ok": true,
    "preflight_detail": "ok"
  },
  "bootstrap_result": "framework_initialized",
  "observed_view_model_state": {
    "TabSize": 8,
    "ShowWhitespace": true,
    "CodeFontSize": 16,
    "liveApplied": true,
    "opened": true,
    "reopened": true
  },
  "assertions": [
    {
      "id": "settings_opened",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Settings surface reachable"
    },
    {
      "id": "apply_ok",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "ApplyAsync"
    },
    {
      "id": "settings_persisted_in_memory",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "ISettingsService.Current reflects editor defaults"
    },
    {
      "id": "settings_json_persisted",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "settings.json contains tabSize 8"
    },
    {
      "id": "document_opened",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "opened=True active=/tmp/zaide-a3-fl-profile-XFo3mQ1p/workspaces/fl05/sample.txt err="
    },
    {
      "id": "editor_font_size_live",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "FontSize=16 expected 16"
    },
    {
      "id": "editor_tab_size_live",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "IndentationSize=8 expected 8"
    },
    {
      "id": "editor_whitespace_live",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "ShowTabs=True ShowSpaces=True"
    },
    {
      "id": "live_applied_overall",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "EditorView TextEditor options match applied settings"
    },
    {
      "id": "reopen_preserves_settings",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "reopened=True FontSize=16 Indent=8"
    }
  ],
  "shutdown_result": "shutdown_completed",
  "limitations": null,
  "control_state_selected": [
    {
      "path": "EditorView.FontSize",
      "property": "FontSize",
      "value": 16,
      "inspectable": true
    },
    {
      "path": "EditorView.IndentationSize",
      "property": "IndentationSize",
      "value": 8,
      "inspectable": true
    },
    {
      "path": "EditorView.ConvertTabsToSpaces",
      "property": "ConvertTabsToSpaces",
      "value": true,
      "inspectable": true
    },
    {
      "path": "EditorView.ShowTabs",
      "property": "ShowTabs",
      "value": true,
      "inspectable": true
    },
    {
      "path": "EditorView.ShowSpaces",
      "property": "ShowSpaces",
      "value": true,
      "inspectable": true
    },
    {
      "path": "EditorView.IsVisible",
      "property": "IsVisible",
      "value": true,
      "inspectable": true
    },
    {
      "path": "EditorView.Found",
      "property": "Found",
      "value": true,
      "inspectable": true
    },
    {
      "path": "EditorView.reopen.FontSize",
      "property": "FontSize",
      "value": 16,
      "inspectable": true
    },
    {
      "path": "EditorView.reopen.IndentationSize",
      "property": "IndentationSize",
      "value": 8,
      "inspectable": true
    }
  ],
  "command_input_sequence": [
    {
      "i": 1,
      "kind": "viewmodel",
      "name": "SetTabSize/ShowWhitespace/CodeFontSize",
      "payload": {
        "newTab": 8,
        "newFont": 16
      },
      "timestamp_utc": "2026-07-31T12:57:25.4335708+00:00"
    },
    {
      "i": 2,
      "kind": "command",
      "name": "EditorTabs.OpenFileCommand",
      "payload": {
        "sampleFile": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/workspaces/fl05/sample.txt",
        "opened": true
      },
      "timestamp_utc": "2026-07-31T12:57:25.6076779+00:00"
    }
  ],
  "filesystem_artifacts": [
    {
      "path": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/workspaces/fl05/sample.txt",
      "exists": true,
      "unix_mode": "0644",
      "notes": "workspaces/fl05/sample.txt"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/config/zaide/settings.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/config/zaide/settings.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-profile-XFo3mQ1p/config/zaide/secrets.json",
      "exists": false,
      "notes": "expected_product_path"
    }
  ]
}
```

---

## 4. Path isolation and real-user safety

| Check | Result |
|-------|--------|
| All resolved settings dirs under disposable `$PROFILE_ROOT/config/zaide` | **Yes** |
| Real `/home/cenoda/.config/zaide` in any evidence JSON | **No** |
| Other `/home/cenoda/*` product paths (excluding repo source reference in project path of runner only) | **None in evidence JSON** |
| Synthetic credentials only | **Yes** |
| Disposable workspaces only | **Yes** (`…/workspaces/fl05/…`) |

---

## 5. Blockers and limitations

| Item | Severity | Notes |
|------|----------|-------|
| Pixel / visual Semi chrome appearance | Limitation | **UNVERIFIED-VIS** for FL-02 pixel claims; does not block resource-composition proof |
| Silent settings recovery UI | Friction | FL-03 recovers correctly but `LoadResult` not user-projected |
| Historical layout wording | Friction | FL-01 product shell ≠ Phase 0 three-panel/right-agent |
| Theme name / switcher | Friction | Navy live palette; no user theme switcher |
| A3 remainder | Scope | Not a blocker for this slice; other journeys not executed |
| Harness temporary | Process | Out-of-tree runner deleted after capture; not a product defect |

**No BLOCKED rows** in this slice. No harness failure preventing classification.

---

## 6. Cleanup

| Artifact | Action |
|----------|--------|
| `/tmp/zaide-a3-fl/` runner, obj, out, evidence working copy | Removed after preserving this summary |
| Disposable profiles `/tmp/zaide-a3-fl-profile-*` | Removed after evidence capture |
| Production / tests / packages | Unchanged |
| Tracked deliverable | This evidence file only (plus prior untracked H0/H1 evidence in same commit) |

---

## 7. Explicit non-claims

1. **A3 is not complete.** Only `A1-FL-01`…`A1-FL-05` were executed.
2. **H1 POC does not classify any A1 row** — this document is the first A3 classification source for FL rows.
3. **No A4, stabilization, or V4 planning.**
4. **No production code, tracked tests, or package pins changed.**
5. **No xdtools, screenshots, OS desktop automation, or manual pointer interaction.**

---

## 8. Status line

**A3 First Launch and Settings smoke (`A1-FL-01`…`A1-FL-05`): complete for this authorized slice.**

**A3 Clean-profile smoke (full matrix): not complete.**

**A3 acceptance (whole audit): not claimed.**

**Classifications:**
`A1-FL-01` = **WORKS_WITH_FRICTION** · `A1-FL-02` = **WORKS_WITH_FRICTION** · `A1-FL-03` = **WORKS_WITH_FRICTION** · `A1-FL-04` = **WORKS** · `A1-FL-05` = **WORKS**.

**A4 / V4: not authorized.**

---

*Recorded 2026-07-31. Out-of-tree Avalonia.Headless 12.0.5 product-runtime runner executed clean-profile smoke for first-launch/settings rows under disposable XDG; temporary runner and profiles removed; no production edits.*
