# Phase 14 M7 — Manual parity bridge evidence

**Milestone:** Parity bridge (Townhall as truthful execution/routing surface; Agent Panel thin host)  
**Date:** 2026-07-21  
**Environment:** Linux desktop; automated parity suite green; interactive GUI rows **not validated on display** in agent session (same limitation as M3–M6).

## Automated parity matrix (proxy for retirement checklist)

| Checklist row | Result |
|---------------|--------|
| Direct send; user + assistant on owning `ConversationId` only | Pass — `Phase14M7ParityBridgeTests.DirectSend_*` |
| Error / cancel visibility on owning conversation | Pass — `ErrorAndCancel_RemainVisibleOnOwningConversation` |
| Re-send after failure (no retry chrome/API) | Pass — `ReSend_AfterFailure_IsNotRetryChrome_WorksViaSendAgain` |
| `@mention` routing without open target panel | Pass — `Routing_MentionWithoutOpenTargetPanel_UsesCatalogActorId` + `AgentRouterTests.RouteAndExecuteAsync_MentionTarget_DoesNotRequireOpenTargetPanel` |
| Navigation during in-flight work keeps conversation busy + history | Pass — `NavigationDuringWork_KeepsConversationBusyAndHistory` |
| Panel close during work does not drop conversation-keyed busy / history | Pass — `PanelCloseDuringWork_DoesNotDropInFlightStatusOnConversation` |
| Private history ownership (no public channel mirror) | Pass — direct-send privacy asserts + M4 shell suite |
| Routing failure visible on source conversation | Pass — `RoutingFailure_VisibleOnSourceConversation_NotLost` |
| Shared conversation draft map (Townhall ↔ panel thin host) | Pass — `Draft_PanelAndTownhallShareConversationOwnedMap` |

## Interactive smoke checklist (not run in agent session)

1. Launch app; open Townhall DM with Alpha; send message; confirm history in Townhall only (no channel copy).
2. Type `@Beta please review` from Alpha DM without opening a Beta panel tab; confirm message lands on Beta conversation.
3. Start a slow request; switch to a channel; return to DM — busy/input disable should reappear until completion; history updates.
4. Start a request; close Agent Panel tab; return via Townhall DM — in-flight should not vanish silently; completion still appears.
5. Type draft in Townhall DM; switch conversation and back; draft retained. Type in panel input for same conversation; reopen Townhall — draft should match.
6. Force routing failure (`@Ghost`); confirm `Routing failed: …` / error entry in owning DM.
7. After error/cancel, type again and send (re-send) — no dedicated Retry control expected.
8. Confirm Agent Panel chrome still present (M8 not started).

## Explicit not-validated rows

| Row | Status |
|-----|--------|
| Interactive GUI on display | **Not run** (agent session) |
| Keyboard focus / accessibility polish | Deferred to M9 |
| Narrow / wide / high-DPI screenshots | Deferred to M9 |
| Post-M8 layout without Agent Panel | Out of scope (M8) |

## Shell boundary (M7)

- `AgentPanelHostView`, `RightColumnHost`, and Agent Panel shell wiring **remain**.
- Panel is a redundant projection / thin host for conversation-owned state.
