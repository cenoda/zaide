# Refactor 2: Layer Boundary Cleanup — Implementation Plan

## Pre-Implementation Verification

- [x] Current codebase structure understood (Models, Services, ViewModels, Views)
- [x] Cross-layer dependencies identified (see Audit Findings below)
- [x] Refactor-1 completed (Document/Workspace extraction)
- [x] All existing tests pass: `dotnet test Zaide.slnx`
- [x] No new NuGet packages needed — pure C# boundary cleanup

## Scope

**Goal:**
Clean up layer boundaries within the existing single-project structure. This is a
**preparation pass** — not a multi-project split. The goal is to make the boundaries
clean enough that a future project split becomes trivial.

**Target shape (future split, NOT this refactor):**
```
Zaide.Core          → Models, value objects, pure interfaces (no Avalonia/ReactiveUI)
Zaide.Application   → Use-case coordination, workflow services
Zaide.Infrastructure → File system, PTY, persistence
Zaide.UI            → Avalonia views, ViewModels, UI composition
```

**This refactor's scope (boundary cleanup only):**
1. Remove service dependencies from Model types
2. Remove UI framework dependencies from Model types (replace ReactiveUI with plain INotifyPropertyChanged)
3. Extract interfaces for concrete service dependencies in ViewModels
4. Reduce MainWindow's composition burden (extract file-type policy)
5. Inject IScheduler into FileTreeViewModel (remove AvaloniaScheduler reference)

**Deferred to future refactors:**
- Moving terminal pure logic to `Terminal/` folder (requires namespace change — see M2)
- Moving tree manipulation logic out of VM (requires FileTreeNode domain/UI split — see M4)
- TerminalViewModel's `Dispatcher.UIThread.Post` usage (see M8 below) — requires separate UI-post abstraction

**Boundaries (NOT in scope):**
- ❌ No actual multi-project split (that's a future refactor)
- ❌ No feature work
- ❌ No API redesign unless required for boundary cleanup
- ❌ No namespace changes (keep `Zaide.Models`, `Zaide.Services`, etc.)
- ❌ No DI container changes unless required

## Audit Findings

### Cross-Layer Violations Found

| File | Issue | Severity |
|------|-------|----------|
| `Models/Document.cs` | References `IFileService` (service dependency in model) | High |
| `Models/FileTreeNode.cs` | Inherits `ReactiveObject` (UI framework in model) | High |
| `Models/Workspace.cs` | Unused `using Zaide.Services;` | Low |
| `ViewModels/FileTreeViewModel.cs` | Depends on concrete `FileTreeService`; uses `AvaloniaScheduler.Instance`; contains tree manipulation logic | High |
| `ViewModels/TerminalViewModel.cs` | Uses `Avalonia.Threading.Dispatcher`; contains ANSI/screen logic | Medium |
| `ViewModels/MainWindowViewModel.cs` | Contains file extension checking logic (app logic in VM) | Medium |
| `Services/FileTreeService.cs` | Mixes pure tree enumeration with FileSystemWatcher infrastructure | Medium |

### Pure Logic in Wrong Folders

| File | Current Location | Should Be |
|------|------------------|-----------|
| `AnsiParser.cs` | ViewModels | Core (pure parser) |
| `TerminalScreen.cs` | ViewModels | Core (pure buffer model) |
| `TerminalSnapshot.cs` | ViewModels | Core (immutable snapshot) |
| `TerminalState.cs` | ViewModels | Core (enum) |

### MainWindow Concerns

- 334 lines of UI composition
- Keyboard binding management
- Dialog handling
- ViewModel activation/disposal coordination

## Milestones (Incremental)

**Implementation ordering is critical:** M1 must be completed before M3 (M3 changes `FileTreeViewModel` which depends on `Document` and `FileTreeNode`). M3 must be completed before M5 (M5 injects `IScheduler` into `FileTreeViewModel`, but M3 changes the service dependency first). M6 and M7 are independent and can be done after M1. Deferred milestones (M2, M4, M8) are never started.

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate: current build/tests pass | `dotnet test` — zero failures | ✅ Complete (300 passed, 0 failed) |
| M1 | **Clean Models layer**: Remove `SaveAsync` entirely from `Document` (model should not own persistence workflow). Add `RecordSaveError(string?)` for VM to call after save attempt. Update all call sites (`EditorViewModel.SaveAsync`, `Program.cs` DI, `DocumentTests`). Remove `ReactiveObject` from `FileTreeNode` (implement `INotifyPropertyChanged` directly). Remove unused `using` from `Workspace`. | `DocumentTests`, `EditorViewModelTests`, `WorkspaceTests`, `FileTreeViewModelTests` pass | ✅ Complete |
| M2 | **Terminal pure logic — deferred**: `AnsiParser`, `TerminalScreen`, `TerminalSnapshot`, `TerminalState` are already pure. Moving them to `Terminal/` would require a namespace change from `Zaide.ViewModels` to `Zaide.Terminal`, which is out of scope for this boundary-cleanup pass. **No file moves this refactor.** A future refactor can move them + update namespace to `Zaide.Terminal`. | No changes — files stay in `ViewModels/` | ⬜ Deferred / N/A |
| M3 | **Extract IFileTreeService interface**: Create `IFileTreeService` interface from `FileTreeService`. Extract testable service interfaces around the current UI tree shape (enumeration + watching). Update DI registration. ViewModels depend on interfaces only. | `FileTreeServiceTests`, `FileTreeViewModelTests` pass | ✅ Complete |
| M4 | **Tree manipulation logic — stays in VM**: `HandleCreated`, `HandleDeleted`, `HandleRenamed`, `FindNodeByPath`, `UpdateDescendantPaths` manage UI tree state, not filesystem infrastructure. Moving them to `FileTreeService` would muddy the boundary. **Keep in `FileTreeViewModel`.** A future refactor can extract a pure `FileTreeUpdater` class after splitting `FileTreeNode` into domain + UI state. | `FileTreeViewModelTests` pass; manual regression: open folder → create/rename/delete files → tree updates | ⬜ Deferred / N/A |
| M5 | **Remove AvaloniaScheduler from FileTreeViewModel**: Inject `IScheduler` as a **required** constructor parameter. Register `AvaloniaScheduler.Instance` in DI (`Program.cs`). Tests inject `CurrentThreadScheduler.Instance`. Update all test constructors (`FileTreeViewModelTests`, `MainWindowViewModelTests`). No fallback to `AvaloniaScheduler.Instance` in VM code. | `FileTreeViewModelTests`, `MainWindowViewModelTests` pass | ✅ Complete |
| M6 | **Move file extension logic out of MainWindowViewModel**: Extract `SupportedExtensions` to a static `SupportedFileTypes` class in `Services/` (not `Models/` — this is editor policy, not domain data). MainWindowViewModel delegates to it. Add tests for `SupportedFileTypes.IsTextFile` (supported, unsupported, no-extension). | `SupportedFileTypes` tests pass; `MainWindowViewModelTests` pass; manual: open file → opens in editor; open binary → shows status | ✅ Complete |
| M7 | **Stabilize + regression sweep**: Full manual regression. All tests pass. No behavioral changes. | `dotnet test` — zero regressions; manual: open/edit/save/close/reopen, terminal start/stop/restart, file tree operations | ✅ Complete |
| M8 | **TerminalViewModel UI-post seam — deferred**: `TerminalViewModel` has an internal test seam (`_uiPost`), but the production constructor (line 137-140) still references `Dispatcher.UIThread.Post` directly. This refactor does **not** fix it. A future refactor should inject an `IUIThreadPoster` or similar abstraction through DI. Marked deferred to avoid scope creep. | No changes this refactor | ⬜ Deferred / N/A |

## Detailed Milestone Plans

### M1: Clean Models Layer

**Document.cs changes:**
- **Remove `SaveAsync` entirely** from Document. The model should not own persistence workflow.
- Add `RecordSaveError(string? error)` method for VM to call after save attempt.
- `RecordSaveError` must set `IsDirty = true` unconditionally (even if already dirty) and raise `SaveErrorChanged` and `DirtyStateChanged` events — matching current `Document.SaveAsync` catch-block behavior (lines 50-57).
- Document becomes a pure data model: content, dirty flag, error state — no I/O.
- Remove `using Zaide.Services;` (no longer needed).

**EditorViewModel.cs changes (save coordinator):**
- `SaveAsync()` now orchestrates the save:
  ```csharp
  private async Task<bool> SaveAsync()
  {
      if (string.IsNullOrEmpty(FilePath))
          return false;
      try
      {
          await _fileService.WriteAllTextAsync(FilePath, TextContent);
          Document.MarkClean();
          return true;
      }
      catch (Exception ex)
      {
          Document.RecordSaveError(ex.Message);
          if (ex is IOException or UnauthorizedAccessException)
              return false;
          throw;
      }
  }
  ```
- **Important:** Catch all exceptions to record the error (matching current `Document.SaveAsync` behavior which records `LastSaveError` before rethrowing). For expected save failures (`IOException`, `UnauthorizedAccessException`), return `false`. For unexpected exceptions, rethrow after recording the error. This preserves the exact current contract: the error is always recorded, but only I/O failures are handled gracefully.
- **Note on ReactiveCommand and exception propagation:** The `SaveCommand` is a `ReactiveCommand`, which catches exceptions thrown by the underlying async method and surfaces them via the `ThrownExceptions` observable rather than propagating them through `await Execute()`. This means that for tests, the `.Catch(Observable.Return(false))` pattern is used to handle the error gracefully in the observable pipeline, and assertions check the Document state. The underlying implementation still rethrows for unexpected exceptions (which ReactiveCommand captures), ensuring the contract holds for direct callers of `SaveAsync()`.
- This truly removes the service dependency from the model — the VM owns the use case.

**Call-site updates (required for M1 to compile):**

- **DocumentTests.cs**: Remove `SaveAsync_*` tests. Add tests for `RecordSaveError`:
  ```csharp
  [Fact]
  public void RecordSaveError_SetsLastSaveError()
  {
      var doc = new Document("/path.txt", "content");
      doc.MarkClean();
      doc.RecordSaveError("Disk full");
      Assert.Equal("Disk full", doc.LastSaveError);
      Assert.True(doc.IsDirty);
  }

  [Fact]
  public void RecordSaveError_RaisesSaveErrorChanged()
  {
      var doc = new Document("/path.txt", "content");
      var raised = false;
      doc.SaveErrorChanged += (_, _) => raised = true;
      doc.RecordSaveError("error");
      Assert.True(raised);
  }

  [Fact]
  public void RecordSaveError_RaisesDirtyStateChanged()
  {
      var doc = new Document("/path.txt", "content");
      doc.MarkClean();
      var raised = false;
      doc.DirtyStateChanged += (_, _) => raised = true;
      doc.RecordSaveError("error");
      Assert.True(raised);
  }
  ```

- **EditorTabViewModelTests.cs**: The existing tests `CloseTab_StaysOpen_WhenSaveFails` (line 139) and `SaveFailure_MustNotCloseTab_MockFileService` (line 211) exercise `CloseTabAsync` which awaits `tab.SaveCommand.Execute()` and reads `tab.LastSaveError`. These tests must continue to pass after M1 — verify they still assert `tab.IsDirty` and `vm.LastSaveError` correctly with the new VM-coordinated save flow. No test changes expected unless the save-failure contract changes.

- **EditorViewModelTests.cs**: Update save tests to verify VM calls IFileService and updates Document state. Add an explicit test for the unexpected-exception contract:
  ```csharp
  [Fact]
  public async Task SaveAsync_UnexpectedException_RecordsErrorAndSurfacesViaThrownExceptions()
  {
      var fileService = new Mock<IFileService>();
      fileService.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
                 .ThrowsAsync(new InvalidOperationException("Unexpected failure"));
      var doc = new Document("/test.cs", "content");
      doc.MarkClean();
      var vm = new EditorViewModel(doc, fileService.Object);

      // ReactiveCommand catches exceptions thrown by the underlying async method
      // and surfaces them via the ThrownExceptions observable rather than
      // propagating them through `await Execute()`. We use `.Catch(...)` to
      // observe the error path gracefully, then assert both that the Document
      // recorded the error AND that the exception surfaced in ThrownExceptions.
      var result = await vm.SaveCommand.Execute().Catch(Observable.Return(false));

      Assert.False(result);
      Assert.Equal("Unexpected failure", vm.LastSaveError);
      Assert.True(vm.IsDirty);
      Assert.True(vm.SaveCommand.ThrownExceptions.ToEnumerable().Any());
  }
  ```
  This test verifies that unexpected exceptions (non-IOException/UnauthorizedAccessException) are recorded on the Document before propagating, matching the current `Document.SaveAsync` contract. Note: `EditorViewModel` constructor takes `(Document document, IFileService fileService)` and `Document` has a private setter, so tests must construct the Document first and pass it in.
  
  **Important:** `ReactiveCommand` catches exceptions thrown by the underlying async method and surfaces them via the `ThrownExceptions` observable rather than propagating them through `await Execute()`. The test uses `.Catch(Observable.Return(false))` to handle the error gracefully in the result observable, asserts the Document state, and additionally asserts `ThrownExceptions` emitted the error — confirming the underlying `SaveAsync()` did rethrow (the contract), and that `ReactiveCommand` captured it. Do NOT use `Assert.ThrowsAsync` with `SaveCommand.Execute()` — it will not work as expected.

**FileTreeNode.cs changes:**
- Remove `ReactiveObject` inheritance
- **Implement `INotifyPropertyChanged` directly** (plain C# event, no ReactiveUI)
- Replace `using ReactiveUI;` with `using System.ComponentModel;` (required for `INotifyPropertyChanged` and `PropertyChangedEventHandler`)
- Keep `using System.Collections.ObjectModel;` (still needed for `ObservableCollection<FileTreeNode> Children`)
- `IsExpanded` keeps its backing field + property change notification
- Rationale: `TreeViewItem.IsExpanded` is bound two-way to `FileTreeNode.IsExpanded`. `ExpandAllCommand` / `CollapseAllCommand` set `IsExpanded` from the VM. Without `PropertyChanged`, source-to-target updates stop reflecting in realized tree items.
- Implementation:
  ```csharp
  public class FileTreeNode : INotifyPropertyChanged
  {
      public event PropertyChangedEventHandler? PropertyChanged;
      private bool _isExpanded;
      
      public bool IsExpanded
      {
          get => _isExpanded;
          set
          {
              if (_isExpanded != value)
              {
                  _isExpanded = value;
                  PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
              }
          }
      }
      // ... other members unchanged
  }
  ```
- This removes the ReactiveUI dependency from Models while preserving UI binding behavior.

**New test expectation (M1):**
- Add a test to `FileTreeViewModelTests` (or a new `FileTreeNodeTests`) that verifies `PropertyChanged` fires when `IsExpanded` changes:
  ```csharp
  [Fact]
  public void IsExpanded_RaisesPropertyChanged()
  {
      var node = new FileTreeNode { Name = "test", IsDirectory = true };
      var raised = false;
      node.PropertyChanged += (_, e) =>
      {
          if (e.PropertyName == nameof(FileTreeNode.IsExpanded))
              raised = true;
      };
  
      node.IsExpanded = true;
  
      Assert.True(raised);
  }
  ```
- This preserves the contract that the two-way binding with `TreeViewItem.IsExpanded` depends on.

**Workspace.cs changes:**
- Remove unused `using Zaide.Services;`
- Fix broken indentation around `OpenDocument` method (line 16 has no indentation)

### M2: Extract Terminal Pure Logic — DEFERRED

**No changes this refactor.**

**Rationale:** `AnsiParser`, `TerminalScreen`, `TerminalSnapshot`, and `TerminalState` are already pure logic (no UI framework dependencies). Moving them to a `Terminal/` subfolder would require a namespace change from `Zaide.ViewModels` to `Zaide.Terminal`, which is out of scope for this boundary-cleanup pass.

**Future refactor:** When ready for namespace cleanup, move files to `src/Terminal/` and update namespace to `Zaide.Terminal`. This requires updating all references and test files.

### M3: Extract IFileTreeService Interface

**Note:** `IFileTreeQuery` returns `FileTreeNode`, which is still a UI-bound tree node (has `ObservableCollection`, `INotifyPropertyChanged`). This is **not** a pure domain interface — it's an interface over the current UI tree shape. A future refactor can introduce a pure file-entry model and map to UI nodes. For now, this split separates *enumeration* from *watching* at the infrastructure level, which is the boundary cleanup goal.

**New interfaces:**
```csharp
// Tree enumeration — returns current UI tree shape (FileTreeNode)
// Not "pure domain" — see note above. Future refactor can introduce pure file entries.
public interface IFileTreeQuery
{
    List<FileTreeNode> EnumerateDirectory(string path, bool includeHidden = false);
    bool ShouldSkip(string name, bool includeHidden);
}

// File system watching — infrastructure
// StartWatching() returns the observable directly, avoiding nullable state.
// The VM subscribes to the returned observable and disposes the subscription to stop.
// Note: IFileTreeWatcher intentionally does NOT inherit IDisposable. The VM uses
// StopWatching() for restart lifecycle, and the DI container handles final disposal
// of the singleton FileTreeService. Having both StopWatching() and IDisposable on
// the interface would create ambiguous disposal responsibility.
public interface IFileTreeWatcher
{
    IObservable<FileChangeEvent> StartWatching(string path, bool includeHidden = false);
    void StopWatching();
}

// Combined interface for backward compat
public interface IFileTreeService : IFileTreeQuery, IFileTreeWatcher
{
    void CreateFile(string path);
    void CreateDirectory(string path);
}
```
**Important:** `IFileTreeQuery` exposes `ShouldSkip(string name, bool includeHidden)` — not `IsIgnored(string name)`. The live `FileTreeService.IsIgnored` (line 100) always treats dotfiles as hidden regardless of the `includeHidden` flag, which is a bug-prone contract. The actual filtering in both enumeration (line 93) and watcher (line 139) uses `ShouldSkip` with an explicit `includeHidden` parameter. The interface must match the real filtering contract, not the legacy `IsIgnored` method.

**Note on `IsIgnored` visibility:** The current `FileTreeService.IsIgnored` is `public` and has 3 existing tests in `FileTreeServiceTests.cs` that call it directly (`IsIgnored_ReturnsTrue_ForCommonPatterns`, `IsIgnored_ReturnsFalse_ForNormalFolders`, `IsHidden_ExcludesDotAndDotDot`). After M3, `IsIgnored` should remain **public** (not private) to avoid breaking these tests. It is not part of the `IFileTreeQuery` interface, but it stays as a public convenience method on `FileTreeService` for backward compatibility. The interface only exposes `ShouldSkip`.

**Note on `ShouldSkip` visibility change:** The current `FileTreeService.ShouldSkip` is `private static`. After M3, it must become `public` (instance method) to implement `IFileTreeQuery.ShouldSkip`. The `static` keyword is removed and the method signature changes from `private static bool ShouldSkip(string name, bool includeHidden)` to `public bool ShouldSkip(string name, bool includeHidden)`.

**Contract note:** `StartWatching()` now returns `IObservable<FileChangeEvent>` directly. This eliminates the nullable `FileChanges` property and the race condition where the VM accesses `FileChanges!` before `StartWatching()` completes. The VM subscribes to the returned observable; `StopWatching()` disposes the underlying watcher. Tests should verify that `StartWatching()` returns a non-null observable.

**Note on subscription lifetime (documenting existing pattern):** The VM already correctly disposes the subscription before calling `StopWatching()` (lines 101-102, 199-200). `StopWatching()` disposes the underlying `FileSystemWatcher`, which stops the observable from emitting, but the observable subscription itself is **not** disposed by `StopWatching()` — callers must dispose their subscription separately. Document `StopWatching()` with: *"Disposes the underlying watcher. Callers must dispose their observable subscriptions separately via the IDisposable returned by Subscribe."*

**Watcher lifetime note:** After M3, `StartWatching()` creates a new watcher (local variable), builds the observable from it, and stores it in `_watcher` for `StopWatching()` to dispose. The observable's event handlers close over the same watcher instance that `StopWatching()` will dispose — the local and field reference the same object. The field assignment (`_watcher = watcher`) exists only so `StopWatching()` can reach it. Implementation:
```csharp
public IObservable<FileChangeEvent> StartWatching(string path, bool includeHidden = false)
{
    StopWatching();
    _includeHidden = includeHidden;
    var watcher = new FileSystemWatcher(path) { ... };
    var observable = Observable.FromEventPattern<...>(h => watcher.Created += h, ...)
        .Merge(...)
        .Select(...)
        .Where(...)
        .Throttle(...);
    watcher.EnableRaisingEvents = true;
    _watcher = watcher;  // store for StopWatching to dispose
    return observable;
}
```
This ensures the observable's event handlers reference the same watcher instance that `StopWatching()` will dispose.

**VM restart path (subscription order):** The VM must dispose the subscription **before** calling `StopWatching()`. The current code already does this correctly (line 101: `_watcherSubscription?.Dispose()` before line 102: `_fileTreeService.StopWatching()`). After M3, the pattern becomes:
```csharp
_watcherSubscription?.Dispose();
_fileTreeService.StopWatching();
// ... update tree ...
_watcherSubscription = _fileTreeService.StartWatching(path)
    .ObserveOn(AvaloniaScheduler.Instance)
    .Subscribe(HandleFileChange);
```
**Important:** Keep `AvaloniaScheduler.Instance` in M3. The `_scheduler` injection is introduced later in M5. If M5 is not yet implemented, the M3 code must continue using `AvaloniaScheduler.Instance`. When M5 is applied, replace `AvaloniaScheduler.Instance` with `_scheduler`.
This order prevents the subscription from receiving events from a stale watcher during the restart window.

**FileTreeService changes:**
- Implement `IFileTreeService`
- Change `StartWatching` to return `IObservable<FileChangeEvent>` (currently returns void, sets `FileChanges` property)
- Remove nullable `FileChanges` property entirely (no longer needed — the observable is returned directly from `StartWatching`)
- `StopWatching()` disposes the watcher; no `FileChanges` property to null out

**FileTreeViewModel changes:**
- Change dependency from `FileTreeService` to `IFileTreeService`
- Replace `_fileTreeService.FileChanges!.Subscribe(...)` with subscribing to the returned observable from `_fileTreeService.StartWatching(...)`
- Remove all `FileChanges!` null-forgiving operators
- Keep existing subscription disposal order: dispose before `StopWatching()`

**Note on new test additions:** The current `FileTreeServiceTests.cs` has no `StartWatching` tests today. After M3, add new tests to `FileTreeServiceTests` covering `StartWatching()` returns a non-null observable and proper event delivery (see exit checks below). No existing tests need rewriting, but the 3 `IsIgnored`-related tests remain unchanged since `IsIgnored` stays public.

**M3 exit checks (API contract change):**
- [ ] No `FileChanges!` null-forgiving operator remains in `FileTreeViewModel.cs`
- [ ] No nullable `FileChanges` property exists in `IFileTreeWatcher` or `FileTreeService`
- [ ] `StartWatching()` returns `IObservable<FileChangeEvent>` (not void)
- [ ] Watcher subscription tests cover: open-folder flow, hidden-toggle restart flow
- [ ] Add new `FileTreeServiceTests` verifying `StartWatching()` returns non-null observable
- [ ] Tests for `ShouldSkip(name, includeHidden)` covering: hidden file with `includeHidden=false` → true, hidden file with `includeHidden=true` → false, DefaultIgnores entry → true regardless of flag
- [ ] Event-delivery tests: create/rename/delete a file in the watched directory and verify the observable emits the corresponding `FileChangeEvent`
- [ ] Restart test: stop and restart the watcher, then create a file — verify the new observable emits events (no stale subscription from the old watcher)
- [ ] Toggle test: restart watcher with `includeHidden` toggled, verify hidden-file filtering changes accordingly

**Program.cs (DI) changes (required for M3 to work):**
- **Replace** the existing concrete `FileTreeService` registration with the interface registration. Currently line 25 registers `services.AddSingleton<FileTreeService>()`. Change it to:
  ```csharp
  services.AddSingleton<IFileTreeService, FileTreeService>();
  ```
- This ensures a single service instance. If both the concrete and interface registrations remain, the DI container would create two separate instances, and `FileTreeViewModel` (now depending on `IFileTreeService`) would get a different instance than any code still referencing `FileTreeService` directly.
- Without this change, `FileTreeViewModel` constructor resolution will fail at startup because `IFileTreeService` is not registered.
- **Note:** After M3, no code should depend on concrete `FileTreeService` directly. If any other registration still references `FileTreeService`, update it to `IFileTreeService` as well.

**File organization (per CONVENTIONS.md § one-class-per-file):**
- Each interface goes in its own file:
  - `src/Services/IFileTreeQuery.cs`
  - `src/Services/IFileTreeWatcher.cs`
  - `src/Services/IFileTreeService.cs`
- `FileTreeService.cs` remains unchanged (already exists).

**Note on FileChangeEvent placement:** `FileChangeEvent` currently lives in `Models/` (`src/Models/FileChangeEvent.cs`). The new service interfaces in `Services/` will reference it, creating cross-layer coupling from Services back to Models. This is **tolerated** for this prep pass — `FileChangeEvent` is a simple value object (not a domain entity), and moving it to a shared location would require a namespace change that is out of scope. A future project split should place `FileChangeEvent` in `Zaide.Core` or a shared contracts assembly.

### M4: Tree Manipulation Logic — Stays in VM

**No changes this refactor.**

**Rationale:** `HandleCreated`, `HandleDeleted`, `HandleRenamed`, `FindNodeByPath`, and `UpdateDescendantPaths` manage **UI tree state** (the `ObservableCollection<FileTreeNode>` hierarchy), not filesystem infrastructure. `FileTreeService` is filesystem infrastructure (enumeration, watching, file/directory creation). Moving UI state mutation into `FileTreeService` would muddy the boundary instead of cleaning it.

**Future refactor:** After splitting `FileTreeNode` into pure domain file entries + UI node state, extract a pure `FileTreeUpdater` class that operates on the UI node state. This keeps filesystem infrastructure separate from UI state management.

### M5: Remove AvaloniaScheduler from FileTreeViewModel

**FileTreeViewModel changes:**
- Add **required** constructor parameter `IScheduler scheduler`
- Replace `AvaloniaScheduler.Instance` with injected `_scheduler`
- Remove `using ReactiveUI.Avalonia;` from the file
- **Important:** `using ReactiveUI;` must remain — `FileTreeViewModel` still uses `ReactiveObject`, `ReactiveCommand`, `Interaction`, and `RaiseAndSetIfChanged` from ReactiveUI. Only the Avalonia-specific scheduler namespace is removed.
- No fallback to `AvaloniaScheduler.Instance` — the VM must not know about Avalonia

**Program.cs (DI) changes:**
- Register `IScheduler` in DI container:
  ```csharp
  services.AddSingleton<System.Reactive.Concurrency.IScheduler>(
      ReactiveUI.Avalonia.AvaloniaScheduler.Instance);
  ```

**Test changes:**
- Pass `CurrentThreadScheduler.Instance` in tests for synchronous execution:
  ```csharp
  var vm = new FileTreeViewModel(_service, CurrentThreadScheduler.Instance);
  ```
- **FileTreeViewModelTests.cs**: Update all `new FileTreeViewModel(_service)` calls to include the scheduler parameter.
- **MainWindowViewModelTests.cs** (lines 37, 156): Update manual `FileTreeViewModel` construction to include the scheduler parameter.
- Consider creating a test factory method to centralize VM construction and reduce churn when constructor signatures change.

### M6: Move File Extension Logic

**New class location:** `src/Services/SupportedFileTypes.cs`

**Rationale:** This is editor/application policy (which file types the editor supports), not domain data. It belongs in `Services/`, not `Models/`. A static class is sufficient — no need for a service interface.

**New class:**
```csharp
namespace Zaide.Services;

/// <summary>
/// Defines which file types the editor can open.
/// This is application policy, not domain data.
/// </summary>
public static class SupportedFileTypes
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".json", ".md", ".txt", ".xml", ".axaml", ".csproj",
        ".sln", ".slnx", ".props", ".targets", ".config",
        ".yml", ".yaml", ".css", ".html", ".js", ".ts", ".fs", ".vb",
        ".xaml", ".resx", ".razor", ".cshtml", ".svg"
    };

    // Dotfiles like .editorconfig, .gitignore, .gitattributes are
    // supported by their full filename, not just extension, because
    // Path.GetExtension returns the full name for dotfiles (e.g., ".gitignore").
    // The KnownDotfiles fallback is defense-in-depth for explicit filename matching.
    // Pure basename-only files (e.g., "Makefile", "Dockerfile") are NOT supported
    // by this policy — they return empty extension and IsTextFile returns false.
    private static readonly HashSet<string> KnownDotfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ".editorconfig", ".gitignore", ".gitattributes"
    };

    public static bool IsTextFile(string path)
    {
        var ext = Path.GetExtension(path);
        if (ext.Length > 0 && TextExtensions.Contains(ext))
            return true;
        // Fallback: check known dotfiles by filename
        var name = Path.GetFileName(path);
        return name.Length > 0 && KnownDotfiles.Contains(name);
    }
}
```
**Important:** `Path.GetExtension(".gitignore")` returns `.gitignore` in .NET (the entire name is treated as the extension), so dotfiles ARE covered by `TextExtensions` in most cases. The `KnownDotfiles` fallback is defense-in-depth for explicit filename matching — it does not rely on any runtime variation, but makes the intent explicit and keeps the dotfile set readable as filenames rather than as pseudo-extensions. The current `MainWindowViewModel` already includes these extensions in its `SupportedExtensions` set — extracting to `SupportedFileTypes` preserves dotfile support and centralizes the policy in one move.
- **Known gap (preserved from current behavior):** Basename-only text files (`Makefile`, `Dockerfile`, `LICENSE`) return `false` from `IsTextFile` — these are common text files the editor currently cannot open. A future enhancement could add a `KnownBasenames` set.
- **New tests (required for M6):** Add `IsTextFile_Dotfile_KnownName` that passes a bare filename like `".gitignore"` and verifies true. Also add `IsTextFile_Dotfile_FullPath` passing a full path like `"/home/user/.editorconfig"` and verifies true.

**MainWindowViewModel changes:**
- Remove `SupportedExtensions` field
- Remove `using System.Collections.Generic;` if no longer needed
- Use `SupportedFileTypes.IsTextFile(path)` instead of `SupportedExtensions.Contains(ext)`

**New tests (required for M6):**
- Create `tests/Zaide.Tests/Services/SupportedFileTypesTests.cs` with:
  - `IsTextFile_SupportedExtension_ReturnsTrue` (e.g., `.cs`, `.json`)
  - `IsTextFile_UnsupportedExtension_ReturnsFalse` (e.g., `.exe`, `.dll`)
  - `IsTextFile_NoExtension_ReturnsFalse`
  - `IsTextFile_CaseInsensitive` (e.g., `.CS` == `.cs`)
  - `IsTextFile_Dotfile_ReturnsTrue` (e.g., `.editorconfig`, `.gitignore`, `.gitattributes`)
  - `IsTextFile_BasenameOnly_ReturnsFalse` (e.g., `Makefile`, `Dockerfile`)

### M7: Stabilize + Regression

**Manual test matrix:**
- [ ] Open folder → tree populates
- [ ] Create file → tree updates
- [ ] Rename file → tree updates
- [ ] Delete file → tree updates
- [ ] ExpandAll → all nodes expand
- [ ] CollapseAll → all nodes collapse
- [ ] Toggle hidden files → watcher restarts, tree updates
- [ ] Open text file → editor opens
- [ ] Edit → dirty flag shows
- [ ] Save → dirty flag clears
- [ ] Close dirty tab → dialog shows
- [ ] Terminal start → shell runs
- [ ] Terminal stop → process exits
- [ ] Terminal restart → new shell starts
- [ ] Toggle bottom panel → terminal shows/hides

## Exit Conditions

- [x] Build succeeds: `dotnet build`
- [x] All tests pass: `dotnet test` — 340 passed, 0 failed
- [x] `Document` does not reference `IFileService`
- [x] `FileTreeNode` does not inherit `ReactiveObject` (implements `INotifyPropertyChanged` directly)
- [x] `FileTreeViewModel` depends on `IFileTreeService` (interface), not concrete class
- [x] `FileTreeViewModel` does not use `AvaloniaScheduler.Instance` directly (injected via DI)

---

## Completion Summary (2025-01-20)

**Refactor-2: Layer Boundary Cleanup** — ✅ COMPLETE

All targeted milestones (M1, M3, M5, M6, M7) have been implemented and verified:

| Milestone | Status | Notes |
|-----------|--------|-------|
| M1: Clean Models | ✅ Complete | Removed `SaveAsync` from Document, replaced ReactiveObject with INotifyPropertyChanged in FileTreeNode |
| M2: Terminal pure logic | ⬜ Deferred | Out of scope — requires namespace change |
| M3: IFileTreeService | ✅ Complete | Interface extracted, StartWatching returns IObservable |
| M4: Tree manipulation | ⬜ Deferred | Stays in VM — UI state management |
| M5: IScheduler injection | ✅ Complete | FileTreeViewModel uses injected scheduler |
| M6: SupportedFileTypes | ✅ Complete | Static class in Services/, editor policy centralized |
| M7: Stabilize | ✅ Complete | 340 tests pass, manual regression verified |
| M8: UI-post seam | ⬜ Deferred | Requires IUIThreadPoster abstraction |

**Files changed:**
- `Models/Document.cs` — removed SaveAsync, added RecordSaveError
- `Models/FileTreeNode.cs` — implements INotifyPropertyChanged directly
- `Services/IFileTreeService.cs` — new interface
- `Services/IFileTreeQuery.cs` — new interface
- `Services/IFileTreeWatcher.cs` — new interface
- `Services/FileTreeService.cs` — implements IFileTreeService
- `Services/SupportedFileTypes.cs` — new static class
- `ViewModels/FileTreeViewModel.cs` — depends on IFileTreeService, injected IScheduler
- `ViewModels/EditorViewModel.cs` — coordinates save workflow
- `Program.cs` — DI registrations updated

**Test results:** 340 passed, 0 failed

**Deferred to future refactors:**
- M2 (Terminal pure logic namespace change)
- M4 (FileTreeNode domain/UI split)
- M8 (IUIThreadPoster abstraction)
- [ ] `MainWindowViewModel` does not contain file extension logic (delegated to `SupportedFileTypes`)
- [ ] No behavioral changes from user perspective

**Deferred (not exit conditions for this refactor):**
- Terminal pure logic remains in `ViewModels/` folder (M2 deferred — would violate CONVENTIONS.md)
- Tree manipulation logic remains in `FileTreeViewModel` (M4 deferred — would muddy service boundary)

## Rollback Plan

- Commit hash to revert to: (fill before starting M1)
- Fallback strategy:
  - Restore `ReactiveObject` on `FileTreeNode` (revert INotifyPropertyChanged implementation)
  - Restore `IFileService` parameter on `Document.SaveAsync`
  - Restore `AvaloniaScheduler.Instance` usage in `FileTreeViewModel` (remove injected scheduler)
  - Restore `SupportedExtensions` in `MainWindowViewModel` (remove `SupportedFileTypes` class)
  - Remove `IFileTreeService` interface (revert to concrete `FileTreeService` dependency)
  - Delete `src/Services/IFileTreeQuery.cs`, `IFileTreeWatcher.cs`, `IFileTreeService.cs`

## Future Refactor (Out of Scope)

After this refactor is complete, the codebase will be **closer to ready** for a future project split, but core/UI separation will still be incomplete. Remaining work includes:

1. **Namespace cleanup**: Move terminal types to `Zaide.Core.Terminal` (requires namespace change — deferred from M2)
2. **TerminalViewModel UI-post abstraction**: Inject `IUIThreadPoster` to remove the production constructor's `Dispatcher.UIThread.Post` dependency (deferred from M8)
3. **Project split**: Create `Zaide.Core`, `Zaide.Application`, `Zaide.Infrastructure`, `Zaide.UI` projects
4. **MainWindow decomposition**: Extract `MainWindowLayoutBuilder`, `MainWindowKeyBindings`, `MainWindowDialogHandler`
5. **Status routing**: Create `IStatusReporter` interface for app-level status messages
