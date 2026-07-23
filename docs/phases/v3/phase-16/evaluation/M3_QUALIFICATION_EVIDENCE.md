# Phase 16 M3 — Qualification Smoke Evidence (TC-T01)

**Status:** **NO-GO** — single qualification attempt stopped at credential gate
(2026-07-23). Pre-launch DNS binding, workspace materialization, and Bubblewrap
isolation pre-check **passed**. **No** DeepSeek sub-key provisioning path was
available without reading ambient, personal, or `~/.config` credentials.
**No** Qwen Code launch, **no** authenticated provider spend, **no** comparative
or quality claims.

**Campaign path:** single-candidate observational only
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`).

**Session ID:** `m3q-20260723T113639Z-b1e764d3`

**Artifact root (outside Zaide repository):**

```text
/tmp/phase16-artifacts/phase-16/records/m3-qualification/m3q-20260723T113639Z-b1e764d3/
/tmp/phase16-artifacts/phase-16/records/dns-binding/m3q-20260723T113639Z-b1e764d3/
```

---

## 1. Scope and Non-Effects

| Authorized in this grant | Performed |
|---|---|
| Execute DNS binding gate immediately before launch | **Yes** (host resolution, hosts map, nft rule text, triple-consistency) |
| Recreate netns + slirp4netns + nft architecture (orchestrator ready) | **Prepared** (inner script; not reached — credential gate blocked) |
| Bubblewrap isolation re-check | **Yes** (writable workspace + host write denial) |
| Materialize TC-T01 synthetic workspace | **Yes** |
| Create dedicated Phase 16 DeepSeek sub-key | **No** — no programmatic DeepSeek key API; no authorized one-shot provisioning file present |
| Inject `DEEPSEEK_API_KEY` / launch Qwen Code | **Not performed** |
| USD 1 smoke / USD 3 cumulative spend | **Not incurred** (no balance-bearing API use) |

| Forbidden | Status |
|---|---|
| Second attempt / retry | **Not performed** |
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
| Dedicated sub-key (C-04 / A-09) | one-shot file at `credentials/subkey.once` | **FAIL** — file absent |
| Balance / cost tracking | DeepSeek `/user/balance` | **Not reached** |
| Egress reprobe + Qwen launch | netns inner script | **Not reached** |

**DNS binding execution verdict:** pre-credential steps **GO**; overall launch
**NO-GO** because credential gate failed before steps 5–9.

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
| `key_source` | `phase16_one_shot_file` (expected) |
| `key_file_path` | `/tmp/phase16-artifacts/phase-16/credentials/subkey.once` |
| `key_prefix` | *(empty — key not loaded)* |
| `value_disclosed` | `NO` |
| Stop reason | `no dedicated Phase16 DeepSeek sub-key one-shot file` |

DeepSeek platform keys are created manually at `platform.deepseek.com`; Phase 16
requires a **dedicated** key delivered via the one-shot file (never `~/.config`
or ambient credentials).

---

## 5. Commands Ledger (excerpt)

```text
m3q-20260723T113639Z-b1e764d3 step=tool_inventory exit=0
m3q-20260723T113639Z-b1e764d3 step=qwen_binary_present exit=0
m3q-20260723T113639Z-b1e764d3 step=dns_resolution exit=0
m3q-20260723T113639Z-b1e764d3 step=dns_triple_consistency exit=0
m3q-20260723T113639Z-b1e764d3 step=materialize_tc_t01 exit=0
m3q-20260723T113639Z-b1e764d3 step=isolation_bwrap_precheck exit=0
(stop before credential_load_one_shot)
```

Full ledger under session artifact root.

---

## 6. GO / NO-GO

### 6.1 This qualification attempt

**NO-GO.** Pre-launch DNS binding and isolation re-check passed. Credential
creation/injection could not proceed without an authorized dedicated sub-key
provisioning path. Qwen Code was **not** launched. Provider spend: **USD 0**.

Candidate remains **`eligible for later M3 qualification`** but **not
qualified**. Do **not** proceed to M4 or a second smoke under this grant.

### 6.2 Retry requirements (out of scope for this slice)

1. Human creates a dedicated DeepSeek API key at `platform.deepseek.com` (label
   e.g. `phase16-m3-smoke`).
2. Write the key **once** to `/tmp/phase16-artifacts/phase-16/credentials/subkey.once`
   (mode `600`); the orchestrator deletes the file immediately after read.
3. Re-issue a **new** qualification grant for a **new** session ID (this grant
   authorized exactly one attempt).

---

## 7. Cross-References

- `M3_DNS_BINDING_GATE.md` — binding rules executed for session above
- `M3A_ACQUISITION_EVIDENCE.md` — pinned binary (not launched here)
- `M3_EGRESS_PROOF_EVIDENCE.md` — egress architecture reused by orchestrator
- `TASK_CORPUS.md` — TC-T01 definition
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 qualification smoke evidence — 2026-07-23. Observational only. NO-GO at
credential gate. No upstream launch. No provider spend.*
