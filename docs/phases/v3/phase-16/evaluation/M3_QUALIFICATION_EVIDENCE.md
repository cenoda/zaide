# Phase 16 M3 — Qualification Smoke Evidence (TC-T01)

**Status:** **NO-GO** — authorized fresh qualification retry session
`m3q-20260724T054307Z-481ad1de` completed pre-launch gates and launched Qwen
Code **exactly once** under the locked write-capable contract
(`--approval-mode yolo`, 12 turns, 60s wall, remediated auth). Qwen performed
the TC-T01 `FetchData` → `RetrieveData` rename (host-side counts
`FetchData=0`, `RetrieveData=11`; host `dotnet build`/`dotnet test` exit 0)
but exited **55** (`FatalBudgetExceededError`: wall-clock budget of **60s**
exceeded). Dual GO criteria require **Qwen exit 0 and** verified rename;
exit 55 alone forces **NO-GO**. Session smoke spend from balance delta
**USD 0.00** (before and after both **USD 3.97**). Bounded post-exit
reap/finalization **ran** (balance-after, workspace-result, cleanup) without
orchestrator hang. **No** second attempt. **No** M4. **No** comparative or
quality claims.

**Campaign path:** single-candidate observational only
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`).

**Session ID (this grant):** `m3q-20260724T054307Z-481ad1de`

**Artifact root (outside Zaide repository):**

```text
/tmp/phase16-artifacts/phase-16/records/m3-qualification/m3q-20260724T054307Z-481ad1de/
/tmp/phase16-artifacts/phase-16/records/dns-binding/m3q-20260724T054307Z-481ad1de/
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

---

## 1. Scope and Non-Effects

| Authorized in this grant | Performed |
|---|---|
| Execute DNS binding gate immediately before launch | **Yes** |
| slirp4netns attach via host-visible `UNSHARE_PID` | **Yes** — `sent tapfd` confirmed |
| Inner egress reprobe (allow + block) | **Yes** |
| Bubblewrap isolation re-check | **Yes** |
| Materialize TC-T01 synthetic workspace | **Yes** |
| Read dedicated one-shot sub-key via orchestrator only | **Yes** — mode `600`, size `36` bytes; consumed and deleted |
| Inject only `DEEPSEEK_API_KEY` (no `~/.config` / ambient) | **Yes** |
| Launch Qwen Code (TC-T01, yolo, JSON, 12 turns, 60s) | **Yes — once** — locked auth + write-capable argv; `qwen_exit=55` |
| USD 1 smoke / USD 3 cumulative spend | Caps **not breached**; session balance delta **USD 0.00** |
| Verify TC-T01 `FetchData` → `RetrieveData` | **Yes** — rename verified; host build/test exit 0 |
| Bounded post-exit reap/finalization | **Yes** — balance-after, workspace-result, cleanup recorded |

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
| Dedicated sub-key (C-04 / A-09) | one-shot file mode `600`, size `36`, consumed after read | **PASS** |
| Balance / cost tracking | DeepSeek `/user/balance` before | **PASS** — USD **3.97** before |
| slirp4netns attach | host-visible `UNSHARE_PID` + tapfd handoff | **PASS** (`sent tapfd=5 for tap0`) |
| Inner egress reprobe | allowlisted TLS + non-allowlisted block | **PASS** — allow body `Authentication Fails (governor)`; block curl exit 28 (timeout) |
| Bubblewrap `/etc` setup | `--tmpfs /etc` + ro-bind hosts + resolv-empty | **PASS** (process entered sandbox) |
| Qwen headless run | TC-T01, locked auth argv, yolo, JSON, 12 turns, 60s | **FAIL exit** — `qwen_exit=55` wall budget (`FatalBudgetExceededError`) |
| TC-T01 task completion | `FetchData` → `RetrieveData` rename | **PASS** — `FetchData` count 0; `RetrieveData` count 11; host build/test 0 |
| Orchestrator finalization | post-balance, workspace, cleanup | **PASS** — completed without hang (`balance_after` exit 0; `cleanup_status=reaped`) |

**DNS binding execution verdict:** **GO** (A-14 sequence complete through
triple-consistency and inner allow/block reprobes; `BOUND_IPV4=3.173.21.63`;
operator-finalized `BINDING_VERDICT=GO` after script stopped on non-zero Qwen
exit before writing the DNS GO line).

**Qualification verdict:** **NO-GO** — verified TC-T01 rename is insufficient
when Qwen exit is not 0.

---

## 3. Locked Smoke argv (executed)

Executable (pinned): `/tmp/phase16-artifacts/phase-16/artifacts/qwen-code/v0.20.1/inspect/qwen-code/bin/qwen`

Argv tail **executed in this session** (locked `Phase16M3QualificationPolicy` +
orchestrator write-capable remediation):

```text
--auth-type openai
--openai-base-url https://api.deepseek.com
--approval-mode yolo
--model deepseek-v4-flash
--output-format json
--max-session-turns 12
--max-wall-time 60s
```

Environment allowlist: **`DEEPSEEK_API_KEY` only** (A-07). Workspace fixture
included `.qwen/settings.json` `modelProviders.openai[]` with
`envKey: DEEPSEEK_API_KEY` and `baseUrl: https://api.deepseek.com` for
`deepseek-v4-flash` (`M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md`).

Prompt source: materialized `TC-T01` prompt (rename `FetchData` → `RetrieveData`).

Exact argv recorded under the session artifact root (`exact-argv.txt`).

**Auth remediation status:** auth-type failure mode remains **cleared**.
**Write-capable remediation status:** `--approval-mode yolo` used as locked.
Failure mode for this session is **wall-time exit 55 after verified rename**,
not auth-type missing, not plan-only, and not turn-limit exit 53.

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
| Stop reason | `qwen_launch_failed exit=55` (orchestrator label; root cause wall budget) |

---

## 5. Commands Ledger (excerpt)

```text
m3q-20260724T054307Z-481ad1de step=tool_inventory exit=0 utc=2026-07-24T05:43:07Z
m3q-20260724T054307Z-481ad1de step=qwen_binary_present exit=0 utc=2026-07-24T05:43:07Z
m3q-20260724T054307Z-481ad1de step=dns_resolution exit=0 utc=2026-07-24T05:43:07Z
m3q-20260724T054307Z-481ad1de step=dns_triple_consistency exit=0 utc=2026-07-24T05:43:07Z
m3q-20260724T054307Z-481ad1de step=materialize_tc_t01 exit=0 utc=2026-07-24T05:43:07Z
m3q-20260724T054307Z-481ad1de step=isolation_bwrap_precheck exit=0 utc=2026-07-24T05:43:07Z
m3q-20260724T054307Z-481ad1de step=credential_load_one_shot exit=0 utc=2026-07-24T05:43:07Z
m3q-20260724T054307Z-481ad1de step=balance_before exit=0 utc=2026-07-24T05:43:07Z
m3q-20260724T054307Z-481ad1de step=slirp4netns_attach exit=0 utc=2026-07-24T05:43:07Z
m3q-20260724T054307Z-481ad1de step=inner_qualification exit=127 utc=2026-07-24T05:44:16Z
m3q-20260724T054307Z-481ad1de step=balance_after exit=0 utc=2026-07-24T05:44:17Z
m3q-20260724T054307Z-481ad1de step=workspace_tc_t01_check exit=0 utc=2026-07-24T05:44:17Z
```

`inner_qualification` exit **127** is from bounded `wait` reporting “pid is not
a child of this shell” after the unshare child already exited; it is **not** an
orchestrator hang. Finalization still ran immediately afterward.

---

## 6. Qwen Result (redacted)

`run/qwen-result.env`: `qwen_exit=55`

`run/qwen.stdout`: empty (0 bytes)

`run/qwen.stderr` (redacted; no credential material):

| Field | Value |
|---|---|
| YOLO headless warning | Present (host Bubblewrap is campaign isolation; Qwen’s own sandbox unset) |
| Error type | `FatalBudgetExceededError` |
| Message | Run aborted: wall-clock budget of 60s exceeded (`--max-wall-time`) |
| Code | **55** |

Workspace verification after exit (orchestrator host-side):

| Check | Value |
|---|---|
| `FetchData` occurrences in `*.cs` | **0** |
| `RetrieveData` occurrences in `*.cs` | **11** |
| `tc_t01_rename_verified` | **YES** |

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
| DNS triple-consistency / egress | **PASS** |
| Plan-only without mutation | **Cleared** — write-capable `--approval-mode yolo`; rename verified |
| Orchestrator post-exit hang | **Cleared** — balance-after, workspace-result, cleanup recorded; no external 300s timeout |

### 7.2 Wall-time exit 55 after verified TC-T01 mutation (this grant)

Qwen Code v0.20.1 under locked yolo mode mutated the TC-T01 workspace to the
required rename and completed the functional outcome (build/test green), but the
process then aborted with **`FatalBudgetExceededError`** because the locked
**60s** wall-time budget was exceeded (`qwen_exit=55`). Dual GO criteria for
this campaign require **both** Qwen exit 0 **and** verified rename; therefore
the session is **NO-GO** despite the verified workspace change.

This is a **process exit / wall-time** failure against locked GO rules, not an
auth, DNS, egress, or plan-mode failure. No second attempt was authorized under
this grant to raise wall-time or re-run.

### 7.3 Bounded reaping note

`wait_inner_with_reap_budget` recorded `inner_wait_status=exited` with
`inner_wait_exit=127` after ~69s because `wait` on the background unshare PID
returned “not a child of this shell” once the child had already exited. The
path still completed finalization (balance-after exit 0, workspace check,
`cleanup_status=reaped`, `orphan_detected=NO`) and did **not** hang.

**Future grant requirements (out of scope; not applied under this grant):**

1. New dedicated one-shot sub-key (this key consumed).
2. New qualification grant + new session ID.
3. Use active locked wall **120s** and same-shell reap path
   (`M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md`); this session’s **60s** /
   exit-127 ledger remain historical.
4. Gate **GO** only on Qwen exit 0 **and** verified TC-T01 workspace change.
5. Preserve session records outside `/tmp` before any host reboot.

---

## 8. Spend

| Metric | Value |
|---|---|
| Balance before | USD **3.97** |
| Balance after | USD **3.97** |
| Session smoke spend (USD) | **0.00** from balance delta (before = after at API precision) |
| Prior campaign ledger | **USD 0.01** (prior session owner-reported less than USD 0.01, conservative ledger) |
| Phase 16 cumulative spend (ledger) | **USD 0.01** after this session (delta 0.00 added) |
| M3 smoke cap (USD 1) / campaign cap (USD 3) | **Not breached** |

Spend basis: authenticated DeepSeek `/user/balance` before and after under the
same one-shot key in memory. No credential material recorded. No USD field from
Qwen JSON (stdout empty).

---

## 9. GO / NO-GO

### 9.1 This qualification attempt

**NO-GO.** DNS binding, slirp4netns attach, egress reprobes, isolation
re-check, credential gate, Bubblewrap `/etc` setup, locked auth argv, and
write-capable yolo mode **passed**. Qwen Code **started once**, **renamed**
`FetchData` → `RetrieveData` (verified; host build/test green), then exited
**55** on the locked **60s** wall budget. Dual GO criteria fail on non-zero
exit. Spend: **USD 0.00** session balance delta; caps not breached. Post-exit
reap/finalization completed without hang.

Candidate remains **`eligible for later M3 qualification`** but **not
qualified**. Do **not** proceed to M4. Do **not** retry under this grant.

### 9.2 Future retry requirements (out of scope for this evidence file)

1. Human provisions a **new** dedicated DeepSeek sub-key one-shot file.
2. Re-issue a **new** qualification grant with a **new** session ID.
3. Keep locked write-capable `--approval-mode yolo` + reap path.
4. Gate **GO** only on Qwen exit 0 **and** verified TC-T01 workspace change.
5. **No second attempt** was authorized or performed under this grant.

---

## 10. Cross-References

- `M3_DNS_BINDING_GATE.md` — binding rules executed for session above
- `M3A_ACQUISITION_EVIDENCE.md` — pinned binary; auth support surface
- `M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md` — argv/modelProviders contract used
- `M3_WRITE_CAPABLE_REMEDIATION_EVIDENCE.md` — write-capable argv + reap fix
- `M3_EGRESS_PROOF_EVIDENCE.md` — egress architecture
- `TASK_CORPUS.md` — TC-T01 definition
- `IMPLEMENTATION_PLAN.md` — phase status

---

## 11. Historical note — session `m3q-20260724T035603Z-2c06e1a4`

Prior authorized plan-mode session: DNS/slirp/egress/tmpfs/auth argv **GO**;
Qwen once then **`qwen_exit=0`** in plan mode with **0** lines changed; TC-T01
incomplete; spend owner-reported less than USD 0.01; orchestrator external
timeout before post-balance. Superseded as **latest** by
`m3q-20260724T054307Z-481ad1de` under this grant (write-capable yolo).

---

*M3 qualification smoke evidence — session `m3q-20260724T054307Z-481ad1de`.
Observational only. NO-GO: Qwen exit 55 (60s wall budget) after verified TC-T01
rename (`RetrieveData` present; host build/test green). Pre-launch DNS, slirp,
egress, Bubblewrap `/etc`, locked auth, and yolo path GO. Post-exit
finalization completed. No second attempt. No M4. No comparative or quality
claims.*
