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
- [x] `dotnet test Zaide.slnx`: 32 passed, 0 failed
- [ ] Phase 1 → Phase 2 exit conditions all met

---

## Open

### [ ] Editor doesn't show file content after M3
Tab opens and displays filename, editor widget renders, but content area is blank.
**Updated debug (2026-06-26):** The ViewModel path is now covered by
`MainWindowViewModelTests.SelectingSupportedFile_OpensActiveTabWithContent`.
The strongest remaining suspect was center layout: `DockPanel` gave fill space to
`_welcomeText` instead of `_editorView`. Candidate patch applied by replacing the
center `DockPanel` with a two-row `Grid`; manual visual verification still needed
before closing ISSUE-002.

### [ ] Close button (×) visually invisible but clickable
Button is present in layout (clickable, highlight works), but `Opacity = 0` is never
changed to 1. The `PointerEntered`/`PointerExited` events on the tab `Border` may not
fire correctly in the running app. The Opacity-based approach is correct — the events
just aren't reaching the handler.

### [ ] Ctrl+` bottom panel toggle doesn't work
Never verified before M3. May be a keybinding issue — `Key.OemTilde` mapping varies
across keyboard layouts. On Linux with non-US keyboards, the tilde key may not map
to `OemTilde`. The `KeyBindings.Add` in `WhenActivated` may also be adding duplicates
if `WhenActivated` fires multiple times.

---

## Phase 1 Carry-Over

### [ ] Hardcoded colors in `MainWindow.axaml.cs` — agent area + bottom (deferred)
Right agent area and bottom panel still use `BuildPanel` with `Color.Parse`.
Deferred to Phase 5 (agent) and Phase 3 (terminal).
