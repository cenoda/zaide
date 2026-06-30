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
| M1 | **Clean Models layer**: Remove `IFileService` from `Document.SaveAsync` (use event/callback pattern instead). Remove `ReactiveObject` from `FileTreeNode` (use plain property with manual notification or move expansion state to VM). Remove unused `using` from `Workspace`. | `DocumentTests`, `WorkspaceTests`, `FileTreeViewModelTests` pass | ⬜ Not started |
| M2 | **Extract terminal pure logic**: Move `AnsiParser`, `TerminalScreen`, `TerminalSnapshot`, `TerminalState` to a new `Terminal/` subfolder (logical grouping, no namespace change yet). These are already pure — just reorganize. | `AnsiParserTests`, `TerminalScreenTests`, `TerminalSnapshotTests` pass | ⬜ Not started |
| M3 | **Extract IFileTreeService interface**: Create `IFileTreeService` interface from `FileTreeService`. Split into pure enumeration (`IFileTreeQuery`) and watching (`IFileTreeWatcher`). ViewModels depend on interfaces only. | `FileTreeServiceTests`, `FileTreeViewModelTests` pass | ⬜ Not started |
| M4 | **Move tree manipulation logic to service**: Move `HandleCreated`, `HandleDeleted`, `HandleRenamed`, `FindNodeByPath`, `UpdateDescendantPaths` from `FileTreeViewModel` to `FileTreeService` (or a new `FileTreeUpdater`). VM becomes a thin reactive bridge. | `FileTreeViewModelTests` pass; manual regression: open folder → create/rename/delete files → tree updates | ⬜ Not started |
| M5 | **Remove AvaloniaScheduler from FileTreeViewModel**: Replace `AvaloniaScheduler.Instance` with an injected `IScheduler` or use `ObserveOn` with an injected scheduler. VM should not know about Avalonia. | `FileTreeViewModelTests` pass | ⬜ Not started |
| M6 | **Move file extension logic out of MainWindowViewModel**: Extract `SupportedExtensions` and the extension-checking logic to a service or constant class. MainWindowViewModel delegates to it. | `MainWindowViewModelTests` (if any) pass; manual: open file → opens in editor; open binary → shows status | ⬜ Not started |
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
- Make `IsExpanded` a plain auto-property
- Move expansion state tracking to `FileTreeViewModel` (it already manages the tree)
- Alternatively: Keep `IsExpanded` in model but use a simple event pattern

**Workspace.cs changes:**
- Remove unused `using Zaide.Services;`

### M2: Extract Terminal Pure Logic

**File moves (no namespace changes):**
- `ViewModels/AnsiParser.cs` → `Terminal/AnsiParser.cs`
- `ViewModels/TerminalScreen.cs` → `Terminal/TerminalScreen.cs`
- `ViewModels/TerminalSnapshot.cs` → `Terminal/TerminalSnapshot.cs`
- `ViewModels/TerminalState.cs` → `Terminal/TerminalState.cs`

**Namespace:** Keep `Zaide.ViewModels` for now (future refactor can change to `Zaide.Core.Terminal`)

**Test moves:**
- `ViewModels/AnsiParserTests.cs` → `Terminal/AnsiParserTests.cs`
- `ViewModels/TerminalScreenTests.cs` → `Terminal/TerminalScreenTests.cs`
- `ViewModels/TerminalSnapshotTests.cs` → `Terminal/TerminalSnapshotTests.cs`

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

### M4: Move Tree Manipulation to Service

**FileTreeService additions:**
- Move `HandleCreated`, `HandleDeleted`, `HandleRenamed` from VM
- Move `FindNodeByPath`, `UpdateDescendantPaths` from VM
- Add method `ApplyChange(FileChangeEvent change, IList<FileTreeNode> rootNodes, string? rootPath)`

**FileTreeViewModel changes:**
- Remove tree manipulation methods
- Call `_fileTreeService.ApplyChange(...)` in `HandleFileChange`
- VM becomes a thin bridge: service events → UI updates

### M5: Remove AvaloniaScheduler from FileTreeViewModel

**FileTreeViewModel changes:**
- Add constructor parameter `IScheduler? scheduler = null`
- Use `scheduler ?? AvaloniaScheduler.Instance` (temporary)
- Future: inject via DI

**Test changes:**
- Pass `CurrentThreadScheduler.Instance` in tests for synchronous execution

### M6: Move File Extension Logic

**New class:**
```csharp
// In Services or Models
public static class SupportedFileTypes
{
    public static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".json", ".md", ".txt", ...
    };
    
    public static bool IsTextFile(string path) => ...
}
```

**MainWindowViewModel changes:**
- Remove `SupportedExtensions` field
- Use `SupportedFileTypes.IsTextFile(path)` instead

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
- [ ] `FileTreeNode` does not inherit `ReactiveObject`
- [ ] `FileTreeViewModel` depends on `IFileTreeService` (interface), not concrete class
- [ ] `FileTreeViewModel` does not use `AvaloniaScheduler.Instance` directly
- [ ] Terminal pure logic (AnsiParser, TerminalScreen, TerminalSnapshot, TerminalState) is in `Terminal/` folder
- [ ] `MainWindowViewModel` does not contain file extension logic
- [ ] No behavioral changes from user perspective

## Rollback Plan

- Commit hash to revert to: (fill before starting M1)
- Fallback strategy:
  - Revert all file moves (M2 is reversible)
  - Restore `ReactiveObject` on `FileTreeNode`
  - Restore `IFileService` parameter on `Document.SaveAsync`
  - Restore tree manipulation methods in `FileTreeViewModel`

## Future Refactor (Out of Scope)

After this refactor is complete, the codebase will be ready for:
1. **Namespace cleanup**: Move terminal types to `Zaide.Core.Terminal`
2. **Project split**: Create `Zaide.Core`, `Zaide.Application`, `Zaide.Infrastructure`, `Zaide.UI` projects
3. **MainWindow decomposition**: Extract `MainWindowLayoutBuilder`, `MainWindowKeyBindings`, `MainWindowDialogHandler`
4. **Status routing**: Create `IStatusReporter` interface for app-level status messages
