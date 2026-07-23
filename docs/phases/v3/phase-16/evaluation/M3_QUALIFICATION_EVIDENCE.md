# Phase 16 M3 — Qualification Smoke Evidence (TC-T01)

**Status:** **NO-GO** — authorized fresh retry (2026-07-24 / session UTC
2026-07-23) stopped after Qwen Code **started** inside Bubblewrap and exited
immediately with an auth-configuration error. Pre-launch gates (A-14 DNS
binding, isolation re-check, one-shot credential load, balance pre-check,
slirp4netns host-PID attach, inner egress reprobes) **passed**. Bubblewrap
`--tmpfs /etc` hosts/resolv bind **passed** (prior resolv-symlink failure
cleared). Qwen process **ran** and emitted JSON
`error_during_execution`: *No auth type is selected* (missing `--auth-type` /
settings). **0** model turns, **0** tokens, TC-T01 rename **not performed**.
Provider spend **USD 0.00** (balance before USD 3.98; post-balance unavailable
after one-shot key deletion; usage proves no API spend). **No** comparative or
quality claims.

**Prior attempts (exhausted grants; not this session):**

1. Session `m3q-20260723T113639Z-b1e764d3` — **NO-GO** at credential gate
   (one-shot file absent). DNS binding + isolation **GO**; Qwen not launched.
2. Session `m3q-20260723T121356Z-1d1e7154` — **NO-GO** at slirp4netns attach
   (inner PID `1` bug); credential consumed; Qwen not launched.
3. Session `m3q-20260723T131730Z-1c8c982f` — **NO-GO** at Bubblewrap
   `/etc/resolv.conf` symlink bind; DNS/slirp/egress **GO**; Qwen not started.
4. Session `m3q-20260723T151034Z-71eea5e4` — same-day partial run (not this
   grant); same auth-type failure pattern recorded under artifact root.

**Campaign path:** single-candidate observational only
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`).

**Session ID (this grant):** `m3q-20260723T151512Z-6996af5f`

**Artifact root (outside Zaide repository):**

```text
/tmp/phase16-artifacts/phase-16/records/m3-qualification/m3q-20260723T151512Z-6996af5f/
/tmp/phase16-artifacts/phase-16/records/dns-binding/m3q-20260723T151512Z-6996af5f/
```

---

## 1. Scope and Non-Effects

| Authorized in this grant | Performed |
|---|---|
| Execute DNS binding gate immediately before launch | **Yes** |
| slirp4netns attach via host-visible `UNSHARE_PID` | **Yes** — `sent tapfd` / `received tapfd` confirmed |
| Inner egress reprobe (allow + block) | **Yes** |
| Bubblewrap isolation re-check | **Yes** |
| Materialize TC-T01 synthetic workspace | **Yes** |
| Read dedicated one-shot sub-key via orchestrator only | **Yes** — file consumed and deleted (mode was `600`) |
| Inject only `DEEPSEEK_API_KEY` (no `~/.config` / ambient) | **Yes** |
| Launch Qwen Code (TC-T01, plan mode, JSON, 5 turns, 60s) | **Yes (process started)** — auth config **FAIL** |
| USD 1 smoke / USD 3 cumulative spend | **Not incurred** (0 tokens; spend USD 0.00) |

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
| Dedicated sub-key (C-04 / A-09) | one-shot file mode `600`, consumed after read | **PASS** |
| Balance / cost tracking | DeepSeek `/user/balance` | **PASS** — USD **3.98** before |
| slirp4netns attach | host-visible `UNSHARE_PID` + tapfd handoff | **PASS** — `unshare_pid=2033305` |
| Inner egress reprobe | allowlisted TLS + non-allowlisted block | **PASS** — allow HTTP 401 body; block curl exit 28 |
| Bubblewrap `/etc` setup | `--tmpfs /etc` + ro-bind hosts + resolv-empty | **PASS** (process entered sandbox) |
| Qwen headless run | TC-T01, `--approval-mode plan`, JSON, 5 turns, 60s | **FAIL** — `qwen_exit=1` auth type not selected |
| TC-T01 task completion | `FetchData` → `RetrieveData` rename | **FAIL** — workspace still has `FetchData` only |

**DNS binding execution verdict:** **GO** (A-14 sequence complete; `BINDING_VERDICT=GO`).

**Qualification verdict:** **NO-GO** — upstream binary started but did not
authenticate; task not observed; no verified workspace change.

---

## 3. Locked Smoke argv (executed)

Executable (pinned): `/tmp/phase16-artifacts/phase-16/artifacts/qwen-code/v0.20.1/inspect/qwen-code/bin/qwen`

Policy-locked tail (owner grant; committed orchestrator):

```text
--approval-mode plan
--model deepseek-v4-flash
--output-format json
--max-session-turns 5
--max-wall-time 60s
```

Prompt source: materialized `TC-T01` prompt (rename `FetchData` → `RetrieveData`).

Exact argv recorded under session artifact root (`exact-argv.txt`).

**Missing relative to M3a support surface:** `--auth-type` was **not** present
in the committed smoke argv. Qwen rejected non-interactive start for that reason.

---

## 4. Key Lifecycle (no value disclosed)

| Field | Recorded value |
|---|---|
| `key_source` | `phase16_one_shot_file` |
| `key_file_path` | `/tmp/phase16-artifacts/phase-16/credentials/subkey.once` |
| Pre-run metadata only | mode `600`, size `36` bytes (value never inspected/logged) |
| Credential material persisted | **NO** |
| `file_consumed` | **YES** — absent after orchestrator read |
| `value_disclosed` | **NO** |
| Ambient / `~/.config` credentials | **Not read** |
| Stop reason | `qwen_auth_type_not_selected exit=1` |

---

## 5. Commands Ledger (excerpt)

```text
m3q-20260723T151512Z-6996af5f step=tool_inventory exit=0
m3q-20260723T151512Z-6996af5f step=qwen_binary_present exit=0
m3q-20260723T151512Z-6996af5f step=dns_resolution exit=0
m3q-20260723T151512Z-6996af5f step=dns_triple_consistency exit=0
m3q-20260723T151512Z-6996af5f step=materialize_tc_t01 exit=0
m3q-20260723T151512Z-6996af5f step=isolation_bwrap_precheck exit=0
m3q-20260723T151512Z-6996af5f step=credential_load_one_shot exit=0
m3q-20260723T151512Z-6996af5f step=balance_before exit=0
m3q-20260723T151512Z-6996af5f step=slirp4netns_attach exit=0
m3q-20260723T151512Z-6996af5f step=inner_qualification exit=4
m3q-20260723T151512Z-6996af5f step=balance_after exit=skipped note=skipped_key_consumed_usage_zero
```

Full ledger under session artifact root.

---

## 6. Qwen Result (redacted)

JSON result (from `qwen.stdout`; no credential material):

```json
[{
  "type": "result",
  "subtype": "error_during_execution",
  "is_error": true,
  "duration_ms": 0,
  "duration_api_ms": 0,
  "num_turns": 0,
  "usage": { "input_tokens": 0, "output_tokens": 0 },
  "error": {
    "message": "No auth type is selected. Please configure an auth type (e.g. via settings or `--auth-type`) before running in non-interactive mode."
  }
}]
```

`qwen_exit=1`. Orchestrator exit gate treated this as fatal (**NO-GO**).

---

## 7. Root Cause

### 7.1 Cleared blockers (prior grants / this grant)

| Prior failure | This grant |
|---|---|
| slirp4netns host PID | **PASS** (`UNSHARE_PID` / `sent tapfd`) |
| Bubblewrap `/etc/resolv.conf` symlink | **PASS** (`--tmpfs /etc` + ro-bind resolv-empty) |
| DNS triple-consistency / egress | **PASS** |

### 7.2 Auth type not selected (this grant)

Qwen Code v0.20.1 requires an explicit non-interactive auth configuration.
The committed smoke orchestrator injects `DEEPSEEK_API_KEY` only and does **not**
pass `--auth-type` (M3a support surface lists
`--auth-type` ∈ {`openai`,`anthropic`,`qwen-oauth`,`gemini`,`vertex-ai`};
DeepSeek is OpenAI-compatible). With no auth type, the binary exits before any
provider call.

**Future grant requirements (out of scope; not applied under this grant):**

1. New dedicated one-shot sub-key (this key consumed).
2. Owner-locked argv must include a verified auth path that remains within
   A-07 (`DEEPSEEK_API_KEY` only) — e.g. `--auth-type openai` and any required
   base-URL/model wiring that does not reintroduce ambient credentials.
3. Re-prove TC-T01 workspace rename + build/test after a successful Qwen exit.

---

## 8. Spend

| Metric | Value |
|---|---|
| Balance before | USD 3.98 |
| Balance after | **Unavailable** (one-shot key deleted before host post-balance; no second credential) |
| Qwen usage | `input_tokens=0`, `output_tokens=0`, `duration_api_ms=0` |
| Session smoke spend | **USD 0.00** |
| Phase 16 cumulative spend | **USD 0.00** (under USD 3 cap) |
| M3 smoke cap (USD 1) | **Not approached** |

Spend basis: auth error before any model API turn; zero token usage.

---

## 9. GO / NO-GO

### 9.1 This qualification attempt

**NO-GO.** DNS binding, slirp4netns attach, egress reprobes, isolation
re-check, credential gate, and Bubblewrap `/etc` setup **passed**. Qwen Code
**started** and **failed auth configuration** (`qwen_exit=1`). TC-T01 **not
executed**; workspace still contains `FetchData` (no `RetrieveData` rename).
Provider spend: **USD 0.00**.

Candidate remains **`eligible for later M3 qualification`** but **not
qualified**. Do **not** proceed to M4. Do **not** retry under this grant.

### 9.2 Future retry requirements (out of scope)

1. Human provisions a **new** dedicated DeepSeek sub-key one-shot file.
2. Re-issue a **new** qualification grant with a **new** session ID.
3. Orchestrator must pass a verified non-interactive auth configuration
   compatible with A-07 (`DEEPSEEK_API_KEY` only) in addition to host-PID
   slirp attach, `--tmpfs /etc`, and Qwen exit gating.
4. Gate **GO** only on Qwen exit 0 **and** verified TC-T01 workspace change.

---

## 10. Cross-References

- `M3_DNS_BINDING_GATE.md` — binding rules executed for session above
- `M3A_ACQUISITION_EVIDENCE.md` — pinned binary; `--auth-type` support surface
- `M3_EGRESS_PROOF_EVIDENCE.md` — egress architecture
- `TASK_CORPUS.md` — TC-T01 definition
- `IMPLEMENTATION_PLAN.md` — phase status

---

*M3 qualification smoke evidence — session `m3q-20260723T151512Z-6996af5f`.
Observational only. NO-GO at Qwen auth-type configuration. Pre-launch DNS,
slirp, egress, and Bubblewrap `/etc` GO. No provider spend. TC-T01 incomplete.*
