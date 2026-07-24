# Phase 16 M3 — Qualification Smoke Evidence (TC-T01)

**Status:** **NO-GO** — authorized fresh qualification retry session
`m3q-20260724T075320Z-939e94cf` (exactly one; new session ID generated and
recorded before launch). Completed fresh-session/DNS/slirp/inner-allow-block
egress preflight **before** reading one-shot credential. Launched Qwen Code
**exactly once** under the locked write-capable contract
(`--approval-mode yolo`, **24** turns, **240s** wall, remediated auth, fixed
parent-shell reap path, deterministic post-session finalization). Qwen performed
the TC-T01 `FetchData` → `RetrieveData` rename (host-side counts `FetchData=0`,
`RetrieveData=11`; host `dotnet build` / `dotnet test` exit 0) but exited **53**
(`FatalTurnLimitedError`: max session turns). Dual GO criteria require **Qwen
exit 0 and** verified rename; exit 53 forces **NO-GO**. Balance-before USD
**3.94**; balance-after USD **3.94**; session smoke spend **USD 0.00**. Bounded
reap recorded real inner exit **4**; full finalization **completed**
(balance-after, workspace-result.env, cleanup.env). **No** second attempt.
**No** M4. **No** comparative or quality claims.

**Campaign path:** single-candidate observational only
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`).

**Session ID (this grant):** `m3q-20260724T075320Z-939e94cf`

**Artifact root (outside Zaide repository):**

```text
/tmp/phase16-artifacts/phase-16/records/m3-qualification/m3q-20260724T075320Z-939e94cf/
/tmp/phase16-artifacts/phase-16/records/dns-binding/m3q-20260724T075320Z-939e94cf/
```

**Prior attempts (exhausted grants; not this session):**

1. Session `m3q-20260723T113639Z-b1e764d3` — **NO-GO** at credential gate
   (one-shot file absent). DNS binding + isolation **GO**; Qwen not launched.
2. Session `m3q-20260723T121356Z-1d1e7154` — **NO-GO** at slirp4netns attach
   (inner PID `1` bug); credential consumed; Qwen not launched.
3. Session `m3q-20260723T131730Z-1c8c982f` — **NO-GO** at Bubblewrap
   `/etc/resolv.conf` symlink bind; DNS/slirp/egress **GO**; Qwen not started.
4. Session `m3q-20260723T151034Z-71eea5e4` — same-day partial run; auth-type
   failure pattern under artifact root.
5. Session `m3q-20260723T151512Z-6996af5f` — **NO-GO** at Qwen auth-type
   missing (`qwen_exit=1`, 0 tokens); pre-launch DNS/slirp/egress/tmpfs **GO**;
   spend **USD 0.00**. Auth argv remediated afterward
   (`M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md`).
6. Session `m3q-20260723T164355Z-c421b379` — **NO-GO** at Qwen max-session-turns
   under then-locked **5**-turn ceiling (`qwen_exit=53`); TC-T01 incomplete;
   spend not measured; artifacts later lost on host reboot. See historical
   section §11.
7. Session `m3q-20260724T035603Z-2c06e1a4` — **NO-GO** under plan-only mode:
   DNS/slirp/egress/tmpfs/auth argv **GO**; `qwen_exit=0` with 0 lines
   changed; TC-T01 incomplete; spend owner-reported less than USD 0.01;
   orchestrator external timeout before post-balance. Write-capable
   remediation followed (`M3_WRITE_CAPABLE_REMEDIATION_EVIDENCE.md`).
8. Session `m3q-20260724T054307Z-481ad1de` — **NO-GO** under write-capable yolo
   with historical **60s** wall: DNS/slirp/egress/tmpfs/auth argv **GO**;
   TC-T01 rename **verified**; **`qwen_exit=55`** (`FatalBudgetExceededError`
   wall 60s); spend balance delta **USD 0.00**; post-exit finalization ran
   (ledger `inner_qualification` exit **127** from subshell wait defect later
   remediated). See historical section §12.
9. Session `m3q-20260724T060109Z-45dd1c5f` — **NO-GO** under then-locked
   **12**-turn / **120s** wall: DNS/slirp/egress GO; yolo; rename verified;
   `qwen_exit=53` (turn limit); delta USD 0.00; finalization ran with inner 4.
10. Session `m3q-20260724T072341Z-8f567943` — **NO-GO** under then-locked
    **24**-turn / **120s** wall: preflight GO before credential; yolo; rename
    verified; **`qwen_exit=55`** (wall 120s); balance-before USD 3.95 (after
    unavailable); inner exit 4; finalization incomplete (sticky `set -e`). See
    historical section §13. Active wall later raised to **240s** (future-policy
    only; not a retry).

**Consumed-but-unlaunched operator event (separate grant; no session record):**

11. ~2026-07-24T06:31:49Z — authorized fresh **24-turn** grant consumed a new
    one-shot sub-key but **did not** create a matching `m3-qualification`
    session artifact and **did not** launch Qwen. Reporting incorrectly reused
    historical session `m3q-20260724T060109Z-45dd1c5f` / `qwen_exit=53`.
    Remediation: `M3_FRESH_SESSION_ELIGIBILITY_REMEDIATION_EVIDENCE.md`.

---

## 1. Scope and Non-Effects

| Authorized in this grant | Performed |
|---|---|
| Execute DNS binding gate immediately before launch | **Yes** |
| slirp4netns attach via host-visible `UNSHARE_PID` | **Yes** — `sent tapfd` confirmed |
| Inner egress reprobe (allow + block) | **Yes** (preflight before credential; also inside launch netns) |
| Bubblewrap isolation re-check | **Yes** |
| Materialize TC-T01 synthetic workspace | **Yes** |
| Read dedicated one-shot sub-key via orchestrator only | **Yes** — mode `600`, size `36` bytes; consumed and deleted after preflight |
| Inject only `DEEPSEEK_API_KEY` (no `~/.config` / ambient) | **Yes** |
| Launch Qwen Code (TC-T01, yolo, JSON, 24 turns, **240s**) | **Yes — once** — locked auth + write-capable argv; `qwen_exit=53` |
| USD 1 smoke / USD 3 cumulative spend | Caps **not breached**; balance-before/after USD **3.94** / **3.94**; delta **USD 0.00** |
| Verify TC-T01 `FetchData` → `RetrieveData` | **Yes** (host-side) — rename verified; build/test 0 |
| Bounded post-exit reap/finalization (fixed parent-shell path) | **Yes** — real inner exit **4**; balance-after, workspace-result, cleanup recorded |

| Forbidden | Status |
|---|---|
| Second attempt / retry under this grant | **Not performed** |
| M4 / comparative / quality claims | **Not performed** |
| `src/` production changes | **Not performed** |
| Credential value in evidence or git | **Not performed** |

---

## 2. Pre-Launch Gate Results

| Step | Gate | Result |
|---|---|---|
| Grant env | `PHASE16_M3_QUALIFICATION_GRANT=1` | **PASS** |
| Tool inventory | bwrap, slirp4netns, nft, curl, unshare, getent, dotnet | **PASS** |
| Pinned Qwen binary present (A-02) | `.../inspect/qwen-code/bin/qwen` | **PASS** |
| DNS resolution (D-01–D-04) | `getent ahostsv4 api.deepseek.com` → single IPv4 | **PASS** — `3.173.21.63` |
| Hosts injection (D-06) | sandbox-only `/etc/hosts` map | **PASS** |
| nft allowlist text (D-07) | `3.173.21.63/32:443` | **PASS** |
| Triple consistency (D-05) | FRESH/HOSTS/NFT IPv4 equal | **PASS** — `CONSISTENT=YES` |
| TC-T01 workspace | materialized under artifact root | **PASS** |
| Bubblewrap isolation pre-check | workspace write + host write denial | **PASS** |
| Dedicated sub-key (C-04 / A-09) | one-shot file mode `600`, size `36`, consumed after preflight | **PASS** |
| Balance / cost tracking | DeepSeek `/user/balance` before + after | **PASS** — USD **3.94** before; USD **3.94** after |
| slirp4netns attach | host-visible `UNSHARE_PID` + tapfd handoff | **PASS** |
| Inner egress preflight | allowlisted TLS + non-allowlisted block | **PASS** — allow body `Authentication Fails (governor)`; block curl exit 28 (timeout) |
| Bubblewrap `/etc` setup | `--tmpfs /etc` + ro-bind hosts + resolv-empty | **PASS** (process entered sandbox) |
| Qwen headless run | TC-T01, locked auth argv, yolo, JSON, 24 turns, **240s** | **FAIL exit** — `qwen_exit=53` turn limit (`FatalTurnLimitedError`) |
| TC-T01 task completion | `FetchData` → `RetrieveData` rename | **PASS** (host) — `FetchData` count 0; `RetrieveData` count 11; host build/test 0 |
| Orchestrator finalization | post-balance, workspace, cleanup | **PASS** — all recorded after non-zero Qwen |

**DNS binding execution verdict:** **GO** (A-14 sequence complete through
triple-consistency and inner allow/block preflight; `BOUND_IPV4=3.173.21.63`;
operator-finalized `BINDING_VERDICT=GO` after script stopped on non-zero Qwen
exit before writing the DNS GO line; `BINDING_VERDICT=PENDING` remains on disk).

**Qualification verdict:** **NO-GO** — verified TC-T01 rename is insufficient
when Qwen exit is not 0.

---

## 3. Locked Smoke argv (executed)

Executable (pinned): `/tmp/phase16-artifacts/phase-16/artifacts/qwen-code/v0.20.1/inspect/qwen-code/bin/qwen`

Argv tail **executed in this session** (locked `Phase16M3QualificationPolicy` +
orchestrator wall-time / write-capable / finalization remediations):

```text
--auth-type openai
--openai-base-url https://api.deepseek.com
--approval-mode yolo
--model deepseek-v4-flash
--output-format json
--max-session-turns 24
--max-wall-time 240s
```

Environment allowlist: **`DEEPSEEK_API_KEY` only** (A-07). Workspace fixture
included `.qwen/settings.json` `modelProviders.openai[]` with
`envKey: DEEPSEEK_API_KEY` and `baseUrl: https://api.deepseek.com` for
`deepseek-v4-flash` (`M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md`).

Prompt source: materialized `TC-T01` prompt (rename `FetchData` → `RetrieveData`).

Exact argv recorded under the session artifact root (`exact-argv.txt`).

**Auth remediation status:** auth-type failure mode remains **cleared**.
**Write-capable remediation status:** `--approval-mode yolo` used as locked.
**Wall-time remediation status:** active lock **240s** used for this session.
Failure mode for **this** session is **turn-limit exit 53**
(`FatalTurnLimitedError` at 24 turns) after verified rename — not wall-budget
exit 55, not auth-type missing, not plan-only.

---

## 4. Key Lifecycle (no value disclosed)

| Field | Recorded value |
|---|---|
| `key_source` | `phase16_one_shot_file` |
| `key_file_path` | `/tmp/phase16-artifacts/phase-16/credentials/subkey.once` |
| Pre-run metadata only | mode `600`, size `36` bytes (value never inspected/logged by operator) |
| Credential material persisted | **NO** |
| `file_consumed` | **YES** — absent after orchestrator read |
| `value_disclosed` | **NO** |
| Ambient / `~/.config` credentials | **Not read** |
| Stop reason | `qwen_launch_failed exit=53` (orchestrator fatal.txt / `stop_with`) |

---

## 5. Commands Ledger (excerpt)

```text
m3q-20260724T075320Z-939e94cf step=tool_inventory exit=0 utc=2026-07-24T07:53:20Z
m3q-20260724T075320Z-939e94cf step=qwen_binary_present exit=0 utc=2026-07-24T07:53:20Z
m3q-20260724T075320Z-939e94cf step=dns_resolution exit=0 utc=2026-07-24T07:53:20Z
m3q-20260724T075320Z-939e94cf step=dns_triple_consistency exit=0 utc=2026-07-24T07:53:20Z
m3q-20260724T075320Z-939e94cf step=materialize_tc_t01 exit=0 utc=2026-07-24T07:53:20Z
m3q-20260724T075320Z-939e94cf step=isolation_bwrap_precheck exit=0 utc=2026-07-24T07:53:20Z
m3q-20260724T075320Z-939e94cf step=slirp4netns_attach exit=0 utc=2026-07-24T07:53:20Z
m3q-20260724T075320Z-939e94cf step=egress_preflight exit=0 utc=2026-07-24T07:53:29Z
m3q-20260724T075320Z-939e94cf step=credential_load_one_shot exit=0 utc=2026-07-24T07:53:29Z
m3q-20260724T075320Z-939e94cf step=balance_before exit=0 utc=2026-07-24T07:53:29Z
m3q-20260724T075320Z-939e94cf step=slirp4netns_attach exit=0 utc=2026-07-24T07:53:29Z
m3q-20260724T075320Z-939e94cf step=inner_qualification exit=4 utc=2026-07-24T07:55:10Z
m3q-20260724T075320Z-939e94cf step=balance_after exit=0 utc=2026-07-24T07:55:10Z
m3q-20260724T075320Z-939e94cf step=workspace_tc_t01_check exit=0 utc=2026-07-24T07:55:10Z
```

`inner_qualification` exit **4** is the real unshare/inner script exit after
Qwen non-zero (`qwen_exit=53` → inner `exit 4`). Parent-shell
`wait_inner_with_reap_budget` recorded `inner_wait_status=exited` /
`inner_wait_exit=4` / `inner_wait_elapsed_sec=101` without inventing bash
**127**. Outer overall budget (320s) and post-qwen budget (45s) were **not**
hit.

---

## 6. Qwen Result (redacted)

`run/qwen-result.env`: `qwen_exit=53`

`run/qwen.stdout`: empty (0 bytes)

`run/qwen.stderr` (redacted; no credential material):

| Field | Value |
|---|---|
| YOLO headless warning | Present (host Bubblewrap is campaign isolation; Qwen’s own sandbox unset) |
| Error type | **`FatalTurnLimitedError`** |
| Message | Reached max session turns for this session. Increase the number of turns by specifying maxSessionTurns in settings.json. |
| Code | **53** |

Workspace verification after exit (host-side; orchestrator
`workspace-result.env` **written**):

| Check | Value |
|---|---|
| `FetchData` occurrences in `*.cs` | **0** |
| `RetrieveData` occurrences in `*.cs` | **11** |
| `tc_t01_rename_verified` | **YES** (host counts; dual GO still fails on exit ≠ 0) |

Post-session host verify (operator; not a second Qwen launch):

| Check | Value |
|---|---|
| `dotnet build Tuning.T01.slnx --no-incremental` | exit **0** |
| `dotnet test Tuning.T01.slnx --no-build` | exit **0** (3 passed) |

Inner verify (`verify-result.env`) was **not** written because the inner script
exits on non-zero Qwen before the bounded verify block; host-side workspace and
operator build/test cover the rename outcome.

---

## 7. Root Cause

### 7.1 Cleared blockers (prior grants / this grant)

| Prior failure | This grant |
|---|---|
| Auth type not selected | **Cleared** — `--auth-type openai` + `--openai-base-url` + modelProviders present |
| slirp4netns host PID | **PASS** (`UNSHARE_PID` / tapfd) |
| Bubblewrap `/etc/resolv.conf` symlink | **PASS** (`--tmpfs /etc` + ro-bind resolv-empty) |
| DNS triple-consistency / egress | **PASS** (preflight before credential) |
| Plan-only without mutation | **Cleared** — write-capable `--approval-mode yolo`; rename verified |
| Wall-budget exit 55 under 120s | **Not this session** — active **240s** wall; failure is turn-limit exit 53 |
| Orchestrator post-exit hang / synthetic 127 | **Cleared** — same-shell wait; real inner exit **4** recorded |
| Post-session finalization after non-zero Qwen | **Cleared** — balance-after / workspace / cleanup completed |

### 7.2 Turn-limit exit 53 after verified TC-T01 mutation (this grant)

Qwen Code v0.20.1 under locked yolo mode mutated the TC-T01 workspace to the
required rename (host: `FetchData` 0 / `RetrieveData` 11; build/test 0), but the
process then aborted with **`FatalTurnLimitedError`** because the locked
**24**-turn session ceiling was reached (`qwen_exit=53`). Dual GO criteria
require **both** Qwen exit 0 **and** verified rename; therefore the session is
**NO-GO** despite the verified workspace change.

Timing evidence rules out outer orchestrator budgets and wall time as the
exit-53 cause:

| Budget | Configured | Observed |
|---|---|---|
| Qwen `--max-session-turns` | **24** | **Hit** — `FatalTurnLimitedError` code 53 |
| Qwen `--max-wall-time` | **240s** | **Not hit** — no `FatalBudgetExceededError` |
| Inner overall wait | **320s** | Not hit — `inner_wait_status=exited`, elapsed **101s** |
| Post-qwen verify | **45s** | Not hit — status is not `post_qwen_budget_exceeded` |

This is a **Qwen turn-limit** failure against locked GO rules, not a wall-time,
auth, DNS, egress, or plan-mode failure. No second attempt was authorized under
this grant to raise turns or re-run. **24-turn / 240s policy was unchanged**
by this session.

### 7.3 Bounded reaping and finalization (this grant)

`wait_inner_with_reap_budget` ran in the **parent shell** (not under `$(...)`)
and recorded:

```text
inner_wait_status=exited
inner_wait_exit=4
inner_wait_elapsed_sec=101
inner_overall_timeout_sec=320
post_qwen_verify_budget_sec=45
inner_reaped_exit=
```

Real unshare/inner exit **4** was preserved (inner script exits 4 when Qwen
non-zero). No synthetic bash **127**.

**Finalization:** after logging `inner_qualification exit=4`,
`launch_netns_inner` returned **0** and published `INNER_WAIT_EXIT=4` (post-session
finalization remediation). Balance-after, workspace-result, cleanup.env, and
`stop_with` summary append all ran:

| Artifact | Result |
|---|---|
| `balance-after.json` | USD **3.94** (exit 0) |
| `workspace-result.env` | `tc_t01_rename_verified=YES` |
| `cleanup.env` | `orphan_detected=NO`, `cleanup_status=reaped` |
| `summary.env` | `QUALIFICATION_VERDICT=NO-GO`, `STOP_REASON=qwen_launch_failed exit=53` |

**Future grant requirements (out of scope; not applied under this grant):**

1. New dedicated one-shot sub-key (this key consumed).
2. New qualification grant + new session ID.
3. Keep locked write-capable yolo + **240** turns + **800s** wall + fixed
   reap/finalization path (active policy per `M3_EXTENDED_SINGLE_SMOKE_POLICY_REMEDIATION_EVIDENCE.md`;
   extended single-smoke exception for **one** retry only).
4. Gate **GO** only on Qwen exit 0 **and** verified TC-T01 workspace change.
5. Preserve session records outside `/tmp` before any host reboot.

---

## 8. Spend

| Metric | Value |
|---|---|
| Balance before | USD **3.94** |
| Balance after | USD **3.94** |
| Session smoke spend (USD) | **0.00** (balance delta) |
| Prior campaign ledger | **USD 0.01** (prior sessions) |
| Phase 16 cumulative spend (ledger) | **USD 0.01** (unchanged; this session delta 0.00) |
| M3 smoke cap (USD 1) / campaign cap (USD 3) | **Not breached** |

Spend basis: authenticated DeepSeek `/user/balance` before and after under the
one-shot key in memory. No credential material recorded. No USD field from Qwen
JSON (stdout empty).

---

## 9. GO / NO-GO

### 9.1 This qualification attempt

**NO-GO.** Fresh-session, DNS binding, slirp4netns attach, inner egress
reprobes (allow+block), isolation pre-check, credential gate (after preflight),
Bubblewrap `/etc` setup, locked auth argv, write-capable yolo mode, **24** turns
/**240s** wall argv **passed**. Key read once and deleted immediately after
preflight; only `DEEPSEEK_API_KEY` injected. Qwen Code **started exactly once**,
performed the rename `FetchData` → `RetrieveData` (host verified: counts 0/11;
build/test 0), then exited **53** on turn limit. Dual GO criteria fail on
non-zero exit. Spend: balance-before/after USD 3.94 / 3.94; delta **USD 0.00**.
Post-launch reap recorded real inner exit **4**. Orchestrator finalization
**completed** (balance-after, workspace, cleanup).

Candidate remains **`eligible for later M3 qualification`** but **not
qualified**. Do **not** proceed to M4. Do **not** retry under this grant.

### 9.2 Future retry requirements (out of scope for this evidence file)

1. Human provisions a **new** dedicated DeepSeek sub-key one-shot file.
2. Re-issue a **new** qualification grant with a **new** session ID.
3. Keep locked write-capable `--approval-mode yolo` + **240** turns + **800s**
   wall + fixed reap/finalization path (active policy per
   `M3_EXTENDED_SINGLE_SMOKE_POLICY_REMEDIATION_EVIDENCE.md`; extended
   single-smoke exception for **one** retry only).
4. Gate **GO** only on Qwen exit 0 **and** verified TC-T01 workspace change.
5. **No second attempt** was authorized or performed under this grant. **No retry**
   occurred in the extended single-smoke policy slice.

---

## 10. Cross-References

- `M3_DNS_BINDING_GATE.md` — binding rules executed for session above
- `M3A_ACQUISITION_EVIDENCE.md` — pinned binary; auth support surface
- `M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md` — argv/modelProviders contract used
- `M3_WRITE_CAPABLE_REMEDIATION_EVIDENCE.md` — write-capable argv + reap path
- `M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md` — prior 120s wall + same-shell reap
- `M3_WALL_TIME_240S_POLICY_REMEDIATION_EVIDENCE.md` — then-active 240s wall used by this session
- `M3_EXTENDED_SINGLE_SMOKE_POLICY_REMEDIATION_EVIDENCE.md` — later active 240-turn / 800s wall (future-policy; not a retry)
- `M3_POST_SESSION_FINALIZATION_REMEDIATION_EVIDENCE.md` — sticky set -e finalization fix exercised successfully here
- `M3_FRESH_SESSION_ELIGIBILITY_REMEDIATION_EVIDENCE.md` — fresh session ID + preflight-before-credential
- `M3_EGRESS_PROOF_EVIDENCE.md` — egress architecture
- `TASK_CORPUS.md` — TC-T01 definition
- `IMPLEMENTATION_PLAN.md` — phase status

---

## 11. Historical note — session `m3q-20260724T035603Z-2c06e1a4`

Prior authorized plan-mode session: DNS/slirp/egress/tmpfs/auth argv **GO**;
Qwen once then **`qwen_exit=0`** in plan mode with **0** lines changed; TC-T01
incomplete; spend owner-reported less than USD 0.01; orchestrator external
timeout before post-balance. Superseded as **latest** by later write-capable
sessions.

---

## 12. Historical note — session `m3q-20260724T054307Z-481ad1de`

Prior authorized write-capable yolo session under **60s** wall lock:
DNS/slirp/egress/tmpfs/auth argv **GO**; Qwen once; TC-T01 rename **verified**
(`FetchData` 0 / `RetrieveData` 11; host build/test 0); **`qwen_exit=55`**
(`FatalBudgetExceededError` wall 60s); spend balance delta **USD 0.00**
(3.97→3.97); ledger `inner_qualification` exit **127** (subshell wait defect;
finalization still ran). Wall raised to **120s** and reaping fixed afterward
(`M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md`).

---

## 13. Historical note — session `m3q-20260724T072341Z-8f567943`

Prior authorized fresh write-capable yolo session under then-locked **24** turns
/**120s** wall: preflight GO before credential; Qwen once; TC-T01 rename
**verified**; **`qwen_exit=55`** (`FatalBudgetExceededError` wall 120s);
balance-before USD 3.95 (after unavailable); inner exit 4; finalization
incomplete (sticky `set -e`). Active wall later raised to **240s**; finalization
path remediated afterward. Session remains historical **NO-GO**. Superseded as
**latest** by `m3q-20260724T075320Z-939e94cf` under this grant.

---

*M3 qualification smoke evidence — session `m3q-20260724T075320Z-939e94cf`.
Observational only. Exactly one fresh M3 retry. NO-GO: Qwen exit 53 (turn limit
24) after verified TC-T01 rename. Preflight GO before credential; key
consumed+deleted post-preflight only. 24 turns/240s/yolo. Finalization complete
(balance delta USD 0.00; cleanup reaped). No second attempt. No M4. No
comparative or quality claims.*
