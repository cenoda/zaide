# Phase 4: Agent Workspace Foundations — Implementation Plan

## Pre-Implementation Verification

- [x] Confirm `docs/phases/phase-4/` did not previously exist
- [x] Confirm the current Townhall-centered shell comes from `refactor-3/4`, not a formal Phase 4 plan
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [ ] Re-read `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `README.md` after the status correction lands

## Planning Status

**Draft.**

Refactor-3 and refactor-4 delivered a Townhall-centered UI scaffold and visual
polish. They did not complete Phase 4 behaviorally. This plan defines the first
real agent-workspace phase on top of the existing shell.

Verified live seams on 2026-07-07:

- `src/MainWindow.axaml.cs`
  - already places Townhall in the center and keeps the editor visible on the right
- `src/ViewModels/TownhallViewModel.cs`
  - currently owns sample channels, sample agents, active-channel switching, and user message sending
- `src/Models/TownhallState.cs`
  - currently stores per-channel message lists, active channel ID, agent list, and draft text only
- `src/Models/TownhallMessage.cs`
  - currently distinguishes only `Normal`, `Warning`, and `System`
- `src/Views/TownhallView.cs`
  - currently wires the visible Townhall scaffold to the ViewModel

## Scope

**Goal:** Turn the Townhall-centered scaffold into a real shared workspace for
user and agent activity without widening into agent panels, routing, or
persistence-heavy architecture work yet.

**In scope:**

- Automatic timestamped activity logging in Townhall
- Clear distinction between chat messages and action/log events
- Scrollable, filterable activity history in the center workspace
- Replace sample-only Townhall assumptions with phase-appropriate session state
- Preserve existing file-tree, editor, and terminal workflows

**Out of scope:**

- Dedicated per-agent panels (Phase 5)
- Agent-to-agent routing / `@mention` orchestration (Phase 6)
- Full persistence architecture unless needed for the narrow Phase 4 outcome
- Real autonomous agent execution engine
- Large layout rewrites; the shell scaffold already exists

## Milestones

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate and baseline verification | `dotnet build`, `dotnet test`, manual Townhall smoke check | ⬜ |
| M1 | Activity/event model for Townhall | Model/ViewModel tests for event kinds, timestamps, and filtering | ⬜ |
| M2 | Auto-log and user/agent distinction | ViewModel tests for generated entries and classification rules | ⬜ |
| M3 | Townhall activity history UI | View tests for rendering, filtering, and scroll behavior | ⬜ |
| M4 | Docs sync and exit audit | `dotnet build`, `dotnet test`, roadmap/overview/readme sync | ⬜ |

## Planned Change Shape

1. Introduce a stricter Townhall activity model.
   The current `TownhallMessage` shape is too narrow for a real shared activity
   surface. Phase 4 should separate conversational messages from structured
   action/history entries instead of overloading `Normal` / `System`.

2. Replace sample-only assumptions in `TownhallViewModel`.
   The current ViewModel seeds sample channels, sample agents, and sample
   messages directly. Phase 4 should move from demo data toward explicit
   session-state initialization suitable for real workspace activity.

3. Add automatic activity entries.
   User-visible actions such as sending a message, switching channels, or later
   agent-visible workflow events should leave timestamped Townhall records.

4. Add filtering without splitting the workspace yet.
   Keep Townhall as a single center workspace, but allow users to filter
   conversational vs activity-oriented entries rather than adding Phase 5 agent
   panels early.

## Exit Conditions

- [ ] Townhall is no longer sample-data-only in practice
- [ ] User actions produce timestamped activity entries automatically
- [ ] Chat vs action/log entries are visually and semantically distinct
- [ ] The center workspace supports scrolling and filtering across activity history
- [ ] `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `README.md` match the implemented Phase 4 state
