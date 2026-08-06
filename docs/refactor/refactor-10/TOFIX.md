# Refactor 10: UI Polish — Theming and Shared Control Layer — TOFIX

## Status

M0 (plan and archive) is complete. M1 is the next task.

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
| M1 — theme variant infrastructure | next | key-parity test + forced runtime variant flip |
| M2 — semantic tokens and both ramps | pending | contrast-ratio tests + full suite |
| M3 — literal replacement and guard | pending | `scripts/check-theme-tokens.sh` |
| M4 — shared control layer | pending | full suite + interaction walkthrough |
| M5 — glass, depth, motion | pending | main screens with and without blur + full suite |

## Open questions

- None. All four scoping decisions were confirmed on 2026-08-06.

## Next task

M1: split `App.axaml` into `Tokens/Light.axaml`, `Tokens/Dark.axaml`, and
`Tokens/Shared.axaml` under `ResourceDictionary.ThemeDictionaries`, remove the
variant pin, add `ThemeBinding`, and eliminate the four `static readonly` brush
caches.
