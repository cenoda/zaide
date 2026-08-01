# A3 Clean-Profile Smoke — Agent Creation and Backend Onboarding (`A1-AC-01`, `A1-AC-02`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 agent creation / backend onboarding execution slice only** — rows
`A1-AC-01`, `A1-AC-02`.
**Evidence date:** 2026-08-01
**Repo head at run:** `a5a26087b3924e00471445802afb01c4faef2ded`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (AC-01/02 only) |
| **A3 slice** | Agent Creation and Backend Onboarding (`A1-AC-01`, `A1-AC-02`) |
| **A3 as a whole** | **Incomplete** — agent send, permissions, trace/memory/usage/termination, restart/recovery, residual journeys **not executed** in this note |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / desktop pointer automation / manual desktop | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| `Avalonia.Headless` added to tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` rewritten to “A3 complete” | **No** |
| Prior A2 / A3 evidence rewritten | **No** |
| Real user `~/.config/zaide` read or written by this run | **No** (disposable `HOME` + `XDG_*` only) |
| Real workspace roots opened | **No** |
| Fake backends / test doubles / internal bind injection as product success | **No** |
| External network / backend install / credential provisioning | **No** |
| `A1-AS`, `A1-TP`, trace/memory/restart rows | **Not executed** (explicit) |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§10)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)

**Out of scope for this slice (explicit):**

- Agent send/response (`A1-AS-*`), tools/permissions (`A1-TP-*`), multi-agent routing beyond catalog observation
- Trace, memory, usage, termination, restart/recovery
- A4, stabilization, V4 planning
- Production code, tracked tests, package pins, audit policy edits
- Positive bind/send outcomes manufactured via `BindNativeHarness`, `BindAcpRuntime`, `SetBinding`, or `RequestAuthenticateAsync`
- Pixel paint of binding status chrome or settings overlay styling (**UNVERIFIED-VIS** where noted)

---

## 1. Two-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-AC-01` | **UNWIRED** | Cold headless shell exposes **no** user-reachable Add/Create Agent command, create-agent button, or dedicated Agent Panel chrome. `ICommandRegistry` (38 commands) has **zero** agent-create-like IDs. People roster seeds only `User` + `Zaide Agent`. `OpenDirectConversationCommand` opens a stable direct DM (`direct:4e957b0b…`) without adding roster agents or panel-host entries — **Townhall DM navigation is conversation entry, not agent-creation**. Catalog still lists routing seeds (Alpha–Gamma, Zaide Agent) but People does not expose a create/configure workflow. |
| `A1-AC-02` | **WORKS_WITH_FRICTION** | **Infrastructure + read-only status:** `IAgentActorBackendSelectionService` resolves; clean-profile Zaide Agent DM shows `BackendBindingLabel="Unbound"`, auth caption `Disconnected`, status region visible; `AgentBackendBindingPanel` has **no** interactive bind/auth/capability controls. **User onboarding gap (friction):** no registry command or UI for Native Harness / ACP bind, actor binding, authentication, capability display, unbind/rebind, or cleanup. Settings overlay (status-bar path) exposes **Editor / Terminal / LLM** only — shared LLM `BaseUrl`/`Model`/`API Key` are **not** counted as per-actor backend binding. `AgentBackendBindingPresenter` is DI-registered with **no** user entry point. Positive bind/configure path **not fabricated**. |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

**Evidence-class distinction:**

| Class | Meaning in this slice |
|-------|------------------------|
| `product-runtime` | Production DI + `MainWindow` + Townhall/Settings ViewModels and visual tree under headless |
| `store-truth` | Conversation id stability for DM find-or-create |
| `infrastructure-readonly` | DI-resolved selection service `GetSnapshot` on clean profile (observation only; not bind injection) |
| `visual-only` | Pixel paint of binding status row, disconnected coloring, settings section chrome — **not claimed** (`UNVERIFIED-VIS`) |
| `blocked-backend` | Positive Native Harness / ACP bind onboarding — **not attempted** (no user path; no fabrication) |

---

## 2. Harness construction (temporary; deleted after evidence capture)

Recreated the H1-proven out-of-tree Avalonia.Headless **12.0.5** runner under `/tmp` (not tracked).

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-ac/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-ac/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Packages | `Avalonia.Headless` **12.0.5**; `ReactiveUI.Avalonia.Microsoft.Extensions.DependencyInjection` **12.0.3** (runner only) |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Isolation | One disposable profile **per scenario process**; `HOME` + all absolute `XDG_*` set **before** production composition |
| Observation | `ICommandRegistry`, `TownhallViewModel`, `IActorCatalog`, `IAgentPanelHost`, settings overlay subtree, read-only `GetSnapshot` |
| Not used | xdtools, OS screenshots, desktop pointer automation, manual UI, `BindNativeHarness`/`BindAcpRuntime` injection, unit tests as proof, real workspace roots |

### 2.1 Isolation protocol

| Variable | Disposable value |
|----------|------------------|
| `HOME` | `$PROFILE_ROOT/home` |
| `XDG_CONFIG_HOME` | `$PROFILE_ROOT/config` (absolute) |
| `XDG_DATA_HOME` | `$PROFILE_ROOT/data` |
| `XDG_STATE_HOME` | `$PROFILE_ROOT/state` |
| `XDG_CACHE_HOME` | `$PROFILE_ROOT/cache` |

Preflight asserted `SettingsPathResolver.GetSettingsDirectory()` under `$XDG_CONFIG_HOME/zaide` and **not** the real-user `/home/cenoda/.config/zaide`.

### 2.2 Package versions

| Package | Version | Notes |
|---------|---------|--------|
| Avalonia (repo pin) | 12.0.5 | Unchanged `Directory.Packages.props` |
| Avalonia.Headless | 12.0.5 | Out-of-tree runner only |
| Avalonia.Headless.XUnit | n/a | Not required |
| ReactiveUI.Avalonia.ME.DI (via app) | 12.0.3 | Production DI bootstrap |

### 2.3 Runner command pattern

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-ac-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

dotnet "/tmp/zaide-a3-ac/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario A1-AC-0N \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-ac/evidence/A1-AC-0N.json" \
  --repo-head "a5a26087b3924e00471445802afb01c4faef2ded"
```

### 2.4 Disposable profiles (final capture)

| Scenario | Profile root | Exit | Assertions |
|----------|--------------|------|------------|
| `A1-AC-01` | `/tmp/zaide-a3-ac-profile-tzyB75NO` | **0** | 9/9 pass |
| `A1-AC-02` | `/tmp/zaide-a3-ac-profile-W2YtRr2f` | **0** | 7/7 pass |

**Total:** 16 product-runtime assertions, all pass on final capture.

Filesystem artifacts under each profile remained only under `$XDG_CONFIG_HOME/zaide/conversations/` (`conversations.json` + `.lastknowngood`). No `settings.json` or `secrets.json` written by these scenarios.

---

## 3. `A1-AC-01` — agent creation / identity workflow absence

### 3.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch; clean disposable profile | product-runtime |
| 2 | Enumerate `ICommandRegistry` for agent-create-like command IDs | product-runtime |
| 3 | Observe People roster (`TownhallViewModel.Agents`) | product-runtime |
| 4 | Compare `IActorCatalog.ListAgents()` vs People roster | product-runtime |
| 5 | `OpenDirectConversationCommand(CanonicalTownhallAgent)`; reopen same pair | product-runtime + store-truth |
| 6 | Walk visual tree for banned Agent Panel chrome and create-agent buttons | product-runtime |

### 3.2 Observed results

| Check | Result |
|-------|--------|
| Registered commands | **38** total |
| Agent-create-like commands | **0** (`agent_create.commands=[]`) |
| People roster | `User`, `Zaide Agent` only |
| Catalog agents (routing) | `Alpha`, `Beta`, `Delta`, `Gamma`, `Zaide Agent` (5) |
| `IAgentPanelHost.Panels` before DM | **0** |
| After DM open | Active `direct:4e957b0b88dd4fe4a03845cd514543bb`; People still 2 rows; panels still **0** |
| Second open same agent | **Same** `ConversationId` (find-or-create) |
| Banned Agent Panel chrome types | **None** (`AgentPanelView`, `AgentPanelChrome`, …) |
| Create-agent buttons in tree | **0** |
| `TownhallView` present | **Yes** |
| `AgentBackendBindingPanel` in tree | **Yes** (status adjunct, not creation chrome) |

### 3.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-AC-01",
  "exitCode": 0,
  "isolation": {
    "profileRoot": "/tmp/zaide-a3-ac-profile-tzyB75NO",
    "resolvedSettingsDir": "/tmp/zaide-a3-ac-profile-tzyB75NO/config/zaide",
    "preflightOk": true
  },
  "observedViewModelState": {
    "command_registry.count": 38,
    "agent_create.commands": [],
    "townhall.people_roster": ["User", "Zaide Agent"],
    "catalog.list_agents": ["Alpha", "Beta", "Delta", "Gamma", "Zaide Agent"],
    "townhall.active_conversation": "direct:4e957b0b88dd4fe4a03845cd514543bb",
    "panel_host.panels_after_dm": 0,
    "agent_panel.banned_types_found": [],
    "ui.create_agent_buttons": [],
    "townhall_dm_is_not_agent_creation": "OpenDirectConversationCommand navigates to existing seeded Zaide Agent DM; does not expose create/rename/remove/configure-agent workflow.",
    "visual_pixel_paint": "UNVERIFIED-VIS"
  },
  "classificationHint": "UNWIRED"
}
```

### 3.4 Classification rationale — **UNWIRED**

No user-reachable path creates, renames, removes, or configures a new agent identity. Dedicated Agent Panel creation chrome remains absent. Townhall People → Zaide Agent is **DM navigation** to a seeded canonical actor, confirmed by stable direct conversation id and unchanged People roster — **not** an agent-creation workflow. Aligns with A2 `Missing` for `A1-AC-01`; A3 product-runtime confirms the gap on a clean disposable profile without fabricating a creation surface.

---

## 4. `A1-AC-02` — backend onboarding infrastructure vs user entry point

### 4.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch | product-runtime |
| 2 | Enumerate registry for bind/backend/ACP/harness/auth commands | product-runtime |
| 3 | Open Zaide Agent DM | product-runtime |
| 4 | Read-only `GetSnapshot(peer)` + Townhall binding projection | infrastructure-readonly + product-runtime |
| 5 | Walk tree for bind buttons / interactive binding-panel controls | product-runtime |
| 6 | User-reachable settings open (`StatusBarViewModel.OpenSettingsCommand`) | product-runtime |
| 7 | Inspect **settings overlay subtree only** for LLM vs ACP/backend-bind sections | product-runtime |
| 8 | Confirm `SettingsModel` schema has no agent/backend section | product-runtime |

### 4.2 Observed results

| Check | Result |
|-------|--------|
| Bind/backend registry commands | **0** |
| `GetSnapshot` (Zaide Agent) | `is_bound=false`, `backend_label=Unbound`, `auth_state=Disconnected`, `advertised_auth_methods=[]` |
| Townhall projection | `BackendBindingLabel=Unbound`, `BackendAuthStatusCaption=Disconnected`, `IsBackendBindingStatusVisible=true` |
| Bind/backend buttons (whole shell) | **0** |
| Interactive controls on `AgentBackendBindingPanel` | **0** (status captions only) |
| Settings sections (overlay subtree) | **Editor**, **Terminal**, **LLM** — Model, Base URL, API Key |
| ACP / Native Harness / backend-binding settings section | **Absent** |
| `SettingsModel` top-level props | `SchemaVersion`, `Editor`, `Llm`, `Keybindings`, `Debug` only |
| `AgentBackendBindingPresenter` registered | **Yes** (DI only) |
| User bind entry point | **Absent** — gap recorded truthfully |
| Positive bind path | **Not fabricated** |

**LLM vs binding distinction (explicit):** shared `Llm.BaseUrl` / `Llm.Model` / `ApiKeySource=secret-store` configure the Native Harness **provider options source** only. They do **not** establish per-actor `AgentActorBackendBindingStore` entries and are **not** scored as backend onboarding success.

### 4.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-AC-02",
  "exitCode": 0,
  "observedViewModelState": {
    "backend_bind.commands": [],
    "backend.snapshot.is_bound": false,
    "backend.snapshot.backend_label": "Unbound",
    "townhall.backend_binding_label": "Unbound",
    "townhall.backend_auth_caption": "Disconnected",
    "binding_panel.interactive_controls": [],
    "settings.section_labels_observed": ["Editor", "Terminal", "LLM", "Apply", "Discard", "Rebase / Refresh"],
    "settings.has_acp_or_backend_bind_section": false,
    "settings_model.top_level_properties": ["Debug", "Editor", "Keybindings", "Llm", "SchemaVersion"],
    "infrastructure.presenter_registered": true,
    "infrastructure.user_bind_entry_point": "absent",
    "infrastructure.gap_truth": "IAgentActorBackendSelectionService and status panel exist; no user-reachable BindNativeHarness/BindAcpRuntime/RequestAuthenticate/unbind/rebind/capability UI.",
    "visual_binding_panel_paint": "UNVERIFIED-VIS"
  },
  "classificationHint": "WORKS_WITH_FRICTION"
}
```

### 4.4 Classification rationale — **WORKS_WITH_FRICTION**

**Works (observed):** pull-based read-only unbound status projection on an open Zaide Agent DM; binding infrastructure composes in production DI.

**Friction / gap (observed, not fabricated):** no user-reachable Native Harness or ACP configuration, actor bind, authentication, advertised-method selection, capability matrix, unbind/rebind, or cleanup workflow. Settings LLM fields are present but explicitly excluded from backend-binding credit. Positive onboarding success was **not** manufactured by calling internal bind APIs. Aligns with A2 `Wired-with-gap` for `A1-AC-02` and confirmed [A1-XX-01](../GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior) infrastructure-vs-entry-point gap.

---

## 5. Cross-cutting isolation and honesty checks

| Check | Result |
|-------|--------|
| Disposable profile preflight (both scenarios) | **Pass** — settings dir under `$XDG_CONFIG_HOME/zaide` |
| Real-user profile writes | **None** observed |
| Real workspace roots | **Not opened** |
| xdtools / screenshots / pointer automation | **Not used** |
| Internal `BindNativeHarness` / `BindAcpRuntime` / `SetBinding` | **Not invoked** |
| Production / tracked tests / packages / audit policy | **Unchanged** |
| Visual-only paint claims | Marked **UNVERIFIED-VIS** only |

---

## 6. What this slice does **not** claim

1. **A3 overall complete** — only `A1-AC-01` and `A1-AC-02`.
2. Successful agent send, assistant responses, tools, permissions, or admitted backend execution.
3. ACP process launch, Native Harness completion, or live authentication protocol behavior.
4. Capability snapshot user projection or reactive binding-caption refresh while the same DM stays active (A2 gaps; not re-tested here beyond initial pull).
5. Pixel paint of binding status coloring or settings overlay layout.
6. Positive bind/send outcomes on clean profile (correctly **blocked** — no user path).

---

## 7. Cleanup

After evidence capture:

- Removed `/tmp/zaide-a3-ac/` (runner, obj, out, evidence JSON copies used only for this note).
- Removed disposable `/tmp/zaide-a3-ac-profile-*` trees.
- No tracked tree changes except this evidence document.

---

## 8. Closeout checklist

| Check | Result |
|-------|--------|
| Evidence file created | **Yes** — this document |
| Only new evidence staged | **Yes** (closeout commit) |
| `git diff --check` | clean |
| Relative Markdown links | verified (see §9) |
| Commit message | `docs(audit): execute A3 agent onboarding smoke` |
| Push `master` → `origin` | performed at closeout |
| `HEAD == origin/master` + clean tree | re-verified at closeout |
| A3 overall complete? | **No** — explicitly incomplete |
| A4 / V4 / agent-send / permissions / trace / restart begun? | **No** |

---

## 9. Link and whitespace verification

Executed after writing this file:

```bash
git diff --no-index --check /dev/null \
  docs/audits/v1-v3-product-reality/evidence/A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md
```

Exit status **1** is expected (files differ); **no whitespace-diagnostic output**.

Relative links in this file resolve to existing paths:

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md)
- [A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)
- [GOAL_MATRIX.md §15](../GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior)

---

## 10. Next bounded A3 slice

**A3 remains incomplete.** This note does **not** begin:

- Agent send / response (`A1-AS-*`)
- Tools / permissions (`A1-TP-*`)
- Trace / memory / usage / termination / restart-recovery
- A4, stabilization, or V4

---

**A3 Agent Creation and Backend Onboarding (`A1-AC-01`, `A1-AC-02`): executed (product-runtime smoke).**

| id | Classification |
|----|----------------|
| `A1-AC-01` | **UNWIRED** (no user-reachable agent creation; Townhall DM ≠ creation) |
| `A1-AC-02` | **WORKS_WITH_FRICTION** (read-only unbound status + composed infrastructure; user bind/configure/auth/capability/unbind onboarding **absent**; LLM settings ≠ actor binding; positive bind **not fabricated**) |

**A3 as a whole: incomplete.**
