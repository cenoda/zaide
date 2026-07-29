# DF-005: Refresh the application theme toward a modern, Cursor-like look

**Area:** UI
**Status:** open
**Priority:** medium
**Discovered:** 2026-07-29
**Related:** application theme, visual design, control styles

## Observation

The current application theme looks dated relative to modern editor and
agent UIs such as Cursor. The theme is described as "oldish" and the user
wants it moved closer to a Cursor-like visual style.

## Expected

The visual style — colors, typography, spacing, control shapes, density,
and overall polish — should match what users now expect from a
current-generation editor or agent UI.

## Current behavior

The theme renders the application with the current palette, typography,
and control styles. A side-by-side comparison with Cursor (or another
reference) has not been captured, and the specific surfaces that feel
dated have not been inventoried.

## Evidence

- Test or smoke-check: Manual visual review
- Reproduction steps: Launch the application and compare the look
  against Cursor or another modern reference editor
- Output, screenshot, or log: None captured
- Relevant code path: Theme resources, control styles, and any
  theme/color tokens under the UI layer (exact paths not yet traced)

## Why deferred

A full theme refresh touches many surfaces at once. It should be planned
and executed as a coherent visual pass rather than as a series of
isolated changes. No theme work is being attempted in this note.

## Investigation notes

Unknown — not investigated yet. Do not assume which controls, palettes,
or typography rules are wrong without a captured comparison and a list
of specific surfaces.

## Revisit trigger

Revisit during a dedicated theme/visual pass, or before the next
user-facing release where the visual style is part of the marketing
surface.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
