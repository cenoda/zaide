# Phase 16 M3 — Wall-Time (240s) Future-Policy Remediation Evidence

**Status:** **Complete (future-policy remediation only)** — repository policy,
smoke orchestrator, focused tests, and active campaign docs updated. Remediation
itself did **not** create credentials, launch Qwen/Node, call APIs, reprobe
egress, acquire artifacts, or run an M3 retry / M4 work.

**Human decisions (this slice):**

1. A future TC-T01 qualification retry may use a **240-second** wall-time
   limit (`--max-wall-time 240s`).
2. Max session turns remains **24**; USD **1** smoke and USD **3** cumulative
   caps remain unchanged.
3. Latest session `m3q-20260724T072341Z-8f567943` remains historical **NO-GO**:
   Qwen exit **55** was caused specifically by its then-locked **120s** wall
   limit, despite verified TC-T01 rename. Historical session records stay at
   120s (and earlier 60s sessions stay at 60s).

---

## 1. Scope

| Authorized | Performed |
|---|---|
| Update every active policy/orchestrator/test/doc surface from 120s → 240s | **Yes** |
| Preserve historical 120s (and 60s) session records unchanged | **Yes** |
| Preserve fixed deterministic finalization/reaping path; prove with 240s argv tests | **Yes** |
| Preserve other locks (TC-T01, yolo, auth, DNS, egress, key lifecycle, 24 turns, USD caps) | **Yes** |
| Focused regression tests for 240s argv + finalization/reaping behavior | **Yes** |
| Qualification retry / credential / Qwen launch / M4 | **No** |

This slice is **solely** a future-policy remediation, **not** a qualification
retry and **not** a reclassification of any historical session.

---

## 2. Wall-time lock (active)

Locked smoke argv tail (policy + orchestrator):

```text
--auth-type openai
--openai-base-url https://api.deepseek.com
--approval-mode yolo
--model deepseek-v4-flash
--output-format json
--max-session-turns 24
--max-wall-time 240s
```

| Property | Locked value |
|---|---|
| Wall time | **`240s`** via `--max-wall-time 240s` |
| Session turns | **24** (unchanged) |
| Smoke / cumulative spend | **USD 1** / **USD 3** (unchanged) |
| Approval mode | **`yolo`** (unchanged) |
| Task / candidate | **TC-T01** / **qwen-code** only (unchanged) |
| GO criteria | Qwen exit **0** **and** verified TC-T01 rename (unchanged) |

Historical session `m3q-20260724T072341Z-8f567943` executed under **120s** and
must not be rewritten; its `qwen_exit=55` / wall-120s evidence stays in
`M3_QUALIFICATION_EVIDENCE.md`. Earlier session
`m3q-20260724T054307Z-481ad1de` remains at **60s**.

Orchestrator overall inner budget default raised from **200s** to **320s** so
probes + 240s Qwen wall + post-qwen verify budget can complete without the
outer wait forcing a premature reap. Same-shell wait/reap and
`launch_netns_inner` always-return-0 finalization path are **preserved**
unchanged (prior remediations).

---

## 3. Repository changes

| Path | Change |
|---|---|
| `tools/.../Phase16M3QualificationPolicy.cs` | `MaxWallTime = 240s` |
| `tools/.../Scripts/m3-qualification-smoke.sh` | 240s argv; overall budget 320s; finalization/reap path preserved |
| `tests/.../Phase16M3QualificationPolicyTests.cs` | assert 240s; reject legacy 60s and 120s |
| `tests/.../Phase16M3QualificationSmokeOrchestratorTests.cs` | 240s argv contract + overall 320s; finalization/reap regressions retained |
| Phase 16 evaluation docs + plan + roadmap active locks | 240s active ceiling; historical sessions keep 120s/60s |

---

## 4. Verification (this slice)

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
git diff --check
```

Qualification smoke execution remains **out of scope**.

---

## 5. Cross-References

- `M3_QUALIFICATION_EVIDENCE.md` — historical 120s wall exit 55 after verified rename (session remains NO-GO)
- `M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md` — prior 120s wall + same-shell reap (127 fix)
- `M3_POST_SESSION_FINALIZATION_REMEDIATION_EVIDENCE.md` — sticky set -e finalization fix
- `CAMPAIGN_LOCK.md` — active campaign ceilings
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 wall-time (240s) future-policy remediation — repository wiring complete;
not a qualification retry; historical session
m3q-20260724T072341Z-8f567943 remains NO-GO at its then-locked 120s wall.*
