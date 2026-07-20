# DF-001: Unify the agent surface with Townhall conversations

**Area:** UI
**Status:** open
**Priority:** high
**Discovered:** 2026-07-11
**Related:** Townhall, agent panel, tab navigation

## Observation

The dedicated Agent Panel duplicates conversation ownership and navigation
that should belong to the Townhall conversation workspace.

## Expected

Public Townhall channels and Agent direct conversations should share one
conversation system. Agent direct conversations are private by default and
must not be implicitly mirrored into public channels. The dedicated Agent
Panel may be retired only after Phase 14 proves required behavior parity.

## Current behavior

The agent surface is currently treated separately from the Townhall tab model.
The V3 discovery roadmap now records the accepted product direction. Detailed
interaction, persistence, migration, and ownership boundaries remain pending
the pre-Phase-14 refactors and Phase 14 M0.

## Evidence

- Test or smoke-check: Product/UI review observation
- Reproduction steps: Not applicable
- Output, screenshot, or log: None captured
- Relevant code path: See `docs/roadmap/V3.md`; live paths must be traced in
  Refactor 7, Refactor 8, and Phase 14 M0

## Why deferred

The direction is accepted, but implementation depends on the conversation
domain, UI foundation, backend capability, persistence, privacy, and migration
contracts. It is not part of an active implementation scope.

## Investigation notes

This is a broader conversation-system decision, not only a visual relocation
or tab-hosting change. V3 discovery requires one owning `ConversationId`,
explicit visibility, participant membership, and private Agent DMs. Refactor 7
and Refactor 8 prepare the domain and UI seams; Phase 14 owns visible unification
and dedicated-panel retirement after parity evidence.

## Revisit trigger

Revisit after Refactor 7 and Refactor 8 closeout; resolve only when Phase
14 closes the visible migration and parity boundary.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:** V3 Refactor 7, Refactor 8, and Phase 14 (planned direction;
  no implementation authorized)
- **Commit or date:**
