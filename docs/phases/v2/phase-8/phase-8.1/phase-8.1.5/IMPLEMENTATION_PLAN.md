# Phase 8.1.5: Settings UI — Implementation Plan

## Scope

**Goal:** Implement M5 only: the status-bar Settings entry and transient,
validated Settings panel lifecycle.

**Dependencies:** Phase 8.1.1, Phase 8.1.2, and Phase 8.1.4 are complete and
green. M5 always constructs `SettingsViewModel(ISettingsService, ISecretStore)`
and includes API-key editing, so M2 is an unconditional prerequisite.

**Out of scope:** Command/keybinding editing, project context, a menu bar,
command palette, M6 closeout, and Phase 8.2/8.3 work.

## Implementation Contract

- Add singleton `StatusBarViewModel`, receiving `MainWindowViewModel` and
  `ISettingsService`; convert `StatusBar` to bind it rather than bind the main
  ViewModel directly. Forward the actual caret, language, project, and branch
  sources reactively.
- Expose `OpenSettingsCommand`, which invokes
  `MainWindowViewModel.ShowSettings` (`Interaction<Unit, bool>`). `MainWindow`
  handles it by creating/removing a full-content, status-bar-visible slide-over
  `SettingsPanelView` in C#.
- Construct `SettingsViewModel(ISettingsService, ISecretStore)` transiently at
  the handler site. It owns a `with`-based candidate plus its base snapshot,
  inline validation, conflict-aware `ApplyAsync(base, candidate)` behavior,
  discard behavior, secret editing, and its subscriptions. Closing the overlay
  disposes that ViewModel before discarding the host.
- Project saved `Llm.Model` as `configured: {model}` through the status bar;
  hide the label if absent. It is not a claim about the effective runtime model,
  which can be overridden by environment variables.
- Keybindings are read-only if shown. Do not add registry-based editing.

## Required Tests

- Gear command → interaction → panel open/close bridge.
- Candidate validation, apply, discard, stale-candidate conflict/rebase, and
  secret persistence behavior.
- Transient `SettingsViewModel` disposal on panel removal.
- Configured-model display/hide behavior and UI-thread subscription delivery.

## Exit Conditions

- [ ] M5 tests, build, and full test suite are green.
- [ ] No command registry, keybinding editing, project context, or M6 closeout
      work was added.

## Rollback Plan

- Revert the isolated M5 commit if panel lifetime, UI-thread delivery, or
  settings application is unsafe; retain the previous green child-phase state.
