# A3 Tools / Permissions Preflight — Negative-Path Evidence (`A1-TP-01`–`A1-TP-03`)

**Audit name:** `v1-v3-product-reality`
**Phase scope of this note:** **A3 tools/permissions preflight only** — negative-path
and absence evidence for rows `A1-TP-01`, `A1-TP-02`, `A1-TP-03`.
**Evidence date:** 2026-08-01
**Repo head at run:** `8a99fedaebfdf1eb152b5ff10312e2b147d7ae5d`

---

## 0. Charter and status

| Item | Status |
|------|--------|
| **Document class** | **A3 product-runtime preflight evidence** (negative-path / absence only) |
| **A3 slice** | Tools / permissions / workspace-mutation preflight (`A1-TP-01`–`A1-TP-03`) |
| **A3 as a whole** | **Incomplete** — positive mediated-action smoke, trace/memory, restart/recovery, A4 **not executed** |
| **A4 / stabilization / V4** | **Not begun** |
| Real desktop UI / xdtools / screenshots / desktop pointer / manual pointer | **Not used** |
| Production code modified | **No** |
| Tracked tests modified | **No** |
| Package pins / audit policy modified | **No** |
| Internal bind APIs (`BindNativeHarness`, `BindAcpRuntime`, `SetBinding`) invoked | **No** |
| Unauthorized temporary backend-binding hook | **No** |
| Fabricated agent tool proposal or permission dialog | **No** |
| Fake backends / test doubles / external backend install | **No** |
| Real user `~/.config/zaide` used | **No** (disposable `HOME` + `XDG_*` only) |
| Repository tree used as workspace root | **No** (disposable `/tmp` fixtures only) |
| Prior A2 / A3 evidence rewritten | **No** |

**Authority inputs:**

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§12)
- [A2_TOOLS_PERMISSIONS.md](./A2_TOOLS_PERMISSIONS.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md) (H0)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md) (H1)
- [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) (`A1-AC-02` bind gap)
- [A3_AGENT_SEND.md](./A3_AGENT_SEND.md) (`A1-AS-02` unbound send rejection)

**Explicitly out of scope this run:**

- Positive allow/deny/ask permission-dialog journeys, live denylist execution, revocation UI, stale-base races, multi-file agent apply, agent rollback success
- Trace/memory/restart, A4, stabilization, V4
- Production / tracked-test / package / policy edits

---

## 1. Classification (authoritative for this preflight)

| id / sub-path | Classification | Summary |
|---------------|----------------|---------|
| **`A1-TP-01` (parent)** | **BLOCKED** | Positive mediated-action control plane (file read/write/command proposal → permission UI → audit attribution) is **not reachable** on a clean disposable profile: no user-reachable command triggers agent actions; Zaide Agent DM is **unbound**; Townhall send (with and without disposable workspace open) produces **no** admitted entries, **no** audit records, **no** `AgentAction`/`ToolCall`/`ToolResult` projections, **no** `PermissionReviewDialog` in the visual tree. Broker lifecycle not entered. Parent row is **not** upgraded to `WORKS`. |
| No user-reachable action trigger | **UNWIRED** | `ICommandRegistry` (38 commands) has **zero** action/permission/tool-like command IDs. Send is the only adjacent user path; it rejects pre-admission when unbound ([A3_AGENT_SEND](./A3_AGENT_SEND.md)). |
| Unbound send negative path | **observed** | Draft clears; entry/message/audit deltas **0**; coordinator not busy; no fabricated dialog. |
| Permission dialog appearance | **UNVERIFIED-VIS** | No dialog node observed headless; pixel/modal UX not claimed. |
| **`A1-TP-02` (parent)** | **BLOCKED** | Positive destructive-action permission UX, selectable approval scope, and user revocation are **not reachable**. Permission dimensions beyond the five-kind broker taxonomy are **not user-exposed**. |
| Permission management / revocation UI | **UNWIRED** | Zero permission/revoke/approval-like registry commands; `AgentBackendBindingPanel` has no interactive bind/auth controls; settings overlay scan found **no** permission section (overlay not opened in this slice — settings permission absence also supported by [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)). |
| Command denylist at runtime | **UNVERIFIED** | `AgentCommandDenylist` is production-wired in broker path ([A2_TOOLS_PERMISSIONS](./A2_TOOLS_PERMISSIONS.md)); **no** user-reachable `ExecuteCommand` trigger on clean unbound profile — denylist behavior not exercised here. |
| Approval scope selectable | **UNWIRED** | Fixed “this exact request only” label in production dialog model; no user scope picker (A2). |
| Permission dialog appearance | **UNVERIFIED-VIS** | Not fabricated; not observed. |
| **`A1-TP-03` (parent)** | **BLOCKED** | Positive multi-file agent edit, conflict surfacing via agent path, and agent-attributed rollback are **not reachable** without bound tool-capable backend. Parent row is **not** upgraded to `WORKS`. |
| Agent rollback command/UI | **UNWIRED** | Zero rollback/agent-changeset/mutate-like registry commands; no production rollback surface (A2). |
| Multi-file agent mutation | **BLOCKED** | No user path to trigger broker-mediated multi-file apply on clean profile. |
| Disposable workspace switch + user editor open | **observed** | `FileTreeViewModel.OpenFolderCommand` opens fixture A; `EditorTabViewModel.OpenFileCommand` opens `sample.txt`; switch to fixture B succeeds; **no** agent audit records; fixture `sample.txt` bytes unchanged. User editor path exercised — **not** agent mutation. |
| Stale-base / workspace-generation invalidation with pending agent action | **UNVERIFIED** | Cannot exercise without mediated action entry (blocked). |
| Agent conflict / rollback visual surfaces | **UNVERIFIED-VIS** | Not claimed. |

Allowed classifications used: `BLOCKED`, `UNWIRED`, `UNVERIFIED`, `UNVERIFIED-VIS`, plus **observed** for negative-path facts that do not upgrade parent rows.

Do **not** read this table as A3 tools/permissions positive smoke complete.

---

## 2. Harness construction (temporary; deleted after capture)

| Item | Value |
|------|--------|
| Runner root | `/tmp/zaide-a3-tp/` (removed after capture) |
| Project | `/tmp/zaide-a3-tp/runner/Zaide.Tests.csproj` |
| Assembly | **`Zaide.Tests`** (`InternalsVisibleTo`) |
| TFM | `net10.0` |
| Packages | `Avalonia.Headless` **12.0.5**; `ReactiveUI.Avalonia` **12.0.3**; `ReactiveUI.Avalonia.Microsoft.Extensions.DependencyInjection` **12.0.3** (runner only) |
| Project reference | `/home/cenoda/zaide/src/Zaide.csproj` (unchanged) |
| Entry | `A3HeadlessEntry.BuildAvaloniaApp()` — does **not** call/patch `Program.BuildAvaloniaApp` |
| Lifetime | `UseHeadless(UseHeadlessDrawing=true)` + `SetupWithClassicDesktopLifetime` + production `Program.ConfigureServices` |
| Observation | `ICommandRegistry`, `TownhallViewModel.SendMessageCommand`, `IAgentActionAuditStore`, `IAgentExecutionCoordinator`, `FileTreeViewModel`, `EditorTabViewModel`, visual-tree scan for `PermissionReviewDialog` |
| Not used | xdtools, screenshots, pointer automation, bind injection, broker `RequestAsync` injection, unit tests as proof |

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
| `A1-TP-01` | `/tmp/zaide-a3-tp-profile-f2SzKohD` | **0** | **9/9** pass |
| `A1-TP-02` | `/tmp/zaide-a3-tp-profile-5EZ7e9yw` | **0** | **4/4** pass |
| `A1-TP-03` | `/tmp/zaide-a3-tp-profile-wZ9iGZ1u` | **0** | **5/5** pass |

### 2.3 Disposable fixtures (never repo tree)

```text
/tmp/zaide-a3-tp/fixtures/workspace-a/
  TpDemo.csproj
  Program.cs
  sample.txt          # "baseline agent-mutation probe"
/tmp/zaide-a3-tp/fixtures/workspace-b/
  TpDemo.csproj
  Program.cs
  other.txt
```

---

## 3. `A1-TP-01` — mediated action entry absence

### 3.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Cold headless launch; clean disposable profile | product-runtime |
| 2 | Scan `ICommandRegistry` for action/permission/tool-like IDs | product-runtime |
| 3 | `OpenDirectConversationCommand(CanonicalTownhallAgent)` | product-runtime |
| 4 | Confirm **unbound** backend (`GetSnapshot`) | blocked-backend |
| 5 | `SendMessageCommand` with deterministic body | product-runtime |
| 6 | Capture entry/message/audit/coordinator/dialog-tree state | store-truth + product-runtime |
| 7 | Open disposable workspace A; send again | product-runtime |
| 8 | Re-capture entry/audit/dialog state | store-truth |

**Deterministic send body:** `A3-TP preflight deterministic send body — not an action trigger`

### 3.2 Observed results

| Check | Result |
|-------|--------|
| Registry total commands | **38** |
| Action/permission/tool-like commands | **0** |
| Backend binding | **Unbound** |
| First send: entry delta | **0** |
| First send: message delta | **0** |
| First send: audit delta | **0** |
| First send: `PermissionReviewDialog` count | **0** |
| First send: coordinator busy | **false** |
| After workspace open: root | `/tmp/zaide-a3-tp/fixtures/workspace-a` |
| Second send: entry delta | **0** |
| Second send: audit delta | **0** |
| Second send: dialog count | **0** |
| Townhall action kinds after sends | **none** (`AgentAction`/`ToolCall`/`ToolResult` absent) |

### 3.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-TP-01",
  "exitCode": 0,
  "isolation": {
    "profileRoot": "/tmp/zaide-a3-tp-profile-f2SzKohD",
    "resolvedSettingsDir": "/tmp/zaide-a3-tp-profile-f2SzKohD/config/zaide",
    "preflightOk": true
  },
  "observed": {
    "command_registry.total": 38,
    "command_registry.action_permission_like": [],
    "backend.bound": false,
    "send.entry_delta": 0,
    "send.audit_delta": 0,
    "permission_dialog_count": 0,
    "workspace_open.root": "/tmp/zaide-a3-tp/fixtures/workspace-a",
    "send_after_workspace.entry_delta": 0,
    "send_after_workspace.audit_delta": 0
  },
  "assertions": "9/9 pass",
  "classificationHint": "BLOCKED"
}
```

**Not claimed:** actor-attributed audit success, permission allow/deny behavior, broker `ActionResultReported` Townhall projection, multi-file rollback.

---

## 4. `A1-TP-02` — permission dimensions and management absence

### 4.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Scan registry for permission/revoke/destructive-like commands | product-runtime |
| 2 | Scan main-window visual tree for settings permission sections (overlay not opened) | product-runtime |
| 3 | Scan `AgentBackendBindingPanel` subtree for interactive bind/auth controls | product-runtime |

### 4.2 Observed results

| Check | Result |
|-------|--------|
| Permission/revoke/approval-like commands | **0** |
| Destructive agent-like commands | **0** |
| Settings overlay permission section | **not found** in default tree scan |
| Binding panel interactive controls | **0** |
| Denylist exercised at runtime | **false** |
| Selectable approval scope | **false** (no UI) |
| User revocation UI | **absent** (lifecycle-driven revocation only per A2) |

### 4.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-TP-02",
  "exitCode": 0,
  "observed": {
    "command_registry.permission_revoke_like": [],
    "command_registry.destructive_like": [],
    "binding_panel.interactive_controls": [],
    "denylist.runtime_exercised": false,
    "approval_scope_selectable": false
  },
  "assertions": "4/4 pass",
  "classificationHint": "BLOCKED"
}
```

**Not claimed:** live `sudo`/`bash` denylist denial, 5-minute expiry, dismiss=deny modal behavior, ACP `session/request_permission` protocol path.

---

## 5. `A1-TP-03` — workspace mutation / rollback absence

### 5.1 Sequence

| Step | Action | Evidence class |
|------|--------|----------------|
| 1 | Scan registry for rollback/agent-changeset commands | product-runtime |
| 2 | `FileTreeViewModel.OpenFolderCommand(workspace-a)` | product-runtime |
| 3 | `EditorTabViewModel.OpenFileCommand(sample.txt)` — user editor path | product-runtime |
| 4 | Switch workspace to `workspace-b` | product-runtime |
| 5 | Verify fixture `sample.txt` bytes unchanged; audit store empty | store-truth + product-runtime |

### 5.2 Observed results

| Check | Result |
|-------|--------|
| Rollback/mutate-like commands | **0** |
| Workspace A root | `/tmp/zaide-a3-tp/fixtures/workspace-a` |
| Editor opened | `/tmp/zaide-a3-tp/fixtures/workspace-a/sample.txt` |
| Workspace B after switch | `/tmp/zaide-a3-tp/fixtures/workspace-b` |
| `sample.txt` bytes unchanged | **true** |
| Agent audit records | **0** |
| Agent rollback path | **UNWIRED** (no command/UI) |
| Multi-file agent edit | **BLOCKED** (no bind) |

### 5.3 Machine-readable excerpt

```json
{
  "scenarioId": "A1-TP-03",
  "exitCode": 0,
  "observed": {
    "command_registry.rollback_mutate_like": [],
    "workspace_a.root": "/tmp/zaide-a3-tp/fixtures/workspace-a",
    "workspace_b.root_after_switch": "/tmp/zaide-a3-tp/fixtures/workspace-b",
    "fixture.sample_bytes_unchanged": true,
    "audit_record_count": 0,
    "agent_rollback_path": "UNWIRED"
  },
  "assertions": "5/5 pass",
  "classificationHint": "BLOCKED"
}
```

**Not claimed:** multi-file atomic apply, agent-attributed change set, rollback restoring agent changes, optimistic stale-base with pending permission dialog, post-consume apply race.

---

## 6. Positive-path blocker (explicit)

Completing positive `A1-TP-01`–`A1-TP-03` smoke requires a **genuine user-reachable** workflow to bind a tool-capable backend (`NativeHarness` or ACP) to the Townhall agent actor, then admit a backend run that issues broker-mediated file/command actions. [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) records **no** such UI on a clean profile; this preflight did **not** invoke internal bind APIs or unauthorized bind hooks.

Until a shipped bind/onboarding path exists, positive permission UX, denylist runtime proof, revocation UX, stale-base protection, and agent rollback remain **BLOCKED** or **UNVERIFIED** for A3 product-runtime acceptance.

---

## 7. Cross-cutting honesty checks

| Check | Result |
|-------|--------|
| Disposable profile preflight | **Pass** all scenarios |
| Real-user profile | **Not used** |
| Repo tree as workspace | **Not used** |
| Internal bind APIs | **Not invoked** |
| Fabricated permission dialog / tool proposal | **No** |
| Unauthorized filesystem mutation by agent path | **None observed** (fixture bytes unchanged) |
| Multi-file rollback or actor-attributed audit | **Not claimed** |
| Production / tests / packages / policy | **Unchanged** |

---

## 8. What this preflight does **not** claim

1. A3 tools/permissions **positive** smoke complete.
2. Permission dialog allow/deny/dismiss behavior (**UNVERIFIED-VIS** / **BLOCKED**).
3. Live command denylist, expiry, or revocation propagation.
4. Stale-base or workspace-generation invalidation with pending agent actions.
5. Multi-file agent apply, conflict surfaces, or rollback success.
6. ACP `session/request_permission` automatic protocol behavior.
7. Trace/memory/restart/A4/V4 work.

---

## 9. Cleanup

After evidence capture:

- Removed `/tmp/zaide-a3-tp/` (runner, obj, out, evidence JSON working copies, fixtures).
- Removed disposable `/tmp/zaide-a3-tp-profile-*`.
- No tracked tree changes except this evidence document.

---

## 10. Closeout checklist

| Check | Result |
|-------|--------|
| Evidence file created | **Yes** — this document |
| Commit scope | evidence file only (closeout) |
| `git diff --check` | see §11 |
| Relative Markdown links | see §11 |
| A3 overall complete? | **No** |
| A4 / V4 / trace / restart begun? | **No** |

---

## 11. Link and whitespace verification

```bash
git diff --no-index --check /dev/null \
  docs/audits/v1-v3-product-reality/evidence/A3_TOOLS_PERMISSIONS_PREFLIGHT.md
```

Relative links in this file resolve to existing paths:

- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md)
- [GOAL_MATRIX.md §12](../GOAL_MATRIX.md#12-tools-permissions-and-workspace-mutation)
- [A2_TOOLS_PERMISSIONS.md](./A2_TOOLS_PERMISSIONS.md)
- [A3_AUTOMATION_READINESS.md](./A3_AUTOMATION_READINESS.md)
- [A3_HEADLESS_RUNNER_POC.md](./A3_HEADLESS_RUNNER_POC.md)
- [A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)
- [A3_AGENT_SEND.md](./A3_AGENT_SEND.md)

---

## 12. Status line

**A3 Tools / Permissions preflight (`A1-TP-01`–`A1-TP-03`): executed (negative-path / absence only).**

| id | Preflight classification |
|----|--------------------------|
| `A1-TP-01` | **BLOCKED** (no user-reachable action trigger; unbound send produces no mediation/audit/dialog) |
| `A1-TP-02` | **BLOCKED** (no destructive permission UX path; permission management **UNWIRED**; denylist runtime **UNVERIFIED**) |
| `A1-TP-03` | **BLOCKED** (no multi-file agent path; rollback **UNWIRED**; fixture unchanged after workspace switch) |

**A3 as a whole: incomplete.**

**A4 / V4: not authorized.**

---

*Recorded 2026-08-01. Out-of-tree Avalonia.Headless 12.0.5 product-runtime runner executed clean-profile tools/permissions preflight under disposable XDG; no bind injection; temporary runner, profiles, and fixtures removed; no production edits.*
