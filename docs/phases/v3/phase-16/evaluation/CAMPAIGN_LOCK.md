# Phase 16 M1: Campaign Lock

**Status:** M1 explicitly human-accepted on 2026-07-23 (all-blocked candidate
eligibility lock). **M2a remains unauthorized.**

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

### 1.3 M1 Campaign Eligibility Outcome (locked)

Source of truth for per-candidate disposition:
[`CANDIDATE_ARTIFACTS.md`](./CANDIDATE_ARTIFACTS.md). M1 re-verification window:
`2026-07-22T15:19:00Z`–`2026-07-22T15:20:57Z` (UTC). No artifact was acquired
or executed.

| Campaign question | Locked M1 answer |
|---|---|
| Candidates `eligible for later M3 qualification` | **None.** Qwen Code, OpenCode, and Grok Build public source are each **`blocked at M1`** |
| Are at least two independently qualified configurations currently possible? | **No** |
| Comparative claim possible at M1? | **No** |
| Single-candidate observational path currently authorized? | **No.** That path still requires at least one later eligibility unblock and a successful M3 qualification |
| Phase 16 comparative evaluation status | **Blocked** until eligibility and qualification conditions change under later authorization |
| When may a comparative claim become possible? | Only if **two** later M3 qualifications actually succeed under identical environmental rules |

**Interpretation:** M1 locks the campaign rules and records that the accepted
candidate set is **not currently eligible** for later M3 qualification.
Phase 16 is **not** re-scoped to a single-candidate observational campaign at
this lock; it is **eligibility-blocked** for multi-candidate comparison, with
future observational or comparative paths contingent on later unblocking and
qualification facts. No comparative claim is possible unless two later M3
qualifications actually succeed.

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
**explicitly unproven at M1**.

The M0 substrate audit recorded:

- Bubblewrap `0.11.2` installed; a harmless `--unshare-all`, read-only-host,
  temporary-filesystem `/bin/true` proof succeeded.
- `slirp4netns`, `pasta`, and `socat` are absent.
- Docker daemon access is unavailable; Podman is absent.

**M1 does not assert provider-restricted egress as an established enforcement
fact.** It is a future qualification requirement that must be proven during M2b
(isolation proof) before any candidate executes. M2b must either:

- Demonstrate provider-only egress with current host tools and a reproducible
  proof, OR
- Record that provider-restricted egress cannot be enforced on the current
  substrate and classify all trials as observational with explicit egress
  limitations.

Until M2b resolves this, the campaign plan assumes default-deny network
isolation (`--unshare-net` or equivalent) with no provider access. A candidate
that requires network access cannot execute until egress is proven.

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

*M1 campaign lock — human-accepted 2026-07-23. All three candidates are blocked
at M1 for later M3 eligibility. No candidate result has been observed. All
execution gates remain closed. M2a remains unauthorized.*
