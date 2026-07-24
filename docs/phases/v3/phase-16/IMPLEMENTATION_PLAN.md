# Phase 16: Controlled Native Harness Evaluation

**Status:** M0 plan was explicitly human-accepted on 2026-07-22. **M1 was explicitly human-accepted on 2026-07-23** with an all-blocked candidate eligibility lock; **M1 amendment (human-accepted 2026-07-23)** unblocked Qwen Code for the single-candidate observational path only (`evaluation/M1_AMENDMENT_QWEN_OBSERVATIONAL.md`). OpenCode and Grok Build remain **blocked at M1**. **M2a was explicitly human-accepted on 2026-07-23** for the standalone offline runner contract and deterministic repository-owned fake-candidate core. **M2b was completed on 2026-07-23** for repository-owned Bubblewrap isolation, lifecycle, mutation, and cleanup proof (`evaluation/ISOLATION_EVIDENCE.md`). M2a/M2b added no production behavior, DI, public production types, upstream artifact acquisition, network access, or real candidate execution. The M3 unblock amendment proposal was accepted as the amendment vehicle on 2026-07-23. **M3a acquisition-and-inspection completed on 2026-07-23** under an explicit separate grant (`evaluation/M3A_ACQUISITION_EVIDENCE.md`): pinned Qwen Code v0.20.1 Linux x64 archive acquired under the phase artifact root only, SHA-256 verified, licenses scanned, A-02/A-03 resolved from inspection; **upstream binary not launched**. **M3 egress proof completed on 2026-07-23** under an explicit separate grant (`evaluation/M3_EGRESS_PROOF_EVIDENCE.md`): provider-restricted egress for `api.deepseek.com:443` only proven with repository-controlled probe (allow PASS, non-allowlisted block PASS); no credentials, no authenticated API, no upstream binary launch. **M3 DNS binding gate defined and published on 2026-07-23** (docs-only;
`evaluation/M3_DNS_BINDING_GATE.md`): deterministic sandbox-only resolution for
`api.deepseek.com`, hosts injection, nft parity, TLS/SNI preservation, and
pre-launch stop conditions. **M3 auth-configuration remediation completed
2026-07-24** (`evaluation/M3_AUTH_CONFIG_REMEDIATION_EVIDENCE.md`): locked
`--auth-type openai`, `--openai-base-url https://api.deepseek.com`, and
workspace `modelProviders` `DEEPSEEK_API_KEY` wiring. **M3 write-capable
qualification remediation (2026-07-24)**
(`evaluation/M3_WRITE_CAPABLE_REMEDIATION_EVIDENCE.md`) locked
`--approval-mode yolo` and post-exit reap/finalization. **M3 wall-time + exit-reap remediation (2026-07-24)**
(`evaluation/M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md`) raised the active
smoke wall lock to **120s** and fixed same-shell wait/reap so inner exit is not
bash **127**. **M3a recovery re-acquisition (2026-07-24)** under a separate
acquisition-and-inspection-only grant recreated `/tmp/phase16-artifacts/phase-16/`,
re-downloaded the pinned archive and `SHA256SUMS`, re-verified SHA-256,
re-scanned licenses, and re-extracted for static inspection only
(`evaluation/M3A_ACQUISITION_EVIDENCE.md` §1.1). **M3 qualification smoke**
(`evaluation/M3_QUALIFICATION_EVIDENCE.md`): prior attempts **NO-GO**
(credential; slirp host-PID; resolv bind; auth-type; max-turns under 5-turn
ceiling; plan-only exit 0 without rename; yolo rename verified with exit 55 at
historical 60s wall). **Latest authorized retry session
`m3q-20260724T060109Z-45dd1c5f`:** DNS/slirp/egress/tmpfs/auth argv **GO**;
write-capable **yolo**; locked **12** turns / **120s** wall; Qwen launched
**once**; TC-T01 rename **verified** (`FetchData` 0 / `RetrieveData` 11; host
build/test 0) but **`qwen_exit=53`** (`FatalTurnLimitedError` 12-turn ceiling)
→ dual GO **NO-GO**; spend balance delta **USD 0.00**; fixed parent-shell reap
recorded real inner exit **4** (not bash 127); post-exit finalization completed
without hang. Candidate remains **not qualified**. **Active** locked smoke ceilings
: max **24** session turns, **120s** wall-time, **USD 1** smoke /
**USD 3** cumulative (24-turn ceiling authorized for future retry; historical
12-turn session records preserved). **M3 fresh-session eligibility remediation
(2026-07-24)** ensures each grant evaluates a new session ID, defers credential
consumption until after egress preflight, and forbids substituting historical
`qwen_exit` for the current session (`evaluation/M3_FRESH_SESSION_ELIGIBILITY_REMEDIATION_EVIDENCE.md`).
Comparative campaign rules remain unchanged.

**Selected outcome:** establish controlled, reproducible Native Harness
evaluation infrastructure and run a provenance-cleared campaign that can inform
a later first-party Native Harness design.

**Boundary:** Phase 16 is evaluation work outside Zaide production. It does not
implement a Native Harness backend, an ACP backend, agent tools, permissions, or
workspace mutation in the application. Candidate inspection and execution are
ideas-only evidence, not adoption.

---

## 1. M0 Decision

### 1.1 Exact goal

Phase 16 will create a repository-owned, deterministic evaluation runner and a
locked task campaign for Native Harness candidates. The campaign must produce:

1. provenance and artifact-identity records;
2. isolation, process-lifecycle, credential, and cleanup evidence;
3. raw per-trial observations from predeclared tasks;
4. an explicit separation between causal, observational, and invalid results;
   and
5. a closeout recommendation for a later, independently planned Native Harness
   backend.

The phase succeeds by producing trustworthy decision evidence. It does not need
to select a winner and cannot authorize adoption or production integration.

### 1.2 Why this is next

Phase 15 closed the backend-neutral session/run/event foundation. Live code now
supports backend identity, immutable capability snapshots, one active run per
conversation, ordered lifecycle/message/failure events, cancellation, and a
single projection into Conversations. It deliberately does not support
production tool execution.

The V3 roadmap requires controlled Native Harness evaluation before architecture
lock. A campaign can proceed without product changes if its runner, candidate
processes, credentials, and disposable workspaces remain phase-owned and
external to `src/`. Its evidence is also needed before product action or
mutation-policy shapes can be designed responsibly.

### 1.3 Candidate comparison

| Candidate outcome | Dependency and evidence fit | Safety, licensing, and testability | Rollback | Decision |
|---|---|---|---|---|
| Controlled Native Harness evaluation infrastructure and campaign | Directly follows Phase 15 and the roadmap's evaluation-before-design order. Produces missing evidence without changing product contracts. | Synthetic tasks, pinned artifacts, fake-candidate tests, isolation gates, and ideas-only provenance are possible. Real execution remains separately gated. | Remove phase docs/tool/test support and external artifacts; production remains unchanged. | **Selected.** |
| Minimal first-party Native Harness backend | The request/event surface carries text, lifecycle, failure, and capability state only. No concrete tool/action, trust, permission, audit, mutation, or agent-process owner exists. | A text wrapper would not be a harness; inventing every missing control-plane contract now would be broad and weakly evidenced. | Product event/schema/DI rollback would be materially wider. | Rejected for Phase 16; requires later independent M0. |
| Independent ACP backend | Phase 15 permits ACP as a sibling backend, but transport, SDK, process, authentication, and capability mappings are undecided. ACP does not produce the Native Harness evidence required first. | Stable ACP wire protocol v1 exists, but the verified official library list has no .NET/C# SDK. It needs its own threat model and conformance tests. | Bounded if independently planned, but not the next dependency. | Deferred to its own later M0. |
| Production trust / permission / tool / workspace-mutation control plane | Live code proves controls are prerequisites before production agent mutation. It does not prove the right action taxonomy or policy surface. | Designing before observing candidate action shapes risks speculative abstractions. Evaluation-runner controls are not reusable product authorization. | Wide cross-feature impact. | Not selected; later Native Harness M0 must define proven needs. |

### 1.4 Locked dependency order

```text
Phase 15 closed backend-neutral foundation
    -> Phase 16 controlled Native Harness evaluation (this plan)
        -> later independent Native Harness production M0
            -> proven trust / permission / tool / mutation contracts

Phase 15 closed backend-neutral foundation
    -> later independent ACP backend M0
```

Native Harness and ACP remain independent sibling backends. Neither is a
compatibility layer for the other, and neither silently becomes the other's
prerequisite.

---

## 2. Authorization Boundary

M0 acceptance authorizes this milestone sequence and its decision gates.
After a milestone's required verification is GO, agents advance automatically
to the next eligible milestone and retain one reviewable commit per milestone
or slice. Automatic progression does not waive technical eligibility, security,
provenance, evidence, or external-side-effect gates; it stops for a failed or
incomplete gate, a material scope conflict, or a user decision.

### In scope after M0 acceptance

- lock the campaign, artifact policy, synthetic task corpus, and threat model;
- build a standalone evaluation runner under `tools/`;
- test it with repository-owned fake candidates before upstream execution;
- qualify candidates one at a time;
- run predeclared pilot, tuning, and held-out slices after their gates pass;
- preserve raw evidence and publish redacted reproducible summaries; and
- close with a bounded recommendation for later planning.

### Non-goals

- no changes to `src/` or Zaide production behavior;
- no Native Harness or ACP backend implementation;
- no changes to Phase 15 contracts, event schema, projection, or DI;
- no product tools, approvals, permissions, trust store, audit store, workspace
  mutation, raw trace, usage/cost, persistence, resume, or reconnect;
- no UI, Conversations, Townhall, Settings, or secret-store feature work;
- no copying, translating, vendoring, or adopting upstream source;
- no benchmark or product-quality claim from exploratory or invalid trials;
- no winner declaration based solely on aggregate scores;
- no mutation of the real Zaide checkout, user home, or unrelated repository;
- no commit, push, package publication, or external message during M0.

---

## 3. M0 Checkout and Phase 15 Audit

Audit date: **2026-07-22**.

- Branch: `master`.
- Working tree before M0 edits: clean.
- `HEAD`: `3cbe85086a88edc0727d6eaf350d5379f025e4c0`.
- `origin/master`: `3cbe85086a88edc0727d6eaf350d5379f025e4c0`
  after `git fetch --prune origin`.
- Baseline commit: `docs: finalize Phase 15 closeout and authorization history`
  (`2026-07-22T17:33:14+09:00`).
- Phase 15 M0 through M4 are recorded complete and closed. Its exit checklist is
  closed, no Phase 15 `TOFIX.md` exists, and the folder contains only the plan
  and three accepted research records.

This proposal is bound to that checkout. Later sessions stop and re-audit if the
baseline changes materially.

---

## 4. Verified Live Seams and Ownership

### 4.1 Agent contracts

- `IAgentBackend` owns backend identity/version/capability discovery and exposes
  one `ExecuteAsync(AgentBackendRequest, CancellationToken)` event stream.
- `AgentBackendRequest` carries session, run, conversation, actors, message ID,
  and text. It has no tool, workspace, trust, permission, environment, or
  mutation payload.
- `AgentBackendEvent` has only `MessageCompleted` and `FailureObserved`.
- `IAgentSessionService` / `AgentSessionService` own in-memory session and run
  lifecycle, ordered events, cancellation, end, and snapshots. One active run is
  allowed per conversation; sessions are not persisted.
- `AgentEvent` schema version 1 covers session/run lifecycle, user and assistant
  messages, failures, and capability snapshots. It has no tool call, approval,
  mutation, or audit action.
- `AgentCapabilityId` reserves Tools, Permissions, Resume, Reconnect, Usage, and
  RawTrace. Reserved capability rows are not implemented behavior or authority.
- `LegacyOpenAiCompatibleAgentBackend` is the sole production backend. It is a
  non-streaming HTTP compatibility adapter and reports tool/permission-related
  capabilities unavailable.
- `IAgentExecutionCoordinator` owns conversation-keyed busy coordination;
  `IAgentExecutionService` preserves the legacy one-message compatibility seam.

### 4.2 Projection, Conversations, and Townhall

- `AgentConversationEventProjection` is the sole Agent-event-to-Conversation
  projection owner. It projects user messages, assistant messages, and terminal
  failures with exact idempotency; it has no tool-entry projection.
- Conversations remain backend-neutral with six typed entry kinds.
  `IConversationStore` remains authoritative; schema-v1 persistence is unchanged.
- Townhall sends direct messages through `IAgentRouter` and the coordinator. It
  owns neither execution, tool policy, nor a second event projection.

### 4.3 Composition, configuration, and shutdown

- Production has 77 `AddSingleton` registrations across 12 modules: Agents 10,
  Conversations 3, Townhall 5, and Settings 3. There are no scoped or transient
  production registrations.
- Settings schema version 3 has one global LLM configuration and a generic
  secret store. No backend registry, trust/tool policy, workspace permission, or
  evaluation configuration exists.
- `ApplicationShutdown` has no Agent process owner because the current backend
  is HTTP-based. An evaluation tool must own and clean every child it starts.

### 4.4 Mutation and process seams

- `IFileTreeService` creates and permanently deletes files/directories,
  including recursively, without an agent path, approval, provenance, or audit
  boundary.
- `IFileService` performs whole-file reads/writes without agent cancellation,
  concurrency, dirty-buffer reconciliation, or permission handling.
- `Workspace` owns open `Document` instances and in-memory dirty state. Direct
  external mutation can diverge from editor state.
- `IManagedProcessRunner` owns one redirected ProjectSystem child at a time and
  kills its process tree on cancellation/disposal. Its string argument contract,
  environment handling, and feature ownership are not a general agent-tool seam.
- `IProjectOperationGate` is a ProjectSystem build/run/test gate, not a general
  agent mutation or approval gate.

**Decision:** Phase 16 will not generalize or reuse these product seams. Its
standalone runner owns a narrower non-production boundary. Production reuse
requires a separately reviewed decision.

### 4.5 Architecture ratchets

The live baseline is 502 tracked production C# types: 337 public and 165
internal, across 450 tracked production C# files (`App` 41, `UI` 4,
`Features` 405). Architecture tests enforce source inventory, visibility,
feature dependencies, composition boundaries, and root-folder admission. A
`tools/` project cannot authorize a production-root or public-surface increase.

---

## 5. Upstream Verification and Provenance Lock

Verification timestamp: **2026-07-22T14:48:47Z through
2026-07-22T14:48:48Z** for the moving Git branch refs recorded below. M0 used
primary official Git refs, repository metadata, and official documentation. No
repository was cloned, no package or binary was installed, and no upstream
process was executed.

Inspection is **ideas-only**. Public HEAD, release/tag, tag commit,
`SOURCE_REV`, distributed artifact, changelog/product, service, model, and
protocol/SDK are separate identities. Unknown mappings remain unknown. Moving
branch HEAD observations do not replace or retarget pinned release/tag records.

### 5.1 Native Harness candidates

| Candidate public source | Moving public HEAD observation | Pinned release / tag identity | Release metadata target identity | `SOURCE_REV` identity | Distributed artifact identity | Changelog / product identity | Service identity | Model identity | Protocol / SDK identity | License observation |
|---|---|---|---|---|---|---|---|---|---|---|
| [Qwen Code public source](https://github.com/QwenLM/qwen-code) | `refs/heads/main` at `d064bd7dcf98e0255283068a775f6e49d70db8aa`, observed through the primary Git ref at `2026-07-22T14:48:47Z` | Release `v0.20.1`, published 2026-07-21; tag commit `305b049100606fa093a14b5cd849bff3be16e31a` | Target string `release/v0.20.1`; separately observed target-branch commit `1d003fd83f028939c7a235fdc34d8957609beb3b`; neither is treated as the tag commit | No mapping verified | Not selected or mapped | Qwen Code product identity is not separately selected or mapped | Not selected or mapped | Not selected | Not selected | Official repository declares Apache-2.0; artifact notices require M1 review |
| [OpenCode public source](https://github.com/anomalyco/opencode) | `refs/heads/dev` at `0a601cf334b9a83cc2854108a2b860f25e6e7e8e`, observed through the primary Git ref at `2026-07-22T14:48:47Z` | Release `v1.18.4`, published 2026-07-20; tag commit `49c69c5ed3ccf706b61b3febb43c8aaff7f8325e` | Distinct target revision `4872c48c230728150e8e3406722943450ed58dcb`; not treated as the tag commit | No mapping verified | Not selected or mapped | OpenCode product identity is not separately selected or mapped | Not selected or mapped | Not selected | Not selected | Official repository declares MIT; artifact notices require M1 review |
| [Grok Build public source](https://github.com/xai-org/grok-build) | `refs/heads/main` at `3af4d5d39897855bdcc74f23e690024a5dc05573`, observed through the primary Git ref at `2026-07-22T14:48:48Z` | No Git tag refs were returned by the primary repository at `2026-07-22T14:50:23Z`; no GitHub release identity was identified during M0 | None identified | Public-source `SOURCE_REV` is the distinct revision `0f4d7c91b8b2b408333f6de1e8a76cb8eaa71899`, read from the pinned public HEAD at `2026-07-22T14:50:23Z` | Not selected or mapped | The separate xAI [Grok CLI product changelog](https://docs.x.ai/docs/grok-cli/changelog) lists `0.2.106` on 2026-07-18; no primary mapping to the Grok Build public HEAD, `SOURCE_REV`, or a distributed artifact is established | Not selected or mapped | Not selected | Not selected | Official public-source repository declares Apache-2.0; artifact notices require M1 review |

The accepted Phase 16 candidate identity is **Grok Build public source**. The
xAI Grok CLI changelog describes a separate product identity; it is not a
release, tag, distributed-artifact mapping, service mapping, or model mapping for
the Grok Build public source unless later primary evidence proves that relation.

No row qualifies an executable. **M1 disposition (re-verified
`2026-07-22T15:19:00Z`–`2026-07-22T15:20:57Z`, no acquisition/execution):**
OpenCode and Grok Build public source remain **`blocked at M1`**. **M1 amendment
(2026-07-23):** Qwen Code is **`eligible for later M3 qualification`** on the
single-candidate observational path with DeepSeek provider configuration; A-02/A-03
invocation fields **resolved at M3a (2026-07-23)** from inspection without
execution (`evaluation/M3A_ACQUISITION_EVIDENCE.md`).
Full field tables and exclusion rules live in
`docs/phases/v3/phase-16/evaluation/CANDIDATE_ARTIFACTS.md` and
`evaluation/M1_AMENDMENT_QWEN_OBSERVATIONAL.md`. **One** candidate is eligible;
comparative claims remain impossible unless two later M3 qualifications actually
succeed. M2a and M2b were completed on 2026-07-23. **M3a and M3 egress proof were
completed on 2026-07-23.** **M3a recovery re-acquisition completed 2026-07-24**
after `/tmp` wipe (inspection only; no retry at recovery time). **Latest M3
qualification smoke** (`m3q-20260724T060109Z-45dd1c5f`) **NO-GO**: write-capable
yolo path under locked **12** turns / **120s** wall verified TC-T01 rename but
`qwen_exit=53` (turn limit); spend balance delta USD 0.00; fixed parent-shell
reap recorded real inner exit 4; candidate still **not qualified**.

### 5.2 ACP is independently verified and deferred

- Official repository: [agentclientprotocol/agent-client-protocol](https://github.com/agentclientprotocol/agent-client-protocol),
  `refs/heads/main` at `fc89063698a6313b778fb818d3c6f4e6ef98f7ed`,
  observed through the primary Git ref at `2026-07-22T14:48:48Z`.
- Pinned M0 release/tag: `schema-v1.20.0`, published 2026-07-21; annotated tag
  object `4908af9a14b9c54ee5eab2c9fbb0f880720b8754`, resolving to commit
  `5e89c71497fe07dd4ae633c181a17224f4a8956d`.
- Stable wire protocol version is 1. This is distinct from schema artifact
  `1.20.0`, schema crate `1.6.0`, and opt-in v2 artifact `2.0.0-alpha.2`.
- The official library list names Kotlin, Java, Python, Rust, and TypeScript; it
  does not list an official .NET/C# SDK at M0.
- Transport, SDK/library, service, authentication, capability mapping, and
  distributed binary, product/changelog, and model identities are **not
  selected**. The moving `main` observation does not replace the pinned release
  tag or its resolved commit.
- The repository declares Apache-2.0; adoption review remains pending.

Any ACP implementation requires its own M0 against then-current official
revisions. Phase 16 cannot become an ACP experiment.

---

## 6. Safety and Evidence Contract

### 6.1 Local substrate observed at M0

- Linux x86_64 kernel `7.1.3`, .NET SDK `10.0.109`, and Git `2.55.0`.
- Bubblewrap `0.11.2` is installed. A harmless `--unshare-all`, read-only-host,
  temporary-filesystem `/bin/true` proof succeeded.
- Docker CLI `29.6.1` exists, but daemon access is unavailable to the current
  user. Podman is absent.
- At M0, `slirp4netns`, `pasta`, and `socat` were absent. **M3 egress proof
  (2026-07-23)** later proved provider-restricted egress for
  `api.deepseek.com:443` with `slirp4netns` and `socat` already present (no
  package install). **`M3_DNS_BINDING_GATE.md` (2026-07-23)** locks how future
  execution must bind a freshly verified IPv4 via sandbox-only `/etc/hosts` and
  matching nft allowlist without ambient candidate DNS.

The harmless Bubblewrap proof is not candidate execution and does not qualify a
campaign. No candidate runs until isolation, egress, DNS binding at launch, and
remaining execution gates pass.

### 6.2 Trust and permission boundary

- Default deny: no executable, artifact, provider/model, network destination,
  environment variable, or writable path is allowed unless accepted in M1.
- Artifact acquisition and candidate execution are separate grants.
- Candidate code is untrusted even when repository and license are known.
- No user-wide config, browser session, SSH agent, Git/cloud credentials,
  package cache, or ambient API key enters the sandbox.
- Later-approved credentials must be phase-specific, least-privilege,
  allowlisted, redacted, and independently revocable.
- Prompts and fixtures contain synthetic data only.

### 6.3 Workspace-mutation boundary

- Trials use generated disposable repositories under an explicit phase artifact
  root, never Zaide or an arbitrary user path.
- The host is read-only except for exact trial workspace and artifact paths.
- Each trial starts from a content-addressed fixture and records diff, file
  inventory, Git status, reset, and cleanup state.
- Fake-candidate tests cover symlink/path traversal, mount escape, absolute path,
  and output/file-count limits before real execution.
- Commit, push, publication, external messaging, and destructive host operations
  are forbidden.

### 6.4 Process ownership, cancellation, and cleanup

- The runner solely owns each candidate process tree.
- Executables and arguments use structured argument lists; no shell
  interpolation.
- Working directory, environment allowlist, streams, wall/idle timeouts, output
  cap, and exit expectations are explicit.
- Cancellation terminates the process tree, captures a bounded terminal record,
  and verifies no child or writable mount remains.
- Cleanup failure invalidates the trial and blocks later real-candidate trials
  pending review.
- Runtime state is cleaned, but evidence is retained in the declared artifact
  root until human disposition. It is not automatically deleted.

### 6.5 Provenance and classification

- Each record binds runner commit/config hash, fixture hash, public source HEAD,
  release/tag identity, release metadata target, `SOURCE_REV`, candidate
  artifact hash, changelog/product identity, provider identity, service identity,
  model identity, protocol/SDK identity, environment fingerprint, timestamps,
  exit state, and output artifact hashes as separate fields.
- Raw records are append-only; summaries link raw record IDs.
- `Causal` requires controlled differences and every declared validity gate.
  `Observational` marks useful uncontrolled evidence. `Invalid` marks identity,
  isolation, task, telemetry, timeout, cleanup, or protocol failure.
- Excluded candidates and invalid trials remain visible in the ledger.
- No upstream material enters `src/` or shipping assets. Later adoption requires
  separate source, license, security, and maintenance review.

### 6.6 Rollback

- M1 is docs-only and rolls back by reverting its commit.
- M2 is confined to `tools/`, test support, project wiring, and phase docs. It
  cannot add production DI or public types.
- Candidate artifacts/raw data live outside the worktree; location, retention,
  and reviewed disposal are documented.
- M3/M4 failures disable the manifest entry and preserve evidence instead of
  hiding or rewriting it.
- M5 cannot authorize adoption. A later production M0 may reject every candidate.

---

## 7. Locked Post-M0 Milestones

All paths are planned, not created or authorized by M0 unless already present.
Tests are written before the implementation they specify.

### M1 — Campaign, artifact, task, and threat lock

**Goal:** turn the candidate set into a reviewable campaign without install or
execution.

**Planned paths:**

- `docs/phases/v3/phase-16/evaluation/CAMPAIGN_LOCK.md`
- `docs/phases/v3/phase-16/evaluation/CANDIDATE_ARTIFACTS.md`
- `docs/phases/v3/phase-16/evaluation/TASK_CORPUS.md`
- `docs/phases/v3/phase-16/evaluation/THREAT_MODEL.md`
- this plan and minimum status surfaces

**Acceptance:** at least two independently qualified configurations are required
for comparative claims. Otherwise record a blocked or single-candidate
observational path. Each candidate must have immutable identity, checksum plan,
licensing review, exact provider identity, exact service identity, exact model
identity, credentials/egress, invocation, and exclusion rules. Tasks, metrics,
repetitions, invalidation, and held-out split are locked before candidate results
are observed.

**Verification:**

```bash
git diff --check
rg -n "Public HEAD|release|tag commit|SOURCE_REV|artifact|service|protocol|license|ideas-only" docs/phases/v3/phase-16
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Architecture'
```

**Commit:** one docs-only M1 commit; stop for campaign review.

### M2a — Runner contract and fake-candidate core

**Status:** explicitly human-accepted on 2026-07-23.

**Goal:** implement the standalone manifest, immutable record, metric validation,
and deterministic fake-candidate harness without upstream artifacts or network.

**Planned paths:**

- `tools/Phase16NativeHarnessEvaluation/Phase16NativeHarnessEvaluation.csproj`
- `tools/Phase16NativeHarnessEvaluation/Program.cs`
- one class per additional file under that tool directory
- `tests/Zaide.Tests/Phase16Evaluation/`
- `tests/Zaide.Tests/Zaide.Tests.csproj` and `Zaide.slnx` only if required for
  project/test wiring
- `docs/phases/v3/phase-16/evaluation/RUNNER_CONTRACT.md`

**Tests first:** manifest rejection, hash binding, append behavior,
deterministic metrics, missing-field invalidation, bounded capture, and an
offline fake run.

**Verification:**

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Architecture'
git diff --check
```

**Commit:** one M2a commit; stop for runner-contract review.

### M2b — Isolation, lifecycle, mutation, and cleanup proof

**Goal:** prove the boundary with adversarial repository-owned fake candidates
before an upstream executable runs.

**Planned paths:** M2a paths plus
`docs/phases/v3/phase-16/evaluation/ISOLATION_EVIDENCE.md`.

**Tests first:** exact argv, environment deny/allowlist, read-only host, exact
writable roots, traversal/symlink escape, timeout, cancellation, descendant
termination, output/file caps, hashes, redaction, dirty/reset evidence, and
cleanup-failure blocking.

**Verification:**

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents|FullyQualifiedName~Zaide.Tests.Architecture'
dotnet test Zaide.slnx --no-build
git diff --check
```

Run the full suite interactively with the default eight-worker configuration.
Use `--settings tests/Zaide.Tests/slow.runsettings` only after a failure or hang.

**Commit:** one M2b commit. On GO, advance automatically to the next eligible
milestone; stop if its eligibility or an external-side-effect gate blocks it.

### M3a / M3b / M3c — Candidate qualification and one-task smoke

**Goal:** in separate slices, acquire, hash, license-check, isolate, and perform
one predeclared smoke task for candidates that become eligible. Qwen Code alone is
eligible for later M3 qualification on the accepted single-candidate observational
path. **M3a acquisition-and-inspection completed 2026-07-23**
(`evaluation/M3A_ACQUISITION_EVIDENCE.md`); **recovery re-acquisition completed
2026-07-24** after `/tmp` wipe (inspection only; no retry). **M3 egress proof
completed 2026-07-23** (`evaluation/M3_EGRESS_PROOF_EVIDENCE.md`). **M3 DNS
binding gate defined 2026-07-23** (`evaluation/M3_DNS_BINDING_GATE.md`). Smoke
execution remains blocked pending a **new** qualification grant, credentials,
and DNS-binding execution at launch. Locked smoke ceilings (policy aligned
2026-07-24; wall raised same day after exit-55 evidence; turns raised to **24**
on 2026-07-24 after exit-53 evidence): max **24** session
turns, **120s** wall-time, **USD 1** smoke / **USD 3** cumulative — policy-only
until a separate execution grant. OpenCode and Grok Build remain blocked at M1.
A Grok Build distributed artifact must be independently identified and mapped
before its slice can execute.

**Planned paths:** Phase 16 tool/test/docs plus external artifacts under the
accepted artifact root. No upstream source enters the repository.

**Tests first:** manifest parsing, binary/package/hash mismatch, service identity
mismatch, model identity mismatch, missing credential, forbidden egress,
candidate output parsing, invalid-run retention, and orphan cleanup.

**Per-slice verification:**

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents|FullyQualifiedName~Zaide.Tests.Architecture'
git diff --check
```

Each slice remains ineligible until artifact, license, isolation, egress, DNS
binding at launch, credential, provider identity, service identity, model
identity, cost ceiling, and cleanup requirements pass. A failure excludes or
makes the candidate observational; it never relaxes the gate.

**Commit:** one commit each for M3a, M3b, and M3c; stop after each.

### M4a — Instrumented pilot

**Goal:** run one locked non-held-out task per qualified configuration to prove
telemetry and find campaign-breaking defects. Pilot results cannot support a
winner or held-out claim.

**Tests first:** repetition ledger, task/config binding, validity state,
cost/time ceiling, artifact completeness, and pilot/held-out separation.

**Verification:**

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents|FullyQualifiedName~Zaide.Tests.Architecture'
git diff --check
```

Also reconcile every pilot summary row manually to its raw record.

**Commit:** one M4a evidence commit; stop and amend the plan if protocol changes.

### M4b — Tuning campaign

**Goal:** run locked tuning tasks with at least three valid repetitions per
task/configuration under M1 ceilings and invalidation rules.

**Tests first:** resume-without-overwrite, duplicate rejection, balanced
repetition counts, missing-run visibility, raw aggregation, and result class.

**Verification:**

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents|FullyQualifiedName~Zaide.Tests.Architecture'
git diff --check
```

Independently recompute every published aggregate from the raw ledger.

**Commit:** one M4b evidence commit; stop before held-out work.

### M4c — Held-out campaign

**Goal:** execute locked held-out tasks after tuning configuration is frozen. No
post-result configuration change is permitted.

**Tests first:** configuration freeze hash, held-out access gate, single campaign
start, completeness, invalidation, and raw/summary reconciliation.

**Verification:**

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents|FullyQualifiedName~Zaide.Tests.Features.Conversations|FullyQualifiedName~Zaide.Tests.Features.Townhall|FullyQualifiedName~Zaide.Tests.App.Composition|FullyQualifiedName~Zaide.Tests.Architecture'
dotnet test Zaide.slnx --no-build
git diff --check
```

The full suite follows the interactive parallel/serial-fallback rule from M2b.

**Commit:** one M4c evidence commit; stop for results review.

### M5 — Analysis, limitations, and closeout

**Goal:** publish raw-first comparison, security/provenance disposition,
limitations, and a bounded recommendation for a later planning gate.

**Planned paths:**

- `docs/phases/v3/phase-16/EVALUATION_REPORT.md`
- `docs/phases/v3/phase-16/CLOSEOUT_EVIDENCE.md`
- this plan and minimum project status surfaces

**Tests first:** no production behavior. Add report-integrity tests only if
needed to prove record coverage and hash linkage.

**Verification:**

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Phase16Evaluation'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Conversations'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Townhall'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.App.Composition'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Architecture'
dotnet test Zaide.slnx --no-build
git diff --check
```

Record exact source/type/DI counts and independently reconcile raw records to
the report. The full suite follows the interactive parallel/serial-fallback rule.

**Commit:** one M5 closeout commit. Human acceptance remains required before
calling Phase 16 closed. The report may recommend no candidate.

---

## 8. Cross-Milestone Verification Baseline

At every code milestone, run the smallest focused tests first. At M2b, M4c, and
M5, run:

```bash
dotnet build Zaide.slnx
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Conversations'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Features.Townhall'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.App.Composition'
dotnet test Zaide.slnx --no-build --filter 'FullyQualifiedName~Zaide.Tests.Architecture'
dotnet test Zaide.slnx --no-build
git diff --check
```

Run the full suite in an interactive terminal. If it fails or hangs, reproduce
with:

```bash
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
```

Record passed/failed/skipped totals, build warnings/errors, tracked production
C# file counts, public/internal/total types, module count, and
`AddSingleton`/`AddScoped`/`AddTransient` counts. Any production baseline change
is a scope violation unless the plan is amended and reaccepted.

---

## 9. Decisions and Unresolved Questions

### Locked decisions

1. Phase 16 is evaluation infrastructure and campaign, not a product backend.
2. Its runner is standalone and phase-owned; it cannot define a product contract
   accidentally.
3. Native Harness and ACP retain independent M0 gates.
4. Upstream inspection/execution is ideas-only until later adoption review.
5. Real execution is forbidden until fake-candidate isolation, lifecycle,
   mutation, and cleanup proofs pass.
6. Public HEAD, release/tag, tag commit, release metadata target, `SOURCE_REV`,
   distributed artifact/package, changelog/product, service/provider, model, and
   protocol/SDK identities remain distinct.
7. Product trust/tool/permission/mutation design waits for concrete evidence;
   evaluation controls do not authorize product reuse.
8. Raw evidence and exclusions are preserved; invalid trials are not results.
9. Every milestone/slice gets one authorization and one reviewable commit.
10. Phase 16 may close with no qualified candidate or adoption recommendation.

### M1 must resolve before M2

M1 answers (docs lock; **human-accepted 2026-07-23**; **M1 amendment
human-accepted 2026-07-23**):

- **Eligible artifacts / invocations:** **Qwen Code only** — **`eligible for
  later M3 qualification`** on single-candidate observational path; **A-02/A-03
  resolved at M3a (2026-07-23)** from inspection without execution
  (`evaluation/M3A_ACQUISITION_EVIDENCE.md`). OpenCode and Grok Build remain
  **`blocked at M1`**. Full records in `evaluation/CANDIDATE_ARTIFACTS.md` and
  `evaluation/M1_AMENDMENT_QWEN_OBSERVATIONAL.md`.
- **Can at least two candidates qualify without source adoption, unsafe install,
  or an inferred binary/source mapping?** **Not from the current eligibility
  set.** One candidate is eligible; comparative claims remain impossible unless
  two later M3 qualifications actually succeed. Phase 16 is re-scoped to
  **single-candidate observational** for Qwen Code only; causal comparative
  campaign remains blocked.
- **Provider / service / model comparability:** **Established for Qwen Code
  observational path only** — DeepSeek, `https://api.deepseek.com`,
  `deepseek-v4-flash`; cost and account isolation locked per M1 amendment.
  OpenCode and Grok Build have no campaign provider configuration.
- **Synthetic corpus without held-out leakage:** Locked in
  `evaluation/TASK_CORPUS.md` (3 pilot + 10 tuning + 10 held-out). Held-out
  definitions carry reproducible `definition_commitment_sha256` commitments;
  prompt/script bodies remain access-controlled until M4c and must re-verify
  against those commitments before release.
- **Success / invalid / timeout / cleanup:** Locked in `TASK_CORPUS.md`
  (per-task success criteria, ceilings, invalidation rules) and
  `CAMPAIGN_LOCK.md` / `THREAT_MODEL.md` (evidence classes, isolation, cleanup).

### M2b must resolve before M3

- Can the chosen substrate enforce exact writable mounts and default-deny host
  access for every candidate? **Yes (M2b).**
- How is provider-only egress constrained and evidenced with current host tools?
  **Proven 2026-07-23** for `api.deepseek.com:443` only
  (`evaluation/M3_EGRESS_PROOF_EVIDENCE.md`).
- How is allowlisted DNS prevented inside the candidate sandbox?
  **Defined 2026-07-23** — sandbox-only `/etc/hosts` + nft IPv4 parity
  (`evaluation/M3_DNS_BINDING_GATE.md`); execution at launch remains gated.
- How are descendant termination and orphan absence proven for every launch mode?
  **Yes (M2b).**
- Where do raw artifacts live, what is the quota, and how are they disposed of?
  **Artifact root + retention/disposal locked in campaign/threat docs.**

Any answer that weakens a locked boundary requires an amended, reaccepted M0.

---

## 10. Limitations

- M0 installed or executed no candidate; no artifact is qualified.
- Repository metadata does not prove published binary-to-source mapping.
- Bubblewrap full network isolation works. Provider-restricted egress for
  `api.deepseek.com:443` was proven on 2026-07-23 under a separate grant
  (`evaluation/M3_EGRESS_PROOF_EVIDENCE.md`); real-candidate launch must reuse
  equivalent enforcement and execute the DNS binding gate immediately before
  launch (`evaluation/M3_DNS_BINDING_GATE.md`).
- Docker daemon access is unavailable and Podman is absent; neither is assumed.
- Provider nondeterminism, rate limits, service changes, and cost limit causal
  comparison even with a controlled runner.
- Synthetic tasks cannot establish production UX, long-run reliability,
  accessibility, or safe real-workspace mutation.
- Evidence informs but cannot replace a later production threat model,
  architecture M0, license review, or human acceptance.

---

## 11. Exit Conditions

- [x] M0 has explicit human acceptance (2026-07-22).
- [x] M1 campaign/artifact/task/threat lock was **explicitly human-accepted on
      2026-07-23** (all three candidates **blocked at M1** for later M3
      eligibility; zero candidates eligible for later M3 qualification; no
      comparative or single-candidate execution path authorized; held-out
      definition commitments recorded; **M2a unauthorized at the M1 boundary**
      — subsequently authorized and human-accepted 2026-07-23).
- [x] M2a runner contract/fake core was **explicitly human-accepted on
      2026-07-23** (standalone offline runner contract and deterministic
      repository-owned fake-candidate core; no production behavior, DI, public
      production types, upstream artifact acquisition, network access, process
      launch, or real candidate execution).
- [x] M2b isolation, lifecycle, mutation, cancellation, and cleanup proof is
      complete (2026-07-23; see `evaluation/ISOLATION_EVIDENCE.md`).
- [ ] Every M3 candidate records qualified, excluded, or blocked without guessed
      identities.
- [ ] M4 follows locked validity rules, or truthfully records why comparison
      could not proceed.
- [ ] Raw records, hashes, exclusions, invalid trials, and aggregates reconcile.
- [ ] No upstream source or shipping artifact was adopted.
- [ ] No production code, DI, public API, Agent schema, Conversations/Townhall
      behavior, or settings schema changed.
- [ ] Build, focused regressions, architecture counts, and `git diff --check`
      pass with exact totals; the full interactive default outcome is recorded,
      and any default failure either passes the required serial reproduction or
      is resolved as a regression before closeout.
- [ ] M5 states limitations and only a later-M0 recommendation.
- [ ] Human closeout acceptance is separate from technical readiness.

---

## 12. M0 Verification Record

Recorded on 2026-07-22 against the checkout in Section 3. These results supported
the explicit human M0 acceptance; they do not authorize M1.

- `dotnet build Zaide.slnx --no-incremental`: passed; 4 warnings, 0 errors.
  The warnings are one existing CS0067 unused-event warning in
  `ProjectDebugTargetResolverTests` and three existing xUnit2013 collection-size
  assertion warnings in `ArchitectureRatchetTests` (two) and
  `ArchitectureVisibilityTests` (one). This non-incremental result is the M0
  warning baseline; the earlier incremental no-op result is not.
- Focused Agents: 361 passed, 0 failed, 0 skipped, 361 total.
- Focused Conversations: 102 passed, 0 failed, 0 skipped, 102 total.
- Focused Townhall: 123 passed, 0 failed, 0 skipped, 123 total.
- Focused DI/composition: 184 passed, 0 failed, 0 skipped, 184 total.
- Focused Architecture: 26 passed, 0 failed, 0 skipped, 26 total.
- Default interactive parallel suite: 2,712 passed, 1 failed, 0 skipped,
  2,713 total. The sole failure was
  `LinuxTerminalServiceTests.Restart_DoesNotLeakFileDescriptors`, reporting an
  FD count increase from 251 to 279 across five restarts.
- Required serial reproduction with `slow.runsettings`: 2,713 passed, 0 failed,
  0 skipped, 2,713 total. The parallel-only FD-sensitive failure is therefore
  recorded as an existing runner-mode limitation, not a Phase 16 regression.
- Production types: 337 public, 165 internal, 502 total.
- Tracked production C# files: App 41, UI 4, Features 405, total 450.
- Production DI: 12 registration modules; 77 `AddSingleton`, 0 `AddScoped`, and
  0 `AddTransient` calls. The Agents module owns 10 singleton rows,
  Conversations 3, Townhall 5, and Settings 3.
- `git diff --check`: passed.

---

## 13. Acceptance Gate

**Human decision (M0):** Phase 16 M0 was explicitly accepted on 2026-07-22. This
acceptance closed the planning gate for the selected controlled Native Harness
evaluation infrastructure and campaign.

**Human decision (M1):** Phase 16 M1 was **explicitly human-accepted on
2026-07-23**. The accepted outcome is the campaign/artifact/task/threat lock
with an **all-blocked candidate eligibility** result:

- Qwen Code, OpenCode, and Grok Build public source are each **blocked at M1**
  (see `evaluation/CANDIDATE_ARTIFACTS.md`);
- no candidate is `eligible for later M3 qualification`;
- no comparative claim path and no single-candidate execution path is
  authorized;
- no candidate acquisition, installation, execution, or benchmark is
  authorized.

**Human decision (M2a):** Phase 16 M2a was **explicitly human-accepted on
2026-07-23**. The accepted outcome is the standalone offline runner contract
and deterministic repository-owned fake-candidate core documented in
`evaluation/RUNNER_CONTRACT.md`. M2a added no production behavior, DI, public
production types, upstream artifact acquisition, network access, process launch,
or real candidate execution.

**Human decision (M1 amendment):** Phase 16 M1 amendment for **Qwen Code
single-candidate observational** path was **explicitly human-accepted on
2026-07-23** (`evaluation/M1_AMENDMENT_QWEN_OBSERVATIONAL.md`):

- accepts `M3_UNBLOCK_AMENDMENT_PROPOSAL.md` as the amendment vehicle;
- Qwen Code → **`eligible for later M3 qualification`** (observational only);
- OpenCode and Grok Build remain **blocked at M1**;
- single-candidate observational path authorized; comparative rules unchanged;
- provider configuration locked to DeepSeek / `https://api.deepseek.com` /
  `deepseek-v4-flash`;
- A-02/A-03 remained `UNRESOLVED` until separately authorized M3a
  acquisition-and-inspection (completed 2026-07-23);
- **did not** authorize acquisition, egress tooling install, credentials,
  provider API calls, or upstream execution.

**M2b was completed and accepted on 2026-07-23** (repository-owned isolation,
lifecycle, mutation, cancellation, and cleanup evidence in
`evaluation/ISOLATION_EVIDENCE.md`; no production behavior, DI, public types,
upstream acquisition, network access, or real candidate execution).

**M3a acquisition-and-inspection was completed on 2026-07-23** under an
explicit separate grant (`evaluation/M3A_ACQUISITION_EVIDENCE.md`): pinned
archive acquired outside the repository, SHA-256 verified, licenses scanned,
A-02/A-03 resolved; binary not launched. **M3a recovery re-acquisition was
completed on 2026-07-24** after host reboot wiped `/tmp/phase16-artifacts`
(same pin/hash/license results; extract for inspection only; binary/Node not
launched; no qualification retry).

**M3 egress proof was completed on 2026-07-23** under an explicit separate
grant (`evaluation/M3_EGRESS_PROOF_EVIDENCE.md`): allowlisted
`api.deepseek.com:443` HTTPS succeeded without credentials; non-allowlisted
HTTPS destinations were blocked; evidence under the phase artifact root.
**M3 DNS binding gate defined 2026-07-23**
(`evaluation/M3_DNS_BINDING_GATE.md`). **M3 write-capable remediation (2026-07-24)** locked `--approval-mode yolo`
and post-exit reaping
(`evaluation/M3_WRITE_CAPABLE_REMEDIATION_EVIDENCE.md`). **M3 wall-time +
exit-reap remediation (2026-07-24)** raised active wall to **120s** and fixed
same-shell wait/reap (`evaluation/M3_WALL_TIME_AND_REAP_REMEDIATION_EVIDENCE.md`).
**M3 24-turn ceiling remediation (2026-07-24)** raised active session-turn
ceiling to **24** (`--max-session-turns 24`) in policy, orchestrator, tests,
and docs; historical 12-turn session records preserved unchanged; not a
qualification retry.
**M3 fresh-session eligibility remediation (2026-07-24)** fixed orchestrator
ordering (egress preflight before credential), fresh-session execution/evidence
contract, and consumed-but-unlaunched recording; not a qualification retry.
**Latest M3 qualification smoke** (`m3q-20260724T060109Z-45dd1c5f`) executed
DNS binding, write-capable yolo argv (12 turns / **120s** wall), and one Qwen
launch; TC-T01 rename **verified** but **NO-GO** on `qwen_exit=53` (turn limit);
spend balance delta USD 0.00; fixed parent-shell reap recorded real inner exit
4; post-exit finalization completed; candidate still **not qualified**.
