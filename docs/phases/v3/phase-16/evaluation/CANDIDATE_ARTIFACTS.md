# Phase 16 M1: Candidate Artifacts

**Status:** M1 explicitly human-accepted on 2026-07-23. **M1 amendment
(human-accepted 2026-07-23):** Qwen Code is **`eligible for later M3
qualification`** on the single-candidate observational path
(`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`). OpenCode and Grok Build public source
remain **`blocked at M1`**. **M2a was explicitly human-accepted on 2026-07-23**
(standalone offline runner contract and fake-candidate core). **M2b was
completed on 2026-07-23** (`ISOLATION_EVIDENCE.md`). **M3a
acquisition-and-inspection was completed on 2026-07-23** under a separate
explicit grant (`M3A_ACQUISITION_EVIDENCE.md`): pinned archive downloaded to
the phase artifact root (not the Zaide repository), SHA-256 verified, tag and
in-archive licenses scanned, A-02/A-03 resolved from static inspection.
**Upstream binary was not launched.** **M3 egress proof was completed on
2026-07-23** (`M3_EGRESS_PROOF_EVIDENCE.md`). **M3 DNS binding gate defined
2026-07-23** (`M3_DNS_BINDING_GATE.md`). **M3 auth-configuration / write-capable remediations (2026-07-24)** locked auth
argv + `modelProviders` wiring and `--approval-mode yolo` with post-exit
reaping (`M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md`,
`M3_WRITE_CAPABLE_REMEDIATION_EVIDENCE.md`). **M3 wall-time + exit-reap
remediation (2026-07-24)** raised the active smoke wall lock to **120s** and
fixed same-shell wait/reap
(`M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md`). **M3a recovery
re-acquisition (2026-07-24)** after `/tmp` wipe re-verified hash and licenses
and re-extracted for inspection only (`M3A_ACQUISITION_EVIDENCE.md` §1.1).
**Latest M3 qualification smoke** (`m3q-20260724T060109Z-45dd1c5f`) **NO-GO**:
write-capable yolo under locked **12** turns / **120s** wall verified TC-T01
rename but `qwen_exit=53` (turn limit); spend balance delta USD 0.00; fixed
parent-shell reap recorded real inner exit 4; candidate still **not qualified**
(`M3_QUALIFICATION_EVIDENCE.md`). Locked smoke ceilings (active): max **12**
session turns, **120s** wall-time, **USD 1** smoke / **USD 3** cumulative.

**M1 source re-verification window:** `2026-07-22T15:19:00Z` through
`2026-07-22T15:20:57Z` (UTC). Methods: GitHub REST API release/ref/contents
metadata, official README/LICENSE text at pinned refs, official documentation
HTML, and the official Qwen `SHA256SUMS` text listing. M1 itself downloaded no
candidate package. **M3a (2026-07-23)** later acquired only the pinned Linux
x64 archive and `SHA256SUMS` under the phase artifact root; no binary was
executed, no install-script pipe was used, and no benchmark was run.

---

## 1. Terminology and Distinction Preservation

The evaluation framework preserves strict distinctions between every identity
field. Mappings are never inferred. Unknown or unverified fields are recorded as
`UNRESOLVED` and, at M1 disposition time, are always paired with an explicit
**blocked at M1** (or narrower) consequence.

| Field | Meaning |
|---|---|
| **Public source HEAD** | Mutable development branch tip on a public repository host, observed at a specific timestamp |
| **Release / tag** | A specific human-created Git tag or GitHub Release |
| **Tag commit** | The exact immutable SHA-1 commit hash pointed to by a tag |
| **Release metadata target** | A distinct target-branch or release-track revision, separately observed; not the tag commit |
| **SOURCE_REV** | The explicit source revision pinned in build manifests or lockfiles |
| **Distributed artifact** | A packaged binary, registry coordinate, container image, or tarball |
| **Product / changelog** | High-level release notes or marketed version number; distinct from source identity |
| **Provider** | Organization hosting the model inference service |
| **Service** | Specific API endpoint or gateway providing inference |
| **Model** | Specific weighted neural network checkpoint or model identifier |
| **Protocol / SDK** | Wire format specification or client library used for communication |

---

## 2. M1 Eligibility Rule (locked)

A candidate is **`eligible for later M3 qualification`** only when **all** of
the following are actually established from primary official evidence at M1:

1. Immutable artifact URL or registry coordinate for the evaluation host
2. Official checksum source (SHA-256 or stronger) for that coordinate
3. License / notices review route (where to read LICENSE, NOTICE, and third-party notices before any acquisition)
4. Source-build mapping **state** (mapped with evidence, or explicitly unmapped)
5. Exact evaluation invocation (executable path after acquisition + structured argument vector; no shell interpolation)
6. Exact provider identity, service identity, and model identity for the campaign configuration
7. Credential and egress requirements for that configuration
8. Explicit exclusion rules

If any item is missing, the disposition is **`blocked at M1`**. `UNRESOLVED`
fields are allowed only when paired with that blocked disposition and a
concrete missing-evidence reason, **except** where an explicit M1 amendment
records accepted `UNRESOLVED` invocation fields deferred to a separately
authorized M3a acquisition-and-inspection slice (see §4 and
`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`). **Eligibility is not qualification:**
even an eligible candidate remains non-executable until its M3 slice succeeds
under later authorization.

---

## 3. Accepted M0 Candidate Set (unchanged membership)

The three candidates below were explicitly accepted at M0 on 2026-07-22. No
candidate has been substituted, removed, or added. M0 identity observations are
carried forward. M1 re-checks primary sources and records disposition.

| Candidate | M0 public source | M1 disposition |
|---|---|---|
| A — Qwen Code | [QwenLM/qwen-code](https://github.com/QwenLM/qwen-code) | **eligible for later M3 qualification** (single-candidate observational; M1 amendment 2026-07-23) |
| B — OpenCode | [anomalyco/opencode](https://github.com/anomalyco/opencode) | **blocked at M1** |
| C — Grok Build (public source) | [xai-org/grok-build](https://github.com/xai-org/grok-build) | **blocked at M1** |

---

## 4. Candidate A: Qwen Code — **eligible for later M3 qualification**

**Campaign configuration:** single-candidate observational path only (M1
amendment 2026-07-23; `M1_AMENDMENT_QWEN_OBSERVATIONAL.md`). No comparative or
quality claims.

### 4.1 M0 identities carried forward

- **Public source:** [QwenLM/qwen-code](https://github.com/QwenLM/qwen-code)
- **Public source HEAD (M0):** `refs/heads/main` at
  `d064bd7dcf98e0255283068a775f6e49d70db8aa`, observed
  `2026-07-22T14:48:47Z`
- **Release / tag (M0):** `v0.20.1`, published 2026-07-21
- **Tag commit (M0):** `305b049100606fa093a14b5cd849bff3be16e31a`
- **Release metadata target (M0):** Target string `release/v0.20.1`; separately
  observed target-branch commit `1d003fd83f028939c7a235fdc34d8957609beb3b`;
  neither is treated as the tag commit

### 4.2 M1 re-verification (primary sources only)

| Check | Result at M1 re-verification |
|---|---|
| Tag `v0.20.1` still resolves | Yes — `refs/tags/v0.20.1` → commit `305b049100606fa093a14b5cd849bff3be16e31a` (`2026-07-22T15:19:00Z`) |
| Moving `main` HEAD | Unchanged from M0: `d064bd7dcf98e0255283068a775f6e49d70db8aa` (`2026-07-22T15:20:57Z`) |
| GitHub Release assets for `v0.20.1` | Present, including platform archives and `SHA256SUMS` |
| License SPDX (repo metadata) | Apache-2.0 |
| `LICENSE` at tag `v0.20.1` | Present (HTTP 200 via contents API) |
| `NOTICE` at tag `v0.20.1` | Absent (HTTP 404) |
| README at tag `v0.20.1` | Present; documents multi-protocol providers and usage forms |

**Pinned immutable artifact (A-01; acquired at M3a 2026-07-23):**

- URL:
  `https://github.com/QwenLM/qwen-code/releases/download/v0.20.1/qwen-code-linux-x64.tar.gz`
- SHA-256 (human-accepted pin):
  `2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e`
- Official checksum sources agreeing at M1 re-verification and M3a re-query:
  - Release asset digest reported by GitHub API:
    `sha256:2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e`
  - Official release file
    `https://github.com/QwenLM/qwen-code/releases/download/v0.20.1/SHA256SUMS`
    line:
    `2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e  qwen-code-linux-x64.tar.gz`
- **M3a verification (2026-07-23; re-verified on recovery 2026-07-24):**
  downloaded archive + `SHA256SUMS` under
  `/tmp/phase16-artifacts/phase-16/artifacts/qwen-code/v0.20.1/download/`;
  computed SHA-256 **matches pin**; `sha256sum -c` **OK**. Full record:
  `M3A_ACQUISITION_EVIDENCE.md` (recovery §1.1). **Do not execute** until DNS
  binding at launch, credentials, and a further execution grant.

**License / notices review route (A-12 / C-05):**

- Final approver: **project owner**
- Before M3a acquisition: re-check `LICENSE` at tag `v0.20.1` (Apache-2.0) —
  **done at M3a** (HTTP 200; Apache-2.0 text)
- M3a in-archive scan:
  - `LICENSE`: **present**, Apache-2.0, **identical** to tag `LICENSE`
  - `NOTICE`: **absent** (matches tag 404)
  - `THIRD-PARTY-NOTICES`: **absent** at package root; many per-dependency
    licenses exist under `node/` and `lib/`
- **Project-owner C-05 clearance (2026-07-23):** approved the recorded execution
  license posture (Apache-2.0 root license, absent root `NOTICE` /
  `THIRD-PARTY-NOTICES`, and present per-dependency license files). See
  `M3A_ACQUISITION_EVIDENCE.md` §4.

**Locked provider configuration (M1 amendment 2026-07-23):**

| Field | Value |
|---|---|
| Provider (A-04) | DeepSeek |
| Service (A-05) | `https://api.deepseek.com` |
| Model (A-06) | `deepseek-v4-flash` |
| Credential (A-07) | Sandbox allowlist: `DEEPSEEK_API_KEY` only; never log or persist credential values |
| M3a smoke cost ceiling (A-08) | USD 1 |
| Campaign cumulative cap (C-03) | USD 3 (later authorization may define a new cap) |
| Account isolation (A-09 / C-04) | Dedicated Phase 16 DeepSeek sub-key after separate execution grant; never `~/.config` credentials; revoke at M3 completion |
| Egress allowlist (A-13 / C-02) | `api.deepseek.com:443` only; all other external hosts, ports, and DNS paths denied; **enforcement proven 2026-07-23** (`M3_EGRESS_PROOF_EVIDENCE.md`) |
| DNS binding at launch (A-14) | **Defined 2026-07-23** (`M3_DNS_BINDING_GATE.md`): host-side single-IPv4 resolution; sandbox-only `/etc/hosts`; nft parity; no ambient candidate DNS; TLS/SNI preserved; **not yet executed** |
| Source-build mapping (A-10) | **Explicitly unmapped** |
| SOURCE_REV (A-11) | **`UNRESOLVED`** |

**Documented usage forms (not the locked evaluation invocation):**

- Interactive: `qwen`
- Headless form documented in README: `qwen -p "..."`
- Auth configuration: interactive `/auth` and official Authentication /
  Model Providers docs (multi-provider)

### 4.3 Field resolution after M3a (execution still blocked)

| Field | State after M3a (2026-07-23) | Resolution gate / notes |
|---|---|---|
| **Post-extract executable path (A-02)** | **`RESOLVED`:** `qwen-code/bin/qwen` (shell launcher → bundled `node/bin/node` + `lib/cli-entry.js`) | Static inspection only; binary not executed |
| **Structured non-interactive argv (A-03)** | **`RESOLVED` (support surface + locked smoke policy):** headless via `-p`/`--prompt` or positional prompt; auth/model/output/approval/ceilings locked in `Phase16M3QualificationPolicy` / orchestrator (`--max-session-turns 12`, `120s`, `--approval-mode yolo` write-capable, DeepSeek OpenAI-compatible auth); workspace = process CWD | Execution still requires a separate qualification grant + credentials + DNS/egress gates; see `M3A_ACQUISITION_EVIDENCE.md` §6, `M3_WRITE_CAPABLE_REMEDIATION_EVIDENCE.md`, and `M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md` |
| **SOURCE_REV (A-11)** | `UNRESOLVED` | Accepted paired with unmapped A-10; not inferred |
| **Protocol / SDK** | Partially observed | DeepSeek preset uses OpenAI-compatible protocol; not a separate product pin |
| **Product / changelog** | Tag `v0.20.1` only | Separate product/changelog surface not mapped beyond release tag |

### 4.4 Disposition after M3a

**`eligible for later M3 qualification`** (single-candidate observational path;
M1 amendment human-accepted 2026-07-23). **Not qualified.** M3a
acquisition-and-inspection **complete** (`M3A_ACQUISITION_EVIDENCE.md`).

**Completed under M3a grant:** download and extract under phase artifact root;
SHA-256 pin verification; tag + in-archive `LICENSE` scan; A-02/A-03 resolution
from inspection.

**Qualification smoke status:** attempted under separate grants; latest session
`m3q-20260724T060109Z-45dd1c5f` **NO-GO** (write-capable yolo; **12** turns /
**120s** wall; TC-T01 rename verified; `qwen_exit=53` turn limit; spend balance
delta USD 0.00; real inner exit 4) — see `M3_QUALIFICATION_EVIDENCE.md`.
Candidate remains **not qualified**. Comparative claims remain forbidden.
**No second attempt** under that grant.

**Completed under M3 egress-proof grant (2026-07-23):** provider-restricted
egress for `api.deepseek.com:443` only (`M3_EGRESS_PROOF_EVIDENCE.md`).

**Re-qualification still requires:** new dedicated sub-key (C-04); separate M3
qualification grant; keep locked write-capable `--approval-mode yolo` + reap
fix; isolation re-check; DNS binding at launch (A-14); egress allowlist
reuse; dual GO = Qwen exit 0 **and** verified TC-T01 workspace change.

**Exclusion rules (locked while not qualified at M3):**

- No execution of Qwen Code artifacts without a subsequent execution grant
- No use of install-script pipes (`curl | bash`) for this campaign
- No ambient user API keys, `~/.config` credentials, or browser OAuth sessions
- No substitution of npm `@latest` / Homebrew floating packages for the pinned
  release coordinate
- Missing or deferred fields are recorded, not guessed

---

## 5. Candidate B: OpenCode — **blocked at M1**

### 5.1 M0 identities carried forward

- **Public source:** [anomalyco/opencode](https://github.com/anomalyco/opencode)
- **Public source HEAD (M0):** `refs/heads/dev` at
  `0a601cf334b9a83cc2854108a2b860f25e6e7e8e`, observed
  `2026-07-22T14:48:47Z`
- **Release / tag (M0):** `v1.18.4`, published 2026-07-20
- **Tag commit (M0):** `49c69c5ed3ccf706b61b3febb43c8aaff7f8325e`
- **Release metadata target (M0):** Distinct target revision
  `4872c48c230728150e8e3406722943450ed58dcb`; not treated as the tag commit

### 5.2 M1 re-verification (primary sources only)

| Check | Result at M1 re-verification |
|---|---|
| Tag `v1.18.4` still resolves | Yes — `refs/tags/v1.18.4` → commit `49c69c5ed3ccf706b61b3febb43c8aaff7f8325e` (`2026-07-22T15:19:00Z`) |
| Moving `dev` HEAD | Unchanged from M0: `0a601cf334b9a83cc2854108a2b860f25e6e7e8e` (`2026-07-22T15:20:57Z`) |
| GitHub Release assets for `v1.18.4` | Present (37 assets), including CLI archives and desktop packages |
| Separate `SHA256SUMS` asset | **Not present** on this release |
| License SPDX (repo metadata) | MIT |
| `LICENSE` at tag `v1.18.4` | Present (HTTP 200) |
| `NOTICE` at tag `v1.18.4` | Absent (HTTP 404) |
| README at tag `v1.18.4` | Present; documents install methods and points to https://opencode.ai/docs |

**Immutable artifact coordinate observed for Linux x86_64 evaluation host
(not acquired):**

- URL:
  `https://github.com/anomalyco/opencode/releases/download/v1.18.4/opencode-linux-x64.tar.gz`
- Official checksum source: GitHub Release asset digest reported by the GitHub
  API at M1 re-verification:
  `sha256:bab463c3fb3224d388bb7cfad63f38703df9cf0be2cfd2ce8cb49d886b53a174`
- **Checksum plan if later authorized to acquire:** re-query the same release
  asset metadata (or an official signed sum file if one is later published);
  verify the downloaded archive against the pinned digest above before any
  execution. No download performed at M1.

**License / notices review route (docs-only at M1):**

- Primary: `LICENSE` at tag `v1.18.4` (MIT)
- Repository license metadata: MIT
- `NOTICE` file: not present at the pinned tag (404)
- Pre-acquisition archive notice inspection remains future work under an M3
  grant (not performed).

**Documented install/usage forms (not a locked evaluation invocation):**

- Install script / package managers / release archives documented in README
- Official docs site https://opencode.ai/docs (HTTP 200 at M1) describes
  multi-provider configuration and requires API keys for chosen LLM providers
- No single Phase 16 provider/model configuration is selected in primary docs

### 5.3 Fields still UNRESOLVED (each blocks eligibility)

| Field | M1 state | Concrete missing evidence |
|---|---|---|
| **SOURCE_REV** | `UNRESOLVED` | No SOURCE_REV file or build-manifest pin mapping the archive to source |
| **Source-build mapping** | `UNRESOLVED` (state: unmapped) | No primary evidence maps `opencode-linux-x64.tar.gz` to tag commit `49c69c5e…` |
| **Exact evaluation invocation** | `UNRESOLVED` | No pinned post-extract executable path + full structured argv for Phase 16 trials |
| **Provider** | `UNRESOLVED` | Official docs require choosing LLM providers; **no campaign provider selected** |
| **Service** | `UNRESOLVED` | No exact service/endpoint identity selected |
| **Model** | `UNRESOLVED` | No exact model identifier selected |
| **Protocol / SDK** | `UNRESOLVED` | Not selected for evaluation |
| **Credential / egress needs** | `UNRESOLVED` | Cannot lock credentials/egress without provider/service selection |
| **Product / changelog** | Release body exists on GitHub Release `v1.18.4`; not used as a separate product pin beyond the tag | — |

### 5.4 M1 disposition

**`blocked at M1`**

**Missing facts:** exact provider, service, and model; exact evaluation
invocation; verified source-build mapping; credential/egress bound to a
selected configuration. A dedicated official `SHA256SUMS` file is also absent
(GitHub asset digests are the only official checksum source observed).

**Consequence:** Candidate B **must not** enter M3b acquisition or execution
while this disposition stands. Unblocking requires establishing every §2 item
from primary evidence before any M3 grant.

**Exclusion rules (locked while blocked):**

- No download, install, or execution of OpenCode artifacts
- No `curl | bash` install-script acquisition for this campaign
- No floating `opencode-ai@latest` / brew / pacman packages as substitutes for
  the pinned release coordinate
- No ambient credentials
- No inferred binary↔source mapping

---

## 6. Candidate C: Grok Build (public source) — **blocked at M1**

### 6.1 M0 identities carried forward

- **Public source:** [xai-org/grok-build](https://github.com/xai-org/grok-build)
- **Public source HEAD (M0):** `refs/heads/main` at
  `3af4d5d39897855bdcc74f23e690024a5dc05573`, observed
  `2026-07-22T14:48:48Z`
- **Release / tag (M0):** none identified
- **SOURCE_REV (M0):** `0f4d7c91b8b2b408333f6de1e8a76cb8eaa71899`, read from
  the public tree at `2026-07-22T14:50:23Z`
- **Product / changelog (M0):** Separate xAI Grok CLI changelog identity; **no
  mapping** to Grok Build public source established

**The accepted Phase 16 candidate identity remains Grok Build public source.**
The xAI Grok CLI / install-script product surface is a **distinct** identity
unless later primary evidence proves a mapping.

### 6.2 M1 re-verification (primary sources only)

| Check | Result at M1 re-verification |
|---|---|
| Git tags | **Empty list** from GitHub tags API (`2026-07-22T15:19:00Z`) |
| GitHub Releases | **Empty list** (`2026-07-22T15:19:00Z`) |
| Moving `main` HEAD | Unchanged from M0: `3af4d5d39897855bdcc74f23e690024a5dc05573` (`2026-07-22T15:20:57Z`) |
| `SOURCE_REV` file on `main` | Present; body `0f4d7c91b8b2b408333f6de1e8a76cb8eaa71899` (HTTP 200) |
| License SPDX | Apache-2.0 |
| `LICENSE` on `main` | Present |
| `NOTICE` on `main` | Absent (404); README points to `THIRD-PARTY-NOTICES` and related notice paths (contents API HTTP 200 for `THIRD-PARTY-NOTICES`) |
| README install section | Documents `curl -fsSL https://x.ai/cli/install.sh \| bash` and source build via Cargo; **does not** publish a GitHub Release asset coordinate for a versioned binary on this repository |

### 6.3 Fields still UNRESOLVED (each blocks eligibility)

| Field | M1 state | Concrete missing evidence |
|---|---|---|
| **Release / tag** | `UNRESOLVED` | No Git tag refs; no GitHub Release identity on the public source repo |
| **Tag commit** | `UNRESOLVED` | No tag exists |
| **Release metadata target** | `UNRESOLVED` | None identified |
| **Distributed artifact** | `UNRESOLVED` | No immutable versioned artifact URL/registry coordinate pinned on the Grok Build public source repository. Install-script URLs are mutable product installers, not a release asset pin, and were **not** fetched for install |
| **Checksum plan** | `UNRESOLVED` | No official checksum for a pinned distributed artifact of this candidate identity |
| **Source-build mapping** | `UNRESOLVED` (state: unmapped) | No primary mapping from any distributed binary to `SOURCE_REV` `0f4d7c91…` or HEAD `3af4d5d3…` |
| **Product / changelog mapping** | `UNRESOLVED` / distinct | README links changelog/docs under x.ai; **no primary mapping** from those product surfaces to this public-source tree is established. Prior separate Grok CLI changelog identity remains non-mapping |
| **Exact evaluation invocation** | `UNRESOLVED` | README documents `grok` after product install and `cargo run -p xai-grok-pager-bin` for source builds; neither is a locked Phase 16 evaluation argv against a pinned artifact |
| **Provider / Service / Model** | `UNRESOLVED` | Not selected; authentication guide referenced but no campaign model pin |
| **Protocol / SDK** | `UNRESOLVED` | Not selected (ACP mentioned as a capability surface, not a campaign protocol pin) |
| **Credential / egress needs** | `UNRESOLVED` | Not establishable without provider/service selection |

### 6.4 M1 disposition

**`blocked at M1`**

**Missing facts:** immutable distributed artifact coordinate; official checksum
source; release/tag identity; source-build mapping to any binary; exact
evaluation invocation; provider, service, and model; credential/egress
requirements; proven mapping (if any) between Grok Build public source and
separate product install/changelog identities.

**Consequence:** Candidate C **must not** enter M3c acquisition or execution
while this disposition stands. Building from source would be a **different**
artifact path requiring its own immutable pin, license/notice review of the
full tree, and still would not select provider/service/model. Product install
scripts must not be treated as the Grok Build public-source artifact pin.

**Exclusion rules (locked while blocked):**

- No install-script acquisition (`x.ai/cli/install.sh` / `.ps1`) for Phase 16
- No inference that Grok CLI product versions equal Grok Build `SOURCE_REV` or
  public HEAD
- No source build, cargo install, or binary execution under this campaign while
  blocked
- No ambient browser auth / user credentials
- ACP mentions do not authorize an ACP evaluation path inside Phase 16

---

## 7. Candidate Identity Rules (M1)

1. **No substitutions.** The M0-accepted set remains Qwen Code, OpenCode, and
   Grok Build public source.
2. **No inferred mappings.** If a field cannot be verified from primary official
   evidence with a timestamp, it is `UNRESOLVED`. For candidates **`blocked at
   M1`**, unresolved fields contribute to that disposition. For Qwen Code
   post-amendment, accepted `UNRESOLVED` invocation fields (A-02/A-03) defer to
   M3a acquisition-and-inspection under separate grant.
3. **Distinct fields preserved.** Public HEAD, release/tag, tag commit, release
   metadata target, SOURCE_REV, distributed artifact, product/changelog,
   provider, service, model, and protocol/SDK remain separate.
4. **Grok Build / product installer separation.** Grok Build public source and
   x.ai CLI install/changelog surfaces remain distinct without proven mapping.
5. **Blocked ≠ hidden.** Blocked candidates remain visible in the ledger and
   docs; they do not silently drop.
6. **Eligibility after M1 amendment (2026-07-23):** Qwen Code is **`eligible
   for later M3 qualification`** on the single-candidate observational path.
   OpenCode and Grok Build remain **`blocked at M1`**. No candidate is qualified
   or authorized for execution.

---

## 8. Campaign-Level Eligibility Outcome (M1 + amendment)

| Question | Answer after M1 amendment (2026-07-23) |
|---|---|
| How many candidates are `eligible for later M3 qualification`? | **One** — Qwen Code (Candidate A). OpenCode and Grok Build remain **`blocked at M1`** |
| Is the single-candidate observational path authorized? | **Yes**, for Qwen Code only (`M1_AMENDMENT_QWEN_OBSERVATIONAL.md`; C-06) |
| Are at least two independently qualified configurations currently possible? | **No.** Qualification has not started |
| Is Phase 16 blocked entirely for comparative evaluation? | **Yes for comparative claims.** One eligible candidate is insufficient for causal comparison |
| When may comparative claims appear? | Only if **two** later M3 qualifications actually succeed under identical environmental rules |

**Summary disposition:** Phase 16 operates on a **single-candidate observational
path** for Qwen Code. Comparative campaign remains blocked. Execution gates
remain closed until separate M3 grants. Future paths:

1. **Qwen Code observational qualification** — M3a acquisition-and-inspection,
   then M3 qualification under separate grants; evidence class **observational**
   only; no comparative or quality claims.
2. **Comparative campaign** — only if a second candidate later becomes eligible
   and both independently qualify at M3.
3. **Fully blocked evaluation for comparison** — if Qwen Code fails M3 or no
   second candidate ever qualifies; Phase 16 may still close with runner/isolation
   evidence and limitations recorded.

---

## 9. Pre-Acquisition Verification Gate (M3a completed)

M1 amendment eligibility was in place before M3a. **M3a (2026-07-23)**
re-verified checksum, tag `LICENSE`, and in-archive license/notice contents;
resolved A-02/A-03 from inspection; and did **not** execute the upstream binary.
Remaining execution gates are DNS binding at launch, credentials, and a separate
qualification grant (`M3A_ACQUISITION_EVIDENCE.md` §9; egress complete per
`M3_EGRESS_PROOF_EVIDENCE.md`; DNS gate defined per
`M3_DNS_BINDING_GATE.md`). OpenCode and Grok Build remain ineligible.
A candidate that cannot complete later M3 gates is recorded as **blocked** for
that slice and cannot support comparative claims.

---

*M1 artifact disposition — human-accepted 2026-07-23; M1 amendment for Qwen
Code observational path human-accepted 2026-07-23. M3a acquisition-and-inspection
completed 2026-07-23 (artifact under phase root only; binary not launched at M3a);
recovery re-acquisition 2026-07-24 after `/tmp` wipe (inspection only). M3
egress proof completed 2026-07-23 (`M3_EGRESS_PROOF_EVIDENCE.md`). M3 DNS
binding gate defined 2026-07-23 (`M3_DNS_BINDING_GATE.md`). Latest M3
qualification smoke `m3q-20260724T060109Z-45dd1c5f` NO-GO: write-capable yolo
under 12 turns / 120s wall verified TC-T01 rename but `qwen_exit=53` (turn
limit); spend balance delta USD 0.00; fixed parent-shell reap real inner exit
4. Qwen Code remains eligible for later M3 qualification but not qualified;
OpenCode and Grok Build blocked at M1. M2b completed 2026-07-23.*
