# Phase 22.3: Agent-Path Enablement — TOFIX

## Status

**M1 residual draft-snapshot correction pushed pending independent re-audit; M2 not authorized.**

Human M0 acceptance was recorded on 2026-08-03 after the independent GO audit.
Separate Phase 22.3 M1 implementation authorization was granted in the same
session. Independent M1 audit returned **NO-GO** (F1 wrong-conversation draft
loss; F2 inactive-channel route-status missing from cached presentation).
Corrective work closed F1/F2, then a residual F1 follow-on: trim-equivalent but
raw-different newer drafts were still cleared. That residual is closed; M1 is
**not accepted**. M2–M5, G5, A3 execution, and V4 have not started.

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
- [ ] M2 explicit live-session termination.
- [ ] M3 safe mediated action path and actor attribution.
- [ ] M4 workspace-owned interrupted-run projection and explicit re-send.
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

M1 remains unaccepted until independent re-audit. M2 not authorized.

## Remaining Open Product Gaps (post-M1)

- `IAgentSessionService.EndAsync` has no production caller. Registered
  continuity/management ViewModels are not a user entry point.
- ACP cancellation currently attempts `CancelPromptAsync` with the already-
  cancelled run token; no backend acknowledgement can be claimed.
- Continuity `Terminate` writes intent/acknowledgement evidence but does not call
  live `EndAsync` or a provider deletion/termination API.
- Phase 17 early denials can return before event/audit projection; action fact
  and audit payloads lack initiating/target actor IDs.
- Phase 17 has no product multi-file transaction, change set, or rollback
  operation.
- Phase 21 durable roots are process-CWD-derived in the live session/startup
  owners. Both sibling backends currently disallow resume; explicit re-send is
  required.
- `SessionEnding` / `SessionEnded` remain unprojected until M2.

## Next Task

Independent human M1 re-audit of the residual draft-snapshot correction. Do not
begin M2 until explicit M2 implementation authorization is recorded after M1
acceptance.
