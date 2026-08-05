# DF-003: Revisit settings panel content alignment

**Area:** UI
**Status:** closed
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

- Test or smoke-check: Manual UI review; `SettingsPanelViewTests.FormColumn_IsLeftAligned_NotRightPinned`
- Reproduction steps: Open the settings panel and inspect the content alignment
- Output, screenshot, or log: `docs/phases/v3/phase-23/temp-screenshots/settings-right-aligned.png`
- Relevant code path: `src/Features/Settings/Presentation/SettingsPanelView.cs` form column `HorizontalAlignment`

## Why deferred

Choosing the alignment should be part of a broader settings-panel layout and
usability pass rather than an isolated visual tweak.

## Investigation notes

Root cause was explicit: the fixed-width (520px) settings `StackPanel` used
`HorizontalAlignment.Right`, pinning the form to the host’s right edge.

## Revisit trigger

Revisit during the next settings-panel visual or usability pass.

## Resolution

- **Outcome:** fixed
- **Fix/issue/phase:** Promoted into Phase 23 **F13** — form column set to
  `HorizontalAlignment.Left` (labels/fields remain start-aligned inside the
  column; not right-aligned field text).
- **Commit or date:** 2026-08-05
