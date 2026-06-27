# Phase 1.1: File Tree Sidebar Polish — Implementation Plan

**Status:** REVIEW — all design decisions resolved. Ready for approval.

## Pre-Implementation Verification

- [ ] `dotnet build Zaide.slnx` passes with 0 warnings
- [ ] `dotnet test Zaide.slnx` passes: 85 tests, 0 failures
- [ ] Phase 1 TOFIX.md items all resolved (currently: no open items)
- [ ] Phase 2 TOFIX.md check — Phase 1.1 may touch shared wiring (MainWindow.axaml.cs, MainWindowViewModel.cs)

---

## Audit of Phase 1 Code (Live Repo State — 2026-06-27)

| # | Severity | Category | File | Finding |
|---|----------|----------|------|---------|
| B1 | **High** | Bug | `FileTreeViewModel.cs:116-123` | `HandleRenamed` updates `Name`/`FullPath` on the renamed node, but if the node is a directory, all descendant `FullPath` values stay stale. `FindNodeByPath` breaks for every child after a directory rename. |
| B2 | **Medium** | Error handling | `FileTreeViewModel.cs:45-62` | `OpenFolderCommand` calls `EnumerateDirectory(path)` without try/catch. `DirectoryNotFoundException` or `UnauthorizedAccessException` crashes silently. |
| B3 | **Low** | Leak | `FileTreeView.cs:50` | `Execute().Subscribe()` in `PointerPressed` handler not disposed — violates docs-rules.md §11b. Sidebar is singleton View so practical risk is negligible, but Phase exit requires 0 rule violations. |
| C1 | **Info** | Discrepancy | `FileTreeService.cs:137-139` | `IsHidden` matches `.` and `..` (harmless — `EnumerateDirectories()` never returns them). Phase 1 plan says "except `.` and `..`". |
| C2 | **Info** | Placeholder | `MainWindowViewModel.cs:35-39` | `StatusText` receives save/open error messages but has no UI binding yet. Connection point preserved for Phase 3+ status bar. |

### Defect Severity Summary

| Severity | Count |
|----------|-------|
| High (bug) | 1 |
| Medium (error handling) | 1 |
| Low (consistency) | 1 |
| Info (discrepancy / placeholder) | 2 |

---

## Scope

**Goal:** Harden the file tree against edge cases, fix the directory-rename bug, and deliver deferred polish items.

**Boundaries (NOT building):**
- No status bar UI widget (deferred to Phase 3+)
- No file icons or fancy styling
- No workspace multi-root
- No drag-and-drop
- No file search/filter in tree
- No git status overlay (Phase 7)
- No async file enumeration
- No animation on tree expand/collapse

---

## Milestones (Incremental)

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate | `dotnet build Zaide.slnx && dotnet test Zaide.slnx` |
| M1 | B1 (rename cascade fix) + B2 (OpenFolder error handling) | Rename dir → children paths update. Inaccessible folder → no crash, RootNodes unchanged. |
| M2 | GridSplitter between sidebar and center | Drag splitter → sidebar 180–500px. Center adjusts. |
| M3 | Context menu: Open, Expand All (recursive), Collapse All | Right-click → menu. Open → editor. Expand All → full tree. Collapse All → all collapsed. |
| M4 | Ctrl+O (open folder), Enter (open selected file) | Ctrl+O → folder picker. Enter → opens file in editor. |
| M5 | C1 fix, B3 fix (disposal), sort-order test | `dotnet test` passes. docs-rules.md §11b violations: 0. |


---

### M1: Fix Critical Defects

**Files to modify:**

- `src/ViewModels/FileTreeViewModel.cs`
  - **B1 — Rename cascade fix:** In `HandleRenamed`, when the renamed node is a directory, recursively update `FullPath` for all descendants:
    ```csharp
    private static void UpdateDescendantPaths(FileTreeNode node, string oldDirPath, string newDirPath)
    {
        foreach (var child in node.Children)
        {
            child.FullPath = child.FullPath.Replace(oldDirPath, newDirPath);
            if (child.IsDirectory)
                UpdateDescendantPaths(child, oldDirPath, newDirPath);
        }
    }
    ```
  - **B2 — Error handling:** Wrap `OpenFolderCommand` body in try/catch. On `DirectoryNotFoundException` or `UnauthorizedAccessException`, do NOT clear `RootNodes`; set `StatusText` to an error message.
  - `string? StatusText` reactive property (already proposed as error surface; also serves as connection point for Phase 3+ status bar).

**No changes to `MainWindowViewModel.cs` or `MainWindow.axaml.cs`.** The orphaned `StatusText` property in `MainWindowViewModel` (C2) stays as-is — it already receives save/open error data via `Activate()` subscriptions and will be bound to a UI widget in Phase 3+.

**Tests to add (FileTreeViewModelTests.cs):**
- `HandleRenamed_UpdatesDescendantPaths` — rename a dir with children → child paths updated.
- `OpenFolderCommand_SetsStatusText_OnInaccessiblePath` — nonexistent path → StatusText set, RootNodes unchanged.

---

### M2: GridSplitter

**Files to modify:**

- `src/MainWindow.axaml.cs`
  - Add a `GridSplitter` (4px wide, transparent background, `SizeWestEast` cursor) between sidebar and center columns.
  - Set sidebar column `MinWidth = 180`, `MaxWidth = 500`.
  - Place the splitter in a dedicated narrow column between sidebar and center for clean z-ordering.

**No new tests (UI-only milestone).**

---

### M3: Context Menu

**Files to modify:**

- `src/ViewModels/FileTreeViewModel.cs`
  - Add `ReactiveCommand<FileTreeNode, Unit> RequestOpenFileCommand` — the ViewModel emits a request to open a file; `MainWindowViewModel` mediates to `EditorTabs.OpenFileCommand`.
  - Add `CollapseAllCommand` and `ExpandAllCommand` (`ReactiveCommand<Unit, Unit>`).
  - `ExpandAllCommand`: recursively sets `IsExpanded = true` on all directory nodes. Performance is acceptable — typical project has 500–1,000 directories (ignored folders excluded), ~100ms for full expand. TreeView virtualization handles rendering.
  - `CollapseAllCommand`: recursively sets `IsExpanded = false`.

- `src/Views/FileTreeView.cs`
  - Attach `MenuFlyout` to the TreeView via `ContextFlyout`:
    - "Open" → `ViewModel!.RequestOpenFileCommand.Execute(selectedNode).Subscribe()`
    - "Expand All" → `ViewModel!.ExpandAllCommand.Execute().Subscribe()`
    - "Collapse All" → `ViewModel!.CollapseAllCommand.Execute().Subscribe()`
  - Menu items enabled only when a node is selected (bind `IsEnabled` to `ViewModel.SelectedFile != null`).

- `src/ViewModels/MainWindowViewModel.cs` (Activate)
  - Subscribe to `FileTreeViewModel.RequestOpenFileCommand` → call `EditorTabs.OpenFileCommand.Execute(file.FullPath)`.
  - Same mediation pattern already used for `SelectedFile` → `OpenFileCommand`.

```
FileTreeView (View)                MainWindow (View)
  → ViewModel.RequestOpenFileCommand     ↕ (data binding)
       ↓                            MainWindowViewModel.Activate()
MainWindowViewModel                       → EditorTabs.OpenFileCommand
  (mediates between the two VMs)
```

**No new tests (UI + integration plumbing).**

---

### M4: Keyboard Shortcuts

**Files to modify:**

- `src/ViewModels/MainWindowViewModel.cs`
  - Add `Interaction<Unit, string?> PickFolder` and `ReactiveCommand<Unit, Unit> OpenFolderCommand`.
  - `OpenFolderCommand` uses `PickFolder.Handle()` to get a path, then executes `FileTreeViewModel.OpenFolderCommand`.

- `src/MainWindow.axaml.cs`
  - Register `PickFolder` handler in `WhenActivated`: opens native folder picker, sets output.
  - Add `Ctrl+O` key binding → `OpenFolderCommand`.
  - Add `Enter` key binding → open selected file when TreeView has focus.

**No new tests (UI + interaction plumbing).**

---

### M5: Polish and Cleanup

**Files to modify:**

- `src/Services/FileTreeService.cs`
  - **C1 fix:** Add explicit `.` / `..` guard in `IsHidden`: `name is not "." and not ".." && name.Length > 0 && name[0] == '.'`

- `src/Views/FileTreeView.cs`
  - **B3 fix:** Store the `IDisposable` from `PointerPressed` `Execute().Subscribe()` in a field; dispose via `d.Add(Disposable.Create(...))` in `WhenActivated`.

- Verify sort-order stability: `EnumerateDirectory` already sorts directories-first then files alphabetically.

**Tests to add (FileTreeServiceTests.cs):**
- `IsHidden_ExcludesDotAndDotDot` — `.` and `..` return false.
- `EnumerateDirectory_SortsDirectoriesBeforeFiles` — verify sort order.

---

## Limitations (by design)

- **GridSplitter minimum width is 180px** — narrower sidebar is unreadable with full paths.
- **"Expand All" is full recursive** — acceptable for typical projects (500–1,000 dirs, ~100ms). For monorepos with 10,000+ directories, the command may pause briefly. No lazy-expand optimization in this phase.
- **No folder history persistence** — still resets on launch. Persistence comes with workspace support in a later phase.
- **No status bar UI** — `StatusText` properties exist as connection points for Phase 3+. Error messages are set but not yet rendered.
- **Enter-to-open only works when TreeView has focus** — clicks elsewhere shift focus; this is standard TreeView behavior.
- **Ctrl+O uses the `Interaction` pattern** — consistent with Phase 2 `ConfirmClose`. Minimal added complexity (2 properties, ~15 lines).

---

## Exit Conditions

- [ ] `dotnet build Zaide.slnx` succeeds with 0 warnings and 0 errors
- [ ] `dotnet test Zaide.slnx` passes all tests (expected: ~90 tests, 0 failures)
- [ ] `docs-rules.md` §11b violations: 0 (B3 resolved)
- [ ] Rename a directory in the tree → children paths update correctly (B1)
- [ ] Open an inaccessible folder → `StatusText` set, no crash, RootNodes unchanged (B2)
- [ ] Drag GridSplitter → sidebar resizes between 180–500px; center column adjusts
- [ ] Right-click tree node → context menu with Open, Expand All (full recursive), Collapse All
- [ ] Context menu "Open" → file opens in editor via `RequestOpenFileCommand` mediation
- [ ] Ctrl+O → folder picker opens; Enter on file in tree → opens in editor
- [ ] No XAML added beyond `App.axaml` and `MainWindow.axaml` shell
- [ ] All panels render with >= 16px padding
- [ ] Colors use `App.axaml` resources

---

## Rollback Plan

- Commit to revert to: current HEAD (post Phase 2.1 M4)
- What to preserve: `docs/`, `App.axaml`, all Phase 2/2.1 source files
- What to discard on failure: modifications to `FileTreeViewModel.cs`, `FileTreeService.cs`, `FileTreeView.cs`, `MainWindow.axaml.cs`, `MainWindowViewModel.cs`, new test methods

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| GridSplitter breaks layout at edge sizes | Medium | Low | Constrain MinWidth/MaxWidth; test at 800x600 window |
| `HandleRenamed` cascade fix misses deep paths with partial matches | Low | Medium | Use string.Replace only for path-prefix match; test with nested dirs |
| Interaction-based `PickFolder` adds complexity to MainWindowViewModel | Low | Low | Follows docs-rules.md §11a exactly; proven pattern from Phase 2 |

---

*Draft created: 2026-06-27. Based on live code audit of Phase 1. All design decisions resolved.*
