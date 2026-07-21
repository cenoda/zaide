# Phase 14 M9 — Design brief (Townhall workspace vs `docs/DESIGN.md`)

**Date:** 2026-07-21  
**Scope:** Delivered Townhall unified conversation workspace after Agent Panel
retirement (M8). Comparison against `docs/DESIGN.md` only — not a brand redesign.

## Intent

Phase 14 ships a navigable conversation workspace (channels + private Agent DMs)
under Townhall. Visual goals remain: DesignSystem tokens, C# view construction,
dark monochromatic palette with blue accent, OS-native chrome, and progressive
enhancement for glass/blur.

## Comparison matrix

| DESIGN.md rule | Delivered Townhall state | Disposition |
|----------------|--------------------------|-------------|
| Prefer C# view construction (XAML last resort) | Townhall views built in C# (`TownhallView`, navigation, chat, input, people) | **Meets** |
| DesignSystem tokens / no ad-hoc palette sprawl | Uses `PaletteTokens`, `LayoutTokens`, `TextStyles`, `IconFactory` | **Meets** (one intentional low-alpha white overlay for hover/active rows, consistent with existing shell patterns) |
| System UI font | No bundled font; platform default | **Meets** |
| OS-native window chrome | Avalonia `MainWindow` title “Zaide”; no custom window controls | **Meets** |
| Spacing: ≥16px panel padding, 8px control gaps | Sidebar/chat/input use spacing tokens; layout remains usable at 800×600 | **Meets** (token-based; not a pixel-perfect audit of every edge) |
| Focus states clear but subtle | ListBox/TextBox use platform focus; primary controls focusable where keyboard path requires | **Meets with residual** — focus ring is platform/theme-dependent; not pixel-asserted in CI |
| Glass / blur progressive enhancement | Solid dark panel surfaces; no broken blur dependency on Linux | **Intentional limitation** — full glass/vibrant effect is aspirational platform enhancement, not Phase 14 exit |
| Animation 150–200ms cubic | Send button uses existing `Animations.CreateScaleBounce` pattern | **Meets** for that control; no new animation system |
| Discord-like composition is scaffolding, not target | Sidebar (People + Channels/Direct) + chat + input; editor remains right column | **Meets intent** — workspace is product-functional, not a Discord clone polish pass |
| Resize / narrow layout | Screenshots at 1400×900, 960×720, 800×600; no Agent Panel chrome | **Meets** with residual risk on extreme widths |
| Screen-reader / semantic controls | Accessible names on channel/direct lists, message input, send, new-message chip, people open-DM rows | **Best-effort meets** — not WCAG certification |

## Intentional limitations (not defects)

1. **No full glass/blur aesthetic** on Linux compositors — DESIGN.md already marks glass as progressive enhancement.
2. **Attachment “+” control** is visual-only in Phase 14 (named “Attachment (unavailable)”).
3. **People open-DM** is pointer-primary; keyboard path for existing directs is the Direct list (Enter/Space).
4. **Send affordance** is a styled `Border` with accessible name; keyboard send is Enter on the input (primary path).
5. **Non-virtualized message list** remains — no measured need for virtualization on realistic histories in this phase; residual risk for very large histories.
6. **Thin non-visual `IAgentPanelHost`** remains for execution seams (DF-001 residual) — not user-facing chrome.
7. **No streaming / retry / multi-window** presentation — product non-goals.

## Defects found in M9 closeout

**F1 (fixed 2026-07-21):** Selecting an Agent DM left the chat header and input placeholder
showing `#townhall-main`. Corrected in F1 — header shows agent identity; `#channel-name` only
when a public channel is selected. See `M9_F1_MANUAL_EVIDENCE.md`.

M9 applied a small naming fix for primary controls that lacked `AutomationProperties` names
(input, send, new-message chip, people rows). No layout redesign beyond the F1 conversation
header.

## Screenshots

See `evidence/m9/` and `M9_MANUAL_EVIDENCE.md`. High-DPI row **not available**
(display 96×96 DPI).
