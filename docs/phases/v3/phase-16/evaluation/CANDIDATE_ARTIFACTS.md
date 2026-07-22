# Phase 16 M1: Candidate Artifacts

**Status:** M1 explicitly human-accepted on 2026-07-23. No artifact has been
acquired, installed, or executed. This document records the accepted M0
candidate set, M1 primary-source re-verification, and one explicit M1
disposition per candidate (all three **blocked at M1**). **M2a remains
unauthorized.**

**M1 source re-verification window:** `2026-07-22T15:19:00Z` through
`2026-07-22T15:20:57Z` (UTC). Methods: GitHub REST API release/ref/contents
metadata, official README/LICENSE text at pinned refs, official documentation
HTML, and the official Qwen `SHA256SUMS` text listing. **No candidate package
was downloaded for install, no binary was executed, no repository was cloned,
and no benchmark was run.**

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
concrete missing-evidence reason. **Eligibility is not qualification:** even an
eligible candidate remains non-executable until its M3 slice succeeds under
later authorization. No candidate is eligible at this M1 lock (see §4–§6).

---

## 3. Accepted M0 Candidate Set (unchanged membership)

The three candidates below were explicitly accepted at M0 on 2026-07-22. No
candidate has been substituted, removed, or added. M0 identity observations are
carried forward. M1 re-checks primary sources and records disposition.

| Candidate | M0 public source | M1 disposition |
|---|---|---|
| A — Qwen Code | [QwenLM/qwen-code](https://github.com/QwenLM/qwen-code) | **blocked at M1** |
| B — OpenCode | [anomalyco/opencode](https://github.com/anomalyco/opencode) | **blocked at M1** |
| C — Grok Build (public source) | [xai-org/grok-build](https://github.com/xai-org/grok-build) | **blocked at M1** |

---

## 4. Candidate A: Qwen Code — **blocked at M1**

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

**Immutable artifact coordinate observed for Linux x86_64 evaluation host
(not acquired):**

- URL:
  `https://github.com/QwenLM/qwen-code/releases/download/v0.20.1/qwen-code-linux-x64.tar.gz`
- Official checksum sources (agreeing on this filename):
  - Release asset digest reported by GitHub API:
    `sha256:2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e`
  - Official release file
    `https://github.com/QwenLM/qwen-code/releases/download/v0.20.1/SHA256SUMS`
    line:
    `2ec957bc79afb4722d08af55bfdfce86f2c5c8cb3dcda27f95324206e9c4026e  qwen-code-linux-x64.tar.gz`
- **Checksum plan if later authorized to acquire:** download the archive and
  `SHA256SUMS` under a future M3 grant; verify
  `sha256sum -c` against the pinned line above; do not execute until M3
  qualification completes.

**License / notices review route (docs-only at M1):**

- Primary: `LICENSE` at tag `v0.20.1` (Apache-2.0)
- Repository license metadata: Apache-2.0
- `NOTICE` file: not present at the pinned tag (404)
- Pre-acquisition review must still inspect the acquired archive’s embedded
  license/notice files when an M3 acquisition grant exists. That archive review
  has **not** been performed (no download).

**Documented usage forms (not a locked evaluation invocation):**

- Interactive: `qwen`
- Headless form documented in README: `qwen -p "..."`
- Auth configuration: interactive `/auth` and official Authentication /
  Model Providers docs (multi-provider)

### 4.3 Fields still UNRESOLVED (each blocks eligibility)

| Field | M1 state | Concrete missing evidence |
|---|---|---|
| **SOURCE_REV** | `UNRESOLVED` | No build-manifest SOURCE_REV mapping for the distributed archive was verified |
| **Source-build mapping** | `UNRESOLVED` (state: unmapped) | No primary evidence maps `qwen-code-linux-x64.tar.gz` bytes to tag commit `305b0491…` or to a build recipe hash |
| **Exact evaluation invocation** | `UNRESOLVED` | No pinned post-extract executable path + full structured argv for Phase 16 trials (workspace root, non-interactive flags, output mode, turn limits). README usage forms are not a campaign argv lock |
| **Provider** | `UNRESOLVED` | Official docs describe multiple providers (OpenAI / Anthropic / Gemini / Qwen / third-party / local). **No single campaign provider was selected at M1** |
| **Service** | `UNRESOLVED` | No exact API endpoint/gateway identity selected for the campaign configuration |
| **Model** | `UNRESOLVED` | No exact model identifier selected for the campaign configuration |
| **Protocol / SDK** | `UNRESOLVED` | Multi-protocol product; no single wire/SDK path selected for evaluation |
| **Credential / egress needs** | `UNRESOLVED` | Cannot lock least-privilege credential or provider-only egress allowlist without selected provider/service |
| **Product / changelog** | `UNRESOLVED` as separate product pin | Release notes exist on the GitHub Release; no separate product identity was selected beyond tag `v0.20.1` |

### 4.4 M1 disposition

**`blocked at M1`**

**Missing facts that prevent `eligible for later M3 qualification`:** exact
provider, service, and model identities; exact evaluation invocation argv;
verified source-build mapping; credential/egress requirements bound to a
selected configuration.

**Consequence for later qualification:** Candidate A **must not** enter M3a
acquisition or execution while this disposition stands. Unblocking requires a
docs-only M1 amendment or a later authorized identity-lock update that
establishes every §2 item from primary evidence **before** any M3 grant.
Artifact URL and checksum sources observed above do **not** alone confer
eligibility.

**Exclusion rules (locked while blocked):**

- No download, install, extract-for-run, or execution of Qwen Code artifacts
- No use of install-script pipes (`curl | bash`) for this campaign
- No ambient user API keys or browser OAuth sessions
- No substitution of npm `@latest` / Homebrew floating packages for the pinned
  release coordinate
- Missing identities are recorded, not guessed

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
   evidence with a timestamp, it is `UNRESOLVED` and contributes to **blocked
   at M1**.
3. **Distinct fields preserved.** Public HEAD, release/tag, tag commit, release
   metadata target, SOURCE_REV, distributed artifact, product/changelog,
   provider, service, model, and protocol/SDK remain separate.
4. **Grok Build / product installer separation.** Grok Build public source and
   x.ai CLI install/changelog surfaces remain distinct without proven mapping.
5. **Blocked ≠ hidden.** Blocked candidates remain visible in the ledger and
   docs; they do not silently drop.
6. **No candidate is eligible for later M3 qualification at this M1 lock.**
   No candidate is authorized to be acquired or executed.

---

## 8. Campaign-Level Eligibility Outcome (M1)

| Question | M1 answer |
|---|---|
| How many candidates are `eligible for later M3 qualification`? | **Zero** (A, B, and C are each **blocked at M1**) |
| Are at least two independently qualified configurations currently possible? | **No.** Qualification has not started; eligibility for later M3 is absent for all three |
| Is Phase 16 limited to a future single-candidate observational path? | **Not at this lock.** A single-candidate observational path would require at least one later eligibility unblock + successful M3 qualification. That condition is **not** met now |
| Is Phase 16 blocked entirely for comparative evaluation? | **Yes for comparative claims.** With zero eligible candidates, the campaign cannot plan two qualified configurations |
| When may comparative claims appear? | **Never**, unless **two** later M3 qualifications actually succeed under identical environmental rules. Eligibility alone is insufficient |

**Summary disposition:** Phase 16 M1 locks a **blocked multi-candidate set**:
artifact and checksum *observations* exist for Qwen Code and OpenCode release
archives, but **no candidate meets the full eligibility bar**. Execution gates
remain closed. Future paths:

1. **Comparative campaign** — only if ≥2 candidates later become eligible and
   then independently qualify at M3.
2. **Single-candidate observational path** — only if exactly one candidate later
   becomes eligible and qualifies at M3; still **no comparative claim**.
3. **Fully blocked evaluation** — if zero candidates ever qualify; Phase 16 may
   still close with runner/isolation evidence and a no-candidate recommendation.

---

## 9. Pre-Acquisition Verification Gate (future M3 slices)

Before any candidate artifact is acquired under a future grant, **every** §2
eligibility item must already be recorded as established (eligibility
unblocked), and the M3 slice must still re-verify checksum, license/notice
contents inside the artifact, isolation, egress, credentials, cost ceiling, and
cleanup. A candidate that cannot be resolved at M3 is recorded as **blocked**
for that slice and cannot support comparative claims.

---

*M1 artifact disposition lock — human-accepted 2026-07-23. No artifact has been
acquired. All candidates are blocked at M1. All execution gates remain closed.
M2a remains unauthorized.*
