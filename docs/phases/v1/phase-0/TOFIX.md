# Phase 0: TOFIX

Code quality issues found during Phase 0 review. Check these before starting Phase 1.

---

## Verified

### ISSUE-001 regression check (2025-06-25)
- Clean build (`rm -rf obj` → `restore` → `build` × 5): all pass, 0 warnings.
- Structural fix holds: `.csproj` in `src/`, `tests/` is sibling, `DefaultItemExcludes` removed.
- No sign of duplicate assembly attributes. ISSUE-001 stays closed.

### Phase 0 build stability (2025-06-25)
- 10+ consecutive `dotnet build` runs: EXIT 0, 0 warnings, 0 errors.
- 2 tests pass. No flaky behavior.

---

## Open

### [x] Remove stale `.gitkeep` from `src/ViewModels/`
`MainWindowViewModel.cs` now lives in this directory. `.gitkeep` is no longer needed.

### [x] Add `x:TypeArguments` to `MainWindow.axaml` (nice-to-have)
`MainWindow` inherits `ReactiveWindow<MainWindowViewModel>`. No bindings in XAML yet, so it's harmless — but when Phase adds XAML bindings, this will be required.
```xml
<Window ... xmlns:vm="using:Zaide.ViewModels"
             x:TypeArguments="vm:MainWindowViewModel">
```
