# A3 Clean-Profile Smoke — Agent Send (`A1-AS-02`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 agent send execution slice only** — row `A1-AS-02`.
**Evidence date:** 2026-08-01
**Repo head at run:** `19cab9a1d64fcb07e52bdc81de74ca5bb0c2a393`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (`A1-AS-02` only) |
| **A3 slice** | Agent Send / Response / Failure Feedback (`A1-AS-02`) |
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
| External network / backend install / credential provisioning | **No** |
| `A1-AS-01` (historical Agent Panel send) | **Out of scope** — already classified **Missing** in A2; not re-executed here |
| `A1-AC-*`, `A1-TP-*`, `A1-MR-*`, `A1-TC-*` rows | **Not executed** (explicit) |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§11)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A2_AGENT_SEND.md](./A2_AGENT_SEND.md)

**Out of scope for this slice (explicit):**

- Agent Panel send path (`A1-AS-01`)
- Backend bind/configure UI (`A1-AC-02` positive path)
- Tools, permissions, multi-agent routing beyond incidental send wiring
- Trace, memory, usage, termination, restart/recovery
- A4, stabilization, V4 planning
- Production code, tracked tests, package pins, audit policy edits
- Positive assistant response or admitted backend run manufactured via `BindNativeHarness`, `BindAcpRuntime`, or internal bind APIs
- Pixel paint of chat bubbles, failure styling, or input chrome (**UNVERIFIED-VIS** where noted)

---

## 1. One-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-AS-02` | **WORKS_WITH_FRICTION** | People → Zaide Agent DM is user-reachable; production `SendMessageCommand` executes on a deterministic body under full production DI. Clean disposable profile is **unbound**; send triggers **pre-admission rejection** with **no** admitted `UserChat`, **no** assistant response, and **no** actionable send-failure in Townhall chat. Townhall `DraftText` clears; backend status shows **Unbound** / **Disconnected** (status chrome, not send-result feedback). Panel host retains `DraftInput` text after rejection. Persistence records the direct conversation shell with **empty** `entries` and empty `drafts`. Positive response/admitted-run outcomes **BLOCKED** without user-reachable backend binding. |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

**Evidence-class distinction:**

| Class | Meaning in this slice |
|-------|------------------------|
| `product-runtime` | Production DI + `MainWindow` + `TownhallViewModel.SendMessageCommand` under headless |
| `store-truth` | Authoritative `IConversationStore` entries and persistence JSON |
| `blocked-backend` | Unbound clean-profile path; positive send success not fabricated |
| `visual-only` | Chat bubble paint, error styling, input disable chrome — **not claimed** (`UNVERIFIED-VIS`) |

---

## 2. Harness construction (temporary; deleted after evidence capture)

Recreated the H1-proven out-of-tree Avalonia.Headless **12.0.5** runner under `/tmp` (not tracked).

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-as/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-as/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Packages | `Avalonia.Headless` **12.0.5**; `ReactiveUI.Avalonia` **12.0.3**; `ReactiveUI.Avalonia.Microsoft.Extensions.DependencyInjection` **12.0.3** (runner only) |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Isolation | One disposable profile per scenario process; `HOME` + all absolute `XDG_*` set **before** production composition |
| Observation | `OpenDirectConversationCommand`, `SendMessageCommand`, `TownhallViewModel` state, `IConversationStore`, `IAgentPanelHost`, `IAgentExecutionCoordinator`, read-only `GetSnapshot`, persistence paths |
| Not used | xdtools, OS screenshots, desktop pointer automation, manual UI, bind injection, unit tests as proof, real workspace roots |

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

### 2.3 Runner command

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-as-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

dotnet "/tmp/zaide-a3-as/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario A1-AS-02 \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-as/evidence/A1-AS-02.json" \
  --repo-head "19cab9a1d64fcb07e52bdc81de74ca5bb0c2a393"
```

### 2.4 Disposable profile (final capture)

| Scenario | Profile root | Exit | Assertions |
|----------|--------------|------|------------|
| `A1-AS-02` | `/tmp/zaide-a3-as-profile-nWoDDzUb` | **0** | 14/14 pass |

Filesystem artifacts under the profile remained only under `$XDG_CONFIG_HOME/zaide/conversations/` (`conversations.json` + `.lastknowngood`). No `settings.json` or `secrets.json` written.

---

## 3. `A1-AS-02` — direct conversation send on clean unbound profile

### 3.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch; clean disposable profile | product-runtime |
| 2 | `OpenDirectConversationCommand(CanonicalTownhallAgent)` — People → Zaide Agent | product-runtime |
| 3 | Probe backend binding via read-only `GetSnapshot` | blocked-backend |
| 4 | Set `DraftText` to deterministic body; execute `SendMessageCommand` | product-runtime |
| 5 | Capture draft, store entries, Townhall messages, panel/coordinator state, persistence | product-runtime + store-truth |
| 6 | Assert no fabricated assistant response or admitted backend run | store-truth + blocked-backend |

**Deterministic message:** `A3-AS-02 agent send smoke deterministic body`

### 3.2 Observed results

| Check | Result |
|-------|--------|
| People roster | `User`, `Zaide Agent` |
| Active conversation | `direct:9d85221d26b646d4a60c331f3586aaf7` |
| Participants | `human:user-1`, `townhall-agent:agent-1` |
| Active channel while DM open | `null` |
| Backend binding | **Unbound** — `No explicit backend binding exists for this actor.` |
| Backend status projection | `BackendBindingLabel=Unbound`, `BackendAuthStatusCaption=Disconnected`, status region visible |
| `SendMessageCommand` | **Completed** (`RanToCompletion`) |
| Draft before send | `A3-AS-02 agent send smoke deterministic body` |
| Townhall `DraftText` after send | **Empty** (cleared) |
| Store entries before/after | `[]` → `[]` (delta **0**) |
| Townhall `Messages` before/after | `[]` → `[]` (delta **0**) |
| Admitted `UserChat` | **No** |
| Assistant response | **No** (not fabricated) |
| Routing / execution failure entries | **No** |
| Townhall `AgentError` messages | **None** |
| Coordinator busy after send | **No** |
| Panel after send | Created (`panel_count` 0→1); `Status=Idle`, `IsBusy=false`; `DraftInput` still holds send body |
| Inferred outcome | `pre_admission_unbound_reject_draft_cleared_no_conversation_projection` |
| Positive path | **BLOCKED** — no user-reachable backend binding on clean profile |

### 3.3 Session / run state

| Surface | Observation |
|---------|-------------|
| `IAgentExecutionCoordinator.IsConversationBusy` | `false` before and after send |
| `TownhallViewModel.IsDirectSendBusy` | `false` after send |
| `TownhallViewModel.IsInputEnabled` | `true` after send |
| `AgentPanelState.Status` | `Idle` |
| `AgentPanelState.IsBusy` | `false` |
| Admitted backend run | **None** — no in-flight session/run state materialized |

Pre-admission rejection occurs before `IAgentSessionService` admission; no session events or admitted-run identifiers were produced.

### 3.4 Townhall projection and status/error feedback

| Feedback channel | Visible send-result feedback? |
|------------------|-------------------------------|
| Townhall chat (`Messages`) | **No** new rows |
| `AgentError` projection | **No** |
| `ExecutionFailure` / `RoutingFailure` store entries | **No** |
| Backend binding status chrome | **Yes** — pre-existing `Unbound` / `Disconnected` (not tied to this send attempt) |
| Panel `Status` / `IsBusy` | Idle / not busy — no error caption |

**Actionable send-failure feedback for this attempt: absent.** Aligns with A2 `Wired-with-gap` and [ISSUE-008](../../../issues/open/ISSUE-008-agent-response-not-showing.md) Attempt 1 hypothesis; A3 product-runtime confirms on clean disposable profile.

Chat bubble paint and error-row styling remain **UNVERIFIED-VIS**.

### 3.5 Persistence artifacts

| Path | Exists | Notes |
|------|--------|-------|
| `$XDG_CONFIG_HOME/zaide/conversations/conversations.json` | Yes (0420) | Direct conversation shell persisted; `entries: []`; `drafts: {}`; `activeConversationId` set to DM |
| `conversations.json.lastknowngood` | Yes (0420) | Mirror |
| `settings.json` | No | Not written by this slice |
| `secrets.json` | No | Not written by this slice |

Send body does **not** appear in persisted `entries`. Townhall draft cleared in UI; persisted `drafts` map is empty.

### 3.6 Machine-readable excerpt

```json
{
  "scenarioId": "A1-AS-02",
  "exitCode": 0,
  "isolation": {
    "profileRoot": "/tmp/zaide-a3-as-profile-nWoDDzUb",
    "resolvedSettingsDir": "/tmp/zaide-a3-as-profile-nWoDDzUb/config/zaide",
    "preflightOk": true
  },
  "observedViewModelState": {
    "active.conversationId": "direct:9d85221d26b646d4a60c331f3586aaf7",
    "backend.bound": false,
    "backend.probe": "unbound:No explicit backend binding exists for this actor.",
    "send.draft_before": "A3-AS-02 agent send smoke deterministic body",
    "after.draft": "",
    "after.entry_delta": 0,
    "after.message_delta": 0,
    "after.panel.draftInput": "A3-AS-02 agent send smoke deterministic body",
    "send.inferred_outcome": "pre_admission_unbound_reject_draft_cleared_no_conversation_projection",
    "send.userchat_admitted": false,
    "send.assistant_response_admitted": false,
    "send.actionable_failure_visible": false,
    "send.draft_cleared": true,
    "positive_path_blocked": "Successful assistant response or admitted backend run requires user-reachable backend binding; not present on clean disposable profile.",
    "visual_chat_paint": "UNVERIFIED-VIS"
  },
  "assertions": "14/14 pass",
  "classificationHint": "WORKS_WITH_FRICTION"
}
```

### 3.7 Classification rationale — **WORKS_WITH_FRICTION**

**What works:** The documented Townhall send entry point is user-reachable on a clean profile. Production `SendMessageCommand` runs through the real router/coordinator stack without test doubles or bind injection.

**Friction / gap (success condition unmet):** The row’s success condition requires **response or actionable failure visible**. On the default clean unbound profile, neither occurs: no admitted chat, no assistant turn, no `AgentError` / `ExecutionFailure` projection. Townhall draft clears silently. Backend status shows **Unbound** before and after — informative but not send-result feedback.

**Blocked positive path:** Successful assistant response or admitted-run terminal outcomes were **not** fabricated. Completing the positive path requires a genuine user-reachable backend binding ([A1-AC-02](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) records no bind UI on clean profile).

**A2 reconciliation:** Confirms A2 `Wired-with-gap` at product runtime for the default unbound negative path. Does **not** re-open `A1-AS-01`.

---

## 4. Cross-cutting isolation and honesty checks

| Check | Result |
|-------|--------|
| Disposable profile preflight | **Pass** — settings dir under `$XDG_CONFIG_HOME/zaide` |
| Real-user profile writes | **None** observed |
| Real workspace roots | **Not opened** |
| xdtools / screenshots / pointer automation | **Not used** |
| Production / tracked tests / packages / audit policy | **Unchanged** |
| Fake backend as product success | **Not used** |
| Internal bind APIs invoked | **No** |
| Visual-only paint claims | Marked **UNVERIFIED-VIS** only |

---

## 5. What this slice does **not** claim

1. **A3 overall complete** — only `A1-AS-02`.
2. **`A1-AS-01`** — historical Agent Panel send; out of scope (A2 **Missing**).
3. Successful assistant responses or admitted-run terminal outcomes on clean profile.
4. Backend bind/configure product success (`A1-AC-02`).
5. Tools, permissions, multi-agent routing, trace/memory/usage/termination, restart/recovery.
6. Pixel-perfect chat/failure chrome (**UNVERIFIED-VIS**).
7. External Native Harness / ACP process smoke.

---

## 6. Cleanup

After evidence capture:

- Removed `/tmp/zaide-a3-as/` (runner, obj, out, evidence JSON working copy).
- Removed disposable `/tmp/zaide-a3-as-profile-nWoDDzUb`.
- No tracked tree changes except this evidence document.

---

## 7. Closeout checklist

| Check | Result |
|-------|--------|
| Evidence file created | **Yes** — this document |
| Only new evidence staged | **Yes** (closeout commit) |
| `git diff --check` | clean |
| Relative Markdown links | verified (see §8) |
| Commit message | `docs(audit): execute A3 agent send smoke` |
| Push `master` → `origin` | performed at closeout |
| `HEAD == origin/master` + clean tree | re-verified at closeout |
| A3 overall complete? | **No** — explicitly incomplete |
| A4 / V4 / permissions / trace / restart begun? | **No** |

---

## 8. Link and whitespace verification

Executed after writing this file:

```bash
git diff --no-index --check /dev/null \
  docs/audits/v1-v3-product-reality/evidence/A3_AGENT_SEND.md
```

Exit status **1** is expected (files differ); **no whitespace-diagnostic output**.

Relative links in this file resolve to existing paths:

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [GOAL_MATRIX.md §11](../GOAL_MATRIX.md#11-agent-send--response--failure-feedback)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md)
- [A2_AGENT_SEND.md](./A2_AGENT_SEND.md)
- [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)
- [ISSUE-008](../../../issues/open/ISSUE-008-agent-response-not-showing.md)

---

## 9. Next bounded A3 slice

**A3 remains incomplete.** This note does **not** begin:

- Permissions (`A1-TP-*`)
- Trace / memory / usage / termination (`A1-TC-*` beyond incidental send observation)
- Restart / recovery / context
- A4, stabilization, or V4

---

**A3 Agent Send (`A1-AS-02`): executed (product-runtime smoke).**

| id | Classification |
|----|----------------|
| `A1-AS-02` | **WORKS_WITH_FRICTION** (send reachable; unbound pre-admission rejection honest; no response/actionable failure; positive path **BLOCKED** without bind UI) |

**A3 as a whole: incomplete.**
