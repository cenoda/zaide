# Phase 1.1: TOFIX

Code quality issues found during Phase 1.1 review. Check these before starting Phase 3.

---

## Verified

### Pre-implementation gate (2026-06-27)
- [x] `dotnet build Zaide.slnx` passes with 0 warnings
- [x] `dotnet test Zaide.slnx` passes: 85 tests, 0 failures
- [x] Phase 1 TOFIX.md items all resolved
- [x] Phase 2 TOFIX.md items all resolved

### Post-implementation (2026-06-27)
- [x] `dotnet build Zaide.slnx`: 0 warnings, 0 errors
- [x] `dotnet test Zaide.slnx`: 85 passed, 0 failed
- [x] `docs-rules.md` §11b violations: 0 (B3 resolved)
- [x] B1 (rename cascade): fixed with `StartsWith` + prefix slice
- [x] B2 (OpenFolder error handling): 4 exception types caught
- [x] C1 (`.`/`..` guard): resolved with `is not "." and not ".."` check
- [x] M2 GridSplitter: dedicated 4px column, 180–500px range
- [x] M3 Context menu + IsExpanded binding: `AttachedToVisualTree` two-way bind
- [x] M4 Keyboard shortcuts: Ctrl+O (`Interaction` pattern), Enter + Double-click (`RequestOpenFileCommand`)
- [x] M5 Sort-order: `.OrderBy()` on both directory and file enumerations

---

## Open

### [x] M1 plan-required tests not created

`docs/phases/phase-1.1/IMPLEMENTATION_PLAN.md` lists 6 test methods for
`tests/Zaide.Tests/ViewModels/FileTreeViewModelTests.cs`:
- `HandleRenamed_UpdatesDescendantPaths`
- `HandleRenamed_UpdatesDescendantPaths_WithNonAscii`
- `HandleRenamed_DoesNotCorruptPaths_WithPartialNameMatch`
- `OpenFolderCommand_SetsStatusText_OnInaccessiblePath`
- `OpenFolderCommand_SetsStatusText_OnFilePath`
- `OpenFolderCommand_SetsStatusText_OnInvalidPath`

All tests have been implemented and are passing. The file now contains all required tests.

---

## Known Limitations (by design, not for TOFIX)

### Enter key collapses parent directory when opening a file in a subdirectory
Avalonia TreeView's internal Enter handler toggles the parent `TreeViewItem`
expand/collapse state before our `KeyDown` handler fires. `AddHandler` with
`handledEventsToo: true` prevents the file from NOT opening, but the parent
directory still collapses visually. Full fix requires TreeViewItem template
customization — deferred to a future polish phase.

---

## Phase 1.1 Carry-Over (for Phase 3+)

### C2: `StatusText` properties have no UI binding
`FileTreeViewModel.StatusText` and `MainWindowViewModel.StatusText` receive
error messages but no visual widget renders them. Planned for Phase 3 status bar.

---

*Last updated: 2026-06-27*
