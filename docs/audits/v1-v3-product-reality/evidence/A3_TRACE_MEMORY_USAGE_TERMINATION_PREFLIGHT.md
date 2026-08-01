# A3 Trace / Memory / Usage / Termination Preflight — Absence Evidence (`A1-TC-02`, `A1-TC-03`, `A1-TC-08`, `A1-TC-09`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 trace/memory/usage/termination preflight only** —
absence and blocked-path evidence for rows `A1-TC-02`, `A1-TC-03`, `A1-TC-08`,
`A1-TC-09`.
**Evidence date:** 2026-08-01
**Repo head at run:** `1676fe069beb2cde54d4199fb07810d2ffd67da9`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime preflight evidence** (absence / blocked-path only) |
| **A3 slice** | Trace / memory / usage / termination preflight (`A1-TC-02`, `A1-TC-03`, `A1-TC-08`, `A1-TC-09`) |
| **A3 as a whole** | **Incomplete** — positive trace/memory/usage/termination smoke, restart/recovery, A4 **not executed** |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / desktop pointer / manual pointer | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| Package pins / audit policy modified | **No** |
| Internal bind APIs (`BindNativeHarness`, `BindAcpRuntime`, `SetBinding`) invoked | **No** |
| Fabricated traces, memories, usage records, or termination events | **No** |
| Fake backends / test doubles / external backend install | **No** |
| Real user `~/.config/zaide` used | **No** (disposable `HOME` + `XDG_*` only) |
| Repository tree used as workspace root | **No** |
| Prior A2 / A3 evidence rewritten | **No** |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§14)
- [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./A2_TRACE_MEMORY_USAGE_TERMINATION.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A3_AGENT_SEND.md](./A3_AGENT_SEND.md) (unbound send negative path)
- [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) (bind gap)

**Explicitly out of scope this run:**

- Positive trace inspect, memory CRUD, usage/cost with backend producer, explicit end-session success
- Restart/recovery, A4, stabilization, V4
- Production / tracked-test / package / policy edits

---

## 1. Classification (authoritative for this preflight)

| id / sub-path | Classification | Summary |
|---------------|----------------|---------|
| **`A1-TC-02` (parent)** | **Missing** | No user-reachable shell command, View, or Townhall gesture opens raw trace inspection. Registry (38 commands) has **zero** `trace.*` / `transparency.*` commands. Candidate `trace.inspect` / `agent.trace` IDs return `Execute=false`. Main-window visual tree has **no** Trace/Transparency nodes. Clean unbound profile: backend **Unbound**; `agents-durable` partition **absent**; trace inspection VM resolution hits **circular DI** on `AgentTraceCoordinator` when probed (no user path requests it). Redaction/retention not exercised. |
| Trace shell/command entry | **UNWIRED** | Zero trace-like registered commands; five candidate IDs unknown/unavailable. |
| Trace capture state (user path) | **UNREACHABLE** | No user entry to observe capture enabled/disabled caption. Pipeline probe: circular DI prevents `AgentTraceInspectionViewModel` resolution. |
| Redaction / retention runtime | **UNVERIFIED** | No user path triggers capture; not manufactured via internal APIs. |
| Trace inspection visual | **UNVERIFIED-VIS** | No trace View in tree; pixel/modal UX not claimed. |
| **`A1-TC-03` (parent)** | **Missing** | No user-reachable command or View for memory list/create/edit/delete/disable/scope/influence. Registry has **zero** memory-like commands. Visual tree: **no** Memory nodes. `AgentMemoryInspectionViewModel` resolves from DI (read-only probe) and reports **0** records / caption `No durable memory records` — empty store, **not** a management surface. No user path invoked CRUD APIs. |
| Memory management commands | **UNWIRED** | Zero `memory.*` / `agent.memory` commands; five candidate IDs unknown. |
| Memory CRUD via user path | **UNREACHABLE** | No command/UI; internal Create/Correct/Disable not invoked. |
| Scope / influence management | **UNREACHABLE** | No user entry; influence recording not exercised. |
| Memory management visual | **UNVERIFIED-VIS** | Not claimed. |
| **`A1-TC-08` (parent)** | **Missing** | No user-reachable usage/cost View or command. Registry: **zero** usage/cost commands. Visual tree: **no** Usage/Cost nodes. Read-only DI probe of `AgentUsageInspectionViewModel`: `capture_enabled=false`, caption **`Usage capture disabled.`** (unavailable pipeline — **not** a zero-cost billing fact). `total_cost_value=0` with capture disabled is **not** backend-reported cost. No backend producer on unbound profile. |
| Usage/cost user entry | **UNWIRED** | Zero usage/cost commands; four candidate IDs unknown. |
| Units / currency / pricing provenance (user path) | **UNREACHABLE** | No UI; `total_cost_currency=null`, `counts_by_origin={}`. |
| Missing-evidence semantics | **observed** | Disabled capture caption distinguishes unavailable from zero evidence (A2 contract). |
| Usage/cost visual | **UNVERIFIED-VIS** | Not claimed. |
| **`A1-TC-09` (parent)** | **BLOCKED** | Positive explicit termination of an active session/run is **not reachable**: no `session.end` / `agent.end` commands; Townhall exposes only send/navigation/context-policy commands (no End/Stop/Terminate); visual tree has **no** end/stop session controls. Unbound send produces **no** admitted run (`entry_delta=0`, coordinator not busy) — nothing to terminate. `IAgentSessionService.EndAsync` and continuity `Terminate` APIs exist per A2 but have **no** production View/command caller. Parent row is **not** upgraded to `WORKS`. |
| Terminate commands | **UNWIRED** | Zero terminate-like commands; eight candidate IDs unknown. (`project.cancel` exists for build workflow — **not** agent session cancel.) |
| Townhall end gesture | **UNWIRED** | Six reactive commands; none terminate-like. |
| Active session to end | **BLOCKED** | Unbound pre-admission reject; no in-flight run. |
| Terminal-state projection | **UNVERIFIED** | Cannot exercise without admitted run + end UI. |
| Termination visual | **UNVERIFIED-VIS** | Not claimed. |

Allowed classifications used: `Missing`, `BLOCKED`, `UNWIRED`, `UNREACHABLE`, `UNVERIFIED`, `UNVERIFIED-VIS`, plus **observed** for negative-path facts that do not upgrade parent rows.

Do **not** read this table as A3 trace/memory/usage/termination positive smoke complete.

---

## 2. Harness construction (temporary; deleted after capture)

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-tc/` (removed after capture) |
| Project | `/tmp/zaide-a3-tc/runner/Zaide.Tests.csproj` |
| Assembly | **`Zaide.Tests`** (`InternalsVisibleTo`) |
| TFM | `net10.0` |
| Packages | `Avalonia.Headless` **12.0.5**; `ReactiveUI.Avalonia` **12.0.3**; `ReactiveUI.Avalonia.Microsoft.Extensions.DependencyInjection` **12.0.3** (runner only) |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Observation | `ICommandRegistry`, `TownhallViewModel` commands, visual-tree scan, read-only inspection VM probes (no record manufacture), `IAgentExecutionCoordinator`, backend snapshot |
| Not used | xdtools, screenshots, pointer automation, bind injection, fake backends, unit tests as proof |

### 2.1 Isolation protocol

| Variable | Disposable value pattern |
|----------|--------------------------|
| `HOME` | `$PROFILE_ROOT/home` |
| `XDG_CONFIG_HOME` | `$PROFILE_ROOT/config` (absolute) |
| `XDG_DATA_HOME` | `$PROFILE_ROOT/data` |
| `XDG_STATE_HOME` | `$PROFILE_ROOT/state` |
| `XDG_CACHE_HOME` | `$PROFILE_ROOT/cache` |

Preflight: `SettingsPathResolver.GetSettingsDirectory()` under `$XDG_CONFIG_HOME/zaide`, not real-user `~/.config/zaide` (`preflight_ok: true` every scenario).

### 2.2 Disposable profiles (final capture)

| Scenario | Profile root | Exit | Assertions |
|----------|--------------|------|------------|
| `A1-TC-02` | `/tmp/zaide-a3-tc-profile-kN8L9uNR` | **0** | **3/3** pass |
| `A1-TC-03` | `/tmp/zaide-a3-tc-profile-awcXMVEK` | **0** | **2/2** pass |
| `A1-TC-08` | `/tmp/zaide-a3-tc-profile-bmb0ArP2` | **0** | **3/3** pass |
| `A1-TC-09` | `/tmp/zaide-a3-tc-profile-ABsN0Hyi` | **0** | **6/6** pass |

---

## 3. `A1-TC-02` — trace inspection absence

### 3.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch; clean disposable profile | product-runtime |
| 2 | Scan `ICommandRegistry` for trace/transparency-like IDs | product-runtime |
| 3 | `Execute` candidate trace command IDs | product-runtime |
| 4 | Scan `MainWindow` visual tree for Trace/Transparency nodes | product-runtime |
| 5 | `OpenDirectConversationCommand(CanonicalTownhallAgent)`; confirm **unbound** | blocked-backend |
| 6 | Probe filesystem `agents-durable` under disposable settings dir | store-truth |
| 7 | Read-only probe `AgentTraceInspectionViewModel` (no capture manufacture) | product-runtime |

### 3.2 Observed results

| Check | Result |
|-------|--------|
| Registry total commands | **38** |
| Trace-like commands | **0** |
| Transparency-like commands | **0** |
| Candidate `trace.inspect` / `agent.trace` / etc. | All **`Execute=false`** (unknown) |
| Visual Trace/Transparency nodes | **0** |
| Backend binding | **Unbound** |
| `agents-durable` directory | **Absent** |
| Trace partition files | **[]** |
| Trace inspection VM probe | **Circular DI** on `AgentTraceCoordinator` |
| Redaction/retention exercised | **No** |
| Backend-reported trace values | **Not claimed** |

### 3.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-TC-02",
  "exitCode": 0,
  "isolation": {
    "profileRoot": "/tmp/zaide-a3-tc-profile-kN8L9uNR",
    "resolvedSettingsDir": "/tmp/zaide-a3-tc-profile-kN8L9uNR/config/zaide",
    "preflightOk": true
  },
  "observed": {
    "command_registry.trace_like": [],
    "visual.trace_view_count": 0,
    "backend.bound": false,
    "filesystem.agents_durable_exists": false,
    "di.pipeline_projection_error": "InvalidOperationException: circular dependency AgentTraceCoordinator",
    "user_entry_point": "UNWIRED"
  },
  "assertions": "3/3 pass",
  "classificationHint": "Missing"
}
```

**Not claimed:** redacted trace content, retention enforcement, capture-state badges in UI, backend-produced trace records.

---

## 4. `A1-TC-03` — memory management absence

### 4.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Scan registry for memory-like commands | product-runtime |
| 2 | `Execute` candidate memory command IDs | product-runtime |
| 3 | Visual tree scan for Memory nodes | product-runtime |
| 4 | Open Zaide Agent DM; confirm unbound | blocked-backend |
| 5 | Read-only `AgentMemoryInspectionViewModel.LoadSummaryAsync` (no Create/Correct/Delete) | product-runtime |

### 4.2 Observed results

| Check | Result |
|-------|--------|
| Memory-like commands | **0** |
| Candidate memory IDs | All **`Execute=false`** |
| Visual Memory nodes | **0** |
| Memory inspection VM | **Resolvable** |
| Total / active records | **0 / 0** |
| Availability caption | `No durable memory records` |
| User CRUD attempted | **0** |
| Scope/influence UI | **Absent** |

### 4.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-TC-03",
  "exitCode": 0,
  "observed": {
    "command_registry.memory_like": [],
    "pipeline.memory.availability_caption": "No durable memory records",
    "pipeline.memory.total_records": 0,
    "memory_create_via_user_path": false,
    "user_entry_point": "UNWIRED"
  },
  "assertions": "2/2 pass",
  "classificationHint": "Missing"
}
```

**Not claimed:** memory create/edit/delete/disable, scope picker, influence inspection UI.

---

## 5. `A1-TC-08` — usage/cost visibility absence

### 5.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Scan registry for usage/cost commands | product-runtime |
| 2 | `Execute` candidate usage command IDs | product-runtime |
| 3 | Visual tree scan for Usage/Cost nodes | product-runtime |
| 4 | Open Zaide Agent DM; confirm unbound | blocked-backend |
| 5 | Read-only `AgentUsageInspectionViewModel` summary (no ledger manufacture) | product-runtime |

### 5.2 Observed results

| Check | Result |
|-------|--------|
| Usage-like commands | **0** |
| Cost/billing-like commands | **0** |
| Candidate usage IDs | All **`Execute=false`** |
| Visual Usage/Cost nodes | **0** |
| `capture_enabled` | **false** |
| Availability caption | **`Usage capture disabled.`** |
| `total_cost_value` / `total_cost_currency` | **0 / null** (with capture disabled — **not** verified billing) |
| `counts_by_origin` | **{}** |
| Backend-reported cost | **Not claimed** |

**Unavailable vs zero:** disabled capture caption is **unavailable evidence**, not “zero cost confirmed.”

### 5.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-TC-08",
  "exitCode": 0,
  "observed": {
    "command_registry.usage_like": [],
    "pipeline.usage.capture_enabled": false,
    "pipeline.usage.availability_caption": "Usage capture disabled.",
    "pipeline.usage.total_cost_currency": null,
    "backend_reported_values_claimed": false,
    "missing_evidence_semantics": "unavailable not zero"
  },
  "assertions": "3/3 pass",
  "classificationHint": "Missing"
}
```

**Not claimed:** token/time/cost from a real backend, currency units, pricing provenance UI.

---

## 6. `A1-TC-09` — explicit termination blocked

### 6.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Scan registry for session/agent terminate commands | product-runtime |
| 2 | `Execute` candidate terminate IDs | product-runtime |
| 3 | List `TownhallViewModel` reactive commands | product-runtime |
| 4 | Scan visual tree for End/Stop session buttons | product-runtime |
| 5 | Open DM; unbound send; observe coordinator/entry state | product-runtime + blocked-backend |

**Deterministic send body:** `A3-TC preflight deterministic send body — not a trace/memory/usage/terminate trigger`

### 6.2 Observed results

| Check | Result |
|-------|--------|
| Terminate-like commands | **0** |
| Agent/session cancel-like commands | **0** (`project.cancel` is build-only — excluded) |
| Candidate terminate IDs | All **`Execute=false`** |
| Townhall commands | `ClearContextPolicyOverrideCommand`, `OpenDirectConversationCommand`, `SelectChannelCommand`, `SelectConversationCommand`, `SendMessageCommand`, `SetContextPolicyFromSelectorCommand` |
| Terminate-like Townhall commands | **0** |
| End/Stop session controls in tree | **0** |
| Send `entry_delta` | **0** |
| Coordinator busy after send | **false** |
| Active session/run to terminate | **None** |
| `EndAsync` user path | **UNWIRED** |

### 6.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-TC-09",
  "exitCode": 0,
  "observed": {
    "command_registry.terminate_like": [],
    "townhall.terminate_like_commands": [],
    "send.entry_delta": 0,
    "send.coordinator_busy_after": false,
    "explicit_terminate_user_path": "UNWIRED",
    "terminal_state_projection_exercised": false
  },
  "assertions": "6/6 pass",
  "classificationHint": "BLOCKED"
}
```

**Not claimed:** terminal session event projection after user end, continuity terminate UX, in-flight cancellation chrome.

---

## 7. Positive-path blocker (explicit)

Completing positive `A1-TC-02`–`A1-TC-09` smoke requires:

1. A **genuine user-reachable** backend bind ([A3_AGENT_CREATION_AND_BACKEND_ONBOARDING](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) records no bind UI on clean profile).
2. Production shell surfaces (commands/Views) for trace inspect, memory manage, usage/cost, and explicit end — **all absent** per this preflight and [A2_TRACE_MEMORY_USAGE_TERMINATION](./A2_TRACE_MEMORY_USAGE_TERMINATION.md).

This preflight did **not** invoke internal bind APIs or manufacture trace/memory/usage/termination events.

---

## 8. Cross-cutting honesty checks

| Check | Result |
|-------|--------|
| Disposable profile preflight | **Pass** all scenarios |
| Real-user profile | **Not used** |
| Repo tree as workspace | **Not used** |
| Internal bind APIs | **Not invoked** |
| Manufactured transparency records | **No** |
| Backend-reported billing/trace values | **Not claimed** |
| Production / tests / packages / policy | **Unchanged** |

---

## 9. What this preflight does **not** claim

1. A3 trace/memory/usage/termination **positive** smoke complete.
2. Redaction, retention, or capture-state UI behavior (**UNVERIFIED** / **UNREACHABLE**).
3. Memory CRUD, scope, or influence management success.
4. Usage/cost with real backend producer or Zaide-verified billing.
5. Explicit end-session terminal projection on an admitted run.
6. Pixel-perfect transparency/termination chrome (**UNVERIFIED-VIS**).
7. Restart/recovery, A4, stabilization, V4 work.

---

## 10. Cleanup

After evidence capture:

- Removed `/tmp/zaide-a3-tc/` (runner, obj, out, evidence JSON working copies).
- Removed disposable `/tmp/zaide-a3-tc-profile-*`.
- No tracked tree changes except this evidence document.

---

## 11. Closeout checklist

| Check | Result |
|-------|--------|
| Evidence file created | **Yes** — this document |
| Commit scope | evidence file only (closeout) |
| `git diff --check` | see §12 |
| Relative Markdown links | see §12 |
| A3 overall complete? | **No** |
| A4 / V4 / restart begun? | **No** |

---

## 12. Link and whitespace verification

```bash
git diff --no-index --check /dev/null \
  docs/audits/v1-v3-product-reality/evidence/A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md
```

Relative links in this file resolve to existing paths:

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [GOAL_MATRIX.md §14](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery)
- [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./A2_TRACE_MEMORY_USAGE_TERMINATION.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md)
- [A3_AGENT_SEND.md](./A3_AGENT_SEND.md)
- [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)

---

## 13. Status line

**A3 Trace / Memory / Usage / Termination preflight (`A1-TC-02`, `A1-TC-03`, `A1-TC-08`, `A1-TC-09`): executed (absence / blocked-path only).**

| id | Preflight classification |
|----|--------------------------|
| `A1-TC-02` | **Missing** (no trace command/View; unbound; no durable trace partition; trace VM circular DI on probe) |
| `A1-TC-03` | **Missing** (no memory command/View; empty store caption only; no user CRUD path) |
| `A1-TC-08` | **Missing** (no usage/cost command/View; capture disabled — unavailable, not zero billing) |
| `A1-TC-09` | **BLOCKED** (no end UI/command; no admitted run to terminate on unbound profile) |

**A3 as a whole: incomplete.**

**A4 / V4 / restart/recovery: not authorized.**

---

*Recorded 2026-08-01. Out-of-tree Avalonia.Headless 12.0.5 product-runtime runner executed clean-profile trace/memory/usage/termination preflight under disposable XDG; no bind injection; temporary runner, profiles, and fixtures removed; no production edits.*
