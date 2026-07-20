# Phase 14 M5 — Manual draft + unread smoke evidence

**Date:** 2026-07-20  
**Platform:** Linux desktop (agent environment)  
**Milestone:** Per-conversation draft + unread/read cursor (in-memory only)

## Environment

- `dotnet build Zaide.slnx`: 0 errors
- Townhall filter: **98 passed**
- Agents filter: **193 passed**
- Shell filter: **166 passed**
- Architecture filter: **26 passed**
- Full suite: **2543 passed** (see IMPLEMENTATION_PLAN.md M5 closeout)
- `git diff --check`: clean
- Interactive GUI smoke: **not validated in this session** — draft/unread rows should be re-checked on a live display.

## Checklist (for human desktop validation)

| # | Step | Expected | Session result |
|---|------|----------|----------------|
| 1 | Type draft in channel → switch to DM → type different draft → switch back | Both drafts preserved in the correct conversation | Not run (GUI) |
| 2 | Send in one conversation | That draft cleared; other conversation drafts intact | Covered by automated tests; GUI not run |
| 3 | With channel selected, cause new activity on a DM (panel send / append) | DM row shows unread affordance in nav | Covered by automated tests; GUI not run |
| 4 | Select that DM | Unread clears; history correct | Covered by automated tests; GUI not run |
| 5 | Agent Panel still works; no public channel spam (M4) | Panel functional; privacy preserved | Privacy suite green; GUI not run |

## Implementation notes verified by automation

- Per-conversation drafts owned by internal `TownhallConversationUiState` (Townhall presentation).
- Selection (`SelectConversation` / channel / open-DM) saves previous draft and loads target draft into `DraftText`.
- Successful send clears only the active conversation draft slot and input.
- Last-read entry id tracked per `ConversationId`; inactive appends mark unread; selecting marks read through latest entry.
- Active conversation appends advance last-read (no sticky unread).
- Nav: channel `HasUnread` + direct `TownhallNavigationItem.HasUnread` with accent unread dot.
- Agent Panel `DraftInput` remains **panel-local** for M5 (not dual-written to conversation draft map).
- M4 privacy: no public mirror of agent sends (existing tests remain green).
- In-memory only — no file I/O; M6 will persist the same field shape when authorized.

## Ownership choice (documented)

Draft and last-read maps live in Townhall presentation (`TownhallConversationUiState`), not in `IConversationStore`. The store remains domain history only. M6 may persist these presentation fields alongside conversation records without moving ownership into the store API unless a later plan amendment says otherwise.

## M6+ still unauthorized

Persistence/load-save/LKG, parity bridge, Agent Panel retirement, and later milestones remain out of scope until explicitly authorized.
