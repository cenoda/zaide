# Refactor 7: Agent and Conversation Domain — Implementation Plan

## Status and authorization

**Refactor 7 status:** **M5b accepted (2026-07-19). M6 only authorized, not
implemented.** M1 accepted at `edc5dac`. M2 accepted at `94a609f`. M3 accepted
at `0902641`. M4 accepted at `38418ed`. M5a accepted at `d3bf701`. M7,
Refactor 8, and Phase 14 remain unauthorized.

This document is the accepted Refactor 7 M0 planning gate. It audits the live
Agent/Townhall behavior at `e597972`, locks the intended boundaries and
milestone order, and defines verification commands. Human acceptance on
2026-07-19 closed **M1** at commit `edc5dac`, **M2** at commit `94a609f`, and
**M3** at commit `0902641` (implementation `674b3cf` plus authorship closeout),
and **M4** at commit `38418ed` (implementation `d1e7f3f` plus routing-failure and
result-invariant hardening `3a318cf`), and **M5a** at commit `d3bf701`
(implementation `b9dea42` plus run-correlation hardening `8ce1e07` and
correlation-id invariant hardening `d3bf701`), and **M5b** at commit `e284ecc`
(implementation `e284ecc` including projection-disposal lifecycle hardening).
**M6 only** is authorized as the next separately verifiable implementation
milestone. M7, Refactor 8, and Phase 14 remain unauthorized.

**Dependency status:** Refactor 6.1, Refactor 6.2, and Refactor 6.3 are
accepted and closed. Refactor 6.3's lifetime map and feature-first composition
modules are the live structural baseline.

## M0 live-code audit

### Current ownership and execution path

| Concern | Live owner / path | M0 finding |
|---------|-------------------|------------|
| Agent identity | `AgentPanelHost` seeds raw `(AgentId, AgentName, AvatarResourceKey)` tuples; `AgentPanelState` copies mutable strings; `TownhallViewModel` separately seeds `WorkspaceAgent` identities | Identity is duplicated, stringly typed, and panel/presentation-owned. `alpha`/`beta` panel IDs and `agent-1` Townhall IDs do not describe one stable identity source. |
| Agent panel lifetime | Singleton `IAgentPanelHost` owns `AgentPanelState` instances from create until close | A panel is presentation state, not an Agent Identity or resumable Agent Session. Closing a panel intentionally does not stop an in-flight request. |
| Direct execution | `AgentExecutionCoordinator.SendAsync` owns per-panel in-flight exclusion, appends `"User: ..."`, `"Assistant: ..."`, or `"Error: ..."` strings, and drives string status values | One concrete execution attempt exists, but it has no run ID, typed outcome/event sequence, or independent owner. |
| Routing | `MentionParser` and `AgentRouter` resolve targets by visible mutable agent names, then route by panel ID | Existing mention behavior must remain, but typed identity must replace name as the post-resolution target key. Mention syntax and visible-name matching are not redesigned here. |
| Townhall conversations | Singleton `TownhallState` owns `Channels`, `ActiveChannelId`, and `ChannelMessages: Dictionary<string, ObservableCollection<TownhallMessage>>` | Per-channel collections retain history, but there is no authoritative `Conversation` aggregate, typed `ConversationId`, kind, or participants. Selection and ownership are coupled through `ActiveChannelId`. |
| Message/event model | `TownhallMessage` stores mutable raw sender IDs/names plus a broad `TownhallMessageKind`; panel output is a second `ObservableCollection<string>` protocol | Townhall and Agent Panel keep separate records. Current status/output prefixes are interpreted after execution. Typed existing-behavior events are required before UI projection rewiring. |
| Agent-to-Townhall mirroring | `AgentTownhallMirrorCoordinator` writes the user entry before awaiting routing, then inspects panel status and the final output string to synthesize response/error activity | The flow can attribute the response to whichever public channel is active after the await. This is the named active-channel attribution defect. |
| Composition | `AddZaideAgents` and `AddZaideTownhall` register application singletons; `AgentTownhallMirrorCoordinator` is constructed by `MainWindowViewModel` | Refactor 7 may add registrations only for concrete domain/application owners introduced by its milestones. It must not create speculative DI scopes. |
| Persistence | Agent panels, Townhall state, conversations, drafts, and execution state are in-memory only | Persistence, restart recovery, backend resume, migration, replay, unread state, and deletion semantics remain Phase 14 M0 decisions. |

### Current behavior that must remain observable

- Creating, activating, and closing Agent Panel tabs retains the current UI
  behavior, including panel-local drafts and output history.
- Direct sends and the existing single-mention route execute against the same
  target panel and configured non-streaming backend.
- One request per panel may be in flight; busy/idle/error presentation and
  draft clearing remain unchanged.
- User, assistant, routing-failure, and execution-error entries remain visible
  through the current Agent Panel and Townhall surfaces.
- Townhall channel selection, channel-local history, filtering, seeded content,
  and direct Townhall sends remain visually and behaviorally unchanged.
- Cancellation and exception behavior remain covered by existing tests; no
  retry, stop-agent, resume, streaming, or tool behavior is added.

### M0 domain decisions

| Decision | Locked Refactor 7 rule |
|----------|------------------------|
| Stable identity | Introduce a typed `ActorId` and the minimum stable Actor/Agent Identity representation needed by current seeded human/agent identities. Identity is not a panel, provider, model, runtime, session, or display name. Existing display name/avatar data may remain mutable presentation/profile data. |
| Actor neutrality | Conversation participants use Actor identity and do not hard-code every direct conversation as Human-to-Agent. Refactor 7 will exercise only existing Human-to-Agent and channel behavior; Human-to-Human UI/flows are not added. |
| Conversation owner (`R61-LT01`) | Accept. A `Conversation` with a typed `ConversationId`, kind, participants, and ordered typed entries becomes the authoritative in-memory owner. Channel selection is a projection key and never determines entry ownership after admission. |
| Agent session (`R61-LT02`) | **Explicitly defer.** The live product has no backend resume token, session restoration, session-switch consumer, or lifecycle distinct from panel/application state. Refactor 7 must not rename `AgentPanelState` to session or introduce `AgentSessionScope`. Phase 14 M0 or a later backend phase must prove this owner. |
| Execution run (`R61-LT03`) | Accept only the minimum current-behavior representation. One admitted send/routed execution receives a typed run ID and terminal outcome/event sequence sufficient to correlate the existing user, assistant, routing-failure, execution-error, and cancellation paths. No tool invocation, run persistence, resume, retry, budget, or capability model is authorized. |
| Session cardinality | The V3 candidate “one session attaches to one conversation” remains a future constraint, not a Refactor 7 type. It cannot be accepted in code until a concrete Agent Session owner exists. Nothing in Refactor 7 may make that later cardinality impossible. |
| Panel projection | `AgentPanelState` remains a temporary presentation projection. Its `PanelId`, selected tab, draft, and close behavior are not domain identity. Panel output must eventually project typed conversation/run entries rather than own an independent string truth. |
| Public mirror | Preserve the currently visible public Townhall mirror during this refactor. Removing implicit public mirroring or exposing private direct-conversation navigation is Phase 14 behavior, not hidden refactor work. |
| Attribution exception | Correct the active-channel attribution defect in one named milestone: capture the target public `ConversationId` when a send is admitted and write all mirrored entries for that attempt to it even if selection changes while awaiting. This is the only pre-approved observable correctness change. |
| Typed events | Replace `"User: "`, `"Assistant: "`, `"Error: "`, and string status interpretation at orchestration boundaries with structured existing-behavior entries/outcomes. Rendering may preserve the exact current strings. Speculative `AgentThink`, `ToolCall`, `ToolResult`, raw trace, and backend-capability producers are excluded. |
| Storage and lifetime | The new owners are in-memory and application-owned unless a milestone proves a narrower existing owner. No persistence store, child DI container, or conversation/session scope is introduced. |

### M1 canonical identity policy

Refactor 7 does not pretend that the existing Townhall roster's `Zaide Agent`
and an Agent Panel's `Alpha` are the same actor. M1 creates one typed Actor
catalog containing distinct canonical rows and lets the two existing
presentation catalogs select from it.

| Canonical ActorId | Kind | Existing display data | Existing consumer | Migration rule |
|-------------------|------|-----------------------|-------------------|----------------|
| `human:user-1` | Human | Legacy surface ID `user-1`; `User`, `avatar-user` | Townhall sends, panel-send author, public mirror | Replace every `"user-1"`/`"User"` orchestration hardcode with this typed canonical row; projected sender ID and rendered values do not change. |
| `townhall-agent:agent-1` | Agent | Legacy surface ID `agent-1`; `Zaide Agent`, `avatar-agent` | Seeded Townhall roster only | Preserve as a distinct actor. Do not alias it to Alpha or any panel solely because it is the only seeded Townhall agent. |
| `panel-seed:alpha` | Agent | Legacy panel agent ID `alpha`; `Alpha`, `Icon.Avatar` | First seeded Agent Panel and its direct conversation | Preserve projected ID and display data through the typed catalog. |
| `panel-seed:beta` | Agent | Legacy panel agent ID `beta`; `Beta`, `Icon.Avatar` | Second seeded Agent Panel and its direct conversation | Preserve projected ID and display data through the typed catalog. |
| `panel-seed:gamma` | Agent | Legacy panel agent ID `gamma`; `Gamma`, `Icon.Avatar` | Third seeded Agent Panel and its direct conversation | Preserve projected ID and display data through the typed catalog. |
| `panel-seed:delta` | Agent | Legacy panel agent ID `delta`; `Delta`, `Icon.Avatar` | Fourth seeded Agent Panel and its direct conversation | Preserve projected ID and display data through the typed catalog. |
| `panel-fallback:N` | Agent | Legacy panel agent ID and name `agent-N` / `Agent N`; `Icon.Avatar` | Fallback Agent Panels after the four fixed seeds | The typed namespace prevents collision with the Townhall row while the existing projected fallback ID/name remain unchanged. |
| `panel-custom:<agentId>` | Agent | Caller-supplied legacy panel agent ID, name, and avatar | Public custom `CreatePanel(...)` path | Register or resolve dynamically under the custom namespace using the contract below. It cannot collide with Townhall, fixed-seed, or fallback actors solely because a legacy ID string matches. |

`ActorId` equality is ordinal and exact. Display names are not keys. M1 may
retain legacy string-shaped projection properties temporarily for compatibility,
but their value must be derived from the canonical Actor row; no second seed
table or independently mutable identity copy may remain after M1. M1 introduces
identity types and seed consumption only. It does **not** change mention syntax,
name matching, `RouteRequest`, or routing targets.

#### Public custom-panel identity contract

M1 retains the public
`CreatePanel(string agentId, string agentName, string avatarResourceKey)`
overload and migrates its consumers; it is not replaced with a typed-only
overload in Refactor 7.

1. Normalize the supplied legacy `agentId` into the opaque typed key
   `panel-custom:<agentId>` without changing the caller-visible/projected
   `AgentId`. Actor IDs are constructed through one factory; callers do not
   concatenate the prefix themselves.
2. If the typed key is absent, atomically register an Agent actor using the
   supplied legacy ID, display name, and avatar, then create the panel from the
   registered row.
3. If the typed key exists with the same kind, legacy ID, display name, and
   avatar, resolve and reuse that Actor. Multiple panels may project the same
   canonical Actor.
4. If the typed key exists with conflicting identity/profile data, throw
   `ArgumentException` before allocating a `PanelId`, conversation, or mutating
   the host/store. First registration wins; silent overwrite, duplicate Actor
   rows, and caller-specific mutable copies are forbidden.
5. Preserve the live overload's acceptance of arbitrary non-colliding values,
   including `agent-x`; M1 adds no new whitespace/format validation beyond the
   explicit collision rule.

Focused M1 tests must cover new custom registration, identical reuse, conflicting
collision with zero partial mutation, arbitrary `agent-x`, and typed-namespace
separation when a custom legacy ID equals `agent-1`, `alpha`, or a fallback ID.

### Type placement and composition policy

| Concept | Locked home | Layer / visibility | Composition owner |
|---------|-------------|--------------------|-------------------|
| `ActorId`, actor kind, Actor/Agent Identity record | `Features/Conversations/Domain` | Public only where an existing cross-feature contract requires it; otherwise internal | `AddZaideConversations` owns the canonical in-memory Actor catalog. |
| `ConversationId`, conversation kind, participant membership, `Conversation`, typed conversation entries | `Features/Conversations/Domain` | Agent-neutral domain; no Avalonia, ViewModel, panel, channel-selection, backend, or DI dependency | Not registered individually. Owned by the conversation store. |
| Conversation store contract | `Features/Conversations/Contracts` | Narrow application-facing contract for create/get/targeted append required by current flows | Registered by `AddZaideConversations`. |
| In-memory conversation store | `Features/Conversations/Application` | Application-lifetime implementation; owns ordered conversations/entries | Registered once by `AddZaideConversations`. |
| Projection mapping/formatting shared by current conversation surfaces | `Features/Conversations/Application` | Typed input to current rendered values; no View ownership | Register only if stateful; pure formatters remain unregistered. |
| Execution run ID, outcome, and current-behavior execution events | `Features/Agents/Domain` | Agent-execution domain; may reference typed Actor/Conversation IDs but no Townhall/Presentation types | Created per admitted send; not a DI service or scope. |
| Agent execution/routing orchestration | Existing `Features/Agents/Application` and `Contracts` | Existing ownership retained | Existing `AddZaideAgents`. |
| Townhall and Agent Panel projections | Existing feature `Presentation` homes | UI/presentation only; consume application contracts and typed projection data | Existing `AddZaideTownhall` / `AddZaideAgents` registrations remain. |

`Features/Conversations` is the agent-neutral home because both Agents and
Townhall consume it; Townhall does not own the domain and Agents must not own
public-channel conversations. Add `AddZaideConversations()` after
`AddZaideAppCore()` and before `AddZaideAgents()`/`AddZaideTownhall()` in the
composition root. Existing modules may depend on its contracts; the new module
must not depend on Agents, Townhall, App shell, or Presentation namespaces.
Architecture and registration tests must enforce this direction and the exact
single registration call.

### Panel-backed direct conversation policy

- `AgentPanelHost.CreatePanel` provisions exactly one in-memory conversation
  when it creates the panel; first send does not lazily create it.
- The conversation kind is `Direct`. `Panel` is a presentation association,
  not a conversation kind.
- Participants are canonical Actor `human:user-1` and the canonical Actor selected
  for that panel. The model remains actor-neutral and does not encode a fixed
  Human-to-Agent pair in its type system.
- The panel holds/references the typed `ConversationId`; `PanelId` remains a
  separate presentation key.
- A panel send records the authoritative user and terminal execution entries
  in its direct conversation. To preserve existing V2 visibility, the same
  admitted send also writes the current public mirror shapes to the public
  channel conversation captured at admission. This is explicit two-target
  projection, not two authoritative copies of one mutable message collection.
- Closing a panel removes only the projection. Its in-memory conversation is
  retained by the application-lifetime store until application shutdown. No
  archive/delete/reopen UI is added; persistence and recovery remain Phase 14.
- M2 establishes conversations and provisioning while legacy
  `OutputHistory` remains. M5a begins the panel projection cutover; M5b removes
  legacy string ownership only after parity is proven.

### Existing cancellation contract

Cancellation semantics are intentionally uneven in the live paths: the mirror
writes the user entry before routing and propagates `OperationCanceledException`
without a terminal mirror, while lower execution/coordinator paths may represent
cancellation as a failure/error depending on where it is observed. M4 must
encode and preserve the currently tested outcome at each boundary. Normalizing
cancellation, adding a new cancelled UI state, or changing retry behavior is
not authorized by this refactor.

## Scope

**Goal:** Establish the minimum typed Agent/Conversation domain required to
represent existing V2 behavior faithfully: stable actor/agent identity,
authoritative in-memory conversation/message ownership, correlated execution
runs, and typed existing output/outcome flow. Rewire the existing UI as a
projection without redesigning it.

**In scope:**

- typed IDs and minimum current Actor/Agent Identity records;
- one authoritative in-memory conversation owner for current channels and
  panel-backed direct history;
- typed current-behavior conversation entries and execution outcomes;
- a minimal execution-run correlation identity and terminal state;
- target-by-ID routing after existing mention-name resolution;
- migration of existing Agent Panel/Townhall orchestration to the typed model;
- the named active-channel attribution correction;
- focused domain, orchestration, composition, architecture, and regression
  tests;
- documentation truth-sync as milestones close.

**Out of scope:**

- any visible Townhall/Agent Panel redesign or new direct-conversation
  navigation;
- retirement of the Agent Panel or resolution of `DF-001`;
- persistence, database/schema migration, restart recovery, replay,
  deduplication, unread/read state, drafts migration, or deletion policy;
- resumable Agent Session or Runtime types, backend session tokens, ACP,
  native harness, backend capabilities, streaming, retry, or reconnect;
- tool invocation, permission, audit, memory, raw trace, context-manifest, or
  policy-assignment models;
- new routing grammar, multiple mentions, conversation branching, reply
  threads, or Agent-to-Agent product behavior beyond preserving the existing
  mention route;
- public-mirror removal, privacy UX, or other Phase 14 behavior;
- Refactor 8 view/component extraction or design-token work;
- unrelated cleanup, broad renaming, assembly splitting, or package changes.

## Dependencies and ordering

1. Refactor 6.1–6.3 must remain accepted and their architecture/lifetime
   ratchets must stay green.
2. Typed identity precedes conversation participants and ID-based routing.
3. Conversation ownership precedes projection rewiring and attribution repair.
4. Typed entries/outcomes and minimal run correlation precede removal of
   string inspection from orchestration.
5. `RouteRequest` remains name-shaped through M3. M4 is the only milestone
   authorized to replace its resolved target with typed Actor/panel identity
   after the existing mention parser completes the same visible-name match.
6. The Agent Panel projection migrates only after the authoritative model can
   represent all existing visible output paths.
7. The attribution exception lands only after targeted conversation writes
   exist and has its own regression tests.
8. Public-surface and architecture ratchets tighten only after the final
   production shape is known.

No milestone may begin before the prior milestone or named slice is accepted.
If a milestone exceeds one agent session, split it into `MNa`/`MNb` slices
without expanding its concern.

## Milestones

| Milestone | Bounded outcome | Verification gate |
|-----------|-----------------|-------------------|
| **M0** | Audit live Agent/Townhall ownership; lock decisions, scope, dependencies, milestones, rollback points, and commands. Documentation only. | `git diff --check`; plan-path/status review; no production/test diff |
| **M1** | Introduce typed Actor/Agent identity, the locked canonical seed table, and the Actor catalog. Replace identity hardcodes/copies without changing rendered names, avatars, panels, mention parsing, `RouteRequest`, or routing behavior. | Build; focused identity/catalog + existing panel/Townhall seed tests; registration/Architecture tests; full suite — **accepted `edc5dac`** |
| **M2** | Introduce the agent-neutral authoritative in-memory `Conversation` owner, typed ID/kind/participants, targeted store contract, channel conversations, and create-time panel direct conversations under the locked retention/dual-target policy. Preserve channel/panel presentation and both legacy collections for migration. | Build; focused Conversation/store/provisioning + Townhall domain/ViewModel + panel lifecycle tests; registration/Architecture tests; full suite — **accepted `94a609f`** |
| **M3** | Introduce a narrower typed conversation-entry model and current-rendering projection for chat, response, routing-error, execution-error, channel-event, and system paths. Keep `TownhallMessageKind` as a presentation compatibility enum; do not promote its unused `AgentThink`, `ToolCall`, or `ToolResult` values into domain types/producers, and leave unused `SourceProvider`, `SourceModel`, `ThreadId`, and `Metadata` fields alone. | Build; focused entry invariants, formatting, exact prefix/content, grouping/filtering, and Townhall projection tests; Architecture tests; full suite — **accepted `0902641`** |
| **M4** | Introduce the minimal correlated execution-run representation and make coordinator/router results structured. After unchanged visible-name parsing, replace `RouteRequest.TargetAgentName` with a resolved typed Actor/panel target here only. Preserve one-in-flight-per-panel and the existing uneven cancellation, status, draft, and backend behavior. | Build; focused execution coordinator/router/service tests including success, failure, cancellation at each boundary, concurrency, and target identity; Architecture tests; full suite — **accepted `38418ed`** |
| **M5a** | Dual-write authoritative typed direct-conversation/run entries and project them into the Agent Panel while retaining `OutputHistory` as a compatibility path. Preserve exact rendered prefixes/order, tab lifecycle, drafts, focus/input behavior, and routing. | Build; focused Agent Panel projection + host/view lifetime + routing tests; Architecture tests; full suite; manual panel smoke — **accepted `d3bf701`** |
| **M5b** | Prove typed-vs-legacy output parity across success, routing failure, execution failure, cancellation, switching, and close-during-flight cases; then remove duplicate string history ownership and its dual-write path. No other UI or lifecycle change. | Build; focused parity/lifetime tests; Architecture tests; full suite; repeat manual panel smoke — **accepted `e284ecc`** |
| **M6** | Capture the public channel `ConversationId` at send admission, then target both the pre-await user write and every terminal response/error/routing-failure write to that same ID. Add the previously missing switch-during-await regression proving no mirrored request or terminal agent entry lands in the newly selected channel; its normal `ChannelEvent` remains allowed. Preserve exact current mirrored content/prefix shapes and public visibility. | Build; focused mirror + MainWindowViewModel + Townhall tests including switch-during-await, allowed switch event, and exact mirrored content; Architecture tests; full suite; manual channel-switch smoke |
| **M7** | Delete superseded string protocols/duplicate identity paths, tighten architecture/public-surface ratchets, update architecture/conventions/status docs, and close only after automated and required manual evidence is truthful. | Build; all focused suites; Architecture tests; full suite; `git diff --check`; manual evidence review |

## Verification commands

Run from the repository root. Milestone branches must start from an accepted,
clean predecessor.

### Baseline and change scope

```bash
git status --short --branch
git rev-parse --short HEAD
git rev-parse --short origin/master
git diff --name-only <accepted-predecessor>...HEAD
git diff --check
```

For M0, the expected changed production/test file count is zero:

```bash
git diff --name-only | rg '^(src|tests)/' && exit 1 || true
test -f docs/refactor/refactor-7/IMPLEMENTATION_PLAN.md
```

### Build and automated tests

```bash
dotnet build Zaide.slnx --no-restore
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Features.Conversations|FullyQualifiedName~Features.Agents|FullyQualifiedName~Features.Townhall|FullyQualifiedName~App.Shell.AgentTownhallMirrorCoordinator|FullyQualifiedName~App.Shell.MainWindowViewModel'
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Zaide.Tests.Architecture'
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build
```

If a milestone changes DI registration, also run:

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~RegistrationModuleTests|FullyQualifiedName~CompositionDiIntegrationTests'
```

This DI gate includes the Agents, Townhall, and Conversations registration
modules whenever their registrations or composition order change.

Do not substitute filtered tests for the full-suite exit gate. Record exact
pass/fail/skip totals at each accepted milestone.

### Required manual checks

M5 manual panel smoke:

1. Create at least two agent panels and verify names/avatars/tabs remain
   stable.
2. Direct-send from one panel and route one existing `@AgentName` message.
3. Verify busy/idle/error, draft clearing, output ordering, tab switching, and
   close behavior match the accepted baseline.

M6/M7 manual attribution smoke:

1. Start a delayed agent send while Townhall channel A is selected.
2. Switch to channel B before the response completes.
3. Verify the mirrored request and terminal response/error remain in channel A
   and no mirrored request or terminal agent entry appears in channel B. The
   existing channel-switch `ChannelEvent` in B is expected and allowed.
4. Verify the existing public mirror remains visible and no direct-message
   navigation or visual redesign was introduced.

Manual backend smoke requires a configured test endpoint and must never expose
credentials in logs or evidence. If it cannot be run, record it as not run;
automated proof must still cover the ownership/attribution contract.

## M3 verification (2026-07-19, accepted)

- Accepted at commit `0902641` after review closeout (implementation `674b3cf`
  plus authorship fix on `0902641`).
- Build: `dotnet build Zaide.slnx --no-restore` — succeeded (0 errors, 4 pre-existing warnings).
- Focused gate: **326 passed**, 0 failed, 0 skipped.
- Registration/DI gate: **67 passed**, 0 failed, 0 skipped.
- Architecture gate: **22 passed**, 0 failed, 0 skipped.
- Full suite: **2408 passed**, 0 failed, 0 skipped.
- `git diff --check` — clean.
- Authorship fix: typed `ActorId` is passed into Townhall entry admission; legacy
  `TryGetByProjectedLegacyId` reverse lookup removed.

## M2 verification (2026-07-19, accepted)

- Accepted at commit `94a609f` after review closeout (includes invariant hardening
  on `94a609f` atop implementation `7c15c06`).
- Build: `dotnet build Zaide.slnx --no-restore` — succeeded (0 errors, 4 pre-existing warnings).
- Focused gate: **282 passed**, 0 failed, 0 skipped.
- Registration/DI gate: **67 passed**, 0 failed, 0 skipped.
- Architecture gate: **22 passed**, 0 failed, 0 skipped.
- Full suite: **2364 passed**, 0 failed, 0 skipped.
- `git diff --check` — clean.

## M1 verification (2026-07-19, accepted)

- Accepted at commit `edc5dac` after review closeout.
- Build: `dotnet build Zaide.slnx --no-restore` — succeeded (0 errors, 4 pre-existing warnings).
- Focused gate: **321 passed**, 0 failed, 0 skipped.
- Architecture gate: **22 passed**, 0 failed, 0 skipped.
- Full suite: **2336 passed**, 0 failed, 0 skipped.
- `git diff --check` — clean.

## Entry conditions for M3

- [x] M2 authoritative conversation owner, store contract, channel provisioning,
      and create-time panel direct-conversation provisioning are accepted at
      `94a609f`.
- [x] Human accepted M2 closeout on 2026-07-19; **M3 only** is authorized.
- [x] M3 accepted at `0902641` on 2026-07-19.

## Entry conditions for M4

- [x] M3 typed conversation-entry model, store append contract, Townhall
      compatibility projection, and focused regression tests are accepted at
      `0902641`.
- [x] Human accepted M3 closeout on 2026-07-19; **M4 only** is authorized.
- [x] M4 implementation complete on 2026-07-19; pending human acceptance. M5–M7, Refactor 8, and Phase 14 remain unauthorized.
- [x] M4 accepted on 2026-07-19; **M5a only** is authorized. M5b–M7, Refactor 8, and Phase 14 remain unauthorized.

## M4 verification (2026-07-19, accepted)

- Accepted at commit `38418ed` after review closeout (implementation `d1e7f3f` plus
  routing-failure correlation and result-invariant hardening `3a318cf`).

- Build: `dotnet build Zaide.slnx --no-restore` — succeeded (0 errors, 4 pre-existing warnings).
- Focused gate: **352 passed**, 0 failed, 0 skipped.
- Registration/DI gate: **67 passed**, 0 failed, 0 skipped.
- Architecture gate: **22 passed**, 0 failed, 0 skipped.
- Full suite: **2434 passed**, 0 failed, 0 skipped.
- `git diff --check` — clean.
- Structured coordinator/router results carry `ExecutionRunId`, typed target identity, terminal outcome, and assistant/error payloads. Routing failures correlate a `RoutingFailure` run on the source panel without coordinator execution. `AgentExecutionCoordinatorResult` enforces outcome/payload invariants at construction. `RouteRequest` no longer exposes `TargetAgentName`. `AgentTownhallMirrorCoordinator` consumes structured results instead of parsing `Status`/`OutputHistory`. `OutputHistory` remains the panel compatibility truth; no M5 direct-conversation dual-write.

## Entry conditions for M5a

- [x] M4 minimal execution-run correlation, structured coordinator/router
      results, typed routing targets, routing-failure run correlation, and
      result-invariant enforcement are accepted at `38418ed`.
- [x] Human accepted M4 closeout on 2026-07-19; **M5a only** is authorized.
- [x] M5a implementation complete on 2026-07-19; pending human acceptance. M5b–M7, Refactor 8, and Phase 14 remain unauthorized.
- [x] M5a accepted on 2026-07-19; **M5b only** is authorized. M6–M7, Refactor 8, and Phase 14 remain unauthorized.

## M5a verification (2026-07-19, accepted)

- Accepted at commit `d3bf701` after review closeout (implementation `b9dea42` plus
  run-correlation hardening `8ce1e07` and correlation-id invariant hardening
  `d3bf701`).
- Build: `dotnet build Zaide.slnx --no-restore` — succeeded (0 errors, 4 pre-existing warnings).
- Focused gate: **393 passed**, 0 failed, 0 skipped.
- Registration/DI gate: **67 passed**, 0 failed, 0 skipped.
- Architecture gate: **22 passed**, 0 failed, 0 skipped.
- Full suite: **2475 passed**, 0 failed, 0 skipped.
- `git diff --check` — clean.
- Manual M5 panel smoke: **not run** (no configured test endpoint in this session).
- `AgentExecutionCoordinator` dual-writes authoritative typed user/terminal entries into each admitted attempt's target direct `ConversationId` via `IConversationStore`, correlating both entries with the same `ExecutionRunId` through the agent-neutral `ConversationEntryCorrelationId` seam (`ExecutionRunCorrelation`). `ConversationEntry` rejects present-but-default correlation ids while allowing omitted/`null` uncorrelated entries. Legacy `OutputHistory` is projected from the same typed entries via `AgentPanelEntryProjection`. Routing failures without coordinator execution remain Townhall-only with no direct-conversation or `OutputHistory` mutation. `OutputHistory` ownership is retained for M5b cutover.

## Entry conditions for M5b

- [x] M5a authoritative typed direct-conversation dual-write, run-correlated
      entries, legacy `OutputHistory` projection, and correlation-id invariants
      are accepted at `d3bf701`.
- [x] Human accepted M5a closeout on 2026-07-19; **M5b only** is authorized.
- [x] M5b implementation complete on 2026-07-19; pending human acceptance. M6–M7, Refactor 8, and Phase 14 remain unauthorized.
- [x] M5b accepted on 2026-07-19; **M6 only** is authorized. M7, Refactor 8, and Phase 14 remain unauthorized.

## M5b verification (2026-07-19, accepted)

- Accepted at commit `e284ecc` after review closeout (implementation `e284ecc`
  including projection-disposal lifecycle hardening).
- Build: `dotnet build Zaide.slnx --no-restore` — succeeded (0 errors, 4 pre-existing warnings).
- Focused gate: **401 passed**, 0 failed, 0 skipped.
- Registration/DI gate: **67 passed**, 0 failed, 0 skipped.
- Architecture gate: **22 passed**, 0 failed, 0 skipped.
- Full suite: **2483 passed**, 0 failed, 0 skipped.
- `git diff --check` — clean.
- Manual M5 panel smoke: **not run** (no configured test endpoint in this session).
- `AgentPanelHost` disposes each panel's `AgentPanelOutputHistoryProjection` on `ClosePanel`; only open panels subscribe to `IConversationStore.EntryAppended`. Retained direct conversations remain authoritative after close.

## Entry conditions for M6

- [x] M5b authoritative typed direct-conversation output projection, read-only
      panel history surface, dual-write removal, parity/lifetime tests, and
      projection disposal on close are accepted at `e284ecc`.
- [x] Human accepted M5b closeout on 2026-07-19; **M6 only** is authorized.
- [ ] M6 implementation has not started.

## Entry conditions for M2

- [x] M1 typed `ActorId`/Actor catalog, canonical seeds, read-only projections,
      and `AddZaideConversations()` composition are accepted at `edc5dac`.
- [x] Human accepted M1 closeout on 2026-07-19; **M2 only** was authorized.
- [x] M2 accepted at `94a609f` on 2026-07-19.

## Entry conditions for M1

- [x] Refactor 6.1–6.3 are accepted and closed.
- [x] Live Agent/Townhall domain and orchestration paths were audited at
      `e597972`.
- [x] `R61-LT01` Conversation ownership is accepted for Refactor 7.
- [x] `R61-LT02` Agent Session is explicitly deferred for lack of a concrete
      current consumer/owner.
- [x] `R61-LT03` is narrowed to a minimum correlated execution-run model.
- [x] The active-channel attribution correction is named and isolated.
- [x] Refactor 8 and Phase 14 boundaries are explicit.
- [x] Human accepted this M0 plan and explicitly authorized M1 only on
      2026-07-19.

## Exit conditions

- [ ] Stable identity is independent of panel, display name, provider, model,
      runtime, and session.
- [ ] Conversation owns ordered entries by stable ID and explicit participants;
      active selection is presentation state only.
- [ ] Existing execution attempts have typed correlation and terminal outcomes
      without speculative session/tool/backend models.
- [ ] Agent Panel and Townhall render/project the authoritative typed records;
      orchestration no longer parses output/status strings to discover results.
- [ ] The active-channel attribution defect is corrected by a focused tested
      exception, while current public mirror visibility remains.
- [ ] Existing visible Agent Panel, Townhall, routing, execution, cancellation,
      draft, channel, and filtering behavior remains accepted.
- [ ] Refactor 8 UI extraction and Phase 14 feature/persistence/privacy work have
      not leaked into scope.
- [ ] Build, focused tests, Architecture tests, DI tests when applicable, and
      the full suite pass with exact totals recorded.
- [ ] Required manual panel and channel-switch evidence is recorded truthfully.
- [ ] `git diff --check` is clean and docs/status surfaces match live code.

## Limitations by design

- Refactor 7 leaves all new domain state in memory.
- No resumable Agent Session exists; `R61-LT02` remains a named deferral.
- The dedicated Agent Panel remains visible until Phase 14 proves retirement
  parity and migration/recovery policy.
- The current public Townhall mirror remains even though V3's eventual direct
  conversations are private by default.
- Existing non-streaming backend behavior remains the only execution
  capability represented.
- Historical seeded/demo data is normalized only enough to use stable typed
  identity; profile versioning and historical author snapshots are later
  concerns unless M1 proves they are required to preserve current behavior.

## Rollback plan

- **M0 baseline:** `e597972` (`master`, synchronized with `origin/master`).
- Prefer one commit per accepted milestone or milestone slice.
- Revert the current milestone commit to restore the last accepted boundary;
  do not roll back unrelated user work.
- M1–M4 must be additive/migratory until focused parity tests are green. Delete
  legacy paths only in the milestone that proves their replacement.
- If projection parity or attribution cannot be proven, keep the authoritative
  model behind the existing UI seam, document the blocker, and stop. Do not
  partially retire the Agent Panel or silently change public mirroring.
- A structural rollback requires
  `docs/refactor/refactor-7/REVERT_LOG.md` per `docs-rules.md`.

---

*Last updated: 2026-07-19 (M5b accepted at `e284ecc`; M6 only authorized and not implemented; M7, Refactor 8, and Phase 14 unauthorized)*
