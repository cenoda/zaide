# A3 Clean-Profile Smoke — Live IDE Context Policy (`A1-TC-01`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 context-policy execution slice only** — row `A1-TC-01`.
**Evidence date:** 2026-08-01
**Repo head at run:** `8a21b43b44182d0694eece5a5057a89201d5ab0e`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (`A1-TC-01` only) |
| **A3 slice** | Live IDE context policy (`A1-TC-01`) |
| **A3 as a whole** | **Incomplete** — trace/memory/usage/termination positive paths, residual journeys, A4 **not executed** in this note |
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
| Fake backends / internal bind injection / internal context assembly calls | **No** |
| Registry / unit tests used as A3 proof | **No** |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§14)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md)
- [A3_AGENT_SEND.md](./A3_AGENT_SEND.md) (unbound send baseline)
- [A3_TOWNHALL_AND_CONVERSATIONS.md](./A3_TOWNHALL_AND_CONVERSATIONS.md) (direct DM entry)

**Out of scope for this slice (explicit):**

- `A1-TC-04`, `A1-TC-05` restart/recovery rows (covered in [A3_RESTART_AND_RECOVERY.md](./A3_RESTART_AND_RECOVERY.md))
- Trace, memory, usage, termination positive paths
- Backend bind/configure UI positive path
- A4, stabilization, V4 planning
- Production code, tracked tests, package pins, audit policy edits
- Positive admitted-run context manifest observation via fake backends or internal assembly APIs
- Pixel paint of selector chrome (**UNVERIFIED-VIS** where noted)

---

## 1. One-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-TC-01` | **WORKS_WITH_FRICTION** | People → Zaide Agent DM exposes the genuine `TownhallContextPolicySelector` (Application default / Off / Minimal / Standard / Detailed + clear override). Production `SetContextPolicyFromSelectorCommand` and `ClearContextPolicyOverrideCommand` update `IAgentContextSessionPolicyService` state and ViewModel projection with correct effective levels and captions. Session overrides are **in-memory only** — lost on restart (returns to hardcoded application default `Standard`). Clean unbound profile: send with Off policy triggers **pre-admission rejection** before context assembly; zero-item/zero-token manifest on send path is **BLOCKED** without eligible backend binding. No settings entry for context policy. Item/token/truncation/redaction disclosure not projected without admitted run. |

Allowed classifications only: `WORKS`, `WORKS_WITH_FRICTION`, `UNDISCOVERABLE`, `UNWIRED`, `BROKEN`, `TEST_ONLY`, `DOCS_ONLY`, `UNVERIFIED`, `BLOCKED`, `UNVERIFIED-VIS`.

**Evidence-class distinction:**

| Class | Meaning in this slice |
|-------|------------------------|
| `product-runtime` | Production DI + `MainWindow` + Townhall policy commands under headless |
| `store-truth` | `IAgentContextSessionPolicyService` state; persistence JSON absence of policy fields |
| `blocked-backend` | Context assembly / manifest / nav disclosure on send requires bound backend |
| `visual-only` | Selector combo/caption paint — **not claimed** (`UNVERIFIED-VIS`) |

---

## 2. Harness construction (temporary; deleted after evidence capture)

Recreated the H1-proven out-of-tree Avalonia.Headless **12.0.5** runner under `/tmp` (not tracked).

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-cp/` (removed after evidence capture) |
| Project | `/tmp/zaide-a3-cp/runner/Zaide.Tests.csproj` |
| Assembly name | **`Zaide.Tests`** (`InternalsVisibleTo` without production edits) |
| TFM | `net10.0` |
| Packages | `Avalonia.Headless` **12.0.5**; `ReactiveUI.Avalonia` **12.0.3**; `ReactiveUI.Avalonia.Microsoft.Extensions.DependencyInjection` **12.0.3** (runner only) |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Audit entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Process CWD | `/tmp/zaide-a3-cp-work` (not Zaide repo) |
| Isolation | One disposable profile per independent scenario group; **separate OS processes** for restart seed/verify pair |
| Observation | `OpenDirectConversationCommand`, `SetContextPolicyFromSelectorCommand`, `ClearContextPolicyOverrideCommand`, `IAgentContextSessionPolicyService`, visual-tree `TownhallContextPolicySelector`, `SendMessageCommand` (Off probe only) |
| Not used | xdtools, OS screenshots, desktop pointer automation, manual UI, bind injection, internal `AssembleContextManifest` calls, unit tests as proof |

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
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-cp-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

cd /tmp/zaide-a3-cp-work
dotnet "/tmp/zaide-a3-cp/runner/bin/Release/net10.0/Zaide.Tests.dll" \
  --scenario <id> \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-cp/evidence/<id>.json" \
  --repo-head "8a21b43b44182d0694eece5a5057a89201d5ab0e"
```

### 2.4 Disposable profiles (final capture)

| Scenario | Profile root | Processes | Assertions (pass/total) | Exit |
|----------|--------------|-----------|-------------------------|------|
| `A1-TC-01` | `/tmp/zaide-a3-cp-profile-main-krmGeULU` | 1 | 33/33 | **0** |
| `A1-TC-01-restart` | `/tmp/zaide-a3-cp-profile-restart-C3HMqVjo` | seed + verify | 3/3 + 6/6 | **0** / **0** |

**Total:** 42 product-runtime assertions, all pass on final capture.

---

## 3. `A1-TC-01` — context policy selector and session override

### 3.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch; clean disposable profile | product-runtime |
| 2 | `OpenDirectConversationCommand(CanonicalTownhallAgent)` — People → Zaide Agent | product-runtime |
| 3 | Locate `TownhallContextPolicySelector` in visual tree | product-runtime |
| 4 | Exercise selector indices 1–4 (Off / Minimal / Standard / Detailed) via `SetContextPolicyFromSelectorCommand` | product-runtime |
| 5 | Exercise `ClearContextPolicyOverrideCommand` and selector index 0 (application default) | product-runtime |
| 6 | Set Off; attempt `SendMessageCommand` on unbound profile | product-runtime + blocked-backend |
| 7 | Inspect persistence artifacts for policy override fields | store-truth |
| 8 | Separate profile: seed Detailed override → graceful shutdown → second process verify | product-runtime + store-truth |

### 3.2 Control location and initial state

| Check | Result |
|-------|--------|
| Direct conversation id | `direct:15c420f4d6bf4e9f8497768db09b7ed0` |
| `TownhallContextPolicySelector` in visual tree | **Yes** (`selector.found=true`) |
| `IsContextPolicySelectorVisible` | **true** on direct DM |
| Automation name (source) | `Agent context policy` |
| Application default (initial) | `Standard`; selector index **0**; caption `Application default (Standard)` |
| Override active (initial) | **false** |
| Backend binding | **Unbound** — `No explicit backend binding exists for this actor.` |
| Settings context-policy entry | **Absent** (`settings.json` not written on clean profile) |

### 3.3 Policy exercise results

| Selector index | Label | Effective level | Override active | Status caption | Nav disclosure | Panel disclosure |
|----------------|-------|-----------------|-----------------|----------------|----------------|------------------|
| 0 (initial) | Application default | Standard | false | `Application default (Standard)` | n/a | n/a |
| 1 | Off | Off | true | `Off (session override)` | empty | empty |
| 2 | Minimal | Minimal | true | `Minimal (session override)` | empty | empty |
| 3 | Standard | Standard | true | `Standard (session override)` | empty | empty |
| 4 | Detailed | Detailed | true | `Detailed (session override)` | empty | empty |
| Clear button | — | Standard | false | `Application default (Standard)` | n/a | n/a |
| Index 0 after Minimal set | Application default | Standard | false | `Application default (Standard)` | n/a | n/a |

**Service parity:** `IAgentContextSessionPolicyService.GetPolicyState` matched ViewModel projection for every step (effective level, override flag, caption).

**Disclosure / item / token / truncation:** Nav `ContextDisclosureStatus` and panel `ContextDisclosureStatus` remained **empty** for all policy levels because no admitted run occurred. Item count, token count, and truncation/redaction indicators are **not projected** to the user without an admitted backend run — recorded as friction, not fabricated.

### 3.4 Off policy + send on unbound profile

| Check | Result |
|-------|--------|
| Effective policy before send | **Off** |
| Send body | `A3-TC-01 context policy off send probe` |
| Store entries before/after | `0` → `0` (delta **0**) |
| `DraftText` after send | **Empty** (cleared — same pre-admission behavior as [A3_AGENT_SEND](./A3_AGENT_SEND.md)) |
| Context assembly reached | **No** — coordinator rejects before `AgentSessionService.SendAsync` admission |
| Zero-item/zero-token manifest on send path | **BLOCKED** — production path does not reach context assembly on unbound clean profile |
| Nav disclosure after send | **Empty** |
| Backend received context | **Not claimed** — unbound pre-admission rejection |

**Honest scope note:** A2 source inspection proves Off yields a valid zero-item/zero-token manifest when assembly runs ([A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md) §5.1). This A3 slice does **not** claim runtime manifest verification on send because the clean disposable profile has no eligible backend binding.

### 3.5 Persistence artifacts

| Path | Exists | Policy override stored? |
|------|--------|-------------------------|
| `$XDG_CONFIG_HOME/zaide/settings.json` | **No** | n/a |
| `$XDG_CONFIG_HOME/zaide/conversations/conversations.json` | **Yes** | **No** (`conversations_contains_policy_override=false`) |
| `$XDG_CONFIG_HOME/zaide/conversations/conversations.json.lastknowngood` | Written on debounced save | **No** policy fields |

Session context-policy overrides are **not** serialized to conversation snapshot or settings.

### 3.6 Restart persistence (separate processes, shared profile)

| Phase | Process | Observation |
|-------|---------|-------------|
| Seed | Process A | Set **Detailed** override; `selector_index=4`; caption `Detailed (session override)`; conversation `direct:e095b2ae309a45088b22147e4871f711`; graceful shutdown after 350 ms debounce wait |
| Verify | Process B | Same profile; reopen DM; `effective_level=Standard`; `is_override_active=false`; `selector_index=0`; caption `Application default (Standard)`; `override_persisted=false` |

**Verdict:** Session override does **not** persist across restart. Application returns to hardcoded default `Standard`. Conversation shell may persist; policy override does not.

### 3.7 Machine-readable excerpt (main scenario)

```json
{
  "scenarioId": "A1-TC-01",
  "exitCode": 0,
  "observedViewModelState": {
    "direct.conversationId": "direct:15c420f4d6bf4e9f8497768db09b7ed0",
    "selector.found": true,
    "backend.bound": false,
    "policy_exercise": [
      { "step": "override_off", "effectiveLevel": "Off", "statusCaption": "Off (session override)" },
      { "step": "override_minimal", "effectiveLevel": "Minimal" },
      { "step": "override_standard", "effectiveLevel": "Standard" },
      { "step": "override_detailed", "effectiveLevel": "Detailed" },
      { "step": "clear_override_button", "effectiveLevel": "Standard", "isOverrideActive": false }
    ],
    "send.off.context_assembly_reached": false,
    "send.off.manifest_verified": "BLOCKED_backend_unbound_pre_admission",
    "friction.settings_mismatch": true,
    "friction.in_memory_only_override": true,
    "friction.disclosure_details_not_user_inspectable": true
  },
  "assertionPassCount": 33,
  "assertionTotal": 33
}
```

### 3.8 Friction register

| Friction | Observation |
|----------|-------------|
| Documented-settings mismatch | [GOAL_MATRIX.md](../GOAL_MATRIX.md) `A1-TC-01` user entry is “Configure context policy in **settings**.” Production entry is Townhall **session** selector only; `SettingsModel` has no context-policy field. |
| In-memory-only override | Overrides survive within process but are lost on restart (verified seed → verify). |
| Missing disclosure details | Item count, token count, truncation/redaction not user-inspectable without admitted run; nav/panel disclosure empty on unbound profile. |
| Application default not user-configurable | Hardcoded `Standard`; user can only session-override per conversation. |
| Backend-dependent subpath **BLOCKED** | Off zero-item/zero-token manifest on send path; post-send disclosure caption; backend context receipt — all require eligible bound backend. |
| Visual selector paint | **UNVERIFIED-VIS** |

### 3.9 Classification rationale — **WORKS_WITH_FRICTION**

The genuine user-reachable context-policy control exists on direct agent conversations and correctly exercises all four policy levels plus clear/reset through production commands and DI. Session override semantics and application-default fallback behave as A2 described. Friction matches documented gaps: no settings entry, in-memory-only overrides, no disclosure inventory without admitted run, and backend-dependent manifest/send path **BLOCKED** on the clean disposable profile. Policy selector pixel paint is **UNVERIFIED-VIS**.

---

## 4. Cleanup

| Item | Action |
|------|--------|
| `/tmp/zaide-a3-cp/` (runner, build outputs, evidence JSON) | **Removed** |
| `/tmp/zaide-a3-cp-work/` | **Removed** |
| `/tmp/zaide-a3-cp-profile-main-krmGeULU` | **Removed** |
| `/tmp/zaide-a3-cp-profile-restart-C3HMqVjo` | **Removed** |
| Child runner processes | None left running |
| Real-user `~/.config/zaide` | Not written |

---

## 5. Closeout

| Check | Result |
|-------|--------|
| Evidence file created | **Yes** — this document |
| Only new evidence staged | **Yes** (closeout commit) |
| `git diff --check` | clean |
| Relative Markdown links | verified (see §6) |
| Commit message | `docs(audit): execute A3 context policy smoke` |
| Push `master` → `origin` | performed at closeout |
| `HEAD == origin/master` + clean tree | re-verified at closeout |
| A3 overall complete? | **No** — explicitly incomplete |
| A4 / V4 / stabilization begun? | **No** |

---

## 6. Link and whitespace verification

Executed after writing this file:

```bash
git diff --no-index --check /dev/null \
  docs/audits/v1-v3-product-reality/evidence/A3_CONTEXT_POLICY.md
```

Exit status **1** is expected (files differ); **no whitespace-diagnostic output**.

Relative links in this file resolve to existing paths:

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [GOAL_MATRIX.md §14](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md)
- [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md)
- [A3_AGENT_SEND.md](./A3_AGENT_SEND.md)
- [A3_TOWNHALL_AND_CONVERSATIONS.md](./A3_TOWNHALL_AND_CONVERSATIONS.md)
- [A3_RESTART_AND_RECOVERY.md](./A3_RESTART_AND_RECOVERY.md)

---

## 7. Next bounded A3 slice

**A3 remains incomplete.** This note does **not** begin:

- Trace / memory / usage / termination positive paths (`A1-TC-02`, `A1-TC-03`, `A1-TC-08`, `A1-TC-09` beyond preflight)
- Permissions (`A1-TP-*`)
- A4, stabilization, or V4

---

**A3 Live IDE Context Policy (`A1-TC-01`): executed (product-runtime smoke).**

| id | Classification |
|----|----------------|
| `A1-TC-01` | **WORKS_WITH_FRICTION** (session selector reachable; all levels + clear work; in-memory-only overrides; no settings entry; disclosure/manifest on send **BLOCKED** without backend) |

**A3 as a whole: incomplete.**
