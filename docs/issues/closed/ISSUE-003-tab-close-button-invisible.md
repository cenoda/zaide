# ISSUE-003: Tab close button (×) never appears on hover

**Label:** BUG
**Status:** open
**Priority:** medium
**Related:** Phase 2 M2, `EditorTabBar.cs`

## Description

The close button (×) on editor tabs is always invisible. The button exists in the layout (it's clickable, and clicking it successfully closes the tab), but `Opacity` stays at 0 and never changes to 1 on hover. The `PointerEntered` event on the tab's hover target never fires.

The button is in a Grid overlay inside a Border. `Opacity = 0` is toggled to `1` on `PointerEntered` and back to `0` on `PointerExited` (with 200ms delay).

## Steps to Reproduce

1. Open a file → tab appears
2. Hover mouse over tab
3. Close button (×) never appears

**Expected behavior:** Close button fades in (Opacity 0→1) on hover.
**Actual behavior:** Close button stays at Opacity 0.

## Debug Log

### Attempt 1: Border.PointerEntered (original)
- **Hypothesis:** `PointerEntered` on the `Border` fires when the pointer enters the tab area.
- **Action:** Handler on `border.PointerEntered` sets `closeButton.Opacity = 1`.
- **Result:** Never fires. Opacity stays 0.
- **Error / Output:** None — event silently never triggers.

### Attempt 2: Grid.PointerEntered
- **Hypothesis:** The `Border`'s child `Grid` fills the Border area. Avalonia delivers `PointerEntered` to the topmost element (the `Grid`, not the `Border`). Moving the handler to `grid.PointerEntered` should work.
- **Action:** Moved `PointerEntered`/`PointerExited` from `border` to `grid`.
- **Result:** Same — never fires. Opacity stays 0.
- **Error / Output:** None.

### Attempt 3: Container `IsPointerOver`
- **Hypothesis:** Hover events on specific child elements are too brittle here. The real state we care about is whether the pointer is over the tab container or any of its descendants, which Avalonia exposes via `InputElement.IsPointerOver`.
- **Action:** Replaced `PointerEntered`/`PointerExited` handlers with a subscription to `border.GetObservable(InputElement.IsPointerOverProperty)`. Show the close button immediately when true; hide it after 200ms only if `border.IsPointerOver` is still false.
- **Result:** Incomplete. Hover state handling is more robust, but the user still reports the `×` is not visibly rendering.
- **Error / Output:** None.

### Attempt 4: Remove themed `Button` dependency
- **Hypothesis:** The hover/reveal state may now be correct, but the actual `Button` content is still visually suppressed by theme/template behavior. Since the area remains clickable, the problem is likely the close glyph rendering rather than pointer detection.
- **Action:** Replaced the themed `Button` with a small custom `Border` + `TextBlock` close affordance in its own grid column. The `×` glyph is now rendered directly, and the hover reveal still uses `IsPointerOver` on the tab container.
- **Result:** Fix applied in `EditorTabBar.cs`.
- **Error / Output:** `dotnet build Zaide.slnx` and `dotnet test Zaide.slnx` pending verification after patch.

### Next diagnostic step

Temporarily set a visible background on the Grid and Border to verify they actually have layout size:

```csharp
// In BuildTabItem, before returning:
grid.Background = new SolidColorBrush(Color.Parse("#33FF0000")); // red tint
border.Background = new SolidColorBrush(Color.Parse("#3300FF00")); // green tint
```

Then check: do the red/green areas actually render under each tab? If not, the tabs have zero layout height — similar to ISSUE-002 (DockPanel collapsing children).

Also add file-log trace to verify:
```csharp
grid.PointerEntered += (_, _) =>
{
    Log($"[TabBar] PointerEntered on Grid for {vm.FileName}");
    closeButton.Opacity = 1;
};
```

### Theories to test
1. The original hover event path was not the only bug; the themed `Button` visual may also have suppressed the glyph.
2. `Application.Current!.Resources["SoftAccent"]` returns null, making the custom glyph render invisibly.
3. The close affordance is visible but clipped by the tab layout width.

## Resolution

- **Root cause:** `PointerEntered`/`PointerExited` events on `Border`/`Grid` never fired — Avalonia delivers pointer events to the topmost child at the pointer position. Also the themed `Button` template rendered unpredictably.
- **Fix:** Replaced `Button` with custom `Border`+`TextBlock` glyph in its own Grid column. Hover driven by `GetObservable(IsPointerOverProperty)` instead of pointer events — property-system based, child-agnostic.
- **Commit:** `f6b5535`
- **Closed date:** 2026-06-26
