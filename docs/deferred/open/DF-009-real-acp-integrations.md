# DF-009: Ship real ACP integrations (Claude Code, OpenCode) so users do not need JSON

**Area:** other
**Status:** open
**Priority:** high
**Discovered:** 2026-07-29
**Related:** ACP, Claude Code, OpenCode, agent backends, onboarding

## Observation

Users currently need to know JSON to configure a working agent backend.
The user wants real ACP integrations — Claude Code and OpenCode named
as examples — so that a user can get a working connection without
writing JSON.

## Expected

A user should be able to pick a named ACP backend (for example, Claude
Code or OpenCode), supply whatever credentials or paths are required
through a normal UI, and have a working agent connection without
editing JSON by hand.

## Current behavior

The list of supported ACP backends, the credential flow, and the gaps
between "named backend" and "JSON-only configuration" have not been
inventoried. It is not yet confirmed whether Claude Code and OpenCode
are partially wired, fully wired, or absent.

## Evidence

- Test or smoke-check: Manual UI review of the agent connection surface
- Reproduction steps: Attempt to configure Claude Code and OpenCode as
  agent backends without editing JSON
- Output, screenshot, or log: None captured
- Relevant code path: Agent backend registration, ACP transport, and the
  settings/agent configuration surface (exact paths not yet traced)

## Why deferred

Each named ACP backend is its own integration: transport, SDK, process,
authentication, and capability mapping. Adding Claude Code and OpenCode
needs its own plan and threat model, not an ad-hoc tweak. No work is
being attempted in this note.

## Investigation notes

Unknown — not investigated yet. Confirm which ACP backends are
currently supported (if any) and what would be required to add Claude
Code and OpenCode as first-class, JSON-free options.

## Revisit trigger

Revisit when ACP backend work is next authorized, or before a
user-facing release where agent onboarding is part of the user
experience.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:**
- **Commit or date:**
