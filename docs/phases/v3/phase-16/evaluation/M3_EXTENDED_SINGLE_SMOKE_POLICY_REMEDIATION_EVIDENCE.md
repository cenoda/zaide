# Phase 16 M3 — Extended Single-Smoke Policy Remediation Evidence

**Status:** **Complete (future-policy remediation only)** — repository policy,
smoke orchestrator, focused tests, and active campaign docs updated. Remediation
itself did **not** create credentials, launch Qwen/Node, call APIs, reprobe
egress, acquire artifacts, or run an M3 retry / M4 work.

**Human decisions (this slice):**

1. The next possible TC-T01 qualification retry may use **240** session turns
   (`--max-session-turns 240`) and an **800-second** wall-time limit
   (`--max-wall-time 800s`).
2. Smoke cap remains **USD 1**; Phase 16 cumulative cap remains **USD 3**.
3. This is an **explicitly approved extended single-smoke exception**, not a
   general relaxation and **not** authorization for multiple attempts.
4. Historical sessions remain unchanged, including the latest 24-turn / 240s
   NO-GO session `m3q-20260724T075320Z-939e94cf`.
5. **No retry occurred in this slice.** A future retry still requires a **new**
   one-shot key and an **explicit** qualification grant.

---

## 1. Scope

| Authorized | Performed |
|---|---|
| Update every active policy/orchestrator/test/doc surface to 240 turns / 800s wall | **Yes** |
| Raise outer orchestration budget so 800s Qwen wall + preflight + finalization are not truncated first | **Yes** |
| Preserve historical session records unchanged (including 24/240s NO-GO) | **Yes** |
| Preserve fixed deterministic finalization/reaping path; prove with 800s argv tests | **Yes** |
| Preserve other locks (TC-T01, yolo, auth, DNS, egress, key lifecycle, USD caps) | **Yes** |
| Focused regression tests for 240-turn / 800s argv + finalization/reaping behavior | **Yes** |
| Qualification retry / credential / Qwen launch / M4 | **No** |

This slice is **solely** a future-policy remediation for **one** possible
extended qualification retry, **not** a qualification retry and **not** a
reclassification of any historical session.

---

## 2. Turn / wall-time lock (active)

Locked smoke argv tail (policy + orchestrator):

```text
--auth-type openai
--openai-base-url https://api.deepseek.com
--approval-mode yolo
--model deepseek-v4-flash
--output-format json
--max-session-turns 240
--max-wall-time 800s
```

| Property | Locked value |
|---|---|
| Session turns | **240** via `--max-session-turns 240` |
| Wall time | **800s** via `--max-wall-time 800s` |
| Smoke / cumulative spend | **USD 1** / **USD 3** (unchanged) |
| Approval mode | **`yolo`** (unchanged) |
| Task / candidate | **TC-T01** / **qwen-code** only (unchanged) |
| GO criteria | Qwen exit **0** **and** verified TC-T01 rename (unchanged) |
| Retry authorization | **One** future attempt only; requires new grant + one-shot key |

Historical session `m3q-20260724T075320Z-939e94cf` executed under **24** turns
and **240s** wall and must not be rewritten; its `qwen_exit=53` /
turn-limit evidence stays in `M3_QUALIFICATION_EVIDENCE.md`. Earlier sessions
remain at their then-locked ceilings (12/120s, 24/120s, 12/60s, etc.).

Orchestrator overall inner budget default raised from **320s** to **880s** so
mandatory DNS/egress preflight + **800s** Qwen wall + post-qwen verify budget
can complete without the outer wait forcing a premature reap before Qwen
finishes. Same-shell wait/reap and `launch_netns_inner` always-return-0
finalization path are **preserved** unchanged (prior remediations).

---

## 3. Repository changes

| Path | Change |
|---|---|
| `tools/.../Phase16M3QualificationPolicy.cs` | `MaxSessionTurns = 240`; `MaxWallTime = 800s` |
| `tools/.../Scripts/m3-qualification-smoke.sh` | 240-turn / 800s argv; overall budget 880s; finalization/reap path preserved |
| `tests/.../Phase16M3QualificationPolicyTests.cs` | assert 240 / 800s; reject legacy 12 / 24 turns and 60s / 120s / 240s wall |
| `tests/.../Phase16M3QualificationSmokeOrchestratorTests.cs` | 240-turn / 800s argv contract + overall 880s; finalization/reap regressions retained |
| Phase 16 evaluation docs + plan + roadmap active locks | 240-turn / 800s active ceiling; historical sessions keep prior ceilings |

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

- `M3_QUALIFICATION_EVIDENCE.md` — historical 24-turn / 240s exit 53 after verified rename (session remains NO-GO)
- `M3_WALL_TIME_240S_POLICY_REMEDIATION_EVIDENCE.md` — prior active 240s wall (superseded as active lock)
- `M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md` — prior 120s wall + same-shell reap (127 fix)
- `M3_POST_SESSION_FINALIZATION_REMEDIATION_EVIDENCE.md` — sticky set -e finalization fix
- `CAMPAIGN_LOCK.md` — active campaign ceilings
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 extended single-smoke policy remediation — repository wiring complete;
not a qualification retry; historical session
m3q-20260724T075320Z-939e94cf remains NO-GO at its then-locked 24-turn /
240s wall. The authorized one future retry was later executed as session
`m3q-20260724T081819Z-7db401c3` (NO-GO, exit 55 at 800s wall) and the
exception is consumed; see `M3_QUALIFICATION_EVIDENCE.md`.*
