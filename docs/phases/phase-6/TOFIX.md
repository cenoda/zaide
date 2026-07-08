# Phase 6 Smoke Test TOFIX

## Issue: Agent Panel Tab Strip Not Scrollable

During Phase 6 smoke testing, the agent panel host’s tab strip (`AgentPanelHostView`) was found to be non-scrollable when many agent panels are open. The editor tab bar (`EditorTabBar`) already used a working horizontal-scroll mechanism, but the agent panel was using a plain `StackPanel` wrapped in a horizontal container, which grants unlimited width and defeats scrolling.

## Root Cause

The first fix attempt wrapped the `_tabsPanel` in a `ScrollViewer` but **still placed that `ScrollViewer` inside a horizontal `StackPanel` (`leftStrip`)**. Avalonia gives horizontal `StackPanel` children unlimited width, so the `ScrollViewer` never received a constrained width to clip against — therefore no scrollbar appeared and the wheel handler had no effect.

## Fix Applied

1. Removed the horizontal `StackPanel` (`leftStrip`) wrapper completely.
2. Placed the `ScrollViewer` **directly in the `stripGrid`'s `Star` column** (same flat Grid placement used by `EditorTabBar`).
3. Added `VerticalAlignment = Center` on the `ScrollViewer` so tabs align vertically within the strip.
4. The `PointerWheelChanged` handler (vertical wheel → horizontal scroll, `delta * 50`, `e.Handled = true`) remains identical to `EditorTabBar`.

This change only affects the view layer; no ViewModel or test contract was altered.

## Verification

- `dotnet build Zaide.slnx --no-restore` → 0 warnings, 0 errors
- `dotnet test Zaide.slnx --no-build` → 721 passed, 0 failed

## Status

- [x] Fix implemented and verified by build/test
- [ ] Manual smoke test pending (visual confirmation of scrolling behavior)
</tool_call>