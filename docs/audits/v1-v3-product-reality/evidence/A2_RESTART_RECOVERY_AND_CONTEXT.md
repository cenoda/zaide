# A2 Wiring Audit — `A2_RESTART_RECOVERY_AND_CONTEXT`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_RESTART_RECOVERY_AND_CONTEXT` (fourth A2 slice; prior:
`A2_AGENT_SEND`, `A2_MULTI_AGENT_ROUTING`,
`A2_TRACE_MEMORY_USAGE_TERMINATION`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`5010b3acf9714733ceb46a8fc0586c44436bd8b8` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Item | Value |
|------|-------|
| Audit | `v1-v3-product-reality` (see [AUDIT_PLAN.md](../AUDIT_PLAN.md)) |
| Slice name | `A2_RESTART_RECOVERY_AND_CONTEXT` |
| Prior A2 slices | [A2_AGENT_SEND.md](./A2_AGENT_SEND.md), [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md), [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./A2_TRACE_MEMORY_USAGE_TERMINATION.md) |
| Goal rows to verdict | `A1-TC-01`, `A1-TC-04`, `A1-TC-05` (per [GOAL_MATRIX.md §14](../GOAL_MATRIX.md#14-trace-context-memory-persistence-restart-and-recovery)) |
| Scoped disposition row | `A1-XX-05` (per [GOAL_MATRIX.md §15](../GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior)) — **not** a user-goal verdict |
| Phase 14 sources | [IMPLEMENTATION_PLAN.md](../../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md) (D11–D15, persistence/recovery contract, M6 closeout), [M9 evidence](../../../phases/v3/phase-14/M9_MANUAL_EVIDENCE.md), [M6 evidence](../../../phases/v3/phase-14/M6_MANUAL_EVIDENCE.md) |
| Phase 18 sources | [IMPLEMENTATION_PLAN.md](../../../phases/v3/phase-18/IMPLEMENTATION_PLAN.md), [TOFIX.md](../../../phases/v3/phase-18/TOFIX.md) |
| Phase 21 sources | [IMPLEMENTATION_PLAN.md](../../../phases/v3/phase-21/IMPLEMENTATION_PLAN.md) (restart behavior, M4), [M4 evidence](../../../phases/v3/phase-21/M4_RESTART_AND_TERMINATION_EVIDENCE.md), [M7 closeout](../../../phases/v3/phase-21/M7_CLOSEOUT_EVIDENCE.md) |
| Verdict categories | `Wired`, `Wired-with-gap`, `Missing`, `Ambiguous` (per [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition)) |
| Method constraint | Inspection only; no production-code edits, no test edits, no app launch, no build, no test execution, no A3 smoke, no real user profile, no commit or push |

### Baseline and safety confirmation

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `5010b3acf9714733ceb46a8fc0586c44436bd8b8` |
| `git rev-parse origin/master` | `5010b3acf9714733ceb46a8fc0586c44436bd8b8` |
| Working tree at start | Clean (`## master...origin/master`) |
| Production code modified | No |
| Tests modified | No |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` modified | No |
| Issue / deferred-finding files modified | No |
| Real user profile read/written | No |
| App launched | No |
| Build or tests run | No |
| External backend / A3 smoke | No |

---

## 2. Sources inspected

### 2.1 Documentation

- [AGENTS.md](../../../../AGENTS.md), [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md), [GOAL_MATRIX.md](../GOAL_MATRIX.md), [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- Prior A2: [A2_AGENT_SEND.md](./A2_AGENT_SEND.md), [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md), [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./A2_TRACE_MEMORY_USAGE_TERMINATION.md)
- Phase 14 / 18 / 21 plans and closeout evidence listed in §1

### 2.2 Production source (minimum named targets)

**Live IDE context policy and assembly**

- [TownhallContextPolicySelector.cs](../../../../src/Features/Townhall/Presentation/TownhallContextPolicySelector.cs)
- [TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs), [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs)
- [TownhallNavigationPanel.cs](../../../../src/Features/Townhall/Presentation/TownhallNavigationPanel.cs), [TownhallNavigationItem.cs](../../../../src/Features/Townhall/Presentation/TownhallNavigationItem.cs)
- [IAgentContextSessionPolicyService.cs](../../../../src/Features/Agents/Contracts/IAgentContextSessionPolicyService.cs), [AgentContextSessionPolicyState.cs](../../../../src/Features/Agents/Contracts/AgentContextSessionPolicyState.cs), [AgentSessionContextPolicyLevel.cs](../../../../src/Features/Agents/Contracts/AgentSessionContextPolicyLevel.cs)
- [AgentSessionService.cs](../../../../src/Features/Agents/Application/AgentSessionService.cs) (policy overrides, `AssembleContextManifestLocked`, `EmitContextDisclosedLocked`)
- [AgentContextApplicationDefault.cs](../../../../src/Features/Agents/Domain/AgentContextApplicationDefault.cs), [AgentContextPolicy.cs](../../../../src/Features/Agents/Domain/AgentContextPolicy.cs), [AgentContextSourcePolicyMatrix.cs](../../../../src/Features/Agents/Domain/AgentContextSourcePolicyMatrix.cs)
- [AgentContextManifestBuilder.cs](../../../../src/Features/Agents/Application/AgentContextManifestBuilder.cs), [AgentContextSnapshotSources.cs](../../../../src/Features/Agents/Application/AgentContextSnapshotSources.cs)
- [AgentContextBudgetEnforcer.cs](../../../../src/Features/Agents/Application/AgentContextBudgetEnforcer.cs), [AgentContextRedactionProcessor.cs](../../../../src/Features/Agents/Application/AgentContextRedactionProcessor.cs), [AgentContextContentComposer.cs](../../../../src/Features/Agents/Application/AgentContextContentComposer.cs)
- [AgentContextHardExclusionRegistry.cs](../../../../src/Features/Agents/Domain/AgentContextHardExclusionRegistry.cs), [AgentContextDisclosurePayload.cs](../../../../src/Features/Agents/Domain/AgentContextDisclosurePayload.cs)
- [AgentExecutionCoordinator.cs](../../../../src/Features/Agents/Application/AgentExecutionCoordinator.cs) (disclosure status projection)
- [NativeHarnessSystemPromptBuilder.cs](../../../../src/Features/Agents/Application/NativeHarnessSystemPromptBuilder.cs), [AcpContextManifestEncoder.cs](../../../../src/Features/Agents/Application/Acp/AcpContextManifestEncoder.cs)
- [SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs) (absence of context-policy settings)

**Conversation persistence and restart restore**

- [ConversationPersistenceService.cs](../../../../src/Features/Conversations/Infrastructure/ConversationPersistenceService.cs)
- [ConversationStorePathResolver.cs](../../../../src/Features/Conversations/Infrastructure/ConversationStorePathResolver.cs)
- [ConversationWorkspaceSnapshot.cs](../../../../src/Features/Conversations/Infrastructure/ConversationWorkspaceSnapshot.cs), [PersistedConversationSnapshot.cs](../../../../src/Features/Conversations/Infrastructure/PersistedConversationSnapshot.cs), [PersistedConversationEntrySnapshot.cs](../../../../src/Features/Conversations/Infrastructure/PersistedConversationEntrySnapshot.cs)
- [ConversationSnapshotSerializer.cs](../../../../src/Features/Conversations/Infrastructure/ConversationSnapshotSerializer.cs)
- [ConversationStore.cs](../../../../src/Features/Conversations/Application/ConversationStore.cs)
- [TownhallConversationPersistenceBridge.cs](../../../../src/Features/Townhall/Presentation/TownhallConversationPersistenceBridge.cs)
- [TownhallConversationUiState.cs](../../../../src/Features/Townhall/Presentation/TownhallConversationUiState.cs)
- DI: [TownhallServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs), [ConversationsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/ConversationsServiceCollectionExtensions.cs)
- [ApplicationShutdown.cs](../../../../src/App/Composition/ApplicationShutdown.cs)

**Interrupted-run classification / no silent resume**

- [AgentSessionContinuityStartupReconciler.cs](../../../../src/Features/Agents/Application/Continuity/AgentSessionContinuityStartupReconciler.cs)
- [AgentSessionContinuityCoordinator.cs](../../../../src/Features/Agents/Application/Continuity/AgentSessionContinuityCoordinator.cs)
- [AgentSessionContinuityEventSubscriber.cs](../../../../src/Features/Agents/Application/Continuity/AgentSessionContinuityEventSubscriber.cs)
- [AgentSessionContinuityRevalidator.cs](../../../../src/Features/Agents/Application/Continuity/AgentSessionContinuityRevalidator.cs)
- [AgentSessionContinuityCheckpointWriter.cs](../../../../src/Features/Agents/Application/Continuity/AgentSessionContinuityCheckpointWriter.cs)
- [AgentSessionContinuityInspector.cs](../../../../src/Features/Agents/Application/Continuity/AgentSessionContinuityInspector.cs)
- [NativeHarnessAgentContinuityAdapter.cs](../../../../src/Features/Agents/Application/Continuity/NativeHarnessAgentContinuityAdapter.cs), [AcpAgentContinuityAdapter.cs](../../../../src/Features/Agents/Application/Continuity/AcpAgentContinuityAdapter.cs)
- [AgentSessionContinuityInspectionViewModel.cs](../../../../src/Features/Agents/Presentation/Transparency/AgentSessionContinuityInspectionViewModel.cs)
- [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs) (startup reconcile)
- [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs)
- [AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs) (absence of continuity projection)

### 2.3 Tests (corroboration only; not proof of user wiring)

- Context: `tests/Zaide.Tests/Features/Agents/Domain/Phase18Context*.cs`, `Phase19ContextConsumptionTests.cs`, `Phase20ContextTests.cs`, `Phase18ContextBypassRatchetTests.cs`
- Persistence: [ConversationPersistenceTests.cs](../../../../tests/Zaide.Tests/Features/Conversations/Infrastructure/ConversationPersistenceTests.cs)
- Continuity: `tests/Zaide.Tests/Features/Agents/Continuity/*`
- Townhall DI: [TownhallRegistrationModuleTests.cs](../../../../tests/Zaide.Tests/App/Composition/TownhallRegistrationModuleTests.cs)

---

## 3. Three-row verdict table

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-TC-01` | **Wired-with-gap** | Live IDE context policy is user-reachable on the **Townhall direct-conversation** selector (Off / Minimal / Standard / Detailed + clear override). Application default is hardcoded `Standard`. Session overrides feed `AgentSessionService` assembly; Off produces a valid zero-item/zero-token manifest (automatic IDE source content excluded; Native Harness and ACP can still receive policy metadata such as `IDE context policy: Off`). Assembled manifests reach Native Harness and ACP request builders on admitted runs; disclosure summary projects to the People/nav caption. **Gaps:** documented goal entry “configure in settings” does not exist; application default is not user-configurable; overrides are in-memory only (lost on restart); exclusion/truncation/redaction details are not a full user-inspectable surface; admitted-run context depends on a bound backend ([A2_AGENT_SEND](./A2_AGENT_SEND.md)). |
| `A1-TC-04` | **Wired-with-gap** | Production DI constructs `ConversationPersistenceService` at Townhall startup; schema-v1 JSON loads into `ConversationStore` and Townhall presentation maps (channels, conversations/entries, active selection, drafts, last-read/unread, direct participant pairs). Debounced atomic write + last-known-good exist. Snapshot schema **does not** hold sessions, runs, backend bindings, capabilities, events, audit, usage/cost, traces, or lifecycle memory. **Gaps:** save/load failures are silent (no user-visible recovery status despite Phase 14 M0 “user-visible status when recovery fails”); Zaide’s explicit owned shutdown sequence does not dispose or flush `ConversationPersistenceService` (a pending debounced save has no source-proven Zaide-owned exit flush; timer completion before process exit may save it; whether the external ReactiveUI/DI bootstrap later disposes the root provider is not established by Zaide source inspected in this A2 slice); corrupt/unsupported outcomes are not surfaced in UI. |
| `A1-TC-05` | **Wired-with-gap** | Production startup calls `AgentSessionContinuityStartupReconciler.ReconcileOnStartupIfNeeded()` which **classifies only** — it does **not** call `Resume`. Live-run or graceful-shutdown checkpoints may be written with `Recoverable` for Running/Accepted; the event subscriber records lifecycle checkpoints for admitted runs. On a normal cold start the in-memory singleton `AgentActorBackendBindingStore` is empty and is not persisted; `ClassifyCheckpoint` returns `Indeterminate` immediately when the actor binding is missing, before the Running/Accepted → Recoverable branch. No production mechanism restores a matching binding before startup reconcile (which runs before any user interaction). Reconciled classification remains invisible to Townhall. Success is not inferred; no production path auto-re-invokes backend execution after restart. User must start a new send. **Gaps:** classification lives in the Phase 21 durable continuity store and **is not projected** into the conversation or Townhall; default production cold-start reconciliation of a persisted interrupted Running/Accepted checkpoint is source-proven `Indeterminate` (distinct from any prior stored `Recoverable` classification), not Recoverable; continuity Resume/Terminate ViewModels remain unbound (same dead-seam pattern as [A2_TRACE_MEMORY_USAGE_TERMINATION](./A2_TRACE_MEMORY_USAGE_TERMINATION.md)); force-kill without graceful shutdown may leave no durable checkpoint. |

Verdict definitions: [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

---

## 4. Optional scoped disposition — `A1-XX-05`

**Label:** scoped disposition only — **not** a user-goal verdict and **not** one of `Wired` / `Wired-with-gap` / `Missing` / `Ambiguous`.

| Constraint claim (documents) | Production observation |
|------------------------------|------------------------|
| Conversation store is application-lifetime (Phase 14 D15) | **Observed.** `IConversationStore` / `ConversationStore` registered as singleton ([ConversationsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/ConversationsServiceCollectionExtensions.cs) L14). |
| Store path is user-config, not workspace-root scoped | **Observed.** `{settings-dir}/conversations/conversations.json` via [ConversationStorePathResolver.cs](../../../../src/Features/Conversations/Infrastructure/ConversationStorePathResolver.cs) L14–19; M6 evidence records application-lifetime isolation. Independent of process CWD and of the folder opened in Zaide. |
| No multi-window / multi-workspace realtime sync | **Observed as constrained / absent.** No multi-window sync path under conversation persistence. Opening different folders does not switch conversation files. |
| Two persistence models | **Conversation persistence:** application/user-config scoped — one process-wide store file independent of both the opened Zaide workspace/folder and process CWD. **Phase 21 durable records** (continuity/trace/usage/memory): path-derived keys via `AgentDurableWorkspaceStorageKeyResolver`. Production composition does **not** inject an opened-workspace-root provider into `AgentSessionService` or `AgentSessionContinuityStartupReconciler`; both fall back to `Environment.CurrentDirectory`. In current production, Phase 21 partitions are therefore **process-CWD-keyed**, not proven to track the workspace/folder opened in Zaide. Opening another folder inside the same process does not itself prove that the durable partition changes. Do **not** call the Phase 21 model genuinely workspace-scoped without this qualification. Startup reconcile and shutdown checkpoint also use `Environment.CurrentDirectory`, so locating the intended partition depends on stable process CWD. |
| Deliberate vs undocumented | **Deliberate** for conversation store (Phase 14 M6 closeout and D15). Multi-window coordination remains explicitly not solved (Phase 21 baseline language). Process-CWD keying for Phase 21 durable records is the current production composition reality, not a proven opened-workspace binding. |

No user-observable acceptance criteria are invented for this row.

---

## 5. End-to-end wiring maps

Legend: **T** = type/contract · **R** = production DI · **C** = production caller · **U** = user-reachable · **P** = user-visible result/failure · **A3** = runtime behavior unproven without A3.

### 5.1 `A1-TC-01` — live IDE context policy and attachment

```text
[user]
  Open direct conversation in Townhall
  → TownhallContextPolicySelector (combo Off/Minimal/Standard/Detailed)
  → TownhallViewModel.SetContextPolicyFromSelectorCommand
  → IAgentContextSessionPolicyService.TrySetSessionOverride / ClearSessionOverride
  → AgentSessionService in-memory _sessionPolicyOverrides

[admitted send — same production path as A2_AGENT_SEND]
  Townhall send → router/coordinator → AgentSessionService.SendAsync
  → AssembleContextManifestLocked (policy + LiveAgentContextSnapshotSources
     + redaction + budget + hard exclusions + optional memory retrieval)
  → AgentBackendRequest.ContextManifest
  → NativeHarnessSystemPromptBuilder / AcpContextManifestEncoder
  → ContextDisclosed event
  → AgentExecutionCoordinator → AgentPanelState.ContextDisclosureStatus
  → TownhallNavigationItem caption ("Context: Off" / "N sources, T tokens")
```

| Layer | Status | Evidence |
|-------|--------|----------|
| 1. Type / contract | Present | Four levels; session policy service; policy matrix; manifest types |
| 2. DI registration | Present | [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L37–39; session policy resolves to `IAgentSessionService` via [Program.cs](../../../../src/App/Composition/Program.cs) L55–57 |
| 3. Production caller | Present | Townhall selector commands ([TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L428–429, L1144–1177); assembly at send ([AgentSessionService.cs](../../../../src/Features/Agents/Application/AgentSessionService.cs) L599–624, L1272–1310) |
| 4. Production effect | Present for admitted runs | Manifest attached to backend request; backends consume it ([NativeHarnessSystemPromptBuilder.cs](../../../../src/Features/Agents/Application/NativeHarnessSystemPromptBuilder.cs) L13–65; [AcpContextManifestEncoder.cs](../../../../src/Features/Agents/Application/Acp/AcpContextManifestEncoder.cs) L15–42) |
| 5. User can reach/configure | **Partial** | Session selector **yes** on direct conversations ([TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L61–65, L336–370). **Settings entry point: no** ([SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs) L24–29: Editor, Llm, Keybindings, Debug only) |
| 6. Result visible | **Partial** | Policy caption + override button on selector; disclosure status on nav item ([TownhallNavigationPanel.cs](../../../../src/Features/Townhall/Presentation/TownhallNavigationPanel.cs) L203–233). Full exclusion/truncation/redaction inventories are not a dedicated UI. Context assembly failure emits indeterminate failure (may project as `ExecutionFailure` if run terminal events fire) |
| 7. Runtime unproven without A3 | Yes | Whether live snapshots (editor/git/diagnostics) match real workspace content on a disposable profile; token counts under real budgets; Off end-to-end with a bound backend |

**Off handling (source-proven):**

- `AgentContextSourcePolicyMatrix.IsSourceIncluded` returns `false` for all sources when level is `Off` ([AgentContextSourcePolicyMatrix.cs](../../../../src/Features/Agents/Domain/AgentContextSourcePolicyMatrix.cs) L62–65).
- Manifest builder short-circuits to a **valid zero-item / zero-token** manifest (requested budget 0, empty items) while retaining the applied policy level ([AgentContextManifestBuilder.cs](../../../../src/Features/Agents/Application/AgentContextManifestBuilder.cs) L58–70). Automatic IDE source content is excluded; this is **not** “absence of a manifest” or “absence of all context-related bytes.”
- Disclosure formatter maps empty/Off to `"No context"` / `"Context: Off"` ([AgentExecutionCoordinator.cs](../../../../src/Features/Agents/Application/AgentExecutionCoordinator.cs) L734–749).
- Native Harness and ACP builders still emit policy metadata such as `IDE context policy: Off` ([NativeHarnessSystemPromptBuilder.cs](../../../../src/Features/Agents/Application/NativeHarnessSystemPromptBuilder.cs) L33–34; [AcpContextManifestEncoder.cs](../../../../src/Features/Agents/Application/Acp/AcpContextManifestEncoder.cs) L50); they skip empty/null source-content blocks for automatic injection. The claim is absence of automatic source-content injection, not absence of all context-related bytes.

**Application default vs session override:**

| Rule | Implementation |
|------|----------------|
| Application default | Hardcoded `AgentContextPolicyLevel.Standard` ([AgentContextApplicationDefault.cs](../../../../src/Features/Agents/Domain/AgentContextApplicationDefault.cs) L8) |
| Session override | Per-`ConversationId` dictionary on `AgentSessionService` (L39, L479–511) |
| Precedence | `SessionOverride?.Level ?? ApplicationDefaultLevel` ([AgentContextPolicy.cs](../../../../src/Features/Agents/Domain/AgentContextPolicy.cs) L30–31) |
| Clear override | Selector index 0 or “Use application default” → `ClearSessionOverride` |
| Persistence of override | **None** — not in conversation snapshot or settings |

**Declared context sources (matrix):** build/test failure, debug exception, project context, active file, language/build diagnostics, test results, workflow state, open files, source-control summary, debug session state, editor caret/selection, durable memory. **Not** included as automatic sources: raw terminal scrollback (hard-excluded), secrets/env, full LSP internals, binary content, debug variable/watch trees ([AgentContextHardExclusionRegistry.cs](../../../../src/Features/Agents/Domain/AgentContextHardExclusionRegistry.cs)).

**Attribution correction vs goal matrix wording:** [GOAL_MATRIX.md](../GOAL_MATRIX.md) `A1-TC-01` user entry is “Configure context policy in **settings**.” Phase 18 delivered a **session** selector and locked “no agent/project policy; only application default and session override” ([Phase 18 plan](../../../phases/v3/phase-18/IMPLEMENTATION_PLAN.md)). This audit treats the session selector as the real production entry point and records the settings path as a **documented-entry gap**, not as total absence of policy UI.

### 5.2 `A1-TC-04` — conversation persistence and restart restoration

```text
[startup]
  DI constructs ConversationPersistenceService (eager via TownhallViewModel factory)
  → LoadAndHydrate: conversations.json → LKG on corrupt
  → ConversationStore.RestoreFromPersistence
  → TownhallConversationPersistenceBridge.ApplyRestoredSnapshot
     (channels, drafts, last-read)
  → TownhallViewModel.InitializeFromPersistedSession
     (rebuild channel messages, restore active selection + draft)

[mutation]
  EntryAppended / presentation change
  → RequestSave (250ms debounce)
  → temp file → atomic replace → write LKG

[shutdown — intended design]
  Dispose → flush if save scheduled

[shutdown — Zaide explicit owned sequence (ApplicationShutdown.Run)]
  Disposes TownhallViewModel (does not dispose ConversationPersistenceService)
  Continuity checkpoint (Environment.CurrentDirectory) + AgentSessionService dispose
  → ConversationPersistenceService.Dispose / flush not invoked by Zaide’s owned sequence
  → whether the external ReactiveUI/DI bootstrap later disposes the root provider
     is not established by Zaide source inspected in this A2 slice
```

| Durable field | Schema support | Production save | Production restore | User-reachable |
|---------------|----------------|-----------------|--------------------|----------------|
| Channels | Yes (`channels[]`) | Bridge capture | Bridge apply | Yes |
| Conversations + typed entries | Yes | Store list | `RestoreFromPersistence` | Yes |
| Active selection | Yes (`activeConversationId`) | Bridge | SelectConversation on restore | Yes |
| Drafts | Yes | UI state export | Import + DraftText | Yes |
| Last-read / unread | Yes (`lastReadEntryIds`) | UI state export | Import + `IsUnread` | Yes |
| Direct participant pairs | Yes (`participants` on direct rows) | Serializer | Store pair index rebuild | Yes |
| Live sessions / active runs | **No** | n/a | n/a | Correctly absent |
| Backend bindings | **No** | n/a | n/a | Correctly absent (also in-memory only per prior A2) |
| Capabilities / normalized events | **No** | n/a | n/a | Correctly absent |
| Action audit / usage / cost / traces / lifecycle memory | **No** (Phase 21 separate durable store) | n/a in conversation snapshot | n/a | Correctly absent from conversation file |
| Session context-policy overrides | **No** | n/a | n/a | Lost on restart |

**Atomic write:** temp path → `File.Move(..., overwrite: true)` then LKG write ([ConversationPersistenceService.cs](../../../../src/Features/Conversations/Infrastructure/ConversationPersistenceService.cs) L230–232).

**Corrupt / unsupported:**

| Condition | Behavior | User-visible? |
|-----------|----------|---------------|
| Missing file | Seed session path | No special status |
| Corrupt main | Try LKG | No status caption |
| Unsupported schema version | Disable further writes (`_persistWritesEnabled = false`) | No status caption |
| Save IO failure | Catch and swallow (best-effort) | No |

Phase 14 M0 contract text required “user-visible status when recovery fails” ([Phase 14 plan](../../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md) persistence table). Production `LoadResult` is public on the service but has **no Townhall/UI consumer**.

### 5.3 `A1-TC-05` — interrupted-run classification and no silent resume

```text
[during admitted run]
  AgentSessionContinuityEventSubscriber ← AgentEventStream
  → RecordCheckpoint (Accepted/Running/terminal kinds)
  AgentSessionService also records BeforeRunStart checkpoint
  → a live-run checkpoint may be written with classification Recoverable

[graceful shutdown]
  ApplicationShutdown → CheckpointActiveSessions(Environment.CurrentDirectory)
  → BeforeApplicationShutdown checkpoint may be written as Recoverable / Running
  → Dispose IAgentSessionService (live ownership ends; no auto re-run)
  → AgentActorBackendBindingStore is an in-memory singleton and is not persisted

[next startup — normal production cold start]
  App.axaml.cs → ReconcileOnStartupIfNeeded  (before any user interaction)
  → workspace root provider falls back to Environment.CurrentDirectory
  → coordinator.Reconcile → ClassifyCheckpoint
  → binding store is empty on cold start
  → missing actor binding → Indeterminate immediately
     (before the Running/Accepted → Recoverable branch)
  → write AfterStartupReconcile with reclassified result
  → does NOT call Resume
  → does NOT write conversation ExecutionFailure for interrupted runs
  → classification is not projected to Townhall

[explicit resume API — not user-reachable]
  AgentSessionContinuityInspectionViewModel.Resume
  → coordinator.Resume
  → records Ready checkpoint + remembers session id for next GetOrCreateSession
  → does not re-invoke in-flight tool work (M4 evidence limitation)
```

| Promise element | Production wiring | User-visible? |
|-----------------|-------------------|---------------|
| Classify interrupted state | Yes — `AgentSessionContinuityRevalidator.ClassifyCheckpoint` | **No UI** |
| Never infer success | Yes — no path marks Completed without backend completion events | n/a |
| Never silently resume side effects | Yes — startup only `Reconcile`; no production caller of `Resume` outside unbound ViewModel | Silent-by-absence of resume |
| Terminal vs interrupted classification | **Distinguish stored vs reconciled:** live-run/graceful-shutdown checkpoints for Running/Accepted may be **written** as `Recoverable`. On normal cold start, revalidation returns **`Indeterminate`** when the actor binding is missing (default: empty in-memory store; no production binding restoration before reconcile). The Running/Accepted → Recoverable branch is only reached when a matching binding is already present. Conversation entries are **not** rewritten on reconcile | Conversation may still show last admitted user message without terminal failure line; reconciled classification is invisible |
| User must re-send | Yes — live sessions not restored; bindings not restored; send is a new user action | User is not told “interrupted — re-send” |

**Difference: conversation snapshot restore vs session/run restore**

| Concern | Restored on restart? |
|---------|----------------------|
| Conversation history / drafts / unread | Yes (A1-TC-04 path; application/user-config scoped) |
| Live `AgentSessionService` sessions | No — in-memory only; cleared on dispose |
| Active runs / busy / IsBusy | No |
| Continuity checkpoints | Yes — Phase 21 durable `SessionRecovery` records (path-derived keys; production partition is process-CWD-keyed via `Environment.CurrentDirectory`, not proven opened-workspace tracking) |
| Continuity classification on cold start | Revalidated on reconcile: default production cold start → **`Indeterminate`** when binding missing; prior stored `Recoverable` is not the cold-start result. **Not** projected to Townhall |
| Backend binding | No — `AgentActorBackendBindingStore` is an in-memory singleton; empty at cold start; not persisted; no production restore path before startup reconcile |

---

## 6. DI registration and production-caller analysis

| Service / type | R | C | U | P | Notes |
|----------------|---|---|---|---|-------|
| `AgentContextManifestBuilder` / `IAgentContextSnapshotSources` | ✓ | ✓ on admitted send | via send | partial disclosure caption | L37–38 |
| `IAgentContextSessionPolicyService` → `AgentSessionService` | ✓ | ✓ Townhall | ✓ direct conv | policy caption | L39; Program resolve |
| `IConversationStore` singleton | ✓ | ✓ | ✓ | ✓ | Conversations module |
| `ConversationPersistenceService` | ✓ | ✓ load + save | restore yes | restore yes; errors no | Townhall module L21–27; constructed eagerly for TownhallViewModel |
| `IConversationWorkspacePersistenceBridge` | ✓ | ✓ | ✓ | ✓ | L20 |
| Continuity checkpoint writer / inspector / revalidator / coordinator | ✓ | ✓ | no continuity UI | no | L151–158 |
| `AgentSessionContinuityStartupReconciler` | ✓ | ✓ App startup | n/a | no UI | App.axaml.cs L93–94 |
| `AgentSessionContinuityEventSubscriber` | ✓ | ✓ App startup resolve | n/a | no UI | App.axaml.cs L95 |
| `AgentSessionContinuityInspectionViewModel` / management VM | ✓ | **no shell caller** | **no** | **no** | Same dead-seam as prior A2 slice |
| `Resume` / `Terminate` continuity APIs | ✓ | only inspection VM / session service wrappers | **no** | **no** | Startup never Resume |

---

## 7. Persistence schema and exclusion analysis

### 7.1 Conversation workspace schema v1

[ConversationWorkspaceSnapshot.cs](../../../../src/Features/Conversations/Infrastructure/ConversationWorkspaceSnapshot.cs):

- `schemaVersion` (current = 1)
- `channels[]` (`id`, `name`, `pinned`)
- `conversations[]` (`id`, `kind`, `participants[]`, `entries[]`)
- `activeConversationId`
- `drafts` map
- `lastReadEntryIds` map

Entry row: id, kind, author, timestamp, content, correlationId — **no** run/session/backend fields.

### 7.2 Explicit non-persistence (conversation snapshot)

Confirmed by schema absence (and Phase 14/21 ownership tables):

| Excluded concern | Owner elsewhere if any |
|------------------|------------------------|
| Live sessions / active runs | `AgentSessionService` (memory only) |
| Backend bindings | `AgentActorBackendBindingStore` (memory only) |
| Capabilities / normalized events | Event stream / backends (ephemeral) |
| Action audit | Phase 17 audit store (separate) |
| Usage / cost / traces / lifecycle memory | Phase 21 durable store (path-derived keys; production process-CWD-keyed via `Environment.CurrentDirectory`), **not** conversation JSON |
| Context policy overrides | Memory only on session service |
| Filter mode / scroll position | Session-local UI |

---

## 8. Source-proven versus runtime-unproven findings

### Source-proven (this A2 slice)

1. Townhall session context-policy selector is bound and drives `IAgentContextSessionPolicyService`.
2. Application default is `Standard`; Off produces a valid zero-item/zero-token manifest that excludes automatic IDE source content while still allowing policy metadata (e.g. `IDE context policy: Off`) on Native Harness / ACP builders.
3. Admitted-run path assembles manifests and attaches them to backend requests.
4. Conversation snapshot load/restore/save path is production-composed (application/user-config scoped).
5. Conversation snapshot excludes sessions/runs/bindings/events/audit/usage/trace/memory.
6. Startup continuity reconcile runs and does not call Resume (no automatic backend re-invocation).
7. Graceful shutdown / live-run checkpoints may write `Recoverable` for Running/Accepted; the binding store is not persisted.
8. On a normal production cold start, the empty in-memory binding store causes `ClassifyCheckpoint` to return `Indeterminate` before the Running/Accepted → Recoverable branch; no production binding restoration exists before startup reconcile.
9. Phase 21 durable keys are path-derived; production `AgentSessionService` and `AgentSessionContinuityStartupReconciler` fall back to `Environment.CurrentDirectory` (process-CWD-keyed, not proven opened-workspace wiring).
10. Zaide’s explicit owned shutdown sequence does not dispose or flush `ConversationPersistenceService`.
11. Continuity inspection/resume UI is registered but unbound.

### Runtime-unproven without A3 (do not promote to runtime proof)

1. Disposable-profile restart actually restores a real draft/unread selection end-to-end.
2. Corrupt-file → LKG recovery under real XDG paths.
3. Live context snapshot content matches open editor/git/diagnostics state.
4. Off truly prevents backend-visible **automatic source-content** injection with a **bound** Native Harness or ACP run (policy metadata line may still be present).
5. Force-quit mid-run leaves a durable checkpoint at all; cold-start reclassification under empty binding is source-proven `Indeterminate` when a checkpoint exists.
6. Whether a pending debounced conversation save is retained on graceful exit: no Zaide-owned exit flush; timer completion before exit may save; eventual container/root-provider disposal is framework/runtime-unproven from Zaide source in this slice.
7. Multi-window / multi-instance store contention (A1-XX-05 related).
8. Whether process CWD matches the user-opened Zaide workspace/folder under real launches; whether changing the opened folder changes the Phase 21 partition without a process restart from a different CWD.

---

## 9. User reachability and visibility findings

| User action / surface | Context policy | Conversation restore | Interrupted-run status |
|-----------------------|----------------|----------------------|------------------------|
| Townhall direct conversation | **Yes** — selector | **Yes** — history/drafts | **No** interrupted banner |
| Townhall channel | Selector hidden | Channel history yes | n/a |
| Settings | **No** context policy | n/a | n/a |
| Command palette | No context/recovery commands found | n/a | n/a |
| Continuity management ViewModel | n/a | n/a | Registered, **unbound** |
| Persistence failure | n/a | **Silent** | n/a |
| After restart mid-run | Overrides reset to default | History may remain (application/user-config store) | Default cold-start reclassification is source-proven **`Indeterminate`** when binding is missing; classification **not** shown in Townhall; user must re-send without prompt |

**Visibility gap for A1-TC-05:** a “no silent resume” contract is **partially** user-wired (startup calls `Reconcile`, not `Resume`; nothing auto-continues side effects), but classification (including cold-start `Indeterminate`) and the required next action are **invisible**. Prior A2 already established that unbound sends also fail without conversation projection ([A2_AGENT_SEND](./A2_AGENT_SEND.md)); recovery messaging is in the same blind spot.

---

## 10. Contradiction / attribution corrections

| Document claim | Live production reading |
|----------------|-------------------------|
| Goal matrix `A1-TC-01` entry “settings” | Production entry is Townhall **session** selector; settings schema has no context policy |
| Phase 14 D12 “in-flight runs become terminal interrupted/cancelled/failed” | Live-run/graceful-shutdown checkpoints may **store** Running/Accepted as **Recoverable** (terminal only for completed/failed/cancelled/timed-out/ended-session patterns when revalidation reaches that branch). On a **normal production cold start**, missing actor binding causes revalidation to return **`Indeterminate`** before the Running/Accepted → Recoverable branch; that is the source-proven default cold-start reconciled result, not Recoverable |
| Phase 14 M0 “user-visible status when recovery fails” | `LoadResult` exists; **no UI projection** |
| Phase 14 M6 “flush on dispose” | Dispose exists on service; Zaide’s explicit owned shutdown sequence (`ApplicationShutdown.Run`) does **not** dispose or flush `ConversationPersistenceService`; TownhallViewModel discards the injected instance with `_ = persistenceService` ([TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L347). Whether the external ReactiveUI/DI bootstrap later disposes the root provider is not established by Zaide source inspected in this A2 slice |
| Phase 21 M4 “Townhall/Agents presentation for interrupted…” (plan intent) | Inspection ViewModels exist; **no production View** binds them (consistent with [A2_TRACE_MEMORY_USAGE_TERMINATION](./A2_TRACE_MEMORY_USAGE_TERMINATION.md)); classification remains invisible to Townhall |
| Phase 21 durable “workspace” keys as opened-folder wiring | Keys are path-derived; production composition uses process CWD (`Environment.CurrentDirectory`) for session service, startup reconcile, and shutdown checkpoint — not a proven opened-workspace-root provider |
| Phase21RestartTests implying default cold-start Recoverable | Tests seed a matching binding and reuse that binding store across the simulated restart; they prove the Recoverable branch **under a matching binding**, not the default production cold-start result (`Indeterminate` when binding is missing) |

No changes were made to issue or deferred-finding files.

---

## 11. Issue / deferred-finding relationships (read-only)

| Artifact | Relationship to this slice |
|----------|----------------------------|
| [ISSUE-009](../../../issues/closed/ISSUE-009-production-di-test-contaminates-conversation-store.md) | Confirms production conversation path under real config dir; DI tests can mutate drafts that flush into production store — isolation concern for A3 disposable profiles |
| [ISSUE-008](../../../issues/open/ISSUE-008-agent-response-not-showing.md) | Unbound/rejection projection gap still blocks end-to-end observation of context attachment on the default cold path |
| [DF-008](../../../deferred/open/DF-008-multiple-agent-connections.md) / [DF-009](../../../deferred/open/DF-009-real-acp-integrations.md) | Backend bind UI absence limits real admitted-run context and continuity checkpoint production |
| Prior A2 send / transparency slices | Continuity ViewModels dead-seam; send path prerequisites reused without re-auditing full agent-send journey |

---

## 12. A3 clean-profile smoke constraints

If A3 later smokes these rows, constraints from this wiring audit:

1. **Disposable isolated profile only** — never real user config, conversation store, or Phase 21 durable partitions ([AUDIT_PLAN.md §3](../AUDIT_PLAN.md#3-safety-and-isolation-rules-mandatory-for-a0a4)).
2. **Backend bind prerequisite** — default production still cannot bind Native Harness/ACP from UI ([A2_AGENT_SEND](./A2_AGENT_SEND.md)). Context-attachment and continuity-checkpoint scenarios need a controlled bind setup or remain limited to policy UI-only checks.
3. **`A1-TC-01` smoke:** open a direct conversation; exercise Off/Minimal/Standard/Detailed + clear override; with a bound backend only, send and observe nav disclosure caption; for Off, treat success as absence of automatic source-content injection while allowing a zero-item manifest and policy metadata; do not treat unit tests as runtime proof of snapshot content.
4. **`A1-TC-04` smoke:** write draft + message; test **immediate graceful exit both before and after the 250ms debounce** on a disposable profile (pending debounced save has no source-proven Zaide-owned exit flush); separately confirm restore after a save that completed; separately corrupt main file with LKG present; **do not** expect a user-visible recovery banner (source says silent). Do not treat eventual DI container disposal as source-proven from Zaide.
5. **`A1-TC-05` smoke:** mid-run force-quit vs graceful exit may differ (checkpoint present/absent). After a **normal cold restart** (empty binding store), expect reconciled classification **`Indeterminate`** when a persisted interrupted Running/Accepted checkpoint is found — do **not** expect default cold-start `Recoverable` unless an authorized test-only mechanism restores a matching binding **before** reconcile. Confirm **no automatic backend re-invocation** (startup calls Reconcile, not Resume); do **not** expect Townhall interrupted classification UI; confirm user must re-send. Distinguish any prior stored `Recoverable` checkpoint classification from the post-reconcile cold-start result.
6. **`A1-XX-05`:** optional observation only — conversation history is application/user-config scoped (shared across folders for one disposable config dir). A CWD-partition test for Phase 21 durable records must **launch separate processes from explicitly controlled working directories**; merely opening two folders inside one process is not sufficient to prove partition change. Not a user-goal smoke.
7. A3 must not treat Phase 14/18/21 unit green as product delivery proof.

---

## 13. Exact next recommended A2 slice, explicitly not started

**Next recommended A2 slice (not begun in this session):**
`A2_TOOLS_PERMISSIONS`

**Suggested goal rows:**

- `A1-TP-01` — mediated action control plane / permission UI
- `A1-TP-02` — permission dimensions and approval scope
- `A1-TP-03` — workspace mutation concurrency / rollback

**Why this next:** it is the highest-risk remaining agent-adjacent user journey after send, routing, transparency, and restart/context. It reuses known coordinator/session seams without reopening closed A2 files. Strong alternatives afterward: first-launch/settings (`A1-FL-*`), Townhall-only rows (`A1-TH-*`), or IDE journeys (editor/LSP/build/debug).

**Explicitly not started here:** that slice, A3, A4, stabilization, V4, corrective implementation, or any other A2 evidence file.

---

## 14. Corroborating tests (non-proof)

| Area | Tests | Prove | Do **not** prove |
|------|-------|-------|------------------|
| Phase 18 context contracts/assembly | `Phase18ContextContractTests`, `Phase18ContextAssemblyTests` | Matrix, Off as zero-item/zero-token path, redaction, budgets | Townhall selector UX or live IDE snapshots; absence of all context-related bytes |
| Context consumption | `Phase19ContextConsumptionTests`, `Phase20ContextTests` | Backend builders honor manifests when tests supply them (including policy metadata) | Bound production send path |
| Conversation persistence | `ConversationPersistenceTests` | Load/LKG/atomic/corrupt matrix in isolation | Zaide-owned ApplicationShutdown flush; eventual container disposal; UI status |
| Continuity | `Phase21RestartTests`, `Phase21RecoveryTests`, `Phase21TerminationTests` | Reconcile without auto-resume; Resume API; **Recoverable branch when tests seed a matching binding and reuse that binding store across the simulated restart** | Default production cold-start result with empty binding store (`Indeterminate`); user-visible recovery surface |
| Architecture ratchets | `Phase18ContextBypassRatchetTests`, `Phase21RecoveryRatchetTests` | Ownership boundaries | Discoverability |

---

## 15. Verification and working-tree closeout

### Pre-closeout checks (to be re-run after writing)

| Check | Expected |
|-------|----------|
| Exactly one new untracked file | `docs/audits/v1-v3-product-reality/evidence/A2_RESTART_RECOVERY_AND_CONTEXT.md` |
| No tracked files modified | Clean aside from that untracked evidence file |
| Whitespace | `git diff --no-index --check /dev/null <evidence-file>` (exit 1 expected for new content; no whitespace error lines) |
| Verdict table IDs exactly once | `A1-TC-01`, `A1-TC-04`, `A1-TC-05` |
| `A1-XX-05` | Scoped disposition only (§4) |
| No later A2 evidence file created | Only this new file under `evidence/` |

### Closeout verdicts (repeat)

| id | verdict |
|----|---------|
| `A1-TC-01` | **Wired-with-gap** |
| `A1-TC-04` | **Wired-with-gap** |
| `A1-TC-05` | **Wired-with-gap** |
| `A1-XX-05` | Scoped disposition only — conversation persistence is application/user-config scoped (not workspace-scoped); no multi-window sync; Phase 21 durable records use separate path-derived keys that are process-CWD-keyed in current production composition, not proven opened-workspace wiring |

**Stop for re-audit.** No next slice started. No commit or push.
