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
