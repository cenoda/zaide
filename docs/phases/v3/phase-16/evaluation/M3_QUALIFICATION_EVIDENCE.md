# Phase 16 M3 — Qualification Smoke Evidence (TC-T01)

**Status:** **NO-GO** — authorized fresh qualification retry session
`m3q-20260724T035603Z-2c06e1a4` completed pre-launch gates and launched Qwen
Code **exactly once** under the locked 12-turn / 60s / auth contract. Qwen
exited **0** in `--approval-mode plan` and emitted a plan-only result
(`totalLinesAdded=0`, `totalLinesRemoved=0`). TC-T01 rename **not** performed
(`FetchData` count **11**; `RetrieveData` count **0**). Orchestrator did not
finalize post-balance (outer wait hung; external safety timeout exit **124**
after 300s). **No** second attempt. **No** M4. **No** comparative or quality
claims.

**Campaign path:** single-candidate observational only
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`).

**Session ID (this grant):** `m3q-20260724T035603Z-2c06e1a4`

**Artifact root (outside Zaide repository):**

```text
/tmp/phase16-artifacts/phase-16/records/m3-qualification/m3q-20260724T035603Z-2c06e1a4/
/tmp/phase16-artifacts/phase-16/records/dns-binding/m3q-20260724T035603Z-2c06e1a4/
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
| Launch Qwen Code (TC-T01, plan mode, JSON, 12 turns, 60s) | **Yes — once** — locked auth argv present; `qwen_exit=0` |
| USD 1 smoke / USD 3 cumulative spend | Caps **not proven breached**; post-balance **not** obtained |
| Verify TC-T01 `FetchData` → `RetrieveData` | **Attempted** — **FAIL** (no rename) |

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
| Balance / cost tracking | DeepSeek `/user/balance` before | **PASS** — USD **3.98** before |
| slirp4netns attach | host-visible `UNSHARE_PID` + tapfd handoff | **PASS** (`sent tapfd=5 for tap0`) |
| Inner egress reprobe | allowlisted TLS + non-allowlisted block | **PASS** — allow body `Authentication Fails (governor)`; block curl exit 28 (timeout) |
| Bubblewrap `/etc` setup | `--tmpfs /etc` + ro-bind hosts + resolv-empty | **PASS** (process entered sandbox) |
| Qwen headless run | TC-T01, locked auth argv, plan, JSON, 12 turns, 60s | **PASS exit** — `qwen_exit=0` (plan-only; see §6) |
| TC-T01 task completion | `FetchData` → `RetrieveData` rename | **FAIL** — `FetchData` count 11; `RetrieveData` count 0 |
| Orchestrator finalization | post-balance, summary verdict, cleanup | **FAIL** — hung on wait after inner work; external timeout 300s (`orchestrator_exit=124`); post-balance unavailable |

**DNS binding execution verdict:** **GO** (A-14 sequence complete through
triple-consistency and inner allow/block reprobes; `BOUND_IPV4=3.173.21.63`;
operator-finalized `BINDING_VERDICT=GO` after timeout).

**Qualification verdict:** **NO-GO** — Qwen exit 0 alone is insufficient; TC-T01
workspace change not verified.

---

## 3. Locked Smoke argv (executed)

Executable (pinned): `/tmp/phase16-artifacts/phase-16/artifacts/qwen-code/v0.20.1/inspect/qwen-code/bin/qwen`

Argv tail **executed in this session** (locked `Phase16M3QualificationPolicy` +
orchestrator):

```text
--auth-type openai
--openai-base-url https://api.deepseek.com
--approval-mode plan
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

**Auth remediation status:** auth-type failure mode remains **cleared**. Failure
mode for this session is **plan-only success without workspace mutation**, not
auth-type missing and not turn-limit exit 53.

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
| Stop reason | `tc_t01_workspace_change_not_verified` |

---

## 5. Commands Ledger (excerpt)

```text
m3q-20260724T035603Z-2c06e1a4 step=tool_inventory exit=0 utc=2026-07-24T03:56:03Z
m3q-20260724T035603Z-2c06e1a4 step=qwen_binary_present exit=0 utc=2026-07-24T03:56:03Z
m3q-20260724T035603Z-2c06e1a4 step=dns_resolution exit=0 utc=2026-07-24T03:56:03Z
m3q-20260724T035603Z-2c06e1a4 step=dns_triple_consistency exit=0 utc=2026-07-24T03:56:03Z
m3q-20260724T035603Z-2c06e1a4 step=materialize_tc_t01 exit=0 utc=2026-07-24T03:56:03Z
m3q-20260724T035603Z-2c06e1a4 step=isolation_bwrap_precheck exit=0 utc=2026-07-24T03:56:03Z
m3q-20260724T035603Z-2c06e1a4 step=credential_load_one_shot exit=0 utc=2026-07-24T03:56:03Z
m3q-20260724T035603Z-2c06e1a4 step=balance_before exit=0 utc=2026-07-24T03:56:03Z
m3q-20260724T035603Z-2c06e1a4 step=slirp4netns_attach exit=0 utc=2026-07-24T03:56:03Z
m3q-20260724T035603Z-2c06e1a4 step=external_timeout_finalization exit=124 utc=2026-07-24T04:02:55Z
```

`inner_qualification` and `balance_after` ledger lines were **not** written by
the orchestrator because it remained blocked after Qwen until the external
safety timeout (300s) terminated the process group.

---

## 6. Qwen Result (redacted)

`run/qwen-result.env`: `qwen_exit=0`

`run/qwen.stderr`: empty

`run/qwen.stdout` (JSON event stream; no credential material):

| Field | Value |
|---|---|
| Result subtype | `success` |
| `is_error` | `false` |
| `permission_mode` (init) | `plan` |
| `num_turns` | **5** (ceiling was 12) |
| `duration_ms` | **24709** |
| `usage.input_tokens` | 160876 |
| `usage.output_tokens` | 3044 |
| `usage.cache_read_input_tokens` | 128640 |
| `usage.total_tokens` | 163920 |
| `stats.files.totalLinesAdded` | **0** |
| `stats.files.totalLinesRemoved` | **0** |
| `exit_plan_mode` tool | **fail** (1 call, success 0) |

Result text (paraphrase / head only): Qwen stated plan mode exit tooling was
unavailable and presented a plan to rename `FetchData` → `RetrieveData` across
eight files / eleven occurrences. **No file edits were applied.**

Workspace verification after exit:

| Check | Value |
|---|---|
| `FetchData` occurrences in `*.cs` | **11** |
| `RetrieveData` occurrences in `*.cs` | **0** |
| `tc_t01_rename_verified` | **NO** |

Post-Qwen inner verify (orchestrator): `verify-result.env` recorded
`build_exit=143` (SIGTERM under outer timeout) and `test_exit=0` against the
**unmodified** fixture (original tests still pass with `FetchData`).

---

## 7. Root Cause

### 7.1 Cleared blockers (prior grants / this grant)

| Prior failure | This grant |
|---|---|
| Auth type not selected | **Cleared** — `--auth-type openai` + `--openai-base-url` + modelProviders present |
| slirp4netns host PID | **PASS** (`UNSHARE_PID` / tapfd) |
| Bubblewrap `/etc/resolv.conf` symlink | **PASS** (`--tmpfs /etc` + ro-bind resolv-empty) |
| DNS triple-consistency / egress | **PASS** |
| Max session turns (5) under prior policy | **Not hit** — used locked **12**; actual turns **5**; exit 0 |

### 7.2 Plan-mode success without TC-T01 mutation (this grant)

Locked smoke argv includes `--approval-mode plan`. Qwen Code v0.20.1 completed
in plan mode with exit **0**, explored the workspace (grep/glob/read), failed
`exit_plan_mode`, and wrote a plan only. File stats show **zero** lines added
or removed. TC-T01 success criteria require a verified
`FetchData` → `RetrieveData` rename; that did **not** occur.

This is a **policy / success-criteria mismatch** relative to mutation-required
GO rules, not an auth or DNS failure. No second attempt was authorized under
this grant to change approval mode or re-run.

### 7.3 Orchestrator hang after Qwen exit (process hygiene)

After Qwen wrote exit 0 and redacted streams, the outer smoke script remained
blocked past the Qwen wall-time window (external safety timeout **300s**,
`orchestrator_exit=124`). Post-balance and orchestrator-written GO/NO-GO
finalization did not run; operator finalized summary/DNS verdict files from
live artifacts. This residual lifecycle defect does **not** change the
qualification technical result: TC-T01 was already unverified.

**Future grant requirements (out of scope; not applied under this grant):**

1. New dedicated one-shot sub-key (this key consumed).
2. New qualification grant + new session ID.
3. Resolve plan-mode vs mutation-required TC-T01 GO criteria (owner decision on
   approval mode or redefined smoke success).
4. Fix orchestrator post-child reaping / hang so post-balance and summary
   finalization always run after Qwen exit.
5. Gate **GO** only on Qwen exit 0 **and** verified TC-T01 workspace change.
6. Preserve session records outside `/tmp` before any host reboot.

---

## 8. Spend

| Metric | Value |
|---|---|
| Balance before | USD **3.98** |
| Balance after | **Unavailable** (orchestrator timed out before post-balance; key file already deleted) |
| Qwen usage object | input **160876**, output **3044**, cache_read **128640**, total **163920**; no USD field in result |
| Session smoke spend (USD) | **Not measured** (cannot invent delta without post-balance or provider console) |
| Phase 16 cumulative spend (ledger) | Prior measured **USD 0.00**; this session’s USD delta **unproven** (`campaign-spend.env` notes `UNKNOWN_SESSION_DELTA`) |
| M3 smoke cap (USD 1) / campaign cap (USD 3) | **Not proven breached** from available measurements; exact delta unknown without provider console |

Spend basis: pre-balance + token counts only. No credential material recorded.
Operator must not invent a spend figure.

---

## 9. GO / NO-GO

### 9.1 This qualification attempt

**NO-GO.** DNS binding, slirp4netns attach, egress reprobes, isolation
re-check, credential gate, Bubblewrap `/etc` setup, and locked auth argv
**passed**. Qwen Code **started once** and **exited 0** under plan mode with a
plan-only result. TC-T01 **not** executed to verified completion; workspace
still contained `FetchData` only (no `RetrieveData` rename). Spend: **not
measured** (post-balance unavailable).

Candidate remains **`eligible for later M3 qualification`** but **not
qualified**. Do **not** proceed to M4. Do **not** retry under this grant.

### 9.2 Future retry requirements (out of scope)

1. Human provisions a **new** dedicated DeepSeek sub-key one-shot file.
2. Re-issue a **new** qualification grant with a **new** session ID.
3. Owner decision on approval-mode vs mutation-required GO criteria for TC-T01.
4. Optionally harden orchestrator wait/reap and durable evidence paths.
5. Gate **GO** only on Qwen exit 0 **and** verified TC-T01 workspace change.
6. **No second attempt** was authorized or performed under this grant.

---

## 10. Cross-References

- `M3_DNS_BINDING_GATE.md` — binding rules executed for session above
- `M3A_ACQUISITION_EVIDENCE.md` — pinned binary; auth support surface
- `M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md` — argv/modelProviders contract used
- `M3_EGRESS_PROOF_EVIDENCE.md` — egress architecture
- `TASK_CORPUS.md` — TC-T01 definition
- `IMPLEMENTATION_PLAN.md` — phase status

---

## 11. Historical note — session `m3q-20260723T164355Z-c421b379`

Prior authorized post-remediation session (then-locked **5** turns): DNS/slirp/
egress/tmpfs/auth argv **GO**; Qwen once then **`qwen_exit=53`**
(`FatalTurnLimitedError`); TC-T01 incomplete; spend not measured; orchestrator
hung; `/tmp` artifacts lost on host reboot. Values for that session were
operator-captured pre-reboot only. Superseded as **latest** by
`m3q-20260724T035603Z-2c06e1a4` under this grant.

---

*M3 qualification smoke evidence — session `m3q-20260724T035603Z-2c06e1a4`.
Observational only. NO-GO: Qwen exit 0 in plan mode without verified TC-T01
rename (`FetchData` remains). Pre-launch DNS, slirp, egress, Bubblewrap `/etc`,
and locked auth GO. Orchestrator external timeout before post-balance. No
second attempt. No M4. No comparative or quality claims.*
