# Phase 1.2: File Tree Essentials — Implementation Plan

**Status:** IMPLEMENTATION — ready for execution.

## Pre-Implementation Verification

- [x] `dotnet build Zaide.slnx` passes with 0 errors
- [x] `dotnet test Zaide.slnx` passes: 93 tests, 0 failures
- [x] Phase 1.1 TOFIX.md reviewed (3 xUnit warnings, 1 open item)
- [x] Phase 2 TOFIX.md check

---

## Scope

**Goal:** Three essential file-tree features: create files/directories, toggle hidden files, copy paths.

**Boundaries (NOT building):**
- No inline rename (F2) — separate polish item
- No delete/trash from tree
- No file templates or content scaffolding
- No "Open in external app" / "Open Containing Folder"
- No drag-and-drop file moving
- No multi-select file operations
- No "Paste" (copy/paste files between dirs)

---

## Milestones (Incremental)

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate | ✅ `dotnet build Zaide.slnx && dotnet test Zaide.slnx` — 0 errors, 93 passed |
| M1 | New File / New Folder via context menu | Right-click → "New File" → prompt → file created. "New Folder" → dir created. Tree updates via watcher. |
| M2 | Show hidden files toggle (Ctrl+Shift+H + context menu) | Ctrl+Shift+H toggles. Right-click toggle. `.agents`/`.gitignore` appear/disappear. |
| M3 | Copy Path / Copy Relative Path | Right-click file → "Copy Path" / "Copy Relative Path" → clipboard. |

---


### M1: New File / New Folder

**Files to modify:**

- `src/Services/FileTreeService.cs`
  - Add `void CreateFile(string path)` and `void CreateDirectory(string path)`. Thin wrappers.

- `src/ViewModels/FileTreeViewModel.cs`
  - Add `ReactiveCommand<(string ParentDir, bool IsDirectory), Unit> CreateNodeCommand`.
  - Calls service. try/catch → `StatusText` on failure. No manual tree insert — watcher picks it up.

- `src/Views/FileTreeView.cs`
  - Context menu: separator, "New File", "New Folder".
  - Parent dir: right-click on dir node → that `FullPath`. Empty tree → `RootPath`. No folder open → disabled.
  - Name prompt: simple modal `Window` with `TextBox` + OK/Cancel.

**No new tests (UI + I/O).**

---

### M2: Show Hidden Files Toggle

**Files to modify:**

- `src/ViewModels/FileTreeViewModel.cs`
  - `bool ShowHiddenFiles` (default `false`). `ReactiveCommand<Unit, Unit> ToggleHiddenFilesCommand`.
  - Toggle re-enumerates `RootPath` with flag.

- `src/Services/FileTreeService.cs`
  - `EnumerateDirectory(string path, bool includeHidden = false)`. When true: skip `IsHidden`, only `DefaultIgnores`.
  - Watcher recreation: `StartWatching` builds the filter pipeline once at call time. Toggling hidden files requires calling `StartWatching(path)` again with a new `includeHidden` flag passed through. Add a `bool _includeHidden` field stored at watch-start time so `FileChanges` observable's `.Where()` filter uses the current value.

- `src/Views/FileTreeView.cs`
  - Context menu: checkable "Show Hidden Files". `Ctrl+Shift+H` on TreeView.

**No new tests (UI + watcher).**

---

### M3: Copy Path / Copy Relative Path

**Files to modify:**

- `src/ViewModels/FileTreeViewModel.cs`
  - `Interaction<string, Unit> CopyToClipboard`. `CopyPathCommand`, `CopyRelativePathCommand`.
  - Relative: `Path.GetRelativePath(RootPath!, node.FullPath)`.

- `src/Views/FileTreeView.cs`
  - Register `CopyToClipboard` handler in `WhenActivated`: `topLevel.Clipboard.SetTextAsync(text)`.
  - Context menu: "Copy Path", "Copy Relative Path".

**No new tests (UI + clipboard).**

---

## Limitations (by design)

- **New File/Folder uses a modal prompt** — no inline rename in tree. Deferred.
- **Hidden files toggle re-enumerates** — no incremental add/remove. Fast enough for Phase 1 project sizes.
- **Copy Relative Path disabled when no folder open** — `RootPath` is null.
- **FileSystemWatcher picks up new files** — no manual insert. Linux inotify edge cases may miss events.

---

## Exit Conditions

- [x] `dotnet build Zaide.slnx`: 0 errors (3 pre-existing xUnit analyzer warnings are out of scope — see Phase 1.1 TOFIX.md)
- [x] `dotnet test Zaide.slnx` passes
- [ ] "New File" → file created on disk, appears in tree
- [ ] "New Folder" → dir created on disk, appears in tree
- [ ] Ctrl+Shift+H → hidden files toggle
- [ ] "Show Hidden Files" checkable menu works
- [ ] "Copy Path" → absolute path in clipboard
- [ ] "Copy Relative Path" → relative path in clipboard
- [ ] No XAML added beyond `App.axaml` + `MainWindow.axaml` shell

---

## Rollback Plan

- Revert to: current HEAD (post Phase 1.1)
- Preserve: `docs/`, `App.axaml`, Phase 1.1/2/2.1 source
- Discard: new methods in `FileTreeViewModel.cs`, `FileTreeService.cs`, `FileTreeView.cs`

---

*Draft created: 2026-06-27. Pending user review.*


