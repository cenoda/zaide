# Phase 14 F1 — Conversation context fix evidence

**Finding:** TOFIX F1 — chat header/input showed `#townhall-main` while an Agent DM was selected  
**Date:** 2026-07-21  
**Environment:** Linux desktop, X11 `DISPLAY=:1`, 96×96 DPI  
**Baseline:** M9 closeout at `e5c26d0`; F1 fix at `09ba27a`

## Root cause

`TownhallView.UpdatePlaceholder()` consulted `ActiveChannelId` before direct context, and
`SelectConversation` raised `ActiveConversationId` before `ApplyDirectSelection` cleared the
stale channel id. Views observing selection mid-transition projected `#channel-name` while a
direct row was already selected.

## Fix summary

- `TownhallViewModel` exposes `ActiveConversationHeaderLabel` and
  `ActiveConversationInputPlaceholder` derived from `ActiveConversationId` (not `ActiveChannelId`).
- `SelectConversation` applies channel/direct side effects before publishing
  `ActiveConversationId`.
- `TownhallChatPanel` renders the active conversation header above the message list.
- `TownhallView` binds header and input placeholder to the ViewModel display-context properties.
- `TownhallNavigationPanel` clears opposing list selection when syncing channel vs direct.

## Automated verification (2026-07-21)

| Command | Result |
|---------|--------|
| `dotnet build Zaide.slnx` | **0 errors**, **4 warnings** (pre-existing test analyzer / unused event) |
| `dotnet test … ~Zaide.Tests.Features.Conversations` | **102** passed |
| `dotnet test … ~Zaide.Tests.Features.Townhall` | **117** passed (+5 F1 regression tests) |
| `dotnet test … ~Zaide.Tests.Features.Agents` | **178** passed |
| `dotnet test … ~Zaide.Tests.App.Shell` | **134** passed |
| `dotnet test … ~Zaide.Tests.Architecture` | **26** passed |
| `dotnet test Zaide.slnx` (full suite) | **2524** passed, 0 failed, 0 skipped |
| `git diff --check` | clean |

New tests: `Phase14F1ConversationContextTests` (channel→DM, DM→channel, header/input parity).

## Interactive Linux GUI verification

| Step | Result | Evidence |
|------|--------|----------|
| Select Direct “Zaide Agent” | **Pass** — header shows agent name; placeholder `Direct message with Zaide Agent`; `#townhall-main` not active | `townhall-dm-default-1280x800.png` (human-supplied acceptance capture) |
| Channel header only on channel select | **Pass** — `#townhall-main` header + `Message #townhall-main` placeholder when channel selected | `townhall-narrow-800x600.png` |
| Narrow layout | **Pass at supported minimum** — 960×600 window; 800×600 artifact is a crop, not actual 800-wide validation | `townhall-channel-narrow-960x600.png` + crop |

### Screenshot notes

- Added a human-supplied 1280×800 DM capture that visibly shows matching Direct selection,
  `Zaide Agent` header, and DM placeholder.
- Relabeled the earlier channel-context captures that had inaccurate `townhall-dm-*` names;
  they are not presented as DM evidence.
- `townhall-narrow-800x600.png` is an **800×600 crop** of the left workspace at the shell minimum
  height/width window (`960×600` client geometry). It is composition evidence only;
  actual 800-wide window behavior is not validated because `MainWindow.MinWidth = 960`.

## Cross-checks (unchanged)

| Claim | Status |
|-------|--------|
| M4 privacy (no public DM mirror) | **Confirmed** — no persistence/routing changes |
| M6 persistence / no auto-resume | **Confirmed** — no store changes |
| M8 Agent Panel retirement | **Confirmed** — no panel chrome restored |

## Phase disposition

F1 corrective milestone complete. Phase 14 **still pending explicit human acceptance**; do not
begin Phase 15.
