# DF-009: Ship user-facing ACP backend onboarding and integrations

**Area:** other
**Status:** open
**Priority:** high
**Discovered:** 2026-07-29
**Related:** ACP, Claude Code, OpenCode, agent backends, onboarding

## Observation

The application does not expose a supported user workflow for configuring an
ACP backend. The earlier claim that users can do so by editing JSON was
incorrect: the persisted settings schema has no ACP or agent-backend
configuration section.

The user wants real ACP integrations — Claude Code and OpenCode named as
examples — so that a user can configure and verify a working connection from
inside Zaide.

## Expected

A user should be able to pick a supported named ACP backend, supply the
required executable, credentials, or authentication through a normal UI,
verify the connection, and bind it to an agent without editing internal data.

## Current behavior

Phase 20 registered an ACP process/session/backend stack and per-actor binding
contracts. However:

- `SettingsModel` persists only editor, LLM, keybinding, and debug settings;
- no production UI invokes `BindAcpRuntime`;
- no supported onboarding flow selects an executable, negotiates
  authentication, validates compatibility, or persists the binding;
- external candidate smoke was not executed during V3 closeout.

Named Claude Code and OpenCode compatibility remains unverified. The current
gap is an absent product onboarding path, not a JSON-only workflow.

## Evidence

- Test or smoke-check: Manual UI review plus live source inventory on
  2026-07-29
- Reproduction steps: Attempt to configure Claude Code and OpenCode as
  agent backends from the application
- Output, screenshot, or log: None captured
- Relevant code paths:
  - `src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs`
  - `src/Features/Agents/Application/AgentActorBackendSelectionService.cs`
  - `src/Features/Agents/Presentation/AgentBackendBindingPresenter.cs`
  - `src/Features/Settings/Domain/SettingsModel.cs`

## Why deferred

Each named ACP backend requires verified protocol compatibility, process
lifecycle, authentication, capability mapping, failure recovery, and a usable
configuration surface. This needs a bounded product plan and threat model, not
an ad-hoc settings tweak. No implementation is attempted in this note.

## Investigation notes

Initial source inventory confirms ACP infrastructure and explicit binding
contracts but no production onboarding caller or persisted ACP settings.
Before implementation, verify the exact supported ACP profile and each named
backend against a real candidate process.

## Revisit trigger

Revisit when ACP backend work is next authorized, or before a
user-facing release where agent onboarding is part of the user
experience.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
