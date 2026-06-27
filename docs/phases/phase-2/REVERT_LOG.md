# Phase 2: Revert Log

## What Was Reverted

- **Reverted from:** `2ff4a01` — `feat: implement unsaved changes dialog and save functionality in editor tabs`
- **Reverted to:** `0971113` — `phase-2: add implementation plan and TOFIX documentation` (docs only, zero implementation)
- **Commits discarded:**
  - `4ca125b` — `phase-2: M1-M3 — editor, tab bar, and file-open wiring`
  - `2ff4a01` — `feat: implement unsaved changes dialog and save functionality in editor tabs`
- **Files removed:**
  - `src/Views/EditorView.cs`
  - `src/Views/EditorTabBar.cs`
  - `src/ViewModels/EditorViewModel.cs`
  - `src/ViewModels/EditorTabViewModel.cs`
  - `tests/Zaide.Tests/ViewModels/EditorViewModelTests.cs`
- **Files modified (reverted to pre-implementation state):**
  - `src/MainWindow.axaml.cs`
  - `src/ViewModels/MainWindowViewModel.cs`
  - `src/Program.cs`
  - `src/Zaide.csproj`
  - `.gitignore`
  - `Directory.Packages.props`
  - `docs/LIBRARIES.md`

## Root Cause

The implementation was fundamentally broken at the structural level — not just
bugs that could be patched, but architectural decisions that would mislead every
future agent who copied them.

### 1. MVVM violation: ViewModel knew about Views

`EditorTabViewModel.ShowUnsavedDialog` was a `Func<EditorViewModel, (bool, bool)>?`
callback set by `MainWindow`. This inverted MVVM — the View injected a UI callback
into the ViewModel. Future agents would copy this pattern and put `Func<Window>`
everywhere.

**Should have used:** `Interaction<EditorViewModel, bool>` — ViewModel exposes
an interaction, View subscribes and owns the dialog.

### 2. Unsaved-changes dialog never worked

`ShowUnsavedDialog` used `result.Show()` which is non-blocking in Avalonia.
The method returned `(false, false)` immediately before any button was clicked.
Clicking [Save], [Don't Save], or [Cancel] had zero effect.

**Should have used:** `ShowDialog<bool>(this)` or an async `TaskCompletionSource`.

### 3. Milestones batched into mega-commits

M1–M3 crammed into one commit (`4ca125b`), M4–M5 into another (`2ff4a01`).
No incremental verification was possible. When M4 was broken, there was no way
to revert just M4–M5 while keeping M1–M3.

**Should have been:** One commit per milestone, each independently testable.

### 4. Plan-required tests never created

`IMPLEMENTATION_PLAN.md` required `EditorTabViewModelTests.cs` with
`OpenFile_CreatesNewTab`, `OpenFile_ActivatesExisting`, `CloseTab_RemovesFromCollection`.
File never existed, but milestones were still marked in-progress.

### 5. Subscription leaks

Multiple `.Subscribe()` calls without `d.Add()` inside `WhenActivated`:
- `EditorTabBar.cs:149` — `CloseTabCommand.Execute(vm).Subscribe()`
- `MainWindow.axaml.cs:74` — `tab.SaveCommand.Execute().Subscribe()`
- `MainWindowViewModel.cs:41` — `OpenFileCommand.Execute().Subscribe()`

### 6. Type safety: `dynamic`

`EditorView.cs` typed `_textMate` as `dynamic?` — disabling all compiler checks.

### 7. No I/O error handling

`EditorViewModel.Save()` called `File.WriteAllText` with no try/catch.
File locked? Permission denied? Silent crash.

### 8. Mixed binding patterns

`EditorView` used both a `TextChanged` event handler AND a `WhenAnyValue`
subscription for the same data flow, because a two-way `Bind` caused a feedback
loop. The workaround was undocumented and confusing.

## Rules Added

- `docs-rules.md` §12a–12j — 10 hard rules enforced by code review:
  - 12a — ViewModels never reference Views (use `Interaction<T,U>`)
  - 12b — Every `.Subscribe()` in `WhenActivated` uses `d.Add()`
  - 12c — One binding pattern per data flow
  - 12d — No `dynamic` in production code
  - 12e — Dialogs are their own `ReactiveWindow`
  - 12f — One milestone per commit
  - 12g — Plan-required tests must exist
  - 12h — All file I/O has error handling
  - 12i — Revert early when code is bad
  - 12j — Verify exit conditions concretely
- `docs-rules.md` §3 — Revert Log Template (this file's template)
- `docs-rules.md` §2 — Trigger: "Reverting a phase → Create REVERT_LOG.md"

## Revert Commit

`36f5e72` — `git reset --hard 0971113` then committed `docs-rules.md` additions
