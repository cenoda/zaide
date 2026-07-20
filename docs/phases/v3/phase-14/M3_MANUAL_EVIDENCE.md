# Phase 14 M3 — Manual smoke evidence

**Date:** 2026-07-20  
**Platform:** Linux desktop (agent environment)  
**Milestone:** Unified conversation surface (history + send + scroll UX)

## Environment

- `dotnet build Zaide.slnx`: 0 errors
- Townhall filter: **96 passed**
- Agents filter: **193 passed**
- Architecture: **26 passed**
- Full suite: **2550 passed**
- `git diff --check`: clean
- Interactive GUI smoke: **not validated in this session** — scroll anchoring, new-message chip, and busy input disable should be re-checked on a live display.

## Checklist (for human desktop validation)

| # | Step | Expected | Session result |
|---|------|----------|----------------|
| 1 | Select channel; send chat | History updates; auto-follow when scrolled to bottom | Not run |
| 2 | Open DM from People / Direct nav | Direct history projects from store | Not run |
| 3 | Send from Townhall input on DM | User + assistant/error entries on that `ConversationId` only | Not run |
| 4 | Scroll up; receive new message (Townhall or panel send) | No scroll jump; new-message affordance appears | Not run |
| 5 | Send while in-flight | Input disabled; re-enabled after completion | Not run |
| 6 | Agent Panel | Still present; panel send still works | Not run |
| 7 | Public mirror | Panel sends still mirror to active channel (until M4) | Not run |

## Implementation notes verified by automation

- **Execution wiring (choice A):** Townhall direct send finds or creates an `IAgentPanelHost` panel via `CreatePanelForActor` and calls existing `IAgentExecutionCoordinator.SendAsync(panelId, …)`.
- Channel send unchanged (`ActiveChannelId` path).
- Direct busy state tracks panel `IsBusy` for the active direct; second send while busy is rejected.
- `EntryAppended` nav refresh remains wrapped; append path does not throw into store writes.
- Scroll policy extracted to `TownhallChatScrollPolicy`; chat panel uses incremental append with near-bottom follow and new-message chip.

## M4+ still unauthorized

Privacy mirror removal, per-conversation drafts/unread, persistence, parity bridge, and Agent Panel retirement remain out of scope.
