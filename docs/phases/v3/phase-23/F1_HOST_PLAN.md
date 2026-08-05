# F1 — Dedicated inspect host (option B)

One-page plan for moving Trace / Memory / Usage out of the chat-column
Auto-row stack. **Open-flag exclusivity is out of scope and must not change.**

## Decision: side sheet (not modal overlay)

| Option | Choice | Why |
|--------|--------|-----|
| **Side sheet** | **Selected** | Sits **beside** the message list inside the Townhall chat column’s **Star** band. Message list keeps a `1*` row/column; inspect content no longer stacks under chat as Auto rows. Chat remains vertically usable with one or all evidence surfaces open. |
| Modal / full-column overlay | Rejected for F1 | Covers messages and fights “inspect while chatting.” Command-palette-style dimmed overlay is the wrong model for durable evidence. |

Host lives **inside** Townhall (not shell-wide), still **outside** the chat message
surface: filter strip and composer stay full-width; only the Star band splits into
`messages | inspect sheet`.

## Concurrent open + stack rules

- **Flags stay independent:** `IsTracePanelOpen` / `IsMemoryPanelOpen` /
  `IsUsagePanelOpen` may all be true. Opening one does **not** close another.
- **Host visibility:** sheet is visible iff **any** of the three flags is true.
- **Inside the sheet:** open panels **stack** top → bottom (Trace, Memory, Usage)
  in a scrollable column. Closed panels stay `IsVisible = false` and take no
  space. No tab bar that implies exclusivity.
- **Width:** fixed usable sheet width (~320px) when open; zero footprint when
  all closed so the message list reclaims the Star band.

## Open / close / toggle contract

Unchanged semantics on `AgentTransparencyManagementViewModel`:

| Action | Contract |
|--------|----------|
| Toolbar / command **Open** | Sets that surface’s flag true; loads/refreshes that surface. |
| Toolbar **Toggle** | Open if closed, close if open (Phase 23 Session 2c). |
| Panel **Close** | Clears only that surface’s flag. |
| Multi-open | Allowed; host shows every open panel in the stack. |

Presentation only moves **where** panels are parented. Do not force mutual
exclusion, do not rename flags, do not alter command ids.

## F3 bundling?

**Do not implement F3 in this change.** F3 is empty/disabled chrome density
inside each panel. Host migration can land with current panel chrome; empty
affordance cleanup is a separate M–L pass after the layout host is stable.

## Out of scope

F5 (Settings schema), F7–F10, F12, product-readiness claims, V4 planning.
