# Phase 15: Backend-Neutral Agent Session and Event Foundation — Implementation Plan

## Status and authorization

**Phase 15 status:** **M0–M3b-1 accepted.** **M3b-2 unauthorized.**
M3b-2 and later milestones remain unauthorized pending separate explicit authorization.

**Production authorization:** **M3a GO.** **M3b-1 accepted on 2026-07-22 (commit `29f66247`).**
M3b-2 and Phase 15 closeout remain **NO-GO** until separately authorized.
Phase 16 has not started. Native Harness production implementation and ACP
integration have not started.

**Audit baseline:**

| Check | Verified result |
|-------|-----------------|
| Branch | `master` |
| `HEAD` | `57d8c44bfa3f4948a400f0aa25ebbe6c9accaf36` |
| `origin/master` | `57d8c44bfa3f4948a400f0aa25ebbe6c9accaf36` |
| Working tree before M0 edits | Clean |
| Phase 14 | Accepted and closed at accepted baseline `67da1394` |
| Verification date | 2026-07-21 |
| M0 acceptance | Accepted by human on 2026-07-21 |

## Pre-implementation verification (M0)

- [x] Read `AGENTS.md`, `docs-rules.md`, `docs/CONVENTIONS.md`,
      `docs/DESIGN.md`, `docs/roadmap/V3.md`,
      `docs/architecture/OVERVIEW.md`, and `docs/phases/README.md` completely.
- [x] Read the Phase 14 plan and the relevant Refactor 7, Refactor 8, and
      Refactor 6.3 lifetime evidence completely.
- [x] Verify the live checkout, branch, remote-tracking commit, and Phase 14
      closeout status.
- [x] Audit Agent execution, routing, conversation/run correlation, Townhall
      projection, configuration, HTTP, DI, lifetime, persistence, recovery,
      cancellation, and architecture ratchets against live source.
- [x] Compare candidate Phase 15 outcomes by dependency order.
- [x] Verify Native Harness research candidates from current primary upstream
      sources without copying or adapting upstream code.
- [x] Lock research/provenance and evaluation requirements before any adoption.
- [x] Define independently verifiable post-M0 milestones, tests-first gates,
      rollback boundaries, and expected commit boundaries.
- [x] Run and record current build, focused-test, architecture-test, full-suite,
      and whitespace baselines.

No new library is required or authorized by M0.

---

## M0 outcome resolution

The accepted V3 roadmap previously stopped at Phase 14 and did not assign an
explicit Phase 15 outcome. The live dependency graph resolves that ambiguity.

### Candidate comparison

| Candidate | Dependency fit | Benefit | Blocking problem | M0 disposition |
|-----------|----------------|---------|------------------|----------------|
| Native Harness research/evaluation foundation | Research can start now, but production design needs a stable Zaide-side session/run/event boundary and a controlled comparison protocol | Directly advances the first-party harness goal | If selected as the whole phase, it would either produce research with no application integration boundary or tempt a harness-specific contract that ACP must later work around | **Required supporting work, not the Phase 15 roadmap outcome** |
| Backend-neutral Agent Session/event contract foundation | Directly follows Phase 14's unified conversations and is required by both independent backend paths | Gives Townhall one truthful lifecycle/event boundary and lets the existing HTTP path become the first concrete compatibility backend | Must stay narrow: current non-streaming behavior is the only concrete producer, so tools, permissions, resume, trace, and usage cannot be invented | **Selected Phase 15 outcome** |
| Smaller run/event-only prerequisite with Agent Session deferred again | Reuses the existing `ExecutionRunId` with the least immediate surface | Smaller first diff | Repeats the live ownership gap: no object owns backend binding, capability version, run admission, cancellation lifecycle, or the relationship between multiple runs in one conversation | **Rejected** |

### Selected roadmap-level outcome

> Phase 15 establishes a backend-neutral, in-memory Agent Session and structured
> event foundation over the current unified conversation model, then adapts the
> existing non-streaming OpenAI-compatible execution path as the first concrete
> compatibility backend without implementing the Native Harness or ACP.

This is dependency-first:

```text
Phase 14 unified conversations
  -> Phase 15 backend-neutral session/run/event foundation
      -> later Zaide Native Harness backend
      -> later independent ACP backend
```

Native Harness and ACP remain sibling backend paths. Neither is implemented in
Phase 15, and neither may depend on the other's internals.

---

## Verified live facts

### Execution, routing, and identity

| Concern | Live owner / exact path | Verified fact |
|---------|-------------------------|---------------|
| Narrow execution interface | `src/Features/Agents/Contracts/IAgentExecutionService.cs` | One `ExecuteAsync(string, CancellationToken)` call returns text-or-error. No backend identity, session, event stream, capability, usage, or structured protocol failure. |
| HTTP execution | `src/Features/Agents/Infrastructure/AgentExecutionService.cs` | One non-streaming manual JSON `POST /chat/completions`; `stream = false`; current user message only; no history, tools, retry, or provider registry. |
| Effective configuration | `AgentExecutionService.BuildEffectiveOptions()` | Resolved per call. URL/model precedence is environment over saved settings. API key precedence is environment over `ISecretStore`. |
| Settings | `src/Features/Settings/Domain/SettingsModel.cs` | Schema v3 stores one `LlmSettings` record: base URL, model, and API-key source marker. It is not a backend registry or per-agent profile. |
| Secrets | `src/Features/Settings/Contracts/ISecretStore.cs`; `Infrastructure/FileSecretStore.cs` | `llm.apiKey` is stored separately from settings. There is no per-backend/per-agent secret namespace contract. |
| HTTP ownership | `src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs` | One application-lifetime singleton `HttpClient` with a 120-second timeout. |
| Coordinator | `src/Features/Agents/Application/AgentExecutionCoordinator.cs` | One in-flight request per `ConversationId`; appends user and terminal entries; clears draft; busy state is in-memory; returns `null` for several no-op/admission-rejection paths. |
| Router | `src/Features/Agents/Application/AgentRouter.cs` | Resolves one visible name against typed Actor catalog, get-or-creates a thin panel host, and invokes the coordinator. Routing failure creates a terminal run result and conversation entry when possible. |
| Actor identity | `src/Features/Conversations/Domain/ActorId.cs`; `Application/ActorCatalog.cs` | Stable typed Actor identity exists and is independent from display name. |
| Conversation identity | `src/Features/Conversations/Domain/ConversationId.cs`; `Conversation.cs` | Stable Channel/Direct conversation identity and ordered entries exist. Selection is presentation state. |
| Run identity | `src/Features/Agents/Domain/ExecutionRunId.cs` | A typed `run:{guid}` identity exists. |
| Run representation | `src/Features/Agents/Domain/ExecutionRun.cs` | Constructed only after the attempt reaches a terminal outcome. It holds conversation, initiating/target actors, target panel id, and terminal outcome; it is not a live lifecycle owner. |
| Run outcomes | `src/Features/Agents/Domain/ExecutionRunOutcome.cs` | Only `Success`, `RoutingFailure`, `ExecutionFailure`, and `Cancelled`. Accepted, queued, running, rejected, timed out, disconnected, indeterminate, cancellation-requested, and cancellation-acknowledged are absent. |

### Conversation persistence and Townhall projection

| Concern | Live owner / exact path | Verified fact |
|---------|-------------------------|---------------|
| Authoritative entries | `src/Features/Conversations/Domain/ConversationEntry.cs` | Entry ID, kind, author, timestamp, content, and optional opaque correlation ID. No session ID, backend ID, sequence, causation, evidence, visibility, payload version, or structured payload. |
| Entry taxonomy | `ConversationEntryKind.cs` | Six produced kinds: user chat, assistant response, routing failure, execution failure, channel event, system notification. Tool/think/raw-trace kinds are intentionally absent. |
| Legacy presentation taxonomy | `src/Features/Townhall/Domain/TownhallMessage.cs` | Compatibility fields/kinds reserve provider/model/thread/tool concepts, but there is no authoritative producer protocol around them. They must not be treated as implemented capabilities. |
| Projection | `src/Features/Townhall/Presentation/TownhallEntryProjection.cs` | Maps authoritative conversation entries to current Townhall display values. It does not consume a backend event stream. |
| Persistence | `src/Features/Conversations/Infrastructure/ConversationPersistenceService.cs` | Schema v1 persists conversations, entries, channel rows, active selection, drafts, and read cursors. It does not persist Agent Sessions, live runs, backend bindings, capability snapshots, tool actions, audit records, usage, or raw traces. |
| Recovery | Same service + Phase 14 contract | Corrupt/LKG recovery is best-effort. Since no run/session is persisted, restart restores conversation history but never invokes or resumes side-effecting agent execution. |
| No-auto-resume | Phase 14 D12/D13 and live schema | Preserved. Interrupted live work is not resumable state. Phase 15 must not silently convert historical correlated entries into an active run. |

### Cancellation, failure, and lifecycle

- `CancellationToken` reaches `HttpClient.SendAsync`, but cancellation is
  converted to a human-readable failure by the execution service.
- The coordinator infers cancellation by inspecting error text containing
  `cancelled`; request, acknowledgement, and confirmed terminal cancellation
  are not separate facts.
- A second send to the same conversation returns `null`; rejection is not a
  structured lifecycle event.
- Closing or navigating away from presentation chrome does not cancel the
  conversation-owned in-flight request.
- No timeout outcome exists even though `HttpClient` has a 120-second timeout.
- No session end, disconnect, reconnect, backend process exit, resume token, or
  recovery owner exists.

### DI, lifetime, visibility, and assembly boundaries

| Baseline | Verified live value |
|----------|---------------------|
| Production projects / assemblies | One `src/Zaide.csproj`; one `Zaide` production assembly |
| Registration modules | 12 files under `src/App/Composition/Registration/` |
| Production DI registrations | 73 `AddSingleton` calls; 0 scoped; 0 transient |
| Historical lifetime map | `docs/refactor/refactor-6.3/LIFETIME_MAP.md` truthfully records the pre-Phase-14 67-registration baseline; Phase 14 added six live singleton registrations |
| Top-level production types | 463 total: 337 public / 126 internal |
| Tracked production C# files | 426: App 41 / UI 4 / Features 381 |
| Architecture findings | Exactly 2 intentional composition locator residuals; namespace-direction allowlist empty |
| Root admissions | `src/Infrastructure/` and `src/UI/Shared/` remain absent and deny-by-default |

Current Agent/Conversation registrations are application-lifetime DI
singletons. Editor and Terminal demonstrate factory-owned shorter semantic
lifetimes, but no Agent Session factory or owner exists. Phase 15 may add only
concrete Agent-session owners required by its accepted milestones; it must not
create child containers or a speculative assembly split.

---

## Concept existence matrix

| Required concept | Live state | Phase 15 disposition |
|------------------|-----------|----------------------|
| Backend-neutral session lifecycle | **Absent** | Implement narrow in-memory start/send/cancel/end lifecycle; resume/reconnect remain unsupported |
| Run identity | **Partial** — `ExecutionRunId` exists | Retain it; do not create a competing run ID |
| Run state machine | **Absent** — terminal result only | Implement explicit valid transitions and structured rejection |
| Streaming events | **Absent** | The contract will deliver ordered events, but the compatibility backend emits non-streaming message completion only; token streaming remains unsupported |
| Structured events | **Partial** — conversation entries and terminal result | Add backend-neutral lifecycle/message/capability events; continue projecting to existing entries |
| Capability discovery/versioning | **Absent** | Add versioned snapshot with separate advertised, available, configured, permitted, and degraded/currently-usable facts |
| Permission requests and decisions | **Absent** | Explicitly defer; no tool/action consumer exists in Phase 15 |
| Tool execution and workspace mutation | **Absent** | Explicitly defer; no tool schemas, permission engine, or file/process mutation path |
| Usage/cost reporting | **Absent** | Explicitly unsupported for the compatibility backend; do not synthesize zero values |
| Audit evidence levels | **Absent** | Introduce the V3 evidence-level vocabulary for emitted activity; do not claim backend reports are Zaide-executed |
| Raw trace boundary | **Absent** | Record boundary and unsupported status only; no capture, persistence, redaction pipeline, or UI |
| Resume/reconnect semantics | **Absent** | Explicitly unsupported; no-auto-resume remains locked |

Capability, permission, availability, configuration, and current usability are
separate facts. A capability snapshot must never collapse them into one
`IsSupported` Boolean. Backend-reported activity must never be labeled as
Zaide-executed or Zaide-verified workspace mutation.

---

## Locked Phase 15 decisions

These became accepted implementation constraints with explicit human M0
acceptance on 2026-07-21.

| ID | Decision | Locked rule |
|----|----------|-------------|
| P15-D01 | Phase outcome | Backend-neutral Agent Session and structured event foundation over the existing conversation model. |
| P15-D02 | Backend independence | Native Harness and ACP remain independent sibling backends behind the same application boundary; neither wraps or imports the other. |
| P15-D03 | Concrete first consumer | The existing non-streaming OpenAI-compatible path is the only Phase 15 backend implementation and serves as a compatibility adapter, not the future Native Harness. |
| P15-D04 | Session cardinality | One Agent Session binds one `Agent Identity`, one `ConversationId`, and one backend identity/version. One conversation may have multiple sessions over time. A session is not a panel, provider, process, model, or conversation. |
| P15-D05 | Run cardinality | One admitted run belongs to exactly one session and one conversation. Reuse `ExecutionRunId`; correlation to conversation entries remains exact. |
| P15-D06 | Lifetime | Sessions and active runs are application-owned in memory for Phase 15. No DI scope, child provider, session persistence, or resume token. |
| P15-D07 | Lifecycle honesty | Admission, running, completion, failure, rejection, cancellation request, confirmed cancellation, timeout, disconnect, and indeterminate states are distinct where evidence exists. Unsupported states are not fabricated. |
| P15-D08 | Events | Ordered, schema-versioned events carry stable event/session/run/conversation/backend identity, sequence, occurred/received timestamps, and evidence level. Only concrete Phase 15 payloads are introduced. |
| P15-D09 | Capability truth | Capability snapshot versioning separates advertisement, availability, configuration, permission, degradation, and current usability. Change notification is deterministic. |
| P15-D10 | Compatibility projection | Townhall continues to render the accepted Phase 14 conversation entry shapes. Backend events are projected once into typed entries; presentation never becomes execution truth. |
| P15-D11 | Persistence/recovery | Conversation schema v1 and privacy remain unchanged. Session/run/event persistence and reconnect are deferred. Restart never auto-resumes execution. |
| P15-D12 | Tools and permissions | No tool execution, workspace mutation, permission request/decision engine, command execution, diff application, or secret-access grant is implemented. |
| P15-D13 | Usage and trace | Missing usage/cost/raw trace is reported as unavailable, not zero or empty evidence. No sensitive raw payload is persisted or rendered. |
| P15-D14 | Dependencies and assembly | No package, SDK, service, process protocol, new project, or assembly split. New types default internal unless an existing cross-feature contract proves public visibility. |
| P15-D15 | Research classification | M0 upstream inspection is `ideas-only / read-only evaluation`. No copied, adapted, translated, generated, or vendored upstream material enters Zaide. |
| P15-D16 | Authorization | M0 acceptance does not authorize M1a. Every milestone or slice needs explicit authorization and one reviewable commit boundary. |

---

## Accepted M0 implementation decisions

The human accepted these implementation locks with the M0 plan on 2026-07-21.

1. Keep Agent-session domain and application contracts under
   `src/Features/Agents/`; Conversations remains agent-neutral and does not
   depend on Agents.
2. Reuse `ExecutionRunId` and `ConversationEntryCorrelationId` rather than
   introducing parallel identifiers.
3. Deliver the application event feed as `IObservable<AgentEvent>` on
   `IAgentSessionService`. The service owns ordering; subscribers do not mutate
   lifecycle state.
4. Keep the compatibility HTTP adapter behind the new backend boundary until
   M3b-2 proves full behavior parity, then delete only genuinely superseded
   orchestration paths.
5. Treat provider/model identity as backend/session metadata, not Agent
   Identity and not routing identity.

---

## Open questions

These do not authorize implementation and must be resolved at the named gate.

| Question | Resolution gate |
|----------|-----------------|
| Exact Phase 16 outcome | Outside Phase 15 M0; remains unassigned |

Implementation locks replacing the rejected alternatives:

- Session creation is lazy on the first admitted send. Opening or selecting a
  direct conversation creates no session and performs no network request.
- M3a distinguishes caller cancellation from HTTP timeout by checking the
  caller token at the caught cancellation boundary; it never classifies by
  human-readable error text.
- `IAgentPanelHost` remains a thin compatibility seam through Phase 15
  closeout. Removing it requires a later separately planned change.

---

## Explicitly deferred concerns

- Native Harness implementation, source reuse, runnable benchmark campaign,
  provider tool loop, context compaction, memory, and subagents.
- ACP protocol/SDK selection, transport, process ownership, authentication,
  reconnect, and extension support.
- Permission UI/engine, tool schemas, command execution, file mutation,
  workspace trust, audit persistence, change attribution, and rollback.
- Session/run/event persistence, raw trace capture/redaction/retention, usage
  accounting, pricing, token budgets, and cost UI.
- Streaming token UI, attachments, multimodal input, multi-provider registry,
  per-agent provider settings, retries, and automatic failover.
- Conversation schema v2, cross-window sync, multi-workspace session recovery,
  and any change to Phase 14 privacy, draft, unread, or no-auto-resume behavior.
- Human-to-Human product flows and unrelated UI/design work.

---

## Native Harness research baseline (primary sources only)

**Verification date:** 2026-07-21. Repository refs and release metadata were
queried directly from the upstream Git repositories and GitHub APIs. No
upstream repository was cloned into Zaide and no upstream source was copied,
adapted, or translated.

| Candidate | Exact upstream source | Current HEAD at verification | Pinned current release | Repository license | M0 classification |
|-----------|-----------------------|------------------------------|------------------------|--------------------|-------------------|
| Qwen Code | `https://github.com/QwenLM/qwen-code` | `1b41cbb516013a335bf954f2be56421f61bd4506` (`main`) | `v0.20.0`; tag commit `92fda5603e84ef62a1b29bf6faf4f6a8124a2bf7`; published 2026-07-19 | Apache-2.0; top-level `LICENSE` blob `2d4f1fee82272cbad5f22394dc064e621f668aac` | Ideas-only/read-only. README states the project originated from Gemini CLI v0.8.2; any adoption must trace inherited provenance and notices. |
| OpenCode | `https://github.com/anomalyco/opencode` | `849c2598abc7d2b40261e74b5826bc74ffc78308` (`dev`) | `v1.18.4`; tag commit `49c69c5ed3ccf706b61b3febb43c8aaff7f8325e`; release metadata target `4872c48c230728150e8e3406722943450ed58dcb`; published 2026-07-20 | MIT; top-level `LICENSE` blob `6439474beed8e0271df9862eff97ffd70ec2464c`; copyright 2025 opencode | Ideas-only/read-only. |

Primary URLs checked:

- `https://github.com/QwenLM/qwen-code`
- `https://github.com/QwenLM/qwen-code/commit/1b41cbb516013a335bf954f2be56421f61bd4506`
- `https://github.com/QwenLM/qwen-code/releases/tag/v0.20.0`
- `https://github.com/QwenLM/qwen-code/blob/1b41cbb516013a335bf954f2be56421f61bd4506/LICENSE`
- `https://github.com/anomalyco/opencode`
- `https://github.com/anomalyco/opencode/commit/849c2598abc7d2b40261e74b5826bc74ffc78308`
- `https://github.com/anomalyco/opencode/releases/tag/v1.18.4`
- `https://github.com/anomalyco/opencode/blob/849c2598abc7d2b40261e74b5826bc74ffc78308/LICENSE`

The top-level license is not a transitive dependency audit. Both repositories
contain workspaces, dependencies, generated material, and project-specific
subtrees that may carry separate notices or obligations. No adoption decision
is made at M0.

### Required provenance record before adoption

Every candidate component, idea translated into non-trivial implementation,
dependency, binary, asset, prompt, fixture, corpus, or generated artifact must
record:

1. upstream project and exact repository URL;
2. exact commit and release/tag, plus source path and range/component;
3. complete applicable licenses and SPDX identifiers;
4. copyright, NOTICE, source-offer, patent, and trademark obligations;
5. classification: `copied`, `adapted`, `translated`, `generated`, or
   `ideas-only`;
6. local modifications and responsible Zaide component;
7. transitive dependency/asset/prompt/corpus obligations;
8. SBOM and lockfile impact;
9. update and security tracking owner;
10. adopt, isolate, rewrite, or reject decision with rationale.

Missing provenance is a hard adoption stop. Apache-2.0 or MIT at repository
root does not automatically clear every subtree or dependency.

---

## Reproducible evaluation protocol

M0 defines the protocol only. It does **not** run a broad benchmark program.

### Controlled comparison manifest

Each causal comparison must freeze and record:

- harness name, exact commit/release, configuration, and local patches;
- model/provider exact version, endpoint, temperature/top-p/seed where exposed,
  reasoning setting, and retry policy;
- repository URL, exact commit, clean/dirty state, platform, runtime, and tool
  versions;
- system/developer/task prompts and context policy;
- available tools, permission policy, network policy, and workspace boundary;
- wall-clock, model-turn, token, monetary, command, tool-call, and output budgets;
- task acceptance tests and rubric version;
- trial number, reset proof, and retained artifact directory.

If a variable cannot be controlled, label the comparison **observational**, not
causal.

### Task classes

Use small, licensed, reproducible tasks across multiple repositories:

1. locate and explain with no mutation;
2. one-file behavioral bug with focused tests;
3. multi-file feature with architecture constraints;
4. test failure diagnosis and recovery;
5. precise refactor with behavior preservation;
6. cancellation/interruption and cleanup;
7. conflict or stale-context recovery;
8. held-out tasks and held-out repositories unavailable during tuning.

Zaide may be one evaluation repository but must not be the only repository.
Task and corpus licensing/contamination status must be recorded.

### Metrics

| Dimension | Required measurement |
|-----------|----------------------|
| Success | Acceptance-test and rubric pass; partial completion recorded separately |
| Regression | Full-suite failures and behavior regressions attributable to the trial |
| Diff quality | Necessary files/lines, unrelated churn, readability, architecture compliance, blinded/rubric review where practical |
| Recovery | Response to tool/command/test failure, stale context, cancellation, and retry loops |
| Efficiency | Wall time, model turns, searches, tool calls, edit/test cycles, latency distribution |
| Tokens/cost | Input/output/cached/reasoning tokens where exposed; monetary cost including failed attempts |
| Safety | Permission correctness, workspace-boundary violations, destructive actions, leaked secrets |
| Reproducibility | Reset success, artifact completeness, environment fingerprint, variance across repeated trials |

### Trial and artifact rules

- Reset repository and environment to the exact snapshot before every trial.
- Use at least three trials per task/configuration before comparative claims;
  retain every failure and catastrophic outlier.
- Never reuse untracked artifacts, caches, conversation state, or modified
  dependencies unless the manifest explicitly defines them.
- Retain prompt/event transcript available from the harness, commands, exit
  codes, stdout/stderr with secrets redacted, final diff, test results, timing,
  usage/cost, and environment fingerprint.
- Hash retained artifacts and record redaction/truncation.
- Keep tuning tasks and held-out tasks separate. A task moves out of held-out
  status permanently once inspected during tuning.
- Do not compare dissimilar model/provider/tool budgets as though the harness
  alone caused the result.

---

## Scope

**Goal:** Establish a backend-neutral in-memory session/run lifecycle and
ordered structured event boundary, prove it with the current non-streaming HTTP
execution path, and preserve the Phase 14 conversation experience.

**In scope:**

- upstream research/provenance/evaluation artifacts;
- session identity, backend binding identity, run states, valid transitions,
  structured lifecycle/message/capability events, evidence levels;
- honest versioned capability snapshot and change notification;
- in-memory Agent Session/run owner;
- compatibility adapter over current `IAgentExecutionService` behavior;
- coordinator/router cutover to structured admission and terminal outcomes;
- one projection from normalized events into current conversation entries;
- tests-first domain, application, infrastructure, DI, architecture, and parity
  coverage;
- docs/status truth-sync at closeout.

**Out of scope:** everything listed under Explicitly deferred concerns,
especially Native Harness, ACP, tool/permission/workspace mutation, packages,
new schemas, session persistence/resume, and raw trace.

---

## Expected file ownership

The following paths are planning locks for post-M0 work. M0 creates none of
them except this plan.

| Concern | Locked owner/path |
|---------|-------------------|
| Research records | `docs/phases/v3/phase-15/research/` |
| Session/run/event/capability value types | `src/Features/Agents/Domain/` |
| Backend and session application contracts | `src/Features/Agents/Contracts/` |
| In-memory lifecycle/state-machine/event coordination | `src/Features/Agents/Application/` |
| Existing HTTP compatibility backend/adapter | `src/Features/Agents/Infrastructure/` |
| Conversation projection | `src/Features/Agents/Application/`; consumes `Features.Conversations` contracts/domain only |
| DI registrations | existing `src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs` |
| Townhall projection consumption | No `TownhallViewModel` change. It continues observing `IConversationStore`; the Agent application projection owns event-to-entry admission. |
| Tests | mirrored `tests/Zaide.Tests/Features/Agents/{Domain,Application,Infrastructure}/`, existing Conversations/Townhall parity suites, and `tests/Zaide.Tests/Architecture/` |

Forbidden ownership:

- no Agent-session type under Conversations, Townhall Presentation, App Shell,
  `UI/Shared`, or root `Infrastructure`;
- no backend implementation under Domain/Application;
- no View or ViewModel dependency from Agent Domain/Application/Infrastructure;
- no provider-specific type in the backend-neutral contracts.

---

## Milestones (incremental)

No milestone below is authorized by M0 acceptance.

### M1a — Research, provenance, and evaluation locks (docs only)

**Scope:**

- Create:
  - `docs/phases/v3/phase-15/research/UPSTREAM_CANDIDATES.md`
  - `docs/phases/v3/phase-15/research/PROVENANCE_REGISTER.md`
  - `docs/phases/v3/phase-15/research/EVALUATION_PROTOCOL.md`
- Re-verify Qwen Code and OpenCode refs/licenses on execution date.
- Inventory task loop, context selection, search/edit/tool loop, failure
  recovery, cancellation, compaction, and test strategy from pinned sources.
- Classify every inspected item ideas-only unless a later milestone receives a
  separately reviewed adoption decision.
- Define only the small evaluation fixtures needed to validate Phase 15
  contracts; do not run the broad harness comparison campaign.

**Non-goals:** production/test code, cloning source into Zaide, dependencies,
benchmark runner, harness ranking, adoption.

**Gate:** exact primary-source refs and license records; rejected-approach log;
`git diff --check`; no `src/` or `tests/` diff.

**Rollback/commit:** delete/revert only the three research documents. Expected
single commit: `docs(phase-15): lock upstream research and evaluation protocol`.

### M1b — Backend-neutral domain and contracts

**Tests first:** add focused failing tests under
`tests/Zaide.Tests/Features/Agents/Domain/` and
`tests/Zaide.Tests/Features/Agents/Contracts/` before production types.

**Planned production paths (exact Phase 15 contract set):**

- `src/Features/Agents/Domain/AgentBackendId.cs`
- `src/Features/Agents/Domain/AgentSessionId.cs`
- `src/Features/Agents/Domain/AgentSessionStatus.cs`
- `src/Features/Agents/Domain/AgentRunStatus.cs`
- `src/Features/Agents/Domain/AgentEventId.cs`
- `src/Features/Agents/Domain/AgentEvent.cs`
- `src/Features/Agents/Domain/AgentEventKind.cs`
- `src/Features/Agents/Domain/AgentActivityEvidenceLevel.cs`
- `src/Features/Agents/Domain/AgentCapabilityId.cs`
- `src/Features/Agents/Domain/AgentCapabilityState.cs`
- `src/Features/Agents/Domain/AgentCapabilitySnapshot.cs`
- `src/Features/Agents/Domain/AgentBackendRequest.cs`
- `src/Features/Agents/Domain/AgentBackendEvent.cs`
- `src/Features/Agents/Domain/AgentSessionSnapshot.cs`
- `src/Features/Agents/Domain/AgentRunSnapshot.cs`
- `src/Features/Agents/Contracts/IAgentBackend.cs`
- `src/Features/Agents/Contracts/IAgentSessionService.cs`

**Required behavior:** ID invariants; one-session/one-conversation/one-agent/
one-backend cardinality; valid run/session transitions; monotonic event
sequence; schema version; evidence level; separate capability facts; unknown
or unavailable values represented honestly. `AgentEvent` is one immutable,
invariant-checked envelope with event/session/run/conversation/backend IDs,
sequence, occurred/received timestamps, optional causation event ID, evidence
level, kind, and exactly one matching lifecycle/message/failure/capability
payload. It has no generic JSON or tool-payload escape hatch.

**Locked contract shape:**

- `IAgentBackend` exposes immutable backend ID/version/capability snapshot and
  `IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(AgentBackendRequest,
  CancellationToken)`.
- `IAgentSessionService` exposes `IObservable<AgentEvent> Events`, one
  `SendAsync` operation that takes conversation/initiator/target/backend/message
  identity and lazily get-or-creates the session, `CancelAsync`, `EndAsync`, and
  read-only session/run snapshots.
- `AgentBackendEvent` carries only backend observations required by the current
  compatibility path. `AgentSessionService` assigns normalized event IDs,
  sequence, received time, and lifecycle truth.
- Session states are `Ready`, `Running`, `Ending`, and `Ended`.
- Run transitions are `Created -> Accepted | Rejected`,
  `Accepted -> Running`, and `Running -> Completed | Failed | Cancelled |
  TimedOut | Disconnected | Indeterminate`. `Running -> CancellationRequested`
  may then reach any truthful terminal state, including late completion.

**Exact tests-first paths:**

- `tests/Zaide.Tests/Features/Agents/Domain/AgentSessionContractTests.cs`
- `tests/Zaide.Tests/Features/Agents/Domain/AgentRunStateContractTests.cs`
- `tests/Zaide.Tests/Features/Agents/Domain/AgentEventContractTests.cs`
- `tests/Zaide.Tests/Features/Agents/Domain/AgentCapabilitySnapshotTests.cs`
- `tests/Zaide.Tests/Features/Agents/Contracts/AgentBackendContractTests.cs`

**Non-goals:** DI wiring, HTTP adapter, UI, persistence, tools, permission
payloads, raw trace, usage types without a producer.

**Gate:** focused Domain/Contracts tests; Architecture 26-test baseline plus
intentional visibility/source-count amendments; full suite; `git diff --check`.

**Rollback/commit:** additive domain/contracts files and matching tests only.
Expected single commit: `feat(agents): add backend-neutral session and event contracts`.

### M2 — In-memory session/run lifecycle owner

**Tests first:** state transition, admission, concurrency, cancellation,
sequence, event-order, capability-version, end-session, and no-resume tests
at these exact paths:

- `tests/Zaide.Tests/Features/Agents/Application/AgentSessionServiceTests.cs`
- `tests/Zaide.Tests/Features/Agents/Application/AgentEventStreamTests.cs`
- existing Agents registration/DI tests

**Planned production paths:**

- `src/Features/Agents/Application/AgentSessionService.cs`
- `src/Features/Agents/Application/AgentSessionStateMachine.cs`
- `src/Features/Agents/Application/AgentRunStateMachine.cs`
- `src/Features/Agents/Application/AgentEventStream.cs`
- existing `src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs`

**Required behavior:** application-owned in-memory sessions; no network on
session creation; explicit structured rejection instead of `null`; one active
run per conversation/session under the retained product rule; deterministic
events and capability snapshots; explicit cancellation request versus terminal
cancellation when observable; end clears live ownership without deleting
conversation history.

**Non-goals:** adapter cutover, Townhall UI, persistence/resume/reconnect,
process ownership, tools.

**Gate:** focused Agents Application + registration/DI tests; Conversations
tests; Architecture; full suite; exact type/source/registration counts;
`git diff --check`.

**Rollback/commit:** lifecycle owner, DI registration, and focused tests form
one revertible unit. Expected commit: `feat(agents): own in-memory agent session lifecycle`.

### M3a — Existing HTTP compatibility backend adapter

**Tests first:** request shape, live configuration precedence, secret boundary,
HTTP success/failure, malformed response, caller cancellation, timeout
classification, no retries, no tool fields, no history submission, capability
snapshot, and event ordering in
`tests/Zaide.Tests/Features/Agents/Infrastructure/LegacyOpenAiCompatibleAgentBackendTests.cs`
plus the existing `AgentExecutionServiceTests` and `LiveLlmConfigTests`.

**Planned production paths:**

- new `src/Features/Agents/Infrastructure/LegacyOpenAiCompatibleAgentBackend.cs`
- existing `src/Features/Agents/Infrastructure/AgentExecutionService.cs`
- existing `src/Features/Agents/Contracts/IAgentExecutionService.cs`
- existing `src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs`

The adapter wraps the live service first. Do not rename/delete the legacy
contract until M3b-2 parity proves every caller migrated.

**Required capability truth:** message completion advertised/available when
configured; non-streaming; cancellation best-effort; tools, permissions,
resume, reconnect, usage/cost, attachments, and raw trace unavailable. Do not
emit fake zero usage or fake streaming deltas.

**Gate:** focused Infrastructure + live-config tests; Agents Application;
registration/DI; Architecture; full suite; `git diff --check`. A local fake
HTTP handler is automated evidence; no real credential is required.

**Rollback/commit:** adapter and registration are additive behind the legacy
path. Expected commit: `feat(agents): adapt legacy HTTP execution as backend`.

**M3a acceptance (2026-07-22):** Human accepted at parent commit
`9e97ca181a4d136afdd1d598054b1de7d7a337af` gate. Implementation commit adds
`LegacyOpenAiCompatibleAgentBackend` (`backend:legacy-openai-compatible`),
typed failure classification in `AgentExecutionService`, versioned capability
snapshots (including resolution-unavailable truth), attachments unavailable row,
and DI registration as the sole `IAgentBackend`. M3b-1 accepted (2026-07-22 at `29f66247`); M3b-2 remains unauthorized.

### M3b-1 — Coordinator/router session cutover

**Tests first:** direct send, mention route, structured admission rejection,
busy state, success/failure/cancellation, switch/close during flight, private
conversation ownership, correlation, and event ordering in new
`tests/Zaide.Tests/Features/Agents/Application/AgentSessionCoordinatorParityTests.cs`
plus the existing coordinator/router suites.

**Planned existing paths:**

- `src/Features/Agents/Application/AgentExecutionCoordinator.cs`
- `src/Features/Agents/Application/AgentRouter.cs`
- `src/Features/Agents/Contracts/IAgentExecutionCoordinator.cs`
- `src/Features/Agents/Domain/ExecutionRun.cs`
- `src/Features/Agents/Domain/ExecutionRunOutcome.cs`
- matching existing Agents, Conversations, Townhall, and Shell tests

**Required behavior:** coordinator/router use `IAgentSessionService` for lazy
session creation, structured admission, lifecycle, capability, cancellation,
and terminal results. The existing conversation writer remains temporarily as
the compatibility projection. `ExecutionRunId` remains the correlation
identity. `IAgentPanelHost` remains unchanged.

**Gate:** Agents, Conversations, Townhall, Shell, registration/DI,
Architecture, full suite, and `git diff --check`.

**Rollback/commit:** one cutover commit with the legacy conversation projection
still active: `feat(agents): route execution through agent sessions`.

**M3b-1 status (2026-07-22):** Implemented in review at parent commit
`24f8b9a6562eaeb577e6320c0ce3a061deea73d6`. Coordinator/router now admit
through `IAgentSessionService` with session-owned `ExecutionRunId` correlation,
typed terminal mapping, structured rejection, and temporary
`AgentPanelDirectConversationWriter` projection. Human acceptance pending.
M3b-2 remains unauthorized.

### M3b-2 — Event-to-conversation projection and parity

**Tests first:** exact one-event-to-one-entry mapping, duplicate-event
idempotency, ordering, private conversation ownership, exact existing Townhall
strings, success/failure/cancellation/timeout/rejection display, and
no-auto-resume in:

- `tests/Zaide.Tests/Features/Agents/Application/AgentConversationEventProjectionTests.cs`
- `tests/Zaide.Tests/Features/Townhall/Presentation/Phase15TownhallParityTests.cs`

**Planned production paths:**

- new `src/Features/Agents/Application/AgentConversationEventProjection.cs`
- existing `src/Features/Agents/Application/AgentPanelDirectConversationWriter.cs`
- existing `src/Features/Agents/Application/AgentExecutionCoordinator.cs`
- existing `src/Features/Agents/Application/AgentRouter.cs`
- existing `src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs`

`src/Features/Townhall/Presentation/TownhallViewModel.cs` is not modified. It
continues to project authoritative `IConversationStore` entries.

**Required behavior:** current conversation entries remain authoritative UI
records; Phase 14 privacy/drafts/unread/persistence remain unchanged; no public
DM mirror; no auto-resume; no duplicate entry per backend event. Remove the
legacy direct writer only after the new projection passes the same commit's
parity gate. Retain `IAgentPanelHost`.

**Manual evidence:** configured endpoint smoke may be recorded as not run when
credentials are unavailable; automated fake-backend parity is mandatory. GUI
smoke must cover channel send, Agent DM send, failure, navigation during an
in-flight run, and privacy.

**Gate:** Conversations, Agents, Townhall, Shell, Architecture, DI, full suite,
manual evidence, and `git diff --check`.

**Rollback/commit:** one projection/parity commit after accepted M3b-1:
`feat(agents): project session events into conversations`.

### M4 — Closeout and architecture truth-sync

**Scope:** delete only proven dead compatibility types; close or explicitly
defer every open M0 question; refresh architecture/DI/type/source baselines;
record capability matrix and manual evidence; update this plan, V3 roadmap,
architecture overview, root README, and phase index.

**Non-goals:** Native Harness, ACP, tools, permissions, resume, Phase 16.

**Gate:** build; all focused suites; Architecture; full suite; public/source/
registration counts; `git diff --check`; provenance/evidence review; no open
Phase 15 TOFIX.

**Rollback/commit:** docs/ratchet closeout only after accepted M3b-2 behavior.
Expected commit: `docs(phase-15): close backend-neutral session foundation`.

---

## Verification commands

Run from the repository root. Record passed/failed/skipped totals and build
warning/error counts at every implementation milestone.

```bash
git status --short --branch
git rev-parse HEAD
git rev-parse origin/master

dotnet build Zaide.slnx

dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.Features.Conversations'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.Features.Townhall'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.App.Shell'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~RegistrationModuleTests|FullyQualifiedName~CompositionDiIntegrationTests'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.Architecture'

dotnet test Zaide.slnx --no-build
git diff --check
```

Architecture/type/source/registration baseline checks:

```bash
rg 'services\.AddSingleton' src/App/Composition/Registration | wc -l
rg 'services\.AddScoped|services\.AddTransient' src/App/Composition/Registration
find src/App/Composition/Registration -maxdepth 1 -name '*.cs' | wc -l
wc -l tests/Zaide.Tests/Architecture/PublicProductionTypeBaseline.txt
```

Do not substitute filtered tests for the full-suite exit gate.

### M0 verification record (2026-07-21)

| Command/check | Result |
|---------------|--------|
| `dotnet build Zaide.slnx` | Succeeded; 0 errors; 4 pre-existing warnings |
| Conversations focused tests | 102 passed, 0 failed, 0 skipped |
| Agents focused tests | 178 passed, 0 failed, 0 skipped |
| Townhall focused tests | 117 passed, 0 failed, 0 skipped |
| App Shell focused tests | 134 passed, 0 failed, 0 skipped |
| Registration/DI focused tests | 67 passed, 0 failed, 0 skipped |
| Architecture tests | 26 passed, 0 failed, 0 skipped |
| `dotnet test Zaide.slnx --no-build` | 2524 passed, 0 failed, 0 skipped |
| `git diff --check` before M0 docs | Clean |
| `git diff --check` after M0 docs | Clean |
| Architecture visibility | 337 public / 126 internal / 463 total |
| Tracked production C# | 426 total: App 41 / UI 4 / Features 381 |
| Production DI | 73 singleton calls / 0 scoped / 0 transient; 12 registration modules |

---

## Entry conditions

### For human acceptance of M0

- [x] Phase 14 accepted and closed.
- [x] Live architecture and behavior audit completed at `57d8c44`.
- [x] Phase 15 outcome selected by dependency order.
- [x] Native Harness/ACP independence preserved.
- [x] Research refs, licenses, provenance policy, and evaluation protocol locked.
- [x] At least three independently verifiable post-M0 milestones defined.
- [x] Full build/test/architecture baselines recorded.
- [x] Human accepted this M0 plan on 2026-07-21.

### For authorizing M1a

- [x] M0 accepted by human on 2026-07-21.
- [ ] Working tree is clean at the accepted M0 boundary.
- [ ] Human explicitly authorizes **M1a only**.

No later milestone may infer authorization from M0 or M1a acceptance.

---

## Phase exit conditions

- [ ] Backend-neutral Agent Session identity/lifecycle exists with a concrete
      in-memory owner and valid transition tests.
- [ ] Existing run identity is retained and correlated through ordered
      structured events to conversation entries.
- [ ] Capability snapshot/versioning is truthful and does not collapse
      advertised, available, configured, permitted, degraded, and usable state.
- [ ] Current HTTP execution works through the compatibility backend with exact
      behavior/privacy parity.
- [ ] Cancellation, failure, timeout/rejection, and unavailable capabilities
      are represented at the evidence level actually observed.
- [ ] Restart never auto-resumes execution; conversation schema v1 remains
      compatible.
- [ ] No Native Harness, ACP, tool, permission, workspace mutation, raw trace,
      usage/cost, or session persistence implementation leaked into scope.
- [ ] Research/provenance artifacts are complete and no upstream material was
      adopted without a reviewed record.
- [ ] Build, focused tests, Architecture, full suite, manual evidence, type/
      source/registration counts, and `git diff --check` are green and exact.
- [ ] Docs/status surfaces match live code.
- [ ] Human explicitly accepts Phase 15 closeout.

---

## Limitations by design

- Phase 15's only concrete backend is the current non-streaming HTTP
  compatibility path.
- Agent Sessions and live runs are in-memory and non-resumable.
- No backend process lifecycle exists because the compatibility backend is HTTP.
- No streaming token events, tools, permissions, workspace mutation, usage,
  cost, attachments, raw trace, or provider registry.
- Capability rows may truthfully be unavailable; this is not degraded product
  placement for future backends.
- Conversation persistence remains schema v1 and does not become an audit log.
- Research candidate versions are a 2026-07-21 snapshot and must be re-verified
  before later use.
- Top-level candidate licenses do not complete transitive license review.

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Contract overfits the current HTTP path | Research first; backend-neutral IDs/events; preserve independent Native/ACP paths; require capability honesty |
| Contract becomes speculative platform work | Only payloads with a Phase 15 producer; tools/permissions/usage/trace deferred |
| Session is confused with conversation/panel/provider | Locked cardinality and identity tests |
| Cancellation remains message-text inference | M3a focused classification tests; explicit evidence distinction |
| Event projection duplicates conversation entries | Monotonic sequence/idempotency tests and one projection owner |
| Phase 14 privacy/recovery regresses | M3b Townhall/Conversations parity plus manual privacy/no-auto-resume smoke |
| Upstream code enters without provenance | M1a registry and hard adoption stop |
| Benchmarks overfit Zaide or one model | controlled manifests, held-out tasks/repos, repeated trials, observational labels |
| Public surface grows unnecessarily | internal-by-default and same-milestone architecture ratchet review |

---

## Rollback plan

- **M0 baseline:** `57d8c44bfa3f4948a400f0aa25ebbe6c9accaf36`.
- M0 rollback deletes this plan and reverts only the minimal roadmap/status
  definition; it touches no product or test code.
- Prefer one commit per milestone or milestone slice. Revert only the current
  unaccepted milestone to restore the last accepted boundary.
- M1b/M2 are additive until M3b-2 proves parity. Do not delete
  `IAgentExecutionService` or its current coordinator path early.
- M3a keeps the adapter behind the legacy path so it can be removed without
  affecting current execution.
- M3b-2 deletes the superseded direct writer only in the same commit that proves
  event-projection parity after M3b-1 is accepted.
- If Phase 14 privacy, persistence, or UI parity fails, revert the Phase 15
  cutover and keep the accepted session foundation unused behind the current
  path; do not patch around a broken ownership model.
- A structural phase rollback requires
  `docs/phases/v3/phase-15/REVERT_LOG.md` per `docs-rules.md`.

---

## Exact next step

1. M3b-1 is accepted (commit `29f66247`, 2026-07-22).
2. M3b-2 is the next eligible slice but remains unauthorized until explicit human authorization.
3. Phase 16, Native Harness production, and ACP have not started.

---

*Last updated: 2026-07-22 (M3b-1 accepted at 29f66247; M3b-2 unauthorized)*
