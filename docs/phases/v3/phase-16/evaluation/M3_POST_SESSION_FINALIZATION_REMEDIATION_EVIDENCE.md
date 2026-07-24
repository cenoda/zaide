# Phase 16 M3 — Post-Session Finalization Remediation Evidence

**Status:** **Complete (remediation only)** — repository orchestrator, focused
tests, and Phase 16 status/evidence docs updated from current-session diagnosis.
Remediation itself did **not** create credentials, launch Qwen/Node, call APIs,
reprobe egress, acquire artifacts, or run an M3 retry / M4 work.

**Session under diagnosis (NO-GO; not reclassified):**
`m3q-20260724T072341Z-8f567943`

---

## 1. Scope

| Authorized | Performed |
|---|---|
| Diagnose exact `qwen_exit=55` cause from **this** session only | **Yes** |
| Diagnose incomplete balance-after/finalization despite fixed reaping | **Yes** |
| Fix orchestrator so finalization is deterministic after every current-session outcome | **Yes** |
| Focused regression tests for the discovered failure path | **Yes** |
| Update Phase 16 evidence/status docs; keep session **NO-GO** | **Yes** |
| Change 24-turn or 120s policy | **No** |
| Qualification retry / credential / Qwen launch / M4 | **No** |

---

## 2. Exact cause of `qwen_exit=55` (this session only)

Non-secret artifacts under:

```text
/tmp/phase16-artifacts/phase-16/records/m3-qualification/m3q-20260724T072341Z-8f567943/
```

| Field | Evidence |
|---|---|
| `run/qwen-result.env` | `qwen_exit=55` |
| `run/qwen.stderr` error type | **`FatalBudgetExceededError`** |
| `run/qwen.stderr` message | `Run aborted: wall-clock budget of 120s exceeded (--max-wall-time).` |
| `run/qwen.stderr` code | **55** |
| Locked argv (`run/exact-argv.txt`) | `--max-wall-time 120s`, `--max-session-turns 24`, `--approval-mode yolo` |
| `run/reap.env` | `inner_wait_status=exited`, `inner_wait_exit=4`, `inner_wait_elapsed_sec=129`, `inner_overall_timeout_sec=200`, `post_qwen_verify_budget_sec=45`, `inner_reaped_exit=` (empty) |
| Outer overall budget (200s) | **Not** the cause — wait finished as normal `exited`, not `overall_timeout` |
| Post-qwen verify budget (45s) | **Not** the cause — status is not `post_qwen_budget_exceeded` |
| Ledger | `inner_qualification exit=4` at `2026-07-24T07:25:59Z` (inner maps non-zero Qwen → exit 4) |
| Host workspace | `FetchData` **0** / `RetrieveData` **11** (rename verified; dual GO still fails on exit ≠ 0) |

**Verdict:** exit **55** is Qwen Code’s own wall-clock budget
(`FatalBudgetExceededError` / `--max-wall-time 120s`). It is **not** the
orchestrator’s 200s overall inner timeout, **not** the 45s post-qwen verify
budget, and **not** a turn-limit exit 53. No inference from older sessions was
required; older sessions remain historical only.

---

## 3. Why balance-after / finalization was incomplete

### 3.1 What ran

| Step | Result |
|---|---|
| Preflight / credential / balance-before | **Yes** (balance-before USD **3.95**) |
| Qwen once under 24/120s yolo | **Yes** → `qwen_exit=55` |
| Parent-shell reap (`wait_inner_with_reap_budget`) | **Yes** — real `inner_wait_exit=4` (no synthetic bash 127) |
| `log_step inner_qualification 4` | **Yes** (last ledger line) |
| `balance-after.json` | **Missing** |
| `workspace-result.env` | **Missing** |
| `cleanup.env` | **Missing** |
| Post-launch `execution.env` update | **Not updated** (still `candidate_execution=NO`, `qwen_exit_source=none`, utc pre-launch) |
| `summary.env` STOP_REASON / QUALIFICATION_VERDICT | **Not appended** (script aborted before `stop_with`) |

### 3.2 Root cause (orchestrator)

Fixed reaping path worked. Finalization failed **after** reap:

1. `launch_netns_inner` called `set +e; wait_inner_with_reap_budget; set -e`.
2. Then `return "$inner_ec"` with `inner_ec=4` (unshare/inner after non-zero Qwen).
3. On Bash 5.x, a function that **enables `set -e` and returns non-zero** makes
   that `set -e` effective when the call completes. The caller’s surrounding
   `set +e` does **not** protect against this sticky abort.
4. The whole orchestrator shell exited with status **4** immediately after the
   ledger `inner_qualification` line — before balance-after, workspace check,
   cleanup, and `stop_with` summary append.

This is distinct from the earlier subshell-wait / bash **127** defect (already
remediated in `M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md`).

### 3.3 Reproduction (local, no Qwen)

Defective pattern under `set -euo pipefail`:

```bash
launch_netns_inner_defective() {
  set +e
  INNER_WAIT_EXIT=4
  set -e
  return "$INNER_WAIT_EXIT"
}
set +e
launch_netns_inner_defective   # aborts whole shell; FINALIZED never runs
INNER_EC=$?
```

Fixed pattern:

```bash
launch_netns_inner_fixed() {
  set +e
  INNER_WAIT_EXIT=4
  return 0                     # status only via INNER_WAIT_EXIT
}
set +e
launch_netns_inner_fixed
INNER_EC="${INNER_WAIT_EXIT:-1}"
# finalization runs
```

---

## 4. Orchestrator fix

File: `tools/Phase16NativeHarnessEvaluation/Scripts/m3-qualification-smoke.sh`

| Change | Detail |
|---|---|
| `launch_netns_inner` contract | Always **`return 0`** after publishing real status in **`INNER_WAIT_EXIT`** (including attach/ready failures) |
| No `return "$inner_ec"` | Removed; child/unshare exit is never the function’s return code |
| Caller inner status | `INNER_EC="${INNER_WAIT_EXIT:-1}"` (not `INNER_EC=$?`) |
| Egress preflight status | `EGRESS_PREFLIGHT_EC="${INNER_WAIT_EXIT:-1}"` (not `$?`) |
| Post-launch marking | If `qwen-result.env` exists → `CANDIDATE_EXECUTION=YES` / `QWEN_EXIT_SOURCE=current_run_dir` even when exit ≠ 0 |
| Finalization path | Unchanged order; now **reachable** after non-zero Qwen/unshare: balance-after attempt, workspace-result, cleanup.env, then stop/NO-GO as appropriate |
| Policy ceilings | **Unchanged** — 24 turns / 120s wall / USD 1 smoke / USD 3 cumulative |

---

## 5. Tests

| Test | Intent |
|---|---|
| `SmokeOrchestrator_LaunchNetnsInnerAlwaysReturnsZeroAfterPublishingExit` | Static contract: no `return "$inner_ec"`; callers read `INNER_WAIT_EXIT`; finalization after launch |
| `LaunchNetnsInnerPattern_NonZeroChildDoesNotAbortParentFinalization` | Behavioral: defective sticky `set -e`+return aborts; fixed return-0 continues to balance/workspace/cleanup |
| `SmokeOrchestrator_FinalizationRunsAfterQwenResultRegardlessOfExit` | Static: always-run finalization markers + candidate_execution before balance-after |

Verification (this slice):

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
git diff --check
```

---

## 6. Session disposition

- Session `m3q-20260724T072341Z-8f567943` remains **NO-GO**.
- Verified TC-T01 rename is **not** qualification success (dual GO requires Qwen exit 0 **and** rename).
- No second attempt, no M4, no policy change to 24/120s without a separate human decision.
- Candidate remains **eligible for later M3 qualification** but **not qualified**.

---

## 7. Cross-References

- `M3_QUALIFICATION_EVIDENCE.md` — session evidence (NO-GO exit 55)
- `M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md` — prior 120s + same-shell reap (127 fix)
- `M3_FRESH_SESSION_ELIGIBILITY_REMEDIATION_EVIDENCE.md` — preflight-before-key; fresh ID
- `CAMPAIGN_LOCK.md` — active 24/120s locks
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 post-session finalization remediation — repository wiring complete from
session m3q-20260724T072341Z-8f567943 diagnosis. Not a qualification retry.*
