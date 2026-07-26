# Phase 18: Live IDE Context for Agent Runs — TOFIX

## Status

M0 accepted. M1 implementation landed, failed NO-GO review, and the corrective pass
is complete as of 2026-07-26. M2 policy evaluation and context assembly landed
2026-07-26 and is complete. M3 run integration and consumption boundary landed
2026-07-26, including the M3 corrective pass for production DI wiring and
integration-test proof. M4/M5 are not started.

## Current work

- [x] M0 review and acceptance by user.
- [x] M1 initial delivery.
- [x] M1 corrective pass for NO-GO findings (2026-07-26).
- [x] M2 policy evaluation and context assembly service (2026-07-26).
- [x] M3 run integration and consumption boundary (2026-07-26).
- [x] M3 corrective pass for production DI wiring and integration proof (2026-07-26).

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

## Next task

- [ ] M4 audit event and disclosure indicator (not started).
- [ ] M5 session policy override and minimal UI (not started).

## Scope boundaries observed

M3 does not implement `ContextDisclosed` event emission, disclosure UI, session
policy selector UI, custom policy support, telemetry, persistence, backend/provider
prompt formatting, legacy backend context consumption, terminal scrollback,
viewport state, or language identification.

M4/M5 remain explicitly not started.
