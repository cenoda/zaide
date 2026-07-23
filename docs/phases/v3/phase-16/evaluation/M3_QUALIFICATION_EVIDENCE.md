# Phase 16 M3 — Qualification Smoke Evidence (TC-T01)

**Status:** **NO-GO** — authorized retry (2026-07-23) stopped at netns egress
gate after credential consumption. Pre-launch DNS binding, workspace
materialization, Bubblewrap isolation re-check, one-shot credential load, and
balance pre-check **passed**. **slirp4netns** attach failed because the
orchestrator passed inner PID-namespace `1` instead of the host-visible
`unshare --fork` PID; **tap0** never appeared. **No** Qwen Code launch, **no**
authenticated provider spend (balance unchanged), **no** comparative or quality
claims.

**Prior attempt (same day, different grant):** session `m3q-20260723T113639Z-b1e764d3`
stopped at credential gate (one-shot file absent). See §8.

**Campaign path:** single-candidate observational only
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`).

**Session ID (this grant):** `m3q-20260723T121356Z-1d1e7154`

**Artifact root (outside Zaide repository):**

```text
/tmp/phase16-artifacts/phase-16/records/m3-qualification/m3q-20260723T121356Z-1d1e7154/
/tmp/phase16-artifacts/phase-16/records/dns-binding/m3q-20260723T121356Z-1d1e7154/
```

---

## 1. Scope and Non-Effects

| Authorized in this grant | Performed |
|---|---|
| Execute DNS binding gate immediately before launch | **Yes** |
| Recreate netns + slirp4netns + nft architecture | **Attempted** — slirp4netns attach **FAIL** (host PID resolution bug) |
| Bubblewrap isolation re-check | **Yes** |
| Materialize TC-T01 synthetic workspace | **Yes** |
| Read dedicated one-shot sub-key via orchestrator only | **Yes** — file consumed and deleted |
| Inject `DEEPSEEK_API_KEY` / launch Qwen Code | **Not performed** (egress gate blocked before launch) |
| USD 1 smoke / USD 3 cumulative spend | **Not incurred** (balance unchanged) |

| Forbidden | Status |
|---|---|
| Second attempt / retry | **Not performed** (this grant exhausted) |
| `src/` production changes | **Not performed** |
| Credential value in evidence or git | **Not performed** |
| Comparative / quality claims | **Not made** |

---

## 2. Pre-Launch Gate Results

| Step | Gate | Result |
|---|---|---|
| Grant env | `PHASE16_M3_QUALIFICATION_GRANT=1` | **PASS** |
| Tool inventory | bwrap, slirp4netns, nft, curl, unshare, getent, dotnet | **PASS** |
| Pinned Qwen binary present (A-02) | `.../inspect/qwen-code/bin/qwen` | **PASS** (not launched) |
| DNS resolution (D-01–D-04) | `getent ahostsv4 api.deepseek.com` → single IPv4 | **PASS** — `3.173.21.63` |
| Hosts injection (D-06) | sandbox-only `/etc/hosts` map | **PASS** |
| nft allowlist text (D-07) | `3.173.21.63/32:443` | **PASS** |
| Triple consistency (D-05) | FRESH/HOSTS/NFT IPv4 equal | **PASS** — `CONSISTENT=YES` |
| TC-T01 workspace | materialized under artifact root | **PASS** |
| Bubblewrap isolation pre-check | workspace write + host write denial | **PASS** |
| Dedicated sub-key (C-04 / A-09) | one-shot file mode `600`, consumed after read | **PASS** |
| Balance / cost tracking | DeepSeek `/user/balance` | **PASS** — USD 3.98 before/after (no spend) |
| slirp4netns attach | host-visible netns PID + tapfd handoff | **FAIL** — PID `1` used; `/proc/1/ns/net: Permission denied` |
| Egress reprobe + Qwen launch | netns inner script | **Not reached** — `tap0_missing` |

**DNS binding execution verdict:** pre-credential steps **GO**; overall launch
**NO-GO** because netns egress attach failed before inner probes and Qwen start.

---

## 3. Locked Smoke argv (not executed)

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

---

## 4. Key Lifecycle (no value disclosed)

| Field | Recorded value |
|---|---|
| `key_source` | `phase16_one_shot_file` |
| `key_file_path` | `/tmp/phase16-artifacts/phase-16/credentials/subkey.once` |
| `key_prefix` | `sk-2c72f...` (redacted) |
| `file_consumed` | **YES** — absent after orchestrator read |
| `value_disclosed` | `NO` |
| Stop reason | `tap0_missing` (slirp4netns host PID resolution) |

---

## 5. Commands Ledger (excerpt)

```text
m3q-20260723T121356Z-1d1e7154 step=tool_inventory exit=0
m3q-20260723T121356Z-1d1e7154 step=qwen_binary_present exit=0
m3q-20260723T121356Z-1d1e7154 step=dns_resolution exit=0
m3q-20260723T121356Z-1d1e7154 step=dns_triple_consistency exit=0
m3q-20260723T121356Z-1d1e7154 step=materialize_tc_t01 exit=0
m3q-20260723T121356Z-1d1e7154 step=isolation_bwrap_precheck exit=0
m3q-20260723T121356Z-1d1e7154 step=credential_load_one_shot exit=0
m3q-20260723T121356Z-1d1e7154 step=balance_before exit=0
m3q-20260723T121356Z-1d1e7154 step=slirp4netns_attach exit=0
m3q-20260723T121356Z-1d1e7154 step=inner_qualification exit=2
m3q-20260723T121356Z-1d1e7154 step=balance_after exit=0
(stop: tap0_missing)
```

Full ledger under session artifact root.

---

## 6. Root Cause and Orchestrator Fix

The inner script wrote `$$` (PID `1` inside the `--pid` namespace) to
`/tmp/phase16-ns-pid`. The host passed that value to `slirp4netns`, which
attempted `/proc/1/ns/net` and failed. M3 egress proof used the host-visible
`unshare --fork` child PID instead (`host_ns_pid=1937523` in egress-proof
`session.env`).

Repository fix (post-attempt, for future grants): `m3-qualification-smoke.sh`
now uses `$UNSHARE_PID` for `slirp4netns`, waits for inner ready, and confirms
`sent tapfd` in slirp stderr before proceeding.

---

## 7. GO / NO-GO

### 7.1 This qualification attempt

**NO-GO.** DNS binding, isolation re-check, and credential gate passed.
Netns egress attach failed; Qwen Code was **not** launched. Provider spend:
**USD 0** (balance unchanged at USD 3.98).

Candidate remains **`eligible for later M3 qualification`** but **not
qualified**. Do **not** proceed to M4. Do **not** retry under this grant.

### 7.2 Future retry requirements (out of scope)

1. Human provisions a **new** dedicated DeepSeek sub-key one-shot file (prior
   key consumed).
2. Re-issue a **new** qualification grant with a **new** session ID.
3. Orchestrator host-PID fix must be present (committed in this slice).

---

## 8. Prior Attempt (credential gate, same day)

Session `m3q-20260723T113639Z-b1e764d3` — **NO-GO** at credential gate
(one-shot file absent). DNS binding + isolation pre-check **GO**; Qwen not
launched; provider spend **USD 0**.

Artifact root:

```text
/tmp/phase16-artifacts/phase-16/records/m3-qualification/m3q-20260723T113639Z-b1e764d3/
/tmp/phase16-artifacts/phase-16/records/dns-binding/m3q-20260723T113639Z-b1e764d3/
```

---

## 9. Cross-References

- `M3_DNS_BINDING_GATE.md` — binding rules executed for session above
- `M3A_ACQUISITION_EVIDENCE.md` — pinned binary (not launched here)
- `M3_EGRESS_PROOF_EVIDENCE.md` — egress architecture (host PID pattern)
- `TASK_CORPUS.md` — TC-T01 definition
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 qualification smoke evidence — 2026-07-23. Observational only. NO-GO at
netns egress gate after credential consumption. No upstream launch. No provider
spend.*
