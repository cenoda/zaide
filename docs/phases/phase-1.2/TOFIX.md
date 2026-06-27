# Phase 1.2: TOFIX

Code quality issues found during Phase 1.2 audit. Check these before starting Phase 1.3.

---

## Verified

### Pre-implementation gate (2026-06-27)
- [x] `dotnet build Zaide.slnx` passes with 0 errors
- [x] `dotnet test Zaide.slnx` passes: 93 tests, 0 failures
- [x] Phase 1.1 TOFIX.md items all resolved or scoped out

### Post-implementation (2026-06-27)
- [x] `dotnet build Zaide.slnx`: 3 warnings (xUnit analyzer), 0 errors
- [x] `dotnet test Zaide.slnx`: 93 passed, 0 failed
- [x] A1 — Failed folder open drops live watcher (FIXED)
- [x] A2 — `RequestOpenFileCommand` payload ignored (FIXED)
- [x] P1 — Ambiguous exit condition in plan (FIXED)
- [x] B1 — `ShowNamePromptAsync` hang on close button (FIXED)
- [x] B2 — Name prompt not actually modal (FIXED)
- [x] B3 — Stale StatusText on successful create (FIXED)
- [x] C1 — Toggle flips flag before re-enumeration succeeds (FIXED)
- [x] C2 — Menu item not explicitly checkable (FIXED)

---

## Open

### [ ] xUnit analyzer warnings (3 warnings, non-blocking)

`FileTreeViewModelTests.cs` uses `Assert.True(..., StartsWith(...))` — prefers `Assert.StartsWith`.
Minor style. Carried over from Phase 1.1; not blocking Phase 1.3.

---

## Audit Findings (2026-06-27)

### [x] A1 — Failed folder open drops live watcher (HIGH) (FIXED 2026-06-27)

`OpenFolderCommand` disposed the watcher *before* `EnumerateDirectory(path)`.
If validation threw, the user was left with the old tree visible but no live
file-system updates.

**Fix:** Move watcher teardown into the `try` block, after successful validation.
On failure, the existing watcher stays active.

**Files:** `src/ViewModels/FileTreeViewModel.cs` (lines 66–111)

---

### [x] A2 — `RequestOpenFileCommand` payload ignored (MEDIUM) (FIXED 2026-06-27)

`FileTreeView` passed `FileTreeNode` via `Execute(selected)`, but `MainWindowViewModel`
subscribed to the command's `IObservable<Unit>` (the result type, not the parameter),
reading `SelectedFile` instead — a fragile hidden coupling.

**Fix:** Added `Subject<FileTreeNode>` (`OpenFileRequested`) in `FileTreeViewModel`.
The command handler publishes the node to the subject. `MainWindowViewModel` subscribes
to `OpenFileRequested` with the actual payload — no dependency on `SelectedFile`.

**Files:** `src/ViewModels/FileTreeViewModel.cs` (lines 26–32, 113–122),
`src/ViewModels/MainWindowViewModel.cs` (lines 86–108)

---

### [x] B1 — `ShowNamePromptAsync` hang on close button (MEDIUM) (FIXED 2026-06-27)

The `TaskCompletionSource` was only completed on OK, Cancel, Enter, or Escape.
Closing the window via the title-bar close button left `await tcs.Task` pending
indefinitely.

**Fix:** Added `window.Closed += (_, _) => tcs.TrySetResult(null)`.

**Files:** `src/Views/FileTreeView.cs` (line 296)

---

### [x] B2 — Name prompt not actually modal (MEDIUM) (FIXED 2026-06-27)

`TopLevel.GetTopLevel(textBox)` ran before `textBox` was attached to the visual
tree, so it returned null and fell back to `window.Show()` — allowing the user
to interact with the main window while the prompt was open.

**Fix:** Changed method from `static` to instance method; uses `TopLevel.GetTopLevel(this)`
(the `FileTreeView` which is already attached when the context menu is shown).

**Files:** `src/Views/FileTreeView.cs` (lines 226, 300–302)

---

### [x] B3 — Stale StatusText on successful create (LOW) (FIXED 2026-06-27)

A previous error message in `StatusText` persisted after a subsequent successful
file/folder creation.

**Fix:** Added `StatusText = null;` before the try block in `CreateNodeCommand`.

**Files:** `src/ViewModels/FileTreeViewModel.cs` (line 149)

---

### [x] C1 — Toggle flips flag before re-enumeration succeeds (MEDIUM) (FIXED 2026-06-27)

`ToggleHiddenFilesCommand` set `ShowHiddenFiles = !ShowHiddenFiles` *before*
re-enumerating and restarting the watcher. If `EnumerateDirectory` or `StartWatching`
failed, the UI state reported the new mode but the tree and watcher still reflected
(or had stopped for) the previous mode.

**Fix:** Compute the new value via local `newValue = !ShowHiddenFiles`, pass it to
`EnumerateDirectory` and `StartWatching`, and only assign `ShowHiddenFiles = newValue`
after both succeed. On failure, the old tree, watcher, and UI state remain in sync.

**Files:** `src/ViewModels/FileTreeViewModel.cs` (lines 174–207)

---

### [x] C2 — Menu item not explicitly checkable (LOW) (FIXED 2026-06-27)

The "Show Hidden Files" `MenuItem` only bound `IsChecked` without setting
`ToggleType`, leaving its checkable behavior dependent on Avalonia defaults.

**Fix:** Added `ToggleType = MenuItemToggleType.CheckBox` to the `MenuItem` initializer.

**Files:** `src/Views/FileTreeView.cs` (line 149)

---

## Known Limitations (by design, not for TOFIX)

### M1 New File/Folder uses modal prompt
No inline rename in tree. Deferred to a future polish phase.

### M2 Hidden files toggle re-enumerates
No incremental add/remove. Fast enough for Phase 1 project sizes.

### M3 Copy Relative Path disabled when no folder open
`RootPath` is null — disabled.

### FileSystemWatcher picks up new files
No manual tree insert. Linux inotify edge cases may miss events.

### Enter key collapses parent directory when opening a file in a subdirectory
Carried over from Phase 1.1. Full fix requires TreeViewItem template customization
— deferred to a future polish phase.

---

## Phase 1.2 Carry-Over (for Phase 3+)

### C2: `StatusText` properties have no UI binding
`FileTreeViewModel.StatusText` and `MainWindowViewModel.StatusText` receive
error messages but no visual widget renders them. Planned for Phase 3 status bar.

---

*Last updated: 2026-06-27*
