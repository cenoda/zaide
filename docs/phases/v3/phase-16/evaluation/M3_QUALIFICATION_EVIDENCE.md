# Phase 16 M3 — Qualification Smoke Evidence (TC-T01)

**Status:** **NO-GO** — authorized fresh qualification retry (post auth-config
remediation) session `m3q-20260723T164355Z-c421b379` completed pre-launch gates
and launched Qwen Code **exactly once** under the locked auth contract. Qwen
exited non-zero (`qwen_exit=53`, `FatalTurnLimitedError` / max session turns).
TC-T01 rename **not** performed (`FetchData` still present; no `RetrieveData`).
Orchestrator hung after Qwen exit waiting on a dead `unshare` child; host later
rebooted. Phase artifact tree under `/tmp/phase16-artifacts` was **not**
preserved across reboot; this record is from operator inspection of the live
session tree **before** reboot. **No** second attempt. **No** M4. **No**
comparative or quality claims.

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

**Campaign path:** single-candidate observational only
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`).

**Session ID (this grant):** `m3q-20260723T164355Z-c421b379`

**Artifact root (outside Zaide repository; lost after host reboot):**

```text
/tmp/phase16-artifacts/phase-16/records/m3-qualification/m3q-20260723T164355Z-c421b379/
/tmp/phase16-artifacts/phase-16/records/dns-binding/m3q-20260723T164355Z-c421b379/
```

**Evidence durability note:** session files were inspected live before reboot
(commands ledger, summary, DNS consistency, exact argv, qwen stderr JSON,
fatal.txt, balance-before, workspace `FetchData`/`RetrieveData` counts). No
durable off-`/tmp` copy was retained. Values below are operator-captured from
that inspection, not a re-run.

---

## 1. Scope and Non-Effects

| Authorized in this grant | Performed |
|---|---|
| Execute DNS binding gate immediately before launch | **Yes** |
| slirp4netns attach via host-visible `UNSHARE_PID` | **Yes** — tapfd handoff confirmed |
| Inner egress reprobe (allow + block) | **Yes** |
| Bubblewrap isolation re-check | **Yes** |
| Materialize TC-T01 synthetic workspace | **Yes** |
| Read dedicated one-shot sub-key via orchestrator only | **Yes** — mode `600`, size `36` bytes; consumed and deleted |
| Inject only `DEEPSEEK_API_KEY` (no `~/.config` / ambient) | **Yes** |
| Launch Qwen Code (TC-T01, plan mode, JSON, 5 turns, 60s) | **Yes — once** — remediated auth argv present; `qwen_exit=53` |
| USD 1 smoke / USD 3 cumulative spend | Caps **not violated to measured knowledge**; post-balance **not** obtained |

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
| slirp4netns attach | host-visible `UNSHARE_PID` + tapfd handoff | **PASS** |
| Inner egress reprobe | allowlisted TLS + non-allowlisted block | **PASS** — block curl exit 28 (timeout); allow body present |
| Bubblewrap `/etc` setup | `--tmpfs /etc` + ro-bind hosts + resolv-empty | **PASS** (process entered sandbox) |
| Qwen headless run | TC-T01, remediated auth argv, plan, JSON, 5 turns, 60s | **FAIL** — `qwen_exit=53` turn limit |
| TC-T01 task completion | `FetchData` → `RetrieveData` rename | **FAIL** — workspace still `FetchData` only (`FetchData` count 11; `RetrieveData` count 0) |
| Orchestrator finalization | post-balance, summary verdict, cleanup | **FAIL** — hung on `wait` after unshare child exit; host reboot later |

**DNS binding execution verdict:** **GO** (A-14 sequence complete through
triple-consistency and inner allow/block reprobes; `BOUND_IPV4=3.173.21.63`).
`BINDING_VERDICT` file remained `PENDING` only because the orchestrator never
reached its end-of-run write.

**Qualification verdict:** **NO-GO** — Qwen did not exit successfully; TC-T01
workspace change not verified.

---

## 3. Locked Smoke argv (executed)

Executable (pinned): `/tmp/phase16-artifacts/phase-16/artifacts/qwen-code/v0.20.1/inspect/qwen-code/bin/qwen`

Policy-locked tail (committed orchestrator + `Phase16M3QualificationPolicy`):

```text
--auth-type openai
--openai-base-url https://api.deepseek.com
--approval-mode plan
--model deepseek-v4-flash
--output-format json
--max-session-turns 5
--max-wall-time 60s
```

Environment allowlist: **`DEEPSEEK_API_KEY` only** (A-07). Workspace fixture
included `.qwen/settings.json` `modelProviders.openai[]` with
`envKey: DEEPSEEK_API_KEY` and `baseUrl: https://api.deepseek.com` for
`deepseek-v4-flash` (`M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md`).

Prompt source: materialized `TC-T01` prompt (rename `FetchData` → `RetrieveData`).

Exact argv was recorded under the session artifact root (`exact-argv.txt`) and
inspected before reboot; content matched the locked contract above.

**Auth remediation vs prior session:** prior session
`m3q-20260723T151512Z-6996af5f` failed with “No auth type is selected.” This
session’s argv **included** `--auth-type openai` and `--openai-base-url
https://api.deepseek.com`. Failure mode changed to **turn-limit exit 53**, not
auth-type missing.

---

## 4. Key Lifecycle (no value disclosed)

| Field | Recorded value |
|---|---|
| `key_source` | `phase16_one_shot_file` |
| `key_file_path` | `/tmp/phase16-artifacts/phase-16/credentials/subkey.once` |
| Pre-run metadata only | mode `600`, size `36` bytes (value never inspected/logged by operator) |
| Credential material persisted | **NO** |
| `file_consumed` | **YES** — absent after orchestrator read; still absent post-reboot |
| `value_disclosed` | **NO** |
| Ambient / `~/.config` credentials | **Not read** |
| Stop reason | `qwen_launch_failed exit=53` (`FatalTurnLimitedError`) |

---

## 5. Commands Ledger (excerpt; from pre-reboot inspection)

```text
m3q-20260723T164355Z-c421b379 step=tool_inventory exit=0 utc=2026-07-23T16:43:55Z
m3q-20260723T164355Z-c421b379 step=qwen_binary_present exit=0 utc=2026-07-23T16:43:55Z
m3q-20260723T164355Z-c421b379 step=dns_resolution exit=0 utc=2026-07-23T16:43:55Z
m3q-20260723T164355Z-c421b379 step=dns_triple_consistency exit=0 utc=2026-07-23T16:43:55Z
m3q-20260723T164355Z-c421b379 step=materialize_tc_t01 exit=0 utc=2026-07-23T16:43:55Z
m3q-20260723T164355Z-c421b379 step=isolation_bwrap_precheck exit=0 utc=2026-07-23T16:43:55Z
m3q-20260723T164355Z-c421b379 step=credential_load_one_shot exit=0 utc=2026-07-23T16:43:55Z
m3q-20260723T164355Z-c421b379 step=balance_before exit=0 utc=2026-07-23T16:43:56Z
m3q-20260723T164355Z-c421b379 step=slirp4netns_attach exit=0 utc=2026-07-23T16:43:56Z
```

No later ledger lines were written. Qwen result and fatal files existed under
`run/` after the inner launch; the outer orchestrator never logged
`inner_qualification` or `balance_after` because it remained blocked on
`wait "$UNSHARE_PID"` after the unshare child had already exited.

---

## 6. Qwen Result (redacted)

`run/qwen-result.env`: `qwen_exit=53`

`run/fatal.txt`: `qwen_launch_failed exit=53`

`run/qwen.stdout`: empty

`run/qwen.stderr` (no credential material):

```json
{
  "error": {
    "type": "FatalTurnLimitedError",
    "message": "Reached max session turns for this session. Increase the number of turns by specifying maxSessionTurns in settings.json.",
    "code": 53
  }
}
```

Orchestrator exit gate treats non-zero Qwen exit as fatal (**NO-GO**).

---

## 7. Root Cause

### 7.1 Cleared blockers (prior grants / this grant)

| Prior failure | This grant |
|---|---|
| Auth type not selected | **Cleared** — `--auth-type openai` + `--openai-base-url` + modelProviders present |
| slirp4netns host PID | **PASS** (`UNSHARE_PID` / tapfd) |
| Bubblewrap `/etc/resolv.conf` symlink | **PASS** (`--tmpfs /etc` + ro-bind resolv-empty) |
| DNS triple-consistency / egress | **PASS** |

### 7.2 Turn limit before verified TC-T01 completion (this grant)

Qwen Code v0.20.1 exited with `FatalTurnLimitedError` / code **53** after
reaching `--max-session-turns 5` (locked smoke ceiling). Empty stdout provided
no parseable token/turn usage object. Workspace inspection after exit showed
**no** `FetchData` → `RetrieveData` rename.

### 7.3 Orchestrator hang after Qwen exit (process hygiene)

After Qwen wrote exit 53, the outer smoke script remained blocked for hours on
`wait` for the `unshare` child while `slirp4netns` still referenced a dead
netns PID. This is an **orchestrator lifecycle defect** (does not change the
qualification technical result: Qwen already failed). Host reboot cleared the
deadlock and wiped `/tmp` artifacts.

**Future grant requirements (out of scope; not applied under this grant):**

1. New dedicated one-shot sub-key (this key consumed).
2. New qualification grant + new session ID.
3. Re-evaluate turn/time ceilings vs TC-T01 complexity if still plan-mode
   locked; do not silently raise caps without owner lock.
4. Fix orchestrator post-child reaping / hang so post-balance and summary
   finalization always run after Qwen exit.
5. Preserve session records outside `/tmp` before any host reboot.
6. Gate **GO** only on Qwen exit 0 **and** verified TC-T01 workspace change.

---

## 8. Spend

| Metric | Value |
|---|---|
| Balance before | USD **3.98** |
| Balance after | **Unavailable** (orchestrator hung before post-balance; key file already deleted; artifacts lost on reboot) |
| Qwen usage object | **Unavailable** (`qwen.stdout` empty; only stderr FatalTurnLimitedError) |
| Session smoke spend | **Not measured** (cannot claim USD 0.00; turn-limit exit implies work may have occurred) |
| Phase 16 cumulative spend (ledger) | Still recorded as **USD 0.00 measured** prior to this session; this session’s delta **unproven** |
| M3 smoke cap (USD 1) / campaign cap (USD 3) | **Not proven breached** from available measurements; exact delta unknown without provider console |

Spend basis: pre-balance only. No credential material recorded. Operator must
not invent a spend figure.

---

## 9. GO / NO-GO

### 9.1 This qualification attempt

**NO-GO.** DNS binding, slirp4netns attach, egress reprobes, isolation
re-check, credential gate, Bubblewrap `/etc` setup, and remediated auth argv
**passed**. Qwen Code **started once** and **failed** with `qwen_exit=53`
(max session turns). TC-T01 **not** executed to verified completion; workspace
still contained `FetchData` (no `RetrieveData` rename). Spend: **not measured**.

Candidate remains **`eligible for later M3 qualification`** but **not
qualified**. Do **not** proceed to M4. Do **not** retry under this grant.

### 9.2 Future retry requirements (out of scope)

1. Human provisions a **new** dedicated DeepSeek sub-key one-shot file.
2. Re-issue a **new** qualification grant with a **new** session ID.
3. Optionally harden orchestrator wait/reap and durable evidence paths.
4. Gate **GO** only on Qwen exit 0 **and** verified TC-T01 workspace change.

---

## 10. Cross-References

- `M3_DNS_BINDING_GATE.md` — binding rules executed for session above
- `M3A_ACQUISITION_EVIDENCE.md` — pinned binary; auth support surface
- `M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md` — argv/modelProviders contract used
- `M3_EGRESS_PROOF_EVIDENCE.md` — egress architecture
- `TASK_CORPUS.md` — TC-T01 definition
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 qualification smoke evidence — session `m3q-20260723T164355Z-c421b379`.
Observational only. NO-GO at Qwen max-session-turns (exit 53). Pre-launch DNS,
slirp, egress, Bubblewrap `/etc`, and remediated auth GO. TC-T01 incomplete.
Artifacts lost on host reboot after orchestrator hang; values from pre-reboot
operator inspection. No second attempt.*
