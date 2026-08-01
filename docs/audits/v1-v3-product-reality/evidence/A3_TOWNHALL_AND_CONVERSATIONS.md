# A3 Clean-Profile Smoke — Townhall and Conversations (`A1-TH-01`, `A1-TH-02`, `A1-TH-04`, `A1-TH-05`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 Townhall / conversations execution slice only** — rows
`A1-TH-01`, `A1-TH-02`, `A1-TH-04`, `A1-TH-05`.
**Evidence date:** 2026-08-01
**Repo head at run:** `fc506505a47d7a9e20ea7c0152431256b57cb65b`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (TH-01/02/04/05 only) |
| **A3 slice** | Townhall and Conversations (`A1-TH-01`, `A1-TH-02`, `A1-TH-04`, `A1-TH-05`) |
| **A3 as a whole** | **Incomplete** — agents, permissions, trace/memory/usage/termination, restart/recovery, residual journeys **not executed** in this note |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / desktop pointer automation / manual desktop | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| `Avalonia.Headless` added to tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` rewritten to “A3 complete” | **No** |
| Prior A2 / A3 evidence rewritten | **No** |
| Real user `~/.config/zaide` read or written by this run | **No** (disposable `HOME` + `XDG_*` only) |
| Real workspace roots opened | **No** (no folder open required for this slice) |
| Registry / unit tests used as A3 proof | **No** (explicitly forbidden) |
| Fake backends injected as product success | **No** |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A2_TOWNHALL_AND_CONVERSATIONS.md](./A2_TOWNHALL_AND_CONVERSATIONS.md)

**Out of scope for this slice (explicit):**

- Agent creation / backend onboarding / tools / multi-agent routing rows (`A1-AC-*`, `A1-AS-*`, `A1-TP-*`, `A1-MR-*`)
- Trace, memory, usage, termination, restart/recovery rows
- A4, stabilization, V4 planning
- Production code, tracked tests, package pins, audit policy edits
- Unit tests as A3 proof
- Pixel paint of chat rows, filter toggle chrome, unread dots (**UNVERIFIED-VIS** where noted)
- Fabricated successful backend execution without an eligible bound backend

---

## 1. Four-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-TH-01` | **WORKS** | Cold headless shell exposes Townhall with three seeded channels (`townhall-main`, `ai-status`, `codebase-refactor`). Channel send appends `UserChat` / Townhall `Chat` and clears draft. Channel switch appends destination `ChannelEvent` (“Switched to #…”) into the store and UI. All / Chat / Activity filters partition correctly (All=2, ChatOnly=1 Chat, ActivityOnly=1 non-Chat). |
| `A1-TH-02` | **WORKS** | People → Zaide Agent opens one private direct conversation (`direct:873fede0…`) for the unordered Human↔Zaide Agent pair; reopening finds the same `ConversationId`. Channel selection is cleared while the DM is active. Direct send under clean profile has **no** eligible backend binding; outcome recorded truthfully as backend-unavailable with **no** admitted `UserChat` (not fabricated). DM body is **not** mirrored into any public channel. Direct nav lists the single owning conversation. |
| `A1-TH-04` | **WORKS** | After headless show + layout, logical/visual tree (1068 nodes) contains `TownhallView` and editor surface (`EditorView` / `EditorTabBar` / `SearchBar`) and **no** banned Agent Panel chrome types/names (`AgentPanelView`, `AgentPanelChrome`, etc.). Residual `IAgentPanelHost` → `AgentPanelHost` remains a non-visual DI adapter. Townhall People roster is the user-facing DM entry (`Zaide Agent`). Pixel paint of shell proportions is **UNVERIFIED-VIS**. |
| `A1-TH-05` | **WORKS_WITH_FRICTION** | From a Zaide Agent DM, unknown `@Ghost hello…` appends `RoutingFailure:Unknown target` on the **source** and projects Townhall `AgentError` (“Routing failed: Unknown target”). No Ghost direct is created. Valid `@Alpha hello…` does **not** fabricate admitted success: Alpha has no backend binding; target Human↔Alpha direct is find-or-created empty; no source/target admitted chat for the routed body. **Blocked sub-path:** successful admitted routed-flow Townhall visibility requires an eligible bound backend (not present in clean disposable profile). |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

**Evidence-class distinction:**

| Class | Meaning in this slice |
|-------|------------------------|
| `product-runtime` | Production DI + `MainWindow` + Townhall ViewModel / commands / conversation store under headless |
| `store-truth` | Authoritative `IConversationStore` entry ownership and privacy partitions |
| `visual-only` | Pixel paint of chat rows, filter chrome, unread dots — **not claimed** (`UNVERIFIED-VIS`) |
| `blocked-backend` | Paths that need an eligible bound agent backend; recorded honestly, not fabricated |

---

## 2. Harness construction (temporary; deleted after evidence capture)

Recreated the H1-proven out-of-tree Avalonia.Headless **12.0.5** runner under `/tmp` (not tracked).

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-th/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-th/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Package | `Avalonia.Headless` **12.0.5** only |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Isolation | One disposable profile **per independent scenario process**; `HOME` + all absolute `XDG_*` set **before** production composition |
| Observation | `TownhallViewModel` commands/state, `IConversationStore`, shell logical/visual tree, backend binding probe |
| Not used | xdtools, OS screenshots, desktop pointer automation, manual UI, service replacements, unit tests as proof, real workspace roots |

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
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-th-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

dotnet "/tmp/zaide-a3-th/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario A1-TH-0N \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-th/evidence/A1-TH-0N.json" \
  --repo-head "fc506505a47d7a9e20ea7c0152431256b57cb65b"
```

### 2.4 Disposable profiles (final capture)

| Scenario | Profile root | Exit | Assertions |
|----------|--------------|------|------------|
| `A1-TH-01` | `/tmp/zaide-a3-th-profile-exj5GNAd` | **0** | 19/19 pass |
| `A1-TH-02` | `/tmp/zaide-a3-th-profile-MHvMOWrk` | **0** | 15/15 pass |
| `A1-TH-04` | `/tmp/zaide-a3-th-profile-6aoCXDIk` | **0** | 11/11 pass |
| `A1-TH-05` | `/tmp/zaide-a3-th-profile-gPR4z0yY` | **0** | 11/11 pass |

**Total:** 56 product-runtime assertions, all pass on final capture.

Filesystem artifacts under each profile remained only under `$XDG_CONFIG_HOME/zaide/conversations/` (`conversations.json` + `.lastknowngood`). Real-user `~/.config/zaide` was not written (mtime unchanged from prior days).

---

## 3. `A1-TH-01` — open Townhall, switch channels, send, activity, filters

### 3.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch; clean disposable profile | product-runtime |
| 2 | Observe seeded channels + default active `channel-1` | product-runtime |
| 3 | `DraftText` + `SendMessageCommand` on `townhall-main` | product-runtime + store-truth |
| 4 | `SelectChannelCommand("channel-2")` | product-runtime + store-truth |
| 5 | Return to `channel-1`; exercise `FilterMode` All / ChatOnly / ActivityOnly via `FilteredMessages` | product-runtime |

### 3.2 Observed results

| Check | Result |
|-------|--------|
| Seeded channels | `townhall-main` (pinned, active), `ai-status`, `codebase-refactor` (pinned) |
| Default active | `channel-1` / `channel:channel-1` |
| After send | UI `Chat\|User\|A3-TH-01 channel message smoke`; draft empty; store `UserChat` |
| After switch to ai-status | Destination UI + store `ChannelEvent:Switched to #ai-status` |
| After return to townhall-main | Chat + `ChannelEvent:Switched to #townhall-main` |
| Filter All | 2 |
| Filter ChatOnly | 1 × `Chat` |
| Filter ActivityOnly | 1 × `ChannelEvent` |
| Partition | chat + activity = all |

### 3.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-TH-01",
  "exitCode": 0,
  "isolation": {
    "profileRoot": "/tmp/zaide-a3-th-profile-exj5GNAd",
    "resolvedSettingsDir": "/tmp/zaide-a3-th-profile-exj5GNAd/config/zaide",
    "preflightOk": true
  },
  "observedViewModelState": {
    "channels.initial": [
      "channel-1:townhall-main:pinned=True:active=True",
      "channel-2:ai-status:pinned=False:active=False",
      "channel-3:codebase-refactor:pinned=True:active=False"
    ],
    "after_send.messages": ["Chat|User|A3-TH-01 channel message smoke"],
    "channel2.messages": ["ChannelEvent|User|Switched to #ai-status"],
    "channel1.after_return.messages": [
      "Chat|User|A3-TH-01 channel message smoke",
      "ChannelEvent|User|Switched to #townhall-main"
    ],
    "filter.All.count": 2,
    "filter.ChatOnly.count": 1,
    "filter.ActivityOnly.count": 1,
    "visual_filter_paint": "UNVERIFIED-VIS"
  },
  "classificationHint": "WORKS"
}
```

### 3.4 Classification rationale — **WORKS**

Channel open, send, switch activity, store authority, and All/Chat/Activity filter partition all observed through production Townhall composition. Filter **toggle paint** and chat row styling are **UNVERIFIED-VIS** (not required for functional classification).

---

## 4. `A1-TH-02` — direct conversation privacy and ownership

### 4.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch | product-runtime |
| 2 | `OpenDirectConversationCommand(CanonicalTownhallAgent)` | product-runtime + store-truth |
| 3 | Leave and reopen same pair (find-or-create) | store-truth |
| 4 | Probe backend binding; send DM body | product-runtime + blocked-backend |
| 5 | Assert no channel leak; single owning `ConversationId` | store-truth |

### 4.2 Observed results

| Check | Result |
|-------|--------|
| People roster | `User` (user) + `Zaide Agent` (agent) |
| Owning ConversationId | `direct:873fede0b640437e9e51a6d26281d84e` |
| Participants | `human:user-1`, `townhall-agent:agent-1` |
| Find-or-create | Second open returns **same** id |
| Active channel while DM open | `null`; all channel `IsActive=false` |
| Backend binding | **Unbound** — `InvalidOperationException: No explicit backend binding exists for this actor.` |
| Send outcome | **Truthful backend-unavailable**: 0 admitted entries; **no** fabricated `UserChat` |
| Channel leaks of DM body | **None** |
| Direct nav | `Zaide Agent` → same ConversationId |

### 4.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-TH-02",
  "exitCode": 0,
  "observedViewModelState": {
    "direct.owningConversationId": "direct:873fede0b640437e9e51a6d26281d84e",
    "direct.participants": ["human:user-1", "townhall-agent:agent-1"],
    "backend.bound": false,
    "backend.probe": "unbound:InvalidOperationException:No explicit backend binding exists for this actor.",
    "direct.send_outcome": "backend_unavailable_no_admitted_userchat",
    "direct.userchat_admitted": false,
    "privacy.channel_leaks": [],
    "directNav": ["direct:873fede0b640437e9e51a6d26281d84e:Zaide Agent"]
  },
  "classificationHint": "WORKS"
}
```

### 4.4 Classification rationale — **WORKS**

Phase 14 D02–D06 privacy and ownership hold at product runtime: one private direct, presentation selection, no public mirror, stable unordered-pair id. Agent response admission is **backend-dependent** and was recorded honestly as unavailable — not required to fabricate a successful assistant turn for this row’s privacy/ownership contract. DM chrome paint is **UNVERIFIED-VIS**.

---

## 5. `A1-TH-04` — Agent Panel chrome absent; Townhall sole DM entry

### 5.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch | product-runtime |
| 2 | Show `MainWindow`, update layout | product-runtime |
| 3 | Walk visual + logical + panel children | product-runtime |
| 4 | Assert banned Agent Panel chrome absent; Townhall + editor present | product-runtime |
| 5 | Confirm residual host is non-visual DI adapter; People has agent | product-runtime |

### 5.2 Observed results

| Check | Result |
|-------|--------|
| Tree nodes walked | 1068 |
| Banned Agent Panel types/names | **None** |
| Townhall present | `Zaide.Features.Townhall.Presentation.TownhallView` (+ chat/nav/people/input panels) |
| Editor right surface | `EditorView`, `EditorTabBar`, `SearchBar` |
| Residual DI host | `AgentPanelHost` (non-visual adapter — **not** chrome) |
| People DM entry | `Zaide Agent` |
| Note | `AgentBackendBindingPanel` appears as Townhall-adjacent status UI, **not** dedicated Agent Panel chrome |

### 5.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-TH-04",
  "exitCode": 0,
  "observedViewModelState": {
    "visual_tree.type_count": 1068,
    "agent_panel.banned_types_found": [],
    "agent_panel.banned_names_found": [],
    "layout.hasTownhallView": true,
    "layout.hasEditorSurface": true,
    "di.IAgentPanelHost": "Zaide.Features.Agents.Presentation.AgentPanelHost",
    "townhall.agents": ["User", "Zaide Agent"],
    "visual_pixel_paint": "UNVERIFIED-VIS"
  },
  "classificationHint": "WORKS"
}
```

### 5.4 Classification rationale — **WORKS**

Dedicated Agent Panel chrome is absent from the realized shell tree; Townhall is the sole user-facing direct-conversation entry observed. Residual non-visual host must not be mistaken for panel chrome (aligns with closed [DF-001](../../../deferred/closed/DF-001-agent-surface-townhall-tab.md)). Pixel proportions of columns are **UNVERIFIED-VIS**.

---

## 6. `A1-TH-05` — @mention routing visibility (valid + unknown)

### 6.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Open source DM with Zaide Agent | product-runtime |
| 2 | Send `@Ghost hello from A3-TH-05` | product-runtime + store-truth |
| 3 | Probe Alpha backend binding | blocked-backend |
| 4 | Send `@Alpha hello routed A3-TH-05` from source DM | product-runtime + blocked-backend |
| 5 | Compare source/target store entries and Townhall projection | store-truth |

### 6.2 Observed results

| Path | Source conversation | Target conversation | Townhall UI |
|------|---------------------|---------------------|-------------|
| Unknown `@Ghost` | `RoutingFailure:Unknown target` on source `direct:594a6724…` | No Ghost direct | Active source shows `AgentError\|Zaide Agent\|Routing failed: Unknown target` |
| Valid `@Alpha` (no backend) | Source keeps only prior `RoutingFailure` (no routed body chat) | Human↔Alpha `direct:12b2d080…` **created empty** (find-or-create via panel host); **0** admitted entries | **No** fabricated success on source or target |
| Alpha backend probe | — | — | `unbound: No explicit backend binding exists for this actor.` |

**Blocked sub-path (explicit):** successful admitted routed-flow visibility (UserChat/AssistantResponse/ExecutionFailure on target with Townhall projection) requires an eligible bound backend. Clean disposable profile has none; success was **not** fabricated.

### 6.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-TH-05",
  "exitCode": 0,
  "observedViewModelState": {
    "source.conversationId": "direct:594a672417b648d9a2782bf23c2b3f4c",
    "unknown.source_entries": ["RoutingFailure:Unknown target"],
    "unknown.ui_messages": ["AgentError|Zaide Agent|Routing failed: Unknown target"],
    "valid_alpha.backend_bound": false,
    "valid_alpha.backend_probe": "unbound:No explicit backend binding exists for this actor.",
    "valid_alpha.target_conversationId": "direct:12b2d08028144934bf52a3285f7c0ec6",
    "valid_alpha.target_entries": [],
    "valid_alpha.target_has_admitted_entries": false,
    "valid_alpha.source_shows_routed_body": false,
    "valid_alpha.outcome": "backend_unavailable_pre_admission_reject_no_store_entry_truthful",
    "valid_alpha.blocked_path": "Successful routed-flow Townhall visibility requires an eligible bound backend; not available in clean disposable profile."
  },
  "classificationHint": "WORKS_WITH_FRICTION"
}
```

### 6.4 Classification rationale — **WORKS_WITH_FRICTION**

Unknown-target routing failures are product-runtime proven on the source with Townhall `AgentError` projection. Valid mention **routing attempt** is executable and stays honest under unbound backend (target shell created empty; no fabricated admissions). Friction / blocked sub-path: **admitted successful routed-flow** Townhall visibility cannot be completed without an eligible backend bind path (belongs to agent/backend journeys; not invented here). Aligns with A2 TH-05 wired-with-gap notes on unbound/pre-admission invisibility without reopening MR/AS rows.

---

## 7. Cross-cutting isolation and honesty checks

| Check | Result |
|-------|--------|
| Disposable profile preflight (all four) | **Pass** — settings dir under `$XDG_CONFIG_HOME/zaide` |
| Real-user profile writes | **None** observed for this run |
| Real workspace roots | **Not opened** |
| xdtools / screenshots / pointer automation | **Not used** |
| Production / tracked tests / packages / audit policy | **Unchanged** |
| Fake backend as product success | **Not used** |
| Visual-only paint claims | Marked **UNVERIFIED-VIS** only |

---

## 8. What this slice does **not** claim

1. **A3 overall complete** — only TH-01/02/04/05.
2. Successful agent assistant responses, tools, permissions, multi-agent debate, or backend bind UI product success.
3. Restart/recovery of drafts, unread, or mid-run continuity (`A1-TC-*`).
4. Pixel-perfect Townhall chrome, filter toggle styling, unread badges, or scroll anchoring.
5. Custom channel creation UI (still absent; not required for TH-01 success conditions exercised here).
6. Catalog Alpha–Delta exposure in People (routing still resolves Alpha via catalog; People lists Zaide Agent only — consistent with A2).

---

## 9. Cleanup

After evidence capture:

- Removed `/tmp/zaide-a3-th/` (runner, obj, out, evidence JSON copies used only for this note).
- Removed disposable `/tmp/zaide-a3-th-profile-*` trees.
- No tracked tree changes except this evidence document.

---

## 10. Next bounded A3 slice

**A3 remains incomplete.** This note does **not** begin:

- Agents / creation / send / tools / permissions
- Multi-agent routing rows beyond TH-05 Townhall visibility
- Trace / memory / usage / termination
- Restart / recovery / context
- A4, stabilization, or V4

---

**A3 Townhall and Conversations (`A1-TH-01`, `A1-TH-02`, `A1-TH-04`, `A1-TH-05`): executed (product-runtime smoke).**

| id | Classification |
|----|----------------|
| `A1-TH-01` | **WORKS** |
| `A1-TH-02` | **WORKS** |
| `A1-TH-04` | **WORKS** |
| `A1-TH-05` | **WORKS_WITH_FRICTION** (successful admitted routed-flow sub-path **blocked** without eligible backend; not fabricated) |

**A3 as a whole: incomplete.**
