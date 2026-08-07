# Refactor 10: UI Polish — Theming and Shared Control Layer — TOFIX

## Status

M0 (plan and archive) through M3 (literal replacement and guard) are implemented.
A post-M3 commit audit (2026-08-07) found honesty and visual gaps; those are
tracked as **R1–R5** in `AUDIT_REMEDIATION_PLAN.md` and must finish before M4.

**Next task: R4** (docs truth-sync after audit remediation).

Refactor 10 was re-scoped on 2026-08-06. The previous content of
`IMPLEMENTATION_PLAN.md` described F8 image preview and accessibility work; that
content was a placeholder and has been discarded. The refactor now owns the
theming mechanism and the shared control layer, with light as the default theme.

## Scope corrections applied

| Item | Disposition |
|---|---|
| F8 image file preview | removed from Refactor 10, moved to a separate phase |
| Accessibility (screen reader, keyboard navigation) | removed; focus rings remain in M4 |
| Theme switcher UI and persistence | removed, separate parallel phase |
| Light + dark ramps | both authored in this refactor (M2) |
| Glass and blur | in scope (M5) with an opaque fallback |

## M0 findings (from the live audit)

- `src/App/Composition/App.axaml:5` pins `RequestedThemeVariant="Dark"`, and
  lines 11–62 are a flat `<Color>` + `<SolidColorBrush {StaticResource}>`
  dictionary. No `ThemeDictionaries`, no `ThemeVariantScope`, no
  `DynamicResource` anywhere in the repository.
- `src/UI/DesignSystem/PaletteTokens.cs` resolves brushes once through
  `Application.Current.Resources[...]` and carries nine hardcoded dark
  fallbacks, so a variant change cannot reach the screen.
- `src/UI/DesignSystem/TypographyTokens.cs` has one token (`FontSizeSm` = 12).
- ~100+ hardcoded color literals in `src/**/*.cs`. Top offenders:
  `SettingsFontPicker.cs` (15), `SourceControlPanel.cs` (14),
  `FileTreeView.cs` (12), `PaletteTokens.cs` (9), `TownhallView.cs` (8),
  `CommandPaletteOverlay.cs` (8), `AgentMemoryPanel.cs` (6),
  `AgentBackendBindingPanel.cs` (6), `StatusBar.cs` (6),
  `BottomPanelHost.cs` (6).
- `static readonly` brush caches block variant switching in
  `BreakpointMargin.cs:21-27`, `InstructionPointerMargin.cs:14`,
  `TerminalRenderControl.cs:186-195`, `TownhallNavigationPanel.cs:25-26`.
- No central `ControlTheme` / `Styles` layer; hover and pressed states are
  hand-wired per panel. Zero shadow or elevation usage — the UI is fully flat.
- 38 raw `new Thickness(` calls exist even though `LayoutTokens` is available.

## M0 result (2026-08-06)

- Rewrote `IMPLEMENTATION_PLAN.md` against the project plan template with
  milestones M0–M5, verified live seams, limitations, and exit conditions.
- Added `archive/legacy-navy-palette.md`, preserving the `DESIGN.md §8` palette
  table verbatim plus the legacy-key to new-token mapping.
- Opened this work board.

Documentation-only milestone, so no build or test gate applies. The baseline
`dotnet test Zaide.slnx --no-build` run belongs to the M1 gate.

## Work board

| Milestone | State | Gate |
|---|---|---|
| M0 — plan and archive | done | docs review |
| M1 — theme variant infrastructure | done* | key-parity test; full panel repaint on flip deferred to M4 |
| M2 — semantic tokens and both ramps | done | contrast-ratio tests + full suite |
| M3 — literal replacement and guard | done* | partial literal migration; weak guard (see audit) |
| R1 — light SurfaceOverlayBrush scrim | done | black 40% scrim; parity + build green |
| R2 — editor TextMate vs app variant | done | Light+/Dark+ from variant; theme tests + build |
| R3 — strengthen guard + Makefile policy | done | `bash scripts/check-theme-tokens.sh`; `make check-theme-tokens` |
| R4 — docs truth-sync | pending | plan/TOFIX match live code |
| R5 — optional polish | pending | subset in AUDIT_REMEDIATION_PLAN |
| M4 — shared control layer | blocked on R1–R4 | full suite + interaction walkthrough |
| M5 — glass, depth, motion | pending | main screens with and without blur + full suite |

\*M1/M3 “done” means the milestone commits landed; audit findings are R1–R4.
See `AUDIT_REMEDIATION_PLAN.md` for prompts and exit conditions.

## Open questions

- None. All four scoping decisions were confirmed on 2026-08-06.

## M2 result (2026-08-06)

- Added ~40 semantic token keys to both `Light.axaml` and `Dark.axaml`: Surface
  (Canvas, Raised1–3, Glass, GlassFallback, Overlay), Border (Subtle, Default,
  Strong, Focus), Text (Tertiary, OnAccent, Disabled), Accent (base, Hover,
  Pressed, SubtleBg), State (Danger, Info + 5 SubtleBg), Interaction (Overlay
  Hover/Pressed/Selected), and Elevation (ShadowSm/Md/Lg).
- Light/Dark key sets remain identical (ThemeTokenParityTests enforces).
- `Shared.axaml` extended with `LineHeightSm/Md/Lg`.
- `TypographyTokens` expanded: 5 sizes (Xs–Xl), 3 line heights, 4 weights.
- `TextStyles` now routes through `TypographyTokens` and `ThemeBinding.GetColor`;
  no more hardcoded font sizes or manual brush resolution.
- `Elevation.cs` added: theme-aware `BoxShadows` accessors with opaque fallbacks.
- `ThemeBinding.CurrentVariant` accessor exposed for `Elevation`.
- Contrast ratios verified: body text ≥ 4.5:1, secondary/borders ≥ 3:1, in both ramps.
- All 4062 tests pass.

## M3 result (2026-08-06)

- Replaced many hardcoded color literals in 16 production files with
  `ThemeBinding.GetBrush`/`GetColor` (and some legacy palette keys).
- Converted remaining static brush/color caches to instance caches invalidated
  on `ActualThemeVariantChanged` (StatusBar, TownhallPeoplePanel,
  TownhallNavigationPanel).
- Routed several raw `new Thickness(` spacing values through `LayoutTokens`;
  1px border widths left as literals (not spacing). Residuals remain (~27
  `new Thickness(` in `src`).
- Added `scripts/check-theme-tokens.sh` that greps `Color.Parse("#…")`,
  `Color.FromArgb(…)`, and `Color.FromRgb(…)` with numeric-literal channels
  (computed channels such as `accent.R` are allowed). Path excludes:
  `TerminalRenderControl.cs` (ANSI palette), `TextStyles.cs` and `Elevation.cs`
  (ThemeBinding fallbacks). Invoked via `bash scripts/check-theme-tokens.sh`
  or `make check-theme-tokens` (Makefile tracked as of R3).
- Residual production color construction still present (terminal ANSI,
  breakpoint RGB, elevation fallbacks, computed alpha overlays, TextStyles
  navy fallbacks).
- Known mapping issues deferred to remediation: ~~editor TextMate `DarkPlus`
  under light default~~ **fixed in R2** (Light+/Dark+ from app variant).
- Build clean; design-system tests green at audit time.

## Audit remediation (2026-08-07)

Plan and per-step agent prompts: `AUDIT_REMEDIATION_PLAN.md`.

| ID | Item | State |
|---|---|---|
| R1 | Light `SurfaceOverlayBrush` dimming scrim | done — `#000000` @ 40% replaces white wash |
| R2 | Editor TextMate theme follows app variant | done — Light+/Dark+; runtime `SetTheme` on variant change |
| R3 | Stronger guard + Makefile git policy | done — Parse/FromArgb/FromRgb literals; Makefile policy A |
| R4 | Truth-sync IMPLEMENTATION_PLAN + TOFIX | pending |
| R5 | Optional: indent guide token, flip smoke test, nits | pending |

## R3 result (2026-08-07)

- Guard now enforces `Color.Parse("#…")`, literal-channel `Color.FromArgb`, and
  literal-channel `Color.FromRgb` in `src/**/*.cs` via `scripts/check-theme-tokens.sh`.
- Computed alpha from theme channels (e.g. `Color.FromArgb(0x30, accent.R, …)`)
  is allowed without path excludes.
- Path excludes (one-line justification each): `TerminalRenderControl.cs` (ANSI
  palette), `DesignSystem/TextStyles.cs` (ThemeBinding fallbacks),
  `DesignSystem/Elevation.cs` (BoxShadow fallbacks behind `ThemeBinding.Resolve`).
- **Makefile policy A:** `Makefile` removed from `.gitignore` and committed with
  `check-theme-tokens` plus existing local `run`/`build`/`test` targets.
- No trivial production violations to fix; residual literal patterns outside the
  guard (e.g. `Colors.White`, `new Thickness(`) remain for M4.

## Next task

**R4** — truth-sync `IMPLEMENTATION_PLAN.md` and this board with post-audit
reality (`AUDIT_REMEDIATION_PLAN.md`).
After R4 (R5 optional): M4 shared control layer.
