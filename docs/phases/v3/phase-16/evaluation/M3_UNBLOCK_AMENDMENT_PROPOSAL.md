# Phase 16 M3 Unblock — Amendment Proposal (NOT ACCEPTED)

**Status:** **Accepted as the amendment vehicle on 2026-07-23.** Human-accepted
decisions for Qwen Code single-candidate observational unblock are recorded in
[`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`](./M1_AMENDMENT_QWEN_OBSERVATIONAL.md).
This proposal document remains the preflight and decision checklist reference.
Acceptance does **not** authorize M3 acquisition, egress tooling installation,
credential creation, provider API calls, or upstream execution. **M3 remains
blocked** until every external-side-effect gate listed below receives a separate
grant.

**Preflight basis:** Post-M2b NO-GO preflight completed **2026-07-23** (docs-only
synthesis; no artifact acquisition, no process launch, no network egress to
providers). Sources: `CANDIDATE_ARTIFACTS.md`, `ISOLATION_EVIDENCE.md`,
`CAMPAIGN_LOCK.md`, `THREAT_MODEL.md`, `TASK_CORPUS.md`.

---

## 1. NO-GO Preflight Outcome

| Gate | Preflight result | Evidence |
|---|---|---|
| M1 eligibility | **NO-GO** — zero candidates `eligible for later M3 qualification` | All three blocked at M1 (`CANDIDATE_ARTIFACTS.md` §4–§6) |
| M2b isolation mechanics | **GO** — repository-owned fake probes | `ISOLATION_EVIDENCE.md` §3 |
| Provider-restricted egress | **NO-GO** — not proven on current host | `ISOLATION_EVIDENCE.md` §6; `THREAT_MODEL.md` §2 |
| Comparative campaign path | **NO-GO** — requires ≥2 later M3 qualifications | `CAMPAIGN_LOCK.md` §1.1 |
| Single-candidate observational path | **NO-GO** — not authorized; requires ≥1 eligibility unblock + M3 qualification | `CAMPAIGN_LOCK.md` §1.3 |
| Upstream acquisition / execution | **NO-GO** — forbidden under current locks | M1 human acceptance 2026-07-23 |

**Preflight conclusion:** M3 qualification cannot be authorized for any candidate.
The minimum unblock path is a **narrow M1 amendment** that locks one candidate's
missing identity fields from **primary evidence or explicit human-supplied campaign
pins**—still docs-only at acceptance time—followed by separate M3 slice grants for
acquisition, credentials, egress proof, and execution.

---

## 2. Amendment Scope and Non-Effects

### 2.1 What this proposal amends

Only the M1 disposition field **`blocked at M1`** → **`eligible for later M3
qualification`** for **at most one** candidate at a time, and **only after** every
§4 decision item for that candidate is explicitly human-accepted and recorded.

### 2.2 What this proposal does not do

- Does **not** convert Phase 16 to a single-candidate campaign without a **separate,
  explicit** campaign-path amendment (`CAMPAIGN_LOCK.md` §1.1 comparative rule
  unchanged).
- Does **not** relax comparative criteria (≥2 independent M3 qualifications still
  required for causal comparative claims).
- Does **not** infer artifact↔source, product↔public-source, or changelog↔release
  mappings.
- Does **not** authorize M3a/M3b/M3c acquisition, install, extract-for-run,
  credential injection, provider API calls, or benchmark execution.
- Does **not** authorize host tooling installation for egress (external side effect).

### 2.3 Separate identities (unchanged)

| Identity | Qwen Code | OpenCode | Grok Build (public source) |
|---|---|---|---|
| Public source repo | [QwenLM/qwen-code](https://github.com/QwenLM/qwen-code) | [anomalyco/opencode](https://github.com/anomalyco/opencode) | [xai-org/grok-build](https://github.com/xai-org/grok-build) |
| Pinned release/tag (M1 observation) | `v0.20.1` / tag commit `305b0491…` | `v1.18.4` / tag commit `49c69c5e…` | **None** on public source |
| Separate product/changelog surface | Qwen Code product docs (multi-provider) | opencode.ai docs | xAI Grok CLI changelog / `x.ai/cli/install.sh` — **distinct, unmapped** |

---

## 3. Campaign-Level Decisions (apply to any unblock)

These decisions are **required once per campaign** before **any** candidate becomes
`eligible for later M3 qualification`. Accepting them for one candidate does not
accept them for others.

| ID | Decision | Options (human must pick one recorded choice) |
|---|---|---|
| **C-01** | **Provider-restricted egress posture** | **(a)** Keep default-deny full network isolation; any candidate requiring live provider access remains non-executable until egress is proven (**observational-only** if later executed offline-only). **(b)** Authorize a **separate** host-tooling grant to prove allowlisted provider HTTPS (install/configure `slirp4netns`, `pasta`, or `socat`; external side effect). **(c)** Accept **observational-only** classification for all network-requiring trials with documented egress limitation (`THREAT_MODEL.md` §2 option 2). |
| **C-02** | **Egress evidence design** (if C-01 is (b)) | Exact proof procedure: allowlisted endpoint pass, non-allowlisted block, capture mechanism, and pass/fail artifacts stored under phase artifact root. |
| **C-03** | **Monetary cost ceiling** (provider API spend) | Explicit USD (or currency) cap per M3 smoke slice and per campaign phase; TASK_CORPUS token/time ceilings alone are insufficient. |
| **C-04** | **Account isolation** | Dedicated phase-only provider account or sub-key; no production Zaide keys; no user ambient OAuth/browser sessions; revocation procedure documented. |
| **C-05** | **Archive license/notice review authority** | Named reviewer role and checklist: repo `LICENSE` at pin, acquired-archive embedded notices, `NOTICE`/`THIRD-PARTY-NOTICES` if present, clearance vs block criteria. |
| **C-06** | **Campaign path acknowledgment** | Confirm comparative rule unchanged; unblocking one candidate does **not** authorize single-candidate observational campaign unless **separate** explicit amendment. |

---

## 4. Per-Candidate Minimum Human Decisions

Each subsection lists decisions **required to make that candidate's eligibility path
reviewable**. Fields marked **observed (M1)** are recorded facts, not decisions.
Fields marked **UNRESOLVED** remain unknown and must **not** be inferred at
acceptance time unless the human explicitly supplies a campaign pin backed by
primary evidence.

### 4.1 Candidate A — Qwen Code

**M1 disposition today:** `blocked at M1`. **Observed (M1, not acquired):**
Linux x86_64 archive
`https://github.com/QwenLM/qwen-code/releases/download/v0.20.1/qwen-code-linux-x64.tar.gz`;
SHA-256 `2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e`
(GitHub asset digest + `SHA256SUMS` line agree); repo `LICENSE` Apache-2.0 at tag;
`NOTICE` absent (404).

| ID | Decision | Current state | Human must accept |
|---|---|---|---|
| **A-01** | **Immutable artifact identity** | Observed URL + digest above | Pin stands or human selects a different **primary-evidence** release asset (same tag only unless tag pin amended). |
| **A-02** | **Post-extract executable path** | UNRESOLVED | Exact path relative to extract root after `tar` extract of pinned archive (e.g. `qwen-code/qwen` — **placeholder only**; human must verify from archive layout **after** a future acquisition grant, or supply primary doc evidence of layout **without** executing). |
| **A-03** | **Structured non-interactive argv** | UNRESOLVED | Full argv array for Phase 16 smoke: workspace root flag, prompt/task input mode, turn limit, non-interactive/headless flags, output capture mode. README forms (`qwen`, `qwen -p "..."`) are **not** sufficient. |
| **A-04** | **Provider identity** | UNRESOLVED (multi-provider product) | Exactly one provider organization for the campaign configuration. |
| **A-05** | **Service endpoint identity** | UNRESOLVED | Exact HTTPS host + path prefix or gateway ID for A-04. |
| **A-06** | **Model ID** | UNRESOLVED | Exact model string/checkpoint ID for A-04/A-05. |
| **A-07** | **Credential class** | UNRESOLVED | e.g. provider API key env var name, OAuth device flow, or local-only (if C-01(a)); scope and injection boundary (sandbox env allowlist). |
| **A-08** | **Cost ceiling** | UNRESOLVED | Per C-03, bound to A-04–A-06. |
| **A-09** | **Account isolation** | UNRESOLVED | Per C-04, bound to A-04. |
| **A-10** | **Source-build mapping state** | UNRESOLVED (explicitly **unmapped**) | Human accepts **unmapped** (no claim that archive bytes equal tag commit `305b0491…`) **or** supplies primary build-reproducibility evidence. **Do not infer.** |
| **A-11** | **SOURCE_REV** | UNRESOLVED | Remains unknown unless primary manifest evidence is cited; acceptance may record `UNRESOLVED` paired with unmapped A-10. |
| **A-12** | **Archive license/notice boundary** | Partial (repo `LICENSE` only) | Per C-05: pre-M3 docs clearance of repo pin; post-acquisition mandatory in-archive notice scan before first execution. |
| **A-13** | **Egress enforcement/evidence** | UNRESOLVED | Per C-01/C-02; bind allowlist to A-05. |

**Still unknown after acceptance (must remain explicit, not inferred):** binary↔tag-commit
provenance; whether archive embeds additional licenses; which wire protocol/SDK path
is used for A-04; product/changelog identity separate from tag `v0.20.1`.

---

### 4.2 Candidate B — OpenCode

**M1 disposition today:** `blocked at M1`. **Observed (M1, not acquired):**
Linux x86_64 archive
`https://github.com/anomalyco/opencode/releases/download/v1.18.4/opencode-linux-x64.tar.gz`;
SHA-256 `bab463c3fb3224d388bb7cfad63f38703df9cf0be2cfd2ce8cb49d886b53a174`
(GitHub asset digest only—**no** official `SHA256SUMS` file); repo `LICENSE` MIT at
tag; `NOTICE` absent (404).

| ID | Decision | Current state | Human must accept |
|---|---|---|---|
| **B-01** | **Immutable artifact identity** | Observed URL + digest above | Pin stands or human selects different primary-evidence asset at tag `v1.18.4`. |
| **B-02** | **Post-extract executable path** | UNRESOLVED | Exact path after extract (human-verified post-acquisition or primary doc evidence). |
| **B-03** | **Structured non-interactive argv** | UNRESOLVED | Full argv for Phase 16 smoke; official docs describe multi-provider setup but no campaign argv. |
| **B-04** | **Provider identity** | UNRESOLVED | Exactly one provider for campaign. |
| **B-05** | **Service endpoint identity** | UNRESOLVED | Exact endpoint for B-04. |
| **B-06** | **Model ID** | UNRESOLVED | Exact model for B-04/B-05. |
| **B-07** | **Credential class** | UNRESOLVED | Per candidate configuration docs; bound to sandbox allowlist. |
| **B-08** | **Cost ceiling** | UNRESOLVED | Per C-03. |
| **B-09** | **Account isolation** | UNRESOLVED | Per C-04. |
| **B-10** | **Source-build mapping state** | UNRESOLVED (explicitly **unmapped**) | Accept **unmapped** or primary evidence; **do not infer** archive ↔ tag commit `49c69c5e…`. |
| **B-11** | **SOURCE_REV** | UNRESOLVED | No manifest pin observed. |
| **B-12** | **Checksum policy** | GitHub API digest only | Human accepts single-source digest policy and re-query procedure at M3 acquisition. |
| **B-13** | **Archive license/notice boundary** | Partial | Per C-05. |
| **B-14** | **Egress enforcement/evidence** | UNRESOLVED | Per C-01/C-02; bind to B-05. |

**Still unknown after acceptance (must remain explicit, not inferred):** binary↔source
mapping; embedded third-party notices inside archive; protocol/SDK pin; separate
opencode.ai product surface vs GitHub release identity.

---

### 4.3 Candidate C — Grok Build (public source)

**M1 disposition today:** `blocked at M1`. **Accepted identity:** [xai-org/grok-build](https://github.com/xai-org/grok-build) public source only. **Observed:** `SOURCE_REV`
`0f4d7c91b8b2b408333f6de1e8a76cb8eaa71899`; `main` HEAD `3af4d5d3…`; **no** Git
tags/releases on public source; README references `x.ai/cli/install.sh` and Cargo
source build—**distinct product installer, not a public-source release pin**.

| ID | Decision | Current state | Human must accept |
|---|---|---|---|
| **C-A01** | **Distributed artifact path** | UNRESOLVED | Human selects **one** primary-evidence path: **(i)** wait for versioned GitHub Release on public source, **(ii)** authorized source-build pin (Cargo lock + commit + built artifact hash—separate from product install), or **(iii)** explicitly reject product install script as campaign artifact (recommended default). **Do not map** Grok CLI changelog `0.2.106` or install script to public source without primary proof. |
| **C-A02** | **Immutable artifact identity** | UNRESOLVED | URL/registry coordinate + official checksum source (none observed for public source). |
| **C-A03** | **Release/tag identity** | UNRESOLVED | None on public source; human may pin `main` + `SOURCE_REV` only for source-build path with explicit non-release label. |
| **C-A04** | **Post-extract executable path** | UNRESOLVED | Depends on C-A01 path (`grok` after product install **not authorized** unless C-A01 selects product path with separate product-identity amendment). |
| **C-A05** | **Structured non-interactive argv** | UNRESOLVED | README `grok` / `cargo run -p xai-grok-pager-bin` are not campaign locks. |
| **C-A06** | **Provider identity** | UNRESOLVED | xAI or other—explicit pin required. |
| **C-A07** | **Service endpoint identity** | UNRESOLVED | Exact xAI or other endpoint. |
| **C-A08** | **Model ID** | UNRESOLVED | Exact Grok or other model string. |
| **C-A09** | **Credential class** | UNRESOLVED | Per authentication guide; sandbox boundary. |
| **C-A10** | **Cost ceiling** | UNRESOLVED | Per C-03. |
| **C-A11** | **Account isolation** | UNRESOLVED | Per C-04. |
| **C-A12** | **Product/changelog mapping** | UNRESOLVED / **distinct** | Human accepts **no mapping** between Grok Build public source and xAI Grok CLI product unless primary evidence is supplied. |
| **C-A13** | **Source-build mapping state** | UNRESOLVED | For any binary: unmapped unless evidence links artifact to `SOURCE_REV` or HEAD. |
| **C-A14** | **Archive license/notice boundary** | Partial (repo `LICENSE`, `THIRD-PARTY-NOTICES` on `main`) | Full tree review for source-build; N/A for unreleased binary. |
| **C-A15** | **Egress enforcement/evidence** | UNRESOLVED | Per C-01/C-02. |

**Still unknown after acceptance (must remain explicit, not inferred):** any
versioned distributed artifact on public source; relationship between Grok CLI
product and Grok Build repo; ACP surface as campaign protocol; official checksum
for any installer or binary.

---

## 5. Acceptance Procedure (if human proceeds)

1. Accept or reject this proposal document as a whole (**not accepted today**).
2. Accept campaign-level decisions **C-01** through **C-06**.
3. Select **one** candidate slice (A, B, or C) and accept **every** decision row
   for that candidate (A-01…A-13, B-01…B-14, or C-A01…C-A15).
4. Record accepted pins in an amended `CANDIDATE_ARTIFACTS.md` disposition update
   (separate commit; still docs-only until M3 grant).
5. **Do not** start M3 acquisition until a **separate** M3 slice authorization
   explicitly grants external side effects.

Unblocking candidate A or B alone yields **at most one** `eligible for later M3
qualification` configuration. Comparative campaign remains blocked until a
**second** candidate completes the same decision package and (later) M3
qualification.

---

## 6. External-Side-Effect Gates (post-amendment, pre-M3 execution)

Even after eligibility unblock, each M3 slice requires separate authorization for:

| Side effect | Gate |
|---|---|
| Archive download / extract | M3a/b/c acquisition grant |
| Provider API calls | Credential grant + cost tracking |
| Host egress tooling install | C-01(b) grant |
| Process launch of upstream binary | M3 qualification grant + isolation re-check |
| Held-out materialization | M4c gate (unchanged) |

---

## 7. Provenance Facts Explicitly Not Inferred

The preflight and this proposal **do not** establish:

1. Qwen Code `qwen-code-linux-x64.tar.gz` bytes were built from tag commit
   `305b049100606fa093a14b5cd849bff3be16e31a` or release metadata target
   `1d003fd83f028939c7a235fdc34d8957609beb3b`.
2. OpenCode `opencode-linux-x64.tar.gz` bytes were built from tag commit
   `49c69c5ed3ccf706b61b3febb43c8aaff7f8325e` or metadata target
   `4872c48c230728150e8e3406722943450ed58dcb`.
3. Any mapping from xAI Grok CLI changelog, install script, or product version to
   Grok Build public-source `SOURCE_REV` `0f4d7c91…` or HEAD `3af4d5d3…`.
4. Any single provider/service/model configuration shared across Qwen Code,
   OpenCode, or Grok Build (comparability is a **later** campaign design choice,
   not assumed).
5. Provider-restricted egress enforceability on the current host without
   additional tooling or observational downgrade.
6. Post-extract executable layout for any Linux archive (not observed without
   acquisition).
7. Embedded license/notice completeness inside any distributed archive (not
   inspected).

---

*Proposed 2026-07-23. Accepted as amendment vehicle 2026-07-23 via
`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`. M3 execution remains blocked pending
separate grants.*
