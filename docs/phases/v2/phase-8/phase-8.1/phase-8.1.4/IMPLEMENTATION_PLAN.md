# Phase 8.1.4: Runtime Editor and Terminal Settings — Implementation Plan

## Scope

**Goal:** Implement M4 only: apply committed settings to the editor and terminal
through their real construction and disposal paths.

**Dependencies:** Phase 8.1.1 is complete and green.

**Out of scope:** Settings-panel UI, secret/LLM work, folder lifecycle,
keybindings, project context, M5–M6, and Phase 8.2/8.3 work.

## Implementation Contract

- Thread `ISettingsService` through `MainWindow.BuildLayout()` → `EditorView`
  and `TerminalTabHost` → `TerminalPanel` → `TerminalRenderControl`. Do not
  replace this explicit composition path with a hidden service locator.
- Each surface applies `settings.Current` at construction and subscribes to
  `WhenChanged` using `ObserveOn(RxApp.MainThreadScheduler)` before changing
  UI state.
- `EditorView` applies code/prose font settings, code size, tab size, insertion
  mode, and whitespace options through AvaloniaEdit. Existing indent-guide code
  continues to derive indentation from editor options.
- Replace terminal readonly font state with mutable state and
  `ApplyFontSettings`. It recomputes character-cell metrics and invalidates
  measure/visual state without panel reconstruction.
- Define disposal ownership: `EditorView`, `TerminalPanel`, and
  `TerminalTabHost` implement the required disposal behavior; removal, host
  replacement, and window close release subscriptions exactly once.

## Required Tests

- Editor initial and live option application.
- Terminal font update recalculates metrics and invalidates rendering without
  recreating a panel.
- Terminal removal, host replacement, and window close dispose subscriptions;
  no subscriptions are owned or disposed by `ISettingsService` consumers.

## Exit Conditions

- [ ] M4 tests, build, and full test suite are green.
- [ ] No Settings UI, keybinding, project-context, or M5+ behavior was added.

## Rollback Plan

- Revert the isolated M4 commit if runtime setting changes leak subscriptions or
  alter editor/terminal behavior beyond the configured preferences.
