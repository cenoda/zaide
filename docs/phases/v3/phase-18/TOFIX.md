# Phase 18: Live IDE Context for Agent Runs — TOFIX

## Status

M0 accepted. M1 implementation landed, failed NO-GO review, and the corrective pass
is complete as of 2026-07-26. M2 policy evaluation and context assembly landed
2026-07-26 and is complete. M3 run integration and consumption boundary landed
2026-07-26, including production DI wiring, integration-test proof, and the M3
publisher corrective pass connecting Editor and Source Control presentation to
passive snapshot services. M4 disclosure event and indicator implemented with corrective
pass as of 2026-07-26. M5 session policy override and minimal UI complete as of
2026-07-27. M6 closeout complete as of 2026-07-27.

## Current work

- [x] M0 review and acceptance by user.
- [x] M1 initial delivery.
- [x] M1 corrective pass for NO-GO findings (2026-07-26).
- [x] M2 policy evaluation and context assembly service (2026-07-26).
- [x] M3 run integration and consumption boundary (2026-07-26).
- [x] M3 corrective pass for production DI wiring and integration proof (2026-07-26).
- [x] M3 publisher corrective pass for Editor and Source Control snapshots (2026-07-26).

## M3 publisher corrective delivery (2026-07-26) - COMPLETE

Production:

- **Editor snapshot publisher:** `IEditorStateSnapshotPublisher` / `EditorStateSnapshotService.TryPublish`
  assigns monotonic generations, defensively copies published state, ignores stale
  updates and post-disposal publication. `EditorTabViewModel` publishes from existing
  tab/document lifecycle signals (open/close, active tab, content/dirty/caret/selection).
- **Source Control snapshot publisher:** `ISourceControlSnapshotPublisher` /
  `SourceControlSnapshotService.TryPublish` with `SourceControlSnapshotMapper` projecting
  orchestrator refresh results into passive snapshots. `SourceControlViewModel` publishes
  after each `ApplyResult` (workspace open/close already flows through the existing
  shell refresh path; Agents never call `Refresh()`).
- **Read-only consumer boundary preserved:** Agents continue to consume
  `IEditorStateSnapshotService` / `ISourceControlSnapshotService` only.

Tests:

- `EditorStateSnapshotServiceTests` — initial empty snapshot, publish/WhenChanged,
  monotonic generation, defensive copy, stale rejection, disposal safety.
- `SourceControlSnapshotServiceTests` — initial `NoWorkspace`, publish/WhenChanged,
  nested defensive clone, monotonic generation, workspace reset, stale rejection,
  disposal safety.
- `Phase18RunIntegrationTests` — `LiveAgentContextSnapshotSources` observes published
  Editor/Source Control snapshots; Detailed-policy manifest includes published source data.
- Registration module tests updated for publisher DI aliases.

Verification (2026-07-26): `dotnet build Zaide.slnx --no-restore` succeeded;
Phase18 84/84; Architecture 37/37; full fast 3165/3165.
Architecture source-file baseline corrected to 580 total / 535 Features.

## M3 corrective delivery (2026-07-26) - COMPLETE

Production:

- **Production DI:** `AddZaideAgents` registers `AgentContextManifestBuilder` and
  `LiveAgentContextSnapshotSources` as `IAgentContextSnapshotSources`. Production
  `AgentSessionService` resolves with both context dependencies from the real
  composition root.
- **Snapshot service registration:** `AddZaideEditor` registers
  `IEditorStateSnapshotService` / `EditorStateSnapshotService`.
  `AddZaideSourceControl` registers `ISourceControlSnapshotService` /
  `SourceControlSnapshotService`. Passive owners start empty/unavailable until
  presentation publishes live state.
- **Assembly failure behavior:** `AgentSessionService` fail-closed on assembly
  exceptions — no manifest attached, safe `FailureReported` event with fixed
  reason `IDE context assembly failed.` (no raw snapshot or exception detail).
- **Legacy backend boundary preserved:** `LegacyOpenAiCompatibleAgentBackend`
  remains inert to `ContextManifest`.

Tests:

- `AgentsRegistrationModuleTests` — context services registered, production
  `AgentSessionService` resolves with usable context dependencies.
- `Phase18RunIntegrationTests` — run-to-manifest integration with deterministic
  snapshot sources, capturing backend, application-default policy path, rejected-run
  non-assembly, backend failure/cancellation lifecycle, assembly-failure behavior,
  and null-dependency guards.

## M3 delivery (2026-07-26) - COMPLETE

Production (`src/Features/Agents/`):

- **Backend request extension:** `AgentBackendRequest` includes optional nullable
  `AgentContextManifest? ContextManifest`.
- **Execution context extension:** `AgentBackendExecutionContext` exposes
  `ContextManifest` from the request.
- **Run integration:** `AgentSessionService` integrates `AgentContextManifestBuilder`
  and `IAgentContextSnapshotSources` through constructor injection.
- **Context assembly:** `AssembleContextManifestLocked` creates a manifest per
  admitted run using application-default policy and available snapshot sources.
- **Manifest attachment:** Context manifest is attached to `AgentBackendRequest`
  before backend execution begins.

## M2 delivery (2026-07-26) - COMPLETE

Production (`src/Features/Agents/`):

- **Policy evaluation:** `AgentContextPolicyEvaluationService` resolves application
  default and session override, applies the locked Off / Minimal / Standard / Detailed
  levels exclusively through `AgentContextSourcePolicyMatrix`, and records policy
  exclusion decisions. Custom policy remains unsupported.
- **Hard exclusions:** `AgentContextManifestBuilder` enforces
  `AgentContextHardExclusionRegistry` before budget accounting.
- **Snapshot consumption:** `LiveAgentContextSnapshotSources` reads contract-level
  services only.
- **Assembly:** `AgentContextContentComposer`, `AgentContextRedactionProcessor`,
  `AgentContextBudgetEnforcer`, and `AgentContextManifestBuilder` produce
  `AgentContextManifest` with provenance, exclusion, and truncation decisions.

## M4 corrective pass (2026-07-26) - COMPLETE

### Corrections applied

- **Event ordering:** `ContextDisclosed` event now emitted only after manifest is
  attached to `AgentBackendRequest` (and execution context created), ensuring backend
  receives non-null manifest before event is published.
- **UI binding:** `ContextDisclosureStatus` on `AgentPanelState` is now projected
  to `TownhallNavigationItem.ContextDisclosureStatus` and rendered in
  `TownhallNavigationPanel.CreateDirectRow` as a gray caption text.

### Production changes

- `src/Features/Agents/Application/AgentSessionService.cs`: Moved `EmitContextDisclosedLocked`
  call to after `AgentBackendRequest` creation with manifest.
- `src/Features/Townhall/Presentation/TownhallNavigationItem.cs`: Added `ContextDisclosureStatus`
  property with change notification.
- `src/Features/Townhall/Presentation/TownhallViewModel.cs`: Added `AgentPanels` accessor
  and updated `CreateDirectNavItem` to populate `ContextDisclosureStatus` from
  corresponding `AgentPanelState`.
- `src/Features/Townhall/Presentation/TownhallNavigationPanel.cs`: Added disclosure
  text display in `CreateDirectRow` with property change handling.

### Tests added

- `ContextDisclosed_EventEmitted_AfterManifestAttachedToBackend`: Verifies event ordering
  (ContextDisclosed emitted after RunRunning, manifest attached to backend).
- `ContextDisclosureStatus_IsConsumedByView_ProjectedToNavigationItem`: Architecture proof
  that the property is consumed by the view.

## M5 delivery (2026-07-27) - COMPLETE

Production (`src/Features/Agents/`, `src/Features/Townhall/`, `src/App/Composition/`):

- **Session policy boundary:** `IAgentContextSessionPolicyService` with
  `AgentContextSessionPolicyState` and `AgentSessionContextPolicyLevel` contracts.
  `AgentSessionService` stores per-conversation overrides in-memory, resolves
  `Session override > Application default` under `_sessionsSync`, and uses the
  resolved policy in `AssembleContextManifestLocked` for subsequent admitted runs.
- **DI:** `IAgentContextSessionPolicyService` resolves to the same
  `AgentSessionService` instance via `Program.ResolveAgentContextSessionPolicyService`
  (composition-root locator; registration module stays locator-free).
- **Presentation:** `TownhallContextPolicySelector` above the direct-message input;
  `TownhallViewModel` commands project effective policy, override-active state, and
  application-default caption without exposing services to the view.
  `AgentPanelState` carries policy projection fields for panel-bound state.

Tests:

- `Phase18SessionPolicyTests` — application default, override precedence, clear/reset,
  all four policy levels, session isolation, subsequent-run behavior, existing-run
  manifest immutability, rejected-run non-assembly, concurrent override updates.
- `Phase18SessionPolicyUiTests` — selector default/override/clear projection, session
  boundary reachability, channel-hidden selector, Townhall view consumption proof.
- Architecture ratchets updated: public baseline 350/646 total; source files 585/540
  Features; locator site preserved (Agents registration module clean).

Verification (2026-07-27): `dotnet build Zaide.slnx --no-restore` succeeded;
Phase18 104/104; Architecture 37/37; full fast 3186/3186; `git diff --check` clean.

## Next task

- [x] M5 session policy override and minimal UI (2026-07-27).
- [x] M6 closeout (2026-07-27).

## M6 closeout delivery (2026-07-27) - COMPLETE

### Adversarial tests added

All M6 adversarial tests added to existing test files:

**Disclosure event adversarial (`Phase18DisclosureEventTests.cs`):**
- `ContextDisclosurePayload_NeverExposesRawSnapshotContent` — reflection verifies
  no Content/RawContent/AgentContextManifest/AgentContextItem properties on
  `AgentContextDisclosurePayload`.
- `ContextDisclosed_NoRawItemContentInDisclosureStatusText` — verified disclosure
  event payload contains only metadata (source IDs, counts), never file content.
- `AssemblyFailure_EmitsSafeReasonWithoutRawSnapshotContent` — null sources
  skip silently without leaking content or exception details.
- `ContextDisclosurePayload_IdentityMatchesRunSessionAndConversation` — verified
  each ContextDisclosed event carries correct run/session/conversation tuple
  across multiple runs.
- `RejectedRun_DoesNotEmitContextDisclosed_Adversarial` — end-to-end gated
  run rejection proves ContextDisclosed is never emitted for rejected runs.

**Session policy adversarial (`Phase18SessionPolicyTests.cs`):**
- `EndAsync_RetainsSessionPolicyOverride_ForReusedConversationId` — documented
  and tested the intended behavior: `EndAsync` destroys the session but does
  NOT clear in-memory policy overrides. Overrides are conversation-scoped,
  not session-scoped. Persistence is deferred.

**Agent panel teardown adversarial (`AgentPanelHostTests.cs`):**
- `ClosePanel_DisposesOutputProjection_NoLeakAfterClose` — verified output
  history projection is disposed on panel close; subsequent appends do not
  reach closed panel.
- `ClosePanel_DetachesDraftSync_ClearsDraftHandler` — verified DraftInput
  PropertyChanged handler is removed on panel close.

### EndAsync policy override behavior (decided and documented)

After `EndAsync`, the `_sessionPolicyOverrides` entry for the conversation is
NOT cleared. If a new session is later created for the same `ConversationId`,
the previous override still applies. This is by design:

- Policy overrides are **conversation-scoped**, not session-scoped.
- `EndAsync` destroys the session but the conversation may be reused.
- Users must call `ClearSessionOverride` explicitly to reset.
- No persistence is implemented in Phase 18; overrides are in-memory only.

### Coverage summary

All M6 requirements are now covered by tests:

| Requirement | Tests |
|-------------|-------|
| Off policy → no context | 3 (M2 assembly + M5 session) |
| Hard exclusions cannot be bypassed | 4 (M2 assembly + M2 contracts) |
| Redaction fail-closed | 2 (M2 assembly + M2 contracts) |
| Raw content/secrets never in UI/events/errors | 2 (M6 adversarial + M4 disclosure) |
| Budget never partially splits item | 3 (M2 assembly) |
| Session override affects only subsequent runs | 2 (M5 session) |
| Existing run manifest immutable after policy change | 3 (M5 session + M3 integration) |
| Rejected runs: no assembly, no ContextDisclosed | 4 (M5 session + M4 disclosure + M6 adversarial) |
| Legacy backend inert to AgentContextManifest | 3 (M1 ratchets + M3 integration) |
| ContextDisclosed identity matches run/session/conversation | 3 (M4 disclosure + M6 adversarial) |
| Architecture inventory, visibility, bypass ratchets | 5 (M1 bypass ratchets + visibility) |
| M5 selector accessible, truthful, session-scoped, resettable | 5 (M5 UI) |
| Townhall/AgentPanel subscriptions disposed on teardown | 4 (TownhallViewModel dispose + M6 AgentPanelHost) |

### Verification (2026-07-27)

| Command | Result |
|---------|--------|
| `dotnet build Zaide.slnx --no-restore` | 0 errors, 0 warnings |
| `dotnet test --filter "FullyQualifiedName~Phase18"` | 110/110 passed |
| `dotnet test --filter "FullyQualifiedName~Architecture"` | 37/37 passed |
| `dotnet test Zaide.slnx --no-build` | 3194/3194 passed |
| `dotnet test Zaide.slnx --no-build --settings slow.runsettings` | 3194/3194 passed |
| `git diff --check` | Clean |

### Scope boundaries confirmed

M6 did not introduce: Custom policy, persistence, telemetry, memory, raw
context preview, viewport state, language ID, terminal scrollback,
provider-specific prompt formatting, Phase 19, or Phase 20 work.

Phase 18 M0–M6 complete. All gates pass.

## Scope boundaries observed

M5 does not implement Custom policy, persistence, telemetry, memory, viewport state,
language identification, terminal scrollback, provider-specific prompt formatting,
raw context preview, or Phase 19/20 backend consumption. M5 does not emit new audit
events beyond preserving M4 `ContextDisclosed` semantics for the policy applied to
each admitted run.

M4 implemented `ContextDisclosed` audit event emission and minimal disclosure indicator.
M4 corrective pass fixed event ordering (ContextDisclosed emitted only after manifest
attached to AgentBackendRequest) and made disclosure indicator real/visible.
