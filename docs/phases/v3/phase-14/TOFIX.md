# Phase 14: Unified Conversation Workspace — TOFIX

**Status:** F1 fixed (2026-07-21). Phase 14 engineering closeout remains complete; **human
acceptance still pending** after F1 evidence review. Phase 15 unauthorized.

**Source:** Human acceptance audit of the published M9 closeout at `e5c26d0`
(2026-07-21).

---

## Fixed — F1: Chat header showed the Townhall channel while an Agent DM was selected

**Severity:** High  
**Area:** Townhall conversation selection and chat header projection  
**Fixed:** 2026-07-21 — see `M9_F1_MANUAL_EVIDENCE.md` and `Phase14F1ConversationContextTests`

**Observed behavior:** After selecting an Agent direct conversation, the chat message panel
still displayed `#townhall-main` as the active conversation label. The selected DM and the
header therefore disagreed about which conversation owned the visible message surface.

**Expected behavior:** When an Agent DM is selected, the chat header and input context must
display that direct conversation. A `#channel-name` label must be shown only when the
corresponding public channel is selected.

**Root cause:** View placeholder/header logic consulted `ActiveChannelId` during the
selection transition before direct side effects cleared the stale channel id;
`ActiveConversationId` was published before `ApplyDirectSelection` completed.

**Fix:** Derive `ActiveConversationHeaderLabel` and `ActiveConversationInputPlaceholder` from
`ActiveConversationId`; reorder `SelectConversation`; bind `TownhallChatPanel` header and
input placeholder to those properties; sync nav list selection.

- [x] Automated regression coverage for channel→DM and DM→channel selection
- [x] Interactive Linux screenshot with matching DM header/input context
- [x] Replaced inaccurate M9 DM and 800×600 screenshot evidence
- [x] Re-ran build, focused tests, architecture tests, full suite, `git diff --check`
