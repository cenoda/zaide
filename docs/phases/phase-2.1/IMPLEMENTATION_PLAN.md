# Phase 2.1: Editor Polish — Implementation Plan

## Pre-Implementation Verification

- [ ] `dotnet build Zaide.slnx` passes with 0 warnings (Phase 2 baseline)
- [ ] `dotnet test Zaide.slnx` passes: 69 tests, 0 failures

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
2. In `Draw()`, iterate `textView.VisualLines` (only visible lines — no perf cost),
   for each line: get `visualLine.FirstDocumentLine`, read leading whitespace
   from `textView.Document.GetText(line.Offset, line.Length)`, compute indent
   depth accounting for tab width (`textView.Options.IndentationSize`),
   draw vertical dotted lines at each indentation boundary
3. Register renderer in `EditorView` constructor:
   ```csharp
   _textEditor.TextArea.TextView.BackgroundRenderers.Add(new IndentGuideRenderer(_textEditor));
   ```

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

## Exit Conditions

- [ ] `dotnet build Zaide.slnx`: 0 warnings, 0 errors
- [ ] `dotnet test Zaide.slnx`: all passing
- [ ] Indent guides visible when editing indented code (.cs, .json)
- [ ] Guides scroll correctly with the document
- [ ] No guides shown for plain text / unindented files
- [ ] Unit tests for indent-depth computation logic

---

*Last updated: 2025-06-26*
