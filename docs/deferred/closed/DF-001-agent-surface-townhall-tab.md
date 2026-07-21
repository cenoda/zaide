# DF-001: Unify the agent surface with Townhall conversations

**Area:** UI
**Status:** closed
**Priority:** high
**Discovered:** 2026-07-11
**Related:** Townhall, agent panel, tab navigation

## Observation

The dedicated Agent Panel duplicated conversation ownership and navigation
that should belong to the Townhall conversation workspace.

## Expected

Public Townhall channels and Agent direct conversations should share one
conversation system. Agent direct conversations are private by default and
must not be implicitly mirrored into public channels. The dedicated Agent
Panel may be retired only after Phase 14 proves required behavior parity.

## Resolution

- **Outcome:** closed — Phase 14 M8 (2026-07-21) retired dedicated Agent Panel
  shell chrome (`AgentPanelHostView`, right-column panel row, `SendAgentMessageAsync`
  panel entry path). Townhall is the sole user-facing workflow for Agent direct
  conversations. M7 parity bridge behavior is preserved via conversation-keyed
  execution and catalog routing.
- **Fix/issue/phase:** Phase 14 M8
- **Commit or date:** 2026-07-21 (M8 acceptance commit on `master`)
- **Evidence:** `docs/phases/v3/phase-14/M8_MANUAL_EVIDENCE.md`;
  `Phase14M8PanelRetirementTests`; M7 `Phase14M7ParityBridgeTests`

## Residual limitation

Thin non-visual `IAgentPanelHost` execution seams remain for conversation
provisioning and coordinator lookup. No user-facing panel state or chrome.
Keyboard/a11y closeout evidence recorded in Phase 14 M9
(`docs/phases/v3/phase-14/M9_MANUAL_EVIDENCE.md`).
