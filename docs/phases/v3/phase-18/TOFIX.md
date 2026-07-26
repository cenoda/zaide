# Phase 18: Live IDE Context for Agent Runs — TOFIX

## Status

M0 accepted. M1 implementation landed, failed NO-GO review, and the corrective pass
is complete as of 2026-07-26. M2 policy evaluation and context assembly landed
2026-07-26 and is complete. M3 run integration and consumption boundary landed
2026-07-26, including production DI wiring, integration-test proof, and the M3
publisher corrective pass connecting Editor and Source Control presentation to
passive snapshot services. M4 disclosure event and indicator implemented with corrective
pass as of 2026-07-26. M5 remains explicitly not started.

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
Phase18 73/73; Architecture 37/37; full fast 3154/3154; serial 3154/3154.
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

## Next task

- [x] M4 audit event and disclosure indicator (2026-07-26) — corrective pass complete.
- [ ] M5 session policy override and minimal UI (not started).

## Scope boundaries observed

M3 does not implement session policy selector UI, custom policy support, telemetry,
persistence, backend/provider prompt formatting, legacy backend context consumption,
terminal scrollback, viewport state, or language identification. M3 does not emit
`ContextDisclosed` (implemented in M4).

M4 implemented `ContextDisclosed` audit event emission and minimal disclosure indicator.
M4 corrective pass fixed event ordering (ContextDisclosed emitted only after manifest
attached to AgentBackendRequest) and made disclosure indicator real/visible.
M5 remains explicitly not started.
