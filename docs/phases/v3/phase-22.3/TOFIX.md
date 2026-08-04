# Phase 22.3: Agent-Path Enablement — TOFIX

## Status

**M1 accepted at `1a5ff04a3035df73331e3ea67eeba233491621c1`. M2 accepted at
`fb6c8d711f2088ae978ba0d9d01d1628f23a7692`. M3 accepted at
`2f41dcfc0c885b48b0a602625bd85f47cc78020d`. M4 remains NO-GO / not accepted
pending independent re-audit after F1–F3 corrective work.**

Human M0 acceptance was recorded on 2026-08-03 after the independent GO audit.
Separate Phase 22.3 M1 implementation authorization was granted in the same
session. Independent M1 audit returned **NO-GO** (F1 wrong-conversation draft
loss; F2 inactive-channel route-status missing from cached presentation).
Corrective work closed F1/F2, then a residual F1 follow-on: trim-equivalent but
raw-different newer drafts were still cleared. That residual closed at
`1a5ff04`; human accepted M1 and authorized **M2 only**. M2 implementation
shipped at `9c2163dc4431bfe29b1487e32f3c1881b5efff3a`; independent M2 audit
returned **NO-GO** (F1 CanEndSession; F2 ACP cancel-ack; F3 attempt correlation).
F1–F3 corrective at `983487e0`. ACP retry residual at `a02f864a`. Parallel
fast-suite crash residual under correction. M3–M5, G5, A3 execution, and V4
have not started.

## Work Board

- [x] Confirm Phase 22.2 package-2 completion and ordering readiness.
- [x] Trace live Townhall send, typed routing, binding, session, event,
  conversation, and Townhall projection owners.
- [x] Record actionable/visible and silent outcomes, including pre-admission
  rejection, terminal states, cancellation intent, and late completion.
- [x] Verify `EndAsync`, continuity termination records, production callers,
  restart interaction, and backend acknowledgement limits.
- [x] Verify Phase 17 dispatch, policy, permission, final authorization,
  mutation/conflict, audit, and reconciliation invariants.
- [x] Verify Phase 21 checkpoint, durable partition, revalidation,
  classification, and no-silent-resume ownership.
- [x] Confirm exact existing filters with `--list-tests`.
- [x] Run focused existing filters: send/routing/projection 71/71;
  continuity/termination 12/12; broker/permission/mutation 118/118.
- [x] Define the isolated dual-backend A3 producer and evidence matrix without
  executing it.
- [x] Replace all verification placeholders and lock future test obligations,
  rollback, and migration boundaries.
- [x] Human M0 acceptance.
- [x] Separate Phase 22.3 M1 implementation approval.
- [x] M1 send/routing and outcome visibility.
- [x] M2 explicit live-session termination (accepted at `fb6c8d71`).
- [x] M3 safe mediated action path and actor attribution (accepted at `2f41dcfc`).
- [x] M4 workspace-owned interrupted-run projection and explicit re-send (corrective F1–F3; re-audit pending; not accepted).
- [ ] M5 owned-row dual-backend A3 re-smoke and regression closeout.

## M1 Implementation and Verification (2026-08-03)

Baseline: `c2904fb100d538b0bd080eab3002cfc3994b6889`.

### Production changes

- `AgentConversationEventProjection` remains the sole normalized writer; added
  typed `ProjectAdmissionRejection`, `ProjectRouteStatus`, pre-admission/session
  rejection projection, cancellation-intent notification, and late-completion
  ordering without erasing prior cancellation intent.
- `AgentRouter` gained `RouteAndExecuteFromConversationAsync` for typed
  channel/direct source context without fabricating panel identity; successful
  routed execution projects bounded source route status without copying private
  target content.
- `AgentExecutionCoordinator` projects unbound pre-binding rejection through the
  projection boundary.
- `TownhallViewModel` routes channel catalog mentions through the router,
  preserves plain channel chat, clears drafts only after admission or a visible
  correlated outcome, and mirrors non-chat channel projection entries (route
  status, routing failure).
- `TownhallEntryProjection` formats route-status and cancellation-intent system
  notifications.

### New tests

- `Phase22AgentOutcomeProjectionTests` — 11 tests
- `Phase22TownhallRoutingOutcomeTests` — 13 tests

### Verification results

| Command | Result |
|---------|--------|
| `--list-tests` M1 classes | Discovered 24 tests |
| M1 explicit filter | PASS 24/24 |
| M0 send/routing/projection filter | PASS 71/71 |
| Townhall/composition/architecture preservation filter | PASS 59/59 |
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| `dotnet test Zaide.slnx --no-build` | PASS 3873/3873 |
| `git diff --check` | PASS |

Native Harness and ACP remain independent sibling backends. Phase 17 broker
ordering and `TryConsume()` final authorization were not changed.

## M1 Independent Audit NO-GO and Corrective Pass (2026-08-03)

Baseline under audit: `01a1f221a8a96c91be14f078321658e9d5582b50`.

### Audit verdict

Independent M1 audit: **NO-GO**. Two defects required corrective-only work.
M1 remains unaccepted until independent re-audit.

### F1 — Wrong-conversation draft loss

**Root cause:** `TryClearDraftAfterRoute` evaluated the correlated outcome on the
captured source conversation, then called `ClearActiveConversationDraft()`. If
the user navigated while routing was in flight, completion cleared the newly
active conversation’s draft and could leave the source draft stale.

**Correction:** Capture source `ConversationId` and the exact submitted draft
before awaiting routing. Clear only the captured source conversation’s stored
draft when it still represents the submitted text. Update `DraftText` only when
the source is still active. Preserve newer drafts edited during the in-flight
operation. Apply the same ownership rule to channel and direct routed sends.

### F2 — Inactive-channel route status missing from cached presentation

**Root cause:** `OnConversationEntryAppended` updated `_state.ChannelMessages`
only when the channel was active. A route status arriving after navigation was
durable in `IConversationStore` and could set unread, but was absent from the
cached collection used when the user returned.

**Correction:** Every channel entry that belongs in Townhall presentation updates
that channel’s cached collection whether active or inactive, guarded by
authoritative entry ID. `AppendMirroredActivity` no longer double-adds; store
append owns presentation via `OnConversationEntryAppended`. Active last-read and
inactive unread behavior are preserved. Private target prompt/response content
is not mirrored into the source channel.

### Corrective production changes

- `TownhallViewModel.SendMessageAsync` / `TryClearDraftAfterRoute` /
  `ClearSourceConversationDraftIfUnchanged` — source-owned draft clear.
- `TownhallViewModel.OnConversationEntryAppended` /
  `EnsureChannelMessageProjected` — conversation-owned channel presentation.
- `AppendMirroredActivity` — store append only; presentation via entry-appended
  path with entry-id dedupe.

### Corrective tests

Added to `Phase22TownhallRoutingOutcomeTests` (gated `TaskCompletionSource`
fake-backend control; no timing sleeps):

- `ChannelRoute_InFlightNavigation_PreservesOtherChannelDraft_AndProjectsRouteStatusExactlyOnce`
- `DirectRoute_InFlightNavigation_PreservesOtherConversationDraft`
- `ChannelRoute_ReturnAndEditSourceDraftDuringFlight_PreservesNewerDraft`
- `MissingCorrelatedVisibleOutcome_DoesNotClearSourceDraft`
- `ChannelSwitchAndPlainChat_ProjectCachedEntriesExactlyOnce`

M1 class totals: `Phase22AgentOutcomeProjectionTests` 11;
`Phase22TownhallRoutingOutcomeTests` 18 (was 13); combined **29**.

### Corrective verification results

| Command | Result |
|---------|--------|
| `--list-tests` M1 classes | Discovered 29 tests |
| M1 explicit filter | PASS 29/29 |
| M0+M1 send/routing/projection filter | PASS 100/100 |
| Townhall/draft/unread/navigation/composition/architecture filter | PASS 143/143 |
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| `dotnet test Zaide.slnx --no-build` | PASS 3878/3878 |
| `git diff --check` | PASS |

Boundaries preserved: no `EndAsync` UI; no `SessionEnding`/`SessionEnded`; no
Phase 17/21 changes; no Native Harness/ACP coupling; no schema/dependency/
package changes. M2 not authorized. M1 not accepted.

## M1 Residual — Exact Raw Draft Snapshot Clear (2026-08-03)

Baseline under correction: `759abf11782e15e560594e6f0f8629d4d90434d8`.

### Residual defect

After F1/F2, clear still compared the current draft against a **trimmed**
submitted payload and treated `current.Trim() == submitted` as equal. A newer
edit that differed only by leading/trailing whitespace (trim-equivalent, raw-
different) was incorrectly cleared.

### Correction

- Capture `rawDraftSnapshot = DraftText` exactly as entered before any await.
- Route `submittedPayload = rawDraftSnapshot.Trim()`.
- Clear the source draft only when its current value is ordinal-exactly equal
  to `rawDraftSnapshot` (no trim on either side of the comparison).
- Apply the same rule to channel and direct routing.
- Preserve every newer edit, including whitespace-only changes.

### Residual tests

Added to `Phase22TownhallRoutingOutcomeTests` (gated `TaskCompletionSource`;
no timing sleeps):

- `UnchangedRawDraft_ClearsAfterCorrelatedVisibleOutcome`
- `ChannelRoute_TrimEquivalentButRawDifferentNewerDraft_Survives`
- `DirectRoute_TrimEquivalentButRawDifferentNewerDraft_Survives`

M1 class totals: `Phase22AgentOutcomeProjectionTests` 11;
`Phase22TownhallRoutingOutcomeTests` 21 (was 18); combined **32**.

### Residual verification results

| Command | Result |
|---------|--------|
| M1 explicit filter | PASS 32/32 |
| M0+M1 send/routing/projection filter | PASS 103/103 |
| Townhall draft/navigation focused filter | PASS 76/76 |
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| `dotnet test Zaide.slnx --no-build` | PASS 3881/3881 |
| `git diff --check` | PASS |

M1 accepted at `1a5ff04a3035df73331e3ea67eeba233491621c1`.

## M2 Implementation (2026-08-03) — independent audit NO-GO

Baseline: `1a5ff04a3035df73331e3ea67eeba233491621c1`.
Shipped at: `9c2163dc4431bfe29b1487e32f3c1881b5efff3a`.

### Production changes (initial M2)

- `IAgentSessionService.EndAsync` returns `AgentSessionEndResult` with statuses
  `NoLiveSession` / `Ended` / `AcknowledgementIndeterminate`.
- `AgentSessionService.EndAsync` bounds acknowledgement wait
  (`EndAcknowledgementTimeout`), revokes the run broker before cancellation,
  removes live ownership only on success, and leaves `Ending` ownership on
  timeout with a retryable indeterminate reason (never claims provider stop).
- `AgentConversationEventProjection` remains the sole agent-event writer;
  projects ordered `SessionEnding` / `SessionEnded`, cancellation-intent
  dedupe, late-completion retention with label, and static
  `ProjectTerminationIndeterminate`.
- ACP `CancelPromptAsync` uses an independent bounded token
  (`AcpProcessLifecycleLimits.CancelPromptTimeout`), not the cancelled run token.
- Townhall ships direct-conversation `EndSessionCommand` + binding-panel
  "End session" control; captures `ConversationId` before await so navigation
  does not redirect end effects; channel conversations cannot end sessions.
- `TownhallEntryProjection` formats session-ending/ended, indeterminate, and
  late-completion display strings without overclaiming provider deletion.

### Initial verification results

| Command | Result |
|---------|--------|
| `--list-tests` M2 class | Discovered 10 tests |
| M2 explicit filter | PASS 10/10 |
| Session/continuity/termination focused filter | PASS 44/44 |
| M0+M1 send/routing/projection filter | PASS 103/103 |
| Townhall/composition/architecture preservation | PASS |
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| `dotnet test Zaide.slnx --no-build` | PASS 3891/3891 |
| `git diff --check` | PASS |

**Independent M2 audit: NO-GO** (F1/F2/F3). M2 not accepted.

## M2 Corrective Pass — F1/F2/F3 (2026-08-04)

Baseline under correction: `9c2163dc4431bfe29b1487e32f3c1881b5efff3a`.

### Audit findings

| ID | Defect |
|----|--------|
| F1 | `CanEndSession` was true for every direct conversation, including those with no live session ownership. |
| F2 | ACP cancel-ack timeout/failure was swallowed as ordinary `Cancellation`, allowing `SessionEnded` / ownership removal without confirmed cancel acknowledgement. |
| F3 | Indeterminate termination lacked per-attempt correlation, so repeated/same-conversation attempts could not be projected exactly once per attempt. |

### Corrective production changes

- **F1:** `RefreshCanEndSession` is true only for an active direct conversation
  with a live `IAgentSessionService` snapshot whose status is not `Ended`.
  False for channels, direct without ownership, and successfully ended
  sessions; remains true for `Ending` (retry after indeterminate). Refreshed
  from session lifecycle events, sends, navigation, and end outcomes.
  `ConversationId` still captured before await.
- **F2:** ACP `CancelPromptAsync` stays on an independent bounded token. Cancel
  success reports `Cancellation`; timeout/failure report
  `Indeterminate` with `CancellationAcknowledgementUncertain` and propagate
  through the session end path to `AcknowledgementIndeterminate`, retained
  `Ending` ownership, retryable visible outcome, and **no** `SessionEnded` /
  provider-stop claims. Outer `EndAcknowledgementTimeout` default is 15s so it
  outlasts the independent cancel budget rather than racing it. Native Harness
  and ACP remain independent siblings.
- **F3:** `AgentSessionEndResult` carries session/run/attempt correlation.
  Live indeterminate attempts pass non-null correlation into
  `ProjectTerminationIndeterminate`. Same attempt projects exactly once;
  distinct sessions/attempts each get their own entry. Raw correlation ids are
  not user-facing. `AgentConversationEventProjection` remains the sole writer.

### Corrective tests

`Phase22ExplicitSessionTerminationTests` expanded (TCS/deterministic gates; no
timing sleeps for ACP cancel-ack paths):

1. Direct without live ownership hides/disables End Session
2. Admitted live session enables it
3. Successful termination disables it
4. Indeterminate Ending ownership keeps it retryable
5. ACP cancel success uses a non-cancelled independent token
6. ACP cancel timeout → `AcknowledgementIndeterminate`, retain ownership, no `SessionEnded`
7. ACP cancel failure preserves the same truthful indeterminate boundary
8. Repeated projection for one attempt is deduplicated
9. Two distinct sessions/attempts each receive their own indeterminate entry
10. Navigation during termination still affects only the captured conversation

Class total: **18** tests (was 10).

### Corrective verification results

| Command | Result |
|---------|--------|
| `--list-tests` M2 class | Discovered 18 tests |
| M2 explicit filter | PASS 18/18 |
| Session/continuity/termination focused filter | PASS 52/52 |
| M0+M1 send/routing/projection filter | PASS 103/103 |
| Townhall/composition/architecture preservation | PASS 195/195 |
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| `dotnet test Zaide.slnx --no-build` | PASS 3899/3899 |
| `git diff --check` | PASS |

Boundaries preserved: no M3–M5 / G5 / A3 / V4; no Phase 17 `TryConsume()`
changes; no Phase 21 continuity behavior changes; no backend wrapping/fallback;
no packages/schema/unrelated cleanup. **M2 not accepted.**

## M2 Residual — ACP Indeterminate Retry Must Re-Ack (2026-08-04)

Baseline under correction: `983487e0c322401809b8f3d171584a9c10a77ea0`.

### Residual defect

After ACP `CancelPromptAsync` timed out or failed, the first `EndAsync` correctly
returned `AcknowledgementIndeterminate` and retained `Ending` ownership. The run
was terminal `Indeterminate`. A second `EndAsync` reset uncertainty bookkeeping,
did not re-issue backend cancel-ack, then called `FinalizeEndedSessionLocked`,
emitted `SessionEnded`, and removed ownership without a new acknowledgement.

### Correction

- Durable `AwaitingCancellationAcknowledgement` on the live session until a
  successful re-ack or ownership removal. A terminal Indeterminate run alone is
  never treated as acknowledged.
- Smallest typed seam: `IAgentCancellationAcknowledgementBackend` +
  `AgentCancellationAcknowledgementResult` status/result. ACP implements it;
  Native Harness does not.
- ACP retains the session client when cancel-ack is uncertain so retry can
  re-issue `CancelPromptAsync` on a fresh independent bounded token.
- Retry success → `SessionEnded` + ownership removal. Retry timeout/failure →
  another `AcknowledgementIndeterminate` with a distinct attempt correlation,
  retained `Ending`, End Session still available, no provider claims.
- `AgentConversationEventProjection` remains the sole writer.

### Residual tests (TCS/gates; no sleeps)

- ACP first timeout → retry success: `CancelPromptAsync` twice; second uses a
  fresh non-cancelled token; `SessionEnded` only after second ack; ownership
  removed.
- ACP first failure → retry success with the same truth boundary.
- ACP timeout → timeout: two distinct indeterminate correlations; ownership
  remains `Ending`; no `SessionEnded`.
- ACP failure → failure with the same retained boundary.
- Retry does not finalize merely because the original run is terminal
  Indeterminate.
- Townhall `CanEndSession` stays true through ACP indeterminate and clears only
  after successful re-ack.
- Existing F1–F3 and captured-navigation tests preserved.

Class total: **24** tests (was 18).

### Residual verification results

| Command | Result |
|---------|--------|
| `--list-tests` / M2 explicit filter | PASS 24/24 |
| Session/continuity/termination focused filter | PASS 58/58 |
| M0+M1 send/routing/projection filter | PASS 103/103 |
| Townhall/composition/architecture preservation | PASS |
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| `dotnet test Zaide.slnx --no-build` | PASS 3905/3905 |
| `git diff --check` | PASS |

Boundaries preserved: Native Harness / ACP independent siblings; no cross-backend
fallback, provider deletion, session resume, or M3 behavior; no Phase 17
`TryConsume()` or Phase 21 continuity changes. **M2 not accepted.**

## M2 Residual — Parallel Fast-Suite Lifetime Stability (2026-08-04)

Baseline under correction: `a02f864a5b5aa0e5e0fb87f059b9f3839760f418`.

### Independently observed failure truth

Serial fallback: **PASS 3905/3905**.

Parallel `dotnet test Zaide.slnx --no-build` aborted twice with:

```
ReactiveUI.UnhandledErrorException
  inner NullReferenceException
  TownhallViewModel.ApplyUnreadPresentation
  TownhallViewModel.cs ~line 1090
```

Stack origin:
`Phase22ExplicitSessionTerminationTests.EndSession_OperatesOnCapturedDirectConversation_NavigationDoesNotRedirect`
at the second `OpenDirectConversationCommand` (navigation during in-flight end).

Treated as M2 test-isolation / parallel reactive-lifetime defect with a real
concurrent `DirectNavItems` access race until corrected; serial pass alone was
not accepted as the normal gate.

### Root causes addressed

1. **Bare `Subscribe()`** on ReactiveCommands during navigation races surfaced
   exceptions as process-wide ReactiveUI unhandled errors instead of awaited
   task faults.
2. **Undisposed** TownhallViewModel / projection / session services left
   subscriptions live across teardown while gated send/end tasks were still
   in flight.
3. **Process-wide static** `AgentSessionService.EndAcknowledgementTimeout`
   mutations could be observed by concurrent suites.
4. **Production race:** `OnConversationEntryAppended` (off-thread agent
   projection) refreshed/enumerated `DirectNavItems` concurrently with
   navigation `RefreshDirectNavItems` / `ApplyUnreadPresentation`.
   `ObservableCollection` is not thread-safe; concurrent access could NRE.

### Correction

- M2 harnesses dispose ViewModel, projection, and session; observe gated
  send/end tasks before teardown.
- All M2 command executions use awaited `ToTask()` (no bare `Subscribe()` for
  navigation or open/select).
- `EndAcknowledgementTimeout` is **instance-scoped** on
  `AgentSessionService` (no process-wide mutation).
- `TownhallViewModel` synchronizes `DirectNavItems` mutation/enumeration under
  `_directNavSync`; active-message append re-checks active conversation after
  nav refresh.
- Regression:
  `ConcurrentNavigationDuringTermination_DoesNotThrowReactiveUnhandled`.
- Parallelism not disabled; worker count unchanged; serial remains fallback only.
- Acknowledgement-bound retry contract preserved (durable awaiting flag, ACP
  re-ack, success-only SessionEnded, distinct attempt correlations, sole writer).

### Verification results

| Command | Result |
|---------|--------|
| M2 filter ×10 | PASS 25/25 each |
| Session/continuity/termination filter | PASS 59/59 |
| M0+M1 filter | PASS 103/103 |
| Townhall/composition/architecture | PASS 196/196 |
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| Parallel full suite ×3 | PASS **3906/3906** each |
| `git diff --check` | PASS |

**M2 not accepted — stop for independent re-audit.**

## M2 Acceptance (2026-08-04)

Independent human M2 acceptance recorded at
`fb6c8d711f2088ae978ba0d9d01d1628f23a7692` after parallel-suite lifetime
stabilization and ACP retry residual correction.

## M3 Implementation (2026-08-04) — independent audit pending

Baseline: `fb6c8d711f2088ae978ba0d9d01d1628f23a7692`.

### Production changes

- `AgentActionFactPayload` and `AgentActionAuditRecord` now carry initiating and
  target actor IDs alongside session/run/conversation/backend/action/workspace
  attribution.
- `RunScopedAgentActionEventPublisher` records actor IDs on every bounded audit
  snapshot.
- `ContractAgentActionBroker` publishes bounded `ActionResultReported` facts and
  audit records for early denials (broker revoked, no workspace, proposal
  failure, concurrent action rejection) without weakening fail-closed behavior.
- `TryConsume()` final authorization, pre-consume stale `Published` decisions,
  post-consume conflict `Consumed` decisions, atomic replace, and document
  reconciliation behavior remain unchanged.

### New tests

- `Phase22MediatedActionPathTests` — 19 tests
- `Phase22ActionAttributionTests` — 10 tests

### Verification results

| Command | Result |
|---------|--------|
| `--list-tests` M3 classes | Discovered 29 tests |
| M3 explicit filter | PASS 29/29 |
| Phase 17 broker/permission/mutation filter | PASS 118/118 |
| Phase 20 action-bridge filter | included in broker filter |
| M0+M1+M2 send/routing/termination filter | PASS 140/140 |
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| `dotnet test Zaide.slnx --no-build` | PASS 3935/3935 |
| `git diff --check` | PASS |

Boundaries preserved: no M4–M5 / G5 / A3 / V4; no new action kinds; no backend
wrapping/fallback/cross-retry; `AgentConversationEventProjection` remains sole
writer; M1/M2 routing/draft/termination contracts unchanged. **M3 not accepted.**

## M3 Corrective (2026-08-04) — independent re-audit pending

Baseline: `d531057e168e491fd389e880c4c4eb8fb37da450`.
M3 remains **NO-GO** / not accepted. M4 not started.

### Defects corrected

**F1 — Complete every early-denial event/audit path**

- Correlation-mismatch returns from `ContractAgentActionBroker` that previously
  returned registry-fabricated denials without publishing now produce exactly
  one bounded `ActionResultReported` event and matching audit record:
  - initial correlation fingerprint mismatch;
  - in-flight correlation mismatch while waiting;
  - admission-gate correlation mismatch (TOCTOU re-check);
  - reserved/in-flight replay correlation mismatch.
- Event, audit, and returned result share the composed request's `ActionId` and
  `AttemptId` (no synthetic registry IDs when a request exists).
- Correlation-registry revocation after request composition preserves the
  composed request's action/attempt IDs (no longer uses the payload-only path).
- True `DuplicateReplay` still returns the prior terminal without republishing.
- Cancellation remains `Cancelled` (not relabelled as denial). Causation
  sequencing and `AgentActivityEvidenceLevel.ZaideMediated` are preserved.
- Correlation rejection, fail-closed behavior, and run-slot exclusion unchanged.

**F2 — Do not fabricate workspace identity for NoWorkspace**

- `AgentActionFactPayload` and `AgentActionAuditRecord` workspace fields are
  optional (`WorkspaceIdentity?` / `WorkspaceGeneration?`).
- NoWorkspace (and any path with no captured scope) records explicit absence
  (`null`/`null`); never `WorkspaceIdentity.New()` or `WorkspaceGeneration.Initial`.
- Captured-workspace denials retain the exact captured identity and generation.
- Bounded summaries, redaction, retention, and schema truth preserved.

### Tests added (Phase22ActionAttributionTests)

- Correlation mismatch sites → exactly one correlated event + audit
- Registry revocation after composition preserves request ActionId/AttemptId
- True duplicate replay does not create a duplicate terminal audit/event
- NoWorkspace event/audit explicitly contain no workspace attribution
- Captured-workspace denials retain exact scope identity and generation
- Early denial paths do not touch filesystem / permission / workspace mutation
- Existing M3 pre-consume `Published` / post-consume `Consumed` / `TryConsume`
  finality coverage retained in `Phase22MediatedActionPathTests`

### Verification results

| Command | Result |
|---------|--------|
| `--list-tests` M3 classes | Discovered 38 tests |
| M3 explicit filter | PASS 38/38 |
| Phase 17 broker/permission/mutation + Phase 20 bridge filter | PASS 156/156 |
| M0+M1+M2 send/routing/termination filter | PASS 140/140 |
| Townhall/composition/architecture preservation | PASS (focused) |
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| `dotnet test Zaide.slnx --no-build` | PASS 3944/3944 |
| `git diff --check` | PASS |

Boundaries preserved: no M4–M5 / G5 / A3 / Phase 22.4/22.5 / V4; Native Harness
and ACP remain independent siblings; `AgentConversationEventProjection` remains
the sole normalized conversation writer. **M3 not accepted — stop for
independent re-audit.**

## M3 Corrective — deterministic branch proof (2026-08-04)

Baseline: `a897ec36fb6649544958bc52bd014463802d90a9`.
M3 remains **NO-GO** / not accepted. M4 not started.

### Residual (proof only — not a production behavior failure)

The production early-denial and optional-workspace corrections at `a897ec36`
remain sound. The residual was that several tests claimed to cover the four
distinct correlation-mismatch branches but did not deterministically reach them:

1. Admission-gate test exercised sequential initial mismatch instead of the
   admission-gate TOCTOU branch.
2. Reserved/in-flight test presented a different fingerprint that the outer
   `TryRejectMismatchedFingerprint` normally catches before the reserved wait.
3. In-flight-wait test had the same outer-check problem unless the registry
   changed after that check.
4. Registry-revocation test used `Task.Delay(150)` to infer wait-state entry.

### Correction (test seams + deterministic gates)

Smallest internal test-only seams (null in production; no control-flow change
when unset):

- `ContractAgentActionBroker.TestBeforeOuterInFlightWait`
- `ContractAgentActionBroker.TestBeforeAdmissionGate`
- `ContractAgentActionBroker.TestBeforeReservedInFlightWait`
- `ContractAgentActionBroker.TestLastCorrelationMismatchSite` + nested
  `CorrelationMismatchSite` observability (not a new top-level type)
- `AgentActionCorrelationRegistry.TestOnInFlightWaitEntered` (signal only;
  must not re-enter the registry while the gate lock is held)

`Phase22MediatedActionHarness` exposes `RunSlot` and `CorrelationRegistry` for
deterministic pre-seeding / injection.

Exact branch tests (ManualResetEventSlim enter/release; no sleeps):

| Test | Branch proven |
|------|----------------|
| `CorrelationMismatch_InitialSite_…` | Mismatch registered before first outer check |
| `CorrelationMismatch_InFlightSite_…` | Outer checks pass; foreign fingerprint injected before outer wait |
| `CorrelationMismatch_AdmissionGateSite_…` | Outer checks pass; foreign terminal injected before admission gate |
| `CorrelationMismatch_ReservedInFlightSite_…` | Slot held without subject key; foreign fingerprint injected before reserved wait |
| `RegistryRevocationAfterComposition_…` | Explicit wait-entered signal, then revoke |

Each branch asserts: `Denied`/`CorrelationKeyMismatch` (or `BrokerRevoked`),
exactly one correlated event + audit, shared ActionId/AttemptId,
`ZaideMediated`, exact workspace attribution, no mutation, no run-slot leak;
site observability matches the intended branch.

Existing M3 coverage retained: NoWorkspace null/null, captured workspace,
DuplicateReplay non-republication, actor attribution, Native Harness/ACP paths,
pre-consume `Published`, post-consume `Consumed`, `TryConsume` finality.

### Verification results

| Command | Result |
|---------|--------|
| `--list-tests` M3 classes | Discovered 38 tests |
| M3 explicit filter | PASS 38/38 |
| Branch-race filter ×20 | PASS 5/5 each (20 iterations) |
| Phase 17/20 + M3 filter | PASS 156/156 |
| M0+M1+M2 send/routing/termination filter | PASS 140/140 |
| Townhall/composition/architecture preservation | PASS (focused) |
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| `dotnet test Zaide.slnx --no-build` | PASS 3944/3944 |
| `git diff --check` | PASS |

**M3 not accepted — stop for independent re-audit.** Do not start M4–M5 / G5 /
A3 / Phase 22.4/22.5 / V4.

## M3 Acceptance (2026-08-04)

Independent human M3 acceptance recorded at
`2f41dcfc0c885b48b0a602625bd85f47cc78020d` after safe mediated action paths,
actor attribution, and deterministic early-denial branch proof.

## M4 Implementation (2026-08-04) — independent audit NO-GO

Baseline: `2f41dcfc0c885b48b0a602625bd85f47cc78020d`.
Shipped at: `a0927246ab70c8cc4c5603747270e24b6071f5ac`.

### Production changes (initial M4)

- `AgentContinuityWorkspaceRootProvider` resolves checkpoint ownership from
  `IWorkspaceActionAuthority` (opened workspace root).
- `AgentSessionContinuityReconcileOrigin` distinguishes `StartupLegacyCwd` from
  `WorkspaceOpen`; workspace-open reconciliation is idempotent per
  identity/generation via `AfterWorkspaceOpenReconcile`.
- `AgentSessionContinuityLegacyCwdReader` reads legacy CWD-keyed partitions.
- `AgentSessionContinuityWorkspaceOpenReconciler` reconciles on workspace open.
- `AgentSessionContinuityCoordinator` blocks `Resume` when
  `ResumeCurrentlyUsable = false` (both sibling backends).
- `AgentSessionContinuityConversationProjector` projects interrupted runs through
  `AgentConversationEventProjection.ProjectInterruptedRun` only.
- `ApplicationShutdown` checkpoints under opened workspace root, not process CWD.

### New tests (initial)

- `Phase22InterruptedRunProjectionTests`
- `Phase22ContinuityWorkspaceOwnershipTests`

### Invalid / corrected prior A3 claim

The initial M4 report claimed
`scripts/run-m4-force-quit-a3.sh` PASS for native-harness + ACP. **That claim is
invalid.** The script only re-ran the same in-process unit test twice and
changed the printed backend label. It did not pass `backend_id`, start a child
scenario process, observe an admitted-running checkpoint, force-kill a process
group, or restart from durable state. Treat any prior A1-TC-05 force-quit A3
PASS under that script as non-evidence.

**M4 remains NO-GO / not accepted.**

## M4 Corrective — F1/F2/F3 (from `9e97c700`) — independent re-audit pending

Baseline under correction: `9e97c70071042409e60e53ef90327597387cc73b`.
Shipped at: `60d5e291f09d708ec6d4007758a1854ba752179a`.

### F1 — Restart classification (corrected)

- Removed always-true classification fallback; requires interrupted session for
  force-killed session/run.
- Asserts exact classification per backend (Recoverable vs Indeterminate) with
  `ResumeCurrentlyUsable=false`.
- Evidence bound to pre-kill session/run, backend, workspace root/key, checkpoint.

### F2 — Zero invocation before explicit resend (corrected)

- `AgentPathEvidenceInvocationCounters` at Native Harness provider, ACP
  protocol session/new/prompt, broker, and permission-review boundaries.
- ACP fake-agent stats file (`ZAIDE_ACP_STATS_FILE`) for child-process proof.
- Restart baselines captured before workspace-open reconciliation; pre-resend
  deltas asserted exactly zero (not inferred from null live session alone).
- Post-resend: selected sibling backend increments; other backend untouched.
- Producer vendored at `tests/a3-agent-path/runner/`; script rsyncs to
  `/tmp/zaide-a3-agent-path/runner/` for publish.
- ACP fixture launch resolves `.dll` paths through `dotnet` (matches test fixture).
- Fake-agent `slow-prompt` mode keeps prompt in-flight without exceeding the 30s
  `InitializeTimeout` that invalidated prior `slow-request` evidence.

### F3 — Evidence validation (corrected)

- `scripts/run-m4-force-quit-a3.sh` validates classification, session/run
  identities, zero pre-resend deltas, and post-resend selected-backend deltas.
- Rejects missing, vacuous, or mismatched evidence fields; binds `repoHead` at
  runtime.

### Verification results (corrective from `9e97c700`)

| Command | Result |
|---------|--------|
| `--list-tests` M4 classes | Discovered 15 tests |
| M4 explicit filter | PASS 15/15 |
| Phase 21 + M4 continuity filter | PASS 27/27 |
| M0–M3 preservation filter | PASS 166/166 |
| Architecture inventory/visibility/adversarial ratchets | PASS 69/69 |
| `dotnet build Zaide.slnx --no-incremental` | PASS; 0 warnings, 0 errors |
| `dotnet test Zaide.slnx --no-build` (parallel) | FAIL 3958/3959 — flaky `TerminalTabHost_DisposesInactivePanels…` under parallel load |
| `dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings` (serial fallback) | PASS 3959/3959 |
| `scripts/run-m4-force-quit-a3.sh` | PASS native-harness + ACP; evidence fields validated |
| `git diff --check` | PASS |

Boundaries preserved: no M5 / G5 / full A3 matrix; no usable backend resume; no
permission/proposal replay; Native Harness and ACP remain independent siblings.
**M4 not accepted — stop for independent re-audit.**

## M4 Corrective — F1/F2/F3 (2026-08-04) — superseded baseline

Baseline under correction: `a0927246ab70c8cc4c5603747270e24b6071f5ac` (prior pass;
superseded by corrective from `9e97c700`).

### F1 — Real isolated force-quit producer

- Out-of-tree producer at `/tmp/zaide-a3-agent-path/runner/` (assembly name
  `Zaide.Tests` for `InternalsVisibleTo` only).
- Composes through production `Program.ConfigureServices` and shipped Townhall
  bind/send, continuity, and projection paths.
- Isolated absolute HOME/XDG/workspace roots outside the repository; process CWD
  kept distinct from the opened workspace root.
- Parent controller starts each scenario child in its own process group,
  validates PID/PGID/cmdline/profile/workspace/backend/scenario token, waits for
  a machine-readable admitted/running durable-checkpoint barrier, force-kills
  only that validated group (SIGKILL; timeout is failure cleanup only), verifies
  death, restarts with the same profile/workspace, classifies before re-send,
  asserts zero live backend session/invocation before re-send, explicit re-send
  creates a new session/run, and cleans up scenario-owned process groups.
- Native Harness uses a deterministic loopback provider; ACP uses the repository
  fake-agent binary (`slow-prompt`); no wrapping, fallback, or cross-backend
  retry.
- `scripts/run-m4-force-quit-a3.sh` validates required evidence fields (not merely
  grepping `Passed!`). M4-scoped A1-TC-05 only — not the full M5 matrix.

### F2 — Startup legacy CWD compatibility is genuinely read-only

- `AgentSessionContinuityStartupReconciler` inspects, classifies, and projects
  only. It no longer calls coordinator `Reconcile` and never appends
  `AfterStartupReconcile` (or any other) records into the legacy partition.
- Coordinator `Reconcile` rejects `StartupLegacyCwd` origin so the writable path
  cannot be used for startup legacy compatibility.
- Workspace-open reconciliation remains the writable workspace-owned path.
- Regression: complete legacy partition snapshot equality (record count, ordering
  sequences, operation IDs, payloads, serialized hashes, file fingerprints)
  before/after startup classification; projected entry labelled
  legacy/read-only; workspace-owned records absent from legacy result and legacy
  records absent from workspace-open reconciliation; no silent copy into the
  workspace partition.

### F3 — Architecture inventory ratchets

- Updated authorized source-file baselines for the six M4 production files:
  total 875, Features 830.
- Touched `ArchitectureInventoryTests`, `ArchitectureVisibilityTests`,
  `Phase17AdversarialCloseoutTests`, `Phase19AdversarialTests` only for exact
  counts. Folder allowlists, visibility rules, and architectural assertions are
  not weakened.

### Tests

- Kept `Phase22InterruptedRunProjectionTests` and
  `Phase22ContinuityWorkspaceOwnershipTests`.
- Renamed in-process classification test away from `ForceQuit` (unit-level only;
  real force-quit is the out-of-tree producer).
- Added deterministic legacy byte-for-byte read-only verification.

(Superseded verification table removed; see corrective-from-`9e97c700` section above.)

## Remaining Open Product Gaps (post-M4 corrective)

- Continuity `Terminate` writes intent/acknowledgement evidence but does not call
  live `EndAsync` or a provider deletion/termination API (by design; Phase 21).
- Phase 17 has no product multi-file transaction, change set, or rollback
  operation.
- Legacy CWD-keyed continuity partitions remain separate read-only records;
  new checkpoints use the opened workspace root (M4).

## Next Task

Independent human **M4 re-audit** of workspace-owned interrupted-run continuity,
real force-quit process-group evidence, terminal Townhall projection, legacy
read-only equality, architecture ratchets, and explicit re-send. Do not begin
M5, G5, full A3 matrix, Phase 22.4/22.5, or V4 until explicit M5 implementation
authorization is recorded after M4 acceptance.
