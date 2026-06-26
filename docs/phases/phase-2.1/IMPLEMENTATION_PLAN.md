# Phase 2.1: Editor Polish — Implementation Plan

## Pre-Implementation Verification

- [ ] `dotnet build Zaide.slnx` passes with 0 warnings (Phase 2 baseline)
- [ ] `dotnet test Zaide.slnx` passes: 69 tests, 0 failures
- [ ] Library understanding confirmed: `IBackgroundRenderer` interface exists in AvaloniaEdit v12 (`AvaloniaEdit.Rendering` namespace), with `Layer` property (`KnownLayer` enum) and `Draw(TextView, DrawingContext)` method. `TextView.BackgroundRenderers` is `IList<IBackgroundRenderer>`. Verified via reflection probe against `AvaloniaEdit.dll` 11.3.0 (transitive dependency of `AvaloniaEdit.TextMate` 12.0.0).
- [ ] Minimal proof-of-concept works: API probe compiled and confirmed `BackgroundGeometryBuilder`, `KnownLayer.Background`, `VisualLine.FirstDocumentLine`, `TextEditorOptions.IndentationSize`, `TextView.VisualLines` all resolve at runtime.
- [ ] Dependencies verified compatible: `AvaloniaEdit.TextMate` 12.0.0 pulls `AvaloniaEdit` ≥11.3.0 which exposes all required rendering APIs. No version conflict.

---

## Scope

**Goal:** Indent guides and small editor refinements deferred from Phase 2.

**Boundaries (NOT building):**
- No brace matching
- No code folding
- No minimap
- No bracket pair colorization
- No multiple selection / multi-cursor

---

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate: Phase 2 clean build + tests | `dotnet build Zaide.slnx && dotnet test Zaide.slnx` |
| M1 | Indent guides via `IBackgroundRenderer` | Visual: vertical dotted lines at indentation levels |

---

### M1: Indent Guides

AvaloniaEdit v12 does not expose `ShowIndentGuides` as a built-in option.
Implement via the `IBackgroundRenderer` interface on the `TextView`:

1. Create `src/Views/IndentGuideRenderer.cs` implementing `IBackgroundRenderer`
   - `Layer` property returns `KnownLayer.Background` (draws behind text)
   - Expose `bool IsEnabled` property — `Draw()` returns early when `false`
   - Expose `static bool ShouldEnableForFile(string filePath)` — returns `true` for extensions with a grammar scope (`.cs`, `.json`), `false` otherwise. Reuses the same extension set as `EditorView.GetGrammarScope()`
2. In `Draw()` (guarded by `IsEnabled`), iterate `textView.VisualLines` (only visible lines — no perf cost),
   for each line: get `visualLine.FirstDocumentLine`, read leading whitespace
   from `textView.Document.GetText(line.Offset, line.Length)`, compute indent
   depth accounting for tab width (`textView.Options.IndentationSize`),
   draw vertical dotted lines at each indentation boundary
3. In `EditorView`:
   - Store renderer as `_indentGuideRenderer` field
   - Register in constructor: `_textEditor.TextArea.TextView.BackgroundRenderers.Add(_indentGuideRenderer);`
   - In `ApplyFileMode()`, add: `_indentGuideRenderer.IsEnabled = IndentGuideRenderer.ShouldEnableForFile(filePath);`

**Considerations:**
- `IBackgroundRenderer` is part of AvaloniaEdit's public API — no fork needed
- Use `VisualLine.FirstDocumentLine` to get the document line, then read leading whitespace from `TextDocument.GetText()`
- Visual column must account for tab width (`TextEditorOptions.IndentationSize`)
- Lines drawn in the `TextView` coordinate system; clip to `textView.Bounds`
- Respect `textView.ScrollOffset` for vertical offset — `VisualLine.VisualTop` already accounts for scroll
- Use a semi-transparent pen color from the app theme (e.g., `TextDisabled` or similar resource) for the dotted lines
- Avoid per-frame allocations: consider caching the `Pen` instance as a static/readonly field
- Extract indent-depth computation into an `internal static` helper so it can be unit-tested without instantiating Avalonia controls

---

## Testing

Extract indent-depth computation into `internal static int GetIndentDepth(string line, int tabWidth)` so unit tests can verify it without Avalonia controls. Add test cases: spaces, tabs, mixed, empty lines, no indentation.

---

## Limitations (by design)

- Indent guides only appear for files with a known TextMate grammar scope (`.cs`, `.json`). Plain text, Markdown, and unknown file types get no guides — matching the "code files only" convention already established by syntax highlighting.
- No dynamic toggle at runtime (e.g., user setting to disable guides). The `IsEnabled` property is controlled by file type only. A user preference toggle is out of scope.
- Tab-width is read from `TextEditorOptions.IndentationSize` (default 4). Mixed tabs+spaces on the same line will compute indent depth correctly but the visual guide position may look off — this is a known limitation of column-based indent guides.

## Exit Conditions

- [ ] `dotnet build Zaide.slnx`: 0 warnings, 0 errors
- [ ] `dotnet test Zaide.slnx`: all passing
- [ ] Indent guides visible when editing indented code (.cs, .json)
- [ ] Guides scroll correctly with the document
- [ ] No guides shown for plain text, Markdown, or unindented files
- [ ] Unit tests for indent-depth computation logic

## Rollback Plan

This phase adds one new file (`src/Views/IndentGuideRenderer.cs`) and one method call in `EditorView` constructor. Revert is a single commit removing both. No database changes, no config changes, no dependency changes.

- **Pre-phase commit:** last commit before Phase 2.1 work starts
- **Files added:** `src/Views/IndentGuideRenderer.cs`, `tests/Zaide.Tests/Views/IndentGuideRendererTests.cs`
- **Files modified:** `src/Views/EditorView.cs` (3 lines: field + constructor + `ApplyFileMode` gate)
- **Revert command:** `git revert <phase-2.1-commits>` or manual removal of the above files/lines

---

*Last updated: 2026-06-26*
