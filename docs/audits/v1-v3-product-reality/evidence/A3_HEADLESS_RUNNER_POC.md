# A3-H1 Headless Runner POC — V1–V3 Product Reality Audit

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3-H1 only** (harness proof-of-concept).
**Evidence date:** 2026-07-31
**Repo head at run:** `5bed4be2f1eb4b7f8833b0a10e9e0c369832c76d`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **Harness evidence only** (not A3 acceptance) |
| **A3 clean-profile smoke** | **Not started** |
| **A3 acceptance** | **Not claimed** |
| **A1 rows classified WORKS from this POC** | **None** (explicitly forbidden) |
| Real desktop UI launch | Not done |
| xdtools / screenshots / pointer automation / manual desktop | Not used |
| Production code modified | **No** |
| Existing tests modified | **No** |
| `Avalonia.Headless` added to any tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` A3 status rewritten | **No** |
| A4 / stabilization / V4 work begun | **No** |
| Commit / push | **No** |

**Related readiness design:** [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (A3-H0).

---

## 1. What this POC proved

A temporary **out-of-tree** .NET 10 runner under `/tmp` can:

1. Bootstrap production `Zaide.App.Composition.App` with an **audit-only** headless `AppBuilder` (does **not** call or patch `Program.BuildAvaloniaApp`).
2. Use `SetupWithClassicDesktopLifetime` so `App.OnFrameworkInitializationCompleted` runs and constructs **`MainWindow`**.
3. Resolve production DI via the same ReactiveUI Microsoft DI path used in production (`Program.ConfigureServices` → `CompositionRoot.Services`).
4. Dispatch one deterministic product command (`view.toggleBottomPanel`) and observe ViewModel state change (`IsBottomPanelVisible` false → true).
5. Complete clean shutdown via `IClassicDesktopStyleApplicationLifetime.Shutdown(0)` (production `desktop.Exit` → `ApplicationShutdown.Run`).
6. Keep settings/persistence resolution under a disposable XDG/`HOME` profile.

**Runtime result:** all vertical-slice assertions **pass**, process **exit code 0**.

**This does not mean A3 is complete.** Journey smoke packs, A1 row verdicts, and clean-profile product acceptance remain for a later authorized A3 execution pass.

---

## 2. Harness construction (temporary; deleted after run)

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-h1/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-h1/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (honors production `InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Package | `Avalonia.Headless` **12.0.5** only |
| `Avalonia.Headless.XUnit` | Not required; not referenced |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Runner outputs | `/tmp/zaide-a3-h1/out/`, `/tmp/zaide-a3-h1/obj/` |
| Audit entry type | `A3HeadlessEntry.BuildAvaloniaApp()` |

### 2.1 Audit-only AppBuilder (design realization)

```csharp
// Out-of-tree only — not present in the repository after cleanup
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<Zaide.App.Composition.App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = true,
        })
        .UseReactiveUIWithMicrosoftDependencyResolver(
            containerConfig: Zaide.App.Composition.Program.ConfigureServices,
            withResolver: sp => CompositionRoot.Services = sp!)
        .WithInterFont()
        .LogToTrace();
```

**Lifetime API proven:**
`builder.SetupWithClassicDesktopLifetime(Array.Empty<string>())`
— initializes the classic desktop lifetime **without** starting the desktop main loop / real platform backend via `UsePlatformDetect`.

**Not used:** production `Program.BuildAvaloniaApp()` (`UsePlatformDetect`), xdtools, OS screenshots, pointer automation.

### 2.2 Isolation protocol

Before any production type construction (process environment + runner re-apply):

| Variable | Disposable value (example run) |
|----------|--------------------------------|
| `HOME` | `/tmp/zaide-a3-h1-profile-2tqARapj/home` |
| `XDG_CONFIG_HOME` | `/tmp/zaide-a3-h1-profile-2tqARapj/config` |
| `XDG_DATA_HOME` | `/tmp/zaide-a3-h1-profile-2tqARapj/data` |
| `XDG_STATE_HOME` | `/tmp/zaide-a3-h1-profile-2tqARapj/state` |
| `XDG_CACHE_HOME` | `/tmp/zaide-a3-h1-profile-2tqARapj/cache` |

Preflight: `SettingsPathResolver.GetSettingsDirectory()` →
`/tmp/zaide-a3-h1-profile-2tqARapj/config/zaide` (absolute `XDG_CONFIG_HOME` prefix; not the real user profile).

---

## 3. Exact runner command

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-h1-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

dotnet "/tmp/zaide-a3-h1/out/Release/net10.0/Zaide.Tests.dll" \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-h1/evidence/A3-H1_HEADLESS_RUNNER_POC.json"
```

**Observed command (this run):**

```text
dotnet "/tmp/zaide-a3-h1/out/Release/net10.0/Zaide.Tests.dll" --profile "/tmp/zaide-a3-h1-profile-2tqARapj" --evidence "/tmp/zaide-a3-h1/evidence/A3-H1_HEADLESS_RUNNER_POC.json"
```

---

## 4. Vertical-slice results

| Check | Result |
|-------|--------|
| Headless application bootstrap reaches framework initialization | **Pass** (`framework_initialized`) |
| Windowing subsystem | `Headless` |
| Application type | `Zaide.App.Composition.App` |
| Lifetime type | `ClassicDesktopStyleApplicationLifetime` |
| Production DI resolves | **Pass** (`MainWindowViewModel`, `ISettingsService`, `ICommandRegistry`) |
| `MainWindow` created under headless lifetime | **Pass** (`Zaide.App.Shell.MainWindow`) |
| Deterministic command dispatch | **Pass** `view.toggleBottomPanel` via `ICommandRegistry` |
| Observed ViewModel state | `IsBottomPanelVisible`: **false → true** |
| Clean shutdown | **Pass** (`shutdown_completed`, `desktop.Shutdown(0)`) |
| Paths under disposable profile | **Pass** |
| Process exit code | **0** |

### 4.1 Filesystem artifacts under disposable profile

| Path | Exists |
|------|--------|
| `$XDG_CONFIG_HOME/zaide/conversations/conversations.json` | Yes (0644) |
| `$XDG_CONFIG_HOME/zaide/conversations/conversations.json.lastknowngood` | Yes (0644) |
| `$XDG_CONFIG_HOME/zaide/settings.json` | No (not written by this slice) |
| `$XDG_CONFIG_HOME/zaide/secrets.json` | No |

All written artifacts remained under the disposable profile root. No real-user config path was used.

---

## 5. Package versions

| Package | Version | Notes |
|---------|---------|--------|
| Avalonia (repo pin) | 12.0.5 | Unchanged `Directory.Packages.props` |
| Avalonia.Headless | 12.0.5 | Out-of-tree runner only |
| Avalonia.Headless.XUnit | n/a | Not required for this POC |
| Avalonia.Desktop (transitive via app) | 12.0.5 | App project reference |
| ReactiveUI.Avalonia.ME.DI (via app) | 12.0.3 | Production DI bootstrap |

---

## 6. Machine-readable evidence (captured run)

```json
{
  "schema_version": "a3-evidence-1",
  "audit": "v1-v3-product-reality",
  "phase": "A3-H1",
  "scenario_id": "A3-H1_HEADLESS_RUNNER_POC",
  "a1_row_ids": [],
  "started_at_utc": "2026-07-31T12:42:18.8023773+00:00",
  "finished_at_utc": "2026-07-31T12:42:19.7772823+00:00",
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "5bed4be2f1eb4b7f8833b0a10e9e0c369832c76d",
    "harness": "a3-h1-headless-runner-poc",
    "harness_version": "poc-0.1"
  },
  "runner_command": "dotnet \"/tmp/zaide-a3-h1/out/Release/net10.0/Zaide.Tests.dll\" --profile \"/tmp/zaide-a3-h1-profile-2tqARapj\" --evidence \"/tmp/zaide-a3-h1/evidence/A3-H1_HEADLESS_RUNNER_POC.json\"",
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5",
    "Avalonia.Headless.XUnit": "not_referenced_not_required"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-h1-profile-2tqARapj",
    "home": "/tmp/zaide-a3-h1-profile-2tqARapj/home",
    "xdg_config_home": "/tmp/zaide-a3-h1-profile-2tqARapj/config",
    "xdg_data_home": "/tmp/zaide-a3-h1-profile-2tqARapj/data",
    "xdg_state_home": "/tmp/zaide-a3-h1-profile-2tqARapj/state",
    "xdg_cache_home": "/tmp/zaide-a3-h1-profile-2tqARapj/cache",
    "resolved_settings_dir": "/tmp/zaide-a3-h1-profile-2tqARapj/config/zaide",
    "preflight_ok": true,
    "preflight_detail": "ok"
  },
  "bootstrap_result": "framework_initialized",
  "di_resolved": true,
  "main_window_created": true,
  "main_window_type": "Zaide.App.Shell.MainWindow",
  "command_input_sequence": [
    {
      "i": 1,
      "kind": "command",
      "name": "view.toggleBottomPanel",
      "payload": {
        "bottom_before": false,
        "bottom_after": true
      },
      "timestamp_utc": "2026-07-31T12:42:19.7772823+00:00"
    }
  ],
  "observed_events": [
    {
      "source": "Harness",
      "name": "AppBuilder.constructed",
      "data": {
        "windowing": "Headless",
        "use_headless_entry": true
      },
      "timestamp_utc": "2026-07-31T12:42:18.8126094+00:00"
    },
    {
      "source": "Harness",
      "name": "SetupWithClassicDesktopLifetime.completed",
      "data": {
        "application_type": "Zaide.App.Composition.App",
        "lifetime_type": "Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime"
      },
      "timestamp_utc": "2026-07-31T12:42:19.3959675+00:00"
    },
    {
      "source": "Service",
      "name": "ProductionDi.resolved",
      "data": {
        "main_window_view_model": "Zaide.App.Shell.MainWindowViewModel",
        "settings_service": "Zaide.Features.Settings.Infrastructure.SettingsService",
        "command_registry": "Zaide.App.Composition.CommandRegistry"
      },
      "timestamp_utc": "2026-07-31T12:42:19.395973+00:00"
    },
    {
      "source": "ViewModel",
      "name": "view.toggleBottomPanel.executed",
      "data": {
        "before": false,
        "after": true,
        "via": "ICommandRegistry"
      },
      "timestamp_utc": "2026-07-31T12:42:19.7528419+00:00"
    },
    {
      "source": "Process",
      "name": "desktop.Shutdown",
      "data": {
        "exit_code": 0
      },
      "timestamp_utc": "2026-07-31T12:42:19.7767587+00:00"
    }
  ],
  "control_state": [
    {
      "path": "MainWindow",
      "property": "Type",
      "value": "Zaide.App.Shell.MainWindow",
      "inspectable": true
    },
    {
      "path": "MainWindow.IsVisible",
      "property": "IsVisible",
      "value": true,
      "inspectable": true
    },
    {
      "path": "MainWindow.ViewModel",
      "property": "ViewModelType",
      "value": "Zaide.App.Shell.MainWindowViewModel",
      "inspectable": true
    },
    {
      "path": "MainWindowViewModel.IsBottomPanelVisible",
      "property": "IsBottomPanelVisible",
      "value": false,
      "inspectable": true
    },
    {
      "path": "MainWindowViewModel.IsBottomPanelVisible",
      "property": "IsBottomPanelVisible",
      "value": true,
      "inspectable": true
    }
  ],
  "observed_view_model_state": {
    "IsBottomPanelVisible.before": false,
    "IsBottomPanelVisible.after": true,
    "MainWindowType": "Zaide.App.Shell.MainWindow"
  },
  "filesystem_artifacts": [
    {
      "path": "/tmp/zaide-a3-h1-profile-2tqARapj/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json"
    },
    {
      "path": "/tmp/zaide-a3-h1-profile-2tqARapj/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-h1-profile-2tqARapj/config/zaide/settings.json",
      "exists": false,
      "notes": "expected_product_path"
    },
    {
      "path": "/tmp/zaide-a3-h1-profile-2tqARapj/config/zaide/secrets.json",
      "exists": false,
      "notes": "expected_product_path"
    }
  ],
  "shutdown_result": "shutdown_completed",
  "exit_code": 0,
  "assertions": [
    {
      "id": "bootstrap_framework",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Headless SetupWithClassicDesktopLifetime completed."
    },
    {
      "id": "production_di",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "MainWindowViewModel / settings / registry resolved from CompositionRoot."
    },
    {
      "id": "mainwindow_created",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "MainWindow assigned on classic desktop lifetime under headless."
    },
    {
      "id": "toggle_bottom_panel",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "IsBottomPanelVisible False -> True."
    },
    {
      "id": "paths_under_profile",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Settings dir=/tmp/zaide-a3-h1-profile-2tqARapj/config/zaide"
    },
    {
      "id": "clean_shutdown",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "shutdown_completed"
    },
    {
      "id": "filesystem_under_disposable_profile",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "All observed profile artifacts and settings paths remain under disposable root."
    }
  ],
  "evidence_classes_used": [
    "product-runtime"
  ],
  "limitations": [
    "Harness proof-of-concept only; not A3 clean-profile smoke acceptance.",
    "Does not classify any A1 row as WORKS.",
    "No Avalonia.Headless package added to the repository.",
    "No production code or existing tests modified."
  ],
  "verdict_hint": "pass",
  "classification": {
    "is_a3_acceptance": false,
    "is_harness_poc_only": true,
    "a3_clean_profile_smoke_status": "not_started",
    "a1_rows_classified_works": []
  }
}
```

---

## 7. Limitations (POC scope)

1. Single vertical slice only — not a journey pack or full A1 matrix execution.
2. Headless drawing only; no visual/pixel acceptance (theme fidelity, layout paint remain UNVERIFIED-VIS for A3).
3. Conversation store files were created under the disposable profile by production composition; settings/secrets were not written by this slice.
4. Harness was temporary under `/tmp` and is **not** checked into the repository.
5. Existing production-DI tests remain non-isolated (ISSUE-009 class); this POC does not fix them.
6. **A3 clean-profile smoke remains not started.**

---

## 8. Cleanup performed

| Artifact | Action |
|----------|--------|
| `/tmp/zaide-a3-h1/` runner, obj, out, evidence working copy | Removed after preserving this summary |
| Disposable profile `/tmp/zaide-a3-h1-profile-*` | Removed after evidence capture |
| Repository tracked content | Only this evidence file added under `docs/audits/.../evidence/` |
| Production / tests / packages | Unchanged |
| Commit / push | Not performed |

---

## 9. Readiness for later authorized A3 execution

| Question | Answer |
|----------|--------|
| Is Avalonia.Headless 12.0.5 runtime-viable for production composition? | **Yes** (this POC) |
| Proven lifetime path | Audit-only `UseHeadless` + `SetupWithClassicDesktopLifetime` |
| Isolation protocol viable? | **Yes** (`HOME` + absolute `XDG_*` before construction) |
| InternalsVisibleTo seam | Out-of-tree assembly named **`Zaide.Tests`** works without production metadata changes |
| Ready for a later authorized A3 clean-profile smoke pass? | **Yes — harness pattern is ready** as a design proof. A3 itself is still **not started**; an authorized session must still implement a durable (or re-created) harness, scenario packs, and evidence aggregation without classifying A1 rows from this POC alone. |

---

## 10. Status line

**A3-H1 Headless Runner POC: complete (harness evidence only).**

**A3 Clean-profile smoke: not started.**

**A3 acceptance: not claimed.**

**No A1 row classified WORKS based on this POC.**

**A4 / V4: not authorized.**

---

*Recorded 2026-07-31. Out-of-tree Avalonia.Headless 12.0.5 runner proved production DI + MainWindow + command dispatch + clean shutdown under disposable XDG; temporary runner and profile removed; no production edits, no commits or pushes.*
