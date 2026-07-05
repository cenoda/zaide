# ISSUE-004: Nav Icons Invisible and Unclickable

**Label:** BUG
**Status:** in-progress
**Priority:** high
**Related:** `src/Views/NavBar.cs`, `src/Views/IconFactory.cs`, `src/Styles/Icons.axaml`

## Description

Replacing the nav bar emoji labels with vector icons made the Explorer and
Source Control icons invisible. A follow-up rendering attempt also made the
left-panel nav unclickable.

## Steps to Reproduce

1. Launch Zaide.
2. Look at the far-left nav bar.
3. Click the Explorer and Source Control nav buttons.

**Expected behavior:** The nav icons are visible, and clicking each 32x32 nav
button switches the left panel mode.

**Actual behavior:** The icons are not visible, and the left-panel nav can stop
responding to clicks.

## Debug Log

### Attempt 1
- **Hypothesis:** `PathIcon` can render the embedded `StreamGeometry` resources
  directly when `Foreground`, `Width`, and `Height` are set.
- **Action:** Added `IconFactory.Create(...)` returning `PathIcon`, wired
  `Icons.axaml`, and replaced nav emoji text with `PathIcon`.
- **Result:** Build and tests passed, but icons were invisible at runtime.
- **Error / Output:** User reported "icon is invisible".

### Attempt 2
- **Hypothesis:** `PathIcon.Foreground` was not painting the geometry, so an
  explicit `Shapes.Path` with `Fill` and `Stretch = Uniform` would render it.
- **Action:** Changed `IconFactory.Create(...)` to return `Path`.
- **Result:** Build and tests passed, but runtime UI was still broken; the left
  panel became unclickable.
- **Error / Output:** User reported "it is not only invisible but also
  unclickable(left pannel)".

## Resolution

- **Root cause:** Pending.
- **Fix:** Pending.
- **Commit:** Pending.
- **Closed date:** Pending.
