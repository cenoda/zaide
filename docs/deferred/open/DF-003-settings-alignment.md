# DF-003: Revisit settings panel content alignment

**Area:** UI
**Status:** open
**Priority:** low
**Discovered:** 2026-07-11
**Related:** settings panel, layout alignment

## Observation

The settings panel content is currently right-aligned.

## Expected

The settings layout should use the alignment that provides the clearest and
most comfortable reading and editing flow. Center alignment or left alignment
should be evaluated as alternatives to the current right alignment.

## Current behavior

Settings content appears right-aligned. The preferred replacement alignment
has not yet been decided.

## Evidence

- Test or smoke-check: Manual UI review
- Reproduction steps: Open the settings panel and inspect the content alignment
- Output, screenshot, or log: None captured
- Relevant code path: Settings panel layout

## Why deferred

Choosing the alignment should be part of a broader settings-panel layout and
usability pass rather than an isolated visual tweak.

## Investigation notes

Unknown — not investigated yet. Compare left, centered, and right alignment in
the context of labels, controls, window width, and long setting descriptions.

## Revisit trigger

Revisit during the next settings-panel visual or usability pass.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
