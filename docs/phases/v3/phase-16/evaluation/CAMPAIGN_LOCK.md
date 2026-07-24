# Phase 16 M1: Campaign Lock

**Status:** M1 explicitly human-accepted on 2026-07-23. **M1 amendment
(human-accepted 2026-07-23):** single-candidate observational path authorized
for Qwen Code only (`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`). Comparative rules
unchanged. **M2a was explicitly human-accepted on 2026-07-23** (standalone
offline runner contract and fake-candidate core). **M2b was completed on
2026-07-23** (`ISOLATION_EVIDENCE.md`).

---

## 1. Governance and Comparative-Claim Rule

### 1.1 Minimum-Configuration Rule

A comparative claim (e.g., "candidate A outperforms candidate B") is **forbidden**
unless at least **two independently qualified configurations** complete the full
task corpus under identical environmental conditions. If fewer than two
candidates qualify, any collected data is classified as **blocked** or
**single-candidate observational only**. No comparative superiority claim may be
derived from single-candidate runs or exploratory pilots.

### 1.2 No-Winner Language

The campaign does not declare a "winner." Aggregate scores, pass-rate rankings,
and efficiency comparisons are raw evidence only. The determination of whether
any evidence supports a later production adoption decision is a separate human
judgment at M5 closeout, not an automated output of the campaign.

Statistical significance tests (e.g., paired bootstrap, p-value thresholds) are
**later analysis methods** that may be applied to the raw ledger during M5.
They cannot, by themselves, determine a winner. A statistical test that fails
to reach a predeclared threshold means the evidence does not support a
comparative claim under that method; it does not mean any candidate is
preferred, rejected, or equivalent. The campaign records what happened; M5
interprets it with explicit limitations.

### 1.3 M1 Campaign Eligibility Outcome (locked + amended 2026-07-23)

Source of truth for per-candidate disposition:
[`CANDIDATE_ARTIFACTS.md`](./CANDIDATE_ARTIFACTS.md). M1 re-verification window:
`2026-07-22T15:19:00Z`–`2026-07-22T15:20:57Z` (UTC). No artifact was acquired
or executed.

| Campaign question | Answer after M1 amendment (2026-07-23) |
|---|---|
| Candidates `eligible for later M3 qualification` | **Qwen Code only.** OpenCode and Grok Build public source remain **`blocked at M1`** |
| Are at least two independently qualified configurations currently possible? | **No** |
| Comparative claim possible now? | **No** |
| Single-candidate observational path authorized? | **Yes — Qwen Code only** (`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`) |
| Phase 16 comparative evaluation status | **Blocked** for causal comparative claims until ≥2 M3 qualifications succeed |
| When may a comparative claim become possible? | Only if **two** later M3 qualifications actually succeed under identical environmental rules |

**Interpretation:** M1 locks comparative rules. The 2026-07-23 amendment
authorizes a **single-candidate observational path** for Qwen Code with
DeepSeek provider configuration. No comparative superiority or quality claim may
be derived from this path. OpenCode and Grok Build remain eligibility-blocked.

### 1.4 Single-Candidate Observational Path (authorized 2026-07-23)

| Property | Locked value |
|---|---|
| Candidate | Qwen Code (`qwen-code`) only |
| Evidence class | **Observational** only |
| Comparative claims | **Forbidden** |
| Quality / superiority claims | **Forbidden** |
| Provider | DeepSeek |
| Service | `https://api.deepseek.com` |
| Model | `deepseek-v4-flash` |
| M3a smoke cost ceiling | USD 1 |
| Phase 16 cumulative API cap | USD 3 (later authorization may define a new cap) |
| Execution | **Not authorized** by the amendment; requires separate M3 grants |
| M3a acquisition (2026-07-23) | **Complete** under separate grant (`M3A_ACQUISITION_EVIDENCE.md`); A-02/A-03 resolved; binary not launched |
| M3a recovery re-acquisition (2026-07-24) | **Complete** under separate acquisition-and-inspection-only grant after `/tmp` wipe (`M3A_ACQUISITION_EVIDENCE.md` §1.1); pinned archive + SHA256SUMS re-downloaded; SHA-256 match; licenses re-scanned; extract for static inspection only; binary/Node **not** launched; **no** qualification retry |
| M3 egress proof (2026-07-23) | **Complete** under separate grant (`M3_EGRESS_PROOF_EVIDENCE.md`); `api.deepseek.com:443` allow PASS; non-allowlisted block PASS |
| M3 DNS binding gate (2026-07-23) | **Design complete** (`M3_DNS_BINDING_GATE.md`); execution at launch required under credential-and-execution grant |
| M3 qualification smoke | **NO-GO** (`M3_QUALIFICATION_EVIDENCE.md`): latest authorized fresh session `m3q-20260724T072341Z-8f567943` — preflight (DNS/slirp/inner egress) **GO** before credential read+delete; write-capable `--approval-mode yolo`; Qwen **once** under **24** turns / **120s** (then-locked); TC-T01 rename **verified** (`FetchData` 0 / `RetrieveData` 11; host build/test 0); **`qwen_exit=55`** (`FatalBudgetExceededError` wall 120s) → dual GO fails; balance-before USD 3.95 (after unavailable); fixed parent reap recorded real inner exit **4**; full finalization incomplete. Prior latest `m3q-20260724T060109Z-45dd1c5f` (12-turn, exit 53). Session remains historical NO-GO. **Active policy 24 turns / 240s wall.** |
| M3 auth-configuration remediation (2026-07-24) | **Complete** (`M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md`): static lock used by latest smoke; auth-type failure mode remains cleared |
| M3 write-capable remediation (2026-07-24) | **Complete** (`M3_WRITE_CAPABLE_REMEDIATION_EVIDENCE.md`): locked `--approval-mode yolo` + post-exit reap/finalization used by latest smoke; remediation itself was not a qualification retry |
| M3 wall-time + exit-reap remediation (2026-07-24) | **Complete** (`M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md`): raised lock from 60s → **120s**; same-shell wait/reap so inner exit is not bash **127**; exercised by later smokes (`inner_wait_exit=4`); not a qualification retry. Superseded as **active** wall by 240s future-policy remediation |
| M3 wall-time 240s future-policy remediation (2026-07-24) | **Complete** (`M3_WALL_TIME_240S_POLICY_REMEDIATION_EVIDENCE.md`): active lock **`--max-wall-time 240s`** (was 120s); overall inner budget **320s**; historical 120s/60s session records unchanged; not a qualification retry |
| M3 fresh-session eligibility remediation (2026-07-24) | **Complete** (`M3_FRESH_SESSION_ELIGIBILITY_REMEDIATION_EVIDENCE.md`): egress preflight before credential; fresh session ID per grant; historical NO-GO informative only; consumed-but-unlaunched records `no candidate launch / no provider execution`; not a qualification retry |
| M3 post-session finalization remediation (2026-07-24) | **Complete** (`M3_POST_SESSION_FINALIZATION_REMEDIATION_EVIDENCE.md`): `launch_netns_inner` always returns 0 and publishes `INNER_WAIT_EXIT` so sticky bash `set -e` cannot skip balance-after/workspace/cleanup after non-zero Qwen; diagnosed from `m3q-20260724T072341Z-8f567943`; not a qualification retry; finalization path preserved under 240s policy |
| Locked smoke turn / time / spend ceilings | `--max-session-turns 24`, `--max-wall-time 240s`, smoke **USD 1**, Phase 16 cumulative **USD 3**. Latest smoke (`m3q-20260724T072341Z-8f567943`) ran under then-locked 24/120s (historical). Prior `m3q-20260724T060109Z-45dd1c5f` under then-12/120s. Historical `m3q-20260724T054307Z-481ad1de` used 60s/12 (unchanged). |
| Locked smoke approval mode | `--approval-mode yolo` (auto-approve all tools; host Bubblewrap required). Used by latest write-capable smoke |
| Next external grants | New qualification grant + **new** dedicated sub-key one-shot file if retry is authorized separately; keep write-capable lock + **24** turns + **240s** wall + fixed reap/finalization path; GO only on exit 0 **and** verified TC-T01 rename |

Full decision record: `M1_AMENDMENT_QWEN_OBSERVATIONAL.md`.

---

## 2. Campaign Phases and Gates

| Phase | Scope | Gate |
|---|---|---|
| **Pilot (M4a)** | 3 pilot tasks, one per qualified candidate, to prove telemetry and find campaign-breaking defects | Requires M3 qualification slices complete; pilot results cannot support a winner or held-out claim |
| **Tuning / Calibration (M4b)** | All 10 tuning tasks, minimum 3 valid repetitions per task/configuration | Requires pilot gate pass; parameter lock after tuning close; no task prompt or metric change after lock |
| **Held-Out (M4c)** | All 10 held-out tasks after tuning configuration is frozen | Requires tuning gate pass; no post-result configuration change permitted; held-out prompts inaccessible until this gate |
| **Analysis and Closeout (M5)** | Raw-first comparison, limitations, recommendation | Requires held-out gate pass; human acceptance is separate from technical readiness |

### 2.1 Parameter Lock

After tuning closes (M4b), the following are frozen and cannot change:

- Task prompts and workspace fixtures
- Verification commands and success criteria
- Per-task and global ceilings
- Invalidation rules
- Repetition count
- Metric definitions
- Runner configuration hash

---

## 3. Evidence Classes

Trials are categorized into three mutually exclusive evidence classes:

1. **Causal:** The candidate configuration is the sole independent variable;
   every declared validity gate passes; environment, isolation, and measurement
   are controlled. Causal evidence requires at least two qualified
   configurations.
2. **Observational:** Useful evidence produced under conditions that cannot
   fully control provider nondeterminism, rate limits, or environmental
   variance; or evidence from a single qualified candidate. Honest limitations
   are recorded; no causal claim is derived.
3. **Invalid:** Identity, isolation, task, telemetry, timeout, cleanup, or
   protocol failure. Invalid trials are preserved in the ledger, excluded from
   all score calculations, and must not be silently dropped.

---

## 4. Provider-Restricted Egress

Provider-restricted egress — the ability to allow outbound network access only
to specific provider API endpoints while denying all other traffic — is
**proven on the host as of M3 egress proof 2026-07-23**
(`M3_EGRESS_PROOF_EVIDENCE.md`). M2b had left it unproven
(`ISOLATION_EVIDENCE.md` §6 historical note).

**Human-accepted design (M1 amendment 2026-07-23) and proof status:**

| Property | Value |
|---|---|
| Posture (C-01) | **(b) Provider-restricted egress proof path** |
| Allowlist (C-02 / A-13) | **`api.deepseek.com:443` only** |
| Proof requirements | Allowlisted success, non-allowlisted block, logs under phase artifact root |
| Proof status (2026-07-23) | **GO** — allow HTTPS PASS (unauthenticated 401); block PASS; evidence retained |
| Enforcement for later trials | Reuse equivalent netns + allowlist architecture; execute DNS binding gate at launch (`M3_DNS_BINDING_GATE.md`); not host-wide unrestricted egress |
| DNS inside candidate sandbox | **Forbidden** — sandbox-only `/etc/hosts` (or equivalent) with single verified IPv4; no ambient resolver |
| TLS / SNI | `https://api.deepseek.com` with full certificate hostname validation; no bypass |
| Default without provider egress configured | Default-deny full network isolation (`--unshare-net` or equivalent) |

Host tooling at proof time: Bubblewrap `0.11.2`, `slirp4netns` 1.3.4, and
`socat` present; `pasta` still absent; Docker daemon/Podman unused. No package
install was required under C-01(b); only ephemeral netns configuration was
applied.

A Qwen Code trial requiring live DeepSeek access still requires a **separate
credential-and-execution grant**, **DNS binding execution at launch** (A-14),
and remaining argv/cost/isolation gates.

---

## 5. Evidence Retention and Disposal

### 5.1 Retention Location

Raw evidence is stored under the phase artifact root, outside the Zaide
worktree:

```
<artifact-root>/phase-16/
  records/         # Immutable append-only trial ledger
  artifacts/       # Output diffs, logs, hashes per trial
  held-out/        # Held-out task definitions (access-controlled)
```

The artifact root path is defined at M2a and recorded in the runner
configuration. It must be on a local filesystem with sufficient free space
(quota below).

### 5.2 Quota

- Maximum total evidence storage: **10 GiB**
- Maximum per-trial record (ledger entry + artifacts): **256 MiB**
- Quota exhaustion is a campaign-halting event; the runner must stop and report
  before any trial whose predicted storage would exceed the quota.

### 5.3 Access Boundary

- Raw records are append-only and must not be modified after write.
- During active campaign phases, only the runner process may write records.
- Read access for manual reconciliation (M4a, M4b, M4c, M5) is limited to the
  human reviewer; no automated process may alter records.

### 5.4 Disposal Trigger

Evidence is retained until **explicit human disposition** at or after M5
closeout. The runner does not automatically delete evidence. Disposal is a
separate reviewed decision; the trigger is the human acceptance of the M5
closeout recommendation. Until that trigger, all raw evidence must remain
available for independent reconciliation.

---

## 6. Repetitions and Variance

- Each task is executed exactly **3 independent times** per qualified candidate
  configuration.
- Each repetition starts from a fresh copy of the content-addressed workspace
  fixture.
- Where the model API permits, identical temperature and random-seed settings
  are requested. Provider nondeterminism is recorded, not hidden.
- All three repetitions count; no best-of or retry selection is permitted.

---

*M1 campaign lock — human-accepted 2026-07-23; Qwen Code observational-path
amendment human-accepted 2026-07-23. M2b completed 2026-07-23. M3 egress proof
completed 2026-07-23. M3 DNS binding gate defined 2026-07-23. Latest M3
qualification smoke session `m3q-20260724T072341Z-8f567943` (fresh, one only)
NO-GO: 24-turn / 120s (then-locked); Qwen exit 55 after verified rename; preflight
before key; finalization incomplete; inner exit 4. Prior
`m3q-20260724T060109Z-45dd1c5f`. Session remains historical NO-GO.
**Active policy 24 turns / 240s wall** (future-policy remediation 2026-07-24;
not a qualification retry). Candidate remains not qualified. No second
attempt under that grant. No M4.*
