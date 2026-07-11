# Phase 8.1.7.2: Settings UX Expansion — Implementation Plan

## Purpose

Turn the existing Phase 8.1 settings slide-over into a usable settings surface
for the preferences that the runtime already owns. This plan does not change
settings persistence, schema ownership, or the Phase 8.1 completion status.

## Dependencies

- Phase 8.1 M1–M5 and M6 closeout remain the baseline.
- The existing immutable `SettingsModel` and `SettingsViewModel` candidate,
  validation, conflict, rebase, discard, and `ApplyAsync` contract remain in
  use.
- `ISecretStore` remains the only API-key persistence boundary.

## Scope

- Organize `SettingsPanelView` into clearly labelled Editor, Terminal, and LLM
  sections.
- Expose the existing editor preferences: code/prose font families and sizes,
  terminal font family and size, tab size, indentation behavior, and
  whitespace visibility options.
- Extend `SettingsViewModel` with narrow candidate-update methods or equivalent
  bindings while preserving immutable `with`-based candidates.
- Make validation errors, Apply, Discard, conflict, and Rebase / Refresh state
  legible and actionable.
- Preserve secret isolation and password-field behavior for API-key editing.
- Add focused ViewModel/UI tests for field projection, candidate updates,
  validation, conflict/rebase, Apply/Discard, and secret isolation.

## Out of Scope

- Settings schema or persistence redesign.
- New settings categories, general chrome typography, provider registry, or
  keybinding editing.
- Menu bar, command palette, project context, Phase 8.2, or Phase 8.3 behavior.
- Desktop acceptance and screenshot closeout; those belong to `8.1.7.3`.

## Implementation Order

1. Add focused candidate-update coverage for each newly exposed setting.
2. Expand the ViewModel surface without bypassing validation or `ApplyAsync`.
3. Build the labelled Editor, Terminal, and LLM sections in the existing C#
   panel.
4. Add UI-level coverage for visibility, feedback, secret isolation, and
   transient disposal.
5. Run the focused tests and leave desktop evidence to `.3`.

## Verification

- `dotnet build Zaide.slnx --no-restore` reports 0 warnings and 0 errors.
- `dotnet test Zaide.slnx --no-build` is green.
- `git diff --check` is clean.
- Existing runtime settings projection tests remain green.
- The panel uses the existing transient ViewModel lifecycle and does not leak
  subscriptions or secret values.

## Exit Conditions

- [ ] Editor, Terminal, and LLM settings are visibly labelled and editable.
- [ ] The candidate remains immutable and all changes use the existing
      validation and conflict-aware Apply flow.
- [ ] API keys remain confined to `ISecretStore` and are absent from settings
      persistence and diagnostics.
- [ ] Focused tests cover the new controls and existing settings lifecycle.
- [ ] No Phase 8.2 or Phase 8.3 behavior was added.

## Rollback Plan

Revert only the settings UX changes if panel lifetime, validation, secret
isolation, or runtime settings application regresses. Retain the accepted
Phase 8.1 M6 baseline.
