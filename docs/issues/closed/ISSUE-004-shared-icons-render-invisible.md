# ISSUE-004: Shared icons render invisible across non-nav surfaces

**Label:** BUG
**Status:** closed
**Priority:** high
**Related:** `src/Views/IconFactory.cs`, `src/Styles/Icons.axaml`, `FileTreeView.cs`, `StatusBar.cs`, `TerminalPanel.cs`, `EditorView.cs`, `TownhallInputArea.cs`

## Description

Shared icons created through `IconFactory` are still invisible at runtime across
multiple non-nav surfaces, even though the icon placeholders clearly occupy
layout space.

The failure is broader than one view. It affects file tree header/rows, status
bar segments, Townhall icons, terminal toolbar/header, and editor file info
chrome. The left nav remains visible only because it uses local custom `Path`
geometry instead of the shared icon path.

The current evidence suggests the resource dictionary is loaded, but the shared
icon rendering pipeline is still wrong or incomplete for Avalonia's runtime
behavior.

## Steps to Reproduce

1. Launch Zaide with the current shared icon changes.
2. Open the default Townhall/file-tree shell.
3. Inspect file tree rows/header, status bar, Townhall pinned/bell icons, and
   send button.

**Expected behavior:** Shared icons render visibly anywhere `IconFactory.Create`
is used.
**Actual behavior:** Icon slots consume space but appear blank/invisible.

## Debug Log

### Attempt 1: Add shared `StreamGeometry` resources and use `Fill`
- **Hypothesis:** The app lacked proper shared icon assets; adding Phosphor
  geometries to `Icons.axaml` and rendering them via `Path.Fill` would replace
  emoji/text glyphs.
- **Action:** Added icon resources, wired `App.axaml` merge, and used
  `IconFactory.Create(...)` across several views.
- **Result:** Icons stayed invisible at runtime, although the replaced UI still
  reserved icon-sized layout space.
- **Error / Output:** User reported the icon surfaces were "invisible yet."

### Attempt 2: Suspect missing import / resource load failure
- **Hypothesis:** `Icons.axaml` might not be imported correctly, so the
  resources were missing at runtime.
- **Action:** Re-checked `App.axaml`, verified merged dictionaries, and
  confirmed `IconFactory` was retrieving resources through
  `Application.Current!.Resources[resourceKey]`.
- **Result:** Import path appears correct. This did not explain why layout
  changed but nothing painted.
- **Error / Output:** No runtime exception; blank icons persisted.

### Attempt 3: Switch shared renderer from `Fill` to `Stroke`
- **Hypothesis:** The Phosphor geometries are mostly open stroke-style paths, so
  drawing them with `Fill` produces invisible output. Rendering with `Stroke`
  plus thickness/caps/joins should make them visible.
- **Action:** Updated `IconFactory` to use `Path.Stroke`,
  `StrokeThickness = 16`, `StrokeLineCap = Round`, and `StrokeJoin = Round`.
  `SetForeground` was updated to set `Stroke` instead of `Fill`.
- **Result:** Build and tests passed, but user still reported that the shared
  icons remained invisible in the app screenshot.
- **Error / Output:** `dotnet build Zaide.slnx` passed. `dotnet test Zaide.slnx --no-build`
  passed (402/402). Runtime issue remains unresolved.

### Attempt 4: Add isolated in-app icon probe
- **Hypothesis:** We need to separate resource-loading failure from wrapper
  failure and geometry-validity failure. Rendering one icon through multiple
  controlled paths in the same debug surface should reveal which layer is
  broken.
- **Action:** Added temporary `IconProbeView` and mounted it under the file tree
  header. The probe renders `Icon.Folder` in four variants:
  current `IconFactory`, direct stroke rendering, direct fill rendering, and a
  code-defined square using the same stroke wrapper.
- **Result:** Runtime screenshot shows only the code-defined square renders.
  `Factory`, direct resource `Stroke`, and direct resource `Fill` all remain
  blank.
- **Error / Output:** `dotnet build Zaide.slnx` passed after replacing the
  initial `UniformGrid` attempt with a plain `Grid`.

### Attempt 5: Compare resource geometry against the same path parsed in code
- **Hypothesis:** If the exact folder path string renders when parsed directly
  in code, then `Icons.axaml` resource loading is the bad layer. If the
  code-parsed folder path is also blank, the path data itself is incompatible
  with Avalonia rendering.
- **Action:** Expanded `IconProbeView` to render five variants:
  `Factory`, `Resource Stroke`, `Resource Fill`, `Parsed Stroke` using the same
  folder path string via `StreamGeometry.Parse(...)`, and `Code Shape`. Also
  added a text line showing `Resource bounds`.
- **Result:** Runtime screenshot shows `Parsed Stroke` and `Code Shape`
  visible, while all resource-backed variants remain blank. The probe reports
  `Resource bounds: <missing>`.
- **Error / Output:** This strongly indicates the geometry path data is valid,
  but merged-dictionary icon resource lookup is failing.

### Attempt 6: Replace raw resource indexer with `TryFindResource`
- **Hypothesis:** `Application.Current!.Resources[resourceKey]` does not resolve
  the merged icon dictionary the way `TryFindResource(...)` does. Shared icons
  remain blank because `IconFactory` is looking in the wrong resource layer.
- **Action:** Updated `IconFactory` to resolve icon geometries through
  `Application.Current.TryFindResource(resourceKey, app.ActualThemeVariant, out var value)`
  before creating the `Path`.
- **Result:** Fixed. Runtime screenshot shows the `Factory` probe cell visible,
  and real shared-icon surfaces now render in the app: file tree header icon,
  People bell, Channels pin, status bar icons, and send arrow.
- **Error / Output:** `dotnet build Zaide.slnx` passed. `dotnet test Zaide.slnx --no-build`
  passed (402/402).

## Next Diagnostic Steps

1. Remove temporary `IconProbeView`.
2. Keep shared icon rendering on the `TryFindResource(...)` path.
3. Continue icon rollout only on normal UI surfaces.

## Resolution

- **Root cause:** `IconFactory` resolved icons through
  `Application.Current!.Resources[resourceKey]`, which did not retrieve the
  merged `Icons.axaml` geometry resources at runtime. The shared icon pipeline
  therefore received no geometry, while locally parsed path data still worked.
- **Fix:** Resolve shared icon geometries with
  `Application.Current.TryFindResource(resourceKey, app.ActualThemeVariant, out var value)`
  and keep rendering them as stroked paths.
- **Commit:** Pending.
- **Closed date:** 2026-07-06
