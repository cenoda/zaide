# A3 Residual Smoke — First Launch Settings Clauses

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 supplemental evidence only** for two previously
unverified contract clauses under first-launch/settings.
**Evidence date:** 2026-07-31
**Repo head at run:** `1c6ae7c0a11430756b7566061a7b18b90171dd47`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 supplemental product-runtime evidence** (residual clauses only) |
| **Prior slice** | [A3_FIRST_LAUNCH_AND_SETTINGS.md](./A3_FIRST_LAUNCH_AND_SETTINGS.md) — preserved as historical evidence; **raw JSON not rewritten** |
| **Clauses executed** | (1) `A1-FL-03` future-schema preservation · (2) `A1-FL-04` environment-variable fallback |
| **Other A3 journeys** | **Not executed** (terminal and all remaining rows out of scope) |
| **A3 as a whole** | **Still incomplete** |
| **A4 / stabilization / V4** | **Not begun; no proceed decision** |
| Real desktop / xdtools / screenshots / pointer automation | **Not used** |
| Production code / tracked tests / package pins / audit policy | **Unchanged** |
| Real user `~/.config/zaide` | **Not read or written** |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A3_FIRST_LAUNCH_AND_SETTINGS.md](./A3_FIRST_LAUNCH_AND_SETTINGS.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md)
- [A2_FIRST_LAUNCH_AND_SETTINGS.md](./A2_FIRST_LAUNCH_AND_SETTINGS.md)

---

## 1. Clause results (authoritative for this supplemental note)

| Clause | Parent row | Prior row classification (historical) | Clause classification (this run) | Summary |
|--------|------------|----------------------------------------|----------------------------------|---------|
| Future-schema preservation | `A1-FL-03` | **WORKS_WITH_FRICTION** ([prior](./A3_FIRST_LAUNCH_AND_SETTINGS.md)) | **WORKS** | Primary `settings.json` with `schemaVersion: 99` → `LoadResult=UnsupportedVersion`; in-memory editor value restored from valid LKG (`CodeFontSize=19`); primary SHA-256 **unchanged** (future file not overwritten). |
| Environment-variable fallback | `A1-FL-04` | **WORKS** ([prior](./A3_FIRST_LAUNCH_AND_SETTINGS.md)) | **WORKS** | With synthetic `AGENT_API_KEY` / `AGENT_API_URL` / `AGENT_MODEL` and **no** secret-store key: Native Harness options resolve **configured**, `api_key_source_class=environment`. Second isolated process without env and without secrets: **unconfigured**, `api_key_source_class=empty`. No network send. |

### Parent-row classification policy

- This note **classifies only the two residual clauses**.
- It does **not** rewrite the prior five-row table or raw JSON in
  [A3_FIRST_LAUNCH_AND_SETTINGS.md](./A3_FIRST_LAUNCH_AND_SETTINGS.md).
- **`A1-FL-03` overall** remains **WORKS_WITH_FRICTION** as recorded previously
  (silent recovery UI friction and other already-evidenced behaviors still apply).
  The future-schema clause itself is **WORKS**.
- **`A1-FL-04` overall** remains **WORKS**; the env-fallback clause is now
  product-runtime proven (also **WORKS**).

---

## 2. Harness

Out-of-tree Avalonia.Headless **12.0.5** runner under `/tmp/zaide-a3-fl-residual/`
(deleted after capture). Assembly name `Zaide.Tests`. Audit-only
`A3HeadlessEntry.BuildAvaloniaApp()` (does not call production
`Program.BuildAvaloniaApp`). Production DI via `Program.ConfigureServices`.
One OS process per independent scenario; absolute disposable `HOME` + `XDG_*`
before composition.

| Package | Version |
|---------|---------|
| Avalonia.Headless | 12.0.5 |
| Avalonia (repo pin) | 12.0.5 |

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-fl-res-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

dotnet "/tmp/zaide-a3-fl-residual/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario <id> [--phase ...] \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-fl-residual/evidence/<id>.json" \
  --repo-head "1c6ae7c0a11430756b7566061a7b18b90171dd47"
```

---

## 3. Scenario A1-FL-03 — future-schema preservation

### 3.1 Inputs

| Step | Action |
|------|--------|
| 1 | Disposable profile |
| 2 | Write valid LKG `settings.json.lastknowngood` (schema **v3**, `codeFontSize` **19**) |
| 3 | Write primary `settings.json` with **schemaVersion 99** (unsupported future) and divergent editor values (`codeFontSize` 77) |
| 4 | Start production-composed headless process |
| 5 | Observe `LoadResult`, in-memory settings, primary/LKG hashes before and after |

### 3.2 Expected

- `LoadResult = UnsupportedVersion`
- In-memory settings from LKG (font size 19), not future primary (77) and not silent defaults-only without LKG
- Primary future-version file **not** overwritten

### 3.3 Observed

| Field | Value |
|-------|--------|
| Profile | `/tmp/zaide-a3-fl-res-profile-IRGrfl39` |
| Process exit | **0** |
| `LoadResult` | **UnsupportedVersion** |
| In-memory `CodeFontSize` | **19** (LKG) |
| In-memory `SchemaVersion` | **3** (from LKG model) |
| Primary SHA-256 before | `4b2fe8758ad83ff8f7046d7cf2480d21cbe75154aeb3f31df82a2dfd8f5772dc` |
| Primary SHA-256 after | `4b2fe8758ad83ff8f7046d7cf2480d21cbe75154aeb3f31df82a2dfd8f5772dc` (unchanged) |
| LKG SHA-256 | `c768eccc39f82693e9613e92f276d8f4384247d8007a720ab1dc021d7b58e3d8` (unchanged) |
| Clause classification | **WORKS** |

### 3.4 Machine-readable evidence

```json
{
  "schema_version": "a3-evidence-1",
  "phase": "A3-residual",
  "scenario_id": "A1-FL-03-future-schema",
  "scenario_phase": "run",
  "clause": "future-schema-preservation",
  "a1_row_ids": [
    "A1-FL-03"
  ],
  "exit_code": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "1c6ae7c0a11430756b7566061a7b18b90171dd47",
    "harness": "a3-first-launch-settings-residual",
    "harness_version": "a3-fl-residual-0.1"
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-fl-res-profile-IRGrfl39",
    "home": "/tmp/zaide-a3-fl-res-profile-IRGrfl39/home",
    "xdg_config_home": "/tmp/zaide-a3-fl-res-profile-IRGrfl39/config",
    "resolved_settings_dir": "/tmp/zaide-a3-fl-res-profile-IRGrfl39/config/zaide",
    "preflight_ok": true,
    "preflight_detail": "ok"
  },
  "bootstrap_result": "framework_initialized",
  "observed_view_model_state": {
    "LoadResult": "UnsupportedVersion",
    "CodeFontSize": 19,
    "SchemaVersion_in_memory": 3,
    "primary_sha256_before": "4b2fe8758ad83ff8f7046d7cf2480d21cbe75154aeb3f31df82a2dfd8f5772dc",
    "primary_sha256_after": "4b2fe8758ad83ff8f7046d7cf2480d21cbe75154aeb3f31df82a2dfd8f5772dc",
    "primary_unchanged": true,
    "lkg_sha256_before": "c768eccc39f82693e9613e92f276d8f4384247d8007a720ab1dc021d7b58e3d8",
    "lkg_sha256_after": "c768eccc39f82693e9613e92f276d8f4384247d8007a720ab1dc021d7b58e3d8"
  },
  "assertions": [
    {
      "id": "load_result_unsupported_version",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "LoadResult=UnsupportedVersion expected UnsupportedVersion"
    },
    {
      "id": "in_memory_from_lkg",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "CodeFontSize=19 expected LKG marker 19 (not future-file 77, not default 14 alone without LKG)"
    },
    {
      "id": "primary_not_overwritten",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Primary settings.json SHA-256 unchanged; future schema preserved on disk"
    },
    {
      "id": "lkg_preserved",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "LKG file hash unchanged during unsupported-version load"
    },
    {
      "id": "future_schema_clause",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Future unsupported schema: LoadResult=UnsupportedVersion, in-memory from LKG, primary not overwritten"
    }
  ],
  "clause_classification_hint": "WORKS",
  "shutdown_result": "shutdown_completed",
  "limitations": [
    "Classifies only the future-schema preservation clause of A1-FL-03.",
    "Does not re-run corrupt-JSON recovery or restart persistence (already evidenced).",
    "Does not rewrite prior A3_FIRST_LAUNCH_AND_SETTINGS evidence."
  ],
  "filesystem_artifacts": [
    {
      "path": "/tmp/zaide-a3-fl-res-profile-IRGrfl39/config/zaide/settings.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json"
    },
    {
      "path": "/tmp/zaide-a3-fl-res-profile-IRGrfl39/config/zaide/settings.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/settings.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-res-profile-IRGrfl39/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-res-profile-IRGrfl39/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json"
    }
  ],
  "control_state": [
    {
      "path": "pre.primary.path",
      "property": "path",
      "value": "/tmp/zaide-a3-fl-res-profile-IRGrfl39/config/zaide/settings.json",
      "inspectable": true
    },
    {
      "path": "pre.primary.sha256",
      "property": "sha256",
      "value": "4b2fe8758ad83ff8f7046d7cf2480d21cbe75154aeb3f31df82a2dfd8f5772dc",
      "inspectable": true
    },
    {
      "path": "pre.primary.schemaVersion_declared",
      "property": "schemaVersion_declared",
      "value": 99,
      "inspectable": true
    },
    {
      "path": "pre.primary.content",
      "property": "content",
      "value": "{
  \"schemaVersion\": 99,
  \"editor\": {
    \"codeFontFamily\": \"FutureFont\",
    \"codeFontSize\": 77,
    \"proseFontFamily\": \"FutureProse\",
    \"terminalFontFamily\": \"FutureTerm\",
    \"terminalFontSize\": 99,
    \"tabSize\": 2,
    \"insertSpaces\": false,
    \"showWhitespace\": true,
    \"showTabs\": true,
    \"showSpaces\": true,
    \"formatOnSave\": true
  },
  \"llm\": {
    \"baseUrl\": \"https://future.example.invalid/v1\",
    \"model\": \"future-model\",
    \"apiKeySource\": \"secret-store\"
  },
  \"keybindings\": {},
  \"debug\": {
    \"breakpointsByWorkspaceRoot\": {}
  },
  \"futureOnlyField\": \"must-not-be-loaded\"
}",
      "inspectable": true
    },
    {
      "path": "pre.lkg.path",
      "property": "path",
      "value": "/tmp/zaide-a3-fl-res-profile-IRGrfl39/config/zaide/settings.json.lastknowngood",
      "inspectable": true
    },
    {
      "path": "pre.lkg.sha256",
      "property": "sha256",
      "value": "c768eccc39f82693e9613e92f276d8f4384247d8007a720ab1dc021d7b58e3d8",
      "inspectable": true
    },
    {
      "path": "pre.lkg.codeFontSize",
      "property": "codeFontSize",
      "value": 19,
      "inspectable": true
    },
    {
      "path": "pre.lkg.content",
      "property": "content",
      "value": "{
  \"schemaVersion\": 3,
  \"editor\": {
    \"codeFontFamily\": \"Cascadia Code, Consolas, monospace\",
    \"codeFontSize\": 19,
    \"proseFontFamily\": \"Georgia, serif\",
    \"terminalFontFamily\": \"Cascadia Code, JetBrains Mono, DejaVu Sans Mono, monospace\",
    \"terminalFontSize\": 14,
    \"tabSize\": 4,
    \"insertSpaces\": true,
    \"showWhitespace\": false,
    \"showTabs\": false,
    \"showSpaces\": false,
    \"formatOnSave\": false
  },
  \"llm\": {
    \"baseUrl\": \"https://api.openai.com/v1\",
    \"model\": \"gpt-4o-mini\",
    \"apiKeySource\": \"secret-store\"
  },
  \"keybindings\": {},
  \"debug\": {
    \"breakpointsByWorkspaceRoot\": {}
  }
}",
      "inspectable": true
    },
    {
      "path": "Settings.LoadResult",
      "property": "LoadResult",
      "value": "UnsupportedVersion",
      "inspectable": true
    },
    {
      "path": "Settings.Current.SchemaVersion",
      "property": "SchemaVersion",
      "value": 3,
      "inspectable": true
    },
    {
      "path": "Settings.Current.Editor.CodeFontSize",
      "property": "CodeFontSize",
      "value": 19,
      "inspectable": true
    },
    {
      "path": "Settings.Current.Editor.TabSize",
      "property": "TabSize",
      "value": 4,
      "inspectable": true
    },
    {
      "path": "post.primary.sha256",
      "property": "sha256",
      "value": "4b2fe8758ad83ff8f7046d7cf2480d21cbe75154aeb3f31df82a2dfd8f5772dc",
      "inspectable": true
    },
    {
      "path": "post.primary.content",
      "property": "content",
      "value": "{
  \"schemaVersion\": 99,
  \"editor\": {
    \"codeFontFamily\": \"FutureFont\",
    \"codeFontSize\": 77,
    \"proseFontFamily\": \"FutureProse\",
    \"terminalFontFamily\": \"FutureTerm\",
    \"terminalFontSize\": 99,
    \"tabSize\": 2,
    \"insertSpaces\": false,
    \"showWhitespace\": true,
    \"showTabs\": true,
    \"showSpaces\": true,
    \"formatOnSave\": true
  },
  \"llm\": {
    \"baseUrl\": \"https://future.example.invalid/v1\",
    \"model\": \"future-model\",
    \"apiKeySource\": \"secret-store\"
  },
  \"keybindings\": {},
  \"debug\": {
    \"breakpointsByWorkspaceRoot\": {}
  },
  \"futureOnlyField\": \"must-not-be-loaded\"
}",
      "inspectable": true
    },
    {
      "path": "post.primary.unchanged",
      "property": "unchanged",
      "value": true,
      "inspectable": true
    },
    {
      "path": "post.primary.still_future_schema",
      "property": "still_future_schema",
      "value": true,
      "inspectable": true
    },
    {
      "path": "post.lkg.sha256",
      "property": "sha256",
      "value": "c768eccc39f82693e9613e92f276d8f4384247d8007a720ab1dc021d7b58e3d8",
      "inspectable": true
    },
    {
      "path": "post.lkg.unchanged",
      "property": "unchanged",
      "value": true,
      "inspectable": true
    }
  ],
  "observed_events": [
    {
      "source": "Harness",
      "name": "seeded_future_schema_profile",
      "data": {
        "primary_schema": 99,
        "lkg_code_font_size": 19,
        "primary_sha256": "4b2fe8758ad83ff8f7046d7cf2480d21cbe75154aeb3f31df82a2dfd8f5772dc",
        "lkg_sha256": "c768eccc39f82693e9613e92f276d8f4384247d8007a720ab1dc021d7b58e3d8"
      },
      "timestamp_utc": "2026-07-31T13:10:21.4932351+00:00"
    },
    {
      "source": "Harness",
      "name": "AppBuilder.constructed",
      "data": {
        "windowing": "Headless"
      },
      "timestamp_utc": "2026-07-31T13:10:21.4956763+00:00"
    },
    {
      "source": "Harness",
      "name": "SetupWithClassicDesktopLifetime.completed",
      "data": {
        "application_type": "Zaide.App.Composition.App",
        "lifetime_type": "Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime"
      },
      "timestamp_utc": "2026-07-31T13:10:22.0651544+00:00"
    },
    {
      "source": "Service",
      "name": "ProductionDi.resolved",
      "data": {
        "settings": "Zaide.Features.Settings.Infrastructure.SettingsService",
        "secrets": "Zaide.Features.Settings.Infrastructure.FileSecretStore"
      },
      "timestamp_utc": "2026-07-31T13:10:22.3741264+00:00"
    },
    {
      "source": "Service",
      "name": "settings.future_schema_load",
      "data": {
        "LoadResult": "UnsupportedVersion",
        "CodeFontSize": 19,
        "primary_unchanged": true,
        "primary_sha256_before": "4b2fe8758ad83ff8f7046d7cf2480d21cbe75154aeb3f31df82a2dfd8f5772dc",
        "primary_sha256_after": "4b2fe8758ad83ff8f7046d7cf2480d21cbe75154aeb3f31df82a2dfd8f5772dc"
      },
      "timestamp_utc": "2026-07-31T13:10:22.3747257+00:00"
    }
  ]
}
```

---

## 4. Scenario A1-FL-04 — environment-variable fallback

### 4.1 Inputs

**Process A (with-env)** — isolated profile; **no** `secrets.json` / secret-store key:

| Variable | Value class |
|----------|-------------|
| `AGENT_API_KEY` | synthetic sentinel (never emitted in evidence) |
| `AGENT_API_URL` | `https://a3-residual.example.invalid/v1` |
| `AGENT_MODEL` | `a3-residual-model` |

Resolve production `AgentExecutionService.BuildEffectiveOptions()` through DI
(Native Harness options path). **No** `ExecuteAsync` / network call.

**Process B (without-env)** — separate disposable profile; env vars **unset**; no secret file.

### 4.2 Expected

- Process A: options **configured**; key source **environment**
- Process B: options **unconfigured** (empty key in fallback chain)

### 4.3 Observed

| Process | Profile | Configured | `api_key_source_class` | BaseUrl | Model | Exit |
|---------|---------|------------|------------------------|---------|-------|------|
| with-env | `/tmp/zaide-a3-fl-res-profile-okEZqhD0` | **true** | **environment** | synthetic URL | synthetic model | **0** |
| without-env | (separate `/tmp/zaide-a3-fl-res-profile-*`) | **false** | **empty** | settings default `https://api.openai.com/v1` | settings default `gpt-4o-mini` | **0** |

Notes:

- Process B may still surface default BaseUrl/Model from `SettingsModel.Defaults`;
  **configuration requires a non-empty API key** — missing env + missing secret ⇒ unconfigured.
- Synthetic key appears only as SHA-256 / booleans in evidence; plaintext key **absent**
  from all evidence JSON (guarded in harness write path).
- No provider/network call.

| Clause classification | **WORKS** |

### 4.4 Machine-readable evidence — with-env

```json
{
  "schema_version": "a3-evidence-1",
  "phase": "A3-residual",
  "scenario_id": "A1-FL-04-env-fallback",
  "scenario_phase": "with-env",
  "clause": "environment-variable-fallback",
  "a1_row_ids": [
    "A1-FL-04"
  ],
  "exit_code": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "1c6ae7c0a11430756b7566061a7b18b90171dd47",
    "harness": "a3-first-launch-settings-residual",
    "harness_version": "a3-fl-residual-0.1"
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-fl-res-profile-okEZqhD0",
    "home": "/tmp/zaide-a3-fl-res-profile-okEZqhD0/home",
    "xdg_config_home": "/tmp/zaide-a3-fl-res-profile-okEZqhD0/config",
    "resolved_settings_dir": "/tmp/zaide-a3-fl-res-profile-okEZqhD0/config/zaide",
    "preflight_ok": true,
    "preflight_detail": "ok"
  },
  "bootstrap_result": "framework_initialized",
  "observed_view_model_state": {
    "configured": true,
    "api_key_source_class": "environment",
    "base_url": "https://a3-residual.example.invalid/v1",
    "model": "a3-residual-model",
    "api_key_nonempty": true,
    "api_key_sha256": "22a2155d3374ce36dbb4d4e21ca1f999493654a8c66736e032486451fe205c81",
    "secret_store_key_present": false,
    "network_send": false
  },
  "assertions": [
    {
      "id": "env_vars_present_at_start",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "AGENT_API_* set before composition (key presence only logged)"
    },
    {
      "id": "no_secret_store_key",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Secret store has no llm.apiKey"
    },
    {
      "id": "options_resolved",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "INativeHarness/AgentExecutionService options resolved"
    },
    {
      "id": "configured_via_env",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "IsConfigured: ApiKey+BaseUrl+Model non-empty"
    },
    {
      "id": "baseurl_from_env",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "BaseUrl=https://a3-residual.example.invalid/v1"
    },
    {
      "id": "model_from_env",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Model=a3-residual-model"
    },
    {
      "id": "api_key_source_environment",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "source_class=environment"
    },
    {
      "id": "no_network_send",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "No ExecuteAsync / HTTP send invoked"
    }
  ],
  "clause_classification_hint": "WORKS",
  "shutdown_result": "shutdown_completed",
  "limitations": [
    "Synthetic credentials only; key value never emitted in evidence.",
    "No provider/network call performed.",
    "Classifies only the env-var fallback clause of A1-FL-04."
  ],
  "filesystem_artifacts": [
    {
      "path": "/tmp/zaide-a3-fl-res-profile-okEZqhD0/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-res-profile-okEZqhD0/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json"
    }
  ],
  "control_state": [
    {
      "path": "env.AGENT_API_KEY.present",
      "property": "present",
      "value": true,
      "inspectable": true
    },
    {
      "path": "env.AGENT_API_URL",
      "property": "AGENT_API_URL",
      "value": "https://a3-residual.example.invalid/v1",
      "inspectable": true
    },
    {
      "path": "env.AGENT_MODEL",
      "property": "AGENT_MODEL",
      "value": "a3-residual-model",
      "inspectable": true
    },
    {
      "path": "env.AGENT_API_KEY.sha256",
      "property": "sha256",
      "value": "22a2155d3374ce36dbb4d4e21ca1f999493654a8c66736e032486451fe205c81",
      "inspectable": true
    },
    {
      "path": "secrets.json.exists_before",
      "property": "exists_before",
      "value": false,
      "inspectable": true
    },
    {
      "path": "secrets.json.exists_after_boot",
      "property": "exists_after_boot",
      "value": false,
      "inspectable": true
    },
    {
      "path": "ISecretStore.llm.apiKey.present",
      "property": "present",
      "value": false,
      "inspectable": true
    },
    {
      "path": "options.resolved",
      "property": "resolved",
      "value": true,
      "inspectable": true
    },
    {
      "path": "options.BaseUrl",
      "property": "BaseUrl",
      "value": "https://a3-residual.example.invalid/v1",
      "inspectable": true
    },
    {
      "path": "options.Model",
      "property": "Model",
      "value": "a3-residual-model",
      "inspectable": true
    },
    {
      "path": "options.ApiKey.nonempty",
      "property": "nonempty",
      "value": true,
      "inspectable": true
    },
    {
      "path": "options.ApiKey.matches_synthetic_sha256",
      "property": "matches_synthetic_sha256",
      "value": true,
      "inspectable": true
    },
    {
      "path": "options.configured",
      "property": "configured",
      "value": true,
      "inspectable": true
    },
    {
      "path": "options.api_key_source_class",
      "property": "api_key_source_class",
      "value": "environment",
      "inspectable": true
    },
    {
      "path": "options.BaseUrl.matches_env",
      "property": "matches_env",
      "value": true,
      "inspectable": true
    },
    {
      "path": "options.Model.matches_env",
      "property": "matches_env",
      "value": true,
      "inspectable": true
    }
  ],
  "observed_events": [
    {
      "source": "Harness",
      "name": "AppBuilder.constructed",
      "data": {
        "windowing": "Headless"
      },
      "timestamp_utc": "2026-07-31T13:10:23.9367808+00:00"
    },
    {
      "source": "Harness",
      "name": "SetupWithClassicDesktopLifetime.completed",
      "data": {
        "application_type": "Zaide.App.Composition.App",
        "lifetime_type": "Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime"
      },
      "timestamp_utc": "2026-07-31T13:10:24.4900394+00:00"
    },
    {
      "source": "Service",
      "name": "ProductionDi.resolved",
      "data": {
        "settings": "Zaide.Features.Settings.Infrastructure.SettingsService",
        "secrets": "Zaide.Features.Settings.Infrastructure.FileSecretStore"
      },
      "timestamp_utc": "2026-07-31T13:10:24.8013095+00:00"
    },
    {
      "source": "Service",
      "name": "native_harness.options_with_env",
      "data": {
        "configured": true,
        "api_key_source_class": "environment",
        "base_url": "https://a3-residual.example.invalid/v1",
        "model": "a3-residual-model",
        "api_key_nonempty": true,
        "api_key_sha256": "22a2155d3374ce36dbb4d4e21ca1f999493654a8c66736e032486451fe205c81"
      },
      "timestamp_utc": "2026-07-31T13:10:24.8017976+00:00"
    }
  ]
}
```

### 4.5 Machine-readable evidence — without-env

```json
{
  "schema_version": "a3-evidence-1",
  "phase": "A3-residual",
  "scenario_id": "A1-FL-04-env-fallback",
  "scenario_phase": "without-env",
  "clause": "environment-variable-fallback",
  "a1_row_ids": [
    "A1-FL-04"
  ],
  "exit_code": 0,
  "host": {
    "os": "linux",
    "rid": "arch-x64",
    "repo_head": "1c6ae7c0a11430756b7566061a7b18b90171dd47",
    "harness": "a3-first-launch-settings-residual",
    "harness_version": "a3-fl-residual-0.1"
  },
  "package_versions": {
    "Avalonia.Headless": "12.0.5",
    "Avalonia (repo pin)": "12.0.5"
  },
  "isolation": {
    "profile_root": "/tmp/zaide-a3-fl-res-profile-wOKwqXEO",
    "home": "/tmp/zaide-a3-fl-res-profile-wOKwqXEO/home",
    "xdg_config_home": "/tmp/zaide-a3-fl-res-profile-wOKwqXEO/config",
    "resolved_settings_dir": "/tmp/zaide-a3-fl-res-profile-wOKwqXEO/config/zaide",
    "preflight_ok": true,
    "preflight_detail": "ok"
  },
  "bootstrap_result": "framework_initialized",
  "observed_view_model_state": {
    "configured": false,
    "api_key_source_class": "empty",
    "api_key_nonempty": false,
    "base_url": "https://api.openai.com/v1",
    "model": "gpt-4o-mini",
    "secret_store_key_present": false,
    "network_send": false
  },
  "assertions": [
    {
      "id": "env_cleared",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "AGENT_API_* unset"
    },
    {
      "id": "no_secret_file_key",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "No secrets.json / empty secret store"
    },
    {
      "id": "api_key_empty",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "Resolved ApiKey empty"
    },
    {
      "id": "unconfigured",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "configured=False api_key_nonempty=False source=empty"
    },
    {
      "id": "fallback_chain_empty_key",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "source_class=empty expected empty when no env and no secret"
    },
    {
      "id": "no_network_send",
      "result": "pass",
      "evidence_class": "product-runtime",
      "detail": "No ExecuteAsync / HTTP send invoked"
    }
  ],
  "clause_classification_hint": "WORKS",
  "shutdown_result": "shutdown_completed",
  "limitations": [
    "Negative path: no env, no secret \u2192 unconfigured (no network).",
    "Default BaseUrl/Model from settings may still be non-empty; configuration requires key."
  ],
  "filesystem_artifacts": [
    {
      "path": "/tmp/zaide-a3-fl-res-profile-wOKwqXEO/config/zaide/conversations/conversations.json.lastknowngood",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json.lastknowngood"
    },
    {
      "path": "/tmp/zaide-a3-fl-res-profile-wOKwqXEO/config/zaide/conversations/conversations.json",
      "exists": true,
      "unix_mode": "0644",
      "notes": "config/zaide/conversations/conversations.json"
    }
  ],
  "control_state": [
    {
      "path": "env.AGENT_API_KEY.present",
      "property": "present",
      "value": false,
      "inspectable": true
    },
    {
      "path": "env.AGENT_API_URL.present",
      "property": "present",
      "value": false,
      "inspectable": true
    },
    {
      "path": "env.AGENT_MODEL.present",
      "property": "present",
      "value": false,
      "inspectable": true
    },
    {
      "path": "secrets.json.exists",
      "property": "exists",
      "value": false,
      "inspectable": true
    },
    {
      "path": "ISecretStore.llm.apiKey.present",
      "property": "present",
      "value": false,
      "inspectable": true
    },
    {
      "path": "options.BaseUrl",
      "property": "BaseUrl",
      "value": "https://api.openai.com/v1",
      "inspectable": true
    },
    {
      "path": "options.Model",
      "property": "Model",
      "value": "gpt-4o-mini",
      "inspectable": true
    },
    {
      "path": "options.ApiKey.nonempty",
      "property": "nonempty",
      "value": false,
      "inspectable": true
    },
    {
      "path": "options.configured",
      "property": "configured",
      "value": false,
      "inspectable": true
    },
    {
      "path": "options.api_key_source_class",
      "property": "api_key_source_class",
      "value": "empty",
      "inspectable": true
    }
  ],
  "observed_events": [
    {
      "source": "Harness",
      "name": "AppBuilder.constructed",
      "data": {
        "windowing": "Headless"
      },
      "timestamp_utc": "2026-07-31T13:10:26.3618786+00:00"
    },
    {
      "source": "Harness",
      "name": "SetupWithClassicDesktopLifetime.completed",
      "data": {
        "application_type": "Zaide.App.Composition.App",
        "lifetime_type": "Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime"
      },
      "timestamp_utc": "2026-07-31T13:10:26.9169186+00:00"
    },
    {
      "source": "Service",
      "name": "ProductionDi.resolved",
      "data": {
        "settings": "Zaide.Features.Settings.Infrastructure.SettingsService",
        "secrets": "Zaide.Features.Settings.Infrastructure.FileSecretStore"
      },
      "timestamp_utc": "2026-07-31T13:10:27.2243803+00:00"
    },
    {
      "source": "Service",
      "name": "native_harness.options_without_env",
      "data": {
        "configured": false,
        "api_key_source_class": "empty",
        "api_key_nonempty": false,
        "base_url": "https://api.openai.com/v1",
        "model": "gpt-4o-mini"
      },
      "timestamp_utc": "2026-07-31T13:10:27.2248472+00:00"
    }
  ]
}
```

---

## 5. Path isolation and safety

| Check | Result |
|-------|--------|
| All resolved settings dirs under disposable `$PROFILE_ROOT/config/zaide` | **Yes** |
| Real `/home/cenoda/.config/zaide` used as profile | **No** |
| Synthetic key plaintext in evidence JSON | **No** (asserted) |
| Network / real provider call | **No** |
| Temporary runner/profiles after cleanup | **Removed** |

---

## 6. Explicit non-claims

1. **A3 remains incomplete.** Terminal and all non-FL residual journeys are not executed.
2. **No A4 / stabilization / V4 decision.**
3. Prior [A3_FIRST_LAUNCH_AND_SETTINGS.md](./A3_FIRST_LAUNCH_AND_SETTINGS.md) is **historical** and was not rewritten.
4. Overall `A1-FL-03` row classification is **not** upgraded solely by this clause; silent recovery UI friction from the prior slice still applies.
5. No production code, tests, package pins, or audit policy changes.

---

## 7. Cleanup

| Artifact | Action |
|----------|--------|
| `/tmp/zaide-a3-fl-residual/` | Removed after preserving this summary |
| `/tmp/zaide-a3-fl-res-profile-*` | Removed |
| Tracked deliverable | This file only |

---

## 8. Status line

**A3 residual first-launch settings clauses: complete for the two authorized clauses.**

| Clause | Classification |
|--------|----------------|
| `A1-FL-03` future-schema preservation | **WORKS** |
| `A1-FL-04` environment-variable fallback | **WORKS** |

**A3 clean-profile smoke (full matrix): incomplete.**

**Next authorized A3 journey (per closeout instruction): terminal** — not started in this session.

**A4 / V4: not authorized.**

---

*Recorded 2026-07-31. Supplemental headless product-runtime evidence only; temporary runner and profiles removed; no production edits.*
