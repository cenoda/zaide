# Archive: Legacy Navy Palette (pre-Refactor 10)

This document preserves the original dark-navy palette and the `concept.png`
based decisions that governed Zaide's appearance from Refactor 3 until
Refactor 10. It is a **historical record**. Do not use it as the current source
of truth — the live token system is documented in `docs/DESIGN.md §8`.

## Why it was archived

Refactor 10 switches the default theme to light and replaces the flat, dark-only
resource dictionary with a variant-aware token system. The palette below was
designed exclusively for a dark surface; inverting its values breaks contrast, so
the light ramp was designed from scratch instead. The user asked that the first
proposal be preserved rather than deleted.

## Source references

- `concept.png` at the repository root — the original dark-navy mockup this
  palette was matched to.
- `docs/refactor/refactor-3/IMPLEMENTATION_PLAN.md` M0.5 — per-component token
  assignments.
- `docs/DESIGN.md §8` prior to Refactor 10 — the table reproduced below.
- `src/App/Composition/App.axaml` prior to Refactor 10 — the flat
  `<Color>` + `<SolidColorBrush Color="{StaticResource ...}">` dictionary that
  carried these values.

## Palette table (verbatim, as it appeared in `docs/DESIGN.md §8`)

- **Color palette:** Monochromatic dark base with blue accent system (matched to concept.png).
  All views must use these tokens by resource key name via `DynamicResource`
  or `Application.Current!.Resources[...]`. No hardcoded hex values in view code.
  See `docs/refactor/refactor-3/IMPLEMENTATION_PLAN.md` M0.5 for per-component assignments.

  | Token Key | Hex | Name | Usage |
  |-----------|-----|------|-------|
  | `PrimaryAccentBrush` | `#066ADB` | Bright Blue | Active tabs, primary buttons, focus rings, links, "Commit Staged" button |
  | `SecondaryAccentBrush` | `#3ED3E4` | Cyan Teal | Code type highlights, secondary indicators, terminal status text |
  | `WarningBrush` | `#FCBB47` | Amber | Warning badges, modified indicators (M), amber status dots |
  | `SuccessBrush` | `#28A745` | Green | Added indicators (A), active status dots, sync indicators |
  | `SurfaceBaseBrush` | `#0A0F19` | Near-Black Navy | Window background, nav bar background, deepest panel base |
  | `SurfacePanelBrush` | `#0B121D` | Lighter Navy | Elevated panels (editor, terminal), code areas, input fields |
  | `PanelDeepBrush` | `#0D1520` | Deep Panel | Bottom panel background, agent area |
  | `TextPrimaryBrush` | `#E3E4F4` | Pale Ice Blue-White | All primary text: code content, names, labels |
  | `TextSecondaryBrush` | `#8B95A5` | Muted Blue-Gray | Timestamps, line numbers, placeholder text, auxiliary labels |
  | `SeparatorBrush` | `#070C16` | Darkest | 1px panel separators, grid lines |
  | `IdleBrush` | `#5A6070` | Muted Slate | Idle status dots, disabled/inactive elements |
  | `BusyBrush` | `#FCBB47` | Amber | Busy status dots (same as WarningBrush for visual consistency) |

## Decisions carried forward into Refactor 10

| Legacy decision | Disposition |
|---|---|
| Monochromatic base with a single blue accent | kept as a principle; accent value retuned per variant |
| Tokens referenced by resource key, never hex in views | kept and now enforced by `scripts/check-theme-tokens.sh` |
| Amber reused for both warning and busy | kept; expressed as `Warning` with a `BusyBrush` alias |
| Three-tier surface stack (`SurfaceBase` / `SurfacePanel` / `PanelDeep`) | superseded by `SurfaceCanvas` + `SurfaceRaised1`–`SurfaceRaised3` |
| Two-tier text hierarchy | superseded by `TextPrimary` / `TextSecondary` / `TextTertiary` |
| `SeparatorBrush` darker than every surface | superseded by translucent `BorderSubtle` / `BorderDefault` / `BorderStrong` |
| Palette values matched to `concept.png` | mockup retained at the repository root as a historical artifact only |

## Legacy key to new token mapping

| Legacy key | New token |
|---|---|
| `SurfaceBaseBrush` | `SurfaceCanvasBrush` |
| `SurfacePanelBrush` | `SurfaceRaised1Brush` |
| `PanelDeepBrush` | `SurfaceRaised2Brush` |
| `SeparatorBrush` | `BorderSubtleBrush` |
| `TextPrimaryBrush` | `TextPrimaryBrush` (unchanged name, revalued per variant) |
| `TextSecondaryBrush` | `TextSecondaryBrush` (unchanged name, revalued per variant) |
| `PrimaryAccentBrush` | `AccentBrush` |
| `SecondaryAccentBrush` | `InfoBrush` |
| `WarningBrush` | `WarningBrush` |
| `SuccessBrush` | `SuccessBrush` |
| `IdleBrush` | `IdleBrush` |
| `BusyBrush` | `WarningBrush` (alias retained) |

The exact dark hex values above are the starting point for the Refactor 10 dark
ramp, so the dark experience remains recognizable after the migration.
