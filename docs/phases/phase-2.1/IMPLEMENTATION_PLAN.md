# Phase 2.1: Editor Polish — Implementation Plan

## Pre-Implementation Verification

- [ ] `dotnet build Zaide.slnx` passes with 0 warnings (current Phase 2 baseline)
- [ ] `dotnet test Zaide.slnx` passes with all tests green
- [ ] Read `docs/phases/phase-2.1/TOFIX.md`
- [ ] Read `docs/phases/phase-2.1/REVERT_LOG.md`
- [ ] Verify against live code that `EditorView` is back to the pre-Phase-2.1 baseline
- [ ] Confirm the exact visual target with a real sample file before writing renderer logic

---

## Scope

**Goal:** Retry indent guides with much smaller milestones and visual proof gates.

**Boundaries (NOT building):**
- No brace matching
- No code folding
- No minimap
- No bracket pair colorization
- No multi-cursor or multiple selection
- No user setting or runtime toggle for indent guides
- No editor polish unrelated to indent guides

---

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate: clean baseline and visual target confirmed | `dotnet build Zaide.slnx && dotnet test Zaide.slnx` |
| M1 | Rendering spike: prove a custom background renderer can draw one known vertical marker in the correct place | Visual check in live editor |
| M2 | Coordinate spike: prove leading whitespace can be mapped to stable X positions for one real `.cs` file | Visual check with spaces, tabs, and mixed indentation |
| M3 | First-guide implementation: render only the first indent level correctly | Visual check in live editor while scrolling |
| M4 | Multi-level guides: extend from one level to all levels only after M3 is stable | Visual check across nested code blocks |
| M5 | File-type gating, cleanup, and focused tests | Build, tests, and manual verification |

---

### M1: Rendering Spike

Create the smallest possible experiment to verify the drawing surface before
implementing indent logic.

1. Add a temporary background renderer prototype in `EditorView` or a throwaway
   renderer file.
2. Draw one obvious vertical marker at a fixed X position.
3. Open a real `.cs` file and confirm:
   - the marker is visible
   - the marker scrolls with the text correctly
   - the marker is clearly on the editor text layer, not the window background

**Exit gate:** If the marker placement or scrolling is wrong, stop here and do
not proceed to indentation math.

---

### M2: Coordinate Spike

Prove that the editor exposes coordinates we can trust for leading whitespace.

1. Use one real sample file with:
   - spaces-only indentation
   - tabs-only indentation
   - mixed tabs and spaces
2. Instrument the prototype to draw markers only at the first indentation
   boundary found on each visible line.
3. Confirm visually whether the marker matches the true indent boundary.
4. Document the coordinate source used by the successful spike directly in code
   comments and in `TOFIX.md` if it still fails.

**Exit gate:** Do not proceed unless one boundary can be placed correctly and
repeatably in the live editor.

---

### M3: First-Guide Implementation

Turn the successful coordinate spike into minimal production code.

1. Create `src/Views/IndentGuideRenderer.cs` implementing `IBackgroundRenderer`
   only after M2 succeeds.
2. Render just the first indent guide level.
3. Keep the renderer disabled by default except for the active experiment path.
4. Add only the helper logic needed for the first guide level.

**Exit gate:** The first guide must look correct in nested C# code and remain
aligned while scrolling vertically.

---

### M4: Multi-Level Guides

Extend the renderer only after a single guide level is proven visually.

1. Add support for multiple indentation levels.
2. Verify nested blocks line up correctly.
3. Re-check mixed whitespace cases before treating the feature as complete.

**Exit gate:** Multi-level guides must be visually correct enough to ship, not
just “close.”

---

### M5: File-Type Gating, Cleanup, and Tests

Only after rendering is trustworthy:

1. Register the renderer in `EditorView`.
2. Gate it to supported code file types only.
3. Add focused helper tests for any non-UI indentation logic.
4. Remove temporary spike code and comments that are no longer needed.

**Exit gate:** Final implementation is clean, limited in scope, and backed by
manual verification plus targeted tests.

---

## Testing

Use two kinds of verification:

1. Automated:
   - `dotnet build Zaide.slnx`
   - `dotnet test Zaide.slnx`
   - Focused unit tests only for pure helper logic
2. Manual:
   - Open a real `.cs` file with nested blocks
   - Verify spaces-only indentation
   - Verify tabs-only indentation
   - Verify mixed indentation
   - Verify vertical scrolling keeps guides aligned
   - Verify unsupported file types do not show guides

Manual verification is the primary gate. Automated tests are necessary but not
sufficient for this phase.

---

## Limitations (by design)

- This phase only succeeds if the visual result is trustworthy in the live
  editor. “Mostly aligned” is a failure.
- If mixed whitespace cannot be rendered correctly with the chosen approach,
  document that limitation explicitly before shipping or revert again.
- If AvaloniaEdit does not expose stable enough coordinates, stop and record the
  constraint instead of forcing a weak implementation through.

## Exit Conditions

- [ ] `dotnet build Zaide.slnx` succeeds with 0 warnings and 0 errors
- [ ] `dotnet test Zaide.slnx` succeeds
- [ ] First indent guide level proven visually before multi-level work begins
- [ ] Multi-level guides look correct in a real `.cs` file
- [ ] Guides scroll correctly with the document
- [ ] No guides shown for unsupported file types
- [ ] Temporary spike code removed
- [ ] Helper tests added only for pure non-UI logic

## Rollback Plan

Rollback must stay cheap.

- Keep each milestone in its own commit
- Do not batch spike code and final production code into one commit
- If M1 or M2 fails, revert that milestone immediately instead of patching forward
- If M3 fails visually, revert to the last successful spike commit
- If M4 fails, keep the single-guide implementation only if it is independently acceptable; otherwise revert

---

*Last updated: 2026-06-26*
