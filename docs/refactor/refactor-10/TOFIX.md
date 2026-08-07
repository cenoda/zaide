# Refactor 10: UI Polish — Theming and Shared Control Layer — TOFIX

## Status

M0 (plan and archive) through M3 (literal replacement and guard) are implemented.
Post-M3 audit remediation **R1–R5** is complete (2026-08-07). **M4** (shared control layer) is done; **M4a** done (2026-08-07); **M4b** done (2026-08-07); **M4c** done (2026-08-07); **M4d** done (2026-08-07).

**Next task: M5** — glass surfaces, elevation, radius and motion polish.

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
| M1 — theme variant infrastructure | done* | key-parity test; full panel repaint on flip **deferred to M4** |
| M2 — semantic tokens and both ramps | done | contrast-ratio tests + full suite |
| M3 — literal replacement and guard | done* | partial migration; guard (Parse/FromArgb/FromRgb + allowlists) |
| R1 — light SurfaceOverlayBrush scrim | done | `#000000` @ 40%; parity + build green |
| R2 — editor TextMate vs app variant | done | Light+/Dark+ from variant; theme tests + build |
| R3 — strengthen guard + Makefile policy | done | `bash scripts/check-theme-tokens.sh`; `make check-theme-tokens` |
| R4 — docs truth-sync | done | plan/TOFIX match live code |
| R5 — optional polish | done | indent guide token, ThemeBinding flip smoke test, stale comments |
| M4 — shared control layer | done | full suite + interaction walkthrough |
| M4a — ControlThemeCatalog + focus ring | done | catalog registration smoke tests + build |
| M4b — AppButton/AppTextBox/ListRow/PanelChrome | done | factory unit tests |
| M4c — hover unification (panels) | done | walkthrough |
| M4d — Resources[] indexer + variant flip repaint | done | design-system repaint tests + zero Resources[] |
| M5 — glass, depth, motion | next | main screens with and without blur + full suite |

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
| R4 | Truth-sync IMPLEMENTATION_PLAN + TOFIX | done — post-R3 reality; M1 flip repaint deferred to M4 |
| R5 | Optional: indent guide token, flip smoke test, nits | done |

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

## R4 result (2026-08-07)

- `IMPLEMENTATION_PLAN.md`: status reflects M0–M3 + R1–R4; exit gates checked
  only when true; M1 full repaint and M3 zero-literal claims corrected; guard
  scope and Makefile/CI notes added.
- This board: R4 done; next task R5 (optional) or M4.
- `AUDIT_REMEDIATION_PLAN.md`: R4 exit filled; status table R1–R4 done.

## R5 result (2026-08-07)

- Editor indent guides use `SeparatorBrush` instead of `AccentSubtleBgBrush` for a
  subtler 1px guide line aligned with the file-tree indent treatment.
- Added `ThemeBindingVariantTests`: flip `RequestedThemeVariant` Light/Dark on a
  test-thread `Application` and assert `ThemeBinding.GetColor` / `GetBrush` change
  for palette keys (no visual-tree repaint assertion; that remains M4).
- NavBar stale `#12FFFFFF` comment updated to `OverlayHoverBrush`; Makefile
  `check-theme-tokens` help text matches R3 guard scope.

## Next task

**M5** — glass surfaces, elevation, radius and motion polish (`IMPLEMENTATION_PLAN.md` M5).

## M4d result (2026-08-07)

- Extended `ThemeBinding` with live `SetBrush` / `SetColor` (apply + re-apply on
  `ActualThemeVariantChanged`) and `SubscribeVariantChanged` for stateful
  imperative repaint (NavBar icons, tab strips, file-tree selection paint).
- `IconFactory.Create(string, string brushKey, …)` binds icon foreground live.
- Migrated all `Application.Current.Resources[]` indexer sites in `src` (Priority A
  shell/panels + Priority B editor/terminal/project-system). **Residual count: 0.**
- M4b factories use `SetBrush` for token-backed surfaces; interaction states
  remain on `DynamicResourceExtension` in `ControlThemeCatalog`.
- Added `ThemeBindingVariantRepaintTests` (border, text, `AppButton`, divider).
- `GetBrush` / `GetColor` resolve via variant fallback order (actual → light →
  default) so fresh test apps without a pinned variant still resolve tokens.
- Build, design-system tests (51), and `check-theme-tokens.sh` verified at M4d close.

## M4c result (2026-08-07)

Migrated listed panels off hand-wired hover/pressed overlay brushes to M4b factories
and `ControlThemeCatalog` interactive surfaces.

| File | Changes |
|---|---|
| `FileTreeView.cs` | `ListRow.Create` for tree rows; removed `_hoveredRow` + pointer hover handlers; selection/parent-folder paint preserved |
| `TownhallNavigationPanel.cs` | `ListRow.Create` for channel/direct rows; removed pointer hover; active-row accent overlay preserved |
| `TownhallPeoplePanel.cs` | `ListRow.Create` for agent rows; removed pointer hover |
| `NavBar.cs` | `AppButton.IconSurface` replaces manual hover overlay borders + pointer handlers; `PointerPressed` retained for mode switch |
| `StatusBar.cs` | `AppButton.Ghost` for settings; removed pointer hover/pressed background swapping; active `OverlaySelectedBrush` when settings open retained |
| `BottomPanelHost.cs` | `AppButton.ToolbarLabel` for mode strip tabs |
| `SourceControlPanel.cs` | `PanelChrome.SectionHeader`, `AppButton.Icon/Secondary/Primary`, `AppTextBox.Input` |

**Not migrated:** `TownhallView.cs` (no hover/chrome to unify).

**Residual pointer handlers (intentional):**

| File | Handler | Reason |
|---|---|---|
| `NavBar.cs` | `PointerPressed` on icon surfaces | Mode switch commands (not hover chrome) |
| `TownhallNavigationPanel.cs` | `PointerPressed` on rows | Selection/navigation |
| `TownhallPeoplePanel.cs` | `PointerPressed` on openable agent rows | Open DM |
| `FileTreeView.cs` | `PointerPressed` on header text | Pick folder |

Build, design-system tests, panel source tests, and `check-theme-tokens.sh` verified at M4c close.

## M4b result (2026-08-07)

- Added internal factories under `src/UI/DesignSystem/Controls/`:
  - `AppButton` — `Primary`, `Secondary`, `Ghost`, `Icon`, `ToolbarLabel`, `IconSurface`
  - `AppTextBox` — `Input`, `Search`, `Inline`
  - `ListRow` — `Create`, `SetSelected`, `DefaultPadding`
  - `PanelChrome` — `Divider`, `EmptyState`, `SectionTitle`, `SectionHeader`, `HeaderBar`
- `ControlThemeCatalog.ApplyInteractiveSurface` wires `zaide-interactive` +
  `Zaide.ControlTheme.InteractiveSurface` on row/icon hosts.
- `ControlFactoryTests` smoke coverage for factory creation, token hooks, and catalog classes.
- Build clean; design-system tests green; theme-token guard passes.

## M4a result (2026-08-07)

- Added `src/UI/DesignSystem/Controls/ControlThemeCatalog.cs`: C#-built
  `ControlTheme` + `Style` objects for hover, pressed, selected, focus, and
  disabled interaction states using `OverlayHoverBrush`, `OverlayPressedBrush`,
  `OverlaySelectedBrush`, and `BorderFocusBrush` via `DynamicResourceExtension`.
- Registered catalog in `App.Initialize()` after XAML load (idempotent; test
  bootstrap picks it up through the same `App` path).
- `zaide-interactive` / `zaide-selected` style classes and
  `Zaide.ControlTheme.InteractiveSurface` resource key for M4b consumers.
- `ControlThemeCatalogTests` smoke coverage for registration and focus-ring token.
- Build clean; design-system tests green; theme-token guard passes.
