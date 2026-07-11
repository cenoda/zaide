# DF-002: Korean IBUS input behaves unexpectedly

**Area:** UI
**Status:** open
**Priority:** high
**Discovered:** 2026-07-11
**Related:** Korean input, IBUS, text input controls

## Observation

Korean text input through IBUS behaves unexpectedly in the application.

## Expected

Korean composition, commit, deletion, and cursor behavior should work normally
in the affected text input controls.

## Current behavior

The exact symptom, affected control, and reproduction sequence have not yet
been captured. The issue was observed during testing with Korean input enabled.

## Evidence

- Test or smoke-check: Manual Korean input test
- Reproduction steps: To be captured during the follow-up investigation
- Output, screenshot, or log: None captured
- Relevant code path: To be traced; likely includes the affected text input,
  Avalonia text-input events, and IBUS composition handling

## Why deferred

The issue needs a focused input-method investigation with a minimal
reproduction and event-level evidence. No workaround or code change is being
attempted in this note.

## Investigation notes

Unknown — not investigated yet. Do not assume whether the cause is in IBUS,
Avalonia event handling, a specific control, or application-level focus/state
management until the symptom is reproduced and logged.

## Revisit trigger

Revisit before relying on Korean text input for a release-quality manual smoke
check or when the affected input workflow is next changed.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
