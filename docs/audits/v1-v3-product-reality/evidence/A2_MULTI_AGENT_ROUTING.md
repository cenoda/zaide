# A2 Wiring Audit — `A2_MULTI_AGENT_ROUTING`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_MULTI_AGENT_ROUTING` (second A2 slice; first was `A2_AGENT_SEND`)
**Evidence date:** 2026-07-30
**Baseline:** branch `master`, HEAD `4cafa7e382ca3f05e0d4f65d80d693f2a284c7e4` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch, build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and scope

| Item | Value |
|------|-------|
| Audit | `v1-v3-product-reality` (see [AUDIT_PLAN.md](../AUDIT_PLAN.md)) |
| Slice name | `A2_MULTI_AGENT_ROUTING` |
| Prior A2 slice | `A2_AGENT_SEND` (complete; see [A2_AGENT_SEND.md](./A2_AGENT_SEND.md)) |
| Phase 6 source documents | [Phase 6 plan §"Mention Parsing Decision" + §"M0 Locked Decisions" + §"Known Gaps"](../../../phases/v1/phase-6/IMPLEMENTATION_PLAN.md), [Phase 6.1 plan §"Live Gaps To Fix" + §"M0 Locked Decisions"](../../../phases/v1/phase-6.1/IMPLEMENTATION_PLAN.md) |
| Phase 14 source documents | [Phase 14 plan §D09 + §"M7 implementation boundary" + §"M7 closeout"](../../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md), [Phase 14 M9 evidence](../../../phases/v3/phase-14/M9_MANUAL_EVIDENCE.md) |
| Goal rows to verdict | `A1-MR-01`, `A1-MR-03` (per [GOAL_MATRIX.md §13](../GOAL_MATRIX.md#13-multi-agent-routing)) |
| Scoped disposition row | `A1-XX-02` (per [GOAL_MATRIX.md §15](../GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior)) |
| A1 §17.4 item 6 | "Routing evolution from Phase 6 to Phase 14" — the slice's named unresolved documentation ambiguity |
| Verdict categories | `Wired`, `Wired-with-gap`, `Missing`, `Ambiguous` (per [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition)) |
| Method constraint | Inspection only; no production-code edits, no test edits, no app launch, no build, no test execution, no A3 smoke |

---

## 2. Sources inspected

### 2.1 Documentation

- [AGENTS.md](../../../../AGENTS.md), [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md), [GOAL_MATRIX.md](../GOAL_MATRIX.md), [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- [A2_AGENT_SEND.md](./A2_AGENT_SEND.md) (prior A2 evidence; reconciles without modification)
- [Phase 6 plan](../../../phases/v1/phase-6/IMPLEMENTATION_PLAN.md), [Phase 6.1 plan](../../../phases/v1/phase-6.1/IMPLEMENTATION_PLAN.md)
- [Phase 14 plan](../../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md), [Phase 14 M9 evidence](../../../phases/v3/phase-14/M9_MANUAL_EVIDENCE.md)

### 2.2 Production source

- [MentionParser.cs](../../../../src/Features/Agents/Application/MentionParser.cs)
- [MentionParseResult.cs](../../../../src/Features/Agents/Application/MentionParseResult.cs), [ParsedRouteIntent.cs](../../../../src/Features/Agents/Application/ParsedRouteIntent.cs)
- [AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs)
- [AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs)
- [ActorCatalog.cs](../../../../src/Features/Conversations/Application/ActorCatalog.cs)
- [CanonicalActorSeeds.cs](../../../../src/Features/Conversations/Application/CanonicalActorSeeds.cs)
- [IActorCatalog.cs](../../../../src/Features/Conversations/Contracts/IActorCatalog.cs)
- [IAgentPanelHost.cs](../../../../src/Features/Agents/Presentation/IAgentPanelHost.cs)
- [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs)
- [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs)
- [TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs)
- [TownhallInputArea.cs](../../../../src/Features/Townhall/Presentation/TownhallInputArea.cs)
- [TownhallEntryProjection.cs](../../../../src/Features/Townhall/Presentation/TownhallEntryProjection.cs)
- [RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs)
- DI: [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs), [ConversationsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/ConversationsServiceCollectionExtensions.cs), [TownhallServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs)

### 2.3 Tests (corroboration only)

- [MentionParserTests.cs](../../../../tests/Zaide.Tests/Features/Agents/Application/MentionParserTests.cs)
- [AgentRouterTests.cs](../../../../tests/Zaide.Tests/Features/Agents/Application/AgentRouterTests.cs)
- [ActorCatalogTests.cs](../../../../tests/Zaide.Tests/Features/Conversations/Application/ActorCatalogTests.cs)
- [AgentPanelHostTests.cs](../../../../tests/Zaide.Tests/Features/Agents/Presentation/AgentPanelHostTests.cs)
- [Phase14M7ParityBridgeTests.cs](../../../../tests/Zaide.Tests/Features/Townhall/Presentation/Phase14M7ParityBridgeTests.cs)
- [TownhallDirectSendTests.cs](../../../../tests/Zaide.Tests/Features/Townhall/Presentation/TownhallDirectSendTests.cs)

---

## 3. Verdict table for `A1-MR-01` and `A1-MR-03`

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-MR-01` | **Missing** | The Phase 6 entry point ("send `@alpha hello` from a panel") is no longer in the production UI. The dedicated Agent Panel chrome was retired in Phase 14 M8 (no `AgentPanelHostView`, no `AgentPanelView`, no right-column panel row, no `MainWindowViewModel.SendAgentMessageAsync`). Although Phase 14 M7 replaced the resolution mechanism with `IActorCatalog.ListAgents()`, the original Phase 6 panel-bound entry point was not preserved. The Phase 6 `AgentRouter` was rewritten to use catalog `ActorId` instead of visible panel names. |
| `A1-MR-03` | **Wired-with-gap** | Townhall direct-conversation send resolves `@mention` against the typed catalog roster (`IActorCatalog.ListAgents()`), with no requirement for an open target panel tab. `AgentRouter.GetOrCreatePanelForActor(targetActorId)` is reachable in production. Routing failures append typed `ConversationEntry.RoutingFailure` to the source conversation and are projected as `TownhallMessageKind.AgentError` in Townhall. **Gap:** the [`A1-MR-03` user-entry row in `GOAL_MATRIX.md`](../GOAL_MATRIX.md#13-multi-agent-routing) promises "Send `@alpha` (or catalog name) from any conversation" — that scope is not satisfied because Townhall **channel** sends bypass `IAgentRouter` and route through `LogActivity(ConversationEntryKind.UserChat, …)`; mention routing is reachable only from the active direct conversation. |

Verdict definitions: [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

---

## 4. Phase 6 → Phase 14 contract-evolution table

The two contracts are deliberately listed separately so a verdict against
one does not silently declare the other wired.

| Contract element | Phase 6 promise (V1) | Phase 14 replacement (V3) | Current production state |
|------------------|----------------------|--------------------------|--------------------------|
| **Parse input** | Zero or one `@AgentName`; case-insensitive exact match; strip the mention on success | Same — `MentionParser` retained verbatim (Phase 6 implementation; not changed by M7) | `MentionParser.Parse` in [MentionParser.cs](../../../../src/Features/Agents/Application/MentionParser.cs) L16–73 implements zero-or-one, case-insensitive, strip-on-success |
| **Target roster** | "Visible agent name" from open panels (`AgentPanelHost.Panels` projection of `AgentName`) | Typed `ActorId` via `IActorCatalog.ListAgents()` snapshot; `DisplayName` for matching, `ActorId` for identity | `AgentRouter.RouteAndExecuteAsync` consumes `_actorCatalog.ListAgents()` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L49–51); no longer references `Panel.AgentName` for routing |
| **Resolution output** | Resolved target panel by `AgentName`; fails if no panel matches | Resolves catalog `ActorId`; get-or-creates thin panel host via `IAgentPanelHost.GetOrCreatePanelForActor(ActorId)` | `AgentRouter.TryResolveTargetActor` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L95–137) and `GetOrCreatePanelForActor(targetActorId)` L77 |
| **User entry point** | "Send from a panel" — `MainWindowViewModel.SendAgentMessageAsync` invoked from a panel send gesture | Townhall direct-conversation send — `TownhallViewModel.SendMessageCommand` invoked from `TownhallInputArea.SendRequested` | No `SendAgentMessageAsync` in production; no `AgentPanelHostView` / `AgentPanelView`. Townhall `SendMessageCommand` is wired at [TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L98 and L255–264 |
| **Direct-send behavior** | Direct send to the source panel without a mention | Direct send (no mention) to the active direct conversation; resolved panel = source panel via `intent.IsDirectSend` branch | `AgentRouter` L75–77 — `intent.IsDirectSend` reuses `sourcePanel`; `MentionParseResult` for no-mention returns `IsDirectSend = true` ([MentionParser.cs](../../../../src/Features/Agents/Application/MentionParser.cs) L29–35) |
| **Routed-request visibility** | Mirror the routed request into Townhall; mirror the source panel's post-execution `OutputHistory` afterward | Successful admit / assistant / admitted-run terminal events go to the **target** `ConversationId` (the routed-to conversation); routing failures append `ConversationEntry.RoutingFailure` to the **source** `ConversationId`; admitted-run terminal failures append `ConversationEntry.ExecutionFailure` to the **target** `ConversationId` | `AgentConversationEventProjection` ([AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs) L281–416) appends typed entries; `RouteRequest.ConversationId` is the target panel's `ConversationId` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L79–85); `AgentExecutionCoordinator.SendAsync` is invoked with the target `PanelId` and writes to the target's `ConversationId`; Townhall projects via `TownhallEntryProjection` ([TownhallEntryProjection.cs](../../../../src/Features/Townhall/Presentation/TownhallEntryProjection.cs) L19–22) |
| **Unknown mention** | Visible `AgentError` (Phase 6.1) | Structured `ConversationEntry.RoutingFailure` on the source owning conversation; `TownhallMessageKind.AgentError` in Townhall | `AgentRouter.CreateRoutingFailureRouteResult` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L139–145) → `ProjectRoutingFailure` ([AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs) L49–93) → `TownhallMessageKind.AgentError` projection |
| **Shell chrome** | `AgentPanelHostView` row 2 in `RightColumnHost` | Removed; `RightColumnHost` is editor-only | [RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs) L12–62 — no `AgentPanelHostView`; no panel-row / splitter |
| **Source panel state** | Required: `MainWindowViewModel.SendAgentMessageAsync(sourcePanelId, …)` and a panel-bound "current panel" | Not required: router is invoked with `panel.PanelId` derived from the active direct conversation, not a UI-selected tab | `TownhallViewModel.SendMessageAsync` derives `panel` from the active direct conversation ([TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L464, L476) |
| **Thin `IAgentPanelHost` execution seam** | Not applicable (chrome was a panel, not a host) | Retained as non-visual thin host for execution; "thin non-visual `IAgentPanelHost` retained (DF-001 residual)" per Phase 14 closeout | [IAgentPanelHost.cs](../../../../src/Features/Agents/Presentation/IAgentPanelHost.cs) L8–11 documents "non-visual thin host" |

The Phase 6 panel-bound entry point was retired; the Phase 6 panel-bound
parser and resolution surface was replaced with the catalog-typed surface.
A Phase 6 matrix row that depended on the panel entry point must be
verdicted against the Phase 6 entry point, not against the Phase 14
replacement. Hence `A1-MR-01` is **Missing** (entry point absent) while
`A1-MR-03` is **Wired-with-gap** (catalog-typed resolution present and reachable from Townhall direct conversations, but the `A1-MR-03` user-entry scope documented in [GOAL_MATRIX.md §13](../GOAL_MATRIX.md#13-multi-agent-routing) — "Send `@alpha` (or catalog name) from any conversation" — is not satisfied because Townhall channel sends bypass the router).

---

## 5. Current user-entry-point reachability matrix

The "from any conversation" wording — the `user_entry_point` documented in the [accepted `A1-MR-03` row in `GOAL_MATRIX.md`](../GOAL_MATRIX.md#13-multi-agent-routing) — is **not literally true** in production. The `A1-MR-03` user-entry row in `GOAL_MATRIX.md` records "Send `@alpha` (or catalog name) from any conversation"; that scope is not satisfied because routing is reachable only from the active **direct** conversation in Townhall; it is not reachable from a Townhall **channel** send, and there is no other production send surface. (The Phase 14 plan §D09 decision text itself speaks of the routing-resolution contract and the `ActorId` roster, not the entry-point scope; the entry-point scope comes from the accepted goal-matrix row, not from §D09 literal text.)

| User-visible entry point | `@mention` routing reachable? | Evidence |
|--------------------------|-------------------------------|----------|
| **Townhall direct conversation** (active) | **Yes** | `SendMessageAsync` branches on `_state.ActiveConversationId`; only when `conversation.Kind == Direct` does it call `EnsurePanelForDirectConversation` then `_agentRouter.RouteAndExecuteAsync(panel.PanelId, draft)` ([TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L433–490, L464, L476) |
| **Townhall channel** (active) | **No** | Channel send branches at L441–451 — `LogActivity(ConversationEntryKind.UserChat, …)` and return. The router is not invoked from channel send. |
| **People panel → "Open DM with {name}"** (entry into a direct conversation) | **Yes** (after opening, the resulting direct send uses the router) | `OpenDirectConversationCommand` ([TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L276–279; [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L425, L619–631) opens a direct; subsequent send uses router |
| **Removed Phase 6 Agent Panel UI** | **No** (no longer in shell) | No `AgentPanelHostView`, no `AgentPanelView`; `RightColumnHost` is editor-only ([RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs) L12–62) |
| **Other production send surfaces** | **None** | No command-palette or settings path that initiates a direct send. The `command registry` ([CommandRegistry.cs](../../../../src/App/Composition/CommandRegistry.cs)) does not register a `mention.route` command. `SendMessageCommand` is the only call site of `RouteAndExecuteAsync` in production source. |

Channel send cannot reach routing because `SendMessageAsync` returns immediately after `LogActivity` for channel kind (L441–451). The only production caller of `IAgentRouter.RouteAndExecuteAsync` is the direct-send branch of `TownhallViewModel.SendMessageAsync` (L476).

---

## 6. End-to-end production routing trace

Townhall input → `TownhallViewModel.SendMessageAsync` → `IAgentRouter.RouteAndExecuteAsync` → `MentionParser.Parse` → `IActorCatalog.ListAgents` → display-name match → typed `ActorId` → `IAgentPanelHost.GetOrCreatePanelForActor(targetActorId)` → `IAgentExecutionCoordinator.SendAsync(panelId, content, ct)` → `AgentConversationEventProjection` → `ConversationStore` → `TownhallEntryProjection` → Townhall chat.

| Seam | Evidence |
|------|----------|
| Townhall input area send gesture | [TownhallInputArea.cs](../../../../src/Features/Townhall/Presentation/TownhallInputArea.cs) L29 — `public event Action? SendRequested;`; L220 — `SendRequested?.Invoke();` (Enter / send button) |
| Townhall view subscribes to `SendRequested` | [TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L98 — `_inputArea.SendRequested += OnSendRequested;`; L255–264 — `OnSendRequested` calls `_viewModel.SendMessageCommand.Execute()` |
| Direct conversation discovery | `OpenDirectConversation` at [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L619–631; `OpenDirectConversationCommand` at L300, L425 |
| Source panel resolution (thin host) | `EnsurePanelForDirectConversation` at L688–694 → `_panelHost.GetOrCreatePanelForActor(peerActorId)`; `IAgentPanelHost` is the non-visual thin host ([IAgentPanelHost.cs](../../../../src/Features/Agents/Presentation/IAgentPanelHost.cs) L8–11) |
| Router call | `_agentRouter.RouteAndExecuteAsync(panel.PanelId, draft)` at L476 |
| Roster source | `MentionParser` is called with `_actorCatalog.ListAgents().Select(static a => a.DisplayName).ToList()` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L49–51) |
| Display-name match (case-insensitive) | `MentionParser.Parse` L49–51 — `string.Equals(n, name, StringComparison.OrdinalIgnoreCase)`; `AgentRouter.TryResolveTargetActor` L116–118 — same `OrdinalIgnoreCase` |
| Typed `ActorId` resolution | `Actor matches[0].Id` at [AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L134; consumed by L77 — `_panelHost.GetOrCreatePanelForActor(targetActorId)` |
| Target host construction (no open panel required) | `IAgentPanelHost.GetOrCreatePanelForActor` at [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L100–109: returns existing panel if present, otherwise calls `CreatePanelForActor` (L87–97) which builds from catalog |
| Direct send (no mention) | `MentionParseResult` with `IsDirectSend = true` for no-mention input ([MentionParser.cs](../../../../src/Features/Agents/Application/MentionParser.cs) L29–35); `AgentRouter` L75–77 — `intent.IsDirectSend ? sourcePanel : _panelHost.GetOrCreatePanelForActor(targetActorId)` |
| Execution dispatch | `_coordinator.SendAsync(targetPanel.PanelId, request.ContentAfterStrip, ct)` at [AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L87–90 |
| Successful admit → conversation | `AgentConversationEventProjection.ProjectUserMessageAdmitted` L281–317 and `ProjectAssistantMessageCompleted` L319–357 append typed `UserChat` / `AssistantResponse` entries to the **target** `ConversationId` (the conversation owned by `RouteRequest.ConversationId`, which is the target panel's conversation). The active Alpha source conversation does **not** receive these entries. |
| Routing failure → conversation | `AgentRouter.TryCreateAndRecordRoutingFailure` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L147–177) → `AgentConversationEventProjection.ProjectRoutingFailure` ([AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs) L49–93) → appends `ConversationEntryKind.RoutingFailure` |
| Townhall chat visibility | `TownhallEntryProjection.ToTownhallMessageKind` ([TownhallEntryProjection.cs](../../../../src/Features/Townhall/Presentation/TownhallEntryProjection.cs) L16–26) maps `RoutingFailure` → `AgentError`, `UserChat` / `AssistantResponse` → `Chat`, `ExecutionFailure` → `AgentError`; townhall view binds to `Messages` collection (L328–330 per A2_AGENT_SEND) |
| `IAgentPanelHost` is non-visual | [IAgentPanelHost.cs](../../../../src/Features/Agents/Presentation/IAgentPanelHost.cs) L8–11: "Non-visual thin host for agent direct-conversation execution seams. Owns in-memory panel state keyed by catalog actor; no UI chrome (Phase 14 M8)." |
| Source requirement | The `IAgentPanelHost` requirement is an **execution adapter**, not a user-facing panel tab. The panel is created on demand for the active direct conversation via `GetOrCreatePanelForActor`; no visible "open panel" prerequisite. |

---

## 7. Mention-parser behavior table

The behaviors below are derived from [MentionParser.cs](../../../../src/Features/Agents/Application/MentionParser.cs) L16–73. Each row is annotated with whether the behavior is reachable through the **production UI** (Townhall direct-conversation send) or only callable in isolation via the `MentionParser` unit tests.

| Input | Parser behavior | `Success` | `FailureReason` | Reachable from production UI? |
|-------|-----------------|-----------|-----------------|-------------------------------|
| `null` or whitespace-only | empty-input branch | `false` | `"Empty input"` | **Not reachable through the production routing path.** `TownhallViewModel.SendMessageAsync` short-circuits whitespace/empty drafts at L435–439 (`if (string.IsNullOrEmpty(draft)) return;`) before the router is invoked; the parser's `"Empty input"` reason is reachable only from the parser unit test, not from the production Townhall direct send. |
| `"hello world"` (no mention) | zero mention tokens | `true` | `null`; `IsDirectSend = true`; `ContentAfterStrip = "hello world"` | Yes |
| `"@Beta please review"` (one valid mention) | one mention token; resolved against catalog `DisplayName` (case-insensitive); content stripped | `true` | `null`; `IsDirectSend = false`; `MatchedAgentName = "Beta"`; `ContentAfterStrip = "please review"` | Yes |
| `"@gAmMa check"` (case-insensitive) | exact name match with `OrdinalIgnoreCase` | `true` | `null`; `MatchedAgentName = "Gamma"` (case-preserving return value via `matchingNames[0]`) | Yes — case-insensitive exact match is reachable |
| `"@Ghost hello"` (unknown target) | zero matches | `false` | `"Unknown target"` | Yes — produces `ConversationEntry.RoutingFailure` on source conversation, projected as `AgentError` |
| `"@Twin hello"` when two actors named `Twin` | more than one match | `false` | `"Ambiguous target"` | **Not constructible through the current clean production UI.** `ActorCatalog` is not user-extensible after Phase 14 M8 retired the dedicated Agent Panel UI (no current production UI calls `RegisterOrGetCustomPanelActor`). The `"Ambiguous target"` branch is reachable only via an **authorized catalog setup hook** (e.g., an A3-only test fixture that calls `RegisterOrGetCustomPanelActor` twice with the same display name before invoking the router) or via a lower-level parser unit test that supplies a duplicate-name list directly. |
| `"@Alpha @Beta both"` (multiple mentions) | more than one mention token | `false` | `"Multiple mentions"` | Yes |
| `"@"` (empty target after `@`) | `mention.Substring(1) == ""` | `false` | `"Empty mention target"` | **Yes** — a lone `"@"` is non-empty and non-whitespace, so it passes `SendMessageAsync` L435–439 (which only short-circuits whitespace/empty drafts) and reaches `MentionParser` through the production Townhall direct-send path. The router appends a `ConversationEntry.RoutingFailure` to the source conversation and projects it as `AgentError`; the draft is cleared by `routeResult.ExecutionResult is not null` in `TownhallViewModel.SendMessageAsync` L477–486. |
| `"@Alpha"` (mention only, strips to empty) | `stripped` is whitespace | `false` | `"Empty content after stripping"` | Yes — production user can type `@Alpha` and press Enter |
| `"  @Beta  hello  "` (whitespace around tokens) | tokens split on space; `@Beta` and `hello` retained | `true` | `null`; `ContentAfterStrip = "hello"` — replacing the mention token with a single space then collapsing all whitespace runs with `Regex.Replace(@"\s+", " ")` and `Trim()` produces the single token `hello` (the trailing space after `@Beta` plus the leading space before `hello` collapse to a single separator) | Yes |
| `"@Beta\nplease\nreview"` (newlines) | `Split(' ')` keeps the whole string as a single token beginning with `@Beta\nplease\nreview`; mention token list still includes the one; matched name is `"Beta\nplease\nreview"`, fails `Unknown target` | `false` | `"Unknown target"` | Yes (newlines pass through; this is a parser-level quirk, not a UI quirk) |

The Phase 6 first-slice promise ("zero or one `@AgentName`; case-insensitive exact match; strip the matched mention token") is honored by [MentionParser.cs](../../../../src/Features/Agents/Application/MentionParser.cs) and is reachable through the production Townhall direct send.

---

## 8. Catalog / `ActorId` resolution findings

| Question | Finding | Evidence |
|----------|---------|----------|
| How is the production catalog populated? | `CanonicalActorSeeds.All` (Human, Townhall Agent, panel seeds alpha–delta) seeded at construction; `RegisterOrGetCustomPanelActor` and `GetOrRegisterPanelFallbackActor` add dynamic entries | [ActorCatalog.cs](../../../../src/Features/Conversations/Application/ActorCatalog.cs) L18–24 (seed loop), L43–62 (fallback), L66–98 (custom); [CanonicalActorSeeds.cs](../../../../src/Features/Conversations/Application/CanonicalActorSeeds.cs) L14–52 |
| Are canonical panel-seed agents resolvable without opened panel tabs? | **Yes — `Alpha`, `Beta`, `Gamma`, and `Delta` are catalog-listed and parser-addressable without open panel tabs.** The canonical Human is excluded from `ListAgents()`, while the canonical Townhall Agent (`Zaide Agent`) is catalog-listed but not parser-addressable because its display name contains whitespace. | [ActorCatalog.cs](../../../../src/Features/Conversations/Application/ActorCatalog.cs) L108–117; [MentionParser.cs](../../../../src/Features/Agents/Application/MentionParser.cs) L26–56; [AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L49–51 |
| Do custom/fallback actors require prior panel registration? | **Yes for the source/destination panel-host code path; no for catalog presence alone** — `ActorCatalog` seeds canonical actors in its constructor ([ActorCatalog.cs](../../../../src/Features/Conversations/Application/ActorCatalog.cs) L18–24 reads `CanonicalActorSeeds.All`). `AgentPanelHost.CreatePanel()` (parameterless, the former "New Panel" button action) invokes `GetOrRegisterPanelFallbackActor` after the canonical panel seeds (alpha, beta, gamma, delta) are exhausted ([AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L54–70). `AgentPanelHost.CreatePanel(agentId, agentName, avatar)` (the former identity-customizing path) invokes `RegisterOrGetCustomPanelActor` ([AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L77–84). Both methods register into the catalog before the panel is created. **For `@mention` resolution to find a non-canonical actor, that actor must exist in the catalog** (i.e., one of the two methods above must have been called for that actor's id). After Phase 14 M8 retired the dedicated Agent Panel UI chrome, no current production UI calls either `CreatePanel()` overload. The catalog also seeds two additional canonical actors that are **not** mention-addressable for distinct reasons — see the "Catalog addressability" row below for the precise distinction. The canonical **Human** is excluded from the mention roster by `IActorCatalog.ListAgents()` (which filters to `ActorKind.Agent` only), and the canonical **Townhall Agent (`Zaide Agent`)** is catalog-listed but excluded by the single-token `MentionParser` syntax (its `DisplayName` contains whitespace and cannot be matched by a single `@`-prefixed token). | [ActorCatalog.cs](../../../../src/Features/Conversations/Application/ActorCatalog.cs) L18–24, L43–98, L108–117; [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L54–84; [CanonicalActorSeeds.cs](../../../../src/Features/Conversations/Application/CanonicalActorSeeds.cs) L14–52; [MentionParser.cs](../../../../src/Features/Agents/Application/MentionParser.cs) L26–43 |
| Catalog addressability (which catalog actors can be reached through `@mention` in production today) | `IActorCatalog.ListAgents()` filters to `ActorKind.Agent` only ([ActorCatalog.cs](../../../../src/Features/Conversations/Application/ActorCatalog.cs) L108–117 — `Where(static a => a.Kind == ActorKind.Agent)`), so the canonical **Human** (`ActorKind.Human`) is **excluded** from the mention roster and cannot be addressed via `@mention`. Of the remaining seeded `ActorKind.Agent` actors, only single-token `DisplayName` values are mention-addressable through the current `MentionParser` syntax: `MentionParser.Parse` splits on single space characters (`rawInput.Split(' ', StringSplitOptions.RemoveEmptyEntries)`, [MentionParser.cs](../../../../src/Features/Agents/Application/MentionParser.cs) L26) and treats the substring after `@` as a single token. Therefore the multi-word seeded display name `"Zaide Agent"` and the multi-word fallback display name `"Agent N"` (where `N` is a number) **cannot be addressed by the current parser** — typing `@Zaide` or `@Zaide Agent` does not match the `Zaide Agent` catalog entry. The **directly mention-addressable canonical actors** in production today are therefore exactly the four single-word panel seeds: **`Alpha`, `Beta`, `Gamma`, `Delta`** (per [CanonicalActorSeeds.cs](../../../../src/Features/Conversations/Application/CanonicalActorSeeds.cs) L29–51). For custom actors: catalog registration via `RegisterOrGetCustomPanelActor` is **necessary** for the actor to be reachable, but a `DisplayName` that contains whitespace is **not parser-addressable** even when registered — only single-word custom display names are mention-addressable through the current parser. | [ActorCatalog.cs](../../../../src/Features/Conversations/Application/ActorCatalog.cs) L108–117; [CanonicalActorSeeds.cs](../../../../src/Features/Conversations/Application/CanonicalActorSeeds.cs) L14–52; [MentionParser.cs](../../../../src/Features/Agents/Application/MentionParser.cs) L26–43 |
| Can duplicate display names produce ambiguity? | **Yes** — if two catalog actors share the same `DisplayName` (e.g., two `RegisterOrGetCustomPanelActor` calls with the same name), `MentionParser` returns `Ambiguous target` and `AgentRouter` returns `"Ambiguous target"`; `IActorCatalog.ListAgents()` does not deduplicate by display name. The canonical seeds have distinct display names, so this is a latent risk only for non-canonical actors | [MentionParser.cs](../../../../src/Features/Agents/Application/MentionParser.cs) L58–61; [AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L127–131 |
| Does the target have to already have a visible/open panel? | **No** — `IAgentPanelHost.GetOrCreatePanelForActor` creates a thin host for the actor on demand | [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L100–109; [IAgentPanelHost.cs](../../../../src/Features/Agents/Presentation/IAgentPanelHost.cs) L41–46 |
| Does the source require a non-visual `AgentPanelState`? | The router uses `_panelHost.Panels.FirstOrDefault(p => p.PanelId == sourcePanelId)` to look up the source panel ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L47). This is satisfied because `TownhallViewModel.EnsurePanelForDirectConversation` calls `_panelHost.GetOrCreatePanelForActor(peerActorId)` for the source direct conversation immediately before invoking the router ([TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L464, L688–694) | Same |
| Is that source requirement a remaining legacy panel-bound dependency? | **No** — it is an execution adapter, not a user-facing panel tab. The source panel is built from the active direct conversation's `ConversationId` and the catalog actor (typed `ActorId`); there is no visible "select a source panel" UI gesture. The source and target panels are functionally symmetric get-or-create host constructions, both indexed by `ActorId` | [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L111–138 (`CreatePanelFromActor` keys conversation by `ActorId`); [AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L75–77 |

---

## 9. Panel-chrome removal and retained adapter analysis

| Surface (Phase 6) | Status in production | Evidence |
|-------------------|----------------------|----------|
| `MainWindowViewModel.SendAgentMessageAsync` | **Removed** — `rg SendAgentMessageAsync src/` returns no matches | (no production source) |
| `AgentPanelHostView` (Phase 5 view) | **Removed** — `rg AgentPanelHostView src/` returns no matches | (no production source) |
| `AgentPanelView` (Phase 5 view) | **Removed** — `rg AgentPanelView src/` returns no matches | (no production source) |
| `AgentTownhallMirrorCoordinator` | **Removed** — `rg AgentTownhallMirrorCoordinator src/` returns no matches | (no production source) |
| Right-column panel row / panel splitter | **Removed** — `RightColumnHost.cs` constructs only `EditorTabBar` / `SearchBar` / `EditorView` / `WelcomeText` (L25–54) | [RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs) L25–54 |
| Open-panel-name-based target resolution | **Removed** — `AgentRouter` resolves via `IActorCatalog.ListAgents()` (L49–51), not `IAgentPanelHost.Panels` display names | [AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L49–51 |
| `IAgentPanelHost` (non-visual thin host) | **Retained** as a non-visual execution seam, no UI chrome | [IAgentPanelHost.cs](../../../../src/Features/Agents/Presentation/IAgentPanelHost.cs) L8–11 |
| `AgentPanelHost` (concrete) | **Retained** as the concrete implementation; keyed by `ActorId`, not by display name; no visible tab strip | [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L1–245; architecture plan §"DF-001 closed with thin non-visual host residual" |
| `AgentPanelState` | **Retained** as the model for in-memory panel state (status, draft, output projection); no UI tab | [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L111–138 (panel construction); `IAgentPanelHost` interface retains the model |

The non-visual `IAgentPanelHost` seam is intentionally retained as the execution adapter (per Phase 14 closeout wording "thin non-visual `IAgentPanelHost` retained (DF-001 residual)"). Routing does not require a user-facing panel tab; the source and target panels are both non-visual adapter objects constructed from `ActorId`.

---

## 10. Success visibility and owning-conversation findings

For a successful mention routing (e.g., user types `@Beta please review` in the active Alpha direct conversation):

| Question | Finding | Evidence |
|----------|---------|----------|
| Which conversation owns the admitted user request? | The **target direct conversation** (Human↔Beta) — `MentionParser` preserves `sourcePanelId` (the source panel reference for failure projection), but `AgentRouter` builds `RouteRequest` with the **target** panel's `ConversationId` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L79–85 — `RouteRequest(... targetPanel.ConversationId, intent.ContentAfterStrip, intent.IsDirectSend)`). `AgentExecutionCoordinator.SendAsync(targetPanel.PanelId, …)` is invoked with the **target** `PanelId`, so successful `UserMessageAdmitted`, `AssistantMessageCompleted`, and admitted-run terminal events are projected to the **target** `ConversationId`. The active Alpha source conversation does **not** receive these successful entries. **Ordinary parser/resolution failures** (unknown / ambiguous / multiple / empty target / empty content after strip / empty mention target — i.e., all cases where the source panel exists and the parser/resolution produces a `RoutingFailure`) remain attached to the **source** conversation because `TryCreateAndRecordRoutingFailure` is keyed by `sourcePanel.ConversationId` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L147–177). The **missing-source-panel** exception is a separate case: when the router is invoked with a `sourcePanelId` that no longer resolves, no source `ConversationId` is available, and the router produces no conversation entry at all (see §11, "Missing source panel" row). | [AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L47, L63–66, L79–92, L147–177; [AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs) L281–357 |
| Is the request immediately visible in the currently selected Townhall conversation? | **No, not in the active source conversation** — successful entries are appended to the **target** `ConversationId`. The active Townhall conversation (source) does not show the user message, the assistant response, or admitted-run terminal failures from the routed execution. The target's `EntryAppended` event fires, but the user is not on the target. The source conversation's `Messages` collection is not updated by the routed execution. | [AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs) L281–416 (events write to `agentEvent.ConversationId`, which is the target); [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L820–858 |
| Is a new target direct conversation created? | **Yes** — `IAgentPanelHost.GetOrCreatePanelForActor` calls `CreatePanelForActor` → `CreatePanelFromActor` → `_conversationStore.GetOrCreateDirectConversation(_actorCatalog.CanonicalHuman.Id, actor.Id)`, which find-or-creates the Human↔Beta direct | [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L100–138 |
| Does the user receive navigation, unread, or other feedback when the routed request belongs to another conversation? | **Unread only** — the `EntryAppended` event on the **target** conversation triggers `OnConversationEntryAppended` → `ApplyUnreadPresentation` → marks `HasUnread = true` on the target direct nav row. No automatic navigation. `MarkConversationRead` is not called. The user remains on the source conversation. | [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L820–858 |
| Is "Townhall shows the request" fully satisfied? | **Partially / not for the routed flow** — successful user/assistant/terminal entries are written to the **target** conversation, not the active source conversation. The active Townhall chat does not display the routed request or response; the user must navigate to the target direct to see the target-side conversation history. **Audit synthesis (current production behavior):** the routed execution lands in a direct conversation that is navigable from Townhall, and the target direct nav row receives an unread indicator when the user is not on it — this is the **current production visibility behavior** for routed flow. Phase 14 D09 itself is credited only with **typed `ActorId` / catalog roster resolution** and **removing the requirement for a dedicated panel tab** (per the D09 decision text in the [Phase 14 plan](../../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md)). The Phase 6 promise of "routed request visible in Townhall as part of the active flow" is **not** satisfied as in-place chat content — it is satisfied only as a navigation affordance (unread indicator on the target direct row). Per [A2_AGENT_SEND §6](./A2_AGENT_SEND.md#6-response-and-failure-projection-findings), admitted execution and admitted-run terminal outcomes are projected to the target conversation, not the source. | (synthesis) |

---

## 11. Routing-failure and execution-failure projection table

Each row is reconciled with [A2_AGENT_SEND.md](./A2_AGENT_SEND.md) (no edits to that file). For each routing failure, the structured result is `RouteResult { Success = false, Request = null, FailureReason = "<reason>", ExecutionResult = AgentExecutionCoordinatorResult.RoutingFailure(run, reason) }`.

| Outcome | Parser / router result | Conversation entry appended? | Owning conversation | Townhall chat visibility | Draft cleared? | Distinguishing label |
|---------|------------------------|------------------------------|---------------------|--------------------------|-----------------|----------------------|
| **Unknown target** | `Success = false`; `FailureReason = "Unknown target"`; `ExecutionResult = RoutingFailure(run, "Unknown target")` | **Yes** — `ConversationEntry.RoutingFailure` on source `ConversationId` | Source direct conversation | `TownhallMessageKind.AgentError` (rendered in source chat) | **Yes** if the conversation store held the source conversation; the Townhall side clears via `routeResult.Success \|\| routeResult.ExecutionResult is not null` ([TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L477–486) | `RoutingFailure` |
| **Ambiguous target** | `Success = false`; `FailureReason = "Ambiguous target"`; `ExecutionResult = RoutingFailure(run, "Ambiguous target")` | **Yes** — same path | Source direct conversation | `AgentError` | **Yes** (same rule) | `RoutingFailure` |
| **Multiple mentions** | `Success = false`; `FailureReason = "Multiple mentions"`; `ExecutionResult = RoutingFailure(run, "Multiple mentions")` | **Yes** — same path | Source direct conversation | `AgentError` | **Yes** (same rule) | `RoutingFailure` |
| **Empty target** (e.g. `"@"`) | `Success = false`; `FailureReason = "Empty mention target"`; `ExecutionResult = RoutingFailure(run, "Empty mention target")` | **Yes** — same path | Source direct conversation | `AgentError` | **Yes** (same rule) | `RoutingFailure` |
| **Empty content after stripping** (e.g. `"@Alpha"`) | `Success = false`; `FailureReason = "Empty content after stripping"`; `ExecutionResult = RoutingFailure(run, "Empty content after stripping")` | **Yes** — same path | Source direct conversation | `AgentError` | **Yes** (same rule) | `RoutingFailure` |
| **Empty input** (whitespace-only) | `Success = false`; `FailureReason = "Empty input"`; `ExecutionResult = RoutingFailure(run, "Empty input")` | **No** — `SendMessageAsync` L435–439 short-circuits empty drafts before reaching the router; the parser's `"Empty input"` reason is reachable only via the parser unit test in production surface area. (In tests that bypass the ViewModel, `ProjectRoutingFailure` is still called.) | n/a (not invoked from production UI) | n/a | n/a | `RoutingFailure` (only via tests) |
| **Missing source panel** (e.g. router invoked with a `sourcePanelId` that no longer exists) | `Success = false`; `FailureReason = "Unknown source panel"`; `ExecutionResult = null` (no source conversation to project onto) | **No** — `TryCreateAndRecordRoutingFailure` returns `null` when `sourcePanel is null` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L151–152) | n/a | **No chat entry, no failure bubble** | **No** — `routeResult.ExecutionResult is null` and `routeResult.FailureReason` is non-empty; the `if (routeResult.Success \|\| routeResult.ExecutionResult is not null \|\| !string.IsNullOrEmpty(routeResult.FailureReason))` guard means the empty-input branch's `routeResult.FailureReason` is non-empty — but the `if` body only clears when `Success` or `ExecutionResult` is non-null, so draft is **retained** (L477–486). This is a small gap: a missing source panel fails silently with draft retained | (silent) |
| **Empty catalog** (synthetic only) | **Not a normal reachable state in production** — `ActorCatalog` seeds canonical actors (Human, Townhall Agent, alpha, beta, gamma, delta) in its constructor ([ActorCatalog.cs](../../../../src/Features/Conversations/Application/ActorCatalog.cs) L18–24), so an empty `IActorCatalog.ListAgents()` does not arise from the production composition root. The synthetic empty-catalog case (constructed in test setup or hypothetical) would behave as: any mention becomes `"Unknown target"` (the parser matches zero display names, and the router's `TryResolveTargetActor` returns `"Unknown target"` at L120–125); a no-mention input would still take the `IsDirectSend = true` branch in the router (L75–77, L101–106), dispatching to the source panel via the coordinator — no agent-roster lookup is required for direct send because `targetActorId` is set to `sourcePanel.ActorId` from the panel itself, not from the catalog. | n/a (no mention path) | n/a | n/a | n/a | n/a |
| **Execution rejection after successful routing** (admission rejected by `AgentSessionService`) | `RunRejected` event emitted, but `AgentConversationEventProjection` explicitly skips `RunRejected` (L131–133) and `ProjectTerminalFailureEntry` requires `_admittedRunIds` (L379–385) | **No** — the `RunRejected` event is intentionally not projected as a conversation entry | n/a | **No** (per [A2_AGENT_SEND §4.9](./A2_AGENT_SEND.md#49-rejected-unbound-failed-cancelled-timed-out-disconnected-indeterminate-feedback)) | **Yes** — `RouteResult.Success = true` and `ExecutionResult` is non-null from the coordinator, so `ClearActiveConversationDraft` runs (L483–485) | n/a (ISSUE-008) |
| **Admitted-run terminal failure** (timeout / disconnected / failed / cancelled / indeterminate) | `RouteResult.Success = true`; `ExecutionResult` reflects the failure; `AgentConversationEventProjection` appends `ConversationEntry.ExecutionFailure` on the **target** `ConversationId` (the admitted run's `ConversationId` is the target panel's, since `AgentExecutionCoordinator.SendAsync` was invoked with the target `PanelId`) | **Yes** — typed `ExecutionFailure` entry on the target conversation | **Target** direct conversation (Human↔Beta in the example) | `TownhallMessageKind.AgentError` once the user navigates to the target direct; **not** visible in the active source conversation's chat | **Yes** — `ClearActiveConversationDraft` runs from the direct-send branch on success/failure result | `ExecutionFailure` |

**Reconciliation with [A2_AGENT_SEND.md](./A2_AGENT_SEND.md):** the ordinary routing-failure path is projected (it is the inverse of A2_AGENT_SEND's pre-session rejection: the routing failure runs **before** the `AgentSessionService.SendAsync` call, so it does have a `RunId` and is admitted at the routing level, hence the `RoutingFailure` entry is appended). The execution-rejection path is the open ISSUE-008 gap (the rejection is not projected). The **missing-source-panel** case is an exception: when the router cannot resolve a source panel, `TryCreateAndRecordRoutingFailure` returns `null` because no source `ConversationId` is available — the ordinary projection to the source conversation cannot run, and no conversation entry is produced. These are distinct labels: `RoutingFailure` is appended at the routing layer; `ExecutionFailure` is appended at the post-admission terminal layer; session-level `RunRejected` is suppressed.

**Difference between `RoutingFailure`, `AgentError`, and execution rejection:**
- `RoutingFailure` — a typed `ConversationEntryKind` (not the same as the Townhall `TownhallMessageKind.AgentError`) produced by `AgentRouter.TryCreateAndRecordRoutingFailure` / `AgentConversationEventProjection.ProjectRoutingFailure`. Surfaces to Townhall as `AgentError`.
- `AgentError` (Townhall `TownhallMessageKind`) — the presentation kind used by `TownhallEntryProjection` to render `RoutingFailure`, `ExecutionFailure`, and certain `SystemNotification` content types.
- "Execution rejection" — an `AgentEventKind.RunRejected` that fires **before** the run is admitted; the projection explicitly suppresses this case, leaving no conversation entry and no visible failure feedback in Townhall chat. This is ISSUE-008's gap; it is distinct from the routed-to-bad-target case (which is `RoutingFailure`, projected) and the admitted-but-failed case (which is `ExecutionFailure`, projected).

---

## 12. Production DI findings

| Service | Registered | Resolved on startup | Production caller | Evidence |
|---------|------------|---------------------|-------------------|----------|
| `MentionParser` | **Yes** | yes (singleton) | `AgentRouter` constructor ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L29) | [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L91 |
| `IAgentRouter` / `AgentRouter` | **Yes** | yes (singleton) | `TownhallViewModel` constructor (optional, factory-injected via `AddZaideTownhall`) | [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L92; [TownhallServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs) L28–43 |
| `IActorCatalog` / `ActorCatalog` | **Yes** | yes (singleton) | `AgentRouter` (L25), `AgentPanelHost` (L23), `TownhallViewModel` (L32) | [ConversationsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/ConversationsServiceCollectionExtensions.cs) L13 |
| `IAgentPanelHost` (thin host) | **Yes** | yes (singleton) | `TownhallViewModel.SendMessageAsync` (L315, L464, L691) | [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L41 |
| `IConversationStore` | **Yes** | yes (singleton) | `AgentRouter` (L26, L166), `AgentPanelHost` (L24, L113), `TownhallViewModel` (L33) | [ConversationsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/ConversationsServiceCollectionExtensions.cs) L14 |
| `TownhallViewModel` | **Yes** (full deps) | yes (singleton) | `MainWindowViewModel` via `AppCoreServiceCollectionExtensions.AddSingleton<MainWindowViewModel>()` resolves it transitively | [TownhallServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs) L28–43 |
| `AgentConversationEventProjection` | **Yes** | yes (eager via `Program.CreateAgentExecutionCoordinator`) | subscribes to `AgentEventStream`; projects admitted / failure / run-terminal events to `IConversationStore` | [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L40 |
| `AgentExecutionCoordinator` (via `IAgentExecutionCoordinator`) | **Yes** | yes (singleton, `Program.CreateAgentExecutionCoordinator`) | `TownhallViewModel.SendMessageAsync` and `AgentRouter.RouteAndExecuteAsync` | [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L90 |

Every registered service in the routing path has a production caller; no service in the routing path is a registered-but-unused dead seam. The router is the only caller of the parser; the catalog is the only resolver; the thin `IAgentPanelHost` is the only execution adapter.

---

## 13. `A1-XX-02` scoped disposition

`A1-XX-02` is the row in [GOAL_MATRIX.md §15](../GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior) that documents the debate/disagreement model from Phase 6 as "not implemented as a specialized feature." Per the slice charter, this row receives a **scoped disposition only** and is not counted as a user-goal verdict.

**Scoped disposition: confirmed absent.**

| Question | Finding | Evidence |
|----------|---------|----------|
| Is there a specialized debate/disagreement surface in the current production UI? | **No** | No view, ViewModel, or model in `src/` references a debate or disagreement surface. `rg -i "debate\|disagreement" src/` returns no production code matches; only doc/issue references in `docs/`. |
| Is there a routing/thread feature in `ConversationEntryKind` that implies debate? | **No** — kinds are `UserChat`, `AssistantResponse`, `RoutingFailure`, `ExecutionFailure`, `ChannelEvent`, `SystemNotification` | [AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs) (event kinds); `ConversationEntryKind` enum |
| Does Phase 6 document it as not implemented? | **Yes** — Phase 6 plan §"Known Gaps" explicit: "No specialized debate/disagreement surfacing in Townhall. Phase 6 routes content to a target panel and mirrors generic chat/error entries, but does not emit a distinct 'agent A requested review from agent B' or 'disagreement' Townhall entry. The `docs/roadmap/PHASES.md` 'Debate model: disagreements surfaced in Townhall' item is **not** implemented as a specialized feature." | [Phase 6 plan](../../../phases/v1/phase-6/IMPLEMENTATION_PLAN.md) L510–514 |
| Was it ever implemented later? | **No** — Phase 14 plan D02 ("Direct conversations are **private by default**. No implicit copy of DM content into a public channel.") and D09 (catalog routing) do not introduce a debate surface. The Phase 14 row `A1-TH-05` (Townhall surfaces routing failures and routed-flow outcomes) does not include a debate contract. Phase 14 routing adds `RoutingFailure` and `ExecutionFailure` entry kinds, but neither carries a "disagreement" semantic. | [Phase 14 plan](../../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md) D02, D09 |

This row is not counted as a third user-goal verdict.

---

## 14. Corroborating-test notes (non-proof)

The tests below corroborate individual contracts but use test doubles (`AgentExecutionTestSupport`, `ConversationsTestSupport`, in-memory `ConversationStore`, mocked `IAgentExecutionCoordinator`) rather than production `Program.ConfigureServices` composition. A passing test does not prove production reachability; it proves the underlying seam behaves as documented under the test setup.

- [MentionParserTests.cs](../../../../tests/Zaide.Tests/Features/Agents/Application/MentionParserTests.cs) — 10 tests: no-mention, recognized mention, case-insensitive, unknown, ambiguous duplicates, multiple mentions, mention-stripping, empty content after strip, empty input, caller-supplied names. All use the parser in isolation.
- [AgentRouterTests.cs](../../../../tests/Zaide.Tests/Features/Agents/Application/AgentRouterTests.cs) — 8 tests: no-mention, valid mention, **mention without open target panel** (proves `GetOrCreatePanelForActor` behavior), unknown target with `ConversationEntry.RoutingFailure` projection, ambiguous, multiple mentions, empty input, empty content after strip. Uses an in-memory test double `IActorCatalog` and a `Mock<IAgentExecutionCoordinator>`.
- [ActorCatalogTests.cs](../../../../tests/Zaide.Tests/Features/Conversations/Application/ActorCatalogTests.cs) — covers canonical seeds, custom registration, conflicting registration, fallback actors, ordering of `ListAgents()`.
- [AgentPanelHostTests.cs](../../../../tests/Zaide.Tests/Features/Agents/Presentation/AgentPanelHostTests.cs) — 30+ tests for the thin host (collection, active selection, get-or-create, close lifecycle, draft sync, output projection disposal).
- [Phase14M7ParityBridgeTests.cs](../../../../tests/Zaide.Tests/Features/Townhall/Presentation/Phase14M7ParityBridgeTests.cs) — includes `Routing_MentionWithoutOpenTargetPanel_UsesCatalogActorId` (L149–168) — proves the catalog-resolved routing path through `TownhallViewModel.SendMessageAsync`; this is the closest automated corroboration of the `A1-MR-03` contract, but uses a fake backend via `AgentExecutionTestSupport.CreateCoordinatorWithFakeBackend`, not the production Native Harness / ACP backends.
- [TownhallDirectSendTests.cs](../../../../tests/Zaide.Tests/Features/Townhall/Presentation/TownhallDirectSendTests.cs) — covers direct-send dispatch but does not assert mention routing explicitly.

**Distinction from production composition:** the tests use `ConversationsTestSupport.CreateStore()` / `CreateCatalog()` / `CreatePanelHost(...)` factory helpers, not the production DI root. `MentionParser`, `AgentRouter`, `ActorCatalog`, `IAgentPanelHost`, `IConversationStore`, and `TownhallViewModel` are constructed in-test. Production reachability is established separately by the source-trace in §6 and the DI registration in §12.

---

## 15. A3 clean-profile smoke constraints for this journey

A3 for `A2_MULTI_AGENT_ROUTING` must respect the A0–A4 disposable-profile rules and the additional constraints inherited from [A2_AGENT_SEND §12](./A2_AGENT_SEND.md#12-a3-clean-profile-smoke-constraints-send-slice).

1. **Disposable `XDG_CONFIG_HOME` first** — A3 must set a disposable absolute config root **before** constructing the service provider, so `ConversationPersistenceService` resolves under the disposable tree only.
2. **Routing from a Townhall direct conversation** is the only entry point. A3 must open a direct conversation (People panel → agent click) before testing `@mention` routing. Channel send cannot reach the router.
3. **Negative-path smoke (routing failures) is executable on a clean profile without backend binding** — typing `@Ghost`, `@Alpha @Beta`, `@Alpha`, or a lone `@` (in the active direct conversation) all produce a `ConversationEntry.RoutingFailure` on the source `ConversationId` and a Townhall `AgentError` entry. These reach the parser, the router, and the projection without any backend binding.
4. **Duplicate-name ambiguity (`@Twin`) is not directly constructible through clean production UI** — `ActorCatalog` seeds only the canonical actors at construction (`Human`, `Townhall Agent`, `Alpha`, `Beta`, `Gamma`, `Delta`) per [CanonicalActorSeeds.cs](../../../../src/Features/Conversations/Application/CanonicalActorSeeds.cs); the catalog is not user-extensible in the current shell because no UI calls `IAgentPanelHost.CreatePanel(...)` (the legacy chrome was removed in Phase 14 M8). To exercise the `"Ambiguous target"` branch through the router, A3 requires an **authorized setup hook** (e.g., an A3-only test fixture that calls `RegisterOrGetCustomPanelActor` twice with the same display name before invoking the router) or a lower-level parser unit test. A3 may NOT report the ambiguity case as a clean-profile smoke without that setup hook.
5. **Positive-path smoke (routed execution landing in target conversation) remains blocked by the backend-binding UI gap** — successful `UserMessageAdmitted` / `AssistantMessageCompleted` / admitted-run terminal events go to the **target** `ConversationId`, but the production UI provides no way to bind a backend (per A2_AGENT_SEND §4.11). Without binding, the default path rejects before `AgentSessionService.SendAsync` and the routing failure is not what the user observes. A3 must either (a) record a negative-path-only smoke, (b) bind a backend through an authorized A3-only test hook (none currently exists), or (c) defer positive-path smoke to a future A2 slice that addresses the binding gap.
6. **Mention parser `"Empty input"` branch is short-circuited by `SendMessageAsync`** — typing a whitespace-only draft is rejected at `TownhallViewModel.SendMessageAsync` L435–439 before the router is invoked; the parser's `"Empty input"` reason is reachable only via parser unit tests, not via the production UI. The `"Empty mention target"` branch (lone `"@"`) is reachable via the production UI.
7. **`A1-XX-02` debate surface** — no smoke required; the disposition is **confirmed absent**, so the negative observation ("no debate surface exists in the running app") is itself the smoke result.

---

## 16. Exact next recommended A2 slice (explicitly not started)

**Recommended slice name:** `A2_TRACE_MEMORY_USAGE_TERMINATION` (or equivalent). It would assign full verdicts to the `A1-TC-02`, `A1-TC-03`, `A1-TC-08`, and `A1-TC-09` rows (trace, memory, usage/cost, explicit termination) and address the open `A1-XX-03` ambiguity. The current `A2_AGENT_SEND` slice already mapped the send and event-projection path; the routing slice (`A2_MULTI_AGENT_ROUTING`) confirmed the catalog resolution and routing-failure projection. The remaining unverified journey in the agent layer is the transparency / lifecycle surface (Phase 21 M2/M3/M5/M6 contracts), which A1-XX-03 flagged as the highest-risk area for V1–V3 product reality.

This next slice is **explicitly not started** in this audit; only the routing slice is delivered here. A2 remains in progress with `A2_AGENT_SEND` + `A2_MULTI_AGENT_ROUTING` complete.

---

## 17. Final safety and working-tree report

| Item | Status |
|------|--------|
| Branch | `master` |
| `git rev-parse HEAD` at session start | `4cafa7e382ca3f05e0d4f65d80d693f2a284c7e4` (matches baseline) |
| Working tree modified | No (no production-code edits, no test edits, no doc edits to existing files) |
| New file produced | `docs/audits/v1-v3-product-reality/evidence/A2_MULTI_AGENT_ROUTING.md` (this file) |
| App launched | No |
| Build executed | No |
| Test suite executed | No |
| A3 smoke executed | No |
| Corrective implementation | None |
| Stabilization work | None |
| V4 work | None |
| A3 / A4 / next-slice work | None |
| Commit | None |
| Push | None |

---

*A2_MULTI_AGENT_ROUTING complete. Read-only audit; no fixes, no A3 work, no commit, no push, no next slice.*
