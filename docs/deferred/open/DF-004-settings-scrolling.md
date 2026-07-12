# DF-004: Add scrolling to the settings panel

**Area:** UI
**Status:** open
**Priority:** medium
**Discovered:** 2026-07-12
**Related:** settings panel, scrolling, window size

## Observation

The settings panel needs scrolling so its content remains accessible when it
does not fit in the available window height.

## Expected

Users should be able to reach every settings control through a natural scroll
interaction without requiring the window to be enlarged beyond practical
limits.

## Current behavior

The settings content can extend beyond the available viewport. The exact
overflow behavior and affected window sizes have not yet been measured.

## Evidence

- Test or smoke-check: Manual UI review
- Reproduction steps: Open the settings panel at a constrained window height
  and inspect whether all controls remain reachable
- Output, screenshot, or log: None captured
- Relevant code path: Settings panel layout and container hierarchy

## Why deferred

The scrolling container, sizing behavior, and interaction with the alignment
decision in DF-003 should be reviewed together during the next settings-panel
layout pass.

## Investigation notes

Unknown — not investigated yet. Confirm the intended scroll direction,
viewport sizing, scrollbar styling, keyboard focus behavior, and whether the
whole panel or only a settings-content region should scroll.

## Revisit trigger

Revisit during the next settings-panel layout and usability pass.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
