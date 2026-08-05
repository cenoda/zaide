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

- **Mutual exclusivity (product choice, 2026-08-06):** opening Trace, Memory, or
  Usage **closes the other two**. At most one inspect surface is open.
- **Host visibility:** sheet is visible iff **any** of the three flags is true
  (in practice, at most one flag is true while the sheet is open).
- **Inside the sheet:** the single open panel is shown; closed panels stay
  `IsVisible = false`. Scrollable stack remains as a layout host (still fine if
  exclusivity is later relaxed).
- **Width:** fixed usable sheet width (~320px) when open; zero footprint when
  all closed so the message list reclaims the Star band.

## Open / close / toggle contract

| Action | Contract |
|--------|----------|
| Toolbar / command **Open** | Closes siblings, sets this surface’s flag true, loads/refreshes. |
| Toolbar **Toggle** | If open → close this only; if closed → open this (and close siblings). |
| Panel **Close** | Clears only that surface’s flag. |
| Multi-open | **Not allowed** — exclusivity enforced on open paths only. |

Do not rename flags or command ids. Exclusivity lives in
`AgentTransparencyManagementViewModel` open paths (`CloseSiblingInspectSurfaces`).

## F3 bundling?

**Do not implement F3 in this change.** F3 is empty/disabled chrome density
inside each panel. Host migration can land with current panel chrome; empty
affordance cleanup is a separate M–L pass after the layout host is stable.

## Out of scope

F5 (Settings schema), F7–F10, F12, product-readiness claims, V4 planning.
