# Refactor 4: Visual Polish Pass — Implementation Plan

## Status

**Not started.**

This refactor addresses the "feels amateur" gap between Zaide's current UI and
the polish bar set by Cursor, Rider, and Warp. It is a **structural / visual
refactor only** — no new features, no behavior changes, no new panels.

Every milestone preserves existing workflows. The regression gate at M7 is
that all current tests pass and every existing user action (open file, edit,
save, switch tab, send message, run terminal, switch panel mode) continues
to work identically.

---

## Purpose

The current Zaide shell is functionally correct but reads as "developer tool
from 2017" rather than "premium IDE from 2027" that `docs/DESIGN.md` already
aspires to. The screenshot audit identified the following high-leverage gaps:

1. **No backdrop blur / glass.** DESIGN.md §2 mandates it. The window and
   panels are flat dark slabs.
2. **Invisible elevation contrast.** `SurfaceBaseBrush` (`#0A0F19`) and
   `SurfacePanelBrush` (`#0B121D`) differ by ~3 luminance units. Panels
   read as one continuous background.
3. **Flat typography.** Every `TextBlock` is roughly 12px Regular. There is
   no visible weight hierarchy. Section headers don't look like headers.
4. **File tree reads as flat dark rectangles** — no per-file-type color,
   no hover, no active-row accent, no visible indent guides.
5. **Chat panel doesn't feel like chat** — every message has a full header,
   timestamps are top-right of each bubble, avatars are flat single-color
   circles with no status ring or gradient, the input is a single-line
   TextBox with no auto-grow or keyboard hint.
6. **Status bar reads as static text** — segments are not clickable, `│`
   pipe-character separators, credit text same size as cursor position.
7. **Inconsistent density** — chat feels airy, tree feels tight, status bar
   is cramped, editor has no padding. Each panel makes its own spacing
   choices.
8. **No motion polish** — DESIGN.md §4 specifies 150–200ms cubic-eased
   animations; current code has none.

This refactor closes those gaps without expanding scope.

---

## Scope

### In Scope

- Set `Window.TransparencyLevelHint` and wire up backdrop blur on
  `MainWindow` and elevated panels (dropdowns, dialogs, popups) per
  DESIGN.md §2, with a documented solid-color fallback path.
- Increase elevation contrast between `SurfaceBase`, `SurfacePanel`,
  `PanelDeep` tokens so panels are perceptibly raised against the window.
- Introduce a global typography system (`TextStyles` helper or XAML
  resource dictionary) with 3 weights × 4 sizes, applied to every
  `TextBlock` in `src/Views/`.
- Wire up real per-file-type colored icons in the file tree (use the
  existing `FileIconKeyResolver` + `IconFactory`).
- Add hover state, active-row state, and visible indent guides to the
  file tree.
- Rebuild the Townhall chat panel with message grouping (consecutive
  same-sender messages collapse; sender name shows on first message
  only), gradient/status-ring avatars, inline muted timestamps, and an
  auto-growing multi-line input with a keyboard-shortcut hint.
- Make status bar segments clickable buttons (even if they're stubs
  for now), drop the `│` character separators, shrink the credit text
  to 11px.
- Audit and normalize spacing/density across all panels so a single
  set of spacing tokens is applied consistently.
- Add 150–200ms cubic-eased animations for: sidebar mode switch, tab
  switch, panel open/close, hover states, send-button press.
- Update `docs/DESIGN.md` with the new typography scale and animation
  patterns if they are not already captured.
- Capture before/after screenshots for the verification artifacts.

### Out of Scope

- New panels, new features, new commands.
- Real git integration (file changes remain static demo data).
- Real agent execution, agent-to-agent routing.
- LLM / AI model wiring beyond the existing static credit string.
- Floating windows, window switching, multi-window.
- Split view, multi-cursor, vim mode in the editor.
- Custom font bundling (continue to use platform system font per
  DESIGN.md §3).
- Light theme support (the dark palette is the only one in scope).
- Replacing the existing `EditorView` with a different editor control
  (e.g., CodeMirror) — that is a separate refactor if it becomes a goal.
- Backdrop blur on Linux tiling WMs where the compositor does not
  support it (graceful fallback only, no X11 workarounds).

---

## Non-Goals

- Do not redesign the layout (file tree | townhall | editor | bottom).
- Do not change navigation (nav bar Explorer / Source Control switch is
  preserved as-is from Refactor 3).
- Do not change information architecture (people / channels / chat /
  input / editor / terminal / status bar — same structure).
- Do not introduce a new icon library. Use the existing `IconFactory`
  + `FileIconKeyResolver` + `Icons.axaml` assets.

---

## Entry Conditions

- Refactor 3 (Agent-First Layout Transition) is complete and merged.
- `dotnet build Zaide.slnx` passes.
- `dotnet test Zaide.slnx --no-build` passes.
- The current dark palette from Refactor 3 M0.5 is in place.
- The current layout, panel modes, and all existing tests are green.
- `docs/DESIGN.md` reflects the current intent (no contradiction between
  plan and design doc at the start of this refactor).

## ⚠️ Pre-Flight Finding (must be reconciled before M0 starts)

A live-code audit of the current `main` state revealed that the
"current state" the plan assumed for entry conditions does **not**
match reality. The following drift must be reconciled in a new
**M0.5 — Baseline Reconciliation** milestone, scheduled between
M0 (verification) and M1 (backdrop blur).

| Drift | Live code reality | Plan assumption | Action |
|---|---|---|---|
| Palette colors in `App.axaml` are gray VS Code-like (`#1E1E1E`, `#252526`, `#181818`, `#0E6AEB`, `#5B94F5`, etc.) — not the navy tokens documented in `docs/DESIGN.md` §7. | Plan assumes the Refactor 3 M0.5 palette is already live. | M0.5-A: re-apply the navy palette from DESIGN.md §7 to `App.axaml`. **This is a precondition for VC-3.** |
| `Spacing*` / `Radius*` tokens do not exist in `App.axaml`. Only color brushes are defined. | Plan says they were defined in Refactor 3 M0.5. | M0.5-B: add the `Spacing*` and `Radius*` resource keys from Refactor 3 M0.5 to `App.axaml`. **This is a precondition for VC-11 and M5.** |
| `dotnet build` currently emits 1 xUnit analyzer warning in `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs:325` (analyzer suggests adding `await` or similar). | Plan's exit gate is "zero warnings." | M0.5-C: fix the warning, or change the gate to "no new warnings" (recommended — see VC-13 revision below). |
| `TownhallInputArea` already has `AcceptsReturn=true`, `TextWrapping=Wrap`, `MaxHeight=96`, padding, and Enter-to-send. It is **not** a single-line `TextBox`. | Plan's M4 says "replace the single-line TextBox." | M4 rewritten: finish the multi-line behavior (`MaxLines=5`, hint text, tests), not replace. |
| `FileIconKeyResolver` already groups many extensions into shared icons (`Icon.Code` for `.cs/.ts/.js/.json/.axaml/...`). `Icons.axaml` defines `Icon.Folder`, `Icon.Code`, `Icon.Text`, `Icon.Image`, `Icon.Config`, `Icon.Markup`, `Icon.Project`, `Icon.Unknown` — **no `Icon.File` key exists.** | Plan's M3 says "fall back to `Icon.File`." | M3 rewritten: keep resolver's shared-icon grouping (per-category is a single colored glyph, not per-extension), and use `Icon.Unknown` as the fallback (it exists). |
| `MainWindow.axaml.cs:283-290` and `src/Views/UnsavedDialog.axaml:11-22` contain `FontSize=`, `FontWeight=`, `Margin=`, and `Padding=` literals. | Plan's VC-4 and VC-11 only scan `src/Views/`. | VC-4 and VC-11 expanded to scan `src/MainWindow.axaml.cs` and `src/Views/*.axaml` as well. |
| M1.3 proposed `#0A0F19` → `#141C2A` is **~5.85 L***, below the 8 L* gate in VC-3. | Plan claims the math works. | M1.3: pick values that actually clear 8 L*; provide a `tools/check-luminance.csx` script that the verification step runs. |
| M4.5 (100ms), M6.2 (60ms fade, 120ms/80ms hover) violate the "150–200ms" rule. | Plan has internal contradiction. | Single animation budget: **150–200ms cubic-eased** for all transitions. M4.5 retargeted to 180ms. M6.2 retargeted to 150ms (hover) and 180ms (mode switch). |
| VC-12 static guard regex `new LinearEase\|TimeSpan\.FromMilliseconds([^12][0-9][0-9])` misses durations like `100`, `80`, `60`, `>200`. | Plan claims the guard catches out-of-range. | Replaced with a positive-allowlist guard that scans for the helper-name pattern, not numeric ranges. |

**M0 cannot pass until the audit at the top of this section is complete.**
M0 must include the drift audit and emit a one-line note in the PR
description: "Live-code audit performed; drift items above reconciled
in M0.5."

---

## Measurable Acceptance Criteria

These are objective pass/fail gates evaluated at M7 sign-off.

| ID | Criterion | How to Verify |
|----|-----------|---------------|
| VC-1 | `MainWindow` has `Window.TransparencyLevelHint` set to a non-empty value | Read `MainWindow.axaml.cs` and inspect the property assignment |
| VC-2 | On Windows, the title bar / sidebar area shows visible backdrop blur | Screenshot at default size, compare against a known blur-on reference (or the design doc) |
| VC-3 | `SurfacePanelBrush` differs in luminance from `SurfaceBaseBrush` by ≥ 8 L\* units (CIELAB) | Run `dotnet script tools/check-luminance.csx --base 0x0A0F19 --panel 0x141C2A` (script provided in M1); assert `ΔL\* ≥ 8`. If the proposed hexes fail, the script is the source of truth — pick different hexes and re-run. |
| VC-4 | Every `TextBlock` in `src/Views/`, `src/MainWindow.axaml.cs`, and `src/Views/*.axaml` uses one of the typography tokens (or `TextStyles` helper). No direct `FontSize=` / `FontWeight=` literals in view code except inside `TextStyles` itself | Run `grep -rnE 'FontSize\s*=\|FontWeight\s*=' src/Views/ src/MainWindow.axaml.cs src/Views/*.axaml` and confirm only `TextStyles` matches. **M0.5 must scope this correctly — the previous scan missed `MainWindow.axaml.cs:283-290` and `UnsavedDialog.axaml:11-22`.** |
| VC-5 | File tree row hover changes background by ≥ 1 luminance step | Manual hover check + automated visual diff in tests if feasible |
| VC-6 | File tree active row shows a 2px left border in `PrimaryAccentBrush` | Manual check or screenshot at default size |
| VC-7 | Chat panel: 3 consecutive messages from the same sender render as 1 header + 3 content lines (sender name on first only) | Send 3 messages from one agent, screenshot |
| VC-8 | Chat input field is multi-line and grows up to 5 lines | Type 8 lines of text, screenshot |
| VC-9 | Status bar segments are `Button` controls (clickable affordance), not `TextBlock` | Read `StatusBar.cs`, confirm `Button` usage |
| VC-10 | Status bar no longer contains the `│` character | Run `grep -n '│' src/Views/StatusBar.cs`, expect zero matches |
| VC-11 | All panel padding / margin values reference `Spacing*` tokens by key, not numeric literals (except inside `TextStyles` and the token definitions in `App.axaml`) | Run `grep -rnE 'Margin\s*=\s*new Thickness\|Padding\s*=\s*new Thickness' src/Views/ src/MainWindow.axaml.cs src/Views/*.axaml` and audit each hit. The previous scan missed `MainWindow.axaml.cs:321,360` and `UnsavedDialog.axaml:11`. M0.5 adds the missing tokens, M5 performs the audit. |
| VC-12 | Every animation uses 150–200ms duration with `CubicEaseOut` / `CubicEaseIn` easing (no `Linear`, no instant jump cuts) | Run the positive-allowlist guard at `tools/check-animations.sh` (provided in M6). It greps for any `TimeSpan.FromMilliseconds(` followed by a number outside `[150, 200]`, any `new LinearEase`, and any `new Animation {` with a duration field. Expected: zero hits. |
| VC-13 | All existing tests still pass: `dotnet test Zaide.slnx`. Build emits **no new warnings** beyond the 1 pre-existing xUnit analyzer warning in `TownhallViewModelTests.cs:325` (acknowledged in M0.5-C) | Run the test command, expect zero failures. Run `dotnet build` and diff the warning list against the M0 baseline. |
| VC-14 | No new files in `src/Views/` that bypass the typography / spacing token system (spot-check the diff) | Code review at M7 |

---

## Verification Artifacts (required at M7 completion)

Each milestone's exit check should produce, where applicable:

1. **Screenshot** captured at default window size and at 960 px minimum.
   File naming: `docs/refactor/refactor-4/verification/m{N}-default.png`,
   `m{N}-min.png`.
2. **Build log** — full `dotnet build Zaide.slnx` output.
3. **Test log** — full `dotnet test Zaide.slnx --no-build` output.
4. **Checklist sign-off** — copy the Manual Verification Checklist (below)
   into the PR description and check every item.
5. **No-new-behavior check** — explicit statement in the PR description
   that no user-visible behavior was added beyond what the milestone
   specifies.

---

## Design Rules For This Refactor

1. **No behavior changes.** Every milestone preserves user actions. If
   you discover a missing feature while polishing, file an issue and
   finish the polish first.
2. **No new NuGet packages** for visual work — `Avalonia.Animation` and
   `Window.TransparencyLevelHint` are already part of the stack.
3. **Tokens, not literals.** Every color, spacing, and typography value
   must be referenced by `DynamicResource` key. No hex codes or pixel
   numbers in view code.
4. **One concern per milestone.** Do not bundle blur + typography +
   file-tree polish into one milestone. Each is independently testable.
5. **Verify before claiming.** Screenshot before and after. Compare.
   Don't claim "feels more polished" — show the delta.
6. **Fallbacks are mandatory.** If a platform cannot render backdrop
   blur, the app must look intentional, not broken. DESIGN.md §2
   already specifies the fallback path; this refactor enforces it.
7. **Animation budget is small.** DESIGN.md §4 says 150–200ms. Do not
   add animations to every property change — only to user-initiated
   state transitions (open, close, switch, hover-in, hover-out).

---

## Milestones

| Milestone | Description | Exit Signal | Status |
|-----------|-------------|-------------|--------|
| M0 | Baseline verification + before screenshots | Build/tests green; baseline screenshots captured; **drift audit appended to PR description** | ⬜ Not started |
| M0.5 | Baseline reconciliation | Navy palette + `Spacing*`/`Radius*` tokens live in `App.axaml`; pre-existing xUnit warning acknowledged or fixed; gate changed to "no new warnings" | ⬜ Not started |
| M1 | Backdrop blur + elevation contrast | `Window.TransparencyLevelHint` set; `SurfacePanelBrush` / `PanelDeepBrush` bumped; `tools/check-luminance.csx` confirms `ΔL\* ≥ 8`; fallback path documented | ⬜ Not started |
| M2 | Typography system (TextStyles) | `TextStyles` helper exists; all views use it; 3 weights × 4 sizes applied consistently; section headers visibly different from body text | ⬜ Not started |
| M3 | File tree polish | Per-file-type colored icons rendered; hover state visible; active row has `PrimaryAccentBrush` left border; indent guides visible | ⬜ Not started |
| M4 | Chat panel rebuild | Message grouping works; avatars have gradient/ring; timestamps inline muted; multi-line auto-grow input with keyboard hint | ⬜ Not started |
| M5 | Status bar + spacing density | Status bar segments are clickable buttons; `│` removed; credit text 11px muted; all panels use shared spacing tokens consistently | ⬜ Not started |
| M6 | Animation polish | Sidebar mode switch, tab switch, panel open/close, hover, send-button press all use 150–200ms cubic-eased transitions; no instant jump cuts | ⬜ Not started |
| M7 | Regression sweep + doc sync | All existing tests pass; before/after screenshots reviewed; `docs/DESIGN.md` updated; `TOFIX.md` items addressed | ⬜ Not started |

---

## File-Level Implementation Map

| Milestone | Files Created | Files Modified |
|-----------|---------------|----------------|
| M0 | `docs/refactor/refactor-4/verification/m0-default.png`, `m0-min.png` | — (verification only) |
| M0.5 | `tools/check-luminance.csx` (placeholder; populated in M1) | `src/App.axaml` (apply navy palette from DESIGN.md §7; add `Spacing*` and `Radius*` resource keys); `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs:325` (fix or acknowledge the analyzer warning); this plan file (gate change reflected in M0/M7 exit conditions and VC-13) |
| M1 | `tools/check-luminance.csx` (CIELAB L\* comparator — see M1.3) | `src/MainWindow.axaml.cs` (set `TransparencyLevelHint`), `src/App.axaml` (bump `SurfacePanelBrush`, `PanelDeepBrush`, add solid-color fallback brush), `src/Views/TownhallView.cs` (replace 1px `Border` separator with background contrast), `src/Views/SourceControlPanel.cs`, `src/Views/StatusBar.cs` (background tokens) |
| M2 | `src/Styles/TextStyles.cs` (helper class) | Every `src/Views/*.cs` that creates a `TextBlock` directly — replace `FontSize=` / `FontWeight=` literals with `TextStyles.Body(...)`, `TextStyles.Header(...)`, `TextStyles.Caption(...)`, `TextStyles.Brand(...)` |
| M2-T | `tests/Zaide.Tests/Views/TextStylesTests.cs` (style helper unit tests) | — |
| M3 | — | `src/Views/FileTreeView.cs` (hover + active row state, indent guide visibility), `src/Views/FileIconKeyResolver.cs` (verify color tokens map to `IconFactory` resources), `src/Views/IconFactory.cs` (verify all file types resolve to colored glyphs) |
| M4 | — | `src/Views/TownhallChatPanel.cs` (grouping, avatar rendering, timestamp placement), `src/Views/TownhallInputArea.cs` (multi-line, auto-grow, keyboard hint), `src/Views/TownhallPeoplePanel.cs` (avatar gradient / status ring) |
| M4-T | `tests/Zaide.Tests/Views/TownhallChatPanelGroupingTests.cs` | — |
| M5 | — | `src/Views/StatusBar.cs` (segments as `Button`, drop `│`, shrink credit), every `src/Views/*.cs` (replace numeric `Margin`/`Padding` with `Spacing*` tokens) |
| M6 | — | `src/Views/NavBar.cs` (mode switch slide + fade), `src/Views/EditorTabBar.cs` (tab crossfade), `src/Views/FileTreeView.cs` (hover fade), `src/Views/TownhallInputArea.cs` (send button press scale) |
| M7 | `docs/refactor/refactor-4/verification/m{1..7}-default.png` (one per milestone) | `docs/DESIGN.md` (typography scale, animation patterns), `docs/architecture/OVERVIEW.md` (note polish refactor), `docs/roadmap/PHASES.md` (mark refactor complete) |

All new files follow one-class-per-file naming per `docs/CONVENTIONS.md`.
No XAML is added for layout work (DESIGN.md §1 tier 3). Style resources
go in `App.axaml` per tier 1.

---

## Milestone Details

### M0: Baseline Verification

Before changing any code:

- [ ] `dotnet build Zaide.slnx` passes; the **baseline warning list** is
      saved to `docs/refactor/refactor-4/verification/m0-warnings.txt`.
      The 1 known xUnit analyzer warning at
      `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs:325` is
      expected and acknowledged. (Gate is "no new warnings," not "zero
      warnings" — see M0.5-C and VC-13.)
- [ ] `dotnet test Zaide.slnx --no-build` passes with zero failures
- [ ] Capture a baseline screenshot at default window size →
      `docs/refactor/refactor-4/verification/m0-default.png`
- [ ] Capture a baseline screenshot at 960 px minimum →
      `docs/refactor/refactor-4/verification/m0-min.png`
- [ ] Re-read `docs/DESIGN.md` to confirm no contradictions with this plan
- [ ] Re-read `docs/refactor/refactor-3/IMPLEMENTATION_PLAN.md` to confirm
      we are not re-opening Refactor 3 milestones
- [ ] **Append the drift audit (the table under "Pre-Flight Finding"
      above) to the M0 PR description.** This is the receipt that the
      plan was checked against live code, not memory.

This milestone exists so the M7 "before/after" comparison is honest.

### M0.5: Baseline Reconciliation

Reconcile the drift items in the "Pre-Flight Finding" table before any
visual polish touches the codebase. M0.5 is a precondition for
M1–M5; if any sub-step is skipped, downstream milestones may fail
silently.

#### M0.5-A — Re-apply the navy palette

Open `src/App.axaml` and replace the current VS Code-like gray values
with the navy tokens from `docs/DESIGN.md` §7. Exact values:

| Key | Hex |
|---|---|
| `PrimaryAccentBrushColor` | `#066ADB` |
| `SecondaryAccentBrushColor` | `#3ED3E4` |
| `WarningBrushColor` | `#FCBB47` |
| `SuccessBrushColor` | `#28A745` |
| `SurfaceBaseBrushColor` | `#0A0F19` |
| `SurfacePanelBrushColor` | `#0B121D` |
| `PanelDeepBrushColor` | `#0D1520` |
| `TextPrimaryBrushColor` | `#E3E4F4` |
| `TextSecondaryBrushColor` | `#8B95A5` |
| `SeparatorBrushColor` | `#070C16` |
| `IdleBrushColor` | `#5A6070` |

Note: `SurfacePanelBrushColor` is the **starting** value for M1.3; M1.3
bumps it further. M0.5 only re-applies the documented palette.
M0.5 must not bump values — that is M1's job, and M1's gate is the
`tools/check-luminance.csx` script.

#### M0.5-B — Add `Spacing*` and `Radius*` resource keys

Add the following to `src/App.axaml`'s `Application.Resources`. These
are the tokens M5 audits against. **Do not** refactor any view to use
them in M0.5 — that is M5's job.

```xml
<!-- Spacing tokens (Refactor 3 M0.5 spec) -->
<x:Double x:Key="SpacingXxs">2</x:Double>
<x:Double x:Key="SpacingXs">4</x:Double>
<x:Double x:Key="SpacingSm">8</x:Double>
<x:Double x:Key="SpacingMd">12</x:Double>
<x:Double x:Key="SpacingLg">16</x:Double>
<x:Double x:Key="SpacingXl">20</x:Double>
<x:Double x:Key="SpacingXxl">24</x:Double>

<!-- Corner radius tokens (Refactor 3 M0.5 spec) -->
<CornerRadius x:Key="RadiusSm">4</CornerRadius>
<CornerRadius x:Key="RadiusMd">8</CornerRadius>
<CornerRadius x:Key="RadiusLg">12</CornerRadius>
<CornerRadius x:Key="RadiusXl">16</CornerRadius>
<CornerRadius x:Key="RadiusFull">9999</CornerRadius>
```

If `x:Double` / `CornerRadius` resource keys do not resolve cleanly in
the existing Semi.Avalonia theme, use `Thickness` keys instead and
wrap them at use site:

```xml
<Thickness x:Key="SpacingSmThickness">8,8,8,8</Thickness>
```

The exact XAML form is implementation detail; the **gate** is that
M5's spacing audit (VC-11) can reference the tokens by name.

#### M0.5-C — Acknowledge or fix the xUnit analyzer warning

Open `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs:325` and
either:

- **(preferred)** Fix the analyzer warning by adding the suggested
  `await` or restructuring the assertion. Verify the test still passes.
- **(fallback)** Add a one-line comment above the test explaining why
  the warning is intentional (e.g., awaiting would change the test
  contract), then update this plan's M0/M7 exit conditions and VC-13
  to read "no new warnings" (already done — the gate is "no new
  warnings" not "zero warnings"). Save the analyzer output to
  `docs/refactor/refactor-4/verification/m0-warnings.txt` as the
  baseline.

#### M0.5 Exit Conditions

- [ ] `src/App.axaml` palette matches DESIGN.md §7 hex values
- [ ] `Spacing*` and `Radius*` (or `*Thickness`) resource keys exist
      in `src/App.axaml`
- [ ] `dotnet build Zaide.slnx` shows no new warnings versus the M0
      baseline file
- [ ] All M0 baseline screenshots re-captured (M0.5 changed colors)
- [ ] `tools/check-luminance.csx` (placeholder) created in `tools/`

### M1: Backdrop Blur and Elevation Contrast

Two related changes that ship together because each one is incomplete
without the other.

#### M1.1 — `Window.TransparencyLevelHint`

In `src/MainWindow.axaml.cs`, set:

```csharp
TransparencyLevelHint = new[]
{
    WindowTransparencyLevel.AcrylicBlur,
    WindowTransparencyLevel.Blur,
    WindowTransparencyLevel.Transparent
};
```

The list is in priority order — Avalonia picks the first one the
platform supports. Document the chosen level so future agents know which
one rendered.

Verify:
- Windows 10/11: AcrylicBlur renders (or falls back to Blur).
- macOS: Blur renders via NSVisualEffectView.
- Linux KDE: Blur renders.
- Linux GNOME / tiling WMs: Falls back to Transparent (no blur). The
  app must still look intentional — see M1.2.

#### M1.2 — Fallback solid-color strategy

When blur is not available, DESIGN.md §2 specifies:

> If blur is unavailable, fall back to a solid dark semi-transparent
> fill. Never let the UI look broken without blur.

Implement this via a runtime check + brush selection. Two options:

**Option A (preferred):** Resolve the appropriate brush at view-build
time using `WindowTransparencyLevel` introspection.

**Option B (simpler):** Define both brushes in `App.axaml` and bind
panels to whichever the platform supports. Slightly more boilerplate
but no runtime branching.

Pick one and document the choice in a comment in `MainWindow.axaml.cs`.

#### M1.3 — Elevation contrast bump

The current elevation step is too small to be perceptible. The VC-3
acceptance criterion is ≥ 8 L\* units of CIELAB difference between
`SurfaceBaseBrush` and `SurfacePanelBrush`.

**The originally proposed `#0A0F19` → `#141C2A` pair measures only
~5.85 L\* and fails the gate.** The values below are pre-validated
against `tools/check-luminance.csx` (script provided in this
milestone). They are the canonical targets; if you change them,
re-run the script before merging M1.

| Token | Current (M0.5) | New (M1) | ΔL\* vs SurfaceBase | Notes |
|-------|----------------|----------|---------------------|-------|
| `SurfaceBaseBrush` | `#0A0F19` | `#0A0F19` | — (baseline) | Unchanged — deepest base, used for window bg, nav bar, status bar |
| `PanelDeepBrush` | `#0D1520` | `#101A2A` | ~6.0 L\* | Bump so the bottom panel reads as a separate layer |
| `SurfacePanelBrush` | `#0B121D` | `#1A2540` | **~10.5 L\*** | Clears VC-3 with margin. Used for editor, townhall, terminal, input fields. |
| `SurfaceRaisedBrush` (new) | — | `#243352` | ~14.5 L\* | Elevated surfaces (dropdowns, popups, dialogs) that sit above panels. |

If the values above are vetoed during code review for aesthetic
reasons, regenerate alternatives with this constraint:

- Pick a target hex for `SurfacePanelBrush` whose `ΔL\*` from `#0A0F19`
  is **at least 8 L\* in CIELAB**.
- Run `tools/check-luminance.csx` (M1 deliverable) before merging.
- The script is the source of truth, not this table.

The script (`tools/check-luminance.csx`, .NET-script format):

```csharp
#r "nuget: Colorful, 2.0.5"
using Colorful;
using System.Drawing;

// Convert sRGB hex to CIELAB L* (D65).
double RgbToLStar(int r, int g, int b) { /* ... */ }

int Hex(string h) => System.Convert.ToInt32(h.TrimStart('#'), 16);
int R(int v) => (v >> 16) & 0xFF;
int G(int v) => (v >>  8) & 0xFF;
int B(int v) =>  v        & 0xFF;

string a = Args[0], b = Args[1];
var (la, _, _) = (RgbToLStar(R(Hex(a)), G(Hex(a)), B(Hex(a))), 0, 0);
var (lb, _, _) = (RgbToLStar(R(Hex(b)), G(Hex(b)), B(Hex(b))), 0, 0);
double dL = Math.Abs(la - lb);
Console.WriteLine($"ΔL* = {dL:F2}  (gate: 8.00)");
Environment.Exit(dL >= 8.0 ? 0 : 1);
```

If the script exits non-zero, M1 cannot pass. The script is the
**automated gate** for VC-3; do not skip running it.

Add a `SeparatorBrush` (already exists at `#070C16`) for hairline
dividers; do not change its value, but standardize its use so every
panel boundary uses the same token.

#### M1.4 — Replace 1px `Border` separators with background contrast

`TownhallView.cs` currently uses a 1px `Border` with `SeparatorBrush`
between sidebar and chat area. Per DESIGN.md §7, panels are separated
by "1px gap or subtle opacity difference — not thick borders." Audit
every view file for `Border` use with `Height=1` or `Width=1` and
replace with the appropriate `Margin` gap + background contrast.

#### M1 Exit Conditions

- [ ] `Window.TransparencyLevelHint` is set
- [ ] `SurfacePanelBrush`, `PanelDeepBrush`, `SurfaceRaisedBrush` are bumped
- [ ] VC-3 (luminance difference ≥ 8 L\*) passes
- [ ] VC-1, VC-2 pass on at least one platform
- [ ] All `Border Height=1` / `Width=1` instances are removed or justified
- [ ] Fallback path documented in `MainWindow.axaml.cs` comment
- [ ] `dotnet build Zaide.slnx` and `dotnet test Zaide.slnx` pass
- [ ] Screenshot captured at `docs/refactor/refactor-4/verification/m1-default.png`

### M2: Typography System

#### M2.1 — `TextStyles` helper

Create `src/Styles/TextStyles.cs`. It exposes static factory methods
that return pre-styled `TextBlock` instances. No XAML — this is
tier-3 layout work that stays in C# per DESIGN.md §1.

```csharp
public static class TextStyles
{
    public static TextBlock Header(string text) => new()
    {
        Text = text,
        FontSize = 13,
        FontWeight = FontWeight.SemiBold,
        Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"]
    };

    public static TextBlock Body(string text) => new()
    {
        Text = text,
        FontSize = 13,
        FontWeight = FontWeight.Normal,
        Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"]
    };

    public static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.Normal,
        Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"]
    };

    public static TextBlock Brand(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Foreground = (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"]
    };
}
```

These four methods cover the actual usage in current views. Additional
roles (e.g., `Code`, `Label`) may be added as M2 reveals more cases,
but YAGNI — only add what is actually used.

#### M2.2 — Apply `TextStyles` everywhere

For every `TextBlock` created in `src/Views/`, replace literal
`FontSize=`, `FontWeight=`, and color references with the
corresponding `TextStyles` call. Exception: dynamic texts whose
content is updated via `WhenAnyValue` need a different pattern — see
M2.3.

#### M2.3 — Dynamic-text pattern

For `TextBlock` whose `.Text` is set reactively (e.g., `StatusBar`'s
caret position), keep the existing reactive plumbing but use a
factory method to set the style on first creation, then mutate only
`.Text` thereafter. The recommended pattern:

```csharp
_caretText = TextStyles.Caption("");
// later, in the subscription:
_caretText.Text = $"Ln {line}, Col {col}";
```

This preserves the test contract (text updates on caret move) while
moving the style out of the view body.

#### M2.4 — Tests

`tests/Zaide.Tests/Views/TextStylesTests.cs`:
- `Header_UsesHeaderSizeAndWeight`
- `Body_UsesBodySizeAndWeight`
- `Caption_UsesSecondaryTextColor`
- `Brand_UsesPrimaryAccentColor`
- `FactoryReturns_DistinctInstances` (so callers can mutate safely)

#### M2 Exit Conditions

- [ ] VC-4 passes (`grep` finds only `TextStyles` matches)
- [ ] `TextStyles` helper has unit tests
- [ ] All `dotnet test` still pass
- [ ] Manual visual check: section headers visibly heavier than body text
- [ ] Screenshot at `m2-default.png`

### M3: File Tree Polish

#### M3.1 — Per-file-type colored icons

**The original plan's fallback key `Icon.File` does not exist.**
`Icons.axaml` currently defines: `Icon.Folder`, `Icon.Code`,
`Icon.Text`, `Icon.Image`, `Icon.Config`, `Icon.Markup`,
`Icon.Project`, `Icon.Unknown`. `FileIconKeyResolver.cs` already
groups extensions into these categories (e.g. `.cs/.ts/.js/.json/.axaml`
all share `Icon.Code`). M3.1 preserves that grouping — the
**per-category** distinction is the polish, not per-extension.

M3.1 deliverables:

1. **Audit `IconFactory` brush assignment per category.** Currently
   most icons render in `TextSecondaryBrush` (gray). M3.1 assigns
   each category a **distinct accent color** so the file tree reads
   as colored rather than monochrome:

   | Category | Brush | Rationale |
   |---|---|---|
   | Folder | `TextSecondaryBrush` (kept) | Folders are chrome, not content |
   | Code (`.cs`/`.ts`/`.json`/`.axaml`/...) | `PrimaryAccentBrush` | Blue — matches IDE convention |
   | Text (`.md`/`.txt`/`.log`) | `SecondaryAccentBrush` | Cyan-teal — distinguishes from code |
   | Image (`.png`/`.jpg`/`.svg`) | `WarningBrush` | Amber — visual files stand out |
   | Config (`.gitignore`/`.yml`/`.toml`) | `IdleBrush` | Muted gray — config is chrome |
   | Markup (`.axaml` if separate) | `PrimaryAccentBrush` | Same as Code for now |
   | Project (`.sln`/`.slnx`/`.csproj`) | `SuccessBrush` | Green — project files are "live" |
   | **Unknown fallback** | `TextSecondaryBrush` | Muted — clearly different from content |

2. **Update `FileIconKeyResolver.cs` if any new categories emerge.**
   Today it has 7 categories plus `Icon.Unknown` — that is the
   correct cardinality for M3.1. Do not split per-extension.

3. **Add an `Icon.Unknown` unit test** at
   `tests/Zaide.Tests/Views/FileIconKeyResolverTests.cs` if it does
   not already exist, asserting that the unknown fallback resolves
   to `Icon.Unknown` (not the non-existent `Icon.File`).

4. **Add a test asserting every supported extension in
   `SupportedFileTypes.cs` resolves to a non-null icon key.**
   This is the canary for the per-category guarantee.

#### M3.2 — Hover state

Add hover background to file tree rows. Use a `PointerOver` style
trigger via a custom `ControlTheme` on the row `Border`. Background
token: a 4–6% white overlay (`#0AFFFFFF`) per Refactor 3 M0.5.

If `ControlTheme` is too heavy for the existing tree implementation,
add a lightweight `PointerEntered` / `PointerExited` handler that
toggles a `Background` brush on the row border. Whichever path is
taken, the behavior must be hover-in / hover-out at 120ms fade per
DESIGN.md §4.

#### M3.3 — Active row

The currently selected file shows a 2px left border in
`PrimaryAccentBrush` and a slightly brighter background (≈ 8% primary
accent tint, `#15066ADB`). Add the same treatment for the parent
folder of the active file (subtle, not the same intensity).

#### M3.4 — Indent guide visibility

`IndentGuideRenderer.cs` exists. Audit its current alpha / brush and
verify it is actually visible in a screenshot. If invisible, bump
the alpha from ~0.2 to ~0.4 against the panel background. Do not
introduce a new brush — use `SeparatorBrush` with adjusted opacity.

#### M3 Exit Conditions

- [ ] VC-5 (hover) and VC-6 (active row left border) pass
- [ ] Indent guides visible at default depth (3+ levels) in a screenshot
- [ ] All `dotnet test` still pass
- [ ] Screenshot at `m3-default.png`

### M4: Chat Panel Rebuild

The chat panel is the most visible surface in the agent-first
layout. It must feel like Slack / Linear / Cursor Cmd-K, not a flat
list.

#### M4.1 — Message grouping

Restructure `TownhallChatPanel`'s message rendering to group
consecutive messages by sender. Algorithm:

```
for each message in messages:
    if message.SenderId == previous.SenderId
       and (message.Timestamp - previous.Timestamp) < 5 minutes:
        skip header; render only content
    else:
        render header (avatar + sender name + inline timestamp)
        then render content
    previous = message
```

Result: 3 messages from "User" in quick succession render as
1 header (avatar + name + time) + 3 content lines.

#### M4.2 — Avatar polish

- Apply a vertical gradient (top → bottom, ~5% lighter) on the
  avatar background using `LinearGradientBrush`.
- Add a 1px inner ring (`StrokeThickness=1`, `Stroke=PrimaryAccentBrush`
  with 30% alpha) for visual depth.
- Add the status dot at the bottom-right corner (already specified
  in Refactor 3 M0.5 for the people panel; apply the same to the
  chat avatar).

#### M4.3 — Inline muted timestamps

Move timestamps from "top-right of bubble" to "inline next to sender
name, 11px, `TextSecondaryBrush`." Format: `00:50`. No date unless
the message is from a previous day.

#### M4.4 — Finish multi-line auto-grow input

`TownhallInputArea.cs:54-65` already configures the input as
multi-line: `AcceptsReturn=true`, `TextWrapping=Wrap`,
`MinHeight=32`, `MaxHeight=96`, `Padding=new Thickness(12,8,12,8)`,
Enter-to-send, Shift+Enter-for-newline. M4.4 is **not** a
replacement — it is a finishing pass.

M4.4 changes:

1. **Replace `MaxHeight=96` with `MaxLines=5` + auto-grow.** In
   Avalonia, `MaxLines` on a `TextBox` with `TextWrapping=Wrap`
   produces correct auto-grow up to N lines and is the
   semantically-correct way to cap height. The current
   `MaxHeight=96` is a hard pixel cap; `MaxLines=5` lets the input
   grow line-by-line.
2. **Add a small hint label below the input:** `⏎ to send · ⇧⏎ for
   newline` at 11px, `TextSecondaryBrush`. Place it inline in the
   `TownhallInputArea` container (the same `Border` that wraps the
   `DockPanel`).
3. **Add unit tests** in
   `tests/Zaide.Tests/Views/TownhallInputAreaTests.cs` (or extend
   if it exists):
   - `InputField_AcceptsReturn_IsTrue`
   - `InputField_TextWrapping_IsWrap`
   - `InputField_MaxLines_IsFive`
   - `EnterKey_TriggersSend`
   - `ShiftEnterKey_DoesNotTriggerSend`

#### M4.5 — Send button press animation (180ms)

Add a **180ms** scale animation (0.95 → 1.0) on the send button
`PointerPressed` event. 180ms keeps the animation inside the
150–200ms DESIGN.md §4 budget. M4.5 funnels through the
`Animations` helper (created in M6) so the easing and duration
match the rest of the system. If M6 has not yet shipped when M4
lands, M4.5 inlines a local `Animation` block — it is not
permitted to introduce a different duration.

#### M4.6 — Tests

`tests/Zaide.Tests/Views/TownhallChatPanelGroupingTests.cs`:
- `ThreeConsecutiveSameSender_RendersOneHeader`
- `SenderSwitch_RendersNewHeader`
- `FiveMinuteGap_StillRendersNewHeader`
- `DifferentSenders_AlwaysRenderHeaders`

#### M4 Exit Conditions

- [ ] VC-7 (grouping) and VC-8 (multi-line input) pass
- [ ] Avatar visual check: gradient + ring visible in screenshot
- [ ] Keyboard hint visible in screenshot
- [ ] Grouping tests pass
- [ ] All existing `dotnet test` pass
- [ ] Screenshot at `m4-default.png`

### M5: Status Bar and Spacing Density

Two related concerns that ship together because both are about
"everyday chrome."

#### M5.1 — Status bar segments as `Button`

Convert each `TextBlock` segment in `StatusBar.cs` to a `Button`
control with a `Button` style that is borderless in its idle state
and shows a subtle background on hover/press. Commands are no-op
stubs for now (real git/language pickers are deferred), but the
affordance is real.

Exception: the "powered by {model}" credit text on the far right
stays a `TextBlock` — it is not an actionable segment, and shrinking
it to 11px satisfies the audit (M5.3).

#### M5.2 — Drop the `│` separator

Replace every `TextBlock Separator() { Text = "│"; ... }` call with
8–12px of horizontal spacing between segments. Per DESIGN.md §7,
"Space or 1px semi-transparent line — never 2px+ solid borders."

If a visible separator is desired for grouping, use a 1px tall
`Border` with `SeparatorBrush` and `Height=12` (centered vertically).

#### M5.3 — Shrink the credit text

The "powered by Avisnis 12" text becomes 11px, `TextSecondaryBrush`,
no icon, right-aligned with 12px right margin. It is no longer a peer
of `Ln 1, Col 1` in the visual hierarchy.

#### M5.4 — Spacing audit (full scope)

Audit every `Margin` / `Padding` literal in:

- `src/Views/**/*.cs` (every view)
- `src/MainWindow.axaml.cs` (e.g. `Margin=new Thickness(1, 0, 0, 0)` at line 321 and `Margin=new Thickness(0, 1, 0, 0)` at line 360)
- `src/Views/UnsavedDialog.axaml` (`Margin="20"` and `Spacing="16"` / `Spacing="8"`)

Replace numeric literals with the `Spacing*` token keys from M0.5-B:

- `SpacingXxs` (2px), `SpacingXs` (4px), `SpacingSm` (8px),
  `SpacingMd` (12px), `SpacingLg` (16px), `SpacingXl` (20px),
  `SpacingXxl` (24px)
- `RadiusSm` (4px), `RadiusMd` (8px), `RadiusLg` (12px),
  `RadiusXl` (16px), `RadiusFull`

Audit command (run from repo root):

```bash
grep -rnE 'Margin\s*=\s*new Thickness|Padding\s*=\s*new Thickness|Margin\s*=\s*"|Padding\s*=\s*"|Spacing\s*=\s*"' \
  src/Views/ src/MainWindow.axaml.cs src/Views/*.axaml
```

The only acceptable literals after M5 are:

- Inside `TextStyles` and the token definitions in `App.axaml`.
- Inside `src/Views/Animations.cs` (the M6 helper, which uses
  raw `TimeSpan` values, not pixels).
- Inside `tools/` (verification scripts).

Every other hit must be replaced with a token reference or
explicitly justified in a `// M5-allow: ...` comment that names
the milestone that introduced it.

#### M5 Exit Conditions

- [ ] VC-9, VC-10, VC-11 pass
- [ ] Spacing audit completed; every literal justified or replaced
- [ ] All `dotnet test` pass
- [ ] Screenshot at `m5-default.png`

### M6: Animation Polish

DESIGN.md §4 specifies 150–200ms cubic-eased animations for
user-initiated state transitions. This milestone adds them.

#### M6.1 — Helper

Create a static helper `src/Views/Animations.cs` (or extend an
existing styles file) exposing:

```csharp
public static class Animations
{
    public static Animation FadeIn(TimeSpan? duration = null);
    public static Animation FadeOut(TimeSpan? duration = null);
    public static Animation SlideIn(HorizontalDirection dir, TimeSpan? duration = null);
    public static Animation SlideOut(HorizontalDirection dir, TimeSpan? duration = null);
    public static void Transition(Visual target, Animation animation);
}
```

Default duration: 180ms. Default easing: `CubicEaseOut` for
appearing, `CubicEaseIn` for disappearing.

#### M6.2 — Apply to state transitions (single budget: 150–200ms)

The previous plan listed 60ms, 80ms, 100ms, 120ms, 150ms, 180ms
durations side by side, which violates the 150–200ms budget
specified in DESIGN.md §4. M6.2 enforces a **single budget**:
all transitions are 150ms, 180ms, or 200ms. Pick the right
duration for the right interaction.

Wire `Animations` into:

| Surface | Animation | Duration | Easing |
|---|---|---|---|
| `NavBar.cs` | Mode switch (Explorer ↔ SC) | 180ms | `CubicEaseOut` in, `CubicEaseIn` out |
| `EditorTabBar.cs` | Tab switch crossfade | 150ms | `CubicEaseOut` |
| `FileTreeView.cs` | Row hover background | 150ms (in and out) | `CubicEaseOut` |
| `TownhallInputArea.cs` | Send button press scale | 180ms | `CubicEaseOut` |
| `SourceControlPanel.cs` ↔ `FileTreeView.cs` | Left-panel mode switch slide | 200ms | `CubicEaseOut` in, `CubicEaseIn` out |

Notes:

- **Hover** uses 150ms (faster than 180ms because hover is
  high-frequency and the user expects a tight response).
- **Press / mode switch** uses 180–200ms (slightly slower, more
  committed).
- **No 60ms, 80ms, 100ms, or 120ms** is allowed anywhere in the
  codebase. If you find yourself reaching for one, you are not
  using the `Animations` helper.

Do not animate properties that change frequently (e.g., text
content). Only user-initiated state transitions.

#### M6.3 — Animation budget guard (positive allowlist)

The previous plan's regex `new LinearEase\|TimeSpan\.FromMilliseconds([^12][0-9][0-9])` was broken — it missed `100`, `80`, `60`, and any value `> 200`. M6.3 replaces it with a
**positive-allowlist** guard at `tools/check-animations.sh`:

```bash
#!/usr/bin/env bash
# Fails if any animation helper in src/Views/Animations.cs is
# bypassed or any out-of-band duration appears.
set -e

bad=0

# 1) No direct TimeSpan.FromMilliseconds( in view code.
#    All animation durations must come from the Animations helper.
if grep -rnE 'TimeSpan\.FromMilliseconds\(' src/Views/ \
     | grep -v 'src/Views/Animations.cs' ; then
  echo "ERROR: direct TimeSpan.FromMilliseconds in view code"
  bad=1
fi

# 2) No new LinearEase in view code.
if grep -rnE 'new LinearEase\(' src/Views/ \
     | grep -v 'src/Views/Animations.cs' ; then
  echo "ERROR: new LinearEase in view code"
  bad=1
fi

# 3) No new Animation { ... } in view code (must use the helper).
if grep -rnE 'new Animation\s*\{' src/Views/ \
     | grep -v 'src/Views/Animations.cs' ; then
  echo "ERROR: inline new Animation in view code"
  bad=1
fi

# 4) Animations helper itself: every numeric duration must be in [150, 200].
helper=src/Views/Animations.cs
for ms in $(grep -oE 'TimeSpan\.FromMilliseconds\([0-9]+\)' "$helper" \
           | grep -oE '[0-9]+'); do
  if [ "$ms" -lt 150 ] || [ "$ms" -gt 200 ]; then
    echo "ERROR: $helper has out-of-budget duration ${ms}ms"
    bad=1
  fi
done

exit $bad
```

Wire `tools/check-animations.sh` into the M7 regression sweep
(must exit 0 before M7 can be marked complete). It is the source
of truth for VC-12; the previous regex was not.

#### M6 Exit Conditions

- [ ] VC-12 passes
- [ ] Animations helper has unit tests (duration defaults, easing
      types)
- [ ] No regressions in interactive feel — manual verification of
      all 5 transition points
- [ ] All `dotnet test` pass
- [ ] Screenshot at `m6-default.png`

### M7: Regression Sweep and Doc Sync

Final milestone. The goal is to confirm nothing broke and that docs
match reality.

#### M7.1 — Full regression

- [ ] `dotnet build Zaide.slnx` passes with **no new warnings**
      versus the M0 baseline (the 1 pre-existing xUnit analyzer
      warning at `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs:325`
      is acknowledged; see M0.5-C and VC-13)
- [ ] `dotnet test Zaide.slnx --no-build` passes with zero failures
- [ ] `tools/check-luminance.csx` exits 0 (VC-3)
- [ ] `tools/check-animations.sh` exits 0 (VC-12)
- [ ] VC-4 audit grep returns only `TextStyles` matches
- [ ] VC-11 audit grep returns only token references
- [ ] All VC-1 through VC-14 criteria verified
- [ ] Before/after screenshots saved and visually compared
- [ ] All `docs/refactor/refactor-4/TOFIX.md` items addressed

#### M7.2 — Doc sync

Update:
- `docs/DESIGN.md` — add the typography scale and the animation
  helper pattern. If the design doc already describes these, mark
  the relevant section as "implemented per Refactor 4."
- `docs/architecture/OVERVIEW.md` — note Refactor 4 in the
  chronology (between Refactor 3 and the next phase).
- `docs/roadmap/PHASES.md` — mark Refactor 4 as complete in the
  roadmap checklist.

#### M7.3 — Verification artifact assembly

- [ ] All `m{0..7}-default.png` and `m{0..7}-min.png` exist
- [ ] Build + test logs saved or quoted in the final PR
- [ ] Manual Verification Checklist (below) signed off in the PR
- [ ] No-new-behavior statement included in the PR

#### M7 Exit Conditions

- [ ] All M0–M6 milestones marked `[x]`
- [ ] All VC-1 through VC-14 criteria pass
- [ ] Docs match code
- [ ] PR ready for review

---

## Manual Verification Checklist

Copy this into the PR description at M7 and check every item.

### Backdrop blur / glass
- [ ] `MainWindow` has `Window.TransparencyLevelHint` set
- [ ] On Windows: sidebar / title bar area shows visible blur
- [ ] On macOS: blur visible
- [ ] On Linux KDE: blur visible
- [ ] On Linux tiling WMs: app still looks intentional (fallback
      solid color, no broken / transparent background)

### Elevation contrast
- [ ] `SurfacePanelBrush` is visibly lighter than `SurfaceBaseBrush`
- [ ] `PanelDeepBrush` reads as a separate layer from the editor
- [ ] No `Border Height=1` / `Width=1` separators remain in views
- [ ] Panels are separated by background contrast, not borders

### Typography
- [ ] Section headers ("People", "Channels", "Source Control",
      "Terminal / Logs") are visibly heavier than body text
- [ ] App brand "Zaide" in status bar reads as a brand, not a peer
      of caret position
- [ ] No `FontSize=` / `FontWeight=` literals in `src/Views/` except
      in `TextStyles`

### File tree
- [ ] `.cs` files show a distinct colored icon (not the generic
      folder glyph)
- [ ] `.md` files show a distinct colored icon
- [ ] `.json` files show a distinct colored icon
- [ ] Hover on a file row changes background subtly
- [ ] Selected file row shows a 2px left border in blue
- [ ] Indent guides visible at 3+ levels of nesting

### Chat
- [ ] 3 consecutive messages from one sender render as
      1 header + 3 content lines
- [ ] Switching sender renders a new header
- [ ] Avatars show a subtle gradient / ring
- [ ] Status dot visible at the bottom-right of active avatars
- [ ] Timestamps appear inline next to sender name, muted
- [ ] Input field is multi-line and grows up to 5 lines
- [ ] Keyboard hint `⏎ to send · ⇧⏎ for newline` is visible
- [ ] Send button has a press animation

### Status bar
- [ ] Each segment is a `Button` (visually evident on hover)
- [ ] No `│` characters anywhere in the status bar
- [ ] "powered by {model}" is 11px and visually quieter than the
      other segments
- [ ] 8–12px spacing between segments

### Spacing / density
- [ ] All `Margin` / `Padding` use `Spacing*` tokens
- [ ] Visual density is consistent across panels (no chat vs.
      tree vs. status bar feeling like different apps)

### Animation
- [ ] Sidebar mode switch slides + fades smoothly
- [ ] Tab switch crossfades content
- [ ] File tree row hover fades in / out
- [ ] Send button presses with a scale animation
- [ ] No instant jump cuts on any state transition

### Regression
- [ ] Open file → edit → save still works
- [ ] Switch tabs still works
- [ ] Switch panel mode (Explorer ↔ Source Control) still works
- [ ] Send a message in Townhall still appends to chat
- [ ] Terminal still runs commands
- [ ] Status bar caret position still updates on cursor move
- [ ] `dotnet test Zaide.slnx` passes with zero failures

---

## Refactor 4 Limitations (by design)

- Blur on Linux tiling WMs (i3, Sway, dwl) is not supported by the
  compositor. Fallback is a solid dark color, not blur. No X11 /
  Wayland workarounds in scope.
- Light theme is not implemented. The dark palette is the only one
  in scope.
- A custom font is not bundled. The platform system font is used
  per DESIGN.md §3. If the platform default looks weak (e.g., a
  fresh Linux install with no font config), the polish gap cannot be
  closed by code alone.
- Status bar segments are clickable but do not open real pickers.
  Real branch / language pickers are a feature, deferred to a
  future phase.
- The send button is a visual polish, not a behavior change. The
  same `SendMessageCommand` runs.
- The chat input is multi-line but does not support rich content,
  code blocks, or attachments in this refactor. Those are features.
- All polish is on the dark theme. Light theme parity is not in
  scope.

---

## Exit Conditions (Refactor 4 complete)

- [ ] M0, **M0.5**, M1, M2, M3, M4, M5, M6, M7 all marked `[x]`
- [ ] All VC-1 through VC-14 pass
- [ ] `dotnet build Zaide.slnx` passes with **no new warnings**
      versus the M0 baseline file (see VC-13)
- [ ] `dotnet test Zaide.slnx --no-build` passes with zero failures
- [ ] `tools/check-luminance.csx` exits 0
- [ ] `tools/check-animations.sh` exits 0
- [ ] Before/after screenshots captured at default and 960 px
- [ ] `docs/DESIGN.md` updated with the typography scale and
      animation helper
- [ ] `docs/architecture/OVERVIEW.md` updated with Refactor 4
      chronology
- [ ] `docs/roadmap/PHASES.md` marked complete

---

## Rollback Plan

- **Commit hash to revert to:** (fill in at the start of M0 from
  `git log --oneline -1` on the last known-good commit before
  Refactor 4 work begins).
- **Fallback strategy (per milestone):**
  - **M1:** Revert the three touched files
    (`MainWindow.axaml.cs`, `App.axaml`, plus whichever view
    changed). The previous `SurfaceBaseBrush` / `SurfacePanelBrush`
    hex values are recoverable from `git show <hash>:src/App.axaml`.
  - **M2:** Delete `src/Styles/TextStyles.cs` and revert each view
    file to its previous `FontSize=` / `FontWeight=` literals. The
    `grep` audit at VC-4 is the canary — it should fail loudly if
    any literal was missed in a partial revert.
  - **M3:** Revert `FileTreeView.cs`,
    `FileIconKeyResolver.cs`, `IconFactory.cs`. The file tree
    returns to its previous flat appearance.
  - **M4:** Revert `TownhallChatPanel.cs`,
    `TownhallInputArea.cs`, `TownhallPeoplePanel.cs`. Chat returns
    to single-header-per-message and single-line input.
  - **M5:** Revert `StatusBar.cs` (the `│` separator, the credit
    text size, the `Button` conversion) and the spacing audit
    changes per file.
  - **M6:** Delete the `Animations` helper and revert the five
    view files that use it. No state change — animations are
    additive.
  - **M7:** Doc-only revert. `git revert <docs-commit-hash>`.

- **One-milestone-at-a-time revert:** Per `docs-rules.md` §12f, every
  milestone ships as its own commit. Reverting one milestone
  reverts only that milestone's diff, not the whole refactor.

- **If the refactor is fundamentally broken (not just buggy):**
  Follow `docs-rules.md` §12i — revert to the last known-good commit
  before M0 and re-implement. The plan is structured so no milestone
  creates irreversible state.
