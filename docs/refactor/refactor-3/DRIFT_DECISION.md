# Refactor 3: Agent-First Layout Drift — Decision

## Status

**Historical layout decision.**

This document records the visual direction explored in Refactor 3. It should
not be treated as proof that formal Phase 4 was completed.

---

## Why This Exists

The earlier refactor-3 documents described a partial layout rearrangement, but
they did not state the real intent strongly enough.

That was misleading.

The real purpose of refactor-3 is to record and authorize a deliberate visual
drift in Zaide's center of gravity:

- away from "editor in the middle, AI on the side"
- toward "agent conversation in the center, editor as execution surface"

This document exists so contributors do not treat the change as a cosmetic
panel shuffle only.

---

## Decision

Refactor 3 established the **visual direction** for an agent-first workspace.

That means:

- The main visual focus should be the shared agent workspace
- The editor remains always visible and important, but it is no longer the
  primary narrative surface
- The file tree and terminal remain first-class support tools
- Future Townhall, agent-panel, and routing work should reinforce this center of
  gravity instead of recreating a classic IDE plus chat sidebar

This is an intentional layout drift, not formal completion of the later Phase 4
agent-workspace behavior.

---

## Previous Layout Model

The pre-refactor shell followed the older layout:

```text
┌──────────┬────────────────────────┬──────────────────┐
│ Files    │ Editor                 │ Agent Area       │
│ (Tree)   │                        │ (placeholder)    │
├──────────┴────────────────────────┴──────────────────┤
│ Terminal / Bottom Panel                                  │
└───────────────────────────────────────────────────────────┘
```

This layout was functional and stable, but it still read as a conventional
editor-first IDE.

---

## Target Direction

The visual direction established by Refactor 3 is:

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

Refactor 3 should be read as a change in **layout direction**, not as the point
where the full agent workspace became real.

Before:

- editor-first
- agent capability layered on later
- chat/agent UI treated as attached or secondary

After this refactor:

- the shell visually points toward an agent-first workflow
- Townhall becomes the center of the mapped layout
- editor supports implementation, inspection, and intervention
- later agent features must fit this center-first model

---

## Non-Goals

This decision does **not** mean:

- immediate full AI/agent integration
- immediate agent-to-agent routing
- immediate persistence or Git features
- removing the editor from the main shell
- claiming that UI remapping alone completed Phase 4

The drift is about **what the main screen privileges**, not about pretending the
rest of the IDE stops mattering.

---

## Implications For Future Docs

From this point on:

- root-level docs should describe Zaide as moving toward an agent-first workspace
- Phase 4 work should be framed as the point where the Townhall scaffold gains
  real workflow behavior
- Phase 5 agent surfaces must not demote Townhall from the visual center
- future implementation plans should inherit this visual direction instead of
  reopening the editor-first baseline

If a later plan contradicts this, it must do so explicitly and justify why the
product direction changed again.

---

## Relationship To Root Docs

This decision is reflected at the root level in:

- `README.md`
- `docs/architecture/OVERVIEW.md`
- `docs/roadmap/PHASES.md`

Those documents define the public-facing product direction. This document is
only the historical explanation for why Refactor 3 changed the shell shape.

---

*Last updated: 2026-07-07*
