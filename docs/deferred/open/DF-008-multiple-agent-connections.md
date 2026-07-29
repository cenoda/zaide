# DF-008: Add production management for multiple agent backend connections

**Area:** agents
**Status:** open
**Priority:** medium
**Discovered:** 2026-07-29
**Related:** agent connections, agent configuration, multi-agent

## Observation

The product UI does not expose a supported workflow for configuring and
maintaining backend connections for multiple agents. This is not a confirmed
single-agent limitation in the underlying application model.

## Expected

Users should be able to configure and connect more than one agent from inside
the application. Each direct conversation should make its actor/backend
binding, authentication state, and connection state clear.

## Current behavior

`AgentActorBackendBindingStore` is keyed by `ActorId` and can hold multiple
bindings concurrently. `AgentActorBackendSelectionService` and
`AgentBackendBindingPresenter` can bind either the Native Harness or an ACP
runtime per actor.

The production UI does not call `BindNativeHarness` or `BindAcpRuntime`, and
the settings model has no persisted agent/backend connection section. The
missing product capability is therefore a discoverable configuration,
connection, and persistence workflow rather than a proven one-agent storage
constraint.

## Evidence

- Test or smoke-check: Manual UI review plus live source inventory on
  2026-07-29
- Reproduction steps: Open a direct agent conversation and attempt to
  configure or connect its backend from the application
- Output, screenshot, or log: None captured
- Relevant code paths:
  - `src/Features/Agents/Application/AgentActorBackendBindingStore.cs`
  - `src/Features/Agents/Application/AgentActorBackendSelectionService.cs`
  - `src/Features/Agents/Presentation/AgentBackendBindingPresenter.cs`
  - `src/Features/Settings/Domain/SettingsModel.cs`

## Why deferred

Production connection management touches configuration storage, process and
authentication lifecycle, conversation routing, and per-actor UI state. It
should be planned as one product workflow rather than as disconnected UI
tweaks. No implementation is attempted in this note.

## Investigation notes

Initial source inventory confirms that the in-memory binding store supports
more than one actor. The remaining investigation must inventory creation,
editing, persistence, authentication, reconnect, removal, and failure
recovery as one end-to-end user journey.

## Revisit trigger

Revisit when the agent-connection surface is next redesigned, or when
multi-agent work is otherwise authorized.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
