# ISSUE-010: C# editor scroll jank from IndentGuideRenderer paint cost

**Label:** BUG
**Status:** closed
**Priority:** high
**Related:** Editor indent guides (Phase 2.1), separate from Phase 22.1
UI projection Post/coalesce (`2f5e9315`) and Phase 22.2 backend binding

## Description

Scrolling large indented `.cs` files felt slow-motion / janky. Markdown and
other non-`.cs` files scrolled fine. The Phase 22.1 Post/coalesce fix did **not**
resolve this symptom — a separate root cause.

## Steps to Reproduce

1. Open a large, multi-level indented C# file in the editor.
2. Scroll vertically (wheel or scrollbar).
3. Open a large `.md` file and scroll the same way.

**Expected behavior:** Smooth scroll on both; indent guides remain aligned on `.cs`.
**Actual behavior:** `.cs` scroll is sluggish / slow-motion; `.md` is fine.

## Hypothesis confirmation

| Check | Result |
|-------|--------|
| Indent guides enabled only for `.cs` (`EditorView.ApplyFileMode`) | Confirmed |
| `IndentGuideRenderer.Draw` short-circuits when `IsEnabled == false` | Confirmed — `.md` never pays paint cost |
| Hot path: per visible line `GetText` + per guide level **two** `GetVisualPosition` + `DrawLine` | Confirmed in pre-fix code |
| Language projection / LSP as primary cause | Ruled out for this symptom (`.cs`-only + Post/coalesce no help) |

Disabling guides (`IsEnabled = false`) is the recovery path: zero work in
`Draw` per scroll frame. Optimization keeps `.cs` enablement and makes `Draw`
cheap instead of leaving guides off.

## Resolution

- **Root cause:** `IndentGuideRenderer.Draw` called `TextView.GetVisualPosition`
  twice per indent level per visible line on every scroll paint. That is O(visible
  lines × levels × GetOrConstructVisualLine + visual-column conversion) — too
  expensive under continuous scroll on deep C#.
- **Fix:**
  1. Cache guide **level counts** per document version / indentation size
     (`IndentGuideLevelCache`).
  2. Place guides at monospaced **visual-column midpoints** via
     `WideSpaceWidth` (`IndentGuideMetrics.GetGuideViewportX`) — no
     `GetVisualPosition` in the draw loop.
  3. Keep `.cs`-only enablement; do not enable on `.md`.
- **Automated tests:** `IndentGuideMetricsTests`, `IndentGuideLevelCacheTests`
  (level counts, tabs/spaces, version and indent-size invalidation, midpoint X).
- **Commit:** `538bd908`
- **Closed date:** 2026-08-01

## Manual verification

Run on Ready build when convenient (main check is scroll feel):

1. **Large indented `.cs`:** open a deep C# file; scroll rapidly — should feel
   responsive (no slow-motion). Guides still align under nested blocks, tabs,
   and mixed whitespace; blank/whitespace-only lines still omit guides.
2. **Guides still correct:** multi-level vertical lines sit at centers of each
   indent block and track horizontal scroll.
3. **`.md` unchanged:** markdown still has guides off and remains fast.
4. **Not Phase 22.2 / language projection:** no LSP or projection wiring changes
   in this fix; Post/coalesce from `2f5e9315` left in place.
