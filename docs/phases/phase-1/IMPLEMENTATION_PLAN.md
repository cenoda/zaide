# Phase 1: File Tree Sidebar — Implementation Plan

## Pre-Implementation Verification

- [ ] `dotnet build Zaide.slnx` passes with 0 warnings (Phase 0 baseline)
- [ ] `dotnet test Zaide.slnx` passes: 2 tests, 0 failures
- [ ] Phase 0 TOFIX.md items are all resolved
- [ ] Avalonia 12 `TreeDataTemplate` C# construction API verified (or `FuncTreeDataTemplate` fallback confirmed)

---

## Scope

**Goal:** Replace the left sidebar placeholder with a real, functional file tree.
Users open a folder, browse its structure, click files, and see live filesystem changes.

**Boundaries (NOT building):**
- No editor / tab opening (Phase 2)
- No file icons or fancy styling — clean, solid-color tree nodes
- No persisted folder path between sessions
- No drag-and-drop, context menus, or multi-select
- No async file enumeration (sync is fine for small projects)
- No git status overlay (Phase 7)
- No virtualisation for large directories (lazy loading via TreeView built-in is sufficient)

---

## Milestones (Incremental)

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate: Phase 0 clean build + tests pass | `dotnet build Zaide.slnx && dotnet test Zaide.slnx` |
| M1 | `FileTreeNode` model + `FileTreeService` (ignore list, directory enumeration) | Unit tests: `IsIgnored` rejects `node_modules`/`.git`/`bin`/`obj`; `EnumerateDirectory` returns correct nested tree for a temp dir |
| M2 | `FileTreeViewModel` with reactive `RootNodes`, `OpenFolderCommand`, `SelectedFile`. DI registration. Wire into `MainWindowViewModel`. | Unit test: `OpenFolderCommand` populates `RootNodes` from a temp dir; `SelectedFile` changes on node selection |
| M3 | `FileTreeView` (C# TreeView, `ReactiveUserControl<FileTreeViewModel>`) replaces sidebar placeholder | Visual: 260px sidebar shows nested tree; nodes expand/collapse; colors from `App.axaml` resources |
| M4 | "Open Folder" button in sidebar header → native OS dialog → tree populates. Click file → center shows "Opened: filename" | Click file in tree → center panel text updates |
| M5 | `FileSystemWatcher` monitors root folder; tree updates when files/dirs change externally | Create a file on disk outside the app → tree adds it within seconds |

---

### M1: `FileTreeNode` model + `FileTreeService`

**Files to create:**

- `src/Models/FileTreeNode.cs`
  - Inherits `ReactiveObject` (justified: `IsExpanded` is UI state the TreeView binds to directly)
  - `Name`, `FullPath`, `IsDirectory`, `IsExpanded`
  - `Children`: `ObservableCollection<FileTreeNode>`

- `src/Services/FileTreeService.cs`
  - `List<FileTreeNode> EnumerateDirectory(string path)` — recursive, directories have `Children`, files are leaves
  - `bool IsIgnored(string name)` — checks against default ignore list
  - Default ignores: `node_modules`, `bin`, `obj`, `.git`, `.vs`, `.idea`, `__pycache__`, `.DS_Store`, `Thumbs.db`
  - Skips hidden files/folders (names starting with `.` except `.` and `..`)

**Tests to add:**

- `tests/Zaide.Tests/Services/FileTreeServiceTests.cs`
  - `IsIgnored_ReturnsTrue_ForCommonPatterns`
  - `IsIgnored_ReturnsFalse_ForNormalFolders`
  - `EnumerateDirectory_ReturnsNestedTree_ForTempFolder`
  - `EnumerateDirectory_SkipsIgnoredDirectories`

**Design decision:** `FileTreeNode` extends `ReactiveObject` even though models are normally plain data (CONVENTIONS.md). Reason: `IsExpanded` is UI-coupled state that the TreeView binds to directly. Keeping it in the model avoids duplicating expand/collapse state in the ViewModel.

---

### M2: `FileTreeViewModel` + DI registration

**Files to create:**

- `src/ViewModels/FileTreeViewModel.cs`
  - Inherits `ReactiveObject`
  - Constructor takes `FileTreeService` (DI injection)
  - `ObservableCollection<FileTreeNode> RootNodes` — top-level tree nodes
  - `ReactiveCommand<string, Unit> OpenFolderCommand` — calls `_fileTreeService.EnumerateDirectory(path)`, clears `RootNodes`, adds new nodes
  - `FileTreeNode? SelectedFile` — reactive property, updated by TreeView selection (M3/M4)
  - `string? RootPath` — current folder path (for display)

**Files to modify:**

- `src/Program.cs` — register `FileTreeService` (Singleton) and `FileTreeViewModel` (Singleton) in DI
- `src/ViewModels/MainWindowViewModel.cs` — add `FileTreeViewModel FileTreeViewModel` property, inject via constructor

**Tests to add:**

- `tests/Zaide.Tests/ViewModels/FileTreeViewModelTests.cs`
  - `RootNodes_IsEmpty_BeforeFolderOpened`
  - `OpenFolderCommand_PopulatesRootNodes_ForTempDirectory`
  - `SelectedFile_DefaultsToNull`

---

### M3: `FileTreeView` replaces sidebar placeholder

**Files to create:**

- `src/Views/FileTreeView.cs`
  - Inherits `ReactiveUserControl<FileTreeViewModel>` (consistency with `MainWindow : ReactiveWindow<T>`)
  - Constructor builds a `TreeView` in C#:
    - `ItemsSource` → bound to `RootNodes` (via `WhenActivated` + `OneWayBind`, or direct `Binding`)
    - `ItemTemplate` → `TreeDataTemplate` with `ItemsSource` bound to `Children`
    - Template shows `TextBlock` bound to `Name`
  - Colors from `Application.Current.Resources`:
    - Background = `(IBrush)App.Current.Resources["DeepBase"]`
    - Foreground = `(IBrush)App.Current.Resources["TextActive"]`
  - Padding: 16px per DESIGN.md §5
  - No extra `ScrollViewer` — TreeView has built-in scrolling

**Files to modify:**

- `src/MainWindow.axaml.cs`
  - Refactor `BuildPanel` to pull colors from `App.Current.Resources` instead of `Color.Parse`:
    - `Background` → `(IBrush)App.Current.Resources["DeepBase"]`
    - `TextBlock.Foreground` → `(IBrush)App.Current.Resources["TextActive"]`
  - Replace `BuildPanel("Sidebar", ...)` with:
  ```csharp
  var sidebar = new FileTreeView
  {
      ViewModel = ViewModel!.FileTreeViewModel
  };
  ```
  Column stays 260px, `GridSplitter` still deferred.

**⚠️ Verify before implementing:** Avalonia 12 `TreeDataTemplate` C# construction API.
`FuncTreeDataTemplate` is the confirmed fallback if `TreeDataTemplate` is not constructable in C#.

**No unit tests (UI-only milestone).**

---

### M4: Open folder dialog + file click feedback

**Files to modify:**

- `src/Views/FileTreeView.cs`
  - Add header `DockPanel` above TreeView with "Open Folder" `Button` (margin-bottom: 8px)
  - Button click handler:
    ```csharp
    var topLevel = TopLevel.GetTopLevel(this);
    var folders = await topLevel!.StorageProvider.OpenFolderPickerAsync(
        new FolderPickerOpenOptions { AllowMultiple = false });
    if (folders.Count > 0)
        ViewModel!.OpenFolderCommand.Execute(folders[0].Path.LocalPath).Subscribe();
    ```
    ⚠️ `TopLevel.GetTopLevel(this)` is null until attached to visual tree. Button click guarantees attachment.

  - TreeView selection binding (event-based — TreeView may not expose `SelectedItem` as a bindable property):
    ```csharp
    treeView.SelectionChanged += (_, e) =>
    {
        ViewModel!.SelectedFile = e.SelectedItems.FirstOrDefault() as FileTreeNode;
    };
    ```

- `src/MainWindow.axaml.cs`
  - Refactor: keep a reference to the center `TextBlock` as a private field `_centerText`
  - Replace `BuildPanel("Center", ...)` with:
    ```csharp
    _centerText = new TextBlock
    {
        Text = "Open a folder to begin",
        Foreground = (IBrush)App.Current.Resources["TextActive"],
        FontSize = 14,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };
    var centerBorder = new Border
    {
        Background = (IBrush)App.Current.Resources["DeepBase"],
        Padding = new Thickness(16),
        Child = _centerText
    };
    ```
  - In `WhenActivated`, bind: `this.OneWayBind(ViewModel, vm => vm.StatusText, v => v._centerText.Text)`

- `src/ViewModels/MainWindowViewModel.cs`
  - Add `string? StatusText` reactive property, default `"Open a folder to begin"`
  - In constructor, subscribe to `FileTreeViewModel.SelectedFile`:
    ```csharp
    this.WhenAnyValue(x => x.FileTreeViewModel.SelectedFile)
        .Subscribe(file => StatusText = file is not null
            ? $"Opened: {file.Name}"
            : "No file selected");
    ```
    No `d.Add()` disposal — `MainWindowViewModel` is a singleton with app-lifetime scope,
    and has no `WhenActivated` (it's not a View). The subscription lives as long as the process.

**No new tests (UI + integration).**

---

### M5: FileSystemWatcher for live updates

**Files to modify:**

- `src/Models/FileChangeEvent.cs`
  - Record: `ChangeType` (Created/Deleted/Renamed — note: file *content* changes are intentionally
    not monitored; only filename/directory changes), `FullPath`, `OldPath?` (for rename)

- `src/Services/FileTreeService.cs`
  - Add `IObservable<FileChangeEvent> FileChanges` property
  - Add `StartWatching(string path)`:
    ```csharp
    var watcher = new FileSystemWatcher(path)
    {
        IncludeSubdirectories = true,
        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
    };
    FileChanges = Observable.FromEventPattern<FileSystemEventArgs>(watcher, "Created")
        .Merge(Observable.FromEventPattern<FileSystemEventArgs>(watcher, "Deleted"))
        .Merge(Observable.FromEventPattern<RenamedEventArgs>(watcher, "Renamed"))
        .Throttle(TimeSpan.FromMilliseconds(100))
        .Select(e => MapToFileChangeEvent(e));
    watcher.EnableRaisingEvents = true;
    ```
    `Throttle` merges rapid sequential events (common on Linux) into one.

  - Add `StopWatching()` → dispose watcher

- `src/ViewModels/FileTreeViewModel.cs`
  - Private field: `IDisposable? _watcherSubscription`
  - In `OpenFolderCommand`:
    1. `_watcherSubscription?.Dispose()`
    2. `_fileTreeService.StopWatching()`
    3. `_fileTreeService.StartWatching(path)`
    4. Subscribe to `_fileTreeService.FileChanges` → `HandleFileChange(change)`
  - `HandleFileChange`: switch on `ChangeType`
    - `Created` → find parent dir node via recursive search, add new `FileTreeNode` to its `Children`
    - `Deleted` → find and remove node from parent's `Children`
    - `Renamed` → update node `Name` and `FullPath`
  - Parent node search: recursive helper traversing `RootNodes` + `Children` (adequate for Phase 1 tree depth)

**Known limitation (Linux):** `FileSystemWatcher` uses `inotify` — may miss events on
networked/special filesystems (tmpfs, NFS, FUSE). Best-effort only; no retry or polling
fallback in Phase 1.

**No new tests (integration-heavy).**

---

## Limitations (by design)

- **No GridSplitter** — column widths are fixed until there's enough content to justify resizing.
- **No file icons** — plain text nodes. Icons come in a polish phase.
- **No keyboard shortcuts** beyond built-in TreeView keys (Enter to open deferred).
- **No lazy loading for large directories** — single-pass recursion on open. TreeView's built-in
  virtualisation handles rendering.
- **FileSystemWatcher is best-effort** — Linux inotify has known edge cases. No polling fallback.
- **No "Open Recent" or history** — always starts empty on launch.
- **No animation** on tree expand/collapse.

---

## Exit Conditions

- [ ] `dotnet build Zaide.slnx` succeeds with 0 warnings
- [ ] `dotnet test Zaide.slnx` passes all tests (2 existing + new ones)
- [ ] Open folder dialog works — tree populates with real files
- [ ] Ignored folders (node_modules, .git, bin, obj) are not shown
- [ ] Click a file in the tree → center panel shows "Opened: filename"
- [ ] Create/delete a file externally → tree updates within seconds
- [ ] No XAML beyond `App.axaml` and minimal `MainWindow.axaml` shell
- [ ] All panels still render with ≥ 16px padding
- [ ] Colors use `App.axaml` resources (not hardcoded hex strings in views)

---

## Rollback Plan

- Commit hash to revert to: current HEAD (post Phase 0)
- What to preserve: `Directory.Packages.props`, `Directory.Build.props`, `App.axaml`, `docs/`
- What to discard on failure: `FileTreeNode.cs`, `FileTreeService.cs`, `FileTreeViewModel.cs`,
  `FileTreeView.cs`, modifications to `MainWindow.axaml.cs`, `MainWindowViewModel.cs`, `Program.cs`

---

*Based on IMPLEMENTATION_TEMPLATE. Last updated: 2025-06-25*

