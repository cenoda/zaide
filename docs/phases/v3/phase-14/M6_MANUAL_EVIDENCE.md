# Phase 14 M6 — Manual restart smoke evidence

**Milestone:** Persistence + recovery (schema v1 file store)  
**Date:** 2026-07-20  
**Environment:** Linux desktop; automated recovery matrix green; interactive GUI rows **not validated on display** in agent session (same limitation as M3–M5).

## Automated recovery matrix (proxy for restart behavior)

| Case | Result |
|------|--------|
| Round-trip (entries, drafts, last-read, active) | Pass — `ConversationPersistenceTests.RoundTrip_*` |
| Missing file → seed defaults | Pass |
| Corrupt JSON → LKG fallback | Pass |
| Unsupported future schema → no LKG overwrite | Pass |
| Interrupted write (temp left) → main/LKG intact | Pass |
| Atomic replace (complete JSON) | Pass |
| No auto-resume after load | Pass — `IsDirectSendBusy` false; no execution stub invoked on reload |
| Idempotent entry ids | Pass |
| Settings path isolation | Pass — `conversations/conversations.json` separate from `settings.json` |

## Interactive smoke checklist (not run in agent session)

1. Send channel + DM messages; set per-conversation drafts; leave one nav item unread; note active selection.
2. Quit app fully.
3. Relaunch → expect history, drafts, unread affordance, and selection restored; **no** spontaneous LLM call.
4. Corrupt `conversations.json` (keep `.lastknowngood`) → app starts with LKG data or empty seed; no crash.
5. Delete store file → seeded empty session; settings and LLM configuration unchanged.

## Store location (D15)

- **Isolation:** application-lifetime (single file per user config dir; not workspace-root scoped).
- **Path:** `{SettingsPathResolver.GetSettingsDirectory()}/conversations/conversations.json`
- **LKG:** `conversations.json.lastknowngood`
- **Temp:** `conversations.json.tmp`

## Schema v1 summary

Root document `schemaVersion: 1` with:

- `channels[]` — `{ id, name, pinned }`
- `conversations[]` — `{ id, kind, participants[], entries[] }`
- `activeConversationId`
- `drafts` — map conversation id → string (empty omitted on save)
- `lastReadEntryIds` — map conversation id → entry id

**Ownership:** Conversations feature owns file I/O and aggregate hydration; Townhall owns presentation maps via `IConversationWorkspacePersistenceBridge`.
