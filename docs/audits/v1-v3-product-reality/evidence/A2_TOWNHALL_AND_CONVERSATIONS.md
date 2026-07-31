# A2 Wiring Audit — `A2_TOWNHALL_AND_CONVERSATIONS`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_TOWNHALL_AND_CONVERSATIONS`
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`f69f86569000bba37b99eedcef171257b014066c` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `f69f86569000bba37b99eedcef171257b014066c` |
| `git rev-parse origin/master` | `f69f86569000bba37b99eedcef171257b014066c` |
| Working tree at audit start | Clean (`git status --short` empty) |
| Six published A2 evidence files | Present (Agent Send, Multi-Agent Routing, Trace/Memory/Usage/Termination, Restart/Recovery/Context, Tools/Permissions, Agent Creation/Backend Onboarding) |
| This slice evidence file before write | Absent |
| A1 acceptance authority | [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md) (2026-07-30) |
| Production code modified | No |
| Tests modified | No |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` edited | No |
| Earlier evidence edited | No |
| Issues / deferred findings edited | No |
| Real user profile read/written | No |
| App launched | No |
| Build or tests run | No |
| A3 executed | No |
| Commit / push | No |

**Safety boundary:** this slice is A2 wiring inspection only. Production
source is verdict authority. Tests and historical closeout documents are
corroboration only. Runtime rendering, scroll, focus, and clean-profile
restart behavior are not claimed from source alone.

**Verdict rows (this slice only):** `A1-TH-01`, `A1-TH-02`, `A1-TH-04`,
`A1-TH-05`. No new verdicts for AS, MR, TC, AC, or XX rows.

---

## 2. Sources inspected

### 2.1 Documentation

- [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§9 Townhall rows; §17.8 A2 progress)
- [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- Published A2 evidence:
  - [A2_AGENT_SEND.md](./A2_AGENT_SEND.md)
  - [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md)
  - [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./A2_TRACE_MEMORY_USAGE_TERMINATION.md)
  - [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md)
  - [A2_TOOLS_PERMISSIONS.md](./A2_TOOLS_PERMISSIONS.md)
  - [A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md](./A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)
- V1 roadmap Phase 4 Townhall goal:
  [PHASES.md §"Phase 4: Agent Workspace Foundations"](../../../roadmap/PHASES.md#phase-4-agent-workspace-foundations)
- [Phase 6 plan](../../../phases/v1/phase-6/IMPLEMENTATION_PLAN.md)
- [Phase 6.1 plan](../../../phases/v1/phase-6.1/IMPLEMENTATION_PLAN.md)
- [Phase 14 plan](../../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md)
  (D02–D06, D09, D11–D15, D17; M2–M8)
- [Phase 14 M9 evidence](../../../phases/v3/phase-14/M9_MANUAL_EVIDENCE.md)
- [Phase 14 M9 F1 evidence](../../../phases/v3/phase-14/M9_F1_MANUAL_EVIDENCE.md)
- [DF-001 closed residual](../../../deferred/closed/DF-001-agent-surface-townhall-tab.md)
- Phase 21 conversation/persistence corroboration via
  [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md)
  and [Phase 21 M7 closeout](../../../phases/v3/phase-21/M7_CLOSEOUT_EVIDENCE.md)

### 2.2 Production source (minimum required + supporting)

- Townhall presentation/domain:
  [TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs),
  [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs),
  [TownhallState.cs](../../../../src/Features/Townhall/Domain/TownhallState.cs),
  [TownhallMessage.cs](../../../../src/Features/Townhall/Domain/TownhallMessage.cs)
  (`TownhallMessageKind`, `FilterMode`),
  [TownhallNavigationPanel.cs](../../../../src/Features/Townhall/Presentation/TownhallNavigationPanel.cs),
  [TownhallPeoplePanel.cs](../../../../src/Features/Townhall/Presentation/TownhallPeoplePanel.cs),
  [TownhallChatPanel.cs](../../../../src/Features/Townhall/Presentation/TownhallChatPanel.cs),
  [TownhallEntryProjection.cs](../../../../src/Features/Townhall/Presentation/TownhallEntryProjection.cs),
  [TownhallConversationUiState.cs](../../../../src/Features/Townhall/Presentation/TownhallConversationUiState.cs),
  [TownhallConversationPersistenceBridge.cs](../../../../src/Features/Townhall/Presentation/TownhallConversationPersistenceBridge.cs),
  [TownhallInputArea.cs](../../../../src/Features/Townhall/Presentation/TownhallInputArea.cs)
- Conversations domain/application/infrastructure:
  [ConversationStore.cs](../../../../src/Features/Conversations/Application/ConversationStore.cs),
  [Conversation.cs](../../../../src/Features/Conversations/Domain/Conversation.cs),
  [ConversationEntry.cs](../../../../src/Features/Conversations/Domain/ConversationEntry.cs),
  [ConversationEntryKind.cs](../../../../src/Features/Conversations/Domain/ConversationEntryKind.cs),
  [ConversationParticipants.cs](../../../../src/Features/Conversations/Domain/ConversationParticipants.cs),
  [DirectParticipantPairKey.cs](../../../../src/Features/Conversations/Application/DirectParticipantPairKey.cs),
  [ConversationPersistenceService.cs](../../../../src/Features/Conversations/Infrastructure/ConversationPersistenceService.cs),
  [ConversationSnapshotSerializer.cs](../../../../src/Features/Conversations/Infrastructure/ConversationSnapshotSerializer.cs),
  [ConversationWorkspaceSnapshot.cs](../../../../src/Features/Conversations/Infrastructure/ConversationWorkspaceSnapshot.cs),
  [CanonicalActorSeeds.cs](../../../../src/Features/Conversations/Application/CanonicalActorSeeds.cs),
  [ActorCatalog.cs](../../../../src/Features/Conversations/Application/ActorCatalog.cs)
- Agents routing/projection/host:
  [AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs),
  [AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs),
  [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs),
  [IAgentPanelHost.cs](../../../../src/Features/Agents/Presentation/IAgentPanelHost.cs)
- Shell / DI:
  [MainLayoutBuilder.cs](../../../../src/App/Shell/MainLayoutBuilder.cs),
  [RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs),
  [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs),
  [TownhallServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs),
  [ConversationsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/ConversationsServiceCollectionExtensions.cs),
  [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs)

### 2.3 Tests (corroboration only; not verdict authority)

- `Phase14M8PanelRetirementTests`, `Phase14M7ParityBridgeTests`,
  `Phase14F1ConversationContextTests`, `TownhallDirectSendTests`,
  `AgentRouterTests`, `ConversationStoreTests`,
  `ConversationPersistenceTests`, `RightColumnHostSourceTests`

---

## 3. Four-row verdict table

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-TH-01` | **Wired-with-gap** | Townhall is shell-reachable as the center workspace. Three channels seed on first run; channel select/send and All/Chat/Activity filtering are wired. Channel send and channel-switch append authoritative `ConversationEntry` records to `ConversationStore` (snapshot-eligible; debounced persistence composed) and project into the active channel collection. **Gaps:** the Phase 4 eight-kind *presentation* taxonomy (`TownhallMessageKind`) still exists, but authoritative production entries use six `ConversationEntryKind` values; `AgentThink` and pure `ToolCall` presentation kinds have no production producer; non-chat kinds share one compact row style (not eight distinct renderers); channel-switch auto-log exists, direct-switch does not; `FilterMode` is session-only (not in snapshot); no production UI creates custom channels; unknown/legacy entry kinds are silently dropped on restore. |
| `A1-TH-02` | **Wired** | Direct conversations satisfy Phase 14 D02–D06: a user can open a DM with an agent (People → Zaide Agent); directs are private by default with no implicit public-channel copy; UI selection is presentation state; every admitted entry has one owning `ConversationId`; find-or-create uses an unordered two-actor pair; panel identity is not conversation identity. Per-conversation drafts and last-read/unread are wired into the snapshot pipeline (restart/flush composition gaps stay under [A1-TC-04](./A2_RESTART_RECOVERY_AND_CONTEXT.md), not re-verdicted here). Contextual limitations that **do not** fail this row: People exposes Zaide Agent but not catalog Alpha–Delta; multi-window sync is an `A1-XX-05` disposition; malformed/unknown peer recovery is not a documented TH-02 success/failure condition; backend bind/send-response gaps belong to AS/AC. |
| `A1-TH-04` | **Wired** | Dedicated Agent Panel chrome is absent from the shell; `RightColumnHost` is editor-only; no production command/menu/shortcut reopens panel chrome; residual `IAgentPanelHost` / `AgentPanelHost` is a non-visual execution adapter; Townhall is the sole shipped user re-entry surface for agent DMs; existing directs are navigable from the Direct list; one `ConversationStore` owns history. Residual host/DI/tests must not be mistaken for panel chrome ([DF-001 residual](../../../deferred/closed/DF-001-agent-surface-townhall-tab.md)). |
| `A1-TH-05` | **Wired-with-gap** | From a direct conversation, ordinary `@mention` routing failures append `RoutingFailure` on the **source** conversation and project as Townhall `AgentError`. Successful routed execution and admitted terminal failures land on the **target** direct conversation; target nav can show unread. **Gaps:** active source chat does not show successful routed request/response; channel send never reaches the router (reconcile [A2_MULTI_AGENT_ROUTING](./A2_MULTI_AGENT_ROUTING.md) `A1-MR-03`); pre-admission/session rejection produces no conversation entry (reconcile [A2_AGENT_SEND](./A2_AGENT_SEND.md) `A1-AS-02` / ISSUE-008); unbound-backend failure remains invisible in chat. Do **not** reopen `A1-MR-01` / `A1-MR-03` verdicts. |

Verdict definitions: [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

---

## 4. Townhall shell / user-reachability map

| Surface | Present in production shell? | How reached | Evidence |
|---------|------------------------------|-------------|----------|
| Townhall center column | Yes | `MainLayoutBuilder` constructs `TownhallView` in column 3; `MainWindow` assigns `ViewModel.TownhallViewModel` | [MainLayoutBuilder.cs](../../../../src/App/Shell/MainLayoutBuilder.cs) L103–107; [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs) L192–193 |
| People panel | Yes | Left sidebar top of Townhall | [TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L58, L104–119 |
| Channels + Directs navigation | Yes | Left sidebar bottom (`TownhallNavigationPanel`) | same |
| Chat + filter + input | Yes | Right side of Townhall | [TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L77–84, L156–189 |
| Dedicated Agent Panel chrome | No | — | [RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs) L12–15, L37–61 |
| Settings / palette path that opens Townhall | Not required | Townhall is always in the default shell layout | layout builder |

**Seeded channels (clean first-run path):**

| Channel id | Name | Pinned |
|------------|------|--------|
| `channel-1` | `townhall-main` | yes |
| `channel-2` | `ai-status` | no |
| `channel-3` | `codebase-refactor` | yes |

Source: `InitializeSeededSession` in
[TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs)
L970–998. Restored sessions use snapshot channel list via
[TownhallConversationPersistenceBridge.ApplyRestoredSnapshot](../../../../src/Features/Townhall/Presentation/TownhallConversationPersistenceBridge.cs)
L40–49.

**Custom channels:** no production UI command creates additional channels.
Snapshot restore can rehydrate whatever channel list was last persisted
(including historically non-seed names if present in the file), but there is
no shipped create-channel workflow.

**People roster (seeded):**

| Display | Role | Open DM? |
|---------|------|----------|
| User (`CanonicalHuman`) | `user` | No (row not openable) |
| Zaide Agent (`CanonicalTownhallAgent`) | `agent` | Yes |

Source: `SeedWorkspaceAgents` L1000–1021; openable gate in
[TownhallPeoplePanel.cs](../../../../src/Features/Townhall/Presentation/TownhallPeoplePanel.cs)
L168–196 (`Role == "agent"`). Catalog seeds Alpha/Beta/Gamma/Delta exist for
routing/execution but are **not** added to `TownhallState.Agents`.

---

## 5. Channel send / switch / activity wiring map

Trace (channel):

```
User selects channel (nav)
  → SelectChannelCommand / SelectConversationCommand
  → SelectConversation(ConversationId.ForChannel(...))
  → ApplyChannelSelection
     · set ActiveChannelId / IsActive flags
     · if switching between channels: LogActivity(ChannelEvent, "Switched to #name")
     · bind Messages to ChannelMessages[channelId]
  → MarkConversationRead (when markRead)
User types + Enter/Send
  → TownhallInputArea.SendRequested
  → SendMessageCommand → SendMessageAsync
  → ActiveChannelId branch: LogActivity(UserChat, draft)
  → ClearActiveConversationDraft
```

| Behavior | Wired? | Authoritative store / persistence pipeline | Notes |
|----------|--------|--------------------------------------------|-------|
| Channel selection | Yes | Presentation + optional `ChannelEvent` append | [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L417–419, L499–550, L633–669 |
| Channel send | Yes | `UserChat` append on channel conversation; snapshot-eligible; debounced persistence composed | L441–450 → `LogActivity` L866–880 |
| Auto-log on channel switch | Yes | `ChannelEvent` on **destination** channel (`ActiveChannelId` already updated); same store/persistence pipeline | L649–658 |
| Auto-log on direct switch | No | n/a | `ApplyDirectSelection` L671–686 has no `LogActivity` |
| Dual-write projection | Yes for channel path | Store append + `ChannelMessages` projection | `AppendMirroredActivity` L882–921 requires `ConversationKind.Channel` |
| Direct entry live update | Yes when active | Store append + `Messages.Add` when conversation is active | `OnConversationEntryAppended` L821–858 |
| Direct history rebuild on select | Yes | Rebuilt from in-memory store entries | `ProjectDirectMessages` L740–748 |

**Persistence boundary for channel send/switch (do not claim immediate durability):**
`LogActivity` → `ConversationStore.AppendEntry` raises `EntryAppended` →
`ConversationPersistenceService` schedules a **250 ms** debounced save. Entries
are authoritative in the store and **snapshot-eligible** once appended. A save
that completes should preserve the activity across restart. Durability is **not**
guaranteed if the process exits or is killed before the debounce/flush completes;
Zaide’s explicit owned shutdown sequence is not proven to dispose/flush
`ConversationPersistenceService` (see [A1-TC-04](./A2_RESTART_RECOVERY_AND_CONTEXT.md);
not re-verdicted here).

`LogActivity` no-ops if `ActiveChannelId` is null or the channel conversation is
missing (L873–877). `AppendMirroredActivity` refuses non-channel conversations
(L893–895) — agent direct events never use this dual-write path.

---

## 6. Entry-kind producer / render / filter matrix

### 6.1 Two taxonomies (do not conflate)

| Taxonomy | Members | Role |
|----------|---------|------|
| Authoritative `ConversationEntryKind` | `UserChat`, `AssistantResponse`, `RoutingFailure`, `ExecutionFailure`, `ChannelEvent`, `SystemNotification` (6) | Store truth; factories on `ConversationEntry` |
| Presentation `TownhallMessageKind` | `Chat`, `ChannelEvent`, `AgentAction`, `AgentThink`, `ToolCall`, `ToolResult`, `AgentError`, `System` (8; Phase 4 schema) | UI kind for filter/render |

Phase 4 claimed an “8-kind entry taxonomy” on the Townhall presentation model
([PHASES.md Phase 4](../../../roadmap/PHASES.md#phase-4-agent-workspace-foundations)).
Domain entries are now six typed kinds; some presentation kinds are derived
from `SystemNotification` payload prefixes, not first-class domain kinds.

### 6.2 Producer → kind → filter → render → persistence

| Production producer | Normalized path / store write | `ConversationEntryKind` | Townhall projection kind | Filter bucket | Visible rendering (source-proven structure) | Persistence pipeline |
|---------------------|-------------------------------|-------------------------|--------------------------|---------------|-----------------------------------------------|----------------------|
| Channel send (`LogActivity`) | Store append + dual-write | `UserChat` | `Chat` | Chat / All | Chat row (avatar/header/body) | Authoritative store entry; snapshot-eligible; debounced persistence composed (250 ms; not guaranteed before fast exit — TC-04) |
| Direct admit user message | `AgentConversationEventProjection.ProjectUserMessageAdmitted` | `UserChat` | `Chat` | Chat / All | Chat row | Same store/pipeline (owning direct) |
| Assistant complete | `ProjectAssistantMessageCompleted` | `AssistantResponse` | `Chat` (`Assistant: …` content) | Chat / All | Chat row | Same store/pipeline (owning conversation) |
| Channel switch | `LogActivity` | `ChannelEvent` | `ChannelEvent` | Activity / All | Compact log row | Authoritative store entry; snapshot-eligible; debounced persistence composed (same boundary as channel send) |
| Ordinary routing failure | `AgentRouter` → `ProjectRoutingFailure` | `RoutingFailure` | `AgentError` (`Routing failed: …`) | Activity / All | Compact log row | Store/pipeline on **source** conversation |
| Admitted run terminal failure | `ProjectFailureReported` / `ProjectRunTerminalFailure` | `ExecutionFailure` | `AgentError` (`Error: …`) | Activity / All | Compact log row | Store/pipeline on owning conversation (routed → target) |
| Action result reported | `ProjectActionResultReported` | `SystemNotification` (`zaide-action\|v1\|…`) | `ToolResult` / `AgentError` / `AgentAction` by result kind | Activity / All | Compact log row + formatted summary | Store/pipeline when event emitted |
| Backend activity reported | `ProjectBackendActivityReported` | `SystemNotification` (`zaide-backend-activity\|v1\|…`) | `AgentAction` | Activity / All | Compact log row | Store/pipeline when event emitted |
| Plain system notification (no special prefix) | any `SystemNotification` content | `SystemNotification` | `System` | Activity / All | Compact row (warning icon brush) | Store/pipeline when written |
| `TownhallMessageKind.AgentThink` | — | — | — | — | **No production producer** | n/a |
| Pure `TownhallMessageKind.ToolCall` (distinct kind without action encoding) | — | — | — | — | **No production producer** as a domain kind; backend activity may *display* “Tool call” text under `AgentAction` | n/a |
| Pre-admission / `RunRejected` | Explicitly suppressed | none | none | invisible | **No entry** | n/a |
| Test-only `CreatePanel` / fixtures | May seed panels/actors | varies | varies | — | **Not clean-profile product behavior** | test-local |

Projection rules:
[TownhallEntryProjection.cs](../../../../src/Features/Townhall/Presentation/TownhallEntryProjection.cs)
L16–112, L210–223.
Filter: `ApplyFilter` L1325–1335 — ChatOnly keeps `Kind == Chat`; ActivityOnly keeps
`Kind != Chat`; All keeps everything.
Render: [TownhallChatPanel.cs](../../../../src/Features/Townhall/Presentation/TownhallChatPanel.cs)
L283–289 — only binary chat vs compact; compact uses one generic `Icon.Info`
for all non-Chat kinds (L361–363 comment: YAGNI).

**Unknown / legacy kinds on restore:**
`ConversationSnapshotSerializer.TryParseEntry` returns false for unparsable
kinds/rows and **skips** them (`continue`) — silent omission, not a user-visible
error ([ConversationSnapshotSerializer.cs](../../../../src/Features/Conversations/Infrastructure/ConversationSnapshotSerializer.cs)
L136–141, L177–180). Unsupported `ConversationEntryKind` in the switch throws
during parse and is caught by the surrounding try → also skip (L188–…).

**Runtime-unproven:** whether compact vs chat contrast is visually adequate;
scroll/filter interaction under large histories; focus after filter toggle.

---

## 7. Direct-conversation identity and privacy analysis

### 7.1 Identity

| Contract (Phase 14 D02–D06) | Production behavior | Evidence |
|-----------------------------|---------------------|----------|
| Find-or-create by unordered pair | `DirectParticipantPairKey.FromActors` sorts by ordinal `ActorId.Value`; index on `ConversationStore` | [DirectParticipantPairKey.cs](../../../../src/Features/Conversations/Application/DirectParticipantPairKey.cs) L7–37; [ConversationStore.cs](../../../../src/Features/Conversations/Application/ConversationStore.cs) L56–74 |
| Exactly two distinct participants | `ConversationParticipants.ForDirect` throws if equal | [ConversationParticipants.cs](../../../../src/Features/Conversations/Domain/ConversationParticipants.cs) L27–36 |
| Repeated open same conversation | `OpenDirectConversation` → `GetOrCreateDirectConversation` | [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L619–630 |
| Panel host reuses same store conversation | `CreatePanelFromActor` uses same get-or-create | [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L111–115 |
| One owning `ConversationId` per entry | `AppendEntry(conversationId, entry)` only | [ConversationStore.cs](../../../../src/Features/Conversations/Application/ConversationStore.cs) L131–147 |
| UI selection is presentation | `TownhallState.ActiveConversationId` / channel flags; not entry ownership | [TownhallState.cs](../../../../src/Features/Townhall/Domain/TownhallState.cs) L18–26; SelectConversation L499–550 |

### 7.2 Privacy

| Question | Answer | Evidence |
|----------|--------|----------|
| Is DM content implicitly copied to public channels? | **No production path** | `AppendMirroredActivity` rejects non-channel; Phase 14 M4 removed public mirror; `AgentTownhallMirrorCoordinator` absent from `src/` |
| Can routed success leak into the wrong conversation? | Entries write to `agentEvent.ConversationId` / `RouteRequest.ConversationId` (target panel conversation for routed send) | [AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L75–90; projection uses event conversation id |
| Source vs target ownership on route | Failures → source; admitted success/terminal → target | Reconcile [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md) § routing visibility |

### 7.3 People exposure vs catalog (context; not `A1-TH-02` verdict gaps)

| Actor | In catalog? | In People UI? | How to open DM |
|-------|-------------|---------------|----------------|
| User | Yes | Yes (not openable) | n/a |
| Zaide Agent | Yes | Yes | People click — **satisfies** “open a DM with an agent” |
| Alpha / Beta / Gamma / Delta | Yes | **No** | `@Name` from a direct conversation (creates target DM + nav row) or tests |
| Custom / fallback panel actors | Via registration APIs | **No product UI** | Test / residual host APIs only |

Catalog Alpha–Delta not appearing in People does **not** fail `A1-TH-02`: the
accepted row requires that a user can open a DM with **an** agent, not that
every catalog agent is People-listed. Zaide Agent is a valid reachable agent.

### 7.4 Edge cases and non-gap context (source-proven)

| Case | Behavior | Is an `A1-TH-02` verdict gap? |
|------|----------|------------------------------|
| Open DM with Human id | `OpenDirectConversation` returns immediately (L621–624) | No — not a documented TH-02 failure path |
| Missing peer on direct | `ResolveDirectPeerActorId` throws `InvalidOperationException` if no non-human participant (L699–704) | No — source limitation; not a documented entry/success/failure condition of this row |
| `CreatePanelForActor` unknown catalog id | Throws `ArgumentException` ([AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L89–94) | No — same |
| Malformed persisted direct pair count ≠ 2 | `ParseDirectParticipants` throws → conversation parse fails → conversation skipped on restore | No — same |
| Multi-window sync | **Absent** | **No** — scoped to [A1-XX-05](./A2_RESTART_RECOVERY_AND_CONTEXT.md) disposition, not Phase 14 D02–D06 / `A1-TH-02` |
| Backend unbound / send-response projection | Default clean path may reject without chat entry | **No** — AS/AC rows ([A2_AGENT_SEND](./A2_AGENT_SEND.md), [A2_AGENT_CREATION…](./A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)); does not negate DM identity/privacy wiring |

**Verdict for `A1-TH-02`:** **Wired** — production satisfies D02–D06 identity,
privacy, ownership, selection-vs-ownership, and unordered-pair find-or-create.

---

## 8. Draft / read / unread / persistence analysis

| Concern | Wired? | Persisted in conversation snapshot? | Notes |
|---------|--------|-------------------------------------|-------|
| Per-conversation draft map | Yes (`TownhallConversationUiState` + `IConversationDraftState`) | Yes (`drafts`) | Switch saves previous draft L507–510; load restores L545, L959 |
| Active input buffer `DraftText` | Yes | Via map + active id | Setter notifies presentation persist L109–113 |
| Last-read entry id | Yes | Yes (`lastReadEntryIds`) | `MarkConversationRead` / `AdvanceLastRead` |
| Unread presentation | Yes (`HasUnread` on channel + direct nav) | Derived from last-read vs latest entry | `IsUnread` L57–71 |
| Active conversation selection | Yes | Yes (`ActiveConversationId`) | Bridge capture L72–77 |
| Channel list | Yes | Yes | Bridge L59–66 |
| Conversation entries (channel + direct) | Yes | Yes (when a save completes) | Authoritative store append; snapshot-eligible; debounced 250 ms save |
| `FilterMode` | Yes (session) | **No** | Default `All`; not in `ConversationWorkspaceSnapshot` |
| Scroll position | n/a | Not exit-critical (Phase 14 D14) | Runtime-unproven |
| Sessions / runs / bindings / events / audit / usage / traces / memory | — | **No** (correct exclusion) | Reuse A1-TC-04 evidence |
| Multi-window draft/read sync | No | n/a | Absent (`A1-XX-05`; not an `A1-TH-02` gap) |

Persistence composition: `ConversationPersistenceService` constructed eagerly
with Townhall DI
([TownhallServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs)
L21–42); load on construction; **250 ms** debounced save on store append
(`EntryAppended`) and presentation change. Appended entries are **authoritative
store entries** and **snapshot-eligible**; they are **not** claimed immediately
durable after append. A save that completes should preserve entries, drafts,
and read state across restart. **TC-04 composition gaps** (silent load/save
failure feedback; Zaide’s explicit owned shutdown sequence not proven to
dispose/flush `ConversationPersistenceService`) remain under `A1-TC-04`
([A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md)) —
not re-verdicted here.

---

## 9. Agent Panel retirement and residual-host analysis

### 9.1 Four distinct layers

| Layer | Status | User-visible? |
|-------|--------|---------------|
| 1. Retired presentation chrome (`AgentPanelHostView`, `AgentPanelView`, right-column panel row, panel send UI) | **Absent** from production assembly / layout | No |
| 2. Still-live internal `IAgentPanelHost` / `AgentPanelHost` | **Present** singleton; get-or-create by `ActorId` | No chrome |
| 3. Unified `ConversationStore` | **Sole** conversation history authority | Indirectly via Townhall |
| 4. Townhall direct-conversation UI | **Sole** shipped DM re-entry surface | Yes |

### 9.2 Shell verification

| Check | Result | Evidence |
|-------|--------|----------|
| `RightColumnHost` contents | Editor tab bar, search bar, editor/welcome only | [RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs) L12–61 |
| `AgentPanelHostView` / `AgentPanelView` types | No production types (tests assert null) | retirement tests; `rg` empty under `src/` |
| Reopen panel chrome via command/menu/shortcut | No production caller of host `CreatePanel` / `ClosePanel` / `ActivatePanel` under `src/` | `rg` callers only in tests |
| Townhall DM open | People → `OpenDirectConversationCommand`; Direct nav → `SelectConversationCommand` | [TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L276–288 |
| Competing conversation truth | No second store; panel `OutputHistory` is projection over store | [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L117–119 |
| DF-001 | Closed with residual thin host | [DF-001](../../../deferred/closed/DF-001-agent-surface-townhall-tab.md) |
| Stale comment only | `MainLayoutBuilder` comment still says “Editor (top) + Agent Panel (bottom)” while constructing editor-only host | [MainLayoutBuilder.cs](../../../../src/App/Shell/MainLayoutBuilder.cs) L124–130 — **comment drift**, not chrome |

### 9.3 Residual host production callers

| API | Production caller |
|-----|-------------------|
| `GetOrCreatePanelForActor` | `TownhallViewModel.EnsurePanelForDirectConversation`; `AgentRouter` for routed target |
| `CreatePanel()` / `CreatePanel(id,name,avatar)` / `ClosePanel` / `ActivatePanel` | **No production callers** (tests only) |

Dormant panel types/tests/DI registration must not be counted as UI.

**Verdict for `A1-TH-04`:** **Wired** — retirement and Townhall re-entry match
the documented success condition; residual host is intentional non-visual support.

---

## 10. Routing source / target visibility matrix

Reconcile with [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md) and
[A2_AGENT_SEND.md](./A2_AGENT_SEND.md); do **not** reassign `A1-MR-01` /
`A1-MR-03`.

| Scenario | Source conversation shows | Target conversation shows | Townhall navigation shows | Explicit routing-failure entry? |
|----------|---------------------------|---------------------------|---------------------------|----------------------------------|
| Valid `@Beta hello` from Human↔Alpha DM | No user/assistant entries for the routed body | `UserChat` + later `AssistantResponse` or terminal failure on Human↔Beta | Target Direct row may get `HasUnread` if not active | No (success path) |
| Unknown mention `@Ghost` | `RoutingFailure` → `AgentError` | No new target needed | Source remains selected | Yes on source |
| Malformed `@` / empty strip / multiple mentions | `RoutingFailure` on source (when panel/store present) | n/a | Unchanged | Yes on source |
| Catalog miss (unknown display name) | Same as unknown | n/a | Unchanged | Yes on source |
| Target DM creation | n/a | Find-or-create via `GetOrCreatePanelForActor` | Direct list refresh on entry append | n/a |
| Unbound target backend (pre-admission reject) | No projected failure entry | No admitted entries | Possibly none | **No** (ISSUE-008 / AS-02) |
| Admitted success | No | Success entries | Unread if inactive | No |
| Admitted terminal failure | No | `ExecutionFailure` | Unread if inactive | No (execution failure, not routing failure) |
| Channel send with `@Name` | Channel `UserChat` only (router never called) | n/a | Channel selection | No |

**Does routed execution fall back to the wrong direct target?** Source-proven:
target panel is chosen by catalog `ActorId` match then
`GetOrCreatePanelForActor(targetActorId)`; direct-send without mention uses
source panel. No silent fallback to a different catalog actor after a failed
match — match failures become routing failures.

**Visibility vs documented Townhall routing goal (`A1-TH-05`):** routing
failures are explicit on the source DM; routed-flow **outcomes** are written as
authoritative store entries on the target DM (snapshot-eligible; debounced
persistence composed) and are navigable with unread affordance, but **not**
shown in the active source chat. That is partial Townhall visibility of routed
flow, not full “source conversation shows the routed request/response” behavior
from the older Phase 6.1 panel-mirror design.

---

## 11. Failure and rejection projection matrix

| Failure class | Store entry? | Kind | Owning conversation | Townhall chat | Notes |
|---------------|--------------|------|---------------------|---------------|-------|
| Ordinary mention parse/resolution failures | Yes | `RoutingFailure` | Source direct | `AgentError` when source active | Projected before session |
| Unknown source panel | No | — | — | Invisible | `TryCreateAndRecordRoutingFailure` returns null when source null |
| Pre-admission / unbound / `RunRejected` | No | — | — | Invisible; draft may clear | [A2_AGENT_SEND](./A2_AGENT_SEND.md); projection suppresses `RunRejected` |
| Admitted terminal failure | Yes | `ExecutionFailure` | Target (routed) or direct (non-routed) | Visible when that conversation active | |
| Action denied/failed (mediated result reported) | Yes | `SystemNotification` → error-ish presentation | Owning run conversation | Compact activity | Requires emitted `ActionResultReported` |
| Missing projection event | No | — | — | Invisible | “No normalized event ⇒ no Townhall row” |

---

## 12. DI registration and production-caller analysis

| Service | DI | Lifetime | Production caller(s) | User-reachable? |
|---------|----|----------|----------------------|-----------------|
| `TownhallState` | `AddZaideTownhall` | Singleton | `TownhallViewModel` | Via shell |
| `TownhallConversationUiState` | yes | Singleton | ViewModel, persistence bridge | Indirect |
| `IConversationWorkspacePersistenceBridge` → `TownhallConversationPersistenceBridge` | yes | Singleton | Persistence service | Indirect |
| `ConversationPersistenceService` | yes (eager for VM factory) | Singleton | VM factory; store/presentation events | Indirect |
| `TownhallViewModel` | yes | Singleton | `MainWindowViewModel` / shell bind | Yes |
| `IConversationStore` / `ConversationStore` | `AddZaideConversations` | Singleton | Townhall, router, projection, panel host | Indirect |
| `IActorCatalog` / `ActorCatalog` | yes | Singleton | Seeds + routing + people | Indirect |
| `IAgentPanelHost` / `AgentPanelHost` | `AddZaideAgents` | Singleton | Townhall ensure-panel; router; coordinator | Non-visual |
| `IAgentRouter` / `AgentRouter` | yes | Singleton | Townhall direct send only | Yes (direct DM send) |
| `AgentConversationEventProjection` | yes (agents module) | Singleton | Session event stream | Indirect projection |
| `TownhallView` | constructed in layout (not DI service) | per window layout | MainLayoutBuilder | Yes |

Channel send does **not** call the router. Parameterless `CreatePanel` has no
production UI caller.

---

## 13. Source-proven versus runtime-unproven findings

### Source-proven

- Townhall is always present in the main shell layout.
- Channel seed, channel select/send, channel-switch `ChannelEvent` logging into
  the authoritative store (snapshot-eligible; debounced persistence composed —
  not an immediate-durability claim).
- All/Chat/Activity filter wiring and Chat vs non-Chat classification.
- Direct find-or-create by unordered pair, one owning `ConversationId`,
  presentation-only selection, privacy (no public mirror) — **`A1-TH-02` Wired**.
- People open-DM for Zaide Agent (sufficient agent entry for TH-02); Direct nav
  for existing directs.
- Drafts, last-read, unread maps and snapshot fields composed.
- Panel chrome retirement; residual non-visual host.
- Routing failure entries on source; success/terminal entries on target.
- DI composition for Townhall + conversations + router.

### Contextual limitations (not `A1-TH-02` gaps)

- People does not list catalog Alpha–Delta; multi-window sync absent (`A1-XX-05`);
  unknown/malformed peer recovery is fail-closed/throw without dedicated UI;
  backend bind/send-response gaps belong to AS/AC.

### Test-only / not clean-profile product behavior

- `AgentPanelHost.CreatePanel` identity seeding of Alpha–Delta as panels.
- Duplicate-name ambiguity setups via custom registration.
- Unit tests that construct isolated stores without production DI.

### Runtime-unproven (require A3; not claimed here)

- Pixel/layout correctness of chat vs compact rows, filter toggles, unread dots.
- Scroll anchoring / new-message chip under live load.
- Keyboard/focus traversal of People rows (M9 residual: pointer-primary).
- Clean-profile first launch channel seed + empty history UX.
- Whether a channel send/switch entry survives a **fast exit/kill before the
  250 ms debounce/flush** (store append is source-proven; on-disk durability is
  not guaranteed before a completed save — TC-04).
- End-to-end restart restore of draft/unread/active selection after a
  **completed** save (source-wired; runtime path has TC-04 gaps).
- Bound-backend action/backend-activity rows appearing under real ACP/Harness
  runs (blocked by onboarding / binding gap AC-02 / XX-01).
- Whether user notices target-DM unread after routing without leaving source.

### Backend-dependent / onboarding-blocked

- Assistant responses, execution failures from real backends, action results,
  backend activity — require admitted runs and bound backends
  ([A2_AGENT_SEND](./A2_AGENT_SEND.md), [A2_AGENT_CREATION…](./A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)).

Green unit tests are **not** treated as proof of user-observable completion.

---

## 14. Reconciliation with prior A2 evidence

| Prior slice | Relationship to this slice | Reassignment? |
|-------------|----------------------------|---------------|
| [A2_AGENT_SEND](./A2_AGENT_SEND.md) | Confirms Townhall direct send, projection gaps (`RunRejected`), draft clear on reject; partial TH notes only. AS gaps do **not** reopen or demote `A1-TH-02`. | **No** AS verdicts reopened |
| [A2_MULTI_AGENT_ROUTING](./A2_MULTI_AGENT_ROUTING.md) | Source/target ownership, channel bypass, catalog resolution — reused for `A1-TH-05` visibility matrix | **No** MR verdicts reopened (`A1-MR-01` Missing; `A1-MR-03` Wired-with-gap stand) |
| [A2_RESTART_RECOVERY_AND_CONTEXT](./A2_RESTART_RECOVERY_AND_CONTEXT.md) | Snapshot fields, silent errors, no multi-window (`A1-XX-05`), shutdown flush gap — reused for drafts/unread/persistence pipeline; multi-window is **not** an `A1-TH-02` gap | **No** TC verdicts reopened |
| [A2_TOOLS_PERMISSIONS](./A2_TOOLS_PERMISSIONS.md) | ActionResult → Townhall only when event projected | **No** TP verdicts reopened |
| [A2_TRACE_MEMORY_USAGE_TERMINATION](./A2_TRACE_MEMORY_USAGE_TERMINATION.md) | Trace/memory/usage/termination not Townhall product surfaces | **No** TC/XX reopened |
| [A2_AGENT_CREATION…](./A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md) | No user agent create; binding unbound by default affects DM send outcomes (AS/AC), not DM identity/privacy | **No** AC/XX reopened |

This slice **assigns** the four TH rows that prior slices only partially covered.
Corrective round 1: `A1-TH-02` is **Wired** (not Wired-with-gap).

---

## 15. A3 disposable-profile constraints (described only; A3 not started)

A3 for this journey must use a disposable isolated profile only (never the real
user profile). Suggested constraints when A3 is later authorized:

1. **Cold launch:** confirm Townhall center column, three seed channels, People
   shows User + Zaide Agent only.
2. **Channel path:** send message; switch channel; observe “Switched to #…” on
   destination; toggle All/Chat/Activity; confirm filter is not restored after
   restart unless product changes.
3. **DM path:** open Zaide Agent DM twice → same conversation; send (expect
   unbound reject path without visible failure per AS-02 unless binding is
   authorized for the scenario); drafts survive conversation switches.
4. **Routing (no backend required for failures):** from a direct, send
   `@Ghost hello`, lone `@`, `@Alpha` with empty body → source shows routing
   failure entries.
5. **Routing success visibility:** only with an authorized bound backend (or
   disposable fixture binding **if** A3 charter allows): `@Beta …` from Alpha
   DM → confirm entries land on Beta DM + unread, not on Alpha chat.
6. **Panel retirement:** confirm no agent panel chrome; right column is editor.
7. **Persistence:** after a **completed** save (allow >250 ms debounce), draft +
   message + unread should restore across restart. Separately exercise fast
   exit/kill before debounce/flush (expect possible loss — TC-04). Do not claim
   multi-window sync. Do not treat an append alone as proven on-disk durability.
8. **Isolation (production DI allowed only under a disposable config root):**
   Full `Program.ConfigureServices` / production DI composition is allowed for a
   harness **only when** an absolute disposable `XDG_CONFIG_HOME` is established
   **before** provider creation. The contamination risk is production DI without
   a disposable config root, not production DI itself
   ([ISSUE-009](../../../issues/open/ISSUE-009-production-di-test-contaminates-conversation-store.md)
   explains why isolation must be established first). A production-DI harness is
   **not** a substitute for a user-observable product smoke. Never use the real
   profile or real conversation store.

A3 is **not** executed in this session. No disposable-profile harness is run here.

---

## 16. Next recommended slice

**Next recommended A2 slice:** `A2_FIRST_LAUNCH_AND_SETTINGS`

| Item | Value |
|------|-------|
| Slice name | `A2_FIRST_LAUNCH_AND_SETTINGS` |
| Goal rows | `A1-FL-01` … `A1-FL-06` |
| Evidence file | `docs/audits/v1-v3-product-reality/evidence/A2_FIRST_LAUNCH_AND_SETTINGS.md` |
| Status in this session | **Explicitly not started** — file not created; no FL verdicts assigned |

---

## 17. Verification and working-tree closeout

### 17.1 Required content checklist

| Required section | Present |
|------------------|---------|
| 1. Audit identity, baseline, safety | Yes |
| 2. Sources inspected | Yes |
| 3. Four-row verdict table | Yes |
| 4. Shell/user-reachability map | Yes |
| 5. Channel send/switch/activity map | Yes |
| 6. Entry-kind producer/render/filter matrix | Yes |
| 7. Direct identity/privacy analysis | Yes |
| 8. Draft/read/unread/persistence analysis | Yes |
| 9. Agent Panel retirement / residual host | Yes |
| 10. Routing source/target visibility matrix | Yes |
| 11. Failure/rejection projection matrix | Yes |
| 12. DI / production-caller analysis | Yes |
| 13. Source-proven vs runtime-unproven | Yes |
| 14. Reconciliation with prior A2 | Yes |
| 15. A3 constraints (described only) | Yes |
| 16. Next slice not started | Yes |
| 17. Verification closeout | Yes |

### 17.2 Truth-constraint self-check

| Constraint | Honored? |
|------------|----------|
| Enum member ≠ production producer | Yes (`AgentThink` / pure `ToolCall` marked unused) |
| Store entry ≠ user can see it | Yes (inactive conversation; source vs target) |
| Projected entry ≠ correct filter shows it | Filter classification documented; runtime look unproven |
| Navigation visibility ≠ source-conversation visibility | Explicit in §10 |
| Private DM ownership ≠ routed-flow discoverability | Explicit in §7 / §10 |
| Internal host ≠ panel chrome | Explicit in §9 |
| Test-only seed ≠ clean-profile | Explicit in §4 / §13 |
| No runtime claims from source alone | Yes |
| Prior-slice verdicts not reassigned | Yes |
| Each TH row once in primary table | Yes (exactly four rows) |

### 17.3 Closeout verification commands (post-write / corrective round 1)

Executed after writing/correcting this file only:

- Confirm exactly one untracked evidence file:
  `docs/audits/v1-v3-product-reality/evidence/A2_TOWNHALL_AND_CONVERSATIONS.md`
- Confirm no tracked modifications
- Whitespace check for the **untracked** file (ordinary `git diff --check` does
  **not** inspect untracked files):

  ```bash
  git diff --no-index --check /dev/null \
    docs/audits/v1-v3-product-reality/evidence/A2_TOWNHALL_AND_CONVERSATIONS.md
  ```

  Exit status **1 is expected** because the files differ; there must be
  **no whitespace-diagnostic output**.
- Relative Markdown paths and fragment links resolve
- Primary verdicts: `A1-TH-01` Wired-with-gap; `A1-TH-02` **Wired**;
  `A1-TH-04` Wired; `A1-TH-05` Wired-with-gap
- `A2_FIRST_LAUNCH_AND_SETTINGS` not created / not started

---

*End of `A2_TOWNHALL_AND_CONVERSATIONS` evidence (corrective round 1). Stop for re-audit. No commit or push.*
