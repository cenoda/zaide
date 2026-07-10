# Phase 4: Agent Workspace Foundations — Umbrella Plan

## Planning Status

**Complete (2026-07-08).**

The original single-plan draft for Phase 4 tried to cover data model, auto-logging,
UI, and docs sync in one pass, plus an undecided agent wire-format question. That
is too wide for one implementation plan, following the same pattern that split
Phase 3 into 3.5–3.9.1. Phase 4 is now an umbrella: each sub-phase gets its own
`docs/phases/v1/phase-4.x/IMPLEMENTATION_PLAN.md` with its own milestones and exit
conditions.

Refactor-3 and refactor-4 delivered a Townhall-centered UI scaffold and visual
polish. They did not complete Phase 4 behaviorally — see
`docs/architecture/OVERVIEW.md` and `docs/roadmap/PHASES.md`.

## Goal

Turn the Townhall-centered scaffold into a real shared workspace for user and
agent activity without widening into agent panels (Phase 5), routing (Phase 6),
or persistence-heavy architecture work.

## Sub-Phases

| Sub-phase | Scope | Status |
|-----------|-------|--------|
| [4.1](../phase-4.1/IMPLEMENTATION_PLAN.md) | Activity/event data model + agent-format decision | ✅ Complete (2026-07-08) |
| [4.2](../phase-4.2/IMPLEMENTATION_PLAN.md) | Auto-logging + real session-state initialization | ✅ Complete (2026-07-08) |
| [4.3](../phase-4.3/IMPLEMENTATION_PLAN.md) | Townhall activity history UI: rendering, filtering, scroll | ✅ Complete (2026-07-08) |
| [4.4](../phase-4.4/IMPLEMENTATION_PLAN.md) | Docs sync + exit audit for all of Phase 4 | ✅ Complete (2026-07-08) |

## Out of Scope (all sub-phases)

- Dedicated per-agent panels (Phase 5)
- Agent-to-agent routing / `@mention` orchestration (Phase 6)
- Full persistence architecture unless narrowly needed
- Real autonomous agent execution engine
- Large layout rewrites; the shell scaffold already exists

## Phase 4 Exit Conditions

These are only satisfied once 4.1–4.4 are all complete:

- [x] Townhall initializes with explicit in-memory session seed data (`InitializeSessionState()` creates channels, agents, and empty per-channel message collections; `LogActivity` produces real entries on send/switch) — ✅ Complete (2026-07-08)
- [x] User actions produce timestamped activity entries automatically — `SendMessageCommand` logs `Chat`, channel switch logs `ChannelEvent`, both with `DateTimeOffset.UtcNow`
- [x] Chat vs action/log entries are visually and semantically distinct — `TownhallChatPanel.CreateMessageRow` branches on `Kind`: `Chat` → full bubble, all others → compact row
- [x] The center workspace supports scrolling and filtering across activity history — `FilterMode`/`FilteredMessages`/`ApplyFilter()` in ViewModel, `ToggleButton` group in `TownhallView`, `ScrollViewer.ScrollToEnd()` in `ChatPanel`
- [x] An agent wire-format decision exists (even if minimal) so the activity
      schema isn't guessing at agent-event shape — `SourceProvider`/`SourceModel`/`ThreadId`/`Metadata` fields on `TownhallMessage`, documented in phase-4.1 Agent Format Decision
- [x] `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `README.md`
      match the implemented Phase 4 state — all three updated in Phase 4.4 (2026-07-08) with accurate session-state wording
