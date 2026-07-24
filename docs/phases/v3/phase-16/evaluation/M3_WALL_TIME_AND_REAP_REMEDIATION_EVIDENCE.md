# Phase 16 M3 — Wall-Time (120s) and Exit-Reaping Remediation Evidence

**Status:** **Complete (remediation only)** — repository policy, smoke
orchestrator, focused tests, and active campaign docs updated. Remediation
itself did **not** create credentials, launch Qwen/Node, call APIs, reprobe
egress, or run an M3 retry / M4 work.

**Human decisions (this slice):**

1. A future TC-T01 qualification retry may use a **120-second** wall-time
   limit (`--max-wall-time 120s`).
2. Max session turns remains **12**; USD **1** smoke and USD **3** cumulative
   caps remain unchanged.
3. Prior yolo session `m3q-20260724T054307Z-481ad1de` completed the verified
   TC-T01 rename but failed only because Qwen exited **55** at the former
   **60-second** wall-time ceiling. Historical session records remain at 60s.

---

## 1. Scope

| Authorized | Performed |
|---|---|
| Update every active policy/orchestrator/test/doc surface from 60s → 120s | **Yes** |
| Preserve historical 60s session records unchanged | **Yes** |
| Diagnose and fix post-exit `pid not a child` (127) reaping/status collection | **Yes** |
| Preserve other locks (TC-T01, yolo, auth, DNS, egress, key lifecycle, 12 turns, USD caps) | **Yes** |
| Focused regression tests for 120s argv + reaping behavior | **Yes** |
| Qualification retry / credential / Qwen launch / M4 | **No** |

---

## 2. Wall-time lock (active)

Locked smoke argv tail (policy + orchestrator):

```text
--auth-type openai
--openai-base-url https://api.deepseek.com
--approval-mode yolo
--model deepseek-v4-flash
--output-format json
--max-session-turns 12
--max-wall-time 120s
```

| Property | Locked value |
|---|---|
| Wall time | **`120s`** via `--max-wall-time 120s` |
| Session turns | **12** (unchanged) |
| Smoke / cumulative spend | **USD 1** / **USD 3** (unchanged) |
| Approval mode | **`yolo`** (unchanged) |
| Task / candidate | **TC-T01** / **qwen-code** only (unchanged) |
| GO criteria | Qwen exit **0** **and** verified TC-T01 rename (unchanged) |

Historical session `m3q-20260724T054307Z-481ad1de` executed under **60s** and
must not be rewritten; its `qwen_exit=55` / wall-60s evidence stays in
`M3_QUALIFICATION_EVIDENCE.md`.

Orchestrator overall inner budget default raised from **120s** to **200s** so
probes + 120s Qwen wall + post-qwen verify budget can complete without the
outer wait forcing a premature reap.

---

## 3. Post-exit reaping root cause and fix

### 3.1 Defect (session `m3q-20260724T054307Z-481ad1de`)

After Qwen wrote `qwen_exit=55` and the unshare child exited, the orchestrator
ledger recorded:

```text
step=inner_qualification exit=127
inner_wait_status=exited
inner_wait_exit=127
```

Root cause: `INNER_EC="$(wait_inner_with_reap_budget)"` ran
`wait_inner_with_reap_budget` (and thus `wait "$UNSHARE_PID"`) inside a
**command-substitution subshell**. Bash `wait` only reaps children of the
**current** shell; the unshare job belonged to the parent shell, so the subshell
`wait` returned **127** (“pid is not a child of this shell”). Finalization still
ran (no hang), but the recorded inner exit was synthetic, not the real child
status.

### 3.2 Remediation in `Scripts/m3-qualification-smoke.sh`

1. **Call `wait_inner_with_reap_budget` in the parent shell** (no `$(...)`).
2. Publish results via globals **`INNER_WAIT_STATUS`** / **`INNER_WAIT_EXIT`**.
3. **`resolve_unshare_exit_code`** — prefer a successful same-shell `wait`; never
   treat bash **127** as the child exit.
4. **`force_reap_children`** — on a real `wait` result, store
   **`INNER_REAPED_EXIT`** so a later “not a child” wait can recover the first
   captured exit instead of inventing 127.
5. Ledger `reap.env` records `inner_wait_*`, `force_reap_unshare_exit` /
   `not_a_child`, and `inner_reaped_exit` for audit.

Qualification GO still uses `qwen-result.env` (`qwen_exit`) plus workspace
rename verification; this fix makes the inner/unshare ledger exit deterministic
for diagnosis without hanging or replacing a known real exit with 127.

---

## 4. Repository changes

| Path | Change |
|---|---|
| `tools/.../Phase16M3QualificationPolicy.cs` | `MaxWallTime = 120s` |
| `tools/.../Scripts/m3-qualification-smoke.sh` | 120s argv; overall budget 200s; same-shell wait/reap; no `$(wait_inner…)` |
| `tests/.../Phase16M3QualificationPolicyTests.cs` | assert 120s; reject legacy 60s |
| `tests/.../Phase16M3QualificationSmokeOrchestratorTests.cs` | argv contract + behavioral reaping regressions |
| Phase 16 evaluation docs + plan + roadmap active locks | 120s active ceiling; historical sessions keep 60s |

---

## 5. Verification (this slice)

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
git diff --check
```

Qualification smoke execution remains **out of scope**.

---

## 6. Cross-References

- `M3_QUALIFICATION_EVIDENCE.md` — historical 60s wall exit 55 after verified rename
- `M3_WRITE_CAPABLE_REMEDIATION_EVIDENCE.md` — prior yolo + first reap/finalization path
- `CAMPAIGN_LOCK.md` — active campaign ceilings
- `IMPLEMENTATION_PLAN.md` — phase status

---

**Post-remediation exercise (separate grant):** session
`m3q-20260724T060109Z-45dd1c5f` ran with locked **120s** wall argv and the fixed
parent-shell reap path. Ledger recorded `inner_wait_status=exited` /
`inner_wait_exit=4` (real unshare/inner exit after `qwen_exit=53`) without
synthetic bash **127**. Qualification overall remained **NO-GO** on turn limit
after verified rename (`M3_QUALIFICATION_EVIDENCE.md`).

---

*M3 wall-time (120s) and exit-reaping remediation — repository wiring complete;
later exercised by session `m3q-20260724T060109Z-45dd1c5f` (not a second
remediation attempt).*
