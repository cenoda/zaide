# DF-006: Add more options to the settings tab

**Area:** settings
**Status:** open
**Priority:** medium
**Discovered:** 2026-07-29
**Related:** settings panel, configuration surface

## Observation

The settings tab exposes only a small set of options today. The user
wants more settings available in the settings tab, without further
specification of which ones.

## Expected

The settings tab should expose the configuration values users actually
need to change, in a discoverable layout that fits alongside the
existing entries, and without requiring JSON editing.

## Current behavior

The current list of settings is small. Which options are missing, which
ones are hidden behind JSON editing, and which ones are simply not yet
implemented at all have not been inventoried.

## Evidence

- Test or smoke-check: Manual UI review
- Reproduction steps: Open the settings tab and list every currently
  exposed option
- Output, screenshot, or log: None captured
- Relevant code path: `src/Features/Settings/Presentation/SettingsPanelView.cs`
  and the settings view-model; the underlying settings store and any
  JSON-only configuration surfaces

## Why deferred

A broader settings coverage pass is needed to decide which options
belong in the tab, which belong in JSON or advanced surfaces, and which
are not yet implemented at all. No work is being attempted in this
note.

## Investigation notes

Unknown — not investigated yet. Capture a list of every value a user
currently has to set outside the settings tab, and a list of values
that are simply not configurable.

## Revisit trigger

Revisit during the next settings-panel layout and usability pass (the
same trigger as DF-003 and DF-004).

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
