# A2 Wiring Audit — `A2_TOOLS_PERMISSIONS`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_TOOLS_PERMISSIONS` (fifth A2 slice; prior:
`A2_AGENT_SEND`, `A2_MULTI_AGENT_ROUTING`,
`A2_TRACE_MEMORY_USAGE_TERMINATION`,
`A2_RESTART_RECOVERY_AND_CONTEXT`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`944ad1b7cbe29c31fee6c6e96a0543f9a6e35434` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Item | Value |
|------|-------|
| Audit | `v1-v3-product-reality` (see [AUDIT_PLAN.md](../AUDIT_PLAN.md)) |
| Slice name | `A2_TOOLS_PERMISSIONS` |
| Prior A2 slices | [A2_AGENT_SEND.md](./A2_AGENT_SEND.md), [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md), [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./A2_TRACE_MEMORY_USAGE_TERMINATION.md), [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md) |
| Goal rows to verdict | `A1-TP-01`, `A1-TP-02`, `A1-TP-03` (per [GOAL_MATRIX.md §12](../GOAL_MATRIX.md#12-tools-permissions-and-workspace-mutation)) |
| Phase 17 sources | [IMPLEMENTATION_PLAN.md](../../../phases/v3/phase-17/IMPLEMENTATION_PLAN.md), [M9 closeout](../../../phases/v3/phase-17/M9_CLOSEOUT_EVIDENCE.md), M3–M8 milestone evidence |
| Phase 19 / 20 sources | [Phase 19 plan](../../../phases/v3/phase-19/IMPLEMENTATION_PLAN.md), [Phase 20 plan](../../../phases/v3/phase-20/IMPLEMENTATION_PLAN.md) |
| Verdict categories | `Wired`, `Wired-with-gap`, `Missing`, `Ambiguous` (per [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition)) |
| Method constraint | Inspection only; no production-code edits, no test edits, no app launch, no build, no test execution, no A3 smoke, no real user profile, no agent backend mutation/command execution, no commit or push |

### Baseline and safety confirmation

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `944ad1b7cbe29c31fee6c6e96a0543f9a6e35434` |
| `git rev-parse origin/master` | `944ad1b7cbe29c31fee6c6e96a0543f9a6e35434` |
| Working tree at start | Clean (`## master...origin/master`) |
| Production code modified | No |
| Tests modified | No |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` modified | No |
| Issue / deferred-finding files modified | No |
| Real user profile read/written | No |
| App launched | No |
| Build or tests run | No |
| External backend / A3 smoke | No |
| Agent file mutation / command execution | No |

---

## 2. Sources inspected

### 2.1 Documentation

- [AGENTS.md](../../../../AGENTS.md), [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md), [GOAL_MATRIX.md](../GOAL_MATRIX.md), [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- Prior A2 evidence listed in §1
- Phase 17: [IMPLEMENTATION_PLAN.md](../../../phases/v3/phase-17/IMPLEMENTATION_PLAN.md), [M9_CLOSEOUT_EVIDENCE.md](../../../phases/v3/phase-17/M9_CLOSEOUT_EVIDENCE.md), [M3](../../../phases/v3/phase-17/M3_PERMISSION_REVIEW_EVIDENCE.md), [M4](../../../phases/v3/phase-17/M4_PROPOSAL_PREVIEW_EVIDENCE.md), [M5](../../../phases/v3/phase-17/M5_WORKSPACE_MUTATION_EVIDENCE.md), [M6](../../../phases/v3/phase-17/M6_DOCUMENT_RECONCILIATION_EVIDENCE.md), [M7](../../../phases/v3/phase-17/M7_COMMAND_EXECUTION_EVIDENCE.md), [M8](../../../phases/v3/phase-17/M8_SESSION_EVENT_INTEGRATION_EVIDENCE.md)
- Phase 19 / 20 plans for Native Harness and ACP tool/action consumption
- Related deferred findings: [DF-008](../../../deferred/open/DF-008-multiple-agent-connections.md), [DF-009](../../../deferred/open/DF-009-real-acp-integrations.md)
- Related issues: [ISSUE-008](../../../issues/open/ISSUE-008-agent-response-not-showing.md), [ISSUE-009](../../../issues/closed/ISSUE-009-production-di-test-contaminates-conversation-store.md)

### 2.2 Production source (minimum named targets)

**Composition / shell**

- [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs) (permission-dialog owner attach)
- [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs)
- [WorkspaceServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/WorkspaceServiceCollectionExtensions.cs)
- [EditorServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/EditorServiceCollectionExtensions.cs)
- [ApplicationShutdown.cs](../../../../src/App/Composition/ApplicationShutdown.cs)

**Session / broker / control plane**

- [AgentSessionService.cs](../../../../src/Features/Agents/Application/AgentSessionService.cs) (`CreateExecutionContextLocked`, revoke/cancel paths)
- [AgentExecutionCoordinator.cs](../../../../src/Features/Agents/Application/AgentExecutionCoordinator.cs)
- [AgentActionBrokerFactory.cs](../../../../src/Features/Agents/Application/AgentActionBrokerFactory.cs)
- [ContractAgentActionBroker.cs](../../../../src/Features/Agents/Application/ContractAgentActionBroker.cs)
- [UnavailableAgentActionBroker.cs](../../../../src/Features/Agents/Application/UnavailableAgentActionBroker.cs)
- [AgentActionRunSlotTracker.cs](../../../../src/Features/Agents/Application/AgentActionRunSlotTracker.cs)
- [AgentActionCorrelationRegistry.cs](../../../../src/Features/Agents/Application/AgentActionCorrelationRegistry.cs)
- [AgentActionRequestComposer.cs](../../../../src/Features/Agents/Application/AgentActionRequestComposer.cs)
- [AgentActionRequestFingerprintComputer.cs](../../../../src/Features/Agents/Application/AgentActionRequestFingerprintComputer.cs)
- [AgentActionPolicyClassifier.cs](../../../../src/Features/Agents/Application/AgentActionPolicyClassifier.cs)
- [RunScopedAgentActionEventPublisher.cs](../../../../src/Features/Agents/Application/RunScopedAgentActionEventPublisher.cs)
- [AgentActionAuditStore.cs](../../../../src/Features/Agents/Application/AgentActionAuditStore.cs)
- [AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs)

**Permission review surface**

- [InteractiveAgentPermissionReviewService.cs](../../../../src/Features/Agents/Application/InteractiveAgentPermissionReviewService.cs)
- [PermissionReviewViewModel.cs](../../../../src/Features/Agents/Application/PermissionReviewViewModel.cs)
- [PermissionReviewDialogPresenter.cs](../../../../src/Features/Agents/Presentation/PermissionReviewDialogPresenter.cs)
- [PermissionReviewDialog.axaml](../../../../src/Features/Agents/Presentation/PermissionReviewDialog.axaml)
- [PermissionReviewDialog.axaml.cs](../../../../src/Features/Agents/Presentation/PermissionReviewDialog.axaml.cs)
- [AgentActionDisplaySummaryBuilder.cs](../../../../src/Features/Agents/Application/AgentActionDisplaySummaryBuilder.cs)
- [AgentPermissionDecision.cs](../../../../src/Features/Agents/Domain/AgentPermissionDecision.cs)

**Proposal / mutation / command / authority**

- [AgentFileProposalGenerator.cs](../../../../src/Features/Agents/Application/AgentFileProposalGenerator.cs)
- [AgentFileActionProposal.cs](../../../../src/Features/Agents/Domain/AgentFileActionProposal.cs)
- [WorkspaceFileMutator.cs](../../../../src/Features/Agents/Infrastructure/WorkspaceFileMutator.cs)
- [WorkspaceFileReader.cs](../../../../src/Features/Agents/Infrastructure/WorkspaceFileReader.cs) (via DI registration)
- [DefaultAgentCommandResolver.cs](../../../../src/Features/Agents/Infrastructure/DefaultAgentCommandResolver.cs)
- [WorkspaceCommandExecutor.cs](../../../../src/Features/Agents/Infrastructure/WorkspaceCommandExecutor.cs)
- [AgentCommandDenylist.cs](../../../../src/Features/Agents/Domain/AgentCommandDenylist.cs)
- [WorkspaceActionAuthority.cs](../../../../src/Features/Workspace/Infrastructure/WorkspaceActionAuthority.cs)
- [WorkspaceEditorDocumentReconciler.cs](../../../../src/Features/Editor/Application/WorkspaceEditorDocumentReconciler.cs)

**Native Harness / ACP consumers**

- [NativeHarnessAgentBackend.cs](../../../../src/Features/Agents/Infrastructure/NativeHarnessAgentBackend.cs)
- [NativeHarnessLoopRunner.cs](../../../../src/Features/Agents/Application/NativeHarnessLoopRunner.cs)
- [NativeHarnessToolArgumentMapper.cs](../../../../src/Features/Agents/Application/NativeHarnessToolArgumentMapper.cs)
- [NativeHarnessToolResultFormatter.cs](../../../../src/Features/Agents/Application/NativeHarnessToolResultFormatter.cs)
- [AcpActionCapableAgentBackend.cs](../../../../src/Features/Agents/Application/Acp/AcpActionCapableAgentBackend.cs)
- [AcpAgentSessionAdapter.cs](../../../../src/Features/Agents/Application/Acp/AcpAgentSessionAdapter.cs)
- [AcpClientActionBridge.cs](../../../../src/Features/Agents/Application/Acp/AcpClientActionBridge.cs)
- [AcpClientPermissionBridge.cs](../../../../src/Features/Agents/Application/Acp/AcpClientPermissionBridge.cs)

**Contracts / taxonomy**

- [IAgentActionBroker.cs](../../../../src/Features/Agents/Contracts/IAgentActionBroker.cs)
- [IAgentActionBrokerFactory.cs](../../../../src/Features/Agents/Contracts/IAgentActionBrokerFactory.cs)
- [IAgentActionRequestCapableBackend.cs](../../../../src/Features/Agents/Contracts/IAgentActionRequestCapableBackend.cs)
- [AgentBackendExecutionContext.cs](../../../../src/Features/Agents/Contracts/AgentBackendExecutionContext.cs)
- [AgentActionKind.cs](../../../../src/Features/Agents/Domain/AgentActionKind.cs)
- [AgentActionBudgets.cs](../../../../src/Features/Agents/Domain/AgentActionBudgets.cs)
- [AgentActionAuditSummary.cs](../../../../src/Features/Agents/Domain/AgentActionAuditSummary.cs)

### 2.3 Tests (corroboration only; not proof of user wiring)

- Phase 17 suites named in M9 closeout (`Phase17Permission*`, `Phase17Proposal*`, `Phase17Workspace*`, `Phase17Command*`, `Phase17SessionEventIntegration*`, `Phase17BypassRatchet*`, `Phase17AdversarialCloseout*`)
- These prove contracts and fail-closed paths under test doubles; they do **not** prove a default cold-profile user can bind a tool-capable backend and complete an allow/deny journey.

---

## 3. Three-row verdict table

| id | a2_wiring_verdict | Summary |
|----|-------------------|---------|
| `A1-TP-01` | **Wired-with-gap** | Production DI composes a run-scoped `ContractAgentActionBroker` for `IAgentActionRequestCapableBackend` backends (Native Harness, ACP). Backend tool/fs requests enter the broker; for requests that **reserve the run slot and enter the lifecycle block**, classification, user review dialog, final-authorization `TryConsume`, executors, Phase 17 action events, in-memory audit records, and terminal Townhall projection of `ActionResultReported` are wired. Permission dialog owner is attached in [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs). `AgentActionRequest` and the permission dialog expose `InitiatingActorId` / `TargetActorId`; **`AgentActionFactPayload` and `AgentActionAuditRecord` do not store either actor id** (strong run/session/backend/action correlation only). **Gaps:** default cold profile has no user workflow to bind a tool-capable backend ([A2_AGENT_SEND](./A2_AGENT_SEND.md), [DF-008](../../../deferred/open/DF-008-multiple-agent-connections.md), [DF-009](../../../deferred/open/DF-009-real-acp-integrations.md)); ACP mediates only `fs/read_text_file` and `fs/write_text_file` (no delete/command bridge); several pre-admission/early-return broker failures (and `UnavailableAgentActionBroker`) return to the calling backend **without** publishing `ActionResultReported`, writing an audit record, or projecting a Townhall action entry; Townhall projects only emitted `ActionResultReported`, not the full action lifecycle and not backend-only denials; audit store is in-memory with no inspection UI and no explicit actor-id fields; dialog/audit can expose bounded proposed file content; ACP `session/request_permission` is automatic and reject-preferring protocol handling (not guaranteed fail-closed: selects `reject_once` when present, otherwise the first supplied option), not a user choice surface and not Phase 17 broker authorization; runtime allow/deny/effect behavior remains A3-unproven. |
| `A1-TP-02` | **Wired-with-gap** | Production implements a closed five-kind taxonomy (`ReadFile`, `CreateFile`, `ReplaceFile`, `DeleteFile`, `ExecuteCommand`) with locked-policy auto-allow for reads, user decision for writes/commands, pre-review command denylist, exact-request fingerprint binding, 5-minute expiry, and broker revocation on cancel/end/dispose/workspace invalidation. **Gaps versus documented goal dimensions:** no dedicated network, Git, secrets, destructive, memory, or external-workspace permission classes; approval scope is a fixed “this exact request only” label, not a user-selectable scope; no persistent/run-scoped grants; no user-reachable permission-management or revocation UI (revocation is lifecycle-driven); working-directory validation is not an OS filesystem/network sandbox. Partial implementation is real, so not `Missing`. |
| `A1-TP-03` | **Wired-with-gap** | Optimistic base-revision checks, workspace-generation invalidation, and one-non-terminal-action-per-run admission are production-wired. **Pre-consume** stale detection re-reads the file before `TryConsume()` and returns `Revoked` / `StaleBaseRevision` while leaving a still-`Published` decision unconsumed (no mutation). **Post-consume** execution helpers revalidate workspace/fingerprint/identity, and `WorkspaceFileMutator.Apply` can still return `Conflict` / `StaleBaseRevision` after the decision is already `Consumed` (no write should occur, but the decision is no longer `Published`). Conflicts stop the action; open-document reconciliation exists. When the lifecycle path emits `ActionResultReported`, terminal conflict/result summaries project to Townhall as system notifications and return to backends as tool/JSON-RPC results; several pre-admission broker denials remain backend-visible only. **Gaps versus documented goal:** no multi-file atomic transaction; no agent-attributed change-set abstraction; no agent-level rollback command/UI (temp-file atomic replace and stale-base rejection are not rollback); no multi-file partial-apply cancellation semantics; no dedicated conflict surface beyond action-result projection when that event is emitted; interactions with build/test/debug are not a separate coordination plane. |

Verdict definitions: [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition).

---

## 4. Supported-action coverage matrix

| Action | Kind | Native Harness tool path | ACP client path | Policy class | Requires user review | Executor | Notes |
|--------|------|--------------------------|-----------------|--------------|----------------------|----------|-------|
| Read file | `ReadFile` | Yes — tool map → `broker.RequestAsync` | Yes — `fs/read_text_file` | `AllowedByLockedPolicy` | No | `IAgentFileReader` | Auto-allowed after composition/workspace checks |
| Create file | `CreateFile` | Yes | Via write when prior read fails as not-found | `RequiresUserDecision` | Yes | `WorkspaceFileMutator` | Proposal fail-closed if target already exists |
| Replace file | `ReplaceFile` | Yes | Via write after successful authoritative read | `RequiresUserDecision` | Yes | `WorkspaceFileMutator` | Base revision required |
| Delete file | `DeleteFile` | Yes | **No ACP bridge method** | `RequiresUserDecision` | Yes | `WorkspaceFileMutator` | Native Harness only among production backends |
| Execute command | `ExecuteCommand` | Yes | **No ACP bridge method** | `RequiresUserDecision` (or `DeniedByPolicy` if denylist) | Yes if not denylisted | `WorkspaceCommandExecutor` | Denylist before review; revalidated before start |
| Unknown / unsupported kind | n/a | Mapped as tool validation failure / not composed | Falls through non-fs methods to fallback router | `DeniedByPolicy` for unknown kind in classifier | n/a | none | Fail closed |

Sources: [NativeHarnessToolArgumentMapper.cs](../../../../src/Features/Agents/Application/NativeHarnessToolArgumentMapper.cs), [AcpClientActionBridge.cs](../../../../src/Features/Agents/Application/Acp/AcpClientActionBridge.cs), [AgentActionPolicyClassifier.cs](../../../../src/Features/Agents/Application/AgentActionPolicyClassifier.cs), [AgentActionKind.cs](../../../../src/Features/Agents/Domain/AgentActionKind.cs).

---

## 5. End-to-end wiring maps

Legend: **T** = type/contract · **R** = production DI · **C** = production caller · **U** = user-reachable · **P** = user-visible result/failure · **A3** = runtime behavior unproven without A3.

### 5.1 Common control-plane spine (shared by direct send + backend-initiated actions)

```text
Townhall direct send
  → AgentRouter / AgentExecutionCoordinator
  → AgentSessionService.SendAsync (when actor is bound)
  → CreateExecutionContextLocked
       if backend is IAgentActionRequestCapableBackend
          and brokerFactory + auditStore present:
            ContractAgentActionBroker (run-scoped)
       else:
            UnavailableAgentActionBroker  (BrokerUnavailable; no event publisher / audit write)
  → backend.ExecuteAsync(AgentBackendExecutionContext)
  → backend tool/fs request
  → IAgentActionBroker.RequestAsync(payload, correlationKey, ct)
  → early-return denials possible BEFORE run-slot reservation
       (revoked broker, invalid payload, no workspace, compose/proposal failure,
        correlation mismatch/duplicate replay, cancel/revoke while waiting,
        concurrent-slot rejection) → backend-only result; no ActionResultReported
  → only if run slot reserved: compose path continues into lifecycle block
  → classify
  → allow / deny / ask
  → final authorization (see §7)
  → executor/mutator (post-consume safety rechecks before effect)
  → RunScopedAgentActionEventPublisher (event stream + audit store)
  → AgentConversationEventProjection projects ActionResultReported only (when emitted)
  → Townhall conversation Messages refresh
```

| Seam | T | R | C | U | P | Evidence |
|------|---|---|---|---|---|----------|
| Townhall send creates admitted run | ✓ | ✓ | ✓ when bound | ✓ | partial | Same path as [A2_AGENT_SEND](./A2_AGENT_SEND.md); unbound rejects pre-session |
| Run-scoped broker created on send | ✓ | ✓ | ✓ for action-capable backends | when bound + admitted | — | [AgentSessionService.cs](../../../../src/Features/Agents/Application/AgentSessionService.cs) `CreateExecutionContextLocked` |
| Same broker for backend-initiated actions | ✓ | ✓ | ✓ | when bound | — | Broker stored on `LiveRun.ActionBroker` and passed as `context.Actions` |
| Unavailable broker for non-capable backends | ✓ | ✓ | ✓ | — | backend-only deny | Legacy / missing factory → `UnavailableAgentActionBroker` |
| Permission dialog owner attach | ✓ | ✓ | ✓ | modal when review needed | ✓ | [App.axaml.cs](../../../../src/App/Composition/App.axaml.cs) `SetOwner` |
| Terminal action projection | ✓ | ✓ | ✓ when lifecycle emits | ✓ when emitted | ✓ when emitted | [AgentConversationEventProjection.cs](../../../../src/Features/Agents/Application/AgentConversationEventProjection.cs) projects `ActionResultReported` **only when that event is emitted**; pre-admission early returns are not projected |
| Full lifecycle projection | partial | ✓ | publishes fact kinds only after run-slot reservation | no | no | Requested/Classified/Decided/Started/Reconciliation remain event/audit-only and only for admitted lifecycle processing |
| Explicit actor ids in audit facts | no | ✓ store | publisher copies non-actor fields | dialog only | no actor ids in audit record | `AgentActionRequest` / `PermissionReviewViewModel` hold actor ids; `AgentActionFactPayload` / `AgentActionAuditRecord` do not |

**Does a Townhall send create the run-scoped broker?**
Yes, when the selected backend implements `IAgentActionRequestCapableBackend` and production DI supplied `IAgentActionBrokerFactory` + `IAgentActionAuditStore`. Otherwise the run receives `UnavailableAgentActionBroker`.

**Is the same broker used by direct sends and backend-initiated actions?**
Yes. Direct send only admits the run and builds the execution context; tool/fs actions reuse `context.Actions` for that run.

**Can Native Harness or ACP bypass the broker?**
Source search of production `src/` finds no second filesystem/command path for these backends outside `IAgentActionBroker.RequestAsync` for mediated actions. Provider transport and ACP process launch are not workspace mutators. ACP `session/request_permission` is intentionally **not** broker authorization ([AcpClientPermissionBridge.cs](../../../../src/Features/Agents/Application/Acp/AcpClientPermissionBridge.cs); Phase 20 plan).

### 5.2 Native Harness map

```text
NativeHarnessAgentBackend.ExecuteAsync(context)
  → NativeHarnessLoopRunner
  → model tool calls
  → NativeHarnessToolArgumentMapper (5 tool names → 5 payloads)
  → context.Actions.RequestAsync(payload, toolCallId, ct)
  → ContractAgentActionBroker ...
  → NativeHarnessToolResultFormatter.Format(result) back into model history
  → ActionResultReported → Townhall (if published)
```

| Seam | T | R | C | U | P | A3 |
|------|---|---|---|---|---|----|
| Implements `IAgentActionRequestCapableBackend` | ✓ | ✓ | ✓ | when bound | — | unproven |
| Tool names for all five actions | ✓ | ✓ | ✓ | when bound | tool result always; Townhall only when `ActionResultReported` is emitted | unproven |
| Unsupported tool name | ✓ | ✓ | ✓ | when bound | model sees validation failure; no broker call | unproven |
| Workspace-authority gated capability | ✓ | ✓ | ✓ | status only | capability snapshot may report no workspace | unproven |

### 5.3 ACP map

```text
AcpActionCapableAgentBackend.ExecuteAsync(context)
  → AcpAgentSessionAdapter(enableActionBridge: true)
  → if context.Actions is not Unavailable:
       advertise filesystem client capabilities
       AcpClientActionBridge(context.Actions, cwd, acpSessionId)
  → inbound:
       fs/read_text_file  → ReadFile payload → Phase 17 broker
       fs/write_text_file → read-then Create/Replace → Phase 17 broker
       session/request_permission → AcpClientPermissionBridge (NOT broker,
         NOT Phase 17 permission dialog, NOT AgentPermissionDecision)
       other methods → fallback router (not Phase 17 mediation)
```

| Seam | T | R | C | U | P | A3 |
|------|---|---|---|---|---|----|
| Action-capable backend | ✓ | ✓ | ✓ | when bound | — | unproven |
| Read/write mediation (Phase 17 broker) | ✓ | ✓ | ✓ | when bound | ACP error/result; Townhall only when lifecycle emits `ActionResultReported` | unproven |
| Delete/command mediation | types exist | ✓ | **no ACP caller** | no via ACP | n/a | n/a |
| `session/request_permission` | ✓ | ✓ | ✓ auto bridge | **no** production user choice surface | automatic reject-preferring ACP protocol response only (not guaranteed fail-closed) | unproven |

**ACP `session/request_permission` production truth (not a user-reachable permission-choice path):**

- The method is intentionally separate from Phase 17 broker authorization.
- `AcpClientPermissionBridge` uses `AcpFailClosedPermissionChoiceSource` when no custom choice source is injected.
- The bridge rejects an empty options list as invalid parameters before calling the choice source.
- No production UI choice source is injected.
- With one or more options, the default source initializes its choice to the first supplied option, searches for an option whose `Kind` is exactly `reject_once`, selects that option when found, and otherwise selects the first option regardless of kind.
- Production ACP permission handling is automatic and reject-preferring, not guaranteed fail-closed: it selects `reject_once` when present, otherwise the first supplied option.
- If the first option is `allow_once` or another permissive kind and no `reject_once` option is supplied, the default source returns that permissive selection automatically.
- The bridge does **not** show Zaide’s permission dialog.
- It does **not** create, publish, or consume `AgentPermissionDecision`.
- It does **not** authorize Phase 17 broker-mediated filesystem mutation or command execution.
- It does **not** create Phase 17 audit attribution.
- Therefore it must not be described as a user choice or a guaranteed rejection path.

**Contradiction note (class name vs live behavior):** Phase 20 documentation and the class name `AcpFailClosedPermissionChoiceSource` describe the default as “fail-closed.” Live code falls back to the first supplied option when `reject_once` is absent. A2 records the live behavior and does not inherit the stronger historical label.

Keep ACP filesystem read/write mediation through the Phase 17 broker **distinct** from ACP `session/request_permission`.

### 5.4 Unavailable / fail-closed branches

Legend for **Audit / Townhall:** only paths that reserve the run slot and enter the lifecycle/publisher block emit Phase 17 action facts (including terminal `ActionResultReported`) and therefore can be audited/projected. Early returns below are **backend-visible only** unless noted otherwise.

| Missing / invalid condition | Observed production behavior | Audit / Townhall | Source |
|-----------------------------|------------------------------|------------------|--------|
| Backend not action-capable or factory/audit null | `UnavailableAgentActionBroker` returns `BrokerUnavailable` (no event publisher; no audit-store write) | None | [AgentSessionService.cs](../../../../src/Features/Agents/Application/AgentSessionService.cs) L1541–1545; [UnavailableAgentActionBroker.cs](../../../../src/Features/Agents/Application/UnavailableAgentActionBroker.cs) |
| Broker already revoked | Early return `BrokerRevoked` before lifecycle | None | [ContractAgentActionBroker.cs](../../../../src/Features/Agents/Application/ContractAgentActionBroker.cs) L137–142 |
| Invalid payload-kind consistency | Early return `InvalidRequest` before lifecycle | None | Broker L146–151 |
| No workspace open | Broker captures null scope; early return `NoWorkspace` before composition/lifecycle | None | Broker L154–161 |
| Request/proposal composition failure | Early return `InvalidRequest` before run-slot reservation | None | Broker L170–212 |
| Correlation mismatch or duplicate replay | Early return mismatch / `DuplicateReplay` before (or without completing) lifecycle publish | None for pure early-return replay/mismatch paths | Broker L216–315 |
| Cancellation/revocation while waiting for correlation | Early return cancelled / `BrokerRevoked` | None | Broker L267–284, L354–361 |
| Failure to reserve run slot / concurrent action | Denied `ConcurrentActionRejected` (or cancel while waiting for slot) without entering lifecycle | None | [AgentActionRunSlotTracker.cs](../../../../src/Features/Agents/Application/AgentActionRunSlotTracker.cs); broker L327–370 |
| Permission presenter has no owner window | Throws → broker maps `PermissionUnavailable` **after** admission into lifecycle | Yes (`ActionResultReported` when lifecycle terminal published) | [PermissionReviewDialogPresenter.cs](../../../../src/Features/Agents/Presentation/PermissionReviewDialogPresenter.cs); broker L451–461 |
| Unknown action kind | Classifier returns `DeniedByPolicy` **after** admission | Yes when lifecycle publishes result | [AgentActionPolicyClassifier.cs](../../../../src/Features/Agents/Application/AgentActionPolicyClassifier.cs) `_ => DeniedByPolicy` |
| Denylisted executable | `DeniedByPolicy` **before** review (after admission); executor also rechecks | Yes when lifecycle publishes result | Classifier L14–17; [WorkspaceCommandExecutor.cs](../../../../src/Features/Agents/Infrastructure/WorkspaceCommandExecutor.cs) |
| Self-approval (`initiatingActorId == targetActorId`) | Denied **after** admission into review path | Yes when lifecycle publishes result | Broker L403–412 |
| Broker revoked after admission (cancel/end/dispose/workspace switch during processing) | Subsequent lifecycle outcomes as published results; bare `Revoke()` does not fabricate action identity | Result path may Townhall when `ActionResultReported` emitted | Session revoke paths + broker `Revoke()` / `PublishRevocationFact` empty |
| Actor unbound (default cold profile) | Coordinator rejects **before** session/broker | n/a (pre-broker) | [A2_AGENT_SEND](./A2_AGENT_SEND.md) — reused, not re-proven as success path |

**Wording for projection:** Townhall projects `ActionResultReported` when that event is emitted; several pre-admission/early-return broker failures emit no action event and remain backend-visible only. Do not treat an eventual assistant message that might summarize a tool result as a guaranteed Zaide action projection.

---

## 6. Permission classification and decision matrix

| Input | Classification | Review? | Terminal without review | Notes |
|-------|----------------|---------|-------------------------|-------|
| `ReadFile` | `AllowedByLockedPolicy` | No | Executes read | Locked policy auto-allow |
| `CreateFile` / `ReplaceFile` / `DeleteFile` | `RequiresUserDecision` | Yes | — | Modal Allow / Deny / dismiss |
| `ExecuteCommand` allowed by denylist | `RequiresUserDecision` | Yes | — | Resolved path/args shown |
| `ExecuteCommand` denylisted shell/privilege helper | `DeniedByPolicy` | No | Denied | Denylist evaluated at composition/classification **before** review |
| Unknown kind | `DeniedByPolicy` | No | Denied | Fail closed |
| Allow | decision `Published`, `IsAllow=true` | — | continues to final auth | Expiry = now + 5 min |
| Deny | decision `Denied`, `IsAllow=false` | — | `PermissionDenied` | Explicit Deny button |
| Dismiss / close dialog | same as deny | — | `PermissionDenied` | `DismissCommand` and window close resolve `false` |
| Review cancelled by token | n/a | — | `Cancelled` | Distinct from deny |
| Review service/UI failure | n/a | — | `PermissionUnavailable` | Fail closed |

Sources: [AgentActionPolicyClassifier.cs](../../../../src/Features/Agents/Application/AgentActionPolicyClassifier.cs), [InteractiveAgentPermissionReviewService.cs](../../../../src/Features/Agents/Application/InteractiveAgentPermissionReviewService.cs), [PermissionReviewViewModel.cs](../../../../src/Features/Agents/Application/PermissionReviewViewModel.cs), [AgentActionBudgets.cs](../../../../src/Features/Agents/Domain/AgentActionBudgets.cs) (`PermissionDecisionLifetime = 5 minutes`).

**Dismissal is equivalent to denial.** Source-proven: `DismissCommand` and `ResolveDismiss()` call `Resolve(false)`; review service maps `isAllowed=false` to `AgentPermissionDecisionStatus.Denied`.

**Are raw file contents, secrets, or unrestricted output exposed?**

| Surface | Content exposure | Redaction / bound |
|---------|------------------|-------------------|
| Permission dialog summary | Bounded proposal preview (create/replace/delete current content) up to 50 lines / 8 KB | Truncation only; not secret-aware |
| Dialog path fields | Workspace-relative and resolved absolute path (containment rechecked) | Escaped paths withheld |
| Audit summary | Request detail / result summary may include proposed text | Weak pattern redaction (`api_key=`, `password=`, `token=`) then 8 KB bound; non-matching secrets not redacted |
| Townhall terminal entry (when `ActionResultReported` emitted) | Action kind, result kind, evidence level, bounded audit summary text | Same audit summary text; not all broker returns produce this entry |
| Command output | Budgeted in executor; not re-audited here | Truncation at command budgets |

There is **no** unrestricted command-output dump into the permission dialog. There **is** deliberate proposed-content preview in the review surface.

---

## 7. Final-authorization ordering analysis

Inspected: [ContractAgentActionBroker.cs](../../../../src/Features/Agents/Application/ContractAgentActionBroker.cs) decision branch for `RequiresUserDecision`.

Exact order for write/command paths that reach user review (admitted lifecycle processing only):

1. **Request / fingerprint validation** — `AgentActionRequestComposer.Compose` builds immutable request + fingerprint; payload kind consistency checked earlier; file proposals generated fail-closed for create/replace/delete.
2. **Workspace validation** — `IsCurrent(_workspaceScope)` before showing review and again after decision returns (workspace-generation currency on the captured scope).
3. **Decision / fingerprint / status / expiry / allow validation** — fingerprint match, proposal/fingerprint/base binding, classification must be `RequiresUserDecision`, status must be `Published` or `Denied`, `DateTimeOffset.UtcNow > ExpiresAtUtc` rejects, deny/dismiss rejected.
4. **Pre-consume file base-revision validation** — `IsFileProposalStaleBeforeConsumption(...)` re-reads the target **before** `TryConsume`. Create must still be absent; replace/delete base must still match. On already-stale base:
   - result = `Revoked` / `StaleBaseRevision`
   - lifecycle → `Revoked`
   - **still-`Published` decision is not consumed**
   - **no mutation executes**
5. **`AgentPermissionDecision.TryConsume()`** — atomic `Published → Consumed`. This is the **final authorization step** (not the final safety validation). Failure → denied without execution.
6. **Post-consume execution-time safety validation** — after successful `TryConsume`:
   - `ExecuteApprovedFileMutation` rechecks workspace currency and request/proposal fingerprint, then `WorkspaceFileMutator.Apply` rechecks target/base state. A race after pre-consume validation but before apply can return `Conflict` / `StaleBaseRevision` **after** the decision is already `Consumed`. No write should occur, but the decision is no longer `Published`.
   - `ExecuteApprovedCommand` revalidates workspace currency, executable resolution, and request fingerprint. A post-consume command-identity failure can revoke/fail **without** starting the process while the decision remains consumed.
7. **Filesystem / process effect** — only if those post-consume checks pass does apply/start produce the mutation or process effect.

**Source truth (do not simplify):**

| Window | Detection | Decision state after | Mutation / process | Typical result |
|--------|-----------|----------------------|--------------------|----------------|
| **Pre-consume stale** | Broker re-reads file before `TryConsume` | Remains `Published` (not consumed) | None | `Revoked` / `StaleBaseRevision` |
| **Post-consume / apply race** | Execution helper + mutator rechecks after successful `TryConsume` | Already `Consumed` | No write/start should occur | File: `Conflict` / `StaleBaseRevision` (or related failure); Command: revoked/failed identity check without process start |

Invariant preserved: a proposal already known stale **before** `TryConsume()` must return `Revoked` / `StaleBaseRevision` **without** consuming its `Published` decision. Do not describe `TryConsume()` as the final safety validation, and do not claim every stale-base outcome leaves the decision `Published`.

Read path (`AllowedByLockedPolicy`) does not use `TryConsume`; it revalidates workspace currency then executes the read under locked policy.

---

## 8. Permission-dimension coverage versus documented goal

Goal language (V3 tools/permissions + Phase 17 / `A1-TP-02`): read/write, workspace-internal/external, process, network, Git, secrets, destructive, memory, approval scope, canonical description, expiry, revocation.

| Goal dimension | Production state | Evidence |
|----------------|------------------|----------|
| Read / write | **Present (partial)** | Read auto-allowed; create/replace/delete require decision |
| Workspace-internal / external | **Containment only** | Paths must resolve under captured workspace root; no external-workspace action kinds |
| Process | **Present (partial)** | `ExecuteCommand` with denylist + working-dir containment; not a full process sandbox taxonomy |
| Network | **Absent as dimension** | No network permission class; command may still touch network if executable is allowed |
| Git | **Absent as dimension** | Generic command only; no Git-specific permission surface |
| Secrets | **Absent as permission class** | Weak audit redaction only |
| Destructive | **Not a separate class** | `DeleteFile` is an ordinary write-like decision; no destructive taxonomy/UI |
| Memory access | **Absent** | Memory subsystem is separate Phase 21 store; not a Phase 17 permission dimension |
| Approval scope | **Fixed label only** | UI text `Scope: this exact request only.`; not user-selectable; no always-allow / run-scoped grant |
| Canonical action description | **Present** | Fingerprint + display/proposal summary bind the exact request |
| Expiry | **Present** | 5-minute decision lifetime |
| Revocation | **Lifecycle-present / user-management absent** | Broker revoke on cancel/end/dispose/workspace invalidation; no permission-management UI |

**Mapping under A2 definitions:** the mediated allow/deny/ask control plane exists, so the row is not `Missing`. The documented multi-dimension permission model is only partially realized → **`Wired-with-gap`**.

Important non-equivalences (source-proven):

- Fixed “this exact request only” is **not** a selectable approval scope.
- Working-directory validation is **not** an OS filesystem or network sandbox ([PermissionReviewViewModel.ContainmentDisclosureText](../../../../src/Features/Agents/Application/PermissionReviewViewModel.cs)).
- Generic commands are **not** dedicated Git/network/package/secrets/destructive dimensions.
- In-memory audit records are **not** a permission-management UI (`IAgentActionAuditStore` has no production UI consumer; only `Record` via publisher).
- Broker revocation on session disposal is **not** user-reachable revocation management.

---

## 9. Mutation / concurrency / rollback subclaim matrix

| # | Subclaim | Production finding | Verdict contribution |
|---|----------|--------------------|----------------------|
| 1 | Optimistic base-revision checks | Present in proposal generation, pre-consume revalidation, and post-consume mutator apply rechecks | Wired |
| 2 | Stale proposal rejection | **Pre-consume:** `Revoked` / `StaleBaseRevision`, still-`Published` unconsumed, no mutation. **Post-consume apply race:** decision already `Consumed`; mutator can still return `Conflict` / `StaleBaseRevision` without writing. Both windows reject the effect; only pre-consume preserves `Published`. | Wired |
| 3 | Workspace-generation invalidation | `WorkspaceActionAuthority` advances generation; broker checks `IsCurrent`; session revokes active run broker on `ScopeInvalidated` | Wired |
| 4 | Concurrent action admission | One non-terminal action per run; second denied | Wired |
| 5 | Conflicts with user/editor/build/test/debug | Base-revision conflicts stop apply; open dirty/clean document reconciliation exists; **no** dedicated build/test/debug coordination plane | Partial |
| 6 | Agent-attributed change set | Individual action ids/attempts + audit facts only; **no** change-set aggregate | Missing |
| 7 | Multi-file transaction behavior | File proposals apply individually; plan limitation confirmed live | Missing |
| 8 | Rollback of one agent’s changes without destroying unrelated work | **No** rollback command, model, or UI. Same-directory temp-file atomic replace is apply safety, not agent rollback. Stale-base rejection is not rollback. | Missing |
| 9 | Cancellation during partially applied multi-file changes | Single-file/command cancellation exists; multi-file partial-apply semantics do not exist because multi-file transactions do not exist | Missing / N/A |

**Conflict visibility**

| Path | Visible? | Evidence |
|------|----------|----------|
| Originating conversation (Townhall) | Yes **only when** terminal `ActionResultReported` is emitted | Projection formats `zaide-action|...|resultKind|...|summary`. Pre-admission/early-return denials emit no action event and are not Townhall-projected. |
| Backend tool/JSON-RPC result | Yes for broker return values (including early denials) | Native Harness tool history; ACP error mapping for stale/denied/conflict; assistant text is not a guaranteed Zaide action projection |
| Dedicated conflict UI | No | No separate conflict panel |
| Reconciliation facts | Event/audit only when published (not projected as separate Townhall kinds) | Projection handles only `ActionResultReported` among action kinds |

Do **not** call atomic temp-file replacement "agent rollback."
Do **not** call rejection of a stale base revision "rollback."
Do **not** claim multi-file cancellation semantics from single-file cancellation paths.

---

## 10. DI registration and production-caller analysis

### 10.1 Production DI registrations

| Service | Registration | File |
|---------|--------------|------|
| `IAgentPermissionDialogPresenter` → `PermissionReviewDialogPresenter` | Singleton | [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) |
| `IAgentPermissionReviewService` → `InteractiveAgentPermissionReviewService` | Singleton | same |
| `IAgentActionAuditStore` → `AgentActionAuditStore` | Singleton | same |
| `IAgentFileReader` / `IAgentFileMutator` | Singleton | same |
| `IAgentCommandResolver` / `IAgentCommandExecutor` | Singleton | same |
| `IAgentActionBrokerFactory` → `AgentActionBrokerFactory` | Singleton | same |
| `IWorkspaceActionAuthority` → `WorkspaceActionAuthority` | Singleton | [WorkspaceServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/WorkspaceServiceCollectionExtensions.cs) |
| `IAgentDocumentReconciler` → `WorkspaceEditorDocumentReconciler` | Singleton | [EditorServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/EditorServiceCollectionExtensions.cs) |
| `NativeHarnessAgentBackend` / `AcpActionCapableAgentBackend` as `IAgentBackend` | Singleton | Agents registration |
| `IAgentSessionService` → `AgentSessionService` | Singleton | Agents registration (constructor-injected optional broker/audit/authority when registered) |

### 10.2 Production callers of `IAgentActionBroker.RequestAsync`

Repository search under `src/` finds only:

1. [NativeHarnessLoopRunner.cs](../../../../src/Features/Agents/Application/NativeHarnessLoopRunner.cs)
2. [AcpClientActionBridge.cs](../../../../src/Features/Agents/Application/Acp/AcpClientActionBridge.cs) (read, write, and internal read for write composition)

`CreateRunScopedBroker` production caller: [AgentSessionService.CreateExecutionContextLocked](../../../../src/Features/Agents/Application/AgentSessionService.cs) only.

`IAgentActionAuditStore.GetRunSnapshot` / `GetCurrentLifetimeSnapshot`: **no production UI/caller** beyond the store implementation itself; only `Record` is used by `RunScopedAgentActionEventPublisher`.

### 10.3 Attribution of facts

Required distinction (source-proven):

- `AgentActionRequest` **contains** `InitiatingActorId` and `TargetActorId`.
- `PermissionReviewViewModel` **exposes** those actor ids in the modal dialog.
- `AgentActionFactPayload` **does not** contain either actor id.
- `AgentActionAuditRecord` **does not** contain either actor id.
- `RunScopedAgentActionEventPublisher` copies session, run, conversation, backend, action, attempt, workspace, sequence, and summary into the audit record, but **does not** copy initiating or target actor identity.
- Townhall chooses an author from conversation/catalog context (`ResolveAgentAuthor`); that inference is **not** equivalent to an actor id recorded in the audit fact.

Therefore production has strong run/session/backend/action correlation but does **not** satisfy a documented explicit actor-attribution promise in the stored action audit fact.

| Fact / surface | Explicit actor ids? | Backend | Session | Run | Action | Where visible |
|----------------|---------------------|---------|---------|-----|--------|---------------|
| `AgentActionRequest` | **Yes** (`InitiatingActorId`, `TargetActorId`) | yes | yes | yes | yes | Broker-internal + permission dialog only |
| Permission dialog | **Yes** (from request) | yes | via request | via request | yes | Modal UI only |
| `AgentActionFactPayload` / action event | **No actor ids** | via event envelope | yes | yes | yes | Event stream when published after admission |
| `AgentActionAuditRecord` | **No actor ids** | yes | yes | yes | yes | In-memory audit store when publisher runs |
| Action request fact | no actor ids in payload | yes | yes | yes | yes | Event + audit only (admitted path) |
| Permission decision fact | no actor ids in payload | yes | yes | yes | yes | Event + audit only (admitted path) |
| Execution result fact | no actor ids in payload | yes | yes | yes | yes | Event/audit **and** Townhall **only if** `ActionResultReported` emitted |
| Reconciliation fact | no actor ids in payload | yes | yes | yes | yes | Event/audit only when published |
| Revocation | no fabricated actor ids; bare revoke publishes nothing | yes | yes | yes | when action-scoped result exists | Result may Townhall when `ActionResultReported` emitted; `PublishRevocationFact` is intentionally empty |
| Townhall system notification author | Inferred conversation/catalog author, **not** audit fact actor fields | via event | via event | via event | via payload action id | Conversation Messages only when projection runs |

---

## 11. User reachability and result/failure visibility

| User-observable claim | Reachable in production source? | Notes |
|-----------------------|----------------------------------|-------|
| Permission UI for unapproved write/command | **Yes, when** a tool-capable backend is bound, a workspace is open, dialog owner is attached, and the backend issues a write/command that reaches admitted lifecycle review | Default cold path blocked by backend-binding gap; pre-admission denials never show this dialog |
| Mediation for reads without dialog | Yes under locked policy when admitted | Still requires broker + workspace; early NoWorkspace/etc. remain backend-only |
| Explicit actor ids in stored audit facts | **No** | Dialog shows request actor ids; `AgentActionFactPayload` / `AgentActionAuditRecord` omit them; run/session/backend/action correlation only |
| In-memory audit record coverage | **Partial** | Records only when publisher runs (admitted lifecycle facts). Not durable; no audit browser UI. Early broker returns write nothing. |
| Action outcome in conversation | **Only when** `ActionResultReported` is emitted | System notification with bounded summary; pre-admission failures remain backend-visible only |
| ACP `session/request_permission` user choice | **No** | Automatic reject-preferring protocol handling via default `AcpFailClosedPermissionChoiceSource` (selects `reject_once` when present, otherwise first supplied option—including permissive kinds); no production UI choice source; does not create/consume `AgentPermissionDecision`; not Phase 17 dialog/audit/broker authorization |
| Approval-scope selection | No | Fixed label |
| Permission revoke management UI | No | Lifecycle revoke only |
| Multi-file edit + rollback UI | No | Not implemented |
| Default cold-profile tool journey | **No** | Reuses [A2_AGENT_SEND](./A2_AGENT_SEND.md) / [DF-008](../../../deferred/open/DF-008-multiple-agent-connections.md) / [DF-009](../../../deferred/open/DF-009-real-acp-integrations.md): no production bind UI |

---

## 12. Source-proven versus runtime-unproven findings

### Source-proven

1. Control-plane types, DI, session broker creation, Native Harness tool loop, ACP fs read/write bridge, permission dialog ownership, final-auth order with pre-consume vs post-consume windows, denylist-before-review, dismiss=deny, one-slot concurrency, workspace-generation checks, admitted-lifecycle event/audit publishing, Townhall projection only of emitted `ActionResultReported`.
2. Actor attribution distinction: request + dialog carry `InitiatingActorId` / `TargetActorId`; fact payload + audit record do **not**; publisher does not copy actor identity; Townhall author is conversation/catalog inference, not an audit actor field.
3. Not every terminal broker return is audited/projected: pre-admission/early-return paths and `UnavailableAgentActionBroker` return to the backend without `ActionResultReported` / audit store writes; only reserved run-slot lifecycle processing publishes action facts and a terminal result event.
4. ACP `session/request_permission` is a separate automatic reject-preferring protocol response (default `AcpFailClosedPermissionChoiceSource`: `reject_once` when present, otherwise first supplied option—not guaranteed fail-closed); no production UI choice source; does not create/consume `AgentPermissionDecision`, does not authorize Phase 17 broker-mediated mutation/command execution, and does not create Phase 17 audit attribution. Distinct from Phase 17 broker mediation of `fs/read_text_file` / `fs/write_text_file`.
5. Explicit product absences: multi-file transactions, agent change sets, agent rollback UI/command, network/Git/secrets/memory/destructive permission classes, selectable approval scopes, audit inspection UI, ACP delete/command mediation, production backend-binding UI, explicit actor ids in stored action audit facts, user-reachable ACP permission-choice path.
6. Default cold profile cannot reach tool mediation without an authorized bind hook or future bind UI (prior A2).

### Runtime-unproven without A3

1. Actual modal appearance, keyboard-only flow, and timing under a live desktop session.
2. Real filesystem create/replace/delete effects and absence of unauthorized effects after deny/dismiss/expiry/revoke and after both pre-consume and post-consume stale windows.
3. Real process start, denylist behavior against live executables, output truncation, and process-tree cancellation; post-consume command-identity failure without process start.
4. End-to-end Native Harness provider tool-calling with a real model endpoint, including which failures stay backend-only vs Townhall-projected.
5. End-to-end ACP candidate process issuing `fs/*` (Phase 17 broker) versus `session/request_permission` (automatic reject-preferring protocol path; permissive first-option fallback when `reject_once` absent; not a user choice surface; not Phase 17 authorization).
6. Whether Townhall action-result entries (when emitted) are discoverable/understandable to a user in practice.
7. Live race outcomes under true concurrent editor saves vs agent apply across the pre-consume and post-consume windows (unit coverage exists; live race not re-run here).

---

## 13. Contradiction or attribution corrections

| Claim source | Correction from this A2 inspection |
|--------------|------------------------------------|
| Phase 17 M9 closeout “control plane complete” | Complete as a **composed mediation substrate**, not as a default-user cold-path product journey. Backend bind gap still blocks entry. |
| Phase 17 limitations “No current production backend gains tool or permission capability in this phase” | Historical Phase 17 statement. **Later** Phase 19/20 production backends do implement `IAgentActionRequestCapableBackend` and call the broker. Do not treat the Phase 17 limitation sentence as current whole-product truth. |
| Treating ACP `session/request_permission` as Zaide permission grant | Incorrect. Bridge is separate, defaults to `AcpFailClosedPermissionChoiceSource`, has no production UI choice source, does not show Zaide’s dialog, does not create/consume `AgentPermissionDecision`, does not authorize Phase 17 broker-mediated mutation/command execution, and does not create Phase 17 audit attribution. |
| Suggesting ACP permission “may surface ACP choice path” to a user | Incorrect for production. Automatic reject-preferring selection only (`reject_once` when present, else first option); not user-reachable. |
| Phase 20 / `AcpFailClosedPermissionChoiceSource` “fail-closed” label | Historical naming. Live default source prefers `reject_once` when present but falls back to the first supplied option (which may be permissive). A2 records live behavior; does not inherit the stronger label. |
| Treating ACP `session/request_permission` as guaranteed fail-closed rejection | Incorrect. Reject-preferring only; permissive first option is returned when no `reject_once` is supplied. Not user choice; not Phase 17 authorization; does not consume `AgentPermissionDecision`. |
| Treating atomic temp-file replace as rollback | Incorrect. |
| Treating stale-base rejection as rollback | Incorrect. |
| Claiming every stale-base rejection leaves the decision unconsumed / `Published` | Incorrect. Only **pre-consume** stale detection preserves `Published`. **Post-consume** apply/command races can fail after the decision is already `Consumed`. |
| Claiming every terminal broker result is audited and Townhall-projected | Incorrect. Pre-admission/early-return denials and `UnavailableAgentActionBroker` are backend-visible only. |
| Claiming `AgentActionFactPayload` / `AgentActionAuditRecord` carry actor attribution | Incorrect. Actor ids exist on request/dialog only; audit facts correlate session/run/backend/action without actor fields. |
| Treating Townhall system-notification author as stored actor attribution | Incorrect; author is conversation/catalog inference, not an audit fact actor id. |
| Treating “Scope: this exact request only” as selectable approval scope | Incorrect; fixed label only. |
| Treating command denylist + cwd checks as network/Git/secrets sandbox | Incorrect. |
| Treating unit-green Phase 17 suites as user-wired proof | Incorrect; tests use doubles and do not bind production UI backends. |

No issue/deferred files were edited. Relationships only:

| Artifact | Relationship |
|----------|--------------|
| [DF-008](../../../deferred/open/DF-008-multiple-agent-connections.md) | Confirmed blocker for default tool/permission journey entry (no bind UI) |
| [DF-009](../../../deferred/open/DF-009-real-acp-integrations.md) | Confirmed; ACP stack exists but user bind + external smoke remain open |
| [ISSUE-008](../../../issues/open/ISSUE-008-agent-response-not-showing.md) | Unbound/rejection projection still masks many pre-admission failures; admitted action results are a different path |
| [ISSUE-009](../../../issues/closed/ISSUE-009-production-di-test-contaminates-conversation-store.md) | A3 disposable-profile isolation still required |
| Prior [A2_AGENT_SEND](./A2_AGENT_SEND.md) | Backend-binding findings reused; not re-litigated as a send-path verdict |

---

## 14. A3 disposable-profile constraints (describe only; not executed)

If A3 later smokes `A1-TP-01`–`A1-TP-03`, constraints from this wiring audit:

1. **Disposable isolated profile only** — temporary `XDG_CONFIG_HOME` (or equivalent); never the real user profile, conversation store, or durable agent partitions ([AUDIT_PLAN.md §3](../AUDIT_PLAN.md#3-safety-and-isolation-rules-mandatory-for-a0a4)).
2. **Disposable workspace only** — scratch folder with harmless files; never the Zaide repository working tree as a mutation target.
3. **Backend bind prerequisite** — default production still has no bind UI. A3 may use an **explicitly authorized temporary bind hook** solely inside the disposable profile, or remain limited to negative-path observations (unbound / unavailable broker). Document the hook; do not claim it is a shipped user workflow.
4. **Harmless operations only** — create/replace/delete only disposable text files; execute only harmless commands (e.g. `true` / `echo`); never package managers, real Git mutation, external network exfil, credential files, or privilege helpers.
5. **Cases to cover when bind+workspace exist:**
   - read auto-allow (no dialog)
   - write allow / deny / dismiss
   - denylisted command denied without review (`sudo`/`bash` basenames)
   - allowed command review + deny/allow
   - decision expiry (5 minutes; may be time-accelerated only if a test seam exists — otherwise document as deferred)
   - revocation via cancel/end/workspace switch
   - **pre-consume stale-base:** edit file while dialog is open, then Allow → expect `Revoked` / `StaleBaseRevision`, decision still unconsumed/`Published`, no mutation
   - **post-consume/apply race:** only exercise if an authorized deterministic seam exists; expect no write after consume on apply conflict; decision already `Consumed`
   - workspace-generation: close/switch folder while review pending
   - concurrent second action rejected on same run (backend-only vs projected visibility as observed)
6. **Unauthorized-effect verification** — after deny/dismiss/revoke/pre-consume-stale/post-consume-conflict/unavailable, assert target file bytes and process side effects did not change.
7. **Visibility distinction** — record both Townhall conversation text and backend-visible tool/JSON-RPC result; they are not the same surface. Explicitly distinguish **backend-only early denials** from Townhall-projected `ActionResultReported`.
8. **Actor-attribution distinction** — note actor ids shown in the permission dialog versus their **absence** from stored `AgentActionFactPayload` / `AgentActionAuditRecord` fields; do not treat Townhall author inference as audit actor attribution.
9. **ACP distinction** — automatic reject-preferring `session/request_permission` protocol response (not guaranteed fail-closed; not Phase 17 broker authorization) versus the separate Phase 17 write/command review dialog for broker-mediated `fs/*` mutations. If an authorized candidate/hook exists, inspect two separate ACP permission cases (**ACP protocol behavior only** — not Phase 17 broker authorization or actual workspace mutation permission):
   - options contain `reject_once` → default chooses it
   - options omit `reject_once` and first option is permissive → default chooses the first permissive option
10. **No multi-file rollback scenario** — source proves absence; A3 should not invent a rollback UI expectation. Optionally confirm absence only.
11. **Cleanup** — remove disposable profile and workspace; never touch real profile or repository state.
12. Do not treat Phase 17 unit suites as A3 substitutes.

Do **not** execute A3 in this session.

---

## 15. Exact next recommended A2 slice, explicitly not started

**Next recommended A2 slice (not begun in this session):**
`A2_AGENT_CREATION_AND_BACKEND_ONBOARDING`

**Suggested goal rows:**

- `A1-AC-01` — historical Agent Panel creation path
- `A1-AC-02` — Native Harness / ACP backend binding and capability honesty
- related scoped disposition `A1-XX-01` (not a user-goal verdict)

**Why this next:** tools/permissions are now classified as composed-but-entry-blocked; the binding/onboarding journey is the direct prerequisite that turns the TP substrate into a default-user product path. Strong alternatives: first-launch/settings (`A1-FL-*`), Townhall-only (`A1-TH-*`), or IDE journeys (editor/LSP/build/debug/git).

**Explicitly not started here:** that slice, A3, A4, stabilization, V4, corrective implementation, or any other A2 evidence file.

---

## 16. Corroborating tests (non-proof)

| Area | Representative tests | Prove | Do **not** prove |
|------|----------------------|-------|------------------|
| Permission lifecycle | `Phase17Permission*` | Allow/deny/dismiss/cancel/expiry contracts | Default cold-profile user journey |
| Proposals / stale base | `Phase17Proposal*` | Fingerprint/base binding; pre-consume vs apply race coverage under fakes | Live editor races with real UI; post-consume live seam not proven here |
| Mutation | `Phase17Workspace*` / M5 evidence suite | Containment, atomic replace, cancel-before-open | Multi-file rollback (absent) |
| Commands | `Phase17CommandExecution*` | Denylist, budgets, process-tree cancel | Network/Git sandbox claims |
| Session integration | `Phase17SessionEventIntegration*` | Broker creation, revoke, Townhall terminal projection under fakes for admitted lifecycle paths | Production backend bind UI; not all early broker returns are projected |
| Bypass ratchets | `Phase17BypassRatchet*` / adversarial closeout | Ownership boundaries | Discoverability of permission UX |
| Native Harness / ACP unit suites | Phase 19/20 tests | Tool/fs mapping through broker under test doubles | External provider/candidate smoke |

---

## 17. Verification and working-tree closeout

### Pre-closeout checks

| Check | Expected / result |
|-------|-------------------|
| Exactly one new untracked file | `docs/audits/v1-v3-product-reality/evidence/A2_TOOLS_PERMISSIONS.md` |
| No tracked files modified | Clean aside from that untracked evidence file |
| Whitespace | `git diff --no-index --check /dev/null <evidence-file>` — no whitespace error lines |
| Markdown links resolve | Relative links under `evidence/` verified against repository paths used above |
| Fragment anchors used | Primary anchors: goal matrix §12; audit plan §2/§3 |
| Verdict table IDs exactly once | `A1-TP-01`, `A1-TP-02`, `A1-TP-03` |
| Verdict wording consistency | All three **Wired-with-gap** throughout |
| Final-authorization order | Documented exactly in §7 (`TryConsume` = final authorization step; pre- vs post-consume stale windows separated) |
| Actor ids in fact/audit types | Documented absent from `AgentActionFactPayload` / `AgentActionAuditRecord` |
| Terminal audit/projection claim | Documented as admitted-lifecycle-only; early returns backend-visible only |
| ACP permission choice user-reachable | Documented **no** (automatic reject-preferring only; not guaranteed fail-closed) |
| ACP default `reject_once` preference + first-option fallback | Documented (both paths; permissive fallback when `reject_once` absent) |
| Unqualified “ACP fail-closed” claim | Removed; live behavior recorded without inheriting historical label |
| No later A2 evidence file created | Only this new file under `evidence/` |

### Closeout verdicts (repeat)

| id | verdict |
|----|---------|
| `A1-TP-01` | **Wired-with-gap** |
| `A1-TP-02` | **Wired-with-gap** |
| `A1-TP-03` | **Wired-with-gap** |

**Stop for re-audit.** No next slice started. No commit or push.
