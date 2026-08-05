# A2 Wiring Audit — `A2_AGENT_SEND`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_AGENT_SEND` (first authorized A2 slice)
**Evidence date:** 2026-07-30
**Baseline:** branch `master`, HEAD `26103ba2b79d0eec56441b089bd4682ab7f0873f` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch, build, test execution, production-code edits, commits, or pushes.

---

## 1. Baseline and safety confirmation

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `26103ba2b79d0eec56441b089bd4682ab7f0873f` |
| `git rev-parse origin/master` | `26103ba2b79d0eec56441b089bd4682ab7f0873f` |
| A1 acceptance authority | [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md) (2026-07-30) |
| Production code modified | No |
| Tests modified | No |
| Real user profile read/written | No |
| App launched | No |
| Build or tests run | No |

---

## 2. Scope and non-goals

### In scope

- Primary verdict rows: `A1-AS-01`, `A1-AS-02`
- Supporting partial coverage: `A1-TH-*` (Townhall projection), `A1-AC-*` (backend onboarding), `A1-XX-01`, `A1-XX-03` (as referenced by slice charter)
- Issues: [ISSUE-008](../../../issues/open/ISSUE-008-agent-response-not-showing.md), [ISSUE-009](../../../issues/closed/ISSUE-009-production-di-test-contaminates-conversation-store.md)
- Deferred: [DF-008](../../../deferred/open/DF-008-multiple-agent-connections.md), [DF-009](../../../deferred/open/DF-009-real-acp-integrations.md)
- Production wiring trace seams 1–12 per slice charter ([GOAL_MATRIX.md §17.5](../GOAL_MATRIX.md#175-recommended-first-a2-wiring-audit-slice))

### Non-goals

- A3 clean-profile smoke execution
- Corrective fixes
- Full verdicts for `A1-TH-*`, `A1-AC-*`, `A1-MR-*`, or `A1-TC-*` rows (partial coverage only where send path intersects)
- Runtime confirmation of backend candidates (Native Harness / ACP external smoke)

---

## 3. Verdict table

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-AS-01` | **Missing** | Documented Phase 5 user entry point (type and send in an agent panel) has no production UI. Agent Panel chrome was removed in Phase 14 M8; the right column is editor-only ([RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs) L12–15). Legacy `AgentExecutionService` remains registered but is not reachable from a panel send surface. |
| `A1-AS-02` | **Wired-with-gap** | Townhall direct-conversation send is user-reachable and wired through router → coordinator → session → registered backends, with event projection back to the conversation store and Townhall UI. Gaps: (1) no production UI binds backends, so the default path rejects before session admission; (2) pre-admission and `RunRejected` outcomes are not projected into the conversation; (3) draft clears on rejection without visible failure feedback (ISSUE-008). |

Verdict definitions: [AUDIT_PLAN.md §A2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

---

## 4. End-to-end production wiring trace

Legend per seam: **T** = type/method exists · **R** = registered in production DI · **C** = called by production path · **U** = reachable from user-visible entry point · **P** = result projected back to UI.

### 4.1 User-visible Townhall / direct-conversation send entry point

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| Townhall input + send gesture | ✓ | ✓ | ✓ | ✓ | — | `TownhallView` wires `_inputArea.SendRequested` → `OnSendRequested` ([TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L97–98, L255–264) |
| Open direct conversation | ✓ | ✓ | ✓ | ✓ | ✓ | People panel → `OpenDirectConversationCommand` ([TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L276–279); `OpenDirectConversation` in [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L619–631 |
| Shell hosts Townhall as center column | ✓ | ✓ | ✓ | ✓ | — | [MainLayoutBuilder.cs](../../../../src/App/Shell/MainLayoutBuilder.cs) L103–107; [MainWindow.axaml.cs](../../../../src/App/Shell/MainWindow.axaml.cs) L193 |

**Agent Panel (A1-AS-01 entry point):** not present. [RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs) L12–15 documents Phase 14 M8 retirement; layout is editor-only (L37–61).

### 4.2 Command / ViewModel handling

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `SendMessageCommand` | ✓ | ✓ | ✓ | ✓ | — | [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L306, L427, L433–497 |
| Channel vs direct branch | ✓ | ✓ | ✓ | ✓ | ✓ | Channel: `LogActivity` L441–450; Direct: router/coordinator path L453–496 |
| Busy / input gating | ✓ | ✓ | ✓ | ✓ | partial | `IsDirectSendBusy` / `IsInputEnabled` L166–185, L709–737 |

### 4.3 Conversation and actor identity resolution

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| Direct conversation find-or-create | ✓ | ✓ | ✓ | ✓ | ✓ | `GetOrCreateDirectConversation` ([TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L626–628) |
| Peer actor resolution | ✓ | ✓ | ✓ | ✓ | — | `ResolveDirectPeerActorId` L696–706 |
| Panel host for actor | ✓ | ✓ | ✓ | ✓ | — | `EnsurePanelForDirectConversation` → `GetOrCreatePanelForActor` L688–693, [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L100–123 |
| `@mention` catalog routing | ✓ | ✓ | ✓ | ✓ | partial | `AgentRouter.TryResolveTargetActor` uses `IActorCatalog.ListAgents` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L49–51, L95–136) — routing failures projected; execution rejections not (see §6) |

### 4.4 Backend-binding lookup and selection

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `AgentActorBackendBindingStore` | ✓ | ✓ | ✓ | — | status only | Empty at startup ([AgentActorBackendBindingStore.cs](../../../../src/Features/Agents/Application/AgentActorBackendBindingStore.cs) L14–15); `GetRequiredBackendId` throws when unbound L33–44 |
| Coordinator binding lookup | ✓ | ✓ | ✓ | — | — | [AgentExecutionCoordinator.cs](../../../../src/Features/Agents/Application/AgentExecutionCoordinator.cs) L109–124 |
| Binding status projection | ✓ | ✓ | ✓ | ✓ | ✓ | `RefreshActiveBackendBindingProjection` ([TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L1103–1131); `AgentBackendBindingPanel` is read-only status ([AgentBackendBindingPanel.cs](../../../../src/Features/Agents/Presentation/AgentBackendBindingPanel.cs) L11–66) |
| `BindNativeHarness` / `BindAcpRuntime` | ✓ | ✓ | **no production caller** | **no** | — | Only defined in [AgentActorBackendSelectionService.cs](../../../../src/Features/Agents/Application/AgentActorBackendSelectionService.cs) L58–86 and [AgentBackendBindingPresenter.cs](../../../../src/Features/Agents/Presentation/AgentBackendBindingPresenter.cs) L26–43; `rg` across `src/` finds no production UI or startup caller |

### 4.5 Router / session / execution coordination

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `IAgentRouter` | ✓ | ✓ | ✓ | ✓ | partial | Registered [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L91–92; Townhall prefers router L474–489 |
| `AgentExecutionCoordinator` | ✓ | ✓ | ✓ | ✓ | panel only | Factory [Program.cs](../../../../src/App/Composition/Program.cs) L44–53; `SendAsync` L91–195 |
| `AgentSessionService` | ✓ | ✓ | ✓ | when bound | via events | `SendAsync` L117–224; admission + backend dispatch L550–635 |
| One-in-flight per conversation | ✓ | ✓ | ✓ | ✓ | ✓ | Coordinator `_inFlightRuns` L27–28, L484–512; Townhall busy tracking L709–737 |

### 4.6 Native Harness or ACP backend dispatch

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `NativeHarnessAgentBackend` | ✓ | ✓ | when bound | — | via session events | Registered L63–71 |
| `AcpActionCapableAgentBackend` | ✓ | ✓ | when bound | — | via session events | Registered L72–79 |
| `IAgentBackend` enumeration | ✓ | ✓ | ✓ | — | — | Two backends registered as `IAgentBackend` L80–89 |
| `LegacyOpenAiCompatibleAgentBackend` | ✓ | **not registered** | — | — | — | Exists in infrastructure but absent from `AddZaideAgents` registration |
| Backend execution observer | ✓ | ✓ | ✓ | — | — | `ObserveBackendAsync` [AgentSessionService.cs](../../../../src/Features/Agents/Application/AgentSessionService.cs) L677–746 |

**Default production path:** unbound actor → coordinator catches `InvalidOperationException` at binding lookup → returns `Rejected` without calling `AgentSessionService` ([AgentExecutionCoordinator.cs](../../../../src/Features/Agents/Application/AgentExecutionCoordinator.cs) L112–124). No backend dispatch occurs.

### 4.7 Normalized response / outcome events

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| `AgentEventStream` | ✓ | ✓ | ✓ | — | — | [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L36 |
| Session lifecycle events | ✓ | ✓ | ✓ | — | partial | Emitted in `AgentSessionService` (e.g. `UserMessageAdmitted` L584–590, `AssistantMessageCompleted` via `ProcessBackendEventLocked` L791–812) |
| `AgentConversationEventProjection` subscription | ✓ | ✓ | ✓ | — | — | Registered L40; eagerly resolved in [Program.CreateAgentExecutionCoordinator](../../../../src/App/Composition/Program.cs) L46 to activate subscription |

### 4.8 Projection into the owning conversation

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| User message admitted → `ConversationStore` | ✓ | ✓ | when admitted | — | ✓ | `ProjectUserMessageAdmitted` [AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs) L281–317 |
| Assistant response → store | ✓ | ✓ | when completed | — | ✓ | `ProjectAssistantMessageCompleted` L319–357 |
| Townhall `Messages` refresh | ✓ | ✓ | ✓ | ✓ | ✓ | `OnConversationEntryAppended` [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L821–858; `FilteredMessages` → chat panel [TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L328–330 |
| Routing failure → store | ✓ | ✓ | ✓ | ✓ | ✓ | `AgentRouter.TryCreateAndRecordRoutingFailure` → `ProjectRoutingFailure` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L147–176) |

### 4.9 Rejected, unbound, failed, cancelled, timed-out, disconnected, indeterminate feedback

| Outcome | Session emits | Projected to conversation | Visible in Townhall chat | Evidence |
|---------|---------------|---------------------------|--------------------------|----------|
| Unbound (pre-session) | No | No | No (draft cleared) | Coordinator L112–124; Townhall clears draft L483–485 |
| `RunRejected` (session admission) | Yes | **Explicitly skipped** | No | `case AgentEventKind.RunRejected: break;` [AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs) L131–133; `ProjectTerminalFailureEntry` requires `_admittedRunIds` L379–385 |
| Routing failure | N/A (router) | Yes (`RoutingFailure`) | Yes | [AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L168–173 |
| Failed / timed-out / disconnected / indeterminate (admitted run) | Yes | Yes (`ExecutionFailure`) | Yes | `ProjectRunTerminalFailure` L369–373; `MapRunStatus` in coordinator L417–428 |
| Cancelled (admitted run) | Yes | Yes | Yes | `RunCancelled` handled L123–128 |
| Busy while in-flight | N/A | N/A | partial (`IsDirectSendBusy`) | Townhall L166–185 |

### 4.10 Production DI registration and composition

| Service | Registered | Resolved on startup | Evidence |
|---------|------------|---------------------|----------|
| `AddZaideAgents` + `AddZaideTownhall` | ✓ | partial | [Program.ConfigureServices](../../../../src/App/Composition/Program.cs) L28–35 |
| `TownhallViewModel` (full deps incl. router + backend selection) | ✓ | ✓ | [TownhallServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs) L28–43; via `MainWindowViewModel` |
| `AgentConversationEventProjection` | ✓ | ✓ (eager via coordinator factory) | [Program.cs](../../../../src/App/Composition/Program.cs) L46 |
| `ConversationPersistenceService` | ✓ | ✓ (side effect of `TownhallViewModel` factory) | [TownhallServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs) L21–27, L40 |
| `AgentBackendBindingPresenter` | ✓ | not wired to UI | [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L53 |

### 4.11 User-visible backend configuration / binding entry point

| Surface | Exists | Configures binding | Evidence |
|---------|--------|-------------------|----------|
| `AgentBackendBindingPanel` | ✓ (status only) | No actions | [AgentBackendBindingPanel.cs](../../../../src/Features/Agents/Presentation/AgentBackendBindingPanel.cs) — `SetBindingProjection` only |
| Settings | ✓ | No agent/backend section | [SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs) L24–29 (Editor, Llm, Keybindings, Debug only) |
| `AgentBackendBindingPresenter` | ✓ (DI) | No production caller | Presenter has bind methods; no View/ViewModel invokes them |

**Conclusion:** Backend binding is **not user-configurable** in production. Status display shows "Unbound" for direct conversations ([AgentActorBackendSelectionService.GetSnapshot](../../../../src/Features/Agents/Application/AgentActorBackendSelectionService.cs) L28–38).

### 4.12 Persistence / disposal behavior (ISSUE-009)

| Seam | Finding | Evidence |
|------|---------|----------|
| Store path | Production XDG config `conversations/conversations.json` | [ConversationStorePathResolver.cs](../../../../src/Features/Conversations/Infrastructure/ConversationStorePathResolver.cs) L14–19 |
| Draft persistence trigger | `DraftText` setter → `NotifyPresentationPersisted` → debounced save | [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L109–114, L1054–1055; [ConversationPersistenceService.cs](../../../../src/Features/Conversations/Infrastructure/ConversationPersistenceService.cs) L112, L77–86 |
| Provider dispose flushes | `ConversationPersistenceService.Dispose` saves if scheduled | L88–106 |
| Production DI test | `ProgramConfigureServices_ResolvesTownhallServicesAsSingletons` mutates `DraftText` with marker `m6g-townhall-di-singleton-sync` on production paths | [TownhallRegistrationModuleTests.cs](../../../../tests/Zaide.Tests/App/Composition/TownhallRegistrationModuleTests.cs) L27–33, L79–99 |
| App shutdown | Disposes `TownhallViewModel` but **not** `ConversationPersistenceService` explicitly | [ApplicationShutdown.cs](../../../../src/App/Composition/ApplicationShutdown.cs) L77 — persistence relies on provider lifetime at exit |

---

## 5. Production DI and user-entry-point findings

1. **Send is user-reachable** through Townhall: open a direct conversation (People panel) → type in input → send ([TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L255–264).
2. **Production composition is complete** for router, coordinator, session, backends, event projection, and Townhall ViewModel injection.
3. **Agent Panel send is not user-reachable**; Phase 14 M8 removed dedicated panel chrome.
4. **No command-palette or settings path** binds an agent backend in production source.
5. **`AgentBackendBindingPresenter` is a dead presentation seam** from the UI's perspective — registered but never consumed by a View.

---

## 6. Response and failure-projection findings

### When a backend is bound and session admits the run

The happy path and admitted-run terminal failures are wired:

1. `UserMessageAdmitted` → conversation `UserChat` entry → Townhall `Messages` update.
2. `AssistantMessageCompleted` → `AssistantResponse` entry → UI update.
3. Admitted-run `ExecutionFailure` / timeout / disconnect / indeterminate → `ExecutionFailure` entry with reason.

### Default production path (unbound)

1. `AgentExecutionCoordinator.SendAsync` returns `Rejected` before `IAgentSessionService.SendAsync` ([AgentExecutionCoordinator.cs](../../../../src/Features/Agents/Application/AgentExecutionCoordinator.cs) L112–124).
2. No session events → `AgentConversationEventProjection` has nothing to project.
3. `AgentRouter` returns `RouteResult` with `Success = true` and non-null `ExecutionResult` ([AgentRouter.cs](../../../../src/Features/Agents/Application/AgentRouter.cs) L87–92).
4. `TownhallViewModel` clears the draft because `routeResult.Success || routeResult.ExecutionResult is not null` ([TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L477–486).
5. **No chat entry, no failure bubble** — confirms ISSUE-008 Attempt 1 hypothesis at source level.

### Session rejections (`RunRejected`)

`AgentConversationEventProjection` explicitly does not project `RunRejected` (L131–133) and `ProjectTerminalFailureEntry` skips runs not in `_admittedRunIds` (L379–385). Session rejections (e.g. concurrent run, identity mismatch) are likewise invisible in chat.

---

## 7. Backend binding / configuration findings

| Claim (A1-AC-02) | Production state |
|------------------|------------------|
| User can configure Native Harness or ACP binding | **Not wired to UI** |
| Binding persisted in settings | **No** — `SettingsModel` has no backend section |
| In-memory binding infrastructure | **Yes** — store + selection service + presenter |
| Status visible in Townhall | **Yes** — read-only `AgentBackendBindingPanel` shows "Unbound" |
| Multiple actors supported in store | **Yes** — keyed by `ActorId` ([AgentActorBackendBindingStore.cs](../../../../src/Features/Agents/Application/AgentActorBackendBindingStore.cs) L14) |

Aligns with [DF-008](../../../deferred/open/DF-008-multiple-agent-connections.md) and [DF-009](../../../deferred/open/DF-009-real-acp-integrations.md).

---

## 8. ISSUE-008 and ISSUE-009 disposition

| Issue | Disposition | Evidence basis |
|-------|-------------|----------------|
| [ISSUE-008](../../../issues/open/ISSUE-008-agent-response-not-showing.md) | **Partially confirmed** (source); runtime sole-cause not proven | Unbound default + silent rejection path traced (§6). ISSUE-008 correctly notes runtime confirmation still pending. Even with binding, external backend smoke was not executed in this audit. |
| [ISSUE-009](../../../issues/closed/ISSUE-009-production-di-test-contaminates-conversation-store.md) | **Confirmed** (source) | Test uses `Program.ConfigureServices` + production `ConversationPersistenceService` paths; sets `DraftText` marker; provider dispose flushes via `ConversationPersistenceService.Dispose` (L88–106). Marker string at [TownhallRegistrationModuleTests.cs](../../../../tests/Zaide.Tests/App/Composition/TownhallRegistrationModuleTests.cs) L97–99. |

---

## 9. DF-008 and DF-009 disposition

| Deferred | Disposition | Evidence basis |
|----------|-------------|----------------|
| [DF-008](../../../deferred/open/DF-008-multiple-agent-connections.md) | **Confirmed** | Store supports multiple actors; no production UI workflow for configure/connect/persist ([§7](#7-backend-binding--configuration-findings)). |
| [DF-009](../../../deferred/open/DF-009-real-acp-integrations.md) | **Confirmed** | ACP stack registered; no UI calls `BindAcpRuntime`; no persisted ACP settings; external candidate smoke not executed ([§4.6](#46-native-harness-or-acp-backend-dispatch), [§7](#7-backend-binding--configuration-findings)). |

---

## 10. Relevant `A1-XX-01` and `A1-XX-03` findings

### `A1-XX-01` (backend-binding user entry point undefined in docs)

| Aspect | Finding |
|--------|---------|
| Disposition | **Confirmed** — production exposes status display only; no supported bind/configure workflow |
| Partial infrastructure | `AgentActorBackendBindingStore`, `AgentActorBackendSelectionService`, `AgentBackendBindingPresenter` all registered |
| User entry point | **Absent** |

### `A1-XX-03` (trace / memory / usage surfaces vs production backend data)

| Aspect | Finding (partial — send slice only) |
|--------|-------------------------------------|
| Disposition | **Still ambiguous** for full row; **partially confirmed absent on send path** |
| Send-path relevance | Phase 21 inspectors/view-models are registered ([AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L121–181) but require admitted backend runs to produce data |
| Default send | Cannot produce trace/memory/usage evidence without binding + successful backend execution |
| Full `A1-XX-03` verdict | Deferred to a dedicated trace/memory/usage A2 slice |

---

## 11. Supporting `A1-TH-*` and `A1-AC-*` partial coverage

| id | Partial finding | Full-row verdict deferred |
|----|-----------------|---------------------------|
| `A1-TH-01` | Channel send + filter wiring present in Townhall; not exhaustively audited | Yes |
| `A1-TH-02` | Direct conversation model + projection on `EntryAppended` wired | Yes |
| `A1-TH-04` | Agent Panel absent from shell; Townhall is sole agent conversation surface | Yes |
| `A1-TH-05` | `@mention` routing via `AgentRouter` + catalog; routing failures projected | Yes |
| `A1-AC-01` | Historical Phase 5 panel path **Missing** (panel retired) | Yes |
| `A1-AC-02` | Binding infrastructure **Wired**; user configuration workflow **Missing** | Yes |

---

## 12. A3 clean-profile smoke constraints (send slice)

A3 for this slice must distinguish **positive-path** scenarios (response or admitted-run
failure feedback) from **negative-path** scenarios (default unbound rejection) and from
**profile isolation** requirements.

### Positive-path smoke (blocked on clean profile)

1. **No production UI to bind a backend** — A3 scenarios that require a successful
   assistant response, or admitted-run terminal outcomes (failed / cancelled / timed-out /
   disconnected / indeterminate), cannot succeed on a clean disposable profile through
   user-visible actions alone. Without corrective binding UI or an explicitly authorized
   A3-only bind hook, these scenarios remain blocked for `A1-AS-02` success-path
   verification.

### Negative-path smoke (executable; failure is valid evidence)

2. **Unbound rejection on default clean profile** — sending in a direct conversation on a
   profile with no backend binding is an executable A3 scenario. The expected observable
   on the current production build is: draft clears, no chat entry, no actionable failure
   bubble (ISSUE-008 hypothesis). Recording that mismatch is valid A3 evidence; it does
   not require pre-binding a backend.

### Profile isolation (disposable `XDG_CONFIG_HOME`; full composition allowed)

3. **Disposable absolute config root before provider creation** — A3 may use full
   `Program.ConfigureServices` production composition. Set an absolute disposable
   `XDG_CONFIG_HOME` (and ensure no real user config is read) **before** constructing the
   service provider so `ConversationPersistenceService` and settings resolve under the
   disposable tree only. A3 is **not** required to replace `ConversationPersistenceService`
   or avoid production composition; isolation is achieved by the disposable config root,
   not by swapping persistence implementations.

4. **ISSUE-009 lesson** — tests that call `Program.ConfigureServices` without a disposable
   `XDG_CONFIG_HOME` can contaminate the real user store ([ISSUE-009](../../../issues/closed/ISSUE-009-production-di-test-contaminates-conversation-store.md)).
   A3 must set the disposable root first; that constraint does not forbid full production
   DI.

### Other open evidence gaps (not A3 blockers for negative path)

5. **External backend smoke not executed** — Native Harness / ACP real-process
   verification remains open per V3 closeout and
   [GOAL_MATRIX.md §17.4 item 7](../GOAL_MATRIX.md#174-unresolved-documentation-ambiguities-a1--a4-gap-report).
   This limits positive-path A3 until binding UI or an authorized hook exists.

6. **`ConversationPersistenceService` not in `ApplicationShutdown` sequence** — normal app
   exit may not flush drafts unless provider disposal occurs; A3 scenarios should document
   flush timing when draft persistence is part of the observation.

---

## 13. Exact next recommended A2 slice (not started)

**Recommended slice name:** `A2_MULTI_AGENT_ROUTING`

**Rationale:** `A2_AGENT_SEND` traced direct send and touched `@mention` routing only peripherally. `A2_MULTI_AGENT_ROUTING` would assign full verdicts to `A1-MR-01` and `A1-MR-03`, complete the routing-failure vs execution-failure projection analysis started here, and resolve [GOAL_MATRIX.md §17.4 item 6](../GOAL_MATRIX.md#174-unresolved-documentation-ambiguities-a1--a4-gap-report) (Phase 6 panel-bound vs Phase 14 catalog routing evolution) without overlapping the persistence/restart journey (`A1-TC-04` / `A1-TC-05`), which merits its own slice after routing is closed.

**Evidence file (planned):** `evidence/A2_MULTI_AGENT_ROUTING.md`

---

## 14. Corroborating test notes (non-proof)

- [TownhallDirectSendTests.cs](../../../../tests/Zaide.Tests/Features/Townhall/Presentation/TownhallDirectSendTests.cs) uses test doubles (`CreateCoordinatorFromHandler`, fake backends), not production `Program.ConfigureServices` binding store — passing tests do not prove production reachability.
- Phase 19/20 integration tests bind backends in test setup; same limitation.

---

*A2_AGENT_SEND complete. Read-only audit; no fixes, A3 work, commits, or pushes.*
