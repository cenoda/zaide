# DF-008: Allow connecting more than one agent at a time

**Area:** settings
**Status:** open
**Priority:** medium
**Discovered:** 2026-07-29
**Related:** agent connections, agent configuration, multi-agent

## Observation

The application currently allows only one agent to be connected at a
time. The user wants multiple agent connections to be available
simultaneously.

## Expected

Users should be able to configure and connect to more than one agent
from inside the application, and the UI should make clear which agent
is the active one for a given conversation or action.

## Current behavior

Only one agent connection is available. Whether the limitation is in
the settings tab, the connection lifecycle, the conversation routing,
or some combination of those has not been inventoried.

## Evidence

- Test or smoke-check: Manual UI review
- Reproduction steps: Open the settings or agent connection surface and
  try to add a second agent
- Output, screenshot, or log: None captured
- Relevant code path: Agent configuration storage, agent connection
  lifecycle, and conversation routing (exact paths not yet traced)

## Why deferred

Multiple agent connections touch configuration storage, the connection
lifecycle, conversation routing, and the UI affordance for picking an
active agent. They should be designed and planned as a single feature
rather than a series of tweaks. No work is being attempted in this
note.

## Investigation notes

Unknown — not investigated yet. Confirm where the current single-agent
restriction lives (settings, lifecycle, or both) and whether any
existing scaffolding supports more than one.

## Revisit trigger

Revisit when the agent-connection surface is next redesigned, or when
multi-agent work is otherwise authorized.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
