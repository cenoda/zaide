# Phase 1.1: TOFIX

Code quality issues found during Phase 1.1 review. Check these before starting Phase 3.

---

## Verified

### Pre-implementation gate — historical baseline (2026-06-27)
- [x] `dotnet build Zaide.slnx` passes with 0 errors
- [x] `dotnet test Zaide.slnx` passes: **85 tests, 0 failures** (baseline before Phase 1.1 work)
- [x] Phase 1 TOFIX.md items all resolved
- [x] Phase 2 TOFIX.md items all resolved

### Post-implementation (2026-06-27)
- [x] `dotnet build Zaide.slnx`: 3 warnings (xUnit analyzer), 0 errors
- [x] `dotnet test Zaide.slnx`: 93 passed, 0 failed
- [x] `docs-rules.md` §11b violations: 0 (B3 resolved)
- [x] B1 (rename cascade): fixed with `StartsWith` + prefix slice
- [x] B2 (OpenFolder error handling): 4 exception types caught
- [x] C1 (`.`/`..` guard): resolved with `is not "." and not ".."` check
- [x] M2 GridSplitter: dedicated 4px column, 180–500px range
- [x] M3 Context menu + IsExpanded binding: `AttachedToVisualTree` two-way bind
- [x] M4 Keyboard shortcuts: Ctrl+O (`Interaction` pattern), Enter + Double-click (`RequestOpenFileCommand`)
- [x] M5 Sort-order: `.OrderBy()` on both directory and file enumerations
- [x] Plan-required tests: all 6 M1 tests created, 93 total

---

## Open

### [ ] xUnit analyzer warnings (3 warnings, non-blocking)

`FileTreeViewModelTests.cs` uses `Assert.True(..., StartsWith(...))` — prefers `Assert.StartsWith`.
Minor style. Not blocking Phase 1.2.

### [x] B2 follow-up: RootPath set before validation (FIXED 2026-06-27)

`RootPath = path;` was before `EnumerateDirectory(path)`. On failure, watcher stopped,
`RootPath` pointed at bad path, no active watcher. Fixed: `RootPath` moved after validation.

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
