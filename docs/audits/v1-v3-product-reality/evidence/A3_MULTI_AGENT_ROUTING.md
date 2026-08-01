# A3 Clean-Profile Smoke — Multi-Agent Routing (`A1-MR-01`, `A1-MR-03`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 multi-agent routing execution slice only** — rows `A1-MR-01`, `A1-MR-03`.
**Evidence date:** 2026-08-01
**Repo head at run:** `7cc605ce79e062317d324b453fb92a1cac8f962c`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (`A1-MR-01`, `A1-MR-03` only) |
| **A3 slice** | Multi-Agent Routing (`A1-MR-01`, `A1-MR-03`) |
| **A3 as a whole** | **Incomplete** — permissions, trace/memory/usage/termination, restart/recovery, residual journeys **not executed** in this note |
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
| Unauthorized catalog setup hooks (duplicate-name ambiguity) | **Not used** |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§13)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md)

**Out of scope for this slice (explicit):**

- Agent send rows beyond routing (`A1-AS-*` not re-verdicted)
- Backend bind/configure UI (`A1-AC-02` positive path)
- Tools, permissions, trace/memory/usage/termination, restart/recovery
- Duplicate-name ambiguity (`@Twin`) without authorized setup hook
- Fabricated successful assistant output or admitted backend run
- Channel `@mention` routing (not claimed)
- A4, stabilization, V4 planning
- Production code, tracked tests, package pins, audit policy edits
- Pixel paint of chat rows or error styling (**UNVERIFIED-VIS** where noted)

---

## 1. Two-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-MR-01` | **Missing** | Historical Phase 6 panel-bound entry point is absent: no Agent Panel chrome in the headless shell tree, no Add/Create Panel command in the production command registry, no panel send surface or `SendAgentMessage` seam on `MainWindowViewModel`. Retained `IAgentPanelHost` / `AgentPanelHost` is a non-visual execution adapter only — not counted as a user-facing panel. |
| `A1-MR-03` | **WORKS_WITH_FRICTION** | Townhall People → Zaide Agent DM is the reachable entry; negative `@mention` cases from the active direct conversation append `RoutingFailure` on the **source** `ConversationId` and project Townhall `AgentError`; drafts clear. Canonical `@Alpha hello…` reaches catalog resolution and creates an empty Human↔Alpha target direct shell but hits **pre-admission unbound rejection** with no admitted store entries (honest). Townhall **channel** sends bypass `IAgentRouter` and log plain `UserChat` — channel `@mention` routing is **not** claimed. Gap vs goal-matrix “from any conversation” wording remains. |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`, `Missing`.

**Evidence-class distinction:**

| Class | Meaning in this slice |
|-------|------------------------|
| `product-runtime` | Production DI + `MainWindow` + Townhall commands under headless |
| `store-truth` | Authoritative `IConversationStore` entries and routing ownership |
| `blocked-backend` | Unbound clean-profile path; positive routed execution not fabricated |
| `visual-only` | Chat bubble / error-row paint — **not claimed** (`UNVERIFIED-VIS`) |

**Scoped observation (not a user-goal verdict):** `A1-XX-02` debate/disagreement surface remains **confirmed absent** in the running shell — consistent with [A2_MULTI_AGENT_ROUTING §13](./A2_MULTI_AGENT_ROUTING.md#13-a1-xx-02-scoped-disposition).

---

## 2. Harness construction (temporary; deleted after evidence capture)

Recreated the H1-proven out-of-tree Avalonia.Headless **12.0.5** runner under `/tmp` (not tracked).

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-mr/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-mr/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Packages | `Avalonia.Headless` **12.0.5**; `ReactiveUI.Avalonia` **12.0.3**; `ReactiveUI.Avalonia.Microsoft.Extensions.DependencyInjection` **12.0.3** (runner only) |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Isolation | One disposable profile **per scenario process**; `HOME` + all absolute `XDG_*` set **before** production composition |
| Observation | `OpenDirectConversationCommand`, `SendMessageCommand`, `SelectChannelCommand`, `IConversationStore`, `IAgentPanelHost`, command registry, shell visual tree |
| Not used | xdtools, OS screenshots, desktop pointer automation, manual UI, bind injection, fake backends, internal binding calls, unauthorized catalog hooks, real workspace roots |

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
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-mr-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

dotnet "/tmp/zaide-a3-mr/runner/bin/Release/net10.0/Zaide.Tests.dll" \
  --scenario A1-MR-0N \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-mr/evidence/A1-MR-0N.json" \
  --repo-head "7cc605ce79e062317d324b453fb92a1cac8f962c"
```

### 2.4 Disposable profiles (final capture)

| Scenario | Profile root | Exit | Assertions |
|----------|--------------|------|------------|
| `A1-MR-01` | `/tmp/zaide-a3-mr-profile-N5xzhwqC` | **0** | 7/7 pass |
| `A1-MR-03` | `/tmp/zaide-a3-mr-profile-1flqhMMM` | **0** | 25/25 pass |

**Total:** 32 product-runtime assertions, all pass on final capture.

---

## 3. `A1-MR-01` — historical panel-bound entry point absent

### 3.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch; clean disposable profile | product-runtime |
| 2 | Enumerate production `ICommandRegistry` for panel create/add and panel send commands | product-runtime |
| 3 | Reflect `MainWindowViewModel` for `Send*Agent*` methods | product-runtime |
| 4 | Confirm `IAgentPanelHost` resolves to non-visual `AgentPanelHost` | product-runtime |
| 5 | Walk shell visual tree for banned Agent Panel chrome and panel send buttons | product-runtime |

### 3.2 Observed results

| Check | Result |
|-------|--------|
| Command registry size | 38 commands |
| Panel create/add commands | **None** (`panel_create.commands: []`) |
| Panel send commands | **None** (`panel_send.commands: []`) |
| `MainWindowViewModel` send-agent methods | **None** |
| `IAgentPanelHost` type | `AgentPanelHost` — **not** a `Control` (`panel_host.is_visual: false`) |
| Visual tree nodes walked | 305 |
| Banned Agent Panel types (`AgentPanelView`, `AgentPanelChrome`, `AgentPanelHostView`, `AgentPanelHost`) | **None** |
| Panel send buttons in Agent Panel paths | **None** |
| Townhall present | `TownhallView` in tree |
| Editor surface present | `EditorView` in tree |

### 3.3 Classification rationale — **Missing**

The Phase 6 user entry (“send `@alpha hello` from a panel”) has no production UI seam. Dedicated Agent Panel chrome and panel-bound send are retired. The retained `AgentPanelHost` is an execution adapter for Townhall direct send and catalog routing — it must not be mistaken for the historical user-facing panel. Reconciles A2 **Missing** at product runtime.

Chat/error chrome paint is **UNVERIFIED-VIS**.

---

## 4. `A1-MR-03` — catalog routing from Townhall direct conversation

### 4.1 Entry path

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch | product-runtime |
| 2 | `OpenDirectConversationCommand(CanonicalTownhallAgent)` — People → Zaide Agent | product-runtime |

| Check | Result |
|-------|--------|
| People roster | `User`, `Zaide Agent` |
| Source `ConversationId` | `direct:2bdf397c2610441ea3e2cb1830d7a272` |
| Catalog roster (`ListAgents`) | `Alpha`, `Beta`, `Delta`, `Gamma`, `Zaide Agent` |

### 4.2 Negative routing cases (active direct conversation)

Each case was sent sequentially from the same source DM. After four failures the source conversation held four cumulative `RoutingFailure` entries (one per case).

| Input | Source `ConversationId` | `RoutingFailure` entry | Townhall `AgentError` projection | Draft after send | Target direct changes |
|-------|-------------------------|------------------------|----------------------------------|------------------|------------------------|
| `@Ghost hello` | `direct:2bdf397c2610441ea3e2cb1830d7a272` | `RoutingFailure:Unknown target` | `AgentError\|Zaide Agent\|Routing failed: Unknown target` | **cleared** | No new directs; panel host 0→1 (source thin panel created on first send) |
| `@Alpha @Beta` | same | `RoutingFailure:Multiple mentions` | `AgentError\|…\|Routing failed: Multiple mentions` | **cleared** | No new directs with admitted entries |
| `@Alpha` | same | `RoutingFailure:Empty content after stripping` | `AgentError\|…\|Routing failed: Empty content after stripping` | **cleared** | No new directs with admitted entries |
| `@` | same | `RoutingFailure:Empty mention target` | `AgentError\|…\|Routing failed: Empty mention target` | **cleared** | No new directs with admitted entries |

All negative cases: `draftCleared: true`; no fabricated target execution; channel conversations unchanged.

### 4.3 Canonical `@Alpha` route — backend boundary (honest)

| Check | Result |
|-------|--------|
| Input | `@Alpha hello routed A3-MR-03` |
| Source `ConversationId` | `direct:2bdf397c2610441ea3e2cb1830d7a272` |
| Alpha `ActorId` | `panel-seed:alpha` |
| Backend binding | **Unbound** — `No explicit backend binding exists for this actor.` |
| Target direct created | `direct:f57a67d2bf79475eab3d300eb3bb7a34` (empty shell via get-or-create panel host) |
| Admitted `UserChat` / `AssistantResponse` on source or target | **No** |
| Townhall draft after send | **cleared** |
| Inferred outcome | `pre_admission_unbound_reject_no_admitted_store_entry_truthful` |
| Assistant output fabricated | **No** |

Thin panel host after route: two panels — source Zaide Agent DM and target Alpha DM (`panelsAfter` in machine-readable excerpt).

**Blocked sub-path:** successful admitted routed-flow visibility (UserChat/assistant on target with Townhall projection) requires an eligible bound backend; not present on clean disposable profile and **not fabricated**.

### 4.4 Channel send bypasses router

| Check | Result |
|-------|--------|
| Active channel | `channel-1` (`townhall-main`) |
| Input | `@Ghost hello channel bypass A3-MR-03` |
| Channel conversation | `channel:channel-1` |
| New channel entries | `UserChat:@Ghost hello channel bypass A3-MR-03` |
| New source `RoutingFailure` | **None** |
| Router bypassed | **Yes** — plain channel chat log, not `IAgentRouter` |
| Channel `@mention` routing claimed | **No** (explicit) |
| Draft after send | **cleared** |

### 4.5 Duplicate-name ambiguity

**Not exercised.** `ActorCatalog` duplicate-display-name ambiguity requires an authorized setup hook; none was provided. Per [A2_MULTI_AGENT_ROUTING §15.4](./A2_MULTI_AGENT_ROUTING.md#15-a3-clean-profile-smoke-constraints-for-this-journey).

### 4.6 Classification rationale — **WORKS_WITH_FRICTION**

**What works at product runtime:** Townhall People → agent DM is the real entry; `SendMessageCommand` drives production `IAgentRouter` from the active direct conversation; negative parser/router failures record on the source and project `AgentError`; catalog `@Alpha` resolution creates the target execution shell without an open panel tab.

**Friction / gaps:**

1. Goal-matrix scope “from **any** conversation” is not satisfied — channel sends bypass the router (confirmed above).
2. Positive admitted routed execution and assistant output remain **blocked** on the default unbound clean profile (honest pre-admission rejection).
3. Successful routed-request visibility on the source conversation for admitted runs was not exercised (blocked sub-path).

Reconciles A2 **Wired-with-gap** without reopening `A1-AS-*` or `A1-TH-*` verdicts. Chat/error row styling is **UNVERIFIED-VIS**.

### 4.7 Machine-readable excerpt (`A1-MR-03`)

```json
{
  "scenarioId": "A1-MR-03",
  "exitCode": 0,
  "source.conversationId": "direct:2bdf397c2610441ea3e2cb1830d7a272",
  "negative_routing_cases": [
    {
      "input": "@Ghost hello",
      "routingFailureEntries": ["RoutingFailure:Unknown target"],
      "townhallAgentErrorProjection": ["AgentError|Zaide Agent|Routing failed: Unknown target"],
      "draftCleared": true
    },
    {
      "input": "@Alpha @Beta",
      "routingFailureEntries": ["RoutingFailure:Multiple mentions"],
      "draftCleared": true
    },
    {
      "input": "@Alpha",
      "routingFailureEntries": ["RoutingFailure:Empty content after stripping"],
      "draftCleared": true
    },
    {
      "input": "@",
      "routingFailureEntries": ["RoutingFailure:Empty mention target"],
      "draftCleared": true
    }
  ],
  "canonical_alpha_route": {
    "backend.bound": false,
    "targetConversationId": "direct:f57a67d2bf79475eab3d300eb3bb7a34",
    "targetHasAdmittedEntries": false,
    "outcome": "pre_admission_unbound_reject_no_admitted_store_entry_truthful"
  },
  "channel_bypass": {
    "newChannelEntries": ["UserChat:@Ghost hello channel bypass A3-MR-03"],
    "routerBypassed": true,
    "channelMentionRoutingClaimed": false
  },
  "assertions": "25/25 pass",
  "classificationHint": "WORKS_WITH_FRICTION"
}
```

---

## 5. Cross-cutting isolation and honesty checks

| Check | Result |
|-------|--------|
| Disposable profile preflight (both scenarios) | **Pass** — settings dir under `$XDG_CONFIG_HOME/zaide` |
| Real-user profile writes | **None** observed |
| Real workspace roots | **Not opened** |
| xdtools / screenshots / pointer automation | **Not used** |
| Production / tracked tests / packages / audit policy | **Unchanged** |
| Fake backend as product success | **Not used** |
| Internal bind APIs invoked | **No** |
| Unauthorized catalog setup hooks | **No** |
| Visual-only paint claims | Marked **UNVERIFIED-VIS** only |

---

## 6. What this slice does **not** claim

1. **A3 overall complete** — only `A1-MR-01` and `A1-MR-03`.
2. Historical panel-bound routing as wired (`A1-MR-01` is **Missing**).
3. Channel `@mention` routing or “from any conversation” full scope.
4. Duplicate-name ambiguity routing without authorized hook.
5. Successful assistant responses or admitted-run terminal outcomes on clean profile.
6. Backend bind/configure product success (`A1-AC-02`).
7. Tools, permissions, trace/memory/usage/termination, restart/recovery.
8. Pixel-perfect chat/failure chrome (**UNVERIFIED-VIS**).
9. `A1-XX-02` as a user-goal verdict (scoped absence observation only).

---

## 7. Cleanup

After evidence capture:

- Removed `/tmp/zaide-a3-mr/` (runner, obj, bin, evidence JSON working copies).
- Removed disposable `/tmp/zaide-a3-mr-profile-N5xzhwqC` and `/tmp/zaide-a3-mr-profile-1flqhMMM`.
- No tracked tree changes except this evidence document.

---

## 8. Closeout checklist

| Check | Result |
|-------|--------|
| Evidence file created | **Yes** — this document |
| Only new evidence staged | **Yes** (closeout commit) |
| `git diff --check` | clean (see §9) |
| Relative Markdown links | verified (see §9) |
| Commit message | `docs(audit): execute A3 multi-agent routing smoke` |
| Push `master` → `origin` | performed at closeout |
| `HEAD == origin/master` + clean tree | re-verified at closeout |
| A3 overall complete? | **No** — explicitly incomplete |
| A4 / V4 / stabilization begun? | **No** |

---

## 9. Link and whitespace verification

Executed after writing this file:

```bash
git diff --no-index --check /dev/null \
  docs/audits/v1-v3-product-reality/evidence/A3_MULTI_AGENT_ROUTING.md
```

Exit status **1** is expected (files differ); **no whitespace-diagnostic output**.

Relative links in this file resolve to existing paths:

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [GOAL_MATRIX.md §13](../GOAL_MATRIX.md#13-multi-agent-routing)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md)
- [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md)
- [A2_MULTI_AGENT_ROUTING §13](./A2_MULTI_AGENT_ROUTING.md#13-a1-xx-02-scoped-disposition)
- [A2_MULTI_AGENT_ROUTING §15.4](./A2_MULTI_AGENT_ROUTING.md#15-a3-clean-profile-smoke-constraints-for-this-journey)

---

## 10. Next bounded A3 slice

**A3 remains incomplete.** This note does **not** begin:

- Permissions (`A1-TP-*`)
- Trace / memory / usage / termination (`A1-TC-*` beyond incidental observation)
- Restart / recovery / context
- A4, stabilization, or V4

---

**A3 Multi-Agent Routing (`A1-MR-01`, `A1-MR-03`): executed (product-runtime smoke).**

| id | Classification |
|----|----------------|
| `A1-MR-01` | **Missing** (historical panel-bound entry absent; non-visual host retained) |
| `A1-MR-03` | **WORKS_WITH_FRICTION** (direct-conversation catalog routing + negative failures proven; channel bypass gap; positive admitted route **blocked** without bind) |

**A3 as a whole: incomplete.**
