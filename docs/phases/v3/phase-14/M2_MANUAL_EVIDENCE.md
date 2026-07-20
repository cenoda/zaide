# Phase 14 M2 — Manual smoke evidence

**Date:** 2026-07-20  
**Platform:** Linux desktop (agent environment)  
**Milestone:** Townhall navigation UI (channels + directs)

## Environment

- Automated regression: **2538 passed** (full suite), Architecture **26 passed**, Townhall **86 passed**, Conversations **93 passed**.
- Interactive GUI smoke: **not validated in this session** — `dotnet run` was not exercised end-to-end on a live display after the navigation change. Residual risk: sidebar list focus/selection chrome should be re-checked on a desktop session before M3.

## Checklist (for human desktop validation)

| # | Step | Expected | Session result |
|---|------|----------|----------------|
| 1 | Launch app | Three seeded channels listed and selectable | Not run (no display session) |
| 2 | Click agent in People panel | Direct appears under **Direct** in sidebar; conversation selected | Not run |
| 3 | Switch back to a channel | Channel chat and send still work | Not run |
| 4 | Agent Panel | Still visible; send from panel still works; public mirror unchanged | Not run |
| 5 | Keyboard | Focus channel/direct list; arrow + Enter/Space changes selection without broken focus | Not run |

## Implementation notes verified by automation

- `ActiveConversationId` is set on channel and direct selection.
- `GetOrCreateDirectConversation` returns stable id when opening the same agent twice.
- Direct store entries project to the center pane (read-only for M2; Townhall send remains channel-only).
- `AgentTownhallMirrorCoordinator` and Agent Panel paths unchanged.

## Interim M2 limitation (center pane)

- Selecting a **direct** shows projected store history only; Townhall input does not send to DMs (M3).
- `DraftText` remains global/channel-oriented (M5 owns per-conversation drafts).
