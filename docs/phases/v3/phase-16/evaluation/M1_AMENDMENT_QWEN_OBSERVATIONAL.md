# Phase 16 M1 Amendment — Qwen Code Single-Candidate Observational

**Status:** **Human-accepted on 2026-07-23 (docs-only).** This amendment accepts
`M3_UNBLOCK_AMENDMENT_PROPOSAL.md` and records explicit campaign- and
candidate-level decisions for **Candidate A (Qwen Code)** on a **single-candidate
observational path only**. It does **not** authorize artifact acquisition,
host egress tooling installation, credential creation, provider API calls,
upstream binary execution, or comparative or quality claims.

**Amendment type:** M1 eligibility unblock + separate single-candidate
observational campaign-path authorization (C-06).

**Non-effects (unchanged):**

- OpenCode and Grok Build public source remain **`blocked at M1`**.
- Comparative campaign rules remain unchanged; causal comparative claims require
  ≥2 independently qualified configurations.
- M3a/M3b/M3c acquisition, egress proof execution, credential injection, and
  process launch each require **separate** explicit authorization grants.

---

## 1. Proposal Acceptance

| Item | Recorded decision |
|---|---|
| M3 unblock amendment proposal | **Accepted** (`M3_UNBLOCK_AMENDMENT_PROPOSAL.md`, 2026-07-23) |
| Candidate scope | **Qwen Code only** |
| Campaign path | **Single-candidate observational only**; no comparative or quality claims |
| Evidence class for this path | **Observational** (`CAMPAIGN_LOCK.md` §3) |

---

## 2. Campaign-Level Decisions (C-01…C-06)

| ID | Decision | Human-accepted choice (2026-07-23) |
|---|---|---|
| **C-01** | Provider-restricted egress posture | **(b) Provider-restricted egress proof path.** Authorize a **separate** host-tooling grant (when explicitly granted) to install/configure egress tooling and prove allowlisted provider HTTPS. Default-deny full isolation remains in force until proof succeeds. |
| **C-02** | Egress evidence design | Allow **only** `api.deepseek.com:443`. Proof must demonstrate: (1) allowlisted endpoint success, (2) non-allowlisted destination blocked, (3) logs preserved under the phase artifact root. |
| **C-03** | Monetary cost ceiling | **USD 1** per M3a smoke slice; **USD 3** current Phase 16 cumulative provider API spend cap. Later authorization may define a new cumulative cap. TASK_CORPUS token/time ceilings alone remain insufficient for provider spend. |
| **C-04** | Account isolation | Create a **dedicated Phase 16 DeepSeek sub-key only after a separate execution grant.** Never use existing `~/.config` credentials or ambient OAuth/browser sessions. Inject **only** `DEEPSEEK_API_KEY` via sandbox env allowlist. Revoke the sub-key at M3 completion. |
| **C-05** | Archive license/notice review authority | **Project owner** is final license approver. Re-check tag `LICENSE` before M3a acquisition. Scan downloaded archive `LICENSE`, `NOTICE`, and `THIRD-PARTY-NOTICES` before any execution. **Block on uncertainty.** |
| **C-06** | Campaign path acknowledgment | Comparative path **unchanged**. This document is the **separate explicit amendment** authorizing the single-candidate observational path for Qwen Code only. Unblocking Qwen Code does not authorize observational or comparative paths for OpenCode or Grok Build. |

---

## 3. Candidate A — Qwen Code Decisions (A-01…A-13)

| ID | Field | Human-accepted value (2026-07-23) |
|---|---|---|
| **A-01** | Immutable artifact identity | Pin `v0.20.1` Linux x86_64 archive: `https://github.com/QwenLM/qwen-code/releases/download/v0.20.1/qwen-code-linux-x64.tar.gz`; SHA-256 `2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e` (GitHub asset digest + official `SHA256SUMS` line agree at M1 re-verification). |
| **A-02** | Post-extract executable path | M1: **`UNRESOLVED`**. **M3a (2026-07-23): `RESOLVED`** → `qwen-code/bin/qwen` (see `M3A_ACQUISITION_EVIDENCE.md`). Do not execute before further approval. |
| **A-03** | Structured non-interactive argv | M1: **`UNRESOLVED`**. **M3a (2026-07-23): support surface `RESOLVED`** (headless `-p`/`--prompt` or positional; `--model`, `--output-format`, turn/time caps, `--approval-mode`). Owner must still lock smoke approval-mode/ceilings before execution. |
| **A-04** | Provider identity | **DeepSeek** |
| **A-05** | Service endpoint identity | **`https://api.deepseek.com`** (host/path pin; **not** `/v1` suffix) |
| **A-06** | Model ID | **`deepseek-v4-flash`** |
| **A-07** | Credential class | Sandbox env allowlist permits **only** `DEEPSEEK_API_KEY`. Never expose credential values in logs or persisted evidence. |
| **A-08** | Per-candidate cost ceiling | **USD 1** M3a smoke cap (within C-03) |
| **A-09** | Per-candidate account isolation | Per C-04: dedicated Phase 16 DeepSeek sub-key created only after separate execution grant; revoke at M3 completion. |
| **A-10** | Source-build mapping state | **Explicitly unmapped.** No claim that archive bytes equal tag commit `305b049100606fa093a14b5cd849bff3be16e31a`. |
| **A-11** | SOURCE_REV | **`UNRESOLVED`** (paired with unmapped A-10) |
| **A-12** | Archive license/notice boundary | Per C-05: tag `LICENSE` re-check before M3a; mandatory in-archive `LICENSE`/`NOTICE`/`THIRD-PARTY-NOTICES` scan before execution; block on uncertainty. Pre-amendment: Apache-2.0 at tag `v0.20.1`; `NOTICE` absent at tag (404). **M3a scan:** archive `LICENSE` Apache-2.0 identical to tag; `NOTICE` and `THIRD-PARTY-NOTICES` absent. **Project owner approved the recorded execution license posture on 2026-07-23**; see `M3A_ACQUISITION_EVIDENCE.md` §4. |
| **A-13** | Egress enforcement/evidence | Allow **only** `api.deepseek.com:443`. All other external hosts, ports, and DNS paths **denied**. Proof design per C-02; **enforcement proven 2026-07-23** (`M3_EGRESS_PROOF_EVIDENCE.md`). Later launch must reuse equivalent allowlist enforcement. |
| **A-14** | DNS binding at launch | **Defined 2026-07-23** (`M3_DNS_BINDING_GATE.md`). Host-side resolution immediately before launch; sandbox-only `/etc/hosts` (or equivalent) maps exactly one verified IPv4 for `api.deepseek.com`; nft allowlist must match hosts IP; **no ambient DNS** inside candidate sandbox; TLS/SNI hostname validation preserved. **Not yet executed** — mandatory under credential-and-execution grant. |

**M1 disposition after amendment:** **`eligible for later M3 qualification`**
(single-candidate observational path). Invocation fields A-02/A-03 were
`UNRESOLVED` at amendment time and were **resolved at M3a (2026-07-23)** from
inspection without execution (`M3A_ACQUISITION_EVIDENCE.md`).

**Still explicit, not inferred:** binary↔tag-commit provenance; embedded archive
licenses; wire protocol/SDK pin; separate product/changelog identity beyond tag
`v0.20.1`.

---

## 4. External-Side-Effect Gates (not authorized by this amendment)

| Side effect | Required separate grant | Status after M3 egress proof (2026-07-23) |
|---|---|---|
| Download/extract pinned Qwen Code archive | M3a acquisition-and-inspection grant | **Done** under M3a grant (2026-07-23); **re-done** under recovery grant (2026-07-24) after `/tmp` wipe — inspection only (`M3A_ACQUISITION_EVIDENCE.md` §1.1) |
| Install/configure egress tooling (`slirp4netns`, `pasta`, `socat`, or equivalent) | C-01(b) host-tooling grant | **Done** under egress-proof grant: inventory only; **no package install** (`slirp4netns`/`socat` already present); ephemeral netns config only |
| Run egress proof (allowed + blocked destinations) | Egress proof grant | **Done** — **GO** (`M3_EGRESS_PROOF_EVIDENCE.md`) |
| Create DeepSeek sub-key / inject credential | Credential-and-execution grant | Performed under latest qualification grant only via one-shot file → `DEEPSEEK_API_KEY`; consumed; value not disclosed |
| DNS binding execution at launch (A-14) | Credential-and-execution grant | **Executed GO** on latest smoke (`m3q-20260724T054307Z-481ad1de`); design remains `M3_DNS_BINDING_GATE.md` |
| Provider API calls | Execution grant + cost tracking | Performed once under latest smoke (authenticated Qwen run + balance before/after); session balance delta USD 0.00 |
| Launch upstream Qwen Code binary | M3 qualification grant + isolation re-check + A-02/A-03 resolution + A-14 binding | Latest smoke session `m3q-20260724T054307Z-481ad1de` launched Qwen once under yolo; TC-T01 rename verified; **`qwen_exit=55`** wall 60s; overall **NO-GO** — see `M3_QUALIFICATION_EVIDENCE.md`. **No second attempt** under that grant. |
| Locked max-session-turns / wall-time / spend ceilings | Qualification grant | Active locked smoke ceilings are **12** turns, **120s** wall-time, **USD 1** smoke / **USD 3** cumulative (`M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md`). Historical session `m3q-20260724T054307Z-481ad1de` used **60s**. |

---

## 5. Cross-References

Updated disposition and campaign-path records:

- `CANDIDATE_ARTIFACTS.md` §4, §8
- `CAMPAIGN_LOCK.md` §1.3, §1.4, §4
- `IMPLEMENTATION_PLAN.md` status and §13

---

*M1 amendment — human-accepted 2026-07-23. Docs-only at acceptance. Subsequent
M3a acquisition-and-inspection (2026-07-23) under a separate grant acquired the
pinned archive outside the repository and resolved A-02/A-03 without launching
the binary (`M3A_ACQUISITION_EVIDENCE.md`). Recovery re-acquisition 2026-07-24
after `/tmp` wipe (inspection only; no qualification retry). Subsequent M3
egress proof (2026-07-23) under a separate grant proved `api.deepseek.com:443`
allowlisted HTTPS and blocked non-allowlisted destinations
(`M3_EGRESS_PROOF_EVIDENCE.md`). M3 DNS binding gate defined 2026-07-23
(`M3_DNS_BINDING_GATE.md`). Repository smoke policy aligned to 12 turns /
write-capable yolo / USD 1 / USD 3 on 2026-07-24; active wall later raised to
**120s** with same-shell reap fix
(`M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md`). Latest qualification smoke
`m3q-20260724T054307Z-481ad1de` **NO-GO** (rename verified; exit 55 at
historical 60s wall). No credentials created under recovery; recovery did not
launch the binary.
