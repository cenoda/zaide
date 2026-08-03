# Phase 22.3 M0 Live-Seam Verification

## Verdict and Authorization Boundary

**Recommendation: GO for human M0 acceptance.** The live seams, gaps,
implementation boundaries, exact existing filters, future test obligations,
isolated A3 procedure, migration limits, and rollback boundaries are specific
enough for an independent plan audit.

**M0 live-seam verification complete; human M0 acceptance pending; M1 not authorized.**

This is documentation-only M0 evidence at baseline
`b0de0d5b9c716720f719cc95a79c26243b39ead7`. No production code, tests, tools,
packages, schemas, or audit evidence was changed. Verification used the build
and existing automated tests only; no application runtime, provider,
credential, real profile, or real workspace was exercised. The future A3
producer was defined but not executed. Phase 22.3 M1–M5, Phase 22.4, Phase
22.5, G5, and V4 were not started.

## Baseline and Dependency

| Check | Result |
|-------|--------|
| Branch | `master` |
| `HEAD` | `b0de0d5b9c716720f719cc95a79c26243b39ead7` |
| `origin/master` | `b0de0d5b9c716720f719cc95a79c26243b39ead7` |
| Initial worktree | Clean; `master...origin/master` |
| Phase 22.2 | Complete; package-2 PASS restored |
| Ordering dependency | Satisfied |
| Phase 22.3 implementation | Not implemented or authorized |

Phase 22.2 completion makes this M0 eligible. It does not accept this M0 and
does not authorize implementation.

## Production Send and Routing Trace

### Owners and call path

```text
TownhallView input / SendMessageCommand
  -> TownhallViewModel.SendMessageAsync
     channel: LogActivity(UserChat) -> IConversationStore, then return
     direct: ensure thin AgentPanelState
       -> IAgentRouter.RouteAndExecuteAsync
          -> MentionParser + IActorCatalog.ListAgents
          -> source panel or target IAgentPanelHost panel
          -> IAgentExecutionCoordinator.SendAsync
             -> IAgentActorBackendBindingStore.GetRequiredBackendId
             -> IAgentSessionService.SendAsync
                -> selected independent IAgentBackend
                   NativeHarnessAgentBackend -> NativeHarnessLoopRunner
                   AcpActionCapableAgentBackend -> AcpAgentSessionAdapter
                -> AgentEventStream
                   -> AgentConversationEventProjection
                      -> IConversationStore.AppendEntry
                         -> TownhallViewModel.EntryAppended
                            -> TownhallEntryProjection -> Messages/navigation
```

`Program.CreateAgentExecutionCoordinator` eagerly resolves
`AgentConversationEventProjection` before constructing the coordinator.
Production DI registers Native Harness and ACP as separate `IAgentBackend`
singletons. `AgentActorBackendBindingStore` selects exactly one backend ID for
the target actor. There is no fallback or wrapping path.

### Outcome visibility and ownership

| Outcome | Live producer/result | Conversation ownership | Visible/actionable now |
|---------|----------------------|------------------------|------------------------|
| Empty/whitespace draft | `TownhallViewModel` returns before routing | None | Draft retained; no action needed |
| Channel plain message or mention | `LogActivity(UserChat)` and immediate return | Source channel | Visible as chat, but mention is not routed; `A1-MR-03` channel scope is open |
| Unknown/ambiguous/multiple/empty mention target | `AgentRouter.ProjectRoutingFailure` | Source direct conversation | Yes: `RoutingFailure` -> Townhall `AgentError`; draft clears |
| Missing source panel | Structured route failure without a known source conversation | None | No entry; draft retained; production race/error gap |
| No actor backend binding | Coordinator returns `Rejected` before session call | Attempted direct/target conversation has no entry | No: draft clears because the route itself succeeded; only pre-existing binding status is visible |
| Unregistered/mismatched backend or session admission rejection | `RunRejected` plus `FailureReported` from `AgentSessionService` | Event carries attempted conversation | No: projection deliberately skips `RunRejected` and suppresses non-admitted terminal failure |
| Admitted | `UserMessageAdmitted` | Direct attempt or routed target direct | Yes: correlated `UserChat`; target becomes unread when inactive |
| Accepted/running | run lifecycle events; coordinator panel busy `Thinking` | Owning execution conversation/panel | Transient status only; no conversation entry. There is no `Queued` state in `AgentRunStatus` |
| Completed | `AssistantMessageCompleted` then `RunCompleted` | Owning execution conversation | Yes: one correlated `AssistantResponse`; `RunCompleted` itself has no row |
| Failed | `FailureReported` then `RunFailed` | Owning execution conversation | Yes after admission: one `ExecutionFailure`; exact backend reason wins |
| Cancelled | `FailureReported` or `RunCancelled` fallback | Owning execution conversation | Terminal cancellation is visible after admission; cancellation intent/ack is not separately visible |
| Timed out | `FailureReported` / `RunTimedOut` | Owning execution conversation | Yes after admission: exact reason or `Request timed out.` |
| Disconnected | `FailureReported` / `RunDisconnected` | Owning execution conversation | Yes after admission: exact reason or `Connection was lost.` |
| Indeterminate | `FailureReported` / `RunIndeterminate` | Owning execution conversation | Yes after admission: exact reason or `Request ended indeterminately.` |
| Late completion after cancellation intent | state machine allows `CancellationRequested -> Completed` | Owning execution conversation | Assistant response is retained and completion wins truthfully; prior cancellation intent is not projected or durable as a separate user fact |

The only normalized event-to-conversation writer is
`AgentConversationEventProjection`. `TownhallViewModel` writes user-authored
channel activity only and projects `IConversationStore.EntryAppended` into the
active chat. `AgentPanelOutputHistoryProjection` is a read-only compatibility
projection, not a writer.

### Routed-flow discoverability

For `@Beta body` sent from a Human↔Alpha direct conversation, the target
Human↔Beta direct owns admitted user/assistant/terminal entries. The source
stays selected and receives no success/status entry; only the target nav row can
become unread. This preserves private target history but is not full active-flow
visibility for `A1-TH-05`. M1 must add bounded source route status/navigation,
not copy private assistant content.

## Explicit Live Termination Seams

### `EndAsync` behavior

`IAgentSessionService.EndAsync` / `AgentSessionService.EndAsync`:

1. returns when no live session exists;
2. transitions the session to `Ending` and emits `SessionEnding`;
3. transitions an active nonterminal run to `CancellationRequested`, emits the
   lifecycle event, revokes the run broker, and cancels its linked token;
4. waits for the backend observer task using the caller's cancellation token;
5. terminalizes any still-active run as cancelled, emits `SessionEnded`,
   removes the conversation-owned session, and permits a later send to create a
   new session without resume.

The broker is revoked before cancellation, so pending permission cannot retain
authority. Conversation history is not deleted.

### Reachability and truth gaps

- Production source has no caller of `EndAsync`; all current call sites are
  tests. DI registration is not user reachability.
- `AgentSessionContinuityInspectionViewModel.Terminate` and
  `AgentTransparencyManagementViewModel` are registered but have no View,
  command, or Townhall consumer. They are not user entry points.
- `AgentConversationEventProjection` ignores `SessionEnding`, `SessionEnded`,
  and `RunCancellationRequested`, so intent, acknowledgement, and ownership
  removal are not visible.
- Native Harness cooperatively observes the run token. This is local harness
  cancellation, not proof that an external provider stopped work.
- ACP catches run cancellation and calls `CancelPromptAsync` with the same
  already-cancelled token. No cancel request/acknowledgement can be assumed;
  the adapter then reports local cancellation and disposes the client/process.
- If a backend ignores cancellation indefinitely, `EndAsync` waits until its
  caller token expires. The session can remain `Ending`; no provider-state claim
  is justified. M2 needs bounded, retryable, explicit indeterminate handling.
- A late backend completion after cancellation intent is allowed by the state
  machine and is preserved as completion. The missing separate intent
  projection is an M2 obligation.

### Phase 21 termination records are distinct

`AgentSessionContinuityCoordinator.Terminate` records local termination intent,
local-process/best-available backend acknowledgement, and terminal
classification. Both current adapters report termination acknowledgement as
unsupported/unavailable. This operation does not call live `EndAsync`, send a
provider delete/close request, or kill a live backend process. It must remain a
durable interrupted-session operation, not be presented as live termination or
provider deletion.

## Phase 17 Broker and Permission Invariants

### Backend dispatch and broker creation

- `AgentSessionService` creates a run-scoped `ContractAgentActionBroker` only
  when the selected backend implements `IAgentActionRequestCapableBackend` and
  the production factory/audit store exist; otherwise it supplies
  `UnavailableAgentActionBroker`.
- Native Harness maps model tool descriptors to the five Phase 17 payload kinds
  and calls `IAgentActionBroker.RequestAsync`.
- ACP advertises its filesystem bridge only after negotiation and routes
  `fs/read_text_file` and `fs/write_text_file` through `AcpClientActionBridge`.
  ACP does not mediate delete or command and must not claim those capabilities.

### Authorization order

The live protected mutation order is:

1. validate broker authority, payload, workspace, correlation, and one-action
   run-slot admission;
2. compose the exact request and immutable file proposal;
3. publish requested/classified facts and evaluate locked policy;
4. show `PermissionReviewDialog` through
   `InteractiveAgentPermissionReviewService` for writes/commands;
5. validate decision status, exact request/proposal fingerprint, expiry,
   workspace generation, user allow, and current base revision;
6. if the base is already stale, return `Revoked/StaleBaseRevision` while the
   decision remains `Published` and perform no mutation;
7. call `AgentPermissionDecision.TryConsume()` as the final authorization step;
8. revalidate workspace/fingerprint/base at execution time, then read/mutate or
   start the command;
9. publish result and optional document reconciliation facts.

`TryConsume()` is the final authorization step, not the final safety check. A
race after consumption can return `Conflict/StaleBaseRevision` with the decision
already `Consumed` and no write. M1–M5 must preserve both windows exactly.

### Permission UI, execution, audit, and reconciliation

- `App.axaml.cs` attaches `PermissionReviewDialogPresenter` to the owned main
  window. Without an owner, permission fails closed as unavailable.
- Reads are `AllowedByLockedPolicy`; create/replace/delete/command require a
  user decision. Shell/privilege denylist checks occur before permission.
- `WorkspaceFileReader` and `WorkspaceFileMutator` enforce captured root,
  containment, regular-file, byte/text, and revision rules. Mutation uses a
  same-directory temp and atomic replacement where supported.
- `WorkspaceEditorDocumentReconciler` reloads clean open documents after a
  confirmed mutation and preserves dirty buffers as an external conflict.
- `RunScopedAgentActionEventPublisher` attributes facts to session, run,
  conversation, backend, action, attempt, workspace identity/generation,
  sequence, causation, and evidence level. The bounded in-memory audit store
  lacks explicit initiating/target actor IDs; request/dialog objects do carry
  them.
- Several pre-reservation/early denials return only to the backend and never
  publish `ActionResultReported` or an audit record. Townhall projects only a
  published terminal action result, not the full lifecycle.
- There is no multi-file transaction, change set, user rollback command, or
  rollback UI. Stale rejection, atomic replace, and document reconciliation are
  not rollback and must not be described as such.

## Phase 21 Continuity Ownership

| Concern | Production owner | Live truth |
|---------|------------------|------------|
| Durable append | `AgentSessionContinuityCheckpointWriter` -> `IAgentDurableRecordStore` | Session-recovery records are append-only/idempotent per phase/scope key |
| Live checkpoint creation | `AgentSessionService.RecordContinuityCheckpointLocked` plus `AgentSessionContinuityEventSubscriber` | Before-session/run and terminal lifecycle records; event subscriber failures are isolated |
| Shutdown checkpoint | `ApplicationShutdown` -> `CheckpointActiveSessions` | Uses `Environment.CurrentDirectory`; clean shutdown differs from force kill |
| Startup classification | `AgentSessionContinuityStartupReconciler` -> coordinator/revalidator | One-shot classify and append; never calls `Resume` |
| Binding/backend revalidation | `AgentSessionContinuityRevalidator` + sibling adapters | Durable binding, backend ID, fingerprint, workspace root, adapter, and capability must match |
| Resume capability | Capability matrix | Native Harness and ACP: checkpoint supported, resume currently unusable |
| Terminal projection | `AgentConversationEventProjection` | Run terminals visible only for admitted live events; reconciled interrupted classification is not projected |

Phase 22.2 now loads durable actor/backend bindings at store construction, so a
matching persisted binding can participate in restart classification. However,
`AgentSessionService`, startup reconcile, and shutdown checkpoint still default
to process CWD rather than the opened workspace authority. A production
workspace-open reconcile seam is absent.

Reconciliation must never invoke a backend run. A recoverable classification
does not mean resume is usable. The user must explicitly re-send, creating a new
run and re-proposing/re-authorizing every material action. No prior permission
decision, correlation terminal result, or side effect may be replayed.

## Existing Test Inventory and M0 Results

The complete list was queried with:

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --list-tests
```

The following exact filters were confirmed to discover tests and were run:

| Area | Existing classes | M0 result |
|------|------------------|-----------|
| Send/routing/projection | `AgentRouterTests`, `AgentExecutionCoordinatorTests`, `AgentConversationEventProjectionTests`, `AgentSessionServiceTests`, `TownhallDirectSendTests`, `Phase19TownhallProjectionTests` | PASS, 71/71 |
| Continuity/termination | `Phase21RestartTests`, `Phase21RecoveryTests`, `Phase21TerminationTests` | PASS, 12/12 |
| Broker/permission/mutation | `Phase17PermissionLifecycleTests`, `Phase17PermissionReviewServiceTests`, `Phase17ProposalBrokerTests`, `Phase17WorkspaceMutationBrokerTests`, `Phase17WorkspaceReadBrokerTests`, `Phase17SessionEventIntegrationTests`, `Phase17DocumentReconciliationTests`, `Phase17WorkspaceMutationMutatorTests`, `Phase20ActionBridgeTests` | PASS, 118/118 |

The exact runnable commands are owned by the implementation plan. Existing
tests prove the current seams and preservation invariants; they do not prove
the missing user entry points or future A3 behavior.

### M0 verification results

| Command | Result |
|---------|--------|
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| Send/routing/projection focused filter | PASS; 71/71 |
| Continuity/termination focused filter | PASS; 12/12 |
| Broker/permission/mutation focused filter | PASS; 118/118 |
| `dotnet test Zaide.slnx --no-build` | PASS; 3849/3849 |
| `dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings` | Not required; the interactive fast suite passed |
| `git diff --check` | PASS |

## Future Test Obligations

| Milestone | Required new class | Minimum obligations |
|-----------|--------------------|---------------------|
| M1 | `Phase22AgentOutcomeProjectionTests` | Pre-binding and session rejection produce one correlated actionable entry; accepted/running transient truth; failed/cancelled/timed-out/disconnected/indeterminate exact labels; cancellation-intent plus late-completion ordering; no duplicate writer |
| M1 | `Phase22TownhallRoutingOutcomeTests` | Direct and channel catalog mention; no open target panel required; source routing failure; target authoritative history; bounded source route status/navigation; no private response copy |
| M2 | `Phase22ExplicitSessionTerminationTests` | Shipped command reachability; intent; bounded ACP/Native cancellation acknowledgement; terminal projection; ownership removal; timeout/indeterminate retry; late completion retained; no provider deletion claim |
| M3 | `Phase22MediatedActionPathTests` | Independent Native/ACP dispatch through broker; permission allow/deny/dismiss; safe read/create/replace; pre-consume stale remains `Published`; post-consume conflict is `Consumed`; reconciliation; no bypass/fallback |
| M3 | `Phase22ActionAttributionTests` | Initiating/target actor plus session/run/conversation/backend/action/workspace attribution; early denials produce bounded audit/event results; redaction and retention preserved |
| M4 | `Phase22InterruptedRunProjectionTests` | Force-quit checkpoint, restart classification, Townhall terminal/interrupted row, explicit re-send, no backend call before re-send, no permission replay |
| M4 | `Phase22ContinuityWorkspaceOwnershipTests` | Opened-workspace root ownership, legacy CWD separation, binding mismatch, workspace mismatch, both sibling adapters, no silent record migration/deletion |

## Future Isolated A3 Procedure

### Isolation and construction

The M4/M5 producer must:

1. live outside the repository at `/tmp/zaide-a3-agent-path/` and use assembly
   name `Zaide.Tests` only for existing `InternalsVisibleTo` access;
2. create absolute scenario-local `HOME`, `XDG_CONFIG_HOME`, `XDG_DATA_HOME`,
   `XDG_STATE_HOME`, and `XDG_CACHE_HOME` roots before production composition;
3. create a disposable workspace outside `/home/cenoda/zaide`, set process CWD
   to that workspace before composition, and open the same root through shipped
   workspace controls;
4. compose through `Program.ConfigureServices` and use the shipped Townhall
   binding, send, routing, permission, and termination controls;
5. bind Native Harness and ACP in separate scenario processes. Native uses a
   loopback deterministic provider with a non-secret fixture value only; ACP
   uses the repository fake-agent binary. Neither may wrap or fall back to the
   other;
6. extend the fake providers with deterministic modes that request only safe
   reads/writes inside the disposable workspace, block at named lifecycle
   barriers, emit timeout/disconnect/indeterminate/late-completion fixtures, and
   never contact an external service;
7. run permission allow/deny/dismiss, safe mutation, pre-consume stale,
   post-consume conflict, dirty/clean document reconciliation, and explicit
   rollback-absence observations;
8. use a parent controller process for `A1-TC-05`: start a child, wait for an
   admitted-running durable checkpoint, force-kill the complete child process
   group, restart with the same isolated profile/workspace, and observe
   classification before any explicit re-send;
9. apply bounded startup, dialog, provider, action, termination, and cleanup
   timeouts. On any failure, terminate the whole child tree, retain evidence,
   then remove disposable runtime state;
10. never read or mutate the real user profile, use real credentials, invoke an
    external provider, or use the Zaide repository as a runtime fixture.

### Exact expected evidence

Each backend gets one machine-readable evidence file with repo HEAD, backend
ID, binding fingerprint/revision, scenario IDs, step timestamps, conversation
IDs, run/session/action IDs, event sequences, permission decision status,
workspace revisions, process IDs, cleanup result, and assertion counts. Secret
or proposed-content fields remain redacted/bounded.

| Goal | Required positive/negative evidence |
|------|-------------------------------------|
| `A1-AS-02` | Shipped bind -> direct send -> admitted user row -> response or exact actionable terminal row. Separately prove pre-admission reject, failure, cancel intent/ack, timeout, disconnect, indeterminate, and late completion without duplicate terminal projection. |
| `A1-TH-05` | Unknown target error in source; valid route status in active source; authoritative admitted/terminal entries in target; target navigation/unread works; no private assistant content copied to source/channel. |
| `A1-MR-03` | Direct and channel valid catalog mention resolve by `ActorId` with no pre-opened target panel; stripped body executes once against the selected bound sibling backend. |
| `A1-TP-01` | Backend-originated safe action reaches capability/policy/broker, shipped permission UI when required, executor, audit/event stream, and Townhall result with session/run/conversation/backend/action/actor attribution. |
| `A1-TP-02` | Read auto-policy plus write allow/deny/dismiss/expiry/revocation; unknown/unavailable fails closed; decision authorizes one exact request; no persistent grant. |
| `A1-TP-03` | Successful disposable mutation; pre-consume stale = `Revoked/StaleBaseRevision` and decision `Published`; post-consume race = no write and `Conflict/StaleBaseRevision` with decision `Consumed`; clean/dirty reconciliation; explicit evidence that no product rollback/change-set operation exists. |
| `A1-TC-05` | Admitted run checkpoint -> force-kill -> restart -> visible interrupted terminal/indeterminate classification; zero backend/action invocation before user re-send; prior permission not replayed; new explicit re-send creates a new run. |
| `A1-TC-09` | Shipped End command -> visible local intent -> cancellation/ack state -> terminal/indeterminate result -> live ownership removed or retryably retained; late completion preserved; Native/ACP provider termination/deletion never overclaimed. |

## Rollback, Migration, and Stop Boundaries

- M1–M4 are independently revertible. Revert the owning commit only.
- Preserve one conversation writer, Phase 17 broker ordering, Phase 21 durable
  records, Phase 22.2 bindings, and sibling-backend independence through every
  rollback.
- No schema or destructive record migration is approved at M0. Legacy CWD and
  new workspace-owned partitions must remain distinguishable and recoverable.
- Stop implementation for a required schema change, a second writer, a
  `TryConsume()` reorder, permission replay, cross-backend fallback, provider-
  state overclaim, real profile/credential/provider use, or a design choice
  that changes the locked milestone boundary.

## Unresolved Decisions and Limitations

These do not block a truthful M0 plan because the safety/product disposition is
locked:

- Exact visual copy and placement of route/termination status rows are M1/M2
  presentation details; ownership, accessibility, actionability, and evidence
  semantics are fixed above.
- `Queued` is absent from the live run model and will be reported as unsupported,
  not invented during Phase 22.3.
- Product rollback/change sets remain absent and must keep `A1-TP-03` below a
  full rollback claim. Phase 22.3 will smoke and classify that limitation, not
  add a new tool category.
- Backend resume remains unusable for both siblings. M4 projects classification
  and requires a new explicit send; it does not expose the dead `Resume` seam.
- Provider-side termination/deletion cannot be proven by local cancellation.
  M2 exposes the acknowledgement boundary rather than upgrading the claim.

No unresolved decision currently requires changing the M1–M5 boundaries. The
technical M0 recommendation is therefore **GO for human M0 acceptance**, not
implementation approval.
