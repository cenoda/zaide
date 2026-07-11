# DF-001: Consider absorbing the agent surface into Townhall tabs

**Area:** UI
**Status:** open
**Priority:** medium
**Discovered:** 2026-07-11
**Related:** Townhall, agent panel, tab navigation

## Observation

The agent window may be a better fit as a Townhall tab instead of remaining a
separate window or surface.

## Expected

Agent conversations and Townhall conversations should have a coherent
navigation model, with the agent surface available as a Townhall tab if that
model provides the clearest user experience.

## Current behavior

The agent surface is currently treated separately from the Townhall tab model.
The exact live interaction and ownership boundaries have not been investigated
as part of this note.

## Evidence

- Test or smoke-check: Product/UI review observation
- Reproduction steps: Not applicable
- Output, screenshot, or log: None captured
- Relevant code path: To be traced during the later UI design pass

## Why deferred

This requires a deeper navigation, layout, state-ownership, and conversation
model review. It is not part of the current implementation scope.

## Investigation notes

Unknown — not investigated yet. Confirm whether this is a visual relocation,
a tab-hosting change, or a broader Townhall/agent composition decision.

## Revisit trigger

Revisit during the next Townhall or agent-surface UX pass, before expanding the
agent workspace navigation model.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
