# Phase 2: TOFIX

Code quality issues found during Phase 2 review. Check these before starting Phase 3.

---

## Verified

### Pre-implementation gate (2026-06-26)
- [x] `dotnet build Zaide.slnx` passes with 0 warnings
- [x] `dotnet test Zaide.slnx` passes: 22 tests, 0 failures
- [x] Phase 1 TOFIX.md items reviewed — one open item ("Hardcoded colors") deferred to Phase 3/5

### Post-M3 visual check (2026-06-26)
- [x] `dotnet build Zaide.slnx`: 0 warnings, 0 errors
- [x] `dotnet test Zaide.slnx`: 33 passed, 0 failed
- [x] Phase 1 → Phase 2 exit conditions all met

---

## Resolved

### [x] Editor blank content — ISSUE-002
Root cause: `DockPanel` gave fill space to `_welcomeText`, collapsing `_editorView`
to zero height. Also missing `AvaloniaEdit.xaml` theme style. Fixed by replacing
`DockPanel` with 2-row `Grid` + adding Fluent theme includes. Commit: `13dabbb`.

### [x] Close button invisible — ISSUE-003
Root cause: `PointerEntered` on `Border`/`Grid` never fired — Avalonia delivers
to topmost child. Fixed by replacing themed `Button` with custom `Border+TextBlock`
glyph in its own Grid column, hover driven by `IsPointerOver` observable instead
of pointer events. Commit: `f6b5535`.

### [x] Ctrl+` toggle broken
Root cause: `Key.OemTilde` fails on non-US keyboards + `WhenActivated` added
duplicate KeyBindings. Fixed by using `Key.Oem3` (physical backtick), adding
`Ctrl+J` fallback, and removing duplicates before adding.

### Post-M6 verification (2026-06-26)
- [x] `dotnet build Zaide.slnx`: 0 warnings, 0 errors
- [x] `dotnet test Zaide.slnx`: 35 passed, 0 failures
- [x] All M1–M6 exit conditions met
- [x] Font switching: Georgia/serif for .md, Cascadia Code for code
- [x] Indent guides deferred to Phase 2.1 (IBackgroundRenderer approach)

---

## Phase 1 Carry-Over

### [ ] Hardcoded colors in `MainWindow.axaml.cs` — agent area + bottom (deferred)
Right agent area and bottom panel still use `BuildPanel` with `Color.Parse`.
Deferred to Phase 5 (agent) and Phase 3 (terminal).

---

## Audit Findings

Production-bound issues found after Phase 2 completion. Fix before treating the
editor as stable.

### [x] Save failure can still close a dirty tab
- `EditorViewModel.Save()` returns `bool`; `CloseTabAsync` awaits result, skips close on false.
- `EditorTabViewModel.LastSaveError` surfaces the error; `MainWindowViewModel` pushes it to `StatusText`.
- `EditorViewModel.LastSaveError` holds the exception message.
- Tests: `SaveCommand_Fails_WhenPathIsDirectory` (VM-level) + `CloseTab_StaysOpen_WhenSaveFails` (full close flow).

### [x] Remove hardcoded `/tmp` debug logging from `MainWindow`
- Removed both `Log()` calls in ActiveTab subscription and the `Log` method itself.
- `grep -r zaide-debug` + `grep -r AppendAllText` across `src/` — zero remaining hits.

### [x] Reset syntax highlighting when switching to unsupported/plain-text files
- `ApplyFileMode` now always calls `SetGrammar(scope!)` — null scope passes through
  `LoadGrammar` (returns null), clearing the previous grammar.
- Verified safe: `TextMateSharp.Registry.LoadGrammar` returns null on null/empty input.

### [x] Move file I/O off the UI thread
- Created `IFileService` + `FileService` with `ReadAllTextAsync` / `WriteAllTextAsync`.
- `EditorViewModel.SaveAsync` + `EditorTabViewModel.OpenFileAsync` use the service.
- Both commands changed to `CreateFromTask`; tests updated to `async Task` with `await`.

### [x] Clean up `EditorTabBar` lifecycle and subscription management
- `DisposeAllSubscriptions()` called before clearing in `SetTabs()` and `Reset`.
- `RemoveTab` disposes both `_hoverSubscriptions` and `_hoverCts`.
- Helper method disposes all subscriptions + cancels all CTS tokens.

### [x] Remove uncancelled hover-delay tasks in `EditorTabBar`
- Replaced `Task.Delay(200).ContinueWith` with `CancellationTokenSource` per tab.
- Hover-in cancels pending hide + shows; hover-out cancels old token, creates new 200ms delay.
- `TaskContinuationOptions.NotOnCanceled` avoids running cancelled continuations.

### [x] Make `EditorView` react to active-tab content changes, not just VM swaps
- `GetObservable(ViewModelProperty).Select(...WhenAnyValue(TextContent)).Switch()` pattern
  tracks the current VM's TextContent — updates on both VM change and programmatic changes.
- `_isUpdatingFromViewModel` guard flag prevents feedback loop; text equality check avoids
  redundant sets. `ApplyFileMode` still tied to VM changes only.

### [x] Stop creating long-lived constructor subscriptions in `MainWindowViewModel`
- Subscriptions moved to `Activate()` with `CompositeDisposable` storage.
- `MainWindow.WhenActivated` calls `vm.Activate()` and disposes on deactivation.
- `IDisposable` implemented; re-entrant guard prevents double-activation.

### [x] Fix misleading status text for extensionless unsupported files
- Changed `"Opened: {file.Name}"` to `"Unsupported file type: (no extension)"` for extensionless files.

### [ ] Add tests for the risky paths
- Missing coverage:
  - save failure must not close the tab
  - cancel close on dirty tab
  - grammar reset on supported -> unsupported switch
  - large/slow file behavior assumptions
  - `EditorTabBar` subscription cleanup/lifecycle
