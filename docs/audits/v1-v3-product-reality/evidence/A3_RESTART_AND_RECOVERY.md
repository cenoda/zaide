# A3 Clean-Profile Smoke — Restart and Recovery (`A1-TC-04`, `A1-TC-05`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 restart/recovery execution slice only** — rows
`A1-TC-04`, `A1-TC-05`. **`A1-TC-01` context policy is out of scope.**
**Evidence date:** 2026-08-01
**Repo head at run:** `e76a2f1d7d5390c69b0009c1e0278678d1af627c`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime clean-profile smoke evidence** (TC-04/05 only) |
| **A3 slice** | Restart / recovery (`A1-TC-04`, `A1-TC-05`) |
| **A3 as a whole** | **Incomplete** — context policy (`A1-TC-01`), trace/memory/usage/termination positive paths, A4 **not executed** in this note |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / desktop pointer / manual desktop | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| `Avalonia.Headless` added to tracked project | **No** |
| `Directory.Packages.props` changed | **No** |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` rewritten to “A3 complete” | **No** |
| Real user `~/.config/zaide` read or written | **No** (disposable `HOME` + `XDG_*` only) |
| Zaide repository used as workspace / conversation fixture | **No** (`/tmp/zaide-a3-rc-work` process CWD) |
| Registry / unit tests used as A3 proof | **No** |
| Fake backends / bind injection / fabricated admitted runs | **No** |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§14)
- [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A3_TOWNHALL_AND_CONVERSATIONS.md](./A3_TOWNHALL_AND_CONVERSATIONS.md) (unbound send baseline)

**Out of scope (explicit):**

- `A1-TC-01` live IDE context policy selector / settings entry / override persistence
- Positive interrupted-run admission with eligible bound backend
- Sessions, runs, backend bindings, traces, usage, or lifecycle memory restored via conversation persistence (observed only as **not restored**)
- Pixel paint of recovery banners or unread dots (**UNVERIFIED-VIS** where noted)

---

## 1. Two-row classification table (authoritative for this slice)

| id | A3 classification | Summary |
|----|-------------------|---------|
| `A1-TC-04` | **WORKS_WITH_FRICTION** | Disposable Townhall direct conversation + channel shell persist and restore across a second headless process: draft, active selection, direct participant pair (`human:user-1`, `townhall-agent:agent-1`), channel messages, and `lastReadEntryIds` cursors in `conversations.json`. After 350 ms debounce, draft persists to disk and restores in UI. Immediate graceful exit **before** 250 ms debounce does **not** persist the draft (`file_contains=False`). Corrupt primary `conversations.json` with valid LKG recovers seeded data (`LoadResult=Corrupt`, draft + channel marker restored). Recovery is **silent** (no recovery/continuity/interrupted nodes in logical tree). **Friction:** debounced-save gap on fast exit; no user-visible recovery status; `ConversationPersistenceService` not in Zaide-owned shutdown dispose path (A2 gap, not re-opened as regression here). **Not claimed:** sessions, runs, bindings, traces, usage, memory. |
| `A1-TC-05` | **BLOCKED** | Clean unbound disposable profile: unbound send produces **no** admitted run (`entry_delta=0`, coordinator not busy) — no fabricated success. **Positive interrupted-run path BLOCKED** — no production executable path to durable interrupted Running/Accepted checkpoint without eligible backend binding and admitted run. Graceful shutdown vs `Environment.Exit` force-exit (skipping `ApplicationShutdown`) both leave **0** `SessionRecovery` durable records (`agents-durable` partition absent). Cold restart: startup reconcile reports `recoverable=0`, `terminal=0`, `indeterminate=0`; stored checkpoint count **0**; no automatic backend re-invocation (`entry_delta=0`, not busy); user must explicitly re-send. Townhall recovery classification UI **absent** from visual tree. **Recoverable vs Indeterminate:** not observable on this profile because no durable interrupted checkpoint was creatable without fakes. |

Allowed classifications used: `WORKS`, `WORKS_WITH_FRICTION`, `BLOCKED`, `UNVERIFIED`, `UNVERIFIED-VIS`.

---

## 2. Harness construction (temporary; deleted after evidence capture)

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-rc/` (removed after capture) |
| Project | `/tmp/zaide-a3-rc/runner/Zaide.Tests.csproj` |
| Assembly | **`Zaide.Tests`** (`InternalsVisibleTo`) |
| TFM | `net10.0` |
| Packages | `Avalonia.Headless` **12.0.5**; `ReactiveUI.Avalonia` **12.0.3**; `ReactiveUI.Avalonia.Microsoft.Extensions.DependencyInjection` **12.0.3** (runner only) |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Process CWD | `/tmp/zaide-a3-rc-work` (not Zaide repo) |
| Isolation | One disposable profile per independent scenario group; **separate OS processes** for each write/verify restart pair |
| Not used | xdtools, screenshots, pointer automation, bind APIs, unit tests as proof |

### 2.1 Isolation protocol

| Variable | Disposable value |
|----------|------------------|
| `HOME` | `$PROFILE_ROOT/home` |
| `XDG_CONFIG_HOME` | `$PROFILE_ROOT/config` (absolute) |
| `XDG_DATA_HOME` | `$PROFILE_ROOT/data` |
| `XDG_STATE_HOME` | `$PROFILE_ROOT/state` |
| `XDG_CACHE_HOME` | `$PROFILE_ROOT/cache` |

Preflight asserted `SettingsPathResolver.GetSettingsDirectory()` under `$XDG_CONFIG_HOME/zaide` and **not** `/home/cenoda/.config/zaide`.

### 2.2 Runner command pattern

```bash
PROFILE_ROOT="$(mktemp -d /tmp/zaide-a3-rc-profile-XXXXXXXX)"
export HOME="${PROFILE_ROOT}/home"
export XDG_CONFIG_HOME="${PROFILE_ROOT}/config"
export XDG_DATA_HOME="${PROFILE_ROOT}/data"
export XDG_STATE_HOME="${PROFILE_ROOT}/state"
export XDG_CACHE_HOME="${PROFILE_ROOT}/cache"
mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME" "$XDG_CACHE_HOME"

cd /tmp/zaide-a3-rc-work
dotnet "/tmp/zaide-a3-rc/out/Release/net10.0/Zaide.Tests.dll" \
  --scenario <id> \
  --profile "$PROFILE_ROOT" \
  --evidence "/tmp/zaide-a3-rc/evidence/<id>.json" \
  --repo-head "e76a2f1d7d5390c69b0009c1e0278678d1af627c"
```

Force-quit scenario adds `--force-exit` (process ends without `desktop.Shutdown` / `ApplicationShutdown`).

### 2.3 Disposable profiles (final capture)

| Scenario group | Profile root | Processes | Assertions (pass/total) |
|----------------|--------------|-----------|-------------------------|
| TC-04 restart restore | `/tmp/zaide-a3-rc-profile-restart-A5kVMND8` | seed + verify + corrupt-restart | 9/9 + 3/3 on verify/corrupt |
| TC-04 debounce early | `/tmp/zaide-a3-rc-profile-early-hkNgcFC0` | write + verify | 1/1 |
| TC-04 debounce late | `/tmp/zaide-a3-rc-profile-late-EzU7l5Kt` | write + verify | 2/2 |
| TC-05 interrupted-run negative | `/tmp/zaide-a3-rc-profile-tc05-oDtSxWAR` | graceful + force + restart-verify | 1/1 + 1/1 + 4/4 |

All scenario processes exited **0**.

---

## 3. `A1-TC-04` — conversation persistence and restart restoration

### 3.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Process A: cold headless launch; channel sends; open Zaide Agent direct DM; set deterministic draft | product-runtime |
| 2 | Wait 350 ms (past 250 ms debounce); graceful shutdown | product-runtime |
| 3 | Process B: same profile; cold launch; verify VM + store | product-runtime + store-truth |
| 4 | Separate profiles: immediate shutdown before debounce vs after debounce | product-runtime |
| 5 | Corrupt primary JSON; restart; verify LKG recovery + silent UI | product-runtime + store-truth |

### 3.2 Observed results — restart restore (process A → B)

| Check | Result |
|-------|--------|
| Direct conversation id | `direct:42587ddafcf741d2915cbed045ff80e1` (stable across seed/verify) |
| Participants | `human:user-1`, `townhall-agent:agent-1` |
| Draft after restart | `A3-TC-04 deterministic draft marker` |
| Active selection | Same direct conversation |
| Direct nav | `direct:…:Zaide Agent` |
| Channel-1 / channel-2 message bodies | Restored (`A3-TC-04 channel-1 marker`, `A3-TC-04 channel-2 marker`) |
| `lastReadEntryIds` in `conversations.json` | Present for `channel:channel-1` and `channel:channel-2` (read cursors persisted) |
| Backend binding after restart | **Unbound** (not restored) |
| Sessions/runs restored via conversation file | **No** (not claimed) |
| Recovery banner / status UI | **None** in tree (`recovery_ui_nodes=[]`); pixel paint **UNVERIFIED-VIS** |
| Unread dot paint | **UNVERIFIED-VIS** (last-read cursors persisted; badge chrome not pixel-tested) |

### 3.3 Debounce vs graceful exit

| Case | Draft in `conversations.json` after exit? | Draft in UI on next launch? |
|------|---------------------------------------------|-----------------------------|
| Shutdown **immediately** after draft set (before 250 ms) | **No** (`file_contains=False`) | **No** (empty draft) |
| Wait 350 ms after draft set, then graceful shutdown | **Yes** | **Yes** (`A3-TC-04 debounce-late draft`) |

### 3.4 Corrupt primary + LKG

| Check | Result |
|-------|--------|
| Primary file corrupted | `{ this is not valid json` |
| LKG present before corrupt | Yes |
| `LoadResult` on restart | `Corrupt` |
| Data recovered from LKG | Draft + channel-1 marker restored |
| User-visible recovery status | **Absent** (silent); visual banner **UNVERIFIED-VIS** |

### 3.5 Machine-readable excerpt (restart verify)

```json
{
  "scenarioId": "A1-TC-04-restart-verify",
  "exitCode": 0,
  "observedViewModelState": {
    "verify.draft": "A3-TC-04 deterministic draft marker",
    "verify.activeConversationId": "direct:42587ddafcf741d2915cbed045ff80e1",
    "verify.participants": ["human:user-1", "townhall-agent:agent-1"],
    "verify.backend_binding_restored": false,
    "recovery_silent": true,
    "recovery_visual_banner": "UNVERIFIED-VIS"
  },
  "assertionPassCount": 9,
  "assertionTotal": 9
}
```

### 3.6 Classification rationale — **WORKS_WITH_FRICTION**

Core conversation snapshot contract holds at product runtime under headless DI: direct shell, draft, selection, participants, channel history, and last-read cursors restore after a clean second process. LKG fallback works silently. Friction matches A2 gaps: fast exit before debounce loses draft; no user-visible recovery failure/success chrome; persistence service flush on exit not proven via Zaide-owned shutdown dispose.

---

## 4. `A1-TC-05` — interrupted-run classification (executable scope only)

### 4.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Unbound profile: open DM; send (no binding) | blocked-backend |
| 2 | Graceful shutdown process | product-runtime |
| 3 | Second process: force exit without `ApplicationShutdown` | product-runtime |
| 4 | Third process: cold restart; inspect reconcile + continuity store + UI | product-runtime |

### 4.2 Observed results

| Check | Result |
|-------|--------|
| Backend binding | **Unbound** |
| Send admitted entries | **0** (`entry_delta=0`) |
| Coordinator busy after send | **false** |
| `agents-durable` after graceful shutdown | **Absent** (`session_recovery_records=0`) |
| `agents-durable` after force exit | **Absent** (`session_recovery_records=0`) |
| Graceful vs force-quit checkpoint delta | **No difference** on this profile (both **0** records) |
| Startup reconcile (cold) | `recoverable=0`, `terminal=0`, `indeterminate=0` |
| Stored `SessionRecovery` checkpoints on disk | **0** |
| Stored `Recoverable` vs cold `Indeterminate` | **Not distinguishable** — no durable interrupted checkpoint creatable without backend |
| Auto backend re-invocation after restart (200 ms idle) | **No** (`entry_delta=0`, not busy) |
| User must re-send | **Yes** (no auto activity) |
| Townhall recovery / continuity / interrupted UI nodes | **Absent** |
| Recovery classification visual | **UNVERIFIED-VIS** |

### 4.3 Machine-readable excerpt (restart verify)

```json
{
  "scenarioId": "A1-TC-05-restart-verify",
  "exitCode": 0,
  "observedViewModelState": {
    "startup.reconcile.recoverable": 0,
    "startup.reconcile.indeterminate": 0,
    "stored_checkpoint_count": 0,
    "auto_backend_reinvoke.entry_delta": 0,
    "townhall_recovery_ui_absent": true,
    "positive_interrupted_run_path": "BLOCKED — no eligible backend binding; no admitted run to interrupt."
  },
  "assertionPassCount": 4,
  "assertionTotal": 4
}
```

### 4.4 Classification rationale — **BLOCKED**

Negative-path contract fragments are observable (no fabricated run, no auto re-invoke, reconcile runs without resume side effects, no Townhall recovery UI). The **positive** interrupted-run checkpoint / `Recoverable` vs `Indeterminate` reconciliation exercise requires an admitted in-flight run with durable continuity records. Clean disposable profile has no eligible backend binding; production send path rejects pre-admission (consistent with [A3_TOWNHALL_AND_CONVERSATIONS.md](./A3_TOWNHALL_AND_CONVERSATIONS.md) `A1-TH-02`). Per charter: classify positive portion **BLOCKED** rather than using internal services or test seams.

---

## 5. Cross-cutting isolation and honesty checks

| Check | Result |
|-------|--------|
| Disposable profile preflight (all groups) | **Pass** |
| Real-user profile writes | **None** observed |
| Zaide repo as workspace/CWD | **No** (`/tmp/zaide-a3-rc-work`) |
| xdtools / screenshots / pointer automation | **Not used** |
| Production / tracked tests / packages / audit policy | **Unchanged** |
| Fabricated admitted runs | **Not used** |
| Conversation persistence claimed to restore sessions/runs/bindings | **No** |

---

## 6. What this slice does **not** claim

1. **A3 overall complete** — only `A1-TC-04` and `A1-TC-05`.
2. `A1-TC-01` context policy behavior or persistence.
3. Interrupted-run **Recoverable** classification with matching binding present.
4. Graceful-shutdown checkpoint when an active bound session exists.
5. Pixel-perfect recovery banners, unread badges, or scroll positions.
6. Trace/memory/usage/session termination positive smoke.

---

## 7. Cleanup

After evidence capture:

- Removed `/tmp/zaide-a3-rc/` (runner, obj, out, evidence JSON working copies).
- Removed `/tmp/zaide-a3-rc-profile-*` disposable trees.
- Removed `/tmp/zaide-a3-rc-work`.
- No tracked tree changes except this evidence document.

---

## 8. Verification

| Check | Result |
|-------|--------|
| `git diff --check` | **Clean** (evidence file only) |
| Relative Markdown links | Verified — paths under `docs/audits/v1-v3-product-reality/` resolve |

Relative links in this file resolve to existing paths:

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md)
- [A3_TOWNHALL_AND_CONVERSATIONS.md](./A3_TOWNHALL_AND_CONVERSATIONS.md)

---

**A3 Restart and Recovery (`A1-TC-04`, `A1-TC-05`): executed (product-runtime smoke).**

| id | Classification |
|----|----------------|
| `A1-TC-04` | **WORKS_WITH_FRICTION** |
| `A1-TC-05` | **BLOCKED** (positive interrupted-run path; negative no-resume/no-reinvoke evidence recorded) |

**A3 as a whole: incomplete.**

**`A1-TC-01`, A4, stabilization, V4: not begun.**
