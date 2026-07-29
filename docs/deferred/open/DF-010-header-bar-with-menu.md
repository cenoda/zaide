# DF-010: Add a header bar with menu

**Area:** UI
**Status:** open
**Priority:** medium
**Discovered:** 2026-07-29
**Related:** header bar, top-level menu, window chrome, navigation

## Observation

The application is missing a top header bar with a menu. The user wants
a header bar with a menu.

## Expected

A persistent header bar at the top of the main window should host the
top-level menu, giving users a standard place to find actions such as
File, Edit, View, and other app-level commands.

## Current behavior

The top header bar and its menu are not present. Where the top-level
menu currently lives, if it exists at all, has not been inventoried.

## Evidence

- Test or smoke-check: Manual UI review
- Reproduction steps: Launch the application and look for a top header
  bar with a menu
- Output, screenshot, or log: None captured
- Relevant code path: Main window chrome, top-level menu, and any
  existing command surfaces (exact paths not yet traced)

## Why deferred

A header bar with a menu touches main window chrome, the menu model,
and the routing of all app-level commands. It should be designed and
executed as a coherent navigation/chrome pass rather than as an
isolated tweak. No work is being attempted in this note.

## Investigation notes

Unknown — not investigated yet. Confirm whether any top-level menu
already exists, where it lives, and which commands would move to a
header-bar menu.

## Revisit trigger

Revisit during a dedicated window chrome / navigation pass, or before
the next user-facing release where the missing header bar is part of
the perceived polish gap.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
