# Phase 19: Zaide Native Harness Backend — Implementation Plan

## Status and authorization

**Status:** M0 accepted by the user on 2026-07-27. M1 research/provenance is
complete with limitation (the full-corpus benchmark gate was retired by explicit
user-directed plan amendment on 2026-07-27; the research/provenance gate is
satisfied by retained evidence). **M2 harness contracts and architecture lock is
complete** (read-only audit gate). **M3 tool-calling execution loop is complete**
(read-only audit gate; M4 not started).

**Authorized work:** M0 acceptance locks this implementation plan. It does not
authorize external source acquisition, candidate execution, credentials,
network egress, paid API use, production code, or production tests. M1 external
activity required separate explicit authorization under docs-rules §11 and the
M1 stop-and-ask checkpoint; that authorization was granted and is closed.

**Plan amendment — M1 benchmark gate retired (2026-07-27):** The original M1
requirement that at least two candidates complete the entire common corpus is
retired by explicit user direction. The local model capability is insufficient
for this campaign to produce meaningful architectural evidence. M1 closes as a
research/provenance milestone with an explicit comparative-execution limitation.
The retained evidence satisfies the research/provenance gate; no architecture
winner was selected. M2 is next but has not started. Do not perform another
full-corpus chase.

**Prior phase:** Phase 18 (Live IDE Context for Agent Runs) is complete and
closed. M0–M6 passed with full verification on 2026-07-27. The Phase 18
shipped-inert boundary (context assembled and attached but not consumed by a
production backend) is intact and structurally enforced. Phase 19 is the first
phase authorized to break that inert boundary by design.

**Planning baseline:**

| Check | Verified result |
|-------|-----------------|
| Branch | `master` |
| `HEAD` | `8eed91d3` |
| Working tree before plan creation | Clean (`git status --porcelain` empty) |
| Phase 18 dependency | Complete, accepted, and closed (2026-07-27) |
| Phase 17 dependency | Complete, accepted, and closed (2026-07-26); ships a complete but backend-gated control plane |
| Phase 15 dependency | Complete and closed (backend-neutral session/run/event foundation) |
| Phase 16 dependency | Parked historical evaluation; not a production prerequisite. Qwen observational path reverted. |
| Build baseline | `dotnet build Zaide.slnx --no-restore` — succeeded, 0 errors, 0 warnings (Phase 18 M6 closeout) |
| Architecture tests | 37 passed with substring filter `FullyQualifiedName~Architecture` (Phase 18 M6 closeout) |
| Full fast suite | 3194/3194 passed (Phase 18 M6 closeout) |
| Serial fallback | 3194/3194 passed (Phase 18 M6 closeout) |
| M0 acceptance | Accepted by the user on 2026-07-27 |
| Verification date | 2026-07-27 |

---

## Pre-implementation verification (M0)

- [x] Read `AGENTS.md`, `docs-rules.md` §3 (Feature Phase Planning) and §13
      (Hard Rules), `docs/CONVENTIONS.md`, `docs/DESIGN.md`,
      `docs/roadmap/V3.md` (§3 principles, §7 backend-neutral experience, §8
      Zaide Native Harness, §17 target source direction, §18 feature sequence,
      §19 discovery questions), and the accepted Phase 15, 17, and 18 plans,
      TOFIXs, and closeout evidence.
- [x] Verify the live checkout: branch `master`, `HEAD` at `8eed91d3`, clean
      working tree.
- [x] Audit the Phase 15 backend-neutral seams: `IAgentBackend`,
      `AgentBackendRequest`, `AgentBackendExecutionContext`, `AgentBackendEvent`,
      `AgentBackendId`/`AgentBackendIds`, `AgentCapabilitySnapshot`,
      `AgentCapabilityId`, `AgentEventKind`, `IAgentSessionService`, and the
      `AgentConversationEventProjection` owner.
- [x] Audit the Phase 17 action control plane: `IAgentActionRequestCapableBackend`
      marker interface, `IAgentActionBroker` / `ContractAgentActionBroker` /
      `UnavailableAgentActionBroker`, `IAgentActionBrokerFactory`,
      `AgentActionKind` (ReadFile, CreateFile, ReplaceFile, DeleteFile,
      ExecuteCommand), `AgentActionPayload` hierarchy, `AgentActionResult` /
      `AgentActionResultKind` / `AgentActionFailureKind`, `AgentPermissionDecision`
      / `AgentActionPermissionClassification`, `IAgentPermissionReviewService`,
      `IAgentActionAuditStore`, `IAgentFileReader` / `IAgentFileMutator` /
      `IAgentCommandResolver` / `IAgentCommandExecutor` / `IAgentDocumentReconciler`,
      and the `WorkspaceActionAuthority` scope-capture surface.
- [x] Audit the Phase 18 context boundary: `AgentContextManifest`,
      `AgentContextItem`, `AgentContextSourceId`, `AgentContextPolicyLevel` /
      `AgentSessionContextPolicyLevel`, `IAgentContextSessionPolicyService` /
      `AgentContextSessionPolicyState`, `AgentContextHardExclusionRegistry`,
      `AgentContextRedactionState` (fail-closed), and the manifest attachment
      point (`AgentBackendRequest.ContextManifest` nullable property;
      `AgentBackendExecutionContext.ContextManifest` computed property).
- [x] Confirm the shipped-inert boundary: `AgentSessionService.CreateExecutionContextLocked`
      routes non-action-capable backends to `UnavailableAgentActionBroker`;
      action-capable backends receive a `ContractAgentActionBroker`. The only
      production-registered `IAgentBackend` (`LegacyOpenAiCompatibleAgentBackend`)
      does not implement `IAgentActionRequestCapableBackend`, so every production
      run resolves `UnavailableAgentActionBroker` and no action capability is
      reachable. The legacy backend never reads `AgentContextManifest`.
- [x] Confirm the legacy HTTP path: `LegacyOpenAiCompatibleAgentBackend`
      delegates to `AgentExecutionService.ExecuteAsync(MessageText, ct)`, which
      constructs a single `{ role = "user", content = userMessage }` array. No
      system role, no history, no tools, no streaming.
- [x] Audit the composition root: `AddZaideAgents` registers 21 Singleton
      services. The only `IAgentBackend` registration is
      `LegacyOpenAiCompatibleAgentBackend`. `IAgentActionBrokerFactory`,
      `IAgentActionAuditStore`, `IAgentFileReader`/`IAgentFileMutator`,
      `IAgentCommandResolver`/`IAgentCommandExecutor`,
      `IAgentPermissionReviewService`, and `IAgentDocumentReconciler` are wired
      but inert.
- [x] Audit the architecture baselines: 646 total / 350 public / 296 internal
      top-level production types; 585 source files / 540 Features / 41 App / 4
      UI; 2 locator sites; 2 legacy allowlist FindingIds. Bypass ratchets in
      `Phase17BypassRatchetTests` and `Phase18ContextBypassRatchetTests`.
- [x] Confirm Phase 16 is parked: evaluation infra in
      `tools/Phase16NativeHarnessEvaluation/` and evidence docs in
      `docs/phases/v3/phase-16/evaluation/` remain as historical reference. The
      Qwen observational qualification path was reverted. Phase 16 is not a
      production prerequisite and its reverted artifacts must not be adopted.
- [x] Lock the phase scope, milestone dependency order, verification commands,
      stop conditions, rollback boundaries, and open decisions owned by later
      milestones.
- [x] Run `dotnet build Zaide.slnx --no-restore` — succeeded, 0 errors,
      0 warnings.
- [x] Run architecture tests — 37 passed with substring filter
      `FullyQualifiedName~Architecture`.
- [x] Run `git diff --check` — clean.
- [x] M0 reviewed and accepted by the user on 2026-07-27.

No new library is required or authorized by M0. Any later dependency proposal
must include a focused proof, compatibility evidence, a license/provenance
check, and an amendment to this plan before adoption (P19-D13).

---

## Accepted implementation decisions

### P19-D01: Feature ownership

Native Harness production types live under `src/Features/Agents/` following the
existing feature-first structure established by Phase 15/17/18. The harness is an
Agents-owned concern. No new top-level feature folder is created.

V3 §17 notes Native Harness and ACP are "strong later candidates for separate
assemblies" because their process, protocol, license, security, and replacement
boundaries are meaningful. Phase 19 does **not** split the assembly: the current
production tree has one project (`src/Zaide.csproj`) and no refactor authorizes a
split. Assembly separation, if justified, belongs to a later structural refactor
with its own M0 — not Phase 19.

### P19-D02: Dependency direction and contract-change boundary

The Native Harness consumes Phase 15 session/event contracts
(`IAgentBackend`, `AgentBackendEvent`, `AgentBackendRequest`,
`AgentBackendExecutionContext`, `AgentEventStream`, `AgentEvent`,
`AgentConversationEventProjection`), Phase 17 action contracts
(`IAgentActionRequestCapableBackend`, `IAgentActionBroker`, `AgentActionPayload`
hierarchy, `AgentActionResult`, `AgentPermissionDecision`,
`IAgentActionEventPublisher`, `AgentActionFactPayload`), and Phase 18 context
contracts (`AgentContextManifest`, `AgentContextItem`). It reads from
Infrastructure (HTTP client, process surfaces) through existing contracts or
Agent-owned infrastructure. It does not depend on any feature's Presentation
layer.

**Contract-change boundary:** Phase 19 does **not** modify Phase 15/17/18
contract signatures, existing `AgentBackendEventKind` values
(`MessageCompleted`, `FailureObserved`), or existing `AgentEventKind` values.
The harness consumes the existing event surfaces:

- Backend text completion and failure flow through `AgentBackendEvent`
  (`MessageCompleted` / `FailureObserved`), observed by
  `AgentSessionService.ObserveBackendAsync`.
- Tool/action activity flows through the **broker event path**, not the backend
  event stream: `IAgentActionBroker.RequestAsync` → `ContractAgentActionBroker`
  → `RunScopedAgentActionEventPublisher` → normalized `AgentEvent` with
  `AgentActionFactPayload` (`ActionRequested`, `ActionPermissionClassified`,
  `ActionPermissionDecided`, `ActionExecutionStarted`, `ActionResultReported`,
  `ActionReconciliationReported`, `ActionRevoked`) → `AgentEventStream` →
  `AgentConversationEventProjection`, which already projects
  `ActionResultReported` into conversation entries.

If M2 design concludes that richer tool-activity rendering in Townhall requires
new `AgentEventKind` values or new `AgentEventPayload` subtypes, that is a
**bounded contract extension** owned and authorized by M2 — not a silent
modification of Phase 15/17/18 contracts. Any extension must preserve existing
kind/payload semantics, add only additive values, and update the architecture
ratchets in the same reviewed change. The legacy backend and
`UnavailableAgentActionBroker` paths must remain unaffected.

### P19-D03: Backend-neutral design

The Native Harness is one `IAgentBackend` implementation. It does not become a
privileged path or a wrapper around ACP. ACP (Phase 20) remains an independent
sibling backend. The harness must not create abstractions that force ACP into a
dishonest lowest common denominator (V3 §7). Where the harness needs a
backend-specific extension, it must be namespaced and preserved rather than
collapsed into the neutral contract.

### P19-D04: Action plane activation

The Native Harness implements `IAgentActionRequestCapableBackend`.
`AgentSessionService.CreateExecutionContextLocked` already routes
action-capable backends to `ContractAgentActionBroker`. The harness consumes
`AgentBackendExecutionContext.Actions` (the run-scoped `IAgentActionBroker`) for
all file and command operations. It must **not** bypass the broker with direct
file, process, or workspace access. Every tool action flows through
`IAgentActionBroker.RequestAsync` with the appropriate `AgentActionPayload`
(`AgentReadFileActionPayload`, `AgentCreateFileActionPayload`,
`AgentReplaceFileActionPayload`, `AgentDeleteFileActionPayload`,
`AgentExecuteCommandActionPayload`).

This is the first production backend that activates the Phase 17 control plane.
The shipped-inert boundary (Phase 17) is broken here — by design and within the
trust boundary Phase 17 established.

### P19-D05: Context manifest consumption

The Native Harness reads `AgentBackendExecutionContext.Request.ContextManifest`
(assembled by Phase 18's `AgentContextManifestBuilder`) and embeds IDE context
into the model prompt or tool-result context. This is the first production
consumer of the Phase 18 manifest. Phase 18's shipped-inert boundary (assembled
but not consumed) is broken here — by design.

The harness must respect Phase 18's hard exclusions, redaction state
(fail-closed: `ProcessingFailed` drops content), and token budget. It must not
re-read IDE state outside the manifest.

### P19-D06: Honest capability reporting

The Native Harness `AgentCapabilitySnapshot` must truthfully report each
capability row using the **six-fact** `AgentCapabilityState` model
(`Advertised`, `Available`, `Configured`, `Permitted`, `Degraded`,
`CurrentlyUsable`), where each fact is one of `Unknown`, `Unavailable`,
`Supported`, `NotSupported` (see `src/Features/Agents/Domain/
AgentCapabilityState.cs`). The plan's earlier draft collapsed these into a
single "Available" value — that was incorrect and is corrected here.

M2 must define exact rows and versioned transitions
(`AgentCapabilitySnapshot.WithRow`, which requires `version > Version`) for at
least: `Tools`, `Permissions`, `IdeContext`, `Streaming`, `Cancellation`, and
`MessageCompletion`. Each row's six facts must reflect:

- **Advertised** — statically declared by the backend identity.
- **Available** — present in this session (e.g., workspace captured for
  `Tools`; provider configured for `Streaming`).
- **Configured** — provider/workspace configuration satisfies the capability
  (e.g., model endpoint set; workspace scope captured).
- **Permitted** — current permission policy grants the capability (e.g.,
  `RequiresUserDecision` not pending denial for `Tools`).
- **Degraded** — a temporary failure reduced usability (e.g., provider
  transport retrying, workspace scope invalidated).
- **CurrentlyUsable** — the conjunction the UI should present as "usable now."

A capability claim is not permission, and a reported event is not proof that
the action matches workspace reality. Evidence levels (V3 §7) apply:

| Evidence level | Harness behavior |
|----------------|-------------------|
| Zaide-executed | File reads, file mutations, command execution through `IAgentActionBroker` — Zaide enforces policy |
| Zaide-mediated | Tool-call requests the model makes, routed through the broker |
| Backend-executed and reported | Model text generation, reasoning — Zaide receives a claim but did not execute the model call |
| Externally observed | Not applicable in Phase 19 (no external agent process) |
| Unobservable | Model internal state, provider-side logging |

UI, audit, and trust language must retain the distinction between advertised,
available, configured, permitted, and currently-usable (V3 §7; Phase 15
capability design).

### P19-D07: Research obligations (V3 §8.1)

Before production harness architecture is locked (M2), Phase 19 must:

- inventory several relevant open-source harnesses at exact versions/commits;
- verify their licenses and transitive obligations;
- trace their task loop, context selection, file search, editing, tool
  execution, failure recovery, and compaction behavior;
- run comparable repository tasks rather than judging architecture only from
  source appearance;
- distinguish copied code, modified code, translated code, and ideas only;
- record rejected approaches and the evidence for rejection;
- avoid benchmark overfitting by using varied task classes and held-out tasks.

For the M1 gate, "several" means at least three candidates inventoried at exact
commits. Comparable runtime evidence must cover at least two runnable candidates
attempting executable repository tasks with exact commands, reset/isolation
method, results, failures, and resource limits recorded. A candidate that cannot
be acquired or executed may remain in the source/license inventory with its
blocker recorded, but it does not satisfy the two-candidate runtime comparison.
Failed comparative execution is retained as research evidence, not treated as a
candidate-selection winner or benchmark result.

**Amendment (2026-07-27):** The original full-corpus benchmark gate
(≥2 candidates completing all 8 common tasks green) is retired by explicit
user-directed plan amendment. The local model capability is insufficient for
meaningful architectural evidence from this campaign. M1 closes as a
research/provenance milestone. The retained evidence satisfies the
research/provenance gate: ≥3 candidates inventoried at exact commits with
licenses, notices, and provenance recorded; ≥2 runnable candidates with
comparable corpus attempts recorded (exact commands, reset/isolation method,
results, failures, resource limits); task-loop, context, search, editing, tool
execution, recovery, and compaction observations recorded. No production code,
tests, tools, dependencies, or architecture decisions are introduced. The
full-corpus failures remain visible as a documented limitation of the local
model/execution environment. No architecture winner was selected. M1 runtime
observations are informative research evidence only and do not select a winning
external architecture.

This research is a prerequisite milestone (M1), not optional. It informs the
architecture locked at M2. The Phase 16 evaluation methodology
(`docs/phases/v3/phase-16/evaluation/` — campaign lock, threat model, task
corpus, isolation evidence, runner contract) may inform M1 research design
without adopting the reverted Qwen candidate path (P19-D09).

### P19-D08: Open-source provenance (V3 §8.3)

Any copied or adapted code must carry a provenance record with:

- upstream project and repository URL;
- exact commit or release;
- source file and copied range or component;
- upstream license and compatibility decision;
- required copyright and NOTICE text;
- local modifications;
- date introduced and responsible Zaide component;
- update/security tracking decision.

The record must cover more than copied C# source: dependencies, vendored or
modified source, binaries, generated/translated code, assets, prompts,
documentation, benchmark fixtures, and test corpora may all carry obligations.
License verification happens **before** code is copied into production. V3 may
reuse good implementation work; it may not erase its origin.

### P19-D09: No Phase 16 artifact adoption

Phase 19 does not adopt reverted Phase 16 artifacts. The Qwen observational
qualification path was reverted (see `docs/phases/v3/phase-16/REVERT_LOG.md`).
Phase 16 evaluation infra in `tools/Phase16NativeHarnessEvaluation/` and
evidence docs in `docs/phases/v3/phase-16/evaluation/` remain as historical
reference for methodology only. Phase 19 may build its own evaluation evidence
but must not reuse the reverted candidate qualification path, the reverted
candidate fixtures, or the reverted M3 qualification harness.

### P19-D10: Run-scoped history and system prompt

Phase 19 implements two distinct history concerns, which must not be conflated:

1. **In-run model/tool loop history (in scope, M3):** Within one admitted run,
   the harness accumulates private in-memory turn records for model messages,
   tool-call requests, tool-result summaries, and assistant text. M2 locks the
   exact internal record types. These records are neither normalized
   `AgentEvent`s nor `ConversationEntry`s, and they are not written to
   `IConversationStore`. Broker dispatch independently emits normalized
   `AgentEvent` action facts through `RunScopedAgentActionEventPublisher`;
   terminal backend completion or failure remains an `AgentBackendEvent`.
   Additional normalized event projection requires the bounded extension
   process in P19-D02. The private turn records live for the run's lifetime only
   and are not persisted (P19-D11).

2. **Prior conversation replay across runs (open decision, owned by M2):** The
   `IConversationStore` exposes the entire conversation
   (`ListConversations`, `TryGet`, `EntryAppended`). A newly admitted run
   starts with only the current user `ConversationEntry`; prior user/assistant
   entries from earlier runs exist in the store. Whether Phase 19 replays prior
   conversation entries (with filtering, token budgeting, and redaction) into
   the model context — or assembles only the in-run loop — is an open decision
   owned by M2. M2 must specify the exact seam (read-only
   `IConversationStore` access through `IAgentSessionService` or a contract
   façade), the filtering rules (entry kinds, recency, token budget from Phase
   18's heuristic), and the no-persistence boundary.

**System prompt (in scope, M3):** The system prompt embeds Phase 18 context
manifest items (subject to hard exclusions and redaction fail-closed). It is
constructed per run, not persisted.

This decision reconciles the Phase 18 deferral (P18-D10 "No multi-turn history
replay" and P18-D11 limitations) without contradicting P19-D02: the harness
reads `IConversationStore` (if M2 authorizes cross-run replay) without
modifying it, and builds an in-memory loop without adding new conversation-store
writers.

### P19-D11: No persistence, memory, or resume

Phase 19 does not implement durable memory, raw trace storage, session resume,
cross-session state, or interrupted-run recovery. Run-scoped history is
in-memory for the run's lifetime only. Persistence and memory belong to Phase 21.
A run interrupted by application crash or backend disconnect must be presented
as terminal or indeterminate, not silently resumed (V3 §19 persistence
questions; Phase 14 M0 no-auto-resume rule).

### P19-D12: No ACP, public API, or Human-to-Human

Phase 19 does not implement ACP integration (Phase 20), a public agent API
(V3 non-goal), or Human-to-Human messaging (V3 non-goal). The participant model
must not hard-code every direct conversation as User-to-Agent in a way that
makes future Human-to-Human impossible (V3 §19 compatibility invariants), but
Phase 19 does not deliver Human-to-Human.

### P19-D13: New library proof required

No new NuGet dependency is authorized by M0. A production harness performing
tool-calling likely needs streaming SSE support and function-calling API
support that the legacy non-streaming `HttpClient` path does not provide. Any
proposal to adopt a model client library (e.g., OpenAI .NET SDK, a
multi-provider client, or a streaming HTTP extension) must include:

- a focused proof that the API works with the project's .NET / Avalonia stack;
- compatibility evidence against `Directory.Packages.props` central pins;
- a license and provenance check per P19-D08;
- an amendment to this plan.

The decision is owned by M2 (architecture lock) and may be informed by M1
research.

### P19-D14: Verification baseline

All verification runs in an interactive terminal using `dotnet test
Zaide.slnx --no-build`. If fast mode fails or hangs, reproduce with the serial
fallback before treating the result as a regression:
`dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings`.
Architecture inventory, visibility, and bypass ratchets are updated for each
production type addition. A filtered test command satisfies its gate only when
the output reports at least one discovered/passed test and zero failures.
`No test matches` or an invalid-filter diagnostic never satisfies a gate,
regardless of the process exit code.

### P19-D15: Production threat model before autonomous tool execution

V3 §15 requires an explicit threat model before autonomous tool execution.
The Native Harness is the first production backend that autonomously executes
file and command operations. M2 must lock a Phase 19 production threat model
**before M3 implementation begins**, covering at minimum the V3 §15 list:

- prompt injection from repository files, command output, diagnostics, and
  other model-tool loop content;
- accidental or malicious secret exfiltration through model prompts, tool
  calls, command output, or audit records;
- workspace trust on first open and workspace-boundary enforcement (Phase 17
  scope capture, symlink traversal, path traversal);
- command substitution and shell interpretation (Phase 17 denies shell
  executables; the threat model must record residual risk);
- denial of service through tokens, processes, events, files, or recursion
  (turn budget, output budgets, non-terminal-action slot, process-tree
  cleanup);
- provider transport security (TLS, credential handling, retry, timeout);
- cancellation correctness, late completion after cancellation, and
  process-tree cleanup;
- dependency and copied-code supply-chain risk (per P19-D08);
- Agent-to-Agent loops, runaway delegation, and deadlock (Phase 19 does not
  implement Agent-to-Agent routing, but the threat model must record the
  boundary).

The threat model is an M2 artifact at
`docs/phases/v3/phase-19/M2_THREAT_MODEL.md` and must be accepted before M3
starts. M6 adversarial tests must exercise the threats the model identifies.

### P19-D16: Network and transport boundary

Three distinct network concerns must not be conflated:

1. **Provider/model transport (in scope, M3):** HTTP traffic between the
   Native Harness and the model provider (e.g., OpenAI-compatible
   `/chat/completions` or function-calling endpoint). This is backend-owned
   infrastructure, not a tool action. It does **not** flow through
   `IAgentActionBroker` (the broker's five kinds are `ReadFile`, `CreateFile`,
   `ReplaceFile`, `DeleteFile`, `ExecuteCommand` — none cover model HTTP).
   The transport is subject to P19-D15 provider-transport threat modeling,
   credential handling, retry, and timeout. The existing `HttpClient`
   registration in `AddZaideAgents` (120s timeout, non-streaming) may require
   a streaming-capable client per the M2 library decision (P19-D13).

2. **Dedicated agent-requested network tools (out of scope):** A `FetchUrl`,
   `HttpRequest`, or browser tool the model can call directly. Phase 19 does
   not implement these. They are not in the Phase 17 `AgentActionKind` taxonomy
   and adding them would require a Phase 17 contract extension outside Phase
   19 scope.

3. **Network access by an approved command (in scope, inherited from Phase
   17):** Phase 17 explicitly states an approved executable can access
   resources permitted to the Zaide process, including paths outside the
   workspace or the network (Phase 17 plan §command environment). Phase 17 is
   process hygiene, not a sandbox. The permission surface must disclose that
   fact. Phase 19 does not weaken or strengthen this boundary; it inherits it.

The earlier draft's "no network fetch" limitation was overstated and is
corrected here: Phase 19 allows provider transport and inherited command
network access; it does not add dedicated agent-requested network tools.

---

## Scope

**Goal:** Ship a first-party Zaide Native Harness `IAgentBackend` that uses the
Phase 17 control plane for real repository work, consumes the Phase 18 IDE
context manifest, publishes structured tool activity through the existing
broker-event path into Townhall, and reports only capabilities Zaide can
verify — activating the shipped-inert action and context boundaries by design.

The Phase 19 trust boundary is:

```text
existing backend-neutral session/run/event surface (Phase 15)
  -> run-scoped IAgentActionBroker (Phase 17) for all file/command operations
  -> AgentContextManifest (Phase 18) consumed into system prompt / tool context
  -> Native Harness task loop (model turns, tool-call parsing, broker dispatch)
  -> AgentBackendEvent stream (MessageCompleted / FailureObserved only)
     observed by AgentSessionService.ObserveBackendAsync
  -> broker action facts: IAgentActionBroker -> ContractAgentActionBroker
     -> RunScopedAgentActionEventPublisher -> AgentEvent (ActionRequested /
        ActionPermissionClassified / ActionPermissionDecided /
        ActionExecutionStarted / ActionResultReported /
        ActionReconciliationReported / ActionRevoked) -> AgentEventStream
     -> AgentConversationEventProjection (already projects ActionResultReported)
  -> truthful AgentCapabilitySnapshot (six-fact rows per P19-D06)
  -> production threat model (P19-D15) gating M3
```

### In scope

- Open-source harness research and provenance record (M1) satisfying V3 §8.1
  and §8.3.
- Phase 19 production threat model (M2, `M2_THREAT_MODEL.md`) covering V3 §15
  before M3 begins.
- Native Harness `IAgentBackend` production implementation implementing
  `IAgentActionRequestCapableBackend`.
- Tool-calling execution loop: model turn management, tool-call parsing,
  `IAgentActionBroker.RequestAsync` dispatch for all five `AgentActionKind`
  values, tool-result formatting, failure recovery, and run-scoped
  cancellation.
- Run-scoped in-memory model/tool loop history within one admitted run
  (P19-D10 concern 1).
- Prior conversation replay decision and seam (P19-D10 concern 2, owned by
  M2).
- System prompt construction embedding Phase 18 context manifest items
  (deferred from Phase 18 P18-D11).
- Truthful `AgentCapabilitySnapshot` with six-fact rows (`Advertised`,
  `Available`, `Configured`, `Permitted`, `Degraded`, `CurrentlyUsable`) for
  `Tools`, `Permissions`, `IdeContext`, `Streaming`, `Cancellation`, and
  `MessageCompletion`.
- Provider/model transport (HTTP to the model provider, separate from the
  broker) per P19-D16.
- Production DI wiring in `AddZaideAgents` for the Native Harness backend.
- Architecture inventory, visibility, and bypass ratchet updates for new
  production types and the activated action plane.
- Closeout adversarial review exercising the M2 threat model: action bypass,
  context leak, capability overstatement, permission bypass, cancellation /
  process-cleanup correctness.

### Out of scope

- ACP integration or any external agent protocol backend (Phase 20).
- Durable memory, raw trace storage, session resume, cross-session state, or
  interrupted-run recovery (Phase 21).
- Raw model I/O or transport trace disclosure (Phase 21).
- A public agent API (V3 non-goal).
- Human-to-Human messaging (V3 non-goal).
- `Custom` context policy level (deferred from Phase 18 P18-D05).
- Agent-level or project-level context policy assignment (deferred from Phase 18
  P18-D06).
- A full comparative benchmark campaign against external harnesses (Phase 16's
  parked domain; Phase 19 closeout verifies the harness works on real repository
  work, not a comparative campaign).
- Adoption of reverted Phase 16 artifacts: the Qwen observational path, reverted
  M3 qualification harness, or reverted candidate fixtures (P19-D09).
- Backend selection UI or multi-backend routing configuration (if the backend
  selection model — see Open Decisions — defers selection to a later phase).
- Dedicated agent-requested network tools: `FetchUrl`, `HttpRequest`, browser,
  or MCP/tool-server integration in Phase 19 (P19-D16). Provider transport and
  inherited command network access remain in scope.
- Tools beyond the Phase 17 `AgentActionKind` taxonomy: no browser, no custom
  tool registration, no MCP/tool-server integration in Phase 19.
- Model-specific tokenization (Phase 18 heuristic token counting remains).
- Cross-workspace context synchronization.

---

## Verified live facts

### Backend and broker contracts (`src/Features/Agents/Contracts/`, `Domain/`)

| Concern | Live owner | Verified state |
|---------|------------|----------------|
| Backend interface | `IAgentBackend` | `AgentBackendId BackendId`, `string BackendVersion`, `AgentCapabilitySnapshot CapabilitySnapshot`, `IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(AgentBackendExecutionContext, CancellationToken)`. |
| Action-capable marker | `IAgentActionRequestCapableBackend : IAgentBackend` | Marker interface only, no members. Legacy backend does not implement it. |
| Action broker | `IAgentActionBroker` | `ValueTask<AgentActionResult> RequestAsync(AgentActionPayload payload, string? correlationKey, CancellationToken ct)`. |
| Broker factory | `IAgentActionBrokerFactory` | `CreateRunScopedBroker(AgentSessionId, ExecutionRunId, ConversationId, ActorId initiatingActorId, ActorId targetActorId, AgentBackendId, IAgentActionEventPublisher)`. |
| Real broker | `ContractAgentActionBroker` | Captures workspace scope at admission via `IWorkspaceActionAuthority.TryCaptureCurrentScope`; rejects with `NoWorkspace` when uncaptured. Constructor takes file reader/mutator, command resolver/executor, permission review, document reconciler, event publisher. Has `Revoke()`. |
| Inert broker | `UnavailableAgentActionBroker` | Every `RequestAsync` returns `Denied` / `BrokerUnavailable` with message "Action capability is unavailable for this backend." |
| Audit store | `IAgentActionAuditStore` | `Record(AgentActionAuditRecord)`, `GetRunSnapshot`/`GetCurrentLifetimeSnapshot` (bounded in-memory). |
| Action kinds | `AgentActionKind` | `ReadFile, CreateFile, ReplaceFile, DeleteFile, ExecuteCommand`. |
| Action payloads | `AgentActionPayload` hierarchy | `AgentReadFileActionPayload`, `AgentCreateFileActionPayload`, `AgentReplaceFileActionPayload`, `AgentDeleteFileActionPayload`, `AgentExecuteCommandActionPayload`. `MatchesKind` validates payload kind. |
| Action result | `AgentActionResult` / `AgentActionResultKind` | `Succeeded, Failed, Denied, Revoked, Conflict, Cancelled, Indeterminate, DuplicateReplay`. `Content?`, `Revision`, `ByteLength`, `CommandExecution?`. |
| Action failure | `AgentActionFailureKind` | 17 values incl. `NoWorkspace`, `BrokerUnavailable`, `PermissionDenied`, `StaleWorkspace`, `ConcurrentActionRejected`, `ExecutionFailed`. |
| Permission | `AgentPermissionDecision` | Bound to `AgentActionRequestFingerprint`. Atomic `TryConsume()` (Published→Consumed via `Interlocked.CompareExchange`). One decision authorizes one execution. |
| Permission classification | `AgentActionPermissionClassification` | `DeniedByPolicy, RequiresUserDecision, AllowedByLockedPolicy`. |
| Audit record | `AgentActionAuditRecord` | Bounded, auto-redacts `api_key=`/`password=`/`token=` to `[redacted]`, truncates UTF-8 to `StoredAuditSummaryMaxBytes`. |
| Fact payload | `AgentActionFactPayload : AgentEventPayload` | Typed payload for action/permission facts through the Phase 15 event stream. |

### Event-flow architecture (two separate paths)

Live code has **two distinct event paths** that must not be conflated:

| Path | Carrier | Kinds | Consumer |
|------|---------|-------|----------|
| Backend observation | `AgentBackendEvent` (`src/Features/Agents/Domain/AgentBackendEvent.cs`) | `MessageCompleted`, `FailureObserved` only. Payload types: `AgentBackendMessageCompletedPayload`, `AgentBackendFailurePayload`. | `AgentSessionService.ObserveBackendAsync` observes the `IAsyncEnumerable<AgentBackendEvent>` returned by `IAgentBackend.ExecuteAsync`. |
| Broker action facts | Normalized `AgentEvent` with `AgentActionFactPayload` | `ActionRequested`, `ActionPermissionClassified`, `ActionPermissionDecided`, `ActionExecutionStarted`, `ActionResultReported`, `ActionReconciliationReported`, `ActionRevoked`. | `IAgentActionBroker` → `ContractAgentActionBroker` → `RunScopedAgentActionEventPublisher.Publish` → `AgentEventStream.Publish` + `IAgentActionAuditStore.Record`. `AgentConversationEventProjection` subscribes to `AgentEventStream` and already projects `ActionResultReported` via `ProjectActionResultReported`. |

The backend observation stream carries **only** text completion and failure —
it does not carry tool activity. Tool/activity facts flow through the broker
as normalized `AgentEvent`s. This is why Phase 19 tool activity in Townhall
flows through the broker-event path, not a new `AgentBackendEvent` kind.

### Capability and event taxonomy

| Concern | Live owner | Verified state |
|---------|------------|----------------|
| Capability IDs | `AgentCapabilityId` | 11 values: `MessageCompletion`, `Attachments`, `Streaming`, `Cancellation`, `Tools`, `Permissions`, `Resume`, `Reconnect`, `UsageReporting`, `RawTrace`, `IdeContext`. All use `capability:` prefix. Legacy backend marks `Tools`, `Permissions`, `Attachments`, `Resume`, `Reconnect`, `UsageReporting`, `RawTrace`, `IdeContext` as `Unavailable`; `Streaming` as `NotSupported`. |
| Capability state (six facts) | `AgentCapabilityState` (`src/Features/Agents/Domain/AgentCapabilityState.cs`) | Six separate facts per row: `Advertised`, `Available`, `Configured`, `Permitted`, `Degraded`, `CurrentlyUsable`. Each fact is one of `AgentCapabilityFactValue`: `Unknown`, `Unavailable`, `Supported`, `NotSupported`. These must not be collapsed into one value. |
| Capability row | `AgentCapabilityRow` | `Create(AgentCapabilityId, AgentCapabilityState)`. One row per capability id per snapshot. |
| Capability snapshot | `AgentCapabilitySnapshot` | Immutable, versioned. `Version` must increase in `WithRow`. `CreateInitial(AgentBackendId, IEnumerable<AgentCapabilityRow>, int version = 1)`. `TryGetState(id, out state)`. |
| Event kinds | `AgentEventKind` | 27 values: session (4), run (10 incl. `RunCancellationRequested`, `RunDisconnected`, `RunIndeterminate`), message (2), backend (2: `FailureReported`, `CapabilitySnapshotChanged`), action (7: `ActionRequested`, `ActionPermissionClassified`, `ActionPermissionDecided`, `ActionExecutionStarted`, `ActionResultReported`, `ActionReconciliationReported`, `ActionRevoked`), context (1: `ContextDisclosed`). |

### Phase 18 context contracts (`src/Features/Agents/Domain/`, `Contracts/`)

| Concern | Live owner | Verified state |
|---------|------------|----------------|
| Manifest | `AgentContextManifest` | `internal sealed`. Constructor takes session/run/conversation IDs, `AgentContextPolicyLevel`, items, token budget, truncation/exclusion decisions, assembled-at UTC. |
| Context item | `AgentContextItem` | `internal sealed`. Source ID, content, scope, fingerprint, redaction state, token count, provenance. Fail-closed: `ProcessingFailed` forces `content = string.Empty`. |
| Source IDs | `AgentContextSourceId` | 12 values with `context-source:` prefix. |
| Policy level (internal) | `AgentContextPolicyLevel` | `Off, Minimal, Standard, Detailed`. Used by manifest/assembly pipeline. Application default = `Standard`. |
| Policy level (public) | `AgentSessionContextPolicyLevel` | `Off, Minimal, Standard, Detailed`. Used by session-override surface. |
| Session policy service | `IAgentContextSessionPolicyService` (public) | `GetPolicyState`, `TrySetSessionOverride`, `ClearSessionOverride`. Overrides affect subsequent admitted runs only. |
| Hard exclusions | `AgentContextHardExclusionRegistry` | 6 unconditionally excluded categories with `context-exclusion:` prefix. No escape hatch in Phase 18. |
| Redaction | `AgentContextRedactionState` | `None, Partial, Full, ProcessingFailed` (fail-closed). |
| Manifest attachment | `AgentBackendRequest.ContextManifest` | Nullable optional property. `AgentBackendExecutionContext.ContextManifest` is a computed property delegating to `Request.ContextManifest`. |

### Composition root (`src/App/Composition/`)

| Concern | Live owner | Verified state |
|---------|------------|----------------|
| Agent DI | `AddZaideAgents` | 21 Singleton registrations. Only `IAgentBackend` is `LegacyOpenAiCompatibleAgentBackend`. `IAgentActionBrokerFactory`, `IAgentActionAuditStore`, `IAgentFileReader`/`IAgentFileMutator`, `IAgentCommandResolver`/`IAgentCommandExecutor`, `IAgentPermissionReviewService`, `IAgentDocumentReconciler` all wired but inert. |
| Policy service resolution | `Program.ResolveAgentContextSessionPolicyService` | Casts `IAgentSessionService` to `IAgentContextSessionPolicyService` (safe: `AgentSessionService` implements both). |
| Execution coordinator | `Program.CreateAgentExecutionCoordinator` | Constructs `AgentExecutionCoordinator` with panel host, session service, conversation store, optional draft state. |

### AgentSessionService run lifecycle (`src/Features/Agents/Application/`)

| Concern | Method | Verified state |
|---------|--------|----------------|
| Run admission | `BeginAdmittedRunLocked` | Allocates `LiveRun`, emits `RunCreated`/`RunAccepted`/`UserMessageAdmitted`/`SessionRunning`/`RunRunning`, calls `AssembleContextManifestLocked`, constructs `AgentBackendRequest` with manifest, calls `CreateExecutionContextLocked`, emits `ContextDisclosed` if manifest non-null, starts `ObserveBackendAsync`. |
| Context assembly | `AssembleContextManifestLocked` | Returns null only if builder/sources null or `Build` throws. Off-policy produces a non-null empty manifest. On exception: emits `FailureReported` with reason "IDE context assembly failed." (no raw snapshot/exception detail). |
| Execution context | `CreateExecutionContextLocked` | Non-action-capable backend OR null factory/audit → `UnavailableAgentActionBroker`. Action-capable → `ContractAgentActionBroker` via factory, assigns `run.ActionBroker`. |
| Broker revocation | `RevokeRunBrokerLocked` | Calls `run?.ActionBroker?.Revoke()`, nulls `run.ActionBroker`. |
| Workspace invalidation | `OnWorkspaceScopeInvalidated` | Iterates sessions, revokes active run brokers on workspace scope invalidation. |

### Legacy backend (`src/Features/Agents/Infrastructure/`)

| Concern | Live owner | Verified state |
|---------|------------|----------------|
| Backend | `LegacyOpenAiCompatibleAgentBackend` | `internal sealed : IAgentBackend` (only `IAgentBackend`; not action-capable). `BackendIdValue = AgentBackendIds.LegacyOpenAiCompatibleValue`. Delegates to `_executionService.ExecuteAsync(request.MessageText, ct)`. |
| HTTP body | `AgentExecutionService.ExecuteCoreAsync` | Single `{ role = "user", content = userMessage }` array. No system, no history, no tools, no streaming. |
| Context inertness | Ratchet-enforced | `Phase18ContextBypassRatchetTests.ContextManifest_DoesNotLeakToLegacyBackend` and `ContextIntegration_DoesNotLeakToLegacyBackend` scan the source for `ContextManifest`/`AgentContextManifest`/`AgentContextItem`/`AgentContextDisclosurePayload` and assert none present. |

### Architecture baselines (`tests/Zaide.Tests/Architecture/`)

| Metric | Baseline | Source |
|--------|----------|--------|
| Total top-level production types | 682 | `ArchitectureInventoryReader.M0TotalTopLevelTypes` |
| Public top-level types | 350 | `PublicProductionTypeBaseline.PublicTopLevelTypes` |
| Internal top-level types | 332 | `PublicProductionTypeBaseline.InternalTopLevelTypes` |
| Total source files | 621 | `ArchitectureInventoryTests` |
| `Features` files | 576 | `ArchitectureInventoryTests` |
| `App` files | 41 | `ArchitectureInventoryTests` |
| `UI` files | 4 | `ArchitectureInventoryTests` |
| Legacy allowlist FindingIds | 2 (0 namespace + 2 locator + 0 root) | `ArchitectureRatchetTests` / `ArchitectureVisibilityTests` |
| Locator sites | 2 (`Program.cs`, `App.axaml.cs`) | `ArchitectureRatchetTests` |

**Pre-M2 baseline (historical):** 646 total / 296 internal / 585 source files / 540 Features files.

**Published M2 baseline (historical):** 667 total / 350 public / 317 internal / 606 source files / 561 Features files.

### Bypass ratchets

| Ratchet | Enforces |
|---------|----------|
| `Phase17BypassRatchetTests` | Backends do not reference editor file IO or workflow runners; Application does not use forbidden BCL or service location; Application does not reference concrete Presentation or cross-feature Infrastructure; control plane does not write conversation store outside projection; `FakeActionRequesterBackend` is test-only and not production-registered. |
| `Phase18ContextBypassRatchetTests` | Context assembly does not bypass policy boundary; assembly service requires policy-matrix registration; manifest does not leak to legacy backend; context assembly does not reference concrete Presentation or cross-feature Infrastructure. |

### Shipped-inert boundary (intact)

The shipped-inert boundary is intact and structurally enforced. To activate the
action plane for real tool use, Phase 19 must:

1. introduce a production `IAgentBackend` implementing
   `IAgentActionRequestCapableBackend` that consumes
   `AgentBackendExecutionContext.Actions` (the run-scoped `IAgentActionBroker`)
   and `context.Request.ContextManifest` for tool-call orchestration;
2. register that backend in `AddZaideAgents` (replacing or supplementing the
   legacy registration);
3. ensure the harness's tool calls flow through `IAgentActionBroker.RequestAsync`
   → `ContractAgentActionBroker` → `RunScopedAgentActionEventPublisher` →
   `AgentEvent` (`ActionResultReported` etc.) → `AgentEventStream` →
   `AgentConversationEventProjection` (which already projects
   `ActionResultReported`). Backend text/failure continues to flow through
   `AgentBackendEvent` (`MessageCompleted` / `FailureObserved`);
4. update the architecture baselines and ratchet expectations for any new
   backend file. The post-M3 baseline is 682 total types, 350 public,
   332 internal, 621 source files, 576 Features files — future M4 activation
   ratchets from this baseline. (Published M2 baseline: 667 total, 350 public,
   317 internal, 606 source files, 561 Features. Pre-M2 reference values for
   historical rollback: 646 total, 350 public, 296 internal, 585 source files,
   540 Features.)

---

## Open decisions (owned by later milestones)

These decisions are named at M0 but not resolved. Each is owned by the milestone
that has the evidence to resolve it.

| Decision | Owner | Notes |
|----------|-------|-------|
| Evaluation scope at closeout: which real-repository tasks verify the harness without a full comparative campaign | M6 | Phase 16 methodology may inform; reverted Qwen path must not be adopted (P19-D09). |

**M2-resolved decisions:** Backend selection model (Native Harness replaces legacy in M4), model provider and protocol (OpenAI-compatible `/chat/completions` with tools/function-calling), streaming (SSE locked as M3 implementation contract), library (no new NuGet; use existing `HttpClient` extended for SSE), tool-calling protocol format (OpenAI tools/function-calling JSON), turn budget and termination (25 turns default, cooperative cancellation), prior conversation replay (bounded read-only via `INativeHarnessPriorConversationReader`), and townhall event surface (reuse existing broker-event path). See `M2_ARCHITECTURE_LOCK.md` §2.

---

## Milestones

| Milestone | Description | Depends on | Test gate |
|-----------|-------------|------------|-----------|
| M0 | Planning gate: verify seams, lock scope, research/provenance obligations, threat-model requirement, milestone order, verification commands, rollback | — | `git diff --check` clean; `dotnet build Zaide.slnx --no-restore` succeeds; `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"` passes (37/37 baseline); this document reviewed and accepted by user |
| M1 | Open-source harness research and provenance. Produce `docs/phases/v3/phase-19/M1_RESEARCH_RECORD.md` and `docs/phases/v3/phase-19/M1_PROVENANCE.md`. **Amendment (2026-07-27):** The original full-corpus benchmark gate is retired by explicit user-directed plan amendment. M1 closes as a research/provenance gate with an explicit comparative-execution limitation. **Stop-and-ask checkpoint:** external source acquisition, candidate execution, credentials, network egress, or paid API use requires explicit user authorization before that activity begins (docs-rules §11). | M0 | `M1_RESEARCH_RECORD.md` inventories ≥3 candidates at exact commits with licenses verified; ≥2 candidates verified runnable through the authorized zero-cost local path; comparable corpus attempts recorded with exact commands, reset/isolation method, results, failures, and resource limits; task-loop, context, search, editing, tool execution, recovery, and compaction observations recorded; `M1_PROVENANCE.md` complete for any code considered for reuse; no production code changed; `dotnet build Zaide.slnx --no-restore` clean; `git diff --check` clean (docs/evidence only). Failed comparative execution retained as research evidence, not treated as a candidate-selection winner or benchmark result. |
| M2 | Harness contracts and architecture lock. Produce `docs/phases/v3/phase-19/M2_ARCHITECTURE_LOCK.md` and `docs/phases/v3/phase-19/M2_THREAT_MODEL.md`. Define Native Harness internal contracts, six-fact capability rows (P19-D06), history seam (P19-D10 concern 2), event-surface extension decision (P19-D02). Resolve open decisions: backend selection model, model provider/protocol, streaming, library (P19-D13), tool-calling format, turn budget. | M1 | `M2_ARCHITECTURE_LOCK.md` exists, resolves every M2-owned open decision, and is reviewed and accepted; `M2_THREAT_MODEL.md` exists and is reviewed and accepted before M3; `dotnet build Zaide.slnx --no-restore` succeeds; contract unit tests pass: `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19Contracts"`; architecture inventory ratchet updated: `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"` passes |
| M3 | Tool-calling execution loop: model turn management, tool-call parsing, `IAgentActionBroker.RequestAsync` dispatch for all five `AgentActionKind` values, tool-result formatting, failure recovery, in-run model/tool loop history (P19-D10 concern 1), system prompt with Phase 18 manifest, run-scoped cancellation | M2 | **Complete (read-only audit gate; M4 not started).** `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19ToolLoop"` passes (8/8); broker dispatch tests cover all 5 `AgentActionKind` values: `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19BrokerDispatch"` passes (6/6); context manifest consumption tests: `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19ContextConsumption"` passes (5/5); architecture inventory ratchet updated to post-M3 baseline (682/350/332, 621/576): `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"` passes; `dotnet build Zaide.slnx --no-restore` clean |
| M4 | Production wiring and capability truthfulness: register Native Harness in `AddZaideAgents`; six-fact `AgentCapabilitySnapshot` rows; action plane activation (`ContractAgentActionBroker` resolves for production runs) | M3 | `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19Integration"` passes (production backend resolves `ContractAgentActionBroker`; end-to-end run with ≥1 tool call succeeds; capability rows verified); `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"` passes with updated baselines; `dotnet build Zaide.slnx --no-restore` clean |
| M5 | Townhall structured activity projection: verify the existing broker-event path (`IAgentActionBroker` → `RunScopedAgentActionEventPublisher` → `AgentEvent` → `AgentConversationEventProjection.ProjectActionResultReported`) renders Native Harness tool activity; extend the projection for richer rendering only if M2 authorized a bounded event-surface extension (P19-D02); honest evidence-level presentation | M3 (may parallelize with M4 if M2 event surface is unchanged) | `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19TownhallProjection"` passes; evidence-level presentation verified; `dotnet build Zaide.slnx --no-restore` clean |
| M6 | Closeout: adversarial tests exercising the M2 threat model (`M2_THREAT_MODEL.md`); architecture ratchet + bypass ratchet finalization; full-suite verification; evaluation evidence on real repository work (not a comparative campaign); documentation truth-sync | M4, M5 | `dotnet test Zaide.slnx --no-build` passes (full fast suite); `dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings` passes (serial fallback); `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19Adversarial"` passes; `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"` passes; `git diff --check` clean |

### Milestone dependency graph

```text
M0 (planning)
  └── M1 (research + provenance, stop-and-ask for external activity)
       └── M2 (contracts + architecture lock + threat model)
            └── M3 (tool-calling loop)
                 └── M4 (production wiring + capability)
                      └── M5 (Townhall activity projection via existing broker-event path)
                           └── M6 (closeout, depends on M4 + M5)
```

M5 may parallelize with M4 if the M2 event-surface design reuses the existing
broker-event path without extension. M6 depends on both M4 and M5.

### Expected commit boundaries

Each milestone targets one reviewable commit with its ordinary documentation
updates included. Corrective passes may add commits within the same milestone.
M1 is documentation/evidence-only and produces no production code commit. M2
produces contract types, tests, the architecture-lock doc, and the threat-model
doc. M3–M5 produce implementation commits. M6 produces closeout evidence and
ratchet updates.

---

## Verification strategy

### Per implementation milestone

| Milestone | Primary verification |
|-----------|---------------------|
| M0 | `dotnet build Zaide.slnx --no-restore` succeeds; `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"` passes (37/37); `git diff --check` clean; plan reviewed and accepted |
| M1 | `M1_RESEARCH_RECORD.md` inventories ≥3 exact-commit candidates with licenses verified; ≥2 candidates verified runnable through authorized zero-cost local path; comparable corpus attempts recorded (exact commands, reset/isolation method, results, failures, resource limits); task-loop, context, search, editing, tool execution, recovery, and compaction observations recorded; `M1_PROVENANCE.md` covers every reuse candidate; `dotnet build Zaide.slnx --no-restore` clean; `git diff --check` clean (no production code). Failed comparative execution retained as research evidence, not treated as a candidate-selection winner or benchmark result. The original full-corpus benchmark gate is retired by explicit user-directed plan amendment. |
| M2 | `M2_ARCHITECTURE_LOCK.md` and `M2_THREAT_MODEL.md` both reviewed and accepted before M3; `dotnet build Zaide.slnx --no-restore` succeeds; `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19Contracts"` passes; `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"` passes with updated inventory |
| M3 | `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19ToolLoop|FullyQualifiedName~Phase19BrokerDispatch|FullyQualifiedName~Phase19ContextConsumption"` passes; `dotnet build Zaide.slnx --no-restore` clean |
| M4 | `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19Integration"` passes; `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"` passes with updated baselines; `dotnet build Zaide.slnx --no-restore` clean |
| M5 | `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19TownhallProjection"` passes; `dotnet build Zaide.slnx --no-restore` clean |
| M6 | `dotnet test Zaide.slnx --no-build` passes (full fast suite); `dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings` passes; `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase19Adversarial"` passes; `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"` passes; `git diff --check` clean |

### Full regression gate

```bash
# Fast suite (interactive terminal only — redirected output can reproduce
# the known parallel-runner hang)
dotnet test Zaide.slnx --no-build

# Serial fallback (if fast mode fails or hangs)
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
```

### Required test layers

| Layer | Purpose |
|-------|---------|
| Contract tests (`Phase19Contracts`) | Native Harness internal contract type correctness and immutability (M2) |
| Tool-loop tests (`Phase19ToolLoop`) | Model turn management, tool-call parsing, broker dispatch for all 5 `AgentActionKind` values, tool-result formatting, failure recovery (M3) |
| Broker dispatch tests (`Phase19BrokerDispatch`) | Every `AgentActionKind` flows through `IAgentActionBroker.RequestAsync`; no direct file/process access; broker-denied paths handled (M3) |
| Context consumption tests (`Phase19ContextConsumption`) | Phase 18 manifest read into system prompt; hard exclusions respected; redaction fail-closed; no re-read of IDE state outside manifest (M3) |
| Integration tests (`Phase19Integration`) | Production backend resolves `ContractAgentActionBroker`; end-to-end run with ≥1 tool call; six-fact capability rows truthful (M4) |
| Townhall projection tests (`Phase19TownhallProjection`) | Existing broker-event path (`ActionResultReported` → `AgentConversationEventProjection.ProjectActionResultReported`) renders tool activity; bounded extension (if M2-authorized) verified; evidence levels presented honestly (M5) |
| Bypass ratchet tests (updated `Phase17BypassRatchetTests` / `Phase18ContextBypassRatchetTests`) | No action bypass (all file/command ops through broker); no context leak to legacy backend; no capability overstatement; no permission bypass (M4/M6) |
| Adversarial tests (`Phase19Adversarial`) | Exercises `M2_THREAT_MODEL.md`: action bypass attempts; context leak under Off policy; capability overstatement; permission bypass; cancellation race; process cleanup; late completion after cancellation; prompt injection vectors from M2 threat model (M6) |

---

## Limitations (by design)

- **No ACP:** Phase 19 does not implement the ACP backend (Phase 20). The
  Native Harness is not a wrapper around ACP and does not create abstractions
  that force ACP into a dishonest lowest common denominator.
- **No persistence, memory, or resume:** Run-scoped history is in-memory only.
  Durable memory, raw trace storage, session resume, and interrupted-run
  recovery belong to Phase 21. A run interrupted by crash or disconnect is
  terminal or indeterminate, not silently resumed.
- **No raw trace disclosure:** Model I/O and transport traces are not exposed to
  the user in Phase 19 (Phase 21 concern).
- **No `Custom` / agent / project context policy:** Phase 18's four-level model
  (Off/Minimal/Standard/Detailed) with application default and session override
  remains the only policy surface. Per-source/per-event, agent-level, and
  project-level policy are deferred.
- **No comparative benchmark campaign:** Phase 19 closeout verifies the harness
  works on real repository work. A full comparative campaign against external
  harnesses is Phase 16's parked domain and is not a Phase 19 gate.
- **No reverted Phase 16 artifacts:** The Qwen observational path, reverted M3
  qualification harness, and reverted candidate fixtures are not adopted.
  Phase 16 methodology informs M1 research design only.
- **No public agent API:** Internal contracts support the application; V3 does
  not commit Zaide to selling or operating a public agent API.
- **No Human-to-Human messaging:** The participant model must not hard-code
  every direct conversation as User-to-Agent in a way that prevents future
  Human-to-Human, but Phase 19 does not deliver it.
- **No backend selection UI:** If the backend selection model (Open Decisions)
  defers multi-backend routing to a later phase, Phase 19 ships a single
  production backend without a selection surface.
- **Tools limited to Phase 17 taxonomy:** Only `ReadFile`, `CreateFile`,
  `ReplaceFile`, `DeleteFile`, `ExecuteCommand`. No dedicated agent-requested
  network tools (`FetchUrl`, `HttpRequest`, browser), no custom tool
  registration, no MCP/tool-server integration in Phase 19 (P19-D16).
  Provider transport and inherited command network access remain in scope.
- **No model-specific tokenization:** Phase 18 heuristic token counting
  (`ceil(character_count / 4)`) remains.
- **No cross-workspace context synchronization:** Context is scoped to the
  current workspace and run only.
- **Research is a prerequisite, not a guarantee:** M1 research informs M2
  architecture but does not lock a winner. The harness is optimized for real
  agent performance, not for novelty or code ownership purity (V3 §3 principle
  5).
- **M1 comparative-execution limitation:** The full-corpus benchmark gate was
  retired by explicit user-directed plan amendment. The local model capability
  is insufficient for this campaign to produce meaningful architectural
  evidence. Failed comparative execution is retained as research evidence and
  does not select a winning external architecture. No architecture winner was
  selected at M1. Do not perform another full-corpus chase.

---

## Stop conditions

Stop work and ask the user if any of the following occur:

1. **Verification failure:** `dotnet build` or `dotnet test` fails and the root
   cause is a Phase 19 change that cannot be fixed within the milestone scope.
2. **Material scope conflict:** A milestone requires work that belongs to Phase
   17 (closed), Phase 18 (closed), Phase 20 (ACP), Phase 21 (persistence/memory),
   or a structural refactor.
3. **Architecture ratchet violation:** A new type or dependency weakens an
   existing architecture baseline and cannot be justified within Phase 19 scope.
4. **Agent-requested action bypass:** The harness performs workspace file I/O or
   process execution outside `IAgentActionBroker`, or introduces a dedicated
   agent-requested network operation outside the Phase 17 action taxonomy.
   Provider/model transport explicitly allowed by P19-D16 does not trigger this
   condition merely because it is backend-owned and bypasses the action broker.
   Network access performed by an approved `ExecuteCommand` remains governed by
   Phase 17's disclosed non-sandbox boundary.
5. **Destructive action:** A proposed change modifies Phase 15/17/18 contracts
   (`IAgentBackend`, `AgentBackendRequest`, `AgentBackendExecutionContext`,
   `IAgentActionBroker`, `AgentContextManifest`) in a way that breaks the
   shipped-inert boundary for other backends or the legacy backend.
6. **Open decision materially changing milestone sequence:** A design choice at
   M1/M2 changes the milestone order, trust model, or delivery boundary in a way
   not anticipated by this plan.
7. **New dependency without focused proof:** A library or tool not listed in
   this plan is needed to complete a milestone, and the focused proof,
   compatibility evidence, or license/provenance check is incomplete (P19-D13).
8. **Provenance or license failure:** Code considered for copying or adaptation
   cannot have its license verified, or the license/NOTICE obligations cannot
   be satisfied (P19-D08). License verification happens before code is copied
   into production.
 9. **Research obligation not met:** M2 architecture lock is attempted before M1
    research obligations (V3 §8.1) are satisfied. **(M1 research/provenance gate
    satisfied by the amended plan; the full-corpus benchmark requirement was
    retired. M2 may proceed when the M1 evidence record is accepted.)**
10. **Threat model not locked before M3:** M3 tool-calling implementation
    begins before `M2_THREAT_MODEL.md` is accepted (P19-D15).
11. **Phase 16 artifact adoption pressure:** A proposal to adopt the reverted
    Qwen path, reverted M3 qualification harness, or reverted candidate fixtures.
12. **ACP overlap:** Tool-calling abstractions are proposed that would force ACP
    (Phase 20) into a dishonest lowest common denominator or make ACP a wrapper
    around the Native Harness (P19-D03).
13. **External side effect requiring authorization (docs-rules §11):** M1
    research requires external source acquisition, candidate execution,
    credentials, network egress, or paid API use that has not received
    explicit user authorization for that specific activity.

---

## Exit conditions

- [ ] Build succeeds: `dotnet build Zaide.slnx --no-restore`
- [ ] All milestone test gates pass
- [ ] Full fast suite passes: `dotnet test Zaide.slnx --no-build`
- [ ] Serial fallback passes: `dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings`
- [ ] Architecture inventory, visibility, and bypass ratchets updated and passing
      for the Native Harness backend and activated action plane
- [ ] Native Harness `IAgentBackend` registered in production DI
      (`AddZaideAgents`)
- [ ] Native Harness implements `IAgentActionRequestCapableBackend`; production
      runs resolve `ContractAgentActionBroker` (not `UnavailableAgentActionBroker`)
- [ ] Action plane activated: all file/command operations flow through
      `IAgentActionBroker.RequestAsync` with the correct `AgentActionPayload`;
      no bypass path exists (`Phase19Adversarial` test verified)
- [ ] Phase 18 context manifest consumed by the Native Harness (inert boundary
      broken by design); hard exclusions and redaction fail-closed behavior
      respected
- [ ] Truthful `AgentCapabilitySnapshot` with six-fact rows
      (`Advertised`/`Available`/`Configured`/`Permitted`/`Degraded`/
      `CurrentlyUsable`) for `Tools`, `Permissions`, `IdeContext`, `Streaming`,
      `Cancellation`, `MessageCompletion`; no capability overstatement
      (`Phase19Adversarial` test verified)
- [ ] Structured tool activity visible in Townhall through the existing
      broker-event path (`IAgentActionBroker` → `RunScopedAgentActionEventPublisher`
      → `AgentEvent` (`ActionResultReported`) → `AgentConversationEventProjection.ProjectActionResultReported`);
      evidence levels presented honestly
- [ ] Backend text/failure flows through `AgentBackendEvent`
      (`MessageCompleted`/`FailureObserved`) only; tool activity never carried
      as a new `AgentBackendEvent` kind
- [ ] `M2_THREAT_MODEL.md` accepted before M3; `Phase19Adversarial` tests
      exercise the threats it identifies
- [ ] Run-scoped in-memory model/tool loop history and system prompt
      construction implemented; prior-conversation-replay decision (P19-D10
      concern 2) resolved at M2; no cross-session or persistent memory
- [ ] Cancellation, process cleanup, and late-completion-after-cancellation
      behavior correct (`Phase19Adversarial` test verified)
- [ ] Research record (`M1_RESEARCH_RECORD.md`) and provenance records
      (`M1_PROVENANCE.md`, P19-D08) complete for any copied or adapted code
- [ ] No Phase 20 (ACP), Phase 21 (persistence/memory/resume/raw trace), public
      agent API, or Human-to-Human work implemented
- [ ] No reverted Phase 16 artifacts adopted
- [ ] No dedicated agent-requested network tools added (P19-D16)
- [ ] `git diff --check` clean

---

## Rollback plan

Phase 19 is documentation-only at M0. If M0 is rejected, this file and
`TOFIX.md` are deleted and no code changes are reverted.

If a later milestone implementation is reverted, rollback is **commit-level
reversal**, not file deletion. Phase 19 adds a new production backend and
modifies the composition root:

- New backend files under `src/Features/Agents/` (Infrastructure and/or
  Application, per M2 architecture lock)
- New contract/domain types under `src/Features/Agents/Contracts/` and
  `Domain/` (per M2)
- `AddZaideAgents` registration (adds or replaces the `IAgentBackend` registration)
- Architecture baselines: `ArchitectureInventoryReader`, `PublicProductionTypeBaseline`
  counts and `PublicProductionTypeBaseline.txt`
- Bypass ratchet expectations in `Phase17BypassRatchetTests` and
  `Phase18ContextBypassRatchetTests`

Rollback procedure:

1. Identify the last known-good commit before Phase 19 implementation (`8eed91d3`).
2. `git revert <phase-19-commit-range>` or `git reset --hard 8eed91d3`.
3. Verify build and test suite pass.
4. Architecture ratchets revert to Phase 18 M6 closeout baselines (646 total /
   350 public / 296 internal, 585 source files / 540 Features).
5. The shipped-inert boundary is restored: `LegacyOpenAiCompatibleAgentBackend`
   is the sole production backend; every production run resolves
   `UnavailableAgentActionBroker`; the context manifest is assembled but not
   consumed.

**Baseline commit:** `8eed91d3` (Phase 18 M6 closeout)
