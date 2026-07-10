# Refactor 5 Temporal Report

## Status

**Created:** 2026-07-08  
**Purpose:** Short pause-point report after Phase 5 completion, before Phase 6 routing work begins.

This report captures the current direction check only. It is not an implementation plan and does not authorize code changes by itself.

## Current State

Phase 5 is complete in the roadmap and root planning docs. The implemented shape is sound enough to continue without a large tree restructure:

- Townhall remains the primary shared workspace and activity ledger.
- Agent panels exist as dedicated focused surfaces inside the existing shell.
- Agent panel collection and active selection are owned by `AgentPanelHost`.
- Direct execution is intentionally narrow: one OpenAI-compatible, non-streaming request path.
- Direct user-to-agent interactions are mirrored into Townhall above the service layer.

## Recommendation

Do **not** do a broad module split or whole-shell redesign immediately after Phase 5.

Instead, treat Refactor 5 as a small routing-readiness pass before Phase 6:

1. Normalize agent identity and panel creation.
2. Keep routing orchestration out of `MainWindowViewModel`.
3. Add a tiny route request/result model before implementing `@mention` behavior.
4. Add lifecycle cleanup for `AgentPanelHostView` subscriptions before router-related subscriptions grow.
5. Keep provider abstractions deferred until a later phase proves they are needed.

## Issues To Watch

### Duplicate Generic Agent Identity

`AgentPanelHostView` currently creates new panels with generic values (`agent`, `Agent`, `Icon.Avatar`). This was acceptable for Phase 5, but Phase 6 routing needs stable and distinguishable agent identities.

### MainWindowViewModel Growth

`MainWindowViewModel` is currently a useful composition seam. Phase 6 should not turn it into the routing engine. Introduce a dedicated routing seam instead.

### Premature Provider Platform

The current `AgentExecutionService` is deliberately small. Do not introduce `IAgentProvider`, `AgentRegistry`, streaming, tool calling, or multi-provider configuration during the first Phase 6 routing slice.

### Layout Pressure

The right column now contains both the editor and agent panel area. Keep this layout for the Phase 6 MVP unless live UI work proves a concrete blocker. Route/debate visibility should primarily surface through Townhall rather than new shell chrome.

## Suggested Refactor 5 Exit Conditions

- New agent panels receive stable, distinguishable agent identities.
- A route parsing/result model exists and is covered by tests.
- A routing orchestration seam exists outside `MainWindowViewModel`.
- Agent panel view-host subscriptions have an explicit cleanup path.
- No provider registry or broad execution platform is introduced.
- Townhall remains the shared activity ledger.

## Next Step

Create `docs/phases/v1/phase-6/IMPLEMENTATION_PLAN.md` from the live codebase before implementing Phase 6.
