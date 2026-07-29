# DF-007: Debugger and output panel are not discoverable

**Area:** UI
**Status:** open
**Priority:** medium
**Discovered:** 2026-07-29
**Related:** debugger, output panel, onboarding, discoverability

## Observation

A user attempting manual testing could not find or learn how to use the
debugger or the output panel, so neither feature was actually exercised.

## Expected

The debugger and output panel should be reachable from a discoverable
UI entry point, and their basic use should be obvious without external
documentation.

## Current behavior

The entry points, gestures, or controls for opening and using the
debugger and output panel are not discoverable to a new user. Whether
the panels themselves are missing, hidden behind an unknown gesture, or
simply not surfaced from inside the application has not been verified.

## Evidence

- Test or smoke-check: Manual UI review by a new user
- Reproduction steps: Install or launch the app as a new user and try
  to find and use the debugger and output panel without external docs
- Output, screenshot, or log: None captured
- Relevant code path: Window/panel chrome, command palette entries, and
  any menu or keyboard shortcut for the debugger and output panel
  (exact paths not yet traced)

## Why deferred

This needs a small UX/IA investigation: are the panels present but
hidden, or absent, and where should they live? No UX work is being
attempted in this note.

## Investigation notes

Unknown — not investigated yet. Confirm whether the debugger and output
panel features exist, where they are surfaced today, and what the
existing discoverability mechanism is (menu, command palette, keyboard
shortcut, etc.).

## Revisit trigger

Revisit before the next user-facing release where a new user is
expected to use the debugger or output panel, or during the next
general UI discoverability pass.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
