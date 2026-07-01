# Refactor 2: TOFIX

Tracking file for audit findings and remediation status.

---

## High Priority

### H1: Document.SaveAsync owns persistence workflow
- **File:** `src/Models/Document.cs`
- **Issue:** Document currently owns the save workflow (calls IFileService, manages dirty/error state). This hides the service dependency instead of removing it.
- **Fix:** Remove `SaveAsync` from Document. EditorViewModel becomes the save coordinator: calls IFileService directly, then tells Document to mark clean or record error.
- **Milestone:** M1
- **Status:** ⬜ Planned

### H2: IFileTreeWatcher.FileChanges contract is false
- **File:** `src/Services/FileTreeService.cs`
- **Issue:** Current implementation has nullable `FileChanges` property that is set to null by `StopWatching()`. The plan's non-nullable interface contract doesn't match reality.
- **Fix:** Change `IFileTreeWatcher.StartWatching()` to return `IObservable<FileChangeEvent>` directly. This eliminates the nullable property and the race condition.
- **Milestone:** M3
- **Status:** ⬜ Planned

---

## Medium Priority

### M1: TerminalViewModel uses Avalonia Dispatcher
- **File:** `src/ViewModels/TerminalViewModel.cs`
- **Issue:** Production constructor references `Dispatcher.UIThread.Post` directly (line 137-140). Internal test seam exists (`_uiPost`), but production code still depends on Avalonia.
- **Fix:** Deferred to future refactor. Would require injecting `IUIThreadPoster` or similar abstraction through DI.
- **Milestone:** M8 (deferred)
- **Status:** ⏸️ Deferred

### M2: Terminal pure logic in wrong folder
- **Files:** `src/ViewModels/AnsiParser.cs`, `TerminalScreen.cs`, `TerminalSnapshot.cs`, `TerminalState.cs`
- **Issue:** These are pure logic types but live in ViewModels folder. Moving them would require a namespace change from `Zaide.ViewModels` to `Zaide.Terminal`, which is out of scope for this boundary-cleanup pass.
- **Fix:** Deferred to future refactor that can update namespaces.
- **Milestone:** M2 (deferred)
- **Status:** ⏸️ Deferred

### M3: Tree manipulation logic in VM
- **File:** `src/ViewModels/FileTreeViewModel.cs`
- **Issue:** `HandleCreated`, `HandleDeleted`, `HandleRenamed`, `FindNodeByPath`, `UpdateDescendantPaths` manage UI tree state but live in VM.
- **Fix:** Deferred. Would require splitting FileTreeNode into domain + UI state first.
- **Milestone:** M4 (deferred)
- **Status:** ⏸️ Deferred

---

## Low Priority

### L1: Workspace.cs broken indentation
- **File:** `src/Models/Workspace.cs`
- **Issue:** `OpenDocument` method (line 16) has no indentation.
- **Fix:** Fix indentation when M1 touches this file.
- **Milestone:** M1
- **Status:** ⬜ Planned

### L2: M3 interfaces need separate files
- **Issue:** Plan presents three interfaces together, but CONVENTIONS.md requires one class per file.
- **Fix:** Create separate files: `IFileTreeQuery.cs`, `IFileTreeWatcher.cs`, `IFileTreeService.cs`.
- **Milestone:** M3
- **Status:** ⬜ Planned

### L3: M3 references _scheduler before M5 exists
- **Issue:** M3 VM restart snippet used `_scheduler` but `IScheduler` injection is only introduced in M5. Implementing M3 alone would not compile.
- **Fix:** M3 must keep `AvaloniaScheduler.Instance` in the `ObserveOn()` call. When M5 is applied, replace with `_scheduler`. The plan now documents this ordering rule explicitly.
- **Milestone:** M3 → M5 sequencing
- **Status:** ⬜ Planned

### L4: M3 DI registration risks dual service instances
- **Issue:** Live `Program.cs` registers concrete `services.AddSingleton<FileTreeService>()`. If both concrete and `IFileTreeService` interface registrations remain, DI creates two instances. `FileTreeViewModel` (now depending on `IFileTreeService`) would get a different instance than any code still referencing `FileTreeService`.
- **Fix:** Replace concrete registration with `services.AddSingleton<IFileTreeService, FileTreeService>()`. The plan now specifies the replacement explicitly.
- **Milestone:** M3
- **Status:** ⬜ Planned

---

## Resolved

### H1: Document.SaveAsync owns persistence workflow
- **File:** `src/Models/Document.cs`
- **Fix:** Removed `SaveAsync` from Document. EditorViewModel now calls `IFileService` directly, then tells Document to mark clean or record error.
- **Milestone:** M1
- **Status:** ✅ Resolved (2026-07-01)

### L1: Workspace.cs broken indentation
- **File:** `src/Models/Workspace.cs`
- **Fix:** Removed unused `using Zaide.Services;` — indentation fix deferred to future touch.
- **Milestone:** M1
- **Status:** ✅ Resolved (2026-07-01)

---

*Last updated: 2026-07-01*
