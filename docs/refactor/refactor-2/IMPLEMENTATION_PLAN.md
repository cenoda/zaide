# Refactor 2: Project Boundary Split — Implementation Plan

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
2. Remove UI framework dependencies from Model types
3. Move pure logic out of ViewModels into appropriate homes
4. Extract interfaces for concrete service dependencies in ViewModels
5. Reduce MainWindow's composition burden

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

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate: current build/tests pass | `dotnet test` — zero failures | ⬜ Not started |
| M1 | **Clean Models layer**: Remove `IFileService` from `Document.SaveAsync` (use event/callback pattern instead). Remove `ReactiveObject` from `FileTreeNode` (implement `INotifyPropertyChanged` directly — see M1 plan below). Remove unused `using` from `Workspace`. | `DocumentTests`, `WorkspaceTests`, `FileTreeViewModelTests` pass | ⬜ Not started |
| M2 | **Terminal pure logic — deferred**: `AnsiParser`, `TerminalScreen`, `TerminalSnapshot`, `TerminalState` are already pure. Moving them to `Terminal/` would violate CONVENTIONS.md (namespace must match folder). **No file moves this refactor.** A future refactor can move them + update namespace to `Zaide.Terminal`. | No changes — files stay in `ViewModels/` | ⬜ Not started |
| M3 | **Extract IFileTreeService interface**: Create `IFileTreeService` interface from `FileTreeService`. Split into pure enumeration (`IFileTreeQuery`) and watching (`IFileTreeWatcher`). ViewModels depend on interfaces only. | `FileTreeServiceTests`, `FileTreeViewModelTests` pass | ⬜ Not started |
| M4 | **Tree manipulation logic — stays in VM**: `HandleCreated`, `HandleDeleted`, `HandleRenamed`, `FindNodeByPath`, `UpdateDescendantPaths` manage UI tree state, not filesystem infrastructure. Moving them to `FileTreeService` would muddy the boundary. **Keep in `FileTreeViewModel`.** A future refactor can extract a pure `FileTreeUpdater` class after splitting `FileTreeNode` into domain + UI state. | `FileTreeViewModelTests` pass; manual regression: open folder → create/rename/delete files → tree updates | ⬜ Not started |
| M5 | **Remove AvaloniaScheduler from FileTreeViewModel**: Inject `IScheduler` as a **required** constructor parameter. Register `AvaloniaScheduler.Instance` in DI (`Program.cs`). Tests inject `CurrentThreadScheduler.Instance`. No fallback to `AvaloniaScheduler.Instance` in VM code. | `FileTreeViewModelTests` pass | ⬜ Not started |
| M6 | **Move file extension logic out of MainWindowViewModel**: Extract `SupportedExtensions` to a static `SupportedFileTypes` class in `Services/` (not `Models/` — this is editor policy, not domain data). MainWindowViewModel delegates to it. | `MainWindowViewModelTests` (if any) pass; manual: open file → opens in editor; open binary → shows status | ⬜ Not started |
| M7 | **Stabilize + regression sweep**: Full manual regression. All tests pass. No behavioral changes. | `dotnet test` — zero regressions; manual: open/edit/save/close/reopen, terminal start/stop/restart, file tree operations | ⬜ Not started |

## Detailed Milestone Plans

### M1: Clean Models Layer

**Document.cs changes:**
- Remove `IFileService` parameter from `SaveAsync`
- Option A: Make `SaveAsync` take a `Func<string, string, Task>` delegate
- Option B: Make `SaveAsync` raise an event that the VM handles
- Option C: Keep `IFileService` but move it to a separate `DocumentSaver` class
- **Decision:** Use Option A (delegate) — keeps Document simple, avoids new class

**FileTreeNode.cs changes:**
- Remove `ReactiveObject` inheritance
- **Implement `INotifyPropertyChanged` directly** (plain C# event, no ReactiveUI)
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

**Workspace.cs changes:**
- Remove unused `using Zaide.Services;`

### M2: Extract Terminal Pure Logic — DEFERRED

**No changes this refactor.**

**Rationale:** `AnsiParser`, `TerminalScreen`, `TerminalSnapshot`, and `TerminalState` are already pure logic (no UI framework dependencies). Moving them to a `Terminal/` subfolder while keeping namespace `Zaide.ViewModels` would violate `CONVENTIONS.md` (namespaces must match folder structure).

**Future refactor:** When ready for namespace cleanup, move files to `src/Terminal/` and update namespace to `Zaide.Terminal`. This requires updating all references and test files.

### M3: Extract IFileTreeService Interface

**New interfaces:**
```csharp
// Pure tree enumeration — no infrastructure
public interface IFileTreeQuery
{
    List<FileTreeNode> EnumerateDirectory(string path, bool includeHidden = false);
    bool IsIgnored(string name);
}

// File system watching — infrastructure
public interface IFileTreeWatcher : IDisposable
{
    IObservable<FileChangeEvent>? FileChanges { get; }
    void StartWatching(string path, bool includeHidden = false);
    void StopWatching();
}

// Combined interface for backward compat
public interface IFileTreeService : IFileTreeQuery, IFileTreeWatcher
{
    void CreateFile(string path);
    void CreateDirectory(string path);
}
```

**FileTreeService changes:**
- Implement `IFileTreeService`
- No other changes in this milestone

**FileTreeViewModel changes:**
- Change dependency from `FileTreeService` to `IFileTreeService`

### M4: Tree Manipulation Logic — Stays in VM

**No changes this refactor.**

**Rationale:** `HandleCreated`, `HandleDeleted`, `HandleRenamed`, `FindNodeByPath`, and `UpdateDescendantPaths` manage **UI tree state** (the `ObservableCollection<FileTreeNode>` hierarchy), not filesystem infrastructure. `FileTreeService` is filesystem infrastructure (enumeration, watching, file/directory creation). Moving UI state mutation into `FileTreeService` would muddy the boundary instead of cleaning it.

**Future refactor:** After splitting `FileTreeNode` into pure domain file entries + UI node state, extract a pure `FileTreeUpdater` class that operates on the UI node state. This keeps filesystem infrastructure separate from UI state management.

### M5: Remove AvaloniaScheduler from FileTreeViewModel

**FileTreeViewModel changes:**
- Add **required** constructor parameter `IScheduler scheduler`
- Replace `AvaloniaScheduler.Instance` with injected `_scheduler`
- Remove `using ReactiveUI.Avalonia;` from the file
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
        ".editorconfig", ".gitignore", ".gitattributes", ".yml",
        ".yaml", ".css", ".html", ".js", ".ts", ".fs", ".vb",
        ".xaml", ".resx", ".razor", ".cshtml", ".svg"
    };

    public static bool IsTextFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Length > 0 && TextExtensions.Contains(ext);
    }
}
```

**MainWindowViewModel changes:**
- Remove `SupportedExtensions` field
- Remove `using System.Collections.Generic;` if no longer needed
- Use `SupportedFileTypes.IsTextFile(path)` instead of `SupportedExtensions.Contains(ext)`

### M7: Stabilize + Regression

**Manual test matrix:**
- [ ] Open folder → tree populates
- [ ] Create file → tree updates
- [ ] Rename file → tree updates
- [ ] Delete file → tree updates
- [ ] Open text file → editor opens
- [ ] Edit → dirty flag shows
- [ ] Save → dirty flag clears
- [ ] Close dirty tab → dialog shows
- [ ] Terminal start → shell runs
- [ ] Terminal stop → process exits
- [ ] Terminal restart → new shell starts
- [ ] Toggle bottom panel → terminal shows/hides

## Exit Conditions

- [ ] Build succeeds: `dotnet build`
- [ ] All tests pass: `dotnet test` — zero regressions
- [ ] `Document` does not reference `IFileService`
- [ ] `FileTreeNode` does not inherit `ReactiveObject` (implements `INotifyPropertyChanged` directly)
- [ ] `FileTreeViewModel` depends on `IFileTreeService` (interface), not concrete class
- [ ] `FileTreeViewModel` does not use `AvaloniaScheduler.Instance` directly (injected via DI)
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

## Future Refactor (Out of Scope)

After this refactor is complete, the codebase will be ready for:
1. **Namespace cleanup**: Move terminal types to `Zaide.Core.Terminal`
2. **Project split**: Create `Zaide.Core`, `Zaide.Application`, `Zaide.Infrastructure`, `Zaide.UI` projects
3. **MainWindow decomposition**: Extract `MainWindowLayoutBuilder`, `MainWindowKeyBindings`, `MainWindowDialogHandler`
4. **Status routing**: Create `IStatusReporter` interface for app-level status messages
