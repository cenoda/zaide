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
Refactor 7 delivered typed conversations and direct-conversation entries behind
the Agent Panel. Refactor 8 extracted shell/Townhall/Agent presentation structure.
The dedicated Agent Panel remains visible; public channel mirroring of agent
sends remains; there is still no Townhall DM navigation.

## Evidence

- Test or smoke-check: Product/UI review observation
- Reproduction steps: Not applicable
- Output, screenshot, or log: None captured
- Relevant code path: See `docs/roadmap/V3.md` and
  `docs/phases/v3/phase-14/IMPLEMENTATION_PLAN.md` (M0 live audit)

## Why deferred

Direction is accepted. Pre-Phase-14 refactors are closed. Phase 14 M0 is
accepted (2026-07-20); production milestones (DM nav, privacy, persistence,
panel retirement) remain unauthorized until each is explicitly approved.

## Investigation notes

This is a broader conversation-system decision, not only a visual relocation
or tab-hosting change. V3 requires one owning `ConversationId`, explicit
visibility, participant membership, and private Agent DMs. Phase 14 M0 locks
milestones M1–M9 and the retirement parity checklist; DF-001 closes when M8
retires the panel (or closeout records an accepted residual).

## Revisit trigger

Revisit when Phase 14 M0 is accepted and implementation milestones are
authorized; resolve when Phase 14 M8/M9 close the visible migration and parity
boundary.

## Resolution

- **Outcome:** open
- **Fix/issue/phase:** Phase 14 (M0 accepted 2026-07-20; M1+ unauthorized)
- **Commit or date:**
