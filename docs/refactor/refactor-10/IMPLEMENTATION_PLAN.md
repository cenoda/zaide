# Refactor 10: UI Polish — Theming and Shared Control Layer — Implementation Plan

## Status

**In progress (2026-08-07).** M0–M3 and audit remediation **R1–R5** are done.
**M4** (shared control layer) is in progress; **M4a** (catalog skeleton + focus ring) and
**M4b** (control factories) are done.

Remediation detail and per-step prompts: `AUDIT_REMEDIATION_PLAN.md`.

Supersedes the earlier placeholder content of this file, which described F8
image preview and accessibility work. Those items are **not** part of
Refactor 10:

| Deferred item | New owner |
|---|---|
| F8 image file preview (PNG/JPG/WebP/GIF) | separate phase |
| Accessibility (screen reader, keyboard navigation) | separate phase; focus rings stay here |
| User-facing theme switcher, settings persistence, OS theme following | separate parallel phase |

## Pre-Implementation Verification

### M0 baseline (2026-08-06 entry gate — historical)

Recorded at refactor open; superseded by M1–M3 work except where noted in
residual-debt rows below.

- [x] Flat dark-only `App.axaml` resource dictionary (no `ThemeDictionaries`).
- [x] `PaletteTokens` / per-view `Application.Current.Resources[...]` one-shot
      resolution blocked variant propagation.
- [x] ~100+ hardcoded color literals and 38 raw `new Thickness(` calls.
- [x] `static readonly` brush caches in margin/terminal/townhall controls.
- [x] No central `ControlTheme` layer; hover hand-wired per panel.

### Post-M3 / post-remediation reality (2026-08-07)

- [x] `ThemeDictionaries` with `Light.axaml` / `Dark.axaml` / `Shared.axaml`;
      `RequestedThemeVariant="Dark"` removed; light is the default.
- [x] `ThemeBinding` helper; `PaletteTokens` fallbacks removed; instance brush
      caches invalidate on `ActualThemeVariantChanged` (no `static readonly`
      brush fields remain in `src`).
- [x] Semantic token ramps, typography scale, `Elevation.cs`, contrast tests.
- [x] Partial literal migration (16 files in M3); guard at
      `scripts/check-theme-tokens.sh` enforces `Color.Parse("#…")` and
      literal-channel `Color.FromArgb` / `Color.FromRgb` with path excludes
      (`TerminalRenderControl.cs`, `TextStyles.cs`, `Elevation.cs`). Invoke via
      `bash scripts/check-theme-tokens.sh` or `make check-theme-tokens`
      (`Makefile` tracked as of R3). **CI does not invoke this guard yet.**
- [x] R1: light `SurfaceOverlayBrush` is `#000000` @ 0.40 (dimming scrim).
- [x] R2: editor TextMate uses `LightPlus` / `DarkPlus` from app variant.
- [ ] **Deferred to M4:** full UI repaint on variant flip (one-shot
      `ThemeBinding` + ~90 `Resources[]` indexer sites); mass hover unification.
- [ ] `TransparencyLevelHint` blur on dev compositor, or opaque fallback proven
      (M5 entry gate).

## Scope

**Goal:** Move the app from a dark-only, literal-driven color path to a
variant-aware token system with a shared control layer, and ship **light as the
default theme** at Linear / Zed / Cursor finish quality, with an Apple-style
glass texture where the platform supports it.

**Boundaries:**

- No theme switcher UI, no settings persistence, no OS theme following.
- No F8 image preview and no full accessibility work.
- No new features and no backend logic changes.
- Reference apps are a **direction, not an imitation target**.

## Confirmed Decisions

1. Design **both** light and dark ramps in this refactor. Only the switching
   trigger is deferred.
2. Archive the legacy navy palette instead of deleting it — see
   `archive/legacy-navy-palette.md` — and rewrite `docs/DESIGN.md §8` onto the
   new token system.
3. The former content of this plan is a placeholder and is ignored.
4. Reference direction: restrained density of Linear / Zed / Cursor plus an
   Apple (Xcode) glass texture.
5. Glass and blur are in scope, with an opaque fallback (`DESIGN.md §2`).
6. The control layer is a **C#-first hybrid**: composition and states live in
   C# factories and code-built `ControlTheme` objects. XAML is limited to cases
   where template replacement is unavoidable (scrollbars, Semi overrides),
   which stays inside `DESIGN.md §1` tier 1–2.

## Token System

| Group | Keys | Notes |
|---|---|---|
| Surface | `SurfaceCanvas`, `SurfaceRaised1`–`SurfaceRaised3`, `SurfaceGlass`, `SurfaceGlassFallback`, `SurfaceOverlay` | 4 elevation tiers plus glass |
| Border | `BorderSubtle`, `BorderDefault`, `BorderStrong`, `BorderFocus` | 1px, translucent |
| Text | `TextPrimary`, `TextSecondary`, `TextTertiary`, `TextOnAccent`, `TextDisabled` | 3-level hierarchy |
| Accent | `Accent`, `AccentHover`, `AccentPressed`, `AccentSubtleBg` | state derivatives |
| State | `Success`, `Warning`, `Danger`, `Info`, `Idle` plus each `*SubtleBg` | badges and dots |
| Interaction | `OverlayHover`, `OverlayPressed`, `OverlaySelected` | translucent overlays |
| Elevation | `ShadowSm`, `ShadowMd`, `ShadowLg` | `BoxShadows` |
| Typography | `FontSizeXs/Sm/Md/Lg/Xl`, `LineHeight*`, `FontWeight*` | restored scale |

Light and dark dictionaries must expose an **identical key set**; a unit test
enforces this.

## File Structure

```
src/App/Composition/App.axaml                        # ThemeDictionaries entry point, variant pin removed
src/UI/DesignSystem/Tokens/Light.axaml               # light ramp (new)
src/UI/DesignSystem/Tokens/Dark.axaml                # dark ramp (new, carries the navy forward)
src/UI/DesignSystem/Tokens/Shared.axaml              # spacing/radius/typography/motion (new)
src/UI/DesignSystem/ThemeBinding.cs                  # C# -> DynamicResource helper (new)
src/UI/DesignSystem/Elevation.cs                     # BoxShadows accessors (new)
src/UI/DesignSystem/GlassSupport.cs                  # blur capability probe (new)
src/UI/DesignSystem/PaletteTokens.cs                 # fallbacks removed
src/UI/DesignSystem/TypographyTokens.cs              # scale expanded
src/UI/DesignSystem/Controls/ControlThemeCatalog.cs  # code-built ControlTheme registration (new)
src/UI/DesignSystem/Controls/AppButton.cs            # primary/secondary/icon factory (new)
src/UI/DesignSystem/Controls/AppTextBox.cs           # input/search factory (new)
src/UI/DesignSystem/Controls/ListRow.cs              # list/tree row factory (new)
src/UI/DesignSystem/Controls/PanelChrome.cs          # header/divider/empty state factory (new)
src/UI/DesignSystem/Themes/Scrollbars.axaml          # template replacement exception (new)
src/UI/DesignSystem/Themes/SemiOverrides.axaml       # Semi default key overrides (new)
scripts/check-theme-tokens.sh                        # color/Thickness literal guard (new)
tests/Zaide.Tests/UI/DesignSystem/                   # key-parity and contrast tests
docs/DESIGN.md                                       # §8 palette table replaced
docs/refactor/refactor-10/archive/legacy-navy-palette.md
```

## Milestones

| Milestone | Description | Test |
|---|---|---|
| M0 | Entry gate: live audit recorded above, plan and archive documents written | docs review; `dotnet test Zaide.slnx --no-build` baseline |
| M1 | Theme variant infrastructure: `ThemeDictionaries`, `ThemeBinding`, variant pin removed, static brush caches eliminated | key-parity test; **full panel repaint on flip deferred to M4** |
| M2 | Semantic tokens and both ramps authored, typography scale and elevation restored | contrast-ratio tests + `dotnet test Zaide.slnx --no-build` |
| M3 | Partial literal migration; guard for Parse/FromArgb/FromRgb literals | `bash scripts/check-theme-tokens.sh` or `make check-theme-tokens` |
| M4 | Shared control layer replaces per-panel hover code | full suite + hover/pressed/focus walkthrough |
| M5 | Glass surfaces, elevation, radius and motion polish | main screens with and without blur + full suite |

### M0 — Entry gate (documentation only)

Record the live audit, lock scope and boundaries, archive the legacy palette,
and open `TOFIX.md` as the work board.

**Exit:** this plan, `archive/legacy-navy-palette.md`, and `TOFIX.md` exist and
agree with live code.

### M1 — Theme variant infrastructure

- Split `App.axaml` resources into `Tokens/Light.axaml`, `Tokens/Dark.axaml`,
  and `Tokens/Shared.axaml`, grouped under
  `ResourceDictionary.ThemeDictionaries`.
- Remove `RequestedThemeVariant="Dark"`; light becomes the default.
- Add `ThemeBinding` so C# views bind brush properties through
  `DynamicResourceExtension`.
- Remove hardcoded fallbacks from `PaletteTokens` and delegate to
  `ThemeBinding`.
- Replace the `static readonly` brushes in `BreakpointMargin`,
  `InstructionPointerMargin`, `TerminalRenderControl`, and
  `TownhallNavigationPanel` with instance caches invalidated on
  `ActualThemeVariantChanged`.

**Exit:** light/dark key sets match; zero `static readonly` brush fields remain.
**Not in M1 scope (M4):** forcing the variant from code repaints every panel
without a restart — one-shot `ThemeBinding` resolution and ~90
`Resources[]` indexer call sites remain; no flip-repaint integration test yet.

### M2 — Semantic tokens and both ramps

- Author every key from the token table in both ramps.
- Light ramp: four low-saturation neutral tiers with a restrained accent.
- Dark ramp: carry the existing navy forward under the new key names.
- Expand `TypographyTokens` to five sizes plus line-height and weight, and wire
  it into `TextStyles`.
- Add `Elevation.cs` (`ShadowSm/Md/Lg`).
- Replace the `docs/DESIGN.md §8` palette table with the new token system.

**Exit:** body text ≥ 4.5:1 and secondary text / borders ≥ 3:1 in both ramps,
asserted by tests.

### M3 — Literal replacement and guard

- Migrate top offenders first, then the remaining files, in reviewable groups.
- Align many raw `new Thickness(` calls to the `LayoutTokens` scale (~27
  `new Thickness(` remain in `src`; 1px border widths excluded).
- Add `scripts/check-theme-tokens.sh` following the `check-animations.sh`
  pattern; wire into the tracked `Makefile` (`make check-theme-tokens`).
  **CI does not invoke the guard yet** (local/Makefile only).

**Exit:** guard passes on current tree for `Color.Parse("#…")` and
literal-channel `Color.FromArgb` / `Color.FromRgb`, with documented path
excludes (terminal ANSI, `TextStyles` / `Elevation` fallbacks). Does **not**
claim zero color construction in `src` — residual `Colors.*`, computed alpha,
and allowlisted paths remain (see `TOFIX.md` M3 result).

### M4 — Shared control layer

- `ControlThemeCatalog` builds `ControlTheme` / `Style` / `Setter` objects in C#
  for hover, pressed, selected, focus, and disabled, and registers them into
  app resources.
- Add `AppButton`, `AppTextBox`, `ListRow`, and `PanelChrome` factories.
- Keep only `Themes/Scrollbars.axaml` and `Themes/SemiOverrides.axaml` in XAML.
- Apply a consistent `BorderFocus` focus ring.
- Replace code-behind hover logic in `SourceControlPanel`, `FileTreeView`,
  `TownhallView`, `BottomPanelHost`, `NavBar`, and `StatusBar`.

**Exit:** identical hover/pressed/focus feedback across panels; no per-panel
hover wiring remains in those files.

### M5 — Glass, depth, and motion

- Set `TransparencyLevelHint` on `MainWindow` and add the `GlassSupport` probe.
- Apply `SurfaceGlass` with `SurfaceGlassFallback` to the command palette,
  overlays, and sidebar.
- Apply `ShadowSm/Md/Lg` to overlays, floating panels, and dropdowns.
- Unify corner radii and replace heavy borders with 1px subtle borders or gaps.
- Apply 150–200 ms cubic-eased hover and focus transitions (`DESIGN.md §4`).

**Exit:** main screens verified with and without blur; full suite green.

## Risks

| Risk | Mitigation |
|---|---|
| Semi.Avalonia light defaults override our keys | place token dictionaries after the Semi include and override conflicting keys explicitly |
| AvaloniaEdit editor colors drift from the palette | define editor highlighting per variant, not only `SearchPanel*` |
| Terminal renderer depends on cached brushes for throughput | keep the cache but invalidate on `ActualThemeVariantChanged` |
| Visual regressions from bulk replacement | migrate in file groups, compare screenshots, run the guard |
| Linux compositor lacks blur | opaque fallback tokens; verify the UI without blur |

## Limitations (by design)

- The variant can only be changed from code in this refactor; there is no user
  control.
- The guard script is textual, so it can be bypassed by indirection; it targets
  the common literal forms only.
- Glass is verified on the development compositor only. Other Linux
  compositors rely on the documented fallback.

## Exit Conditions

Refactor-wide gates (M4/M5 still open):

- [x] `RequestedThemeVariant="Dark"` removed; light renders by default
- [x] Guard enforces `Color.Parse` hex and literal-channel `FromArgb` /
      `FromRgb` in `src/**/*.cs` (allowlisted paths excluded); **not** full
      zero-literal coverage (`Colors.*`, Thickness, unguarded forms remain)
- [x] Zero `static readonly` brush fields in `src`
- [x] Contrast ≥ 4.5:1 body text, ≥ 3:1 secondary text and borders (tests)
- [x] `dotnet build Zaide.slnx` succeeds (verified through M3/R3)
- [ ] `dotnet test Zaide.slnx --no-build` passes in full (re-verify at M4 close)
- [ ] No layout breakage at 800×600 (re-verify at M4/M5)
- [ ] Full panel repaint on variant flip (M4)
- [ ] Shared control layer / hover unification (M4)
- [ ] Glass, elevation, motion polish (M5)

## Rollback Plan

Each milestone lands as one reviewable commit. Revert to the commit preceding
the failing milestone; M1 is the structural boundary, so a rollback past M1
restores the flat dark dictionary and the archived palette document remains the
reference for those values.
