# Phase 16 M3 — Qualification Smoke Evidence (TC-T01)

**Status:** **NO-GO** — authorized fresh retry (2026-07-23) stopped at Qwen
Bubblewrap launch after DNS binding, slirp4netns attach, and egress reprobes
**passed**. Pre-launch gates (A-14 DNS binding, isolation re-check, one-shot
credential load, balance pre-check) **passed**. **slirp4netns** host-visible
`UNSHARE_PID` attach **passed** (`sent tapfd` confirmed). Inner egress reprobes
**passed** (allowlisted TLS reachability, non-allowlisted block). Qwen Code
**attempted** but **failed to start** inside Bubblewrap:
`/etc/resolv.conf` bind rejected over the host symlink (exit **1**); TC-T01
rename **not performed**. Provider spend **USD 0** (balance unchanged at USD
3.98). Orchestrator initially recorded a false **GO** (inner script did not
gate on `qwen_exit`); repository fix applied post-session. **No** comparative
or quality claims.

**Prior attempts (same day, exhausted grants):**

1. Session `m3q-20260723T113639Z-b1e764d3` — **NO-GO** at credential gate
   (one-shot file absent). DNS binding + isolation **GO**; Qwen not launched.
2. Session `m3q-20260723T121356Z-1d1e7154` — **NO-GO** at slirp4netns attach
   (inner PID `1` bug); credential consumed; Qwen not launched.

**Campaign path:** single-candidate observational only
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`).

**Session ID (this grant):** `m3q-20260723T131730Z-1c8c982f`

**Artifact root (outside Zaide repository):**

```text
/tmp/phase16-artifacts/phase-16/records/m3-qualification/m3q-20260723T131730Z-1c8c982f/
/tmp/phase16-artifacts/phase-16/records/dns-binding/m3q-20260723T131730Z-1c8c982f/
```

---

## 1. Scope and Non-Effects

| Authorized in this grant | Performed |
|---|---|
| Execute DNS binding gate immediately before launch | **Yes** |
| slirp4netns attach via host-visible `UNSHARE_PID` | **Yes** — `sent tapfd` confirmed |
| Inner egress reprobe (allow + block) | **Yes** |
| Bubblewrap isolation re-check | **Yes** |
| Materialize TC-T01 synthetic workspace | **Yes** |
| Read dedicated one-shot sub-key via orchestrator only | **Yes** — file consumed and deleted |
| Launch Qwen Code (TC-T01, plan mode, JSON, 5 turns, 60s) | **Attempted** — Bubblewrap launch **FAIL** |
| USD 1 smoke / USD 3 cumulative spend | **Not incurred** (balance unchanged) |

| Forbidden | Status |
|---|---|
| Second attempt / retry | **Not performed** (this grant exhausted) |
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
| Dedicated sub-key (C-04 / A-09) | one-shot file mode `600`, consumed after read | **PASS** |
| Balance / cost tracking | DeepSeek `/user/balance` | **PASS** — USD 3.98 before/after |
| slirp4netns attach | host-visible `UNSHARE_PID` + tapfd handoff | **PASS** — `unshare_pid=1993783`, `sent tapfd` |
| Inner egress reprobe | allowlisted TLS + non-allowlisted block | **PASS** |
| Qwen Bubblewrap launch | sandbox hosts + no ambient DNS | **FAIL** — `qwen_exit=1` |
| TC-T01 task completion | `FetchData` → `RetrieveData` rename | **Not reached** — workspace unchanged |

**DNS binding execution verdict:** **GO** (A-14 sequence complete; `BINDING_VERDICT=GO`).

**Qualification verdict:** **NO-GO** — upstream binary did not run successfully;
task not observed.

---

## 3. Locked Smoke argv (attempted)

Executable (pinned): `/tmp/phase16-artifacts/phase-16/artifacts/qwen-code/v0.20.1/inspect/qwen-code/bin/qwen`

Policy-locked tail (owner grant 2026-07-23):

```text
--approval-mode plan
--model deepseek-v4-flash
--output-format json
--max-session-turns 5
--max-wall-time 60s
```

Prompt source: materialized `TC-T01` prompt (rename `FetchData` → `RetrieveData`).

Exact argv recorded under session artifact root (`exact-argv.txt`).

---

## 4. Key Lifecycle (no value disclosed)

| Field | Recorded value |
|---|---|
| `key_source` | `phase16_one_shot_file` |
| `key_file_path` | `/tmp/phase16-artifacts/phase-16/credentials/subkey.once` |
| Credential material persisted | **NO** |
| `file_consumed` | **YES** — absent after orchestrator read |
| `value_disclosed` | **NO** |
| Platform-key disposition | **Revoked by project owner after this NO-GO**; do not reuse |
| Stop reason | `qwen_launch_failed exit=1` (Bubblewrap `/etc/resolv.conf` bind) |

---

## 5. Commands Ledger (excerpt)

```text
m3q-20260723T131730Z-1c8c982f step=tool_inventory exit=0
m3q-20260723T131730Z-1c8c982f step=qwen_binary_present exit=0
m3q-20260723T131730Z-1c8c982f step=dns_resolution exit=0
m3q-20260723T131730Z-1c8c982f step=dns_triple_consistency exit=0
m3q-20260723T131730Z-1c8c982f step=materialize_tc_t01 exit=0
m3q-20260723T131730Z-1c8c982f step=isolation_bwrap_precheck exit=0
m3q-20260723T131730Z-1c8c982f step=credential_load_one_shot exit=0
m3q-20260723T131730Z-1c8c982f step=balance_before exit=0
m3q-20260723T131730Z-1c8c982f step=slirp4netns_attach exit=0
m3q-20260723T131730Z-1c8c982f step=inner_qualification exit=0
m3q-20260723T131730Z-1c8c982f step=balance_after exit=0
(truthful stop: qwen_exit=1 — orchestrator qwen-exit gate added post-session)
```

Full ledger under session artifact root.

---

## 6. Root Cause and Orchestrator Fixes

### 6.1 slirp4netns host PID (prior attempt — fixed before this grant)

Prior retry used inner PID-namespace `1` instead of host-visible
`unshare --fork` PID. This grant used `$UNSHARE_PID`; slirp attach **passed**.

### 6.2 Bubblewrap `/etc/resolv.conf` symlink overlay (this attempt)

Host `/etc/resolv.conf` is a symlink to `/run/systemd/resolve/stub-resolv.conf`.
Bubblewrap `--ro-bind` of the sandbox `resolv-empty.txt` over that symlink
failed:

```text
bwrap: Can't create file at /etc/resolv.conf: No such file or directory
```

**Repository fix (post-session, for future grants):** add `--tmpfs /etc` before
hosts/resolv binds in the Qwen Bubblewrap invocation.

### 6.3 Missing Qwen exit gate (this attempt)

The inner script continued after `qwen_exit=1`, causing a false orchestrator
**GO**. **Repository fix (post-session):** inner script writes `fatal.txt` and
exits non-zero on Qwen failure; outer script re-checks `qwen-result.env`.

---

## 7. Spend

| Metric | Value |
|---|---|
| Balance before | USD 3.98 |
| Balance after | USD 3.98 |
| Session smoke spend | **USD 0.00** |
| Phase 16 cumulative spend | **USD 0.00** (under USD 3 cap) |
| M3 smoke cap (USD 1) | **Not approached** |

---

## 8. GO / NO-GO

### 8.1 This qualification attempt

**NO-GO.** DNS binding, slirp4netns attach, egress reprobes, isolation
re-check, and credential gate **passed**. Qwen Code **failed to launch**
(Bubblewrap resolv.conf bind). TC-T01 **not executed**. Provider spend:
**USD 0**.

Candidate remains **`eligible for later M3 qualification`** but **not
qualified**. Do **not** proceed to M4. Do **not** retry under this grant.

### 8.2 Future retry requirements (out of scope)

1. Human provisions a **new** dedicated DeepSeek sub-key one-shot file (prior
   key consumed).
2. Re-issue a **new** qualification grant with a **new** session ID.
3. Orchestrator must include host-PID slirp attach, `--tmpfs /etc` resolv bind,
   and Qwen exit gating (committed in this slice).

---

## 9. Cross-References

- `M3_DNS_BINDING_GATE.md` — binding rules executed for session above
- `M3A_ACQUISITION_EVIDENCE.md` — pinned binary (launch attempted, failed)
- `M3_EGRESS_PROOF_EVIDENCE.md` — egress architecture
- `TASK_CORPUS.md` — TC-T01 definition
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 qualification smoke evidence — 2026-07-23. Observational only. NO-GO at
Qwen Bubblewrap launch. slirp4netns host-PID attach proven. No provider spend.*
