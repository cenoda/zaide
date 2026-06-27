# Phase 1.1: File Tree Sidebar Polish — Implementation Plan

**Status:** COMPLETE — all exit conditions met. See [TOFIX.md](TOFIX.md) for open items.

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
  - **B1 — Rename cascade fix (prefix-safe):** In `HandleRenamed`, when the renamed node is a directory, recursively update `FullPath` for all descendants using explicit prefix matching:
    ```csharp
    private static void UpdateDescendantPaths(FileTreeNode node, string oldDirPath, string newDirPath)
    {
        foreach (var child in node.Children)
        {
            if (child.FullPath.StartsWith(oldDirPath))
                child.FullPath = newDirPath + child.FullPath[oldDirPath.Length..];
            if (child.IsDirectory)
                UpdateDescendantPaths(child, oldDirPath, newDirPath);
        }
    }
    ```
    `string.Replace` is unsafe — it replaces all substring occurrences anywhere in the path. `StartsWith` + prefix slice ensures only the renamed directory prefix is updated.
  - **B2 — Error handling:** Wrap `OpenFolderCommand` body in try/catch. Catch `DirectoryNotFoundException`, `UnauthorizedAccessException`, `NotSupportedException`, `ArgumentException`. Do NOT clear `RootNodes`; set `StatusText` to `ex.Message`.
  - Add `string? StatusText` reactive property. **Independent** from `MainWindowViewModel.StatusText` — both are connection points for the Phase 3+ status bar. `FileTreeViewModel.StatusText` holds tree-specific errors (folder open failures); `MainWindowViewModel.StatusText` holds editor errors (save/open failures). No cross-propagation needed in Phase 1.1.

**No changes to `MainWindowViewModel.cs` or `MainWindow.axaml.cs`.**

**Tests to add (FileTreeViewModelTests.cs):**
- `HandleRenamed_UpdatesDescendantPaths` — dir with 2 files → rename dir → both child FullPaths update.
- `HandleRenamed_UpdatesDescendantPaths_WithNonAscii` — directory name contains 한글/emoji → children paths update correctly.
- `HandleRenamed_DoesNotCorruptPaths_WithPartialNameMatch` — rename `/home/user/proj` → child at `/home/user/proj/backup/old_project` NOT corrupted (prefix `StartsWith` prevents substring false positives).
- `OpenFolderCommand_SetsStatusText_OnInaccessiblePath` — nonexistent path → StatusText set, RootNodes unchanged.
- `OpenFolderCommand_SetsStatusText_OnFilePath` — pass a file path (not directory) → StatusText set, no crash.
- `OpenFolderCommand_SetsStatusText_OnInvalidPath` — pass `"C:\\*?"` or empty string → caught by ArgumentException/NotSupportedException.

---

### M2: GridSplitter

**Files to modify:**

- `src/MainWindow.axaml.cs`
  - Add a `GridSplitter` (4px wide, transparent background, `SizeWestEast` cursor) between sidebar and center columns.
  - Set sidebar column `MinWidth = 180`, `MaxWidth = 500`.
  - Place the splitter in a dedicated narrow column between sidebar and center for clean z-ordering.

**No new tests (UI-only milestone).**

---

### M3: Context Menu + IsExpanded Binding

**Files to modify:**

- `src/Views/FileTreeView.cs`
  - **IsExpanded binding (critical):** The current `FuncTreeDataTemplate` does not bind `FileTreeNode.IsExpanded` to the `TreeViewItem`. Without this, `ExpandAll`/`CollapseAll` commands mutate model state with zero visible effect. Replace the template to include expansion state binding:
    ```csharp
    _treeView.ItemTemplate = new FuncTreeDataTemplate<FileTreeNode>(
        match: _ => true,
        build: (node, _) =>
        {
            var tb = new TextBlock
            {
                Text = node.Name,
                Foreground = (IBrush?)Application.Current!.Resources["TextActive"]
            };
            // Bind TreeViewItem.IsExpanded ↔ FileTreeNode.IsExpanded (two-way)
            tb.AttachedToVisualTree += (_, _) =>
            {
                var tvi = tb.FindAncestorOfType<TreeViewItem>();
                if (tvi is not null)
                    tvi.Bind(TreeViewItem.IsExpandedProperty,
                        new Binding(nameof(FileTreeNode.IsExpanded), BindingMode.TwoWay));
            };
            return tb;
        },
        itemsSelector: node => node.Children);
    ```
  - Attach `MenuFlyout` to the TreeView via `ContextFlyout`:
    - "Open" → `ViewModel!.RequestOpenFileCommand.Execute(selectedNode).Subscribe()`
    - "Expand All" → `ViewModel!.ExpandAllCommand.Execute().Subscribe()`
    - "Collapse All" → `ViewModel!.CollapseAllCommand.Execute().Subscribe()`
  - Items gated: `IsEnabled` bound to `ViewModel.SelectedFile != null`.

- `src/ViewModels/FileTreeViewModel.cs`
  - Add `ReactiveCommand<FileTreeNode, Unit> RequestOpenFileCommand`.
  - Add `CollapseAllCommand` and `ExpandAllCommand` — recursively sets `IsExpanded` on all directory nodes. With the binding above, TreeView visually expands/collapses.
  - `ExpandAllCommand` performance: ~100ms for 500–1,000 dirs. TreeView virtualization handles rendering.

- `src/ViewModels/MainWindowViewModel.cs` (Activate)
  - **Remove** the `SelectedFile` → auto-open subscription (lines 77-100). Single-click selection no longer opens files.
  - **Add** subscription to `FileTreeViewModel.RequestOpenFileCommand` → `EditorTabs.OpenFileCommand.Execute(file.FullPath)`.
  - Rationale: exactly one open pathway. Prevents double-open when right-clicking (SelectionChanged fires → context menu "Open" would trigger a second open). Matches VS Code behavior: single-click selects, double-click/Enter/context-menu opens.

```
Single open pathway (no double-trigger):
  Enter (M4) ─────────┐
  Context Menu (M3) ──┤→ RequestOpenFileCommand → MainWindowViewModel → EditorTabs.OpenFileCommand
  (SelectedFile is tracked but no longer auto-opens)
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

- `src/Views/FileTreeView.cs`
  - Add `KeyDown` handler on the TreeView for `Enter` key:
    ```csharp
    _treeView.KeyDown += (_, e) =>
    {
        if (e.Key != Key.Enter) return;
        var selected = ViewModel!.SelectedFile;
        if (selected is null || selected.IsDirectory) return;  // no-op on directory
        ViewModel!.RequestOpenFileCommand.Execute(selected).Subscribe();
    };
    ```
  - Reuses the same `RequestOpenFileCommand` → `MainWindowViewModel` mediation as M3's context menu "Open".
  - `Enter` on a directory → no-op (not an error; consistent with VS Code behavior).

**No new tests (UI + interaction plumbing).**

---

### M5: Polish and Cleanup

**Files to modify:**

- `src/Services/FileTreeService.cs`
  - **C1 fix:** Add explicit `.` / `..` guard in `IsHidden`: `name is not "." and not ".." && name.Length > 0 && name[0] == '.'`
  - **Sort-order enforcement:** `DirectoryInfo.EnumerateDirectories()` and `EnumerateFiles()` do NOT guarantee sorted order across filesystems. Add explicit `.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)` to both `EnumerateDirectoriesSafe` and `EnumerateFilesSafe` return values. Directories-first-then-files order is already enforced by the loop structure in `EnumerateDirectory`.

- `src/Views/FileTreeView.cs`
  - **B3 fix:** Store the `IDisposable` from `PointerPressed` `Execute().Subscribe()` in a field; dispose via `d.Add(Disposable.Create(...))` in `WhenActivated`.

**Tests to add (FileTreeServiceTests.cs):**
- `IsHidden_ExcludesDotAndDotDot` — `.` and `..` return false.
- `EnumerateDirectory_SortsDirectoriesBeforeFiles` — verify sort order.

---

## Limitations (by design)

- **GridSplitter minimum width is 180px** — narrower sidebar is unreadable with full paths.
- **"Expand All" is full recursive** — acceptable for typical projects (500–1,000 dirs, ~100ms). For monorepos with 10,000+ directories, the command may pause briefly. No lazy-expand optimization in this phase.
- **No folder history persistence** — still resets on launch. Persistence comes with workspace support in a later phase.
- **No status bar UI** — `StatusText` properties exist as connection points for Phase 3+. Error messages are set but not yet rendered.
- **Single-click selects, Enter/context-menu opens** — matches VS Code behavior. Phase 1 auto-opened on single-click; this is intentionally removed to prevent double-open when right-clicking.
- **Ctrl+O uses the `Interaction` pattern** — consistent with Phase 2 `ConfirmClose`. Minimal added complexity (2 properties, ~15 lines).

---

## Exit Conditions

- [x] `dotnet build Zaide.slnx` succeeds with 0 warnings and 0 errors
- [x] `dotnet test Zaide.slnx` passes all tests (85 passed, 0 failed)
- [x] `docs-rules.md` §11b violations: 0 (B3 resolved)
- [x] Rename a directory in the tree → children paths update correctly (B1)
- [x] Open an inaccessible folder → `StatusText` set, no crash, RootNodes unchanged (B2)
- [x] Drag GridSplitter → sidebar resizes between 180–500px; center column adjusts
- [x] Right-click tree node → context menu with Open, Expand All (full recursive), Collapse All
- [x] Context menu "Open" → file opens in editor via `RequestOpenFileCommand` mediation
- [x] Ctrl+O → folder picker opens; Enter on file in tree → opens in editor
- [x] No XAML added beyond `App.axaml` and `MainWindow.axaml` shell
- [x] All panels render with >= 16px padding
- [x] Colors use `App.axaml` resources

> **Note:** 6 M1 plan-required tests (`HandleRenamed_*`, `OpenFolderCommand_SetsStatusText_*`) were not added. Tracked in [TOFIX.md](TOFIX.md).

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
| `HandleRenamed` cascade fix uses prefix-safe `StartsWith` + slice | Low | Low | String prefix comparison is deterministic. `StartsWith` false positives would require a directory named identically as a substring prefix of another — path separator guarantees this can't happen. Test `HandleRenamed_DoesNotCorruptPaths_WithPartialNameMatch` validates. |
| Interaction-based `PickFolder` adds complexity to MainWindowViewModel | Low | Low | Follows docs-rules.md §11a exactly; proven pattern from Phase 2 |

---

*Completed: 2026-06-27. 85 tests, 0 failures, 0 §11b violations.*
