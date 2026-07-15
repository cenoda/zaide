# ISSUE-006: Editor crashes on selection change via GetOffset(line, column)

**Label:** BUG
**Status:** closed
**Priority:** critical
**Related:** Phase 9 M6 selection tracking, `src/Views/EditorView.cs` (outside Phase 13
hardening scope)

## Description

Editing or deleting text in the editor could crash the app with
`System.ArgumentOutOfRangeException` from AvaloniaEdit
`TextDocument.GetOffset(int line, int column)`, thrown inside
`EditorView`'s `TextArea.SelectionChanged` handler.

This was a production crash in the Phase 9 selection-status projection path.
It is **not** Phase 13 performance or recovery work; it is a focused bugfix so
desktop measurement and normal editing can continue.

## Steps to Reproduce

1. Launch `zaide` from the repository root.
2. Open `tests/fixtures/workflow-console/Program.cs`.
3. Edit or delete text in the editor (or clear a selection so the caret-only
   empty selection is reported).

**Expected behavior:** Editor remains open; selection/caret status updates.
**Actual behavior:** Process crashes:

```text
System.ArgumentOutOfRangeException: Value must be between 1 and 3
at AvaloniaEdit.Document.TextDocument.GetOffset(Int32 line, Int32 column)
at Zaide.Views.EditorView.<.ctor>g__OnSelectionChanged|...
```

(The upper bound is the document's `LineCount` at the time of the event.)

## Debug Log

### Attempt 1: Live code + AvaloniaEdit empty-selection semantics
- **Hypothesis:** `OnSelectionChanged` converted `Selection.StartPosition`
  line/column through `GetOffset`, but AvaloniaEdit's `EmptySelection` reports
  `TextLocation.Empty` (line 0, column 0). `GetLineByNumber(0)` throws. The same
  conversion also fails when a replaced/shortened document leaves a stale line
  greater than `LineCount`.
- **Action:** Stopped using line/column for selection reporting. Project from
  offset-based `TextEditor.SelectionStart` / `SelectionLength` (caret offset when
  empty), clamp against `Document.TextLength`, and centralize the logic in
  `EditorView.ProjectSelectionState`. Added
  `EditorSelectionProjectionTests` covering empty selection, valid selection,
  empty document, and replaced-document stale offsets.
- **Result:** Build and focused tests pass; empty-selection and stale-line
  `GetOffset` paths are no longer on the status-projection call path.
- **Error / Output:** Root-cause proof: `doc.GetOffset(0, *)` throws
  `ArgumentOutOfRangeException` with message `Value must be between 1 and N`.

## Resolution

- **Root cause:** Phase 9 M6 selection tracking called
  `Document.GetOffset(selection.StartPosition.Line, Column)`. For an empty
  selection AvaloniaEdit returns `TextLocation.Empty` (line 0). `GetOffset`
  requires a 1-based line in `[1, LineCount]`. Editing/deleting frequently
  fires `SelectionChanged` with an empty selection, which crashed the process.
  Document replace/delete could also leave line/column temporarily out of range.
- **Fix:** Project selection with offset-based APIs only
  (`SelectionStart` / `SelectionLength` / clamped offsets). Never convert empty
  or stale line/column pairs through `GetOffset` for status reporting.
- **Commit:** (recorded when landed)
- **Closed date:** 2026-07-15
