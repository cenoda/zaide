# Refactor 3: Agent-First Layout Drift — Decision

## Status

**Decided.**

Refactor 3 is no longer treated as a narrow layout refactor or a conventional
implementation plan. It is the explicit product/UI drift from an editor-first
IDE shell toward an agent-first workspace.

---

## Why This Exists

The earlier refactor-3 documents described a partial layout rearrangement, but
they did not state the real intent strongly enough.

That was misleading.

The real purpose of refactor-3 is to record and authorize a deliberate drift in
Zaide's center of gravity:

- away from "editor in the middle, AI on the side"
- toward "agent conversation in the center, editor as execution surface"

This document replaces the earlier refactor-3 plan so contributors do not treat
the change as a cosmetic panel shuffle.

---

## Decision

Zaide is now explicitly **agent-first** in product direction.

That means:

- The main visual focus should be the shared agent workspace
- The editor remains always visible and important, but it is no longer the
  primary narrative surface
- The file tree and terminal remain first-class support tools
- Future Townhall, agent-panel, and routing work should reinforce this center of
  gravity instead of recreating a classic IDE plus chat sidebar

This is an intentional drift, not accidental doc drift and not a temporary
experiment.

---

## Previous Layout Model

The current implemented shell is still the older layout:

```text
┌──────────┬────────────────────────┬──────────────────┐
│ Files    │ Editor                 │ Agent Area       │
│ (Tree)   │                        │ (placeholder)    │
├──────────┴────────────────────────┴──────────────────┤
│ Terminal / Bottom Panel                                  │
└───────────────────────────────────────────────────────────┘
```

This layout is functional and stable, but it still reads as a conventional
editor-first IDE.

---

## Target Direction

The decided direction is:

```text
┌──────────┬────────────────────────────────────┬──────────────┐
│ Files    │ Townhall                           │ Editor       │
│ (Tree)   │ active thread, agent discussion,   │ focused file │
│          │ user intervention, task state      │ diff/edit    │
├──────────┴────────────────────────────────────┴──────────────┤
│ Terminal / Logs                                                │
└────────────────────────────────────────────────────────────────┘
```

Interpretation:

- **Left:** file tree and project navigation
- **Center:** Townhall, the primary attention surface
- **Right:** editor, always available for direct code work
- **Bottom:** terminal and logs

This keeps the useful stability of the current shell while changing the user's
default visual and workflow priority.

---

## What Changed Conceptually

Refactor 3 should now be read as a change in **product stance**, not just in
panel placement.

Before:

- editor-first
- agent capability layered on later
- chat/agent UI treated as attached or secondary

After:

- agent-first
- Townhall becomes the center of the working session
- editor supports implementation, inspection, and intervention
- later agent features must fit this center-first model

---

## Non-Goals

This decision does **not** mean:

- immediate full AI/agent integration
- immediate agent-to-agent routing
- immediate persistence or Git features
- removing the editor from the main shell
- downgrading file tree or terminal into throwaway UI

The drift is about **what the main screen privileges**, not about pretending the
rest of the IDE stops mattering.

---

## Implications For Future Docs

From this point on:

- root-level docs must describe Zaide as moving toward an agent-first workspace
- Phase 4 work should be framed as the layout transition plus Townhall
  foundations
- Phase 5 agent surfaces must not demote Townhall from the visual center
- future implementation plans should inherit this decision instead of reopening
  the editor-first baseline

If a later plan contradicts this, it must do so explicitly and justify why the
product direction changed again.

---

## Implementation Guidance

When the implementation plan is rewritten later, it should preserve the spirit
of this decision:

- keep the shell stable enough that existing editor/file-tree/terminal workflows
  do not collapse
- move visual attention to Townhall first
- keep the editor present and strong, but no longer central by default
- avoid language that reduces the pivot to "swap these two columns"

The implementation can be incremental.
The direction is not incremental.

---

## Relationship To Root Docs

This decision is already reflected at the root level in:

- `README.md`
- `docs/architecture/OVERVIEW.md`
- `docs/roadmap/PHASES.md`

Those documents define the public-facing product direction.
This document exists to explain why refactor-3 changed shape so dramatically.

---

*Last updated: 2026-07-01*
