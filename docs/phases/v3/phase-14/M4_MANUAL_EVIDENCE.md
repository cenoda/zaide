# Phase 14 M4 — Manual privacy smoke evidence

**Date:** 2026-07-20  
**Platform:** Linux desktop (agent environment)  
**Milestone:** Privacy — remove implicit public Townhall mirror (D02)

## Environment

- `dotnet build Zaide.slnx`: 0 errors
- Shell filter: **166 passed**
- Townhall filter: **90 passed**
- Agents filter: **193 passed**
- Architecture filter: **26 passed**
- Full suite: see IMPLEMENTATION_PLAN.md M4 closeout
- `git diff --check`: clean
- Interactive GUI smoke: **not validated in this session** — privacy rows should be re-checked on a live display.

## Checklist (for human desktop validation)

| # | Step | Expected | Session result |
|---|------|----------|----------------|
| 1 | Select public channel `#townhall-main` (or seeded channel) | Channel history visible | Not run |
| 2 | Agent Panel: send a message | Channel history **unchanged**; panel/direct history shows user + reply/error | Not run |
| 3 | Townhall: open DM, send | Only that direct updates; public channels unchanged | Not run |
| 4 | Townhall: send in a channel | Channel still receives user chat | Not run |
| 5 | Optional: `@mention` route between panels | No public channel spam | Not run |
| 6 | Pre-M4 mirrored history | Existing mirrored entries in session are **not** rewritten | N/A (limitation) |

## Implementation notes verified by automation

- `AgentTownhallMirrorCoordinator` forwards to `IAgentRouter.RouteAndExecuteAsync` only; no `AddMirroredActivityToConversation` calls.
- Removed public `TownhallViewModel.AddMirroredActivityToConversation` and `TryGetActiveChannelConversationId` (mirror-only APIs).
- Channel send (`SendMessageCommand` on `ActiveChannelId`) and channel events (`LogActivity`) unchanged.
- Townhall DM send (M3) unchanged; writes to direct `ConversationId` via panel/coordinator path.
- Agent Panel remains visible and functional.

## Limitation

Historical public mirror entries already present in an in-memory session before M4 are not rewritten or removed.

## M5+ still unauthorized

Per-conversation draft/unread, persistence, parity bridge, Agent Panel retirement, and share-to-channel UI remain out of scope.
