# ISSUE-007: Unsaved-changes dialog crashes with XAML InvalidCastException

**Label:** BUG
**Status:** closed
**Priority:** critical
**Related:** `src/Views/UnsavedDialog.axaml` (out-of-band production crash; not Phase 13
hardening)

## Description

Closing a dirty editor tab crashed the process before the unsaved-changes dialog
appeared. ReactiveUI surfaced the failure as `UnhandledErrorException` with an
inner `System.InvalidCastException` during compiled Avalonia XAML population of
`UnsavedDialog`.

This is **not** Phase 13 performance or recovery work; it is a focused bugfix so
normal dirty-tab close and Phase 13 M0 desktop timing can continue.

## Steps to Reproduce

1. Launch `zaide` from the repository root.
2. Open `tests/fixtures/workflow-console/Program.cs`.
3. Make an unsaved edit.
4. Close the tab (trigger unsaved-changes confirmation).

**Expected behavior:** Unsaved-changes dialog appears (Save / Don't Save / Cancel).
**Actual behavior:** Process crashes before the dialog appears:

```text
ReactiveUI.UnhandledErrorException
  inner: System.InvalidCastException: Specified cast is not valid.
at CompiledAvaloniaXaml.XamlDynamicSetters.XamlDynamicSetter_2(Layoutable, Object)
at Zaide.Views.UnsavedDialog.!XamlIlPopulate(...)
in src/Views/UnsavedDialog.axaml:line 11
```

## Debug Log

### Attempt 1: Resource type vs property type
- **Hypothesis:** Line 11 used `Margin="{StaticResource SpacingXl}"`. `SpacingXl`
  is defined in `App.axaml` as `x:Double` (20), while `Layoutable.Margin` is
  `Thickness`. Compiled Avalonia XAML setters cast the resource value and throw
  `InvalidCastException`. `Spacing="{StaticResource SpacingLg}"` is valid because
  `StackPanel.Spacing` is `double`.
- **Action:** Removed the Double→Margin StaticResource binding. Named the root
  panel and applied `RootPanel.Margin = LayoutTokens.Uniform(LayoutTokens.SpacingXl)`
  in code-behind after `InitializeComponent`. Added `UnsavedDialogTests` covering
  construction, button clicks, dirty Save/Don't Save/Cancel, and clean-tab close.
- **Result:** Dialog constructs without cast exception (headless smoke:
  `construct=ok`, `margin=20,20,20,20`, button clicks ok). Dirty-tab
  ConfirmClose outcomes preserved; clean tabs still skip confirmation.
  Regression: `UnsavedDialogTests` (8 cases).
- **Error / Output:** Root cause matches the stack: `XamlDynamicSetter_2(Layoutable,
  Object)` is the Margin setter; Double cannot cast to Thickness.

## Resolution

- **Root cause:** `UnsavedDialog.axaml` bound `Margin` (Thickness) to the
  `SpacingXl` resource (`x:Double`). Compiled Avalonia XAML does not type-convert
  StaticResource values the way string attributes do; it casts and fails.
- **Fix:** Stop using Double spacing tokens as Margin StaticResources. Apply
  uniform SpacingXl margin via `LayoutTokens` in code-behind. Keep
  `Spacing="{StaticResource SpacingLg|SpacingSm}"` (double→double) in XAML.
- **Commit:** (recorded when landed)
- **Closed date:** 2026-07-15

## Phase note

Out-of-band production crash fix. Not Phase 13 M0/M1a/M1b scope. Phase 13 M0
desktop timing work can resume after this lands.
