# Phase 20: ACP Agent Backend — Implementation Plan

## Status and authorization

**Status:** M3 complete and published at `2d90604991dd9b87cb6e22a2c8c9a7b771504de6` with publication-record correction `04831f1c` (depends on M2 at
`880b4524c9c53190687aee0cc10843900191b8ce`). M4 and all later milestones are
not started and not authorized by this document. Phase 21 has not started.

**Authorized work in M1:** Stable ACP v1 schema/codec lock, threat model,
internal wire DTOs, JSON-RPC/newline framing, pure protocol session plumbing,
truthful capability advertisement, focused protocol tests, and architecture
inventory ratchets. No process execution, production DI, broker bridge, UI, or
Native Harness references.

**Prior phase:** Phase 19 is complete, published, accepted, and closed. Nothing
in this plan reopens, modifies, or reinterprets Phase 19 history. Phase 20 is an
independent backend outcome and does not depend on Phase 19.

### M0 planning baseline

| Check | Verified result |
|-------|-----------------|
| Branch | `master` |
| Planning-base `HEAD` | `4e0c89162b33547e3461aa4b2f845bb7cbbb1314` |
| Planning-base `origin/master` | `4e0c89162b33547e3461aa4b2f845bb7cbbb1314` |
| Working tree before planning | Clean (`git status --short --branch` reported only `## master...origin/master`) |
| Phase 15 dependency | Complete and closed; backend-neutral in-memory session/run/event foundation exists |
| Phase 17 dependency | Complete, accepted, and closed; run-scoped action broker exists |
| Phase 18 dependency | Complete and closed; policy-filtered run context manifest exists |
| Phase 19 relationship | Complete and closed; Native Harness remains a sibling backend, not an ACP layer |
| Existing ACP implementation | None under `src/`, `tests/`, or `tools/`; no ACP package reference |
| Current architecture baseline | 682 total / 350 public / 332 internal top-level production types; 621 tracked production C# files (576 Features / 41 App / 4 UI); 2 approved composition locator findings |
| Verification date | 2026-07-28 |

---

## Phase outcome

Phase 20 delivers the outcome locked by `docs/roadmap/V3.md`:

> Zaide can connect ACP agents as peer backend choices with equal Townhall
> placement, explicit identity binding, honest capability limits, and Zaide
> mediation wherever the protocol permits.

ACP means the **Agent Client Protocol**. The ACP backend is a peer
`IAgentBackend` implementation over the existing Phase 15 session boundary. It
is not:

- a Native Harness wrapper;
- a Native Harness transport;
- an automatic fallback from or to the Native Harness;
- a reactivation of Phase 16 runner contracts;
- a public Zaide API; or
- proof of entitlement to any provider account, subscription, model, or login.

The dependency direction remains:

```text
Townhall / Agent direct conversation
  -> Phase 15 session/run/event owner
      -> Native Harness backend (Phase 19, independent)
      -> ACP backend (Phase 20, independent)

ACP backend
  -> ACP process and protocol adapter owned by Agents
  -> Phase 18 AgentContextManifest for disclosed run context
  -> Phase 17 IAgentActionBroker only for client-side actions Zaide can mediate
```

ACP failure never causes silent Native Harness fallback. The selected backend
fails, disconnects, or becomes indeterminate truthfully.

---

## Primary official ACP source lock

All sources below were accessed read-only without credentials on 2026-07-28.
The release artifact, not an unversioned documentation page, is the
implementation contract.

| Source | URL | Exact claim used |
|--------|-----|------------------|
| Protocol repository and versioning | https://github.com/agentclientprotocol/agent-client-protocol | The current stable wire protocol is version `1`; wire compatibility is negotiated through `initialize.protocolVersion`, independently from schema/SDK artifact versions. |
| Stable schema release | https://github.com/agentclientprotocol/agent-client-protocol/releases/tag/schema-v1.20.0 | Exact stable schema artifact is `schema-v1.20.0`, published 2026-07-21. Tag resolves to commit `5e89c71497fe07dd4ae633c181a17224f4a8956d`. |
| Stable schema asset | https://github.com/agentclientprotocol/agent-client-protocol/releases/download/schema-v1.20.0/schema.json | Frozen stable schema digest is `sha256:92c1dfcda10dd47e99127500a3763da2b471f9ac61e12b9bf0430c32cf953796`. |
| Stable metadata asset | https://github.com/agentclientprotocol/agent-client-protocol/releases/download/schema-v1.20.0/meta.json | Declares protocol version `1` and the exact stable method names recorded below. Digest is `sha256:e0bf36f8123b2544b499174197fdc371ec49a1b4572a35114513d56492741599`. |
| Transport | https://agentclientprotocol.com/protocol/v1/transports | ACP v1 uses UTF-8 JSON-RPC 2.0; stdio frames are newline-delimited, contain no embedded newline, stdout carries only ACP messages, and stderr may carry logs. Streamable HTTP remains draft. |
| Initialization and capabilities | https://agentclientprotocol.com/protocol/v1/initialization | Client and agent must agree on integer protocol version; omitted capabilities mean unsupported; baseline agents support `session/new`, `session/prompt`, `session/cancel`, and `session/update`. |
| Session and prompt lifecycle | https://agentclientprotocol.com/protocol/v1/session-setup and https://agentclientprotocol.com/protocol/v1/prompt-turn | A client creates a session with absolute `cwd`, sends content blocks through `session/prompt`, receives `session/update`, and gets a terminal stop reason. |
| Content model | https://agentclientprotocol.com/protocol/v1/content | Stable content blocks are text, image, audio, resource link, and embedded resource; text and resource link are baseline prompt support, while image/audio/embedded context are capability-gated. |
| Tool and permission reporting | https://agentclientprotocol.com/protocol/v1/tool-calls | Tool calls and updates are agent reports. `session/request_permission` returns an ACP option choice; it does not itself prove that Zaide executed or mediated the operation. |
| Client filesystem methods | https://agentclientprotocol.com/protocol/v1/file-system | `fs/read_text_file` and `fs/write_text_file` are optional client capabilities and use absolute paths. |
| Client terminal methods | https://agentclientprotocol.com/protocol/v1/terminals | `terminal: true` means the complete create/output/wait/kill/release lifecycle is available, not merely synchronous command execution. |
| Authentication | https://agentclientprotocol.com/protocol/v1/authentication | Agents advertise `authMethods`; the client selects an advertised method ID with `authenticate`; optional `logout` is capability-gated. The agent owns its authentication flow. |
| Cancellation | https://agentclientprotocol.com/protocol/v1/cancellation | `$/cancel_request` is optional request cancellation; prompt cancellation also has the feature-specific `session/cancel` notification and `cancelled` stop reason. |
| Extensibility | https://agentclientprotocol.com/protocol/v1/extensibility | Custom data belongs in `_meta`; custom methods start with `_`; custom capability use must be negotiated. |
| ACP v2 status | https://agentclientprotocol.com/announcements/acp-v2-draft | ACP v2 was published as Draft on 2026-07-20 and may change before stabilization; it must not ship by default. |
| Official SDK status | https://agentclientprotocol.com/announcements/sdk-1-0-releases and https://agentclientprotocol.com/libraries/community | Official Rust and TypeScript SDKs reached `1.0.0`; the official library list has no .NET SDK. .NET entries are community managed. |
| Official agent registry | https://cdn.agentclientprotocol.com/registry/v1/latest/registry.json | Registry format version `1.0.0`; current agent entries are provenance leads, not executed compatibility evidence. |
| Registry authentication boundary | https://github.com/agentclientprotocol/registry/blob/main/AUTHENTICATION.md | Registry agents must expose agent-managed or terminal authentication. Registry inclusion does not authorize Zaide to authenticate, install, or execute an agent. |

### Locked protocol and SDK profile

| Concern | Phase 20 decision |
|---------|-------------------|
| Wire protocol | ACP v1 only (`initialize.protocolVersion = 1`) |
| Stable schema artifact | `schema-v1.20.0` at commit `5e89c71497fe07dd4ae633c181a17224f4a8956d` and the digests above |
| Unstable schema | Forbidden (`schema.unstable.json` is not an implementation input) |
| ACP v2 | Forbidden in Phase 20 production; Draft research only |
| SDK | **No ACP SDK dependency.** No official .NET SDK exists. Phase 20 uses the pinned stable schema and existing .NET `System.Text.Json`, stream, and process primitives. |
| Community .NET SDKs | Not adopted. `nuskey8/acp-csharp` and `MertBasar0/acp-net` are provenance leads only; a later dependency proposal requires a plan amendment, focused proof, license review, and explicit authorization. |
| Transport | Local child process over stdio only |
| Framing | UTF-8, one JSON-RPC message per newline, no embedded newline, stdout protocol-only, bounded/redacted stderr logging |
| Streamable HTTP / WebSocket | Not supported; the official v1 page still describes Streamable HTTP as draft |
| Custom transport | Not supported |
| MCP servers over ACP | Client sends an empty `mcpServers` list; Zaide does not configure, launch, or credential MCP servers in Phase 20 |
| Custom ACP methods/capabilities | Not enabled; unknown `_` notifications are ignored safely, unknown requests receive method-not-found, and `_meta` is preserved only as bounded opaque data where needed for forward compatibility |

The no-SDK decision is deliberate and exact, not an unresolved version
placeholder. Any SDK adoption changes the dependency, provenance, schema, and
rollback boundary and therefore requires an approved plan amendment before
implementation.

### Exact stable method profile

The `schema-v1.20.0` `meta.json` declares these methods:

| Direction | Stable methods |
|-----------|----------------|
| Client calls agent | `initialize`, `authenticate`, `session/new`, `session/load`, `session/set_mode`, `session/set_config_option`, `session/prompt`, `session/cancel`, `session/list`, `session/delete`, `session/resume`, `session/close`, `logout` |
| Agent calls client | `session/request_permission`, `session/update`, `fs/write_text_file`, `fs/read_text_file`, `terminal/create`, `terminal/output`, `terminal/release`, `terminal/wait_for_exit`, `terminal/kill` |
| Either side | `$/cancel_request` |

Phase 20's initial supported subset is narrower and truthful:

- Required: `initialize`, `session/new`, `session/prompt`,
  `session/cancel`, `session/update`.
- Authentication boundary: parse `authMethods`; invoke `authenticate` only
  after explicit user action in M5; invoke `logout` only when advertised and
  explicitly requested.
- Client filesystem: advertise `fs.readTextFile` and `fs.writeTextFile` only
  after the M4 broker bridge passes.
- Permission requests: support `session/request_permission`, but label the
  result as an ACP-agent permission choice, never a Phase 17 broker decision.
- Protocol request cancellation: support `$/cancel_request` where possible and
  retain feature-specific `session/cancel`.
- Parse and bound all stable session updates. Projection support is defined
  below.
- Do not invoke `session/load`, `session/list`, `session/delete`,
  `session/resume`, or `session/close` in the initial profile. Persistent or
  resumed session product behavior belongs to Phase 21.
- Do not invoke session mode/config methods, advertise boolean-config client
  support, or expose slash-command UI in the initial profile.
- Advertise `terminal: false`. Phase 17 offers one bounded synchronous
  `ExecuteCommand` result, while ACP `terminal: true` promises the complete
  asynchronous terminal-ID lifecycle. Claiming equivalence would be false.

### Exact message, tool, and event model

The stable `SessionUpdate` union has eleven variants:

1. `user_message_chunk`
2. `agent_message_chunk`
3. `agent_thought_chunk`
4. `tool_call`
5. `tool_call_update`
6. `plan`
7. `available_commands_update`
8. `current_mode_update`
9. `config_option_update`
10. `session_info_update`
11. `usage_update`

Stable prompt stop reasons are `end_turn`, `max_tokens`,
`max_turn_requests`, `refusal`, and `cancelled`.

Phase 20 maps them as follows:

| ACP input | Zaide treatment |
|-----------|-----------------|
| `agent_message_chunk` | Bounded ordered accumulation per ACP session/prompt; emit the existing backend message completion only when the prompt response terminates successfully. Optional ACP message IDs are opaque correlation keys, never Actor IDs. |
| `user_message_chunk` | Accepted only during protocol-defined replay; initial profile does not invoke replay methods, so unexpected live chunks are retained as bounded protocol evidence and not appended as a second user message. |
| `agent_thought_chunk` | Never presented as hidden reasoning or chain of thought, never persisted, and never relabeled as an assistant answer. It is discarded after bounded validation in Phase 20; raw trace belongs to Phase 21. |
| `tool_call` / `tool_call_update` | Normalize as backend-reported activity unless the corresponding concrete operation passes through the Phase 17 broker. Never fabricate `AgentActionId`, permission, or Zaide execution evidence from a report. |
| `plan` | Optional, backend-reported structured activity; bounded replacement semantics. No separate planner ownership. |
| commands, mode, config, session-info, usage updates | Parse and bound for compatibility; do not expose unsupported controls. Usage/cost is backend-reported and Phase 21 owns the final product surface. |
| `session/request_permission` | ACP-level user choice only. It cannot consume or replace `AgentPermissionDecision` and cannot authorize workspace mutation. |
| `fs/read_text_file` | Convert absolute path to a validated workspace-relative request, then use `IAgentActionBroker` with `AgentReadFileActionPayload`. |
| `fs/write_text_file` | First obtain authoritative state through the broker; compose `AgentCreateFileActionPayload` or revision-bound `AgentReplaceFileActionPayload`; execute only through the same broker. |
| `terminal/*` | Reject as method unsupported because `terminal` is not advertised. |
| unknown stable/custom update | Bound, preserve discriminator for diagnostics, do not project as trusted activity, and fail or ignore according to JSON-RPC/ACP rules. |

Tool-call status and permission claims are observations about an external
runtime. Only a successful Phase 17 broker result is
`ZaideExecuted`/`ZaideMediated`. Direct external-agent work is
`BackendExecutedAndReported`, `ExternallyObserved`, or `Unobservable`.

### Optional-extension disposition

| Extension or optional feature | Phase 20 disposition |
|-------------------------------|----------------------|
| Image/audio prompts | Not advertised by Zaide until a later UI/content milestone proves support |
| Embedded context | Use only when the agent advertises `embeddedContext`; otherwise send a bounded attributed text block from the Phase 18 manifest |
| `loadSession` / list / delete / resume / close | Parsed capability only; not invoked; Phase 21 owns continuity and persistence |
| Session modes / config options / slash commands | Parse updates, no user control surface in Phase 20 |
| Usage/cost updates | Backend-reported, bounded, no durable or billing claim; final presentation deferred to Phase 21 |
| Agent plans | May be projected as backend-reported activity after M3/M5 tests |
| MCP HTTP/SSE/stdio server configuration | Empty list only; no server launch or credentials |
| `_meta` and `_` methods | No Zaide extensions in Phase 20 |
| Elicitation | Not in the pinned stable schema asset; unsupported |
| Terminal authentication | Not in the pinned stable `AuthMethod` union; unsupported |
| ACP v2 features | Unsupported; Draft only |

### Recorded source ambiguity

The live documentation site on 2026-07-28 contains stabilized announcements
and pages for features, including elicitation and terminal authentication, that
are absent from the latest released stable `schema-v1.20.0` asset. The website
also publishes ACP v2 as Draft.

Resolution:

- `schema-v1.20.0` is the Phase 20 implementation contract.
- A documentation-page claim not present in that stable artifact is not
  implemented or advertised.
- M1 records a schema-conformance fixture from the pinned asset and must not
  update it automatically.
- A later stable schema release is upgrade research, not an implicit change.
  Adopting it requires a plan amendment, compatibility diff, and rerun of all
  protocol/architecture gates.

No required primary source was unavailable or authentication-gated during M0.

---

## Candidate and provider provenance research

The official ACP registry was read on 2026-07-28 at registry format `1.0.0`.
The following are **provenance leads only**:

| Registry ID | Registry version | Distribution/repository provenance | M0 claim |
|-------------|------------------|------------------------------------|----------|
| `codex-acp` | `1.1.7` | `@agentclientprotocol/codex-acp@1.1.7`; https://github.com/agentclientprotocol/codex-acp; Apache-2.0 | Listed by the official registry only |
| `claude-acp` | `0.63.0` | `@agentclientprotocol/claude-agent-acp@0.63.0`; https://github.com/agentclientprotocol/claude-agent-acp; registry marks proprietary | Listed by the official registry only |
| `gemini` | `0.52.0` | `@google/gemini-cli@0.52.0 --acp`; https://github.com/google-gemini/gemini-cli; Apache-2.0 | Listed by the official registry only |

M0 did not install, download, execute, initialize, authenticate, or send a
prompt to any candidate. Registry inclusion and declared version do not prove:

- compatibility with Zaide's pinned profile;
- successful authentication;
- account or subscription entitlement;
- permission interposition;
- action observability;
- model availability;
- provider terms; or
- production support.

Any candidate smoke is a later, separately authorized, zero-cost-or-explicitly
budgeted activity. It requires exact distribution/version provenance,
license/terms review, executable origin verification, clean isolated
configuration, no inherited credentials, and an explicit stop before login or
paid use unless the user authorizes that exact activity.

---

## Verified live reusable seams and ownership

### Phase 15 session and event boundary

| Seam | Live truth | Phase 20 use |
|------|------------|--------------|
| `IAgentBackend` | Internal backend-neutral interface exposes `BackendId`, `BackendVersion`, `CapabilitySnapshot`, and `ExecuteAsync(AgentBackendExecutionContext, CancellationToken)` | `AcpAgentBackend` implements this directly |
| `AgentBackendExecutionContext` | Holds immutable request plus run-scoped `IAgentActionBroker`; context manifest delegates to the request | ACP consumes only this run input |
| `AgentBackendRequest` | Zaide owns session, run, conversation, actor, message, text, and optional context-manifest IDs | ACP IDs remain separate adapter state |
| `AgentBackendEvent` | Only `MessageCompleted` and `FailureObserved` | ACP buffers stream updates and terminates through these existing outcomes |
| `AgentEventStream` | Owns ordered normalized events for sessions/runs/actions/context | Additive ACP reported-activity events may enter here; no direct Townhall write |
| `AgentCapabilitySnapshot` | Immutable, versioned, six-fact capability rows | ACP negotiation is mapped without flattening facts |
| `AgentSessionService` | Owns one session per conversation, binds target Actor and backend, assembles context, creates broker, observes backend | Remains lifecycle authority; ACP cannot own Zaide session truth |

The live `AgentSessionService` already stores multiple `IAgentBackend`
instances by ID. The live `AgentExecutionCoordinator`, however, still owns one
fixed backend ID and defaults it to the legacy ID. Merely adding a second DI
registration cannot provide a truthful peer choice. M5 owns an explicit
per-Agent-Identity backend binding seam and removes fixed coordinator
selection. This is new Phase 20 work and does not reopen Phase 19 history.

### Phase 17 action-control boundary

| Seam | Live truth | Phase 20 rule |
|------|------------|---------------|
| `IAgentActionRequestCapableBackend` | Marker causing `AgentSessionService` to create a real run-scoped broker | ACP implements the marker only when the M4 client filesystem bridge is active |
| `IAgentActionBroker` | Accepts one typed payload plus correlation and cancellation | Sole client-side file-action entry point |
| `ContractAgentActionBroker` | Captures workspace scope; mediates read/create/replace/delete/command; emits action facts; reconciles documents | Never bypassed by ACP application code |
| `RunScopedAgentActionEventPublisher` | Publishes ordered action facts and bounded audit records | Reused unchanged for broker-mediated ACP actions |
| `AgentPermissionDecision.TryConsume()` | Atomic `Published -> Consumed` transition after all validations | Remains the final authorization step |

For file mutation, the live broker revalidates proposal/base/workspace state
immediately before `TryConsume()`. A stale proposal returns
`Revoked/StaleBaseRevision` while the `Published` decision remains unconsumed.
Phase 20 must preserve this exact ordering.

ACP `session/request_permission` is not a substitute for this contract. It
lacks Zaide's canonical action fingerprint, workspace generation, revision
binding, and one-decision/one-execution consumption.

### Phase 18 context-manifest boundary

`AgentContextManifest` is immutable and run-scoped. It carries selected items,
policy level, fingerprints/provenance, token budget, truncation/exclusion
decisions, and UTC assembly time. `ProcessingFailed` content is empty and hard
exclusions have no Phase 20 escape hatch.

ACP receives context only from
`AgentBackendExecutionContext.ContextManifest`. It must not re-read editor,
terminal, debug, diagnostics, workspace, or process state. The existing
`ContextDisclosed` event remains the user-visible disclosure record.

### Phase 19 Native Harness boundary

Phase 19's `NativeHarnessAgentBackend`, loop history, provider transport,
system-prompt builder, tool mapper, SSE reader, and provider options are
Native-Harness-owned. ACP must not reference, wrap, instantiate, fall back to,
or reuse these types.

Reusable Phase 19 outcomes are limited to the neutral contracts that Phase 19
already consumes and the architecture/security precedents enforced by its
tests. No Phase 19 file or history is modified by Phase 20 M0.

### Townhall projection path

The live path is:

```text
IAgentBackend / IAgentActionBroker
  -> AgentSessionService / RunScopedAgentActionEventPublisher
  -> AgentEventStream
  -> AgentConversationEventProjection
  -> IConversationStore
  -> TownhallViewModel observes EntryAppended
  -> TownhallEntryProjection renders the entry
```

`AgentConversationEventProjection` is the sole normalized-agent-event writer
to conversation/Townhall. ACP transport, process, protocol, authentication,
and backend classes must never call `IConversationStore.AppendEntry`.

### Dependencies and architecture tests

- Production is one `Zaide` project/assembly targeting .NET 10.
- Existing production dependencies contain no ACP SDK or JSON-RPC package
  suitable for ACP. `System.Text.Json` and BCL process/stream APIs require no
  package.
- `StreamJsonRpc` is already present for LSP but is Language-owned; ACP must
  not create a cross-feature Infrastructure dependency on Language. Reuse
  would require a separately justified feature-neutral admission.
- Architecture tests enforce feature ownership, public-by-exception, two
  composition locator residuals, source/type counts, conversation dependency
  direction, and Phase 17/18/19 bypass rules.
- Phase 20 production types default to `internal`; no public baseline growth is
  authorized by this plan.

---

## Ownership and allowed production placement

ACP is an Agents-owned backend concern inside the existing assembly:

```text
src/Features/Agents/
  Application/Acp/       # Zaide lifecycle, normalization, capability and action coordination
  Infrastructure/Acp/    # ACP v1 wire DTOs, codec, stdio process transport
  Contracts/             # only minimal cross-layer process/transport/binding interfaces
  Domain/                # only stable Zaide-owned ACP identity/value types
```

Rules:

- Wire DTOs and JSON-RPC framing remain under
  `Infrastructure/Acp`; they do not leak into Domain, Townhall, or
  Conversations.
- Application code consumes process/transport abstractions and the existing
  neutral Agent contracts; it does not use `System.Diagnostics.Process`,
  `System.IO`, Views, or concrete Townhall Presentation.
- App composition registers concrete infrastructure only in M5.
- No new project, assembly, root `Infrastructure/`, `UI/Shared`, provider
  registry, plugin system, or public API is introduced.
- `StreamJsonRpc`, Native Harness types, Phase 16 tools, and Townhall
  presentation types are forbidden dependencies for the protocol adapter.

---

## Identity and Townhall rules

1. Equal placement means the same Townhall navigation, conversation kind,
   draft/unread behavior, message semantics, and activity surface. No ACP-only
   side panel or inferior conversation path is allowed.
2. `ActorId`, `ConversationId`, `AgentSessionId`, and `ExecutionRunId` remain
   Zaide-owned. ACP `sessionId`, request ID, message ID, tool-call ID,
   `agentInfo`, process ID, executable path, registry ID, provider, model, and
   login account are not identity substitutes.
3. An ACP runtime binds to an existing Agent Identity only through an explicit
   user selection that records:
   - stable Zaide Actor ID;
   - ACP backend ID;
   - configured executable identity and argument profile;
   - expected registry/distribution provenance when applicable; and
   - last observed `agentInfo.name` and version as evidence, not routing keys.
4. Reconnection or process restart must not silently bind a different
   executable or `agentInfo` to an existing Actor. A mismatch fails closed and
   requires a new explicit binding.
5. Duplicate display names are allowed; routing and binding use typed IDs.
6. Existing actors remain bound to their explicitly selected backend. Absence
   or failure of ACP does not silently select Native Harness.
7. ACP message chunks become the existing assistant response only after
   protocol completion. ACP tool/plan activity remains separate structured
   activity with honest evidence level.

---

## Capability truthfulness

ACP negotiation is mapped into the existing six facts:
`Advertised`, `Available`, `Configured`, `Permitted`, `Degraded`, and
`CurrentlyUsable`.

At minimum M3/M5 must define versioned rows for:

| Zaide capability | Truth source |
|------------------|--------------|
| `MessageCompletion` | ACP v1 negotiation complete, authenticated if required, session created, prompt method usable |
| `Cancellation` | `session/cancel` supported by baseline, plus observed process/session state; request cancellation is a separate optional fact |
| `Tools` | Agent reports tool calls; this alone is backend-reported, not Zaide-mediated |
| `Permissions` | ACP permission request support is separate from Phase 17 broker availability |
| `IdeContext` | Phase 18 manifest exists and can be encoded under negotiated prompt-content capabilities |
| `Attachments` | Only negotiated content types Zaide can actually send |
| `Streaming` | ACP session updates are available; this does not claim raw model streaming |
| `Resume` / `Reconnect` | Not currently usable in Phase 20 initial profile even when advertised; Phase 21 owns product support |
| `UsageReporting` | Available only when valid `usage_update` is observed; cost is backend-reported |
| `RawTrace` | Not supported; Phase 21 |

Omitted capability means unsupported. An advertised agent capability can remain
unavailable, unconfigured, unpermitted, degraded, or not currently usable.
Capability versions increase only when the effective snapshot changes.

---

## Authentication, network, and external-execution boundary

### Authentication

- The ACP agent owns credentials, tokens, OAuth callback, browser flow, and
  provider account state.
- Zaide may display bounded advertised `authMethods` and invoke
  `authenticate(methodId)` only after explicit user action in M5.
- Zaide does not collect, proxy, persist, log, export, inject into context, or
  inspect credentials or tokens.
- Only the stable schema's agent-managed auth method is supported. Terminal
  auth and elicitation are unsupported under the pinned artifact.
- Logout is invoked only when advertised and explicitly selected. Active
  sessions after logout are treated as potentially failed or indeterminate.
- Authentication success proves only that the agent accepted the method. It
  does not prove subscription entitlement, model access, cost, or action
  mediation.

### Network

- ACP stdio transport itself performs no Zaide network request.
- The external ACP process may access the network with the OS authority of the
  launched process. Phase 20 does not claim to sandbox or mediate that traffic.
- No proxy credentials, API keys, MCP headers, or environment secrets are
  supplied by Zaide.
- Registry/document lookup is research only. Production registry discovery,
  download, auto-update, and installer behavior are out of scope.

### Process execution

- Launching a configured ACP runtime is backend transport setup, not an
  agent-requested Phase 17 action. It requires explicit executable selection
  and trust.
- Launch uses an absolute, canonical executable path plus argument vector,
  never a shell command string.
- Environment is an explicit minimal allowlist; inherited secret-bearing
  variables are denied.
- Stdout is protocol-only and bounded per frame; stderr is separately bounded
  and redacted.
- Initialization, authentication, prompt, cancellation, close, and forced
  process-tree termination have finite timeouts.
- `ApplicationShutdown` owns final exactly-once ACP process teardown.
- The runtime is not an OS sandbox. It may directly read/write files or launch
  processes outside client RPC methods. Those actions are not
  Zaide-mediated and must be labeled backend-reported, externally observed, or
  unobservable.

---

## Security and threat boundary

M1 must produce `M1_THREAT_MODEL.md` before any real process host is
implemented. It covers at least:

- malicious or compromised ACP executable and supply-chain substitution;
- stdout framing injection, oversized lines, JSON depth/number attacks,
  duplicate response IDs, response/request confusion, method spoofing, and
  malformed UTF-8;
- unbounded stderr, secret leakage, terminal escape sequences, log injection,
  and UI-thread blocking;
- forged `agentInfo`, session IDs, message IDs, tool IDs, capability claims,
  permission claims, usage/cost, plans, and tool results;
- prompt injection through ACP messages, tool output, plans, `_meta`, and
  context content;
- path traversal, absolute-path normalization, symlink escape, stale workspace
  generation, stale file revision, and direct-process workspace mutation;
- permission races and the distinction between ACP option selection and
  `AgentPermissionDecision.TryConsume()`;
- cancellation races, late updates, late responses, process exit, reconnect,
  orphan processes, and PID reuse;
- authentication phishing, malicious browser URLs, local callback risk,
  token/credential disclosure, logout ambiguity, and account confusion;
- denial of service through sessions, requests, messages, tools, files,
  processes, output, recursion, or reconnect loops;
- network and filesystem authority inherited by the external process;
- capability overstatement and false Zaide mediation language;
- cross-workspace/session/actor leakage; and
- ACP v2/draft/custom-extension downgrade or confusion.

Unknown, unverifiable, stale, or mismatched state fails closed.

---

## Scope

### In scope for Phase 20 after M0

- Pinned ACP v1 schema/codec and stdio transport.
- One internal ACP `IAgentBackend` sibling implementation.
- Explicit per-Agent-Identity backend/runtime binding.
- ACP initialize/new/prompt/update/cancel lifecycle.
- Agent-managed auth method presentation and explicit invocation boundary.
- Phase 18 context-manifest encoding under negotiated capabilities.
- Phase 17-mediated `fs/read_text_file` and `fs/write_text_file`.
- ACP permission requests as separately labeled external permission choices.
- Backend-reported tool/plan/activity normalization.
- Equal Townhall projection through the existing sole projection owner.
- Six-fact capability mapping.
- Bounded process lifecycle, shutdown, audit, adversarial tests, and optional
  separately authorized candidate smoke.

### Out of scope

- Native Harness modification, wrapping, fallback, or shared internals.
- ACP v2 or unstable schemas.
- Streamable HTTP, WebSocket, or custom ACP transports.
- ACP server implementation or public Zaide API.
- Production registry discovery, download, installation, update, or package
  management.
- Community ACP SDK adoption without a plan amendment.
- MCP server launch/configuration, MCP-over-ACP, browser tools, or dedicated
  network tools.
- `terminal: true` and the ACP terminal lifecycle.
- Direct deletion/move support not expressible through the selected client
  filesystem methods.
- Elicitation, terminal authentication, custom `_` methods, or Zaide ACP
  extensions.
- Persistent ACP session mapping, load/list/delete/resume/reconnect product
  behavior, durable memory, raw trace, usage/cost product, or interrupted-run
  recovery (Phase 21).
- Human-to-Human messaging.
- Candidate execution, authentication, credentials, network login, or paid
  use without separate activity-specific authorization.
- Phase 16 runner/corpus adoption.

---

## Milestones

| Milestone | Outcome | Depends on |
|-----------|---------|------------|
| M0 | Documentation-only live-seam audit, official protocol lock, scope, milestones, commands, rollback, and stop conditions | — |
| M1 | Stable ACP v1 schema/codec lock and threat model; no process execution | M0 accepted |
| M2 | Bounded stdio process and JSON-RPC lifecycle behind deterministic fake processes; no production DI or real agent | M1 accepted |
| M3 | ACP backend/session adapter, context mapping, event normalization, and six-fact capability mapping behind fake transport | M2 |
| M4 | Phase 17-mediated client filesystem and separate ACP permission boundary | M3 |
| M5 | Explicit identity/backend binding, production composition, equal Townhall placement, auth UI boundary, and unauthenticated local conformance proof | M4 |
| M6 | Adversarial closeout, optional separately authorized candidate smoke, full verification, and documentation closeout | M5 |

### M0 — Planning gate

**Allowed surfaces:**

- `docs/phases/v3/phase-20/IMPLEMENTATION_PLAN.md`
- `docs/phases/v3/phase-20/TOFIX.md`
- current status surfaces only where needed to say M0 planning exists while
  implementation has not started

**Artifacts:**

- this plan;
- `TOFIX.md`;
- recorded primary-source URLs, access date, claims, and ambiguity.

**Verification:**

```bash
git diff --cached --check
git diff --cached --name-only
git diff --cached --name-only -- src tests tools
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
```

The Architecture command must discover at least one test and pass with zero
failures. No full suite is required for documentation-only M0 unless the
targeted gate exposes a regression.

**Exit:** Staged scope is documentation-only; exact source lock and later
boundaries are reviewable; implementation remains not started.

### M1 — Protocol contract, schema conformance, and threat model

**Allowed production surfaces:**

- `src/Features/Agents/Infrastructure/Acp/` for internal stable v1 wire DTOs,
  JSON-RPC envelopes, codec, and newline framing
- minimal ACP value types under `src/Features/Agents/Domain/` only when they
  express Zaide-owned identity/invariants rather than wire DTOs

**Allowed test surfaces:**

- `tests/Zaide.Tests/Features/Agents/Acp/Protocol/`
- architecture inventory/count baselines required by added internal types

**Required artifacts:**

- `docs/phases/v3/phase-20/M1_SCHEMA_CONFORMANCE.md`
- `docs/phases/v3/phase-20/M1_THREAT_MODEL.md`
- frozen schema digest/version fixture generated from the already pinned
  public artifact, with no live download in tests

**Forbidden:**

- `System.Diagnostics.Process`, production DI, real agent launch, network,
  auth, UI, action broker, Townhall, Native Harness references, SDK/package
  addition, or schema auto-update

**Verification:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Protocol"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

### M2 — Stdio process and request lifecycle

**Allowed production surfaces:**

- `src/Features/Agents/Infrastructure/Acp/` for process host, bounded
  stdin/stdout/stderr, request correlation, timeouts, cancellation, and
  process-tree cleanup
- minimal interfaces under `src/Features/Agents/Contracts/` when required to
  test process/transport behavior without a real process
- `src/App/Composition/ApplicationShutdown.cs` only to add final ACP host
  teardown after M2 ownership is proven; no DI registration yet

**Allowed test surfaces:**

- `tests/Zaide.Tests/Features/Agents/Acp/Transport/`
- deterministic repository-owned fake child-process fixture under tests only
- architecture ratchets/counts

**Required artifact:**

- `docs/phases/v3/phase-20/M2_PROCESS_LIFECYCLE_EVIDENCE.md`

**Forbidden:**

- real ACP candidate, auth, paid service, production registration, Townhall,
  action methods, shell execution, inherited secret environment, network
  transport, or automatic restart

**Verification:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Transport|FullyQualifiedName~Phase20ProcessLifecycle"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

### M3 — Backend, session, context, events, and capabilities

**Allowed production surfaces:**

- `src/Features/Agents/Application/Acp/`
- `src/Features/Agents/Infrastructure/Acp/AcpAgentBackend.cs`
- `src/Features/Agents/Domain/AgentBackendIds.cs`
- additive internal ACP activity payload/kind types under Agents Domain
- narrowly additive `AgentSessionService` handling needed to normalize ACP
  streaming/session outcomes
- architecture and Phase 18 bypass ratchets

**Allowed test surfaces:**

- `tests/Zaide.Tests/Features/Agents/Acp/Backend/`
- Phase 20 context/capability/event tests

**Required artifact:**

- `docs/phases/v3/phase-20/M3_BACKEND_CONTRACT_EVIDENCE.md`

**Forbidden:**

- production DI, real process/candidate, credentials, Townhall direct write,
  client filesystem/terminal advertisement, persistent resume, Native Harness
  reference, public type growth, or Phase 21 usage/raw-trace UI

**Verification:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Backend|FullyQualifiedName~Phase20Context|FullyQualifiedName~Phase20Capabilities"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

### M4 — Zaide-mediated client actions and ACP permissions

**Allowed production surfaces:**

- `src/Features/Agents/Application/Acp/` action bridge
- existing Phase 17 payload/broker contracts as consumers only
- narrowly additive Phase 20 bypass ratchets

**Allowed test surfaces:**

- `tests/Zaide.Tests/Features/Agents/Acp/Actions/`
- focused Phase 17 regression tests; no baseline weakening

**Required artifact:**

- `docs/phases/v3/phase-20/M4_ACTION_MEDIATION_EVIDENCE.md`

**Required behavior:**

- advertise filesystem client capabilities only after bridge availability;
- no direct file/process/workspace service access;
- map read/create/replace through the run broker;
- preserve stale-base revalidation immediately before
  `AgentPermissionDecision.TryConsume()`;
- keep ACP permission choice distinct from Zaide broker authorization;
- reject `terminal/*` without advertising terminal support;
- report direct external-agent actions as non-mediated.

**Verification:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20ActionBridge|FullyQualifiedName~Phase20Permission"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase17ProposalBroker|FullyQualifiedName~Phase17PermissionLifecycle"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

### M5 — Identity binding, composition, Townhall, and auth boundary

**Allowed production surfaces:**

- `src/Features/Agents/Contracts/` minimal backend-binding contract
- `src/Features/Agents/Application/` explicit per-Actor backend selection and
  coordinator integration
- `src/Features/Agents/Domain/` typed ACP runtime/binding identity values
- `src/Features/Agents/Presentation/` only the backend/auth selection controls
  necessary for explicit user action
- `src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs`
- `src/App/Composition/Program.cs` only for composition
- `src/App/Composition/ApplicationShutdown.cs` final lifecycle ownership
- `AgentConversationEventProjection` and
  `TownhallEntryProjection` for additive structured external-activity
  projection
- `TownhallViewModel`/navigation presentation only if needed to show the same
  identity, backend, capability, auth, and disconnected state in the existing
  conversation surface

**Allowed test surfaces:**

- `tests/Zaide.Tests/Features/Agents/Acp/Integration/`
- existing Agents/Townhall/composition tests
- architecture, bypass, and ownership ratchets

**Required artifact:**

- `docs/phases/v3/phase-20/M5_INTEGRATION_EVIDENCE.md`

**Forbidden:**

- separate ACP conversation UI, automatic fallback, implicit identity
  inheritance, real login, stored credentials, registry installer, candidate
  download, persistent resume, terminal capability, or direct conversation
  store writes outside `AgentConversationEventProjection`

**Verification:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Integration"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20TownhallProjection|FullyQualifiedName~Phase20IdentityBinding"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

The conformance proof uses a repository-owned fake ACP process only. Real
authentication or provider execution is not an M5 completion requirement.

### M6 — Adversarial and release closeout

**Allowed surfaces:**

- Phase 20 production/test files already admitted by M1-M5
- architecture/bypass ratchets
- Phase 20 evidence and current status documents

**Required artifact:**

- `docs/phases/v3/phase-20/M6_CLOSEOUT_EVIDENCE.md`

**Optional external evidence:**

- one or more exact-version registry candidates may be smoke-tested only after
  separate explicit authorization for acquisition, execution, authentication,
  network, account, and cost;
- lack of that authorization is recorded as “not executed,” not a product
  failure and not permission to weaken automated conformance gates.

**Verification:**

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Adversarial"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Integration|FullyQualifiedName~Phase20TownhallProjection|FullyQualifiedName~Phase20ActionBridge"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
git diff --check
```

Every filtered gate must discover at least one test and pass with zero
failures. `No test matches` is a failed gate regardless of process exit code.

---

## Global forbidden surfaces and exclusions

Unless a milestone explicitly admits a surface above, it is forbidden:

- Phase 19 files or Native Harness contracts/loop/provider types;
- Phase 16 tools, runners, fixtures, or authorization assumptions;
- Phase 15/17/18 breaking contract changes;
- direct Editor/Workspace/ProjectSystem Infrastructure consumption;
- direct Townhall or conversation-store writes from ACP code;
- root `Infrastructure/`, `UI/Shared`, new project/assembly, public API, plugin
  architecture, or service locator;
- new NuGet/npm/cargo dependency without approved plan amendment and proof;
- test deletion, weakening, skipping, baseline masking, or parallelism
  disabling;
- credentials, secrets, inherited provider environment, paid APIs, or
  authenticated services without exact authorization;
- registry download/install/update product behavior;
- ACP v2, unstable schema, custom extension, Streamable HTTP, WebSocket, MCP
  server, terminal capability, browser/network tool, persistent resume, raw
  trace, memory, or Phase 21 behavior;
- unrelated cleanup or refactoring.

---

## Stop conditions

Stop and ask before continuing when any of these occurs:

1. A required official source becomes unavailable, authentication-gated, or
   inconsistent with the pinned stable release.
2. A stable protocol/schema upgrade is proposed after M0.
3. A community/official SDK or any new dependency becomes necessary.
4. ACP implementation requires a breaking Phase 15, 17, or 18 contract change.
5. Full ACP terminal support is required; the current synchronous broker
   command contract cannot truthfully satisfy the terminal-ID lifecycle.
6. Any ACP file/workspace/process operation bypasses `IAgentActionBroker`.
7. `TryConsume()` would cease to be the final authorization step, or a stale
   proposal would consume a published decision.
8. ACP permission selection is proposed as Zaide broker authorization.
9. Direct external-agent activity would be labeled Zaide-mediated or
   Zaide-executed.
10. The process host needs shell interpolation, inherited secret environment,
    unbounded output, or lacks process-tree cleanup.
11. An ACP runtime would be silently rebound to an Actor or silently fall back
    to Native Harness.
12. Authentication, browser login, credentials, network access, candidate
    acquisition/execution, paid service, or account entitlement is needed
    without exact authorization.
13. A test/build/architecture gate fails and correction would exceed the
    current milestone.
14. Phase 19 history would need modification or Phase 21 work would need to
    start.
15. A destructive or irreversible action is proposed.

---

## Exit conditions

### M0 exit

- [x] Exact staged documentation scope verified.
- [x] `git diff --cached --check` clean.
- [x] `git diff --cached --name-only -- src tests tools` empty.
- [x] Build succeeds with `--no-restore`.
- [x] Architecture filter discovers tests and passes with zero failures.
- [x] One documentation commit is published to `origin/master` at
      `0bb44c85b743dee9dc1c8f18553097fd4d4a8ca7`.
- [x] Working tree is clean and `HEAD == origin/master`.
- [x] Phase 20 remains M0 planning / implementation not started. *(M0
      historical exit evidence only; current status is M2 complete and
      published.)*
- [x] Phase 19 remains accepted and closed.
- [x] Phase 21 has not started.

### M1 exit

- [x] Pinned `schema-v1.20.0` fixtures and digest conformance tests pass.
- [x] ACP v1 JSON-RPC envelopes, wire DTOs, codec, and newline framing
      implemented.
- [x] Pure protocol session plumbing (`initialize`, `session/new`,
      `session/prompt`, `session/cancel`, `session/update`, `$/cancel_request`)
      implemented.
- [x] Truthful M1 client capability profile (`terminal: false`, filesystem
      flags false).
- [x] `M1_SCHEMA_CONFORMANCE.md` and `M1_THREAT_MODEL.md` recorded.
- [x] Focused `Phase20Protocol` tests and architecture inventory ratchets pass.
- [x] No `System.Diagnostics.Process`, production DI, broker bridge,
      Townhall/UI, authentication, network provider execution, Native Harness
      reference, or new dependency.
- [x] One reviewable M1 commit published to `origin/master` at
      `314076ebc8dcf2c9910baecc5ef96c461910cb1b`.
- [x] Working tree is clean and `HEAD == origin/master`.
- [x] M2 and later Phase 20 milestones remain not started and not authorized.
- [x] Phase 19 remains complete, published, accepted, and closed.
- [x] Phase 21 has not started.

### M2 exit

- [x] Bounded stdio process host owns one child process, protocol session, and stderr reader.
- [x] Request correlation, timeouts, cancellation, process exit, and late-response counting implemented.
- [x] Exact process-tree cleanup and `ApplicationShutdown` host teardown proven by transport tests.
- [x] Deterministic repository-owned fake child-process fixture and `M2_PROCESS_LIFECYCLE_EVIDENCE.md` recorded.
- [x] Focused `Phase20Transport` / `Phase20ProcessLifecycle` tests and architecture inventory ratchets pass.
- [x] No production DI, real ACP candidate, broker bridge, Townhall/UI, authentication, network provider execution, Native Harness reference, automatic restart, or M3+ surfaces.
- [x] One reviewable M2 commit published to `origin/master` at
      `880b4524c9c53190687aee0cc10843900191b8ce`, with publication record
      `6b197a8b`.
- [x] Working tree is clean and `HEAD == origin/master`.
- [x] M3 and later Phase 20 milestones remain not started and not authorized.
- [x] Phase 19 remains complete, published, accepted, and closed.
- [x] Phase 21 has not started.

### M3 exit

- [x] `AcpAgentBackend` implements `IAgentBackend` with independent `backend:acp` identity.
- [x] Phase 18 context consumed only through `AcpContextManifestEncoder`.
- [x] ACP `session/update` activity normalized separately from assistant completion.
- [x] Six-fact capability rows and versioned usage observation implemented.
- [x] Deterministic fake transport tests and `M3_BACKEND_CONTRACT_EVIDENCE.md` recorded.
- [x] Focused `Phase20Backend` / `Phase20Context` / `Phase20Capabilities` tests and architecture ratchets pass.
- [x] One reviewable M3 commit published at `2d90604991dd9b87cb6e22a2c8c9a7b771504de6` to `origin/master`, with publication-record correction `04831f1c`.
- [x] Working tree is clean and `HEAD == origin/master`.
- [ ] M4 and later Phase 20 milestones remain not started and not authorized.
- [ ] Phase 19 remains complete, published, accepted, and closed.
- [ ] Phase 21 has not started.

### Phase exit after M6

- ACP v1 `schema-v1.20.0` conformance and stdio lifecycle gates pass.
- Explicit Actor/backend/runtime binding and no-fallback behavior pass.
- Phase 18 context is used only through the manifest.
- Client filesystem methods are broker-mediated; terminal remains unadvertised.
- Townhall placement is equal and uses the sole projection owner.
- Capabilities and evidence levels are truthful.
- Authentication and external-process limitations are visible.
- Threat-model/adversarial gates and full fast/serial suites pass.
- No Phase 21 behavior is claimed.
- Final human acceptance remains a separate gate.

---

## Rollback plan

**M0 baseline:** `4e0c89162b33547e3461aa4b2f845bb7cbbb1314`.

M0 is documentation-only. If rejected, revert the single M0 documentation
commit; no source/test/tool/dependency rollback is needed.

Later milestones use one reviewable commit per coherent outcome. Roll back with
`git revert <milestone-commit>` in reverse milestone order:

- M6: remove closeout/ratchet changes only.
- M5: remove ACP production registration, explicit binding/UI/projection, and
  restore the pre-M5 Native Harness selection behavior; never retain an
  automatic fallback.
- M4: stop advertising client filesystem capabilities and remove the ACP action
  bridge; Phase 17 broker remains unchanged.
- M3: remove ACP backend/normalization/capability types; neutral Phase 15/18
  seams remain unchanged.
- M2: terminate/remove ACP process host and shutdown ownership.
- M1: remove codec/schema fixtures/threat-model implementation artifacts and
  restore architecture counts.

After every revert, run the owning milestone gates plus Architecture. If an ACP
process might be live, terminate its exact owned process tree before reverting
composition. Never use a broad or unresolved process target.

---

## M0 next gate (historical)

Stop after publishing this documentation at the read-only M0 audit gate. The
next possible action is review of this plan. Do not begin M1 without a separate
authorization. *(Historical M0 evidence; M1 is complete and published at
`314076ebc8dcf2c9910baecc5ef96c461910cb1b`; M2 complete and published at
`880b4524c9c53190687aee0cc10843900191b8ce` with publication record
`6b197a8b`; M3 complete and published at
`2d90604991dd9b87cb6e22a2c8c9a7b771504de6` with publication-record correction
`04831f1c`. M4 and all later milestones are not started.)*
