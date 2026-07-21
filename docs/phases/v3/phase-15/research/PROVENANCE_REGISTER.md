# Phase 15 M1a Provenance Register

## Authorization lock

This register covers research performed on **2026-07-21** for Qwen Code,
OpenCode, and Grok Build. Every entry is **ideas-only/read-only**. M1a adopts no
upstream material and authorizes no copying, adaptation, translation, porting,
vendoring, dependency, binary use, generated output, or executable evaluation.

Consequences for Zaide:

- Local destination: **none** for every upstream item.
- Local modification: **none** for every upstream item.
- Zaide dependency/SBOM/lockfile impact: **none**.
- Security/update owner: **unassigned until a later, separately authorized
  adoption review**.
- Current material decision: **reject for adoption under M1a**. This is an
  authorization decision, not a comparative product ranking.
- Missing provenance is a **hard adoption stop**.

## Required record schema

Every future record must contain:

1. upstream project and repository;
2. exact inspected commit and release/tag, if any;
3. source component/path;
4. applicable license and SPDX identifier;
5. copyright, NOTICE, patent, and trademark obligations;
6. classification as `copied`, `adapted`, `translated`, `generated`, or
   `ideas-only/read-only`;
7. local destination or `none`;
8. local modifications or `none`;
9. transitive dependency, asset, prompt, fixture, and corpus concerns;
10. SBOM and lockfile impact;
11. security/update owner;
12. `adopt`, `isolate`, `rewrite`, or `reject` decision and rationale.

The presence of a root Apache-2.0 or MIT license never clears a dependency,
subtree, asset, prompt, corpus, generated artifact, or bundled binary by itself.

## Register summary

| ID | Upstream material | Exact snapshot | Classification | Local destination | SBOM/lockfile impact | M1a decision |
|----|-------------------|----------------|----------------|-------------------|----------------------|--------------|
| PR-001 | Qwen Code repository and selected architecture files | `3fb1b98a279d4c36ef05366f5e8e24517564548e`; stable release `v0.20.0` recorded separately | Ideas-only/read-only | None | None | Reject adoption: research only |
| PR-002 | Qwen Code Gemini CLI lineage | Qwen snapshot above; declared origin Gemini CLI v0.8.2 | Ideas-only/read-only | None | None | Reject adoption: file-level ancestry unresolved |
| PR-003 | OpenCode repository and selected architecture files | `849c2598abc7d2b40261e74b5826bc74ffc78308`; release `v1.18.4` metadata and tag recorded separately | Ideas-only/read-only | None | None | Reject adoption: research only |
| PR-004 | Grok Build first-party client source | Public `a881e6703f46b01d8c7d4a5437683546df30449d`; `SOURCE_REV` `c5c4ce03436b4bb2cec43d3feaa27dee0109bf37`; no GitHub release/tag | Ideas-only/read-only | None | None | Reject adoption: research only |
| PR-005 | Grok Build generated workspace/dependency metadata | Same public commit | Ideas-only/read-only | None | None | Reject adoption: dependency closure not cleared |
| PR-006 | Grok Build root third-party notice closure | Same public commit | Ideas-only/read-only | None | None | Reject adoption: notice review is not adoption clearance |
| PR-007 | Grok Build vendored `third_party/` Mermaid stack | Same public commit | Ideas-only/read-only | None | None | Reject adoption: vendored/modified third-party source |
| PR-008 | Grok Build Codex-derived tool implementations | Same public commit; original Codex commit not published in notice | Ideas-only/read-only | None | None | Reject adoption: missing exact origin mapping |
| PR-009 | Grok Build OpenCode-derived tool implementations | Same public commit; original OpenCode commit not published in notice | Ideas-only/read-only | None | None | Reject adoption: missing exact origin mapping |
| PR-010 | Grok Build bundled search-tool binaries | Build-dependent, not mapped to a GitHub release or inspected binary | Ideas-only/read-only metadata only | None | None | Reject adoption: binary composition and source mapping unresolved |
| PR-011 | Grok Build announcement, changelog, and distributed binary identities | Announcement `2026-07-15`; latest observed changelog version `0.2.106` dated `2026-07-18`; no source, binary-hash, or service mapping | Ideas-only/read-only | None | None | Reject equivalence assumptions |

## Detailed records

### PR-001 — Qwen Code repository snapshot

- **Upstream project/repository:** [Qwen Code](https://github.com/QwenLM/qwen-code).
- **Exact commit/release:** repository `3fb1b98a279d4c36ef05366f5e8e24517564548e` on `main`; stable release `v0.20.0`, tag ref commit `92fda5603e84ef62a1b29bf6faf4f6a8124a2bf7`, published `2026-07-19T07:27:26Z`, recorded separately from the inspected HEAD.
- **Components/paths:** [`packages/core/src/core/`](https://github.com/QwenLM/qwen-code/tree/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/core), [`packages/core/src/tools/`](https://github.com/QwenLM/qwen-code/tree/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/tools), [`packages/core/src/services/chatCompressionService.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/services/chatCompressionService.ts), [`packages/core/src/utils/memoryDiscovery.ts`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/packages/core/src/utils/memoryDiscovery.ts), tests, root metadata.
- **License/SPDX:** root `Apache-2.0`; pinned [`LICENSE`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/LICENSE).
- **Obligations:** for Apache-licensed material, preserve license and applicable notices, mark modifications, retain relevant copyright/patent/trademark/attribution notices, and respect the patent-termination and trademark clauses. Component ancestry must be checked before relying on the root license.
- **Classification:** ideas-only/read-only.
- **Local destination/modifications:** none / none.
- **Transitive concerns:** npm workspace dependencies and lockfile; Gemini-derived source; provider adapters; prompts; extension packages; fixtures; assets; generated schemas/build outputs. No top-level NOTICE was found in the inspected snapshot.
- **SBOM/lockfile impact:** none in Zaide.
- **Security/update owner:** unassigned.
- **Decision:** reject adoption under M1a. Rationale: research authorization only; component-level provenance and transitive obligations are incomplete.

### PR-002 — Qwen Code Gemini CLI lineage

- **Upstream project/repository:** Qwen Code, with declared origin [Google Gemini CLI](https://github.com/google-gemini/gemini-cli) v0.8.2.
- **Exact commit/release:** declaration inspected at Qwen Code commit `3fb1b98a279d4c36ef05366f5e8e24517564548e`; no exact Gemini CLI source commit or file mapping was established by M1a.
- **Component/path:** pinned Qwen [`README.md`](https://github.com/QwenLM/qwen-code/blob/3fb1b98a279d4c36ef05366f5e8e24517564548e/README.md); potentially inherited source is not enumerated here because the upstream declaration does not provide a complete file map.
- **License/SPDX:** Qwen root `Apache-2.0`; applicable Gemini component licenses/notices require separate verification.
- **Obligations:** preserve all applicable upstream copyright/NOTICE and Apache requirements for any derived file; do not assume Qwen's root license replaces inherited attribution.
- **Classification:** ideas-only/read-only.
- **Local destination/modifications:** none / none.
- **Transitive concerns:** unknown retained Gemini files, prompts, tests, assets, dependencies, and generated artifacts.
- **SBOM/lockfile impact:** none in Zaide.
- **Security/update owner:** unassigned.
- **Decision:** reject adoption. Rationale: exact ancestry, source commit, modifications, and notice coverage are unresolved; missing provenance is a hard stop.

### PR-003 — OpenCode repository snapshot

- **Upstream project/repository:** [OpenCode](https://github.com/anomalyco/opencode).
- **Exact commit/release:** repository `849c2598abc7d2b40261e74b5826bc74ffc78308` on `dev`; release `v1.18.4`, tag ref commit `49c69c5ed3ccf706b61b3febb43c8aaff7f8325e`, release metadata target `4872c48c230728150e8e3406722943450ed58dcb`, published `2026-07-20T15:28:21Z`.
- **Components/paths:** [`packages/opencode/src/session/`](https://github.com/anomalyco/opencode/tree/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/session), [`packages/opencode/src/tool/`](https://github.com/anomalyco/opencode/tree/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/src/tool), [`packages/opencode/test/`](https://github.com/anomalyco/opencode/tree/849c2598abc7d2b40261e74b5826bc74ffc78308/packages/opencode/test), root/workspace metadata.
- **License/SPDX:** root `MIT`; pinned [`LICENSE`](https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/LICENSE).
- **Obligations:** retain the MIT copyright and permission notice in copies or substantial portions; separately review every dependency, subtree, prompt, asset, fixture, corpus, and generated artifact.
- **Classification:** ideas-only/read-only.
- **Local destination/modifications:** none / none.
- **Transitive concerns:** Bun workspace/lock data, provider plugins, prompt templates, recorded tool-loop fixtures, MCP/ACP dependencies, UI assets, and generated schemas.
- **SBOM/lockfile impact:** none in Zaide.
- **Security/update owner:** unassigned.
- **Decision:** reject adoption under M1a. Rationale: research authorization only and no transitive clearance.

### PR-004 — Grok Build first-party public source

- **Upstream project/repository:** [Grok Build](https://github.com/xai-org/grok-build), published by SpaceXAI.
- **Exact commit/release:** public repository `a881e6703f46b01d8c7d4a5437683546df30449d`; root [`SOURCE_REV`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/SOURCE_REV) records monorepo commit `c5c4ce03436b4bb2cec43d3feaa27dee0109bf37`; no GitHub release or tag existed.
- **Components/paths:** first-party shell/session/sampler/pager/workspace/extension/sandbox/ACP source, excluding items separately registered below. Principal paths: [`xai-grok-shell/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-shell), [`xai-grok-pager/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-pager), [`xai-grok-sampler/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-sampler).
- **License/SPDX:** declared first-party `Apache-2.0`; pinned [`LICENSE`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/LICENSE), Copyright 2023-2026 SpaceXAI.
- **Obligations:** Apache-2.0 license, modification notices, applicable attribution/NOTICE retention, patent terms, and no trademark grant beyond descriptive use. First-party classification does not cover ports, vendors, or dependencies.
- **Classification:** ideas-only/read-only.
- **Local destination/modifications:** none / none.
- **Transitive concerns:** large Cargo workspace, generated metadata, registry/git dependencies, ported source, vendored source, themes/assets, prompts, prebuilt tools, build-time inputs, and possibly unpublished/private or service-side components.
- **SBOM/lockfile impact:** none in Zaide.
- **Security/update owner:** unassigned.
- **Decision:** reject adoption under M1a. Rationale: research only; public-source/binary/service equivalence is unknown.

### PR-005 — Grok Build generated workspace and Cargo lock

- **Upstream project/repository:** Grok Build.
- **Exact commit/release:** public repository `a881e6703f46b01d8c7d4a5437683546df30449d`; no GitHub release/tag.
- **Components/paths:** generated root [`Cargo.toml`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/Cargo.toml), generated [`Cargo.lock`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/Cargo.lock), per-crate manifests.
- **License/SPDX:** workspace package default `Apache-2.0`; resolved dependencies carry their own expressions.
- **Obligations:** evaluate every resolved package and feature actually shipped; preserve package-specific notices/source obligations; distinguish generated workspace membership from copyright provenance.
- **Classification:** ideas-only/read-only metadata.
- **Local destination/modifications:** none / none.
- **Transitive concerns:** registry and Git dependencies; platform/feature-specific closures; build-only and test dependencies; generated workspace may differ from private monorepo or release build inputs.
- **SBOM/lockfile impact:** none in Zaide; no Cargo material entered Zaide.
- **Security/update owner:** unassigned.
- **Decision:** reject adoption. Rationale: a pinned lockfile aids reproducibility but is neither an SBOM for Zaide nor proof of binary composition/license clearance.

### PR-006 — Grok Build root third-party notices

- **Upstream project/repository:** Grok Build plus the dependency/asset origins enumerated in its notice file.
- **Exact commit/release:** public repository `a881e6703f46b01d8c7d4a5437683546df30449d`.
- **Component/path:** root [`THIRD-PARTY-NOTICES`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/THIRD-PARTY-NOTICES).
- **License/SPDX:** multiple expressions, including MIT, Apache-2.0, BSD variants, ISC, Zlib, Unicode licenses, BSL-1.0, MPL-2.0, CDLA-Permissive-2.0, and special libgit2 linking terms, as recorded upstream.
- **Obligations:** package-specific copyright/license/NOTICE retention; MPL-covered files remain under MPL; special binary/library and linking notices must be evaluated for the exact build.
- **Classification:** ideas-only/read-only legal metadata.
- **Local destination/modifications:** none / none.
- **Transitive concerns:** notice-generation completeness, feature/platform closure, dropped packages, build-time inputs, and whether a distributed binary used the same closure.
- **SBOM/lockfile impact:** none in Zaide.
- **Security/update owner:** unassigned.
- **Decision:** reject adoption. Rationale: useful primary provenance evidence, but no material or dependency is authorized and build equivalence is unknown.

### PR-007 — Grok Build vendored Mermaid stack

- **Upstream project/repository:** Grok Build vendors `warpdotdev/mermaid-to-svg`, `r3alst/dagre-rust`, `r3alst/graphlib-rust`, and `r3alst/ordered-hashmap`.
- **Exact commit/release:** vendored state in Grok Build public commit `a881e6703f46b01d8c7d4a5437683546df30449d`; upstream commit mappings require the per-crate headers/notices and were not independently completed in M1a.
- **Component/path:** [`third_party/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/third_party), [`third_party/README.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/third_party/README.md), [`third_party/NOTICE`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/third_party/NOTICE).
- **License/SPDX:** MIT for `mermaid-to-svg`; Apache-2.0 for the three Rust graph/layout crates; additional Mermaid/dagre ancestry notices apply.
- **Obligations:** retain adjacent license/LICENCE files and notices; preserve modification notices; inspect ancestry and per-crate vendoring notes before redistribution.
- **Classification:** ideas-only/read-only; explicitly vendored third-party, not first-party Apache-2.0.
- **Local destination/modifications:** none / none.
- **Transitive concerns:** JavaScript/Rust ancestry, locally applied patches, dropped tests/binaries, and untrusted-model-output rendering surface.
- **SBOM/lockfile impact:** none in Zaide.
- **Security/update owner:** unassigned.
- **Decision:** reject adoption. Rationale: vendored third-party source is outside M1a authorization and lacks Zaide review.

### PR-008 — Grok Build OpenAI Codex tool ports

- **Upstream project/repository:** declared origin [`openai/codex`](https://github.com/openai/codex).
- **Exact commit/release:** published Grok Build state `a881e6703f46b01d8c7d4a5437683546df30449d`; **original Codex commit/release not recorded by the published notices**.
- **Original source path:** `codex-rs/core/src/tools/handlers/` and the apply-patch crate.
- **Published component/path:** [`crates/codegen/xai-grok-tools/src/implementations/codex/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/implementations/codex), covering `apply_patch`, `grep_files`, `list_dir`, and `read_file`.
- **License/SPDX:** `Apache-2.0`; Copyright 2025 OpenAI.
- **Modification notice:** root and crate-local notices say the files are ported or derived, translated between languages where applicable, adapted to Grok Build's `Tool` trait/runtime, extended, and modified. Classification is **ported/adapted**, not verified unmodified copy.
- **Obligations:** Apache-2.0 license and applicable copyright/patent/trademark/attribution retention; prominent change notice under section 4(b).
- **Classification:** ideas-only/read-only for Zaide; the upstream Grok material itself is declared ported/adapted.
- **Local destination/modifications:** none / none.
- **Transitive concerns:** missing original commit/file map; apply-patch ancestry; possible further third-party dependencies and tests.
- **SBOM/lockfile impact:** none in Zaide.
- **Security/update owner:** unassigned.
- **Decision:** reject adoption. Rationale: M1a prohibits porting/copying and the exact original provenance mapping is missing.

Primary notice: pinned [`xai-grok-tools/THIRD_PARTY_NOTICES.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/THIRD_PARTY_NOTICES.md).

### PR-009 — Grok Build OpenCode tool ports

- **Upstream project/repository:** declared origin [`sst/opencode`](https://github.com/sst/opencode). The notice's origin is recorded verbatim; this register does not silently substitute the independently inspected `anomalyco/opencode` candidate identity.
- **Exact commit/release:** published Grok Build state `a881e6703f46b01d8c7d4a5437683546df30449d`; **original OpenCode commit/release not recorded by the published notices**.
- **Original source path:** `packages/opencode/src/tool/`.
- **Published component/path:** [`crates/codegen/xai-grok-tools/src/implementations/opencode/`](https://github.com/xai-org/grok-build/tree/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/src/implementations/opencode), covering `bash`, `edit`, `glob`, `grep`, `read`, `skill`, `todowrite`, and `write`.
- **License/SPDX:** `MIT`; Copyright (c) 2025 opencode.
- **Modification notice:** root and crate-local notices say the files are ported or derived, translated where applicable, adapted to Grok Build's interfaces/runtime, extended, and modified. Classification is **ported/adapted**, not verified unmodified copy.
- **Obligations:** retain the MIT copyright and permission notice in copies or substantial portions.
- **Classification:** ideas-only/read-only for Zaide; the upstream Grok material itself is declared ported/adapted.
- **Local destination/modifications:** none / none.
- **Transitive concerns:** missing original commit/file map, possible lineage changes between `sst/opencode` and current OpenCode repositories, related tests/prompts/dependencies.
- **SBOM/lockfile impact:** none in Zaide.
- **Security/update owner:** unassigned.
- **Decision:** reject adoption. Rationale: M1a prohibits porting/copying and exact source provenance is missing.

Primary notice: pinned [`xai-grok-tools/THIRD_PARTY_NOTICES.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/THIRD_PARTY_NOTICES.md).

### PR-010 — Grok Build bundled search binaries

- **Upstream project/repository:** ripgrep, ugrep, bfs, and their relevant binary dependencies as recorded by Grok Build.
- **Exact commit/release:** Grok Build public source commit `a881e6703f46b01d8c7d4a5437683546df30449d`; exact embedded binary versions/checksums are build-pipeline dependent and were not inspected.
- **Component/path:** [`xai-grok-tools/THIRD_PARTY_NOTICES.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/THIRD_PARTY_NOTICES.md), [`xai-grok-tools/build.rs`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/crates/codegen/xai-grok-tools/build.rs).
- **License/SPDX:** ripgrep MIT/Unlicense election recorded as MIT; ugrep BSD-3-Clause; bfs 0BSD; statically linked components may add obligations.
- **Obligations:** exact-build binary notices, linked-component review, source/version/checksum mapping, and redistribution conditions.
- **Classification:** ideas-only/read-only metadata; no binary was downloaded or executed.
- **Local destination/modifications:** none / none.
- **Transitive concerns:** release-pipeline environment variables choose which binaries are embedded; prebuilt artifact provenance and linkage can differ by build.
- **SBOM/lockfile impact:** none in Zaide.
- **Security/update owner:** unassigned.
- **Decision:** reject adoption. Rationale: binary composition, versions, checksums, and source equivalence are unverified.

### PR-011 — Grok Build publication and version identities

- **Upstream project/repository:** SpaceXAI announcement/changelog and `xai-org/grok-build`.
- **Exact commit/release:** public commit `a881e6703f46b01d8c7d4a5437683546df30449d`; monorepo `SOURCE_REV` `c5c4ce03436b4bb2cec43d3feaa27dee0109bf37`; no GitHub release/tag; latest observed changelog version `0.2.106` dated `2026-07-18`; announcement dated `2026-07-15`.
- **Component/path:** [official open-source announcement](https://x.ai/news/grok-build-open-source), [official changelog](https://x.ai/build/changelog), pinned [`README.md`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/README.md), pinned [`SOURCE_REV`](https://github.com/xai-org/grok-build/blob/a881e6703f46b01d8c7d4a5437683546df30449d/SOURCE_REV).
- **License/SPDX:** announcement/changelog content is not treated as source code under the repository license; repository first-party source is Apache-2.0 subject to separate third-party records.
- **Obligations:** do not map any changelog or binary version to public source without primary evidence; verify any binary's provenance, notices, checksum, build inputs, and corresponding source separately.
- **Classification:** ideas-only/read-only metadata.
- **Local destination/modifications:** none / none.
- **Transitive concerns:** private monorepo, build pipeline, separately distributed binaries, server-side components, and post-snapshot changes.
- **SBOM/lockfile impact:** none in Zaide.
- **Security/update owner:** unassigned.
- **Decision:** reject equivalence assumptions. Rationale: primary evidence does not map any changelog or binary version to the public source commit or monorepo `SOURCE_REV`, nor does it establish an equivalent distributed binary hash or hosted service.

## Adoption gate for any later phase

Before any future use, a separately authorized review must:

1. select an exact component rather than a repository-level idea;
2. pin the original upstream commit, source path, and release/binary mapping;
3. reconstruct file-level ancestry for inherited or ported material;
4. audit all applicable licenses, NOTICE files, modifications, patents, and
   trademarks;
5. audit dependencies, assets, prompts, fixtures, corpora, generated files,
   and binary linkage;
6. choose adopt/isolate/rewrite/reject with a named owner;
7. update Zaide's SBOM/lockfiles only in the same authorized change; and
8. stop if any provenance field remains missing.

No such review or adoption occurred in M1a.
