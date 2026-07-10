# Phase 2.1: Editor Polish — Implementation Plan

## Pre-Implementation Verification

- [x] `dotnet build Zaide.slnx` passes with 0 warnings (2026-06-27)
- [x] `dotnet test Zaide.slnx` passes with all tests green (87/87 on 2026-06-27)
- [ ] Read `docs/phases/v1/phase-2.1/TOFIX.md`
- [ ] Read `docs/phases/v1/phase-2.1/REVERT_LOG.md`
- [x] Verify against live code what Phase 2.1 state actually exists
- [x] Decide whether to fully revert the M2 spike or treat the current spike as the M2 baseline
- [ ] Confirm the exact visual target with a real sample file before writing renderer logic

### Live Repo State Snapshot (2026-06-27)

The repository now contains the cleaned-up M4 multi-level implementation.

- `src/Views/SpikeIndentGuideRenderer.cs` has been removed
- `src/Views/IndentGuideRenderer.cs` now implements the multi-level renderer
- `src/Views/IndentGuideMetrics.cs` contains the pure indentation helper logic
- `EditorView` enables the renderer only for the current C# experiment path
- Focused helper tests exist in `tests/Zaide.Tests/Views/IndentGuideMetricsTests.cs`
- Blank lines intentionally remain disconnected in Phase 2.1

Manual visual validation was completed for M3 on 2026-06-27 using a dedicated
sample file covering spaces, tabs, mixed indentation, blank lines, and deeper
nesting. M4 visual validation was also completed on 2026-06-27.

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

1. Promote the proven parts of the spike into `IndentGuideRenderer`.
2. Create `src/Views/IndentGuideRenderer.cs` implementing `IBackgroundRenderer`
   only after M2 succeeds visually in the live app.
3. Define the first-guide rule before coding:
   - draw one guide only for lines that reach at least one full indent level
   - document how tabs, mixed whitespace, and blank lines are handled
4. Render just the first indent guide level.
5. Keep the renderer disabled by default except for the active experiment path.
6. Add only the helper logic needed for the first guide level.

**Exit gate:** The first guide must look correct in nested C# code and remain
aligned while scrolling vertically.

### M3 Readiness Notes

M3 was hard because it was not just a file rename from
`SpikeIndentGuideRenderer` to `IndentGuideRenderer`.

- The old spike marked the first non-whitespace boundary on each visible line,
  which is not automatically the same as "render the first indent guide"
- M3 needs a clear production rule for lines with less than one indent level,
  blank lines, and mixed tab/space prefixes
- `TextView.GetVisualPosition(...)` appears to be the key coordinate source in
  the spike, but visual trust must still be confirmed while scrolling in a real
  `.cs` file before treating it as production-ready
- The final M3 experiment hookup in `EditorView` is intentionally limited to
  C# files until the live visual checks pass

---

### M4: Multi-Level Guides

Extend the renderer only after a single guide level is proven visually.

1. [x] Add support for multiple indentation levels.
2. [x] Verify nested blocks line up correctly.
3. [x] Re-check mixed whitespace cases before treating the feature as complete.

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

- [x] `dotnet build Zaide.slnx` succeeds with 0 warnings and 0 errors
- [x] `dotnet test Zaide.slnx` succeeds
- [x] First indent guide level proven visually before multi-level work begins
- [x] Multi-level guides look correct in a real `.cs` file
- [x] Guides scroll correctly with the document
- [x] No guides shown for unsupported file types
- [x] Temporary spike code removed
- [x] Helper tests added only for pure non-UI logic

## Rollback Plan

Rollback must stay cheap.

- Keep each milestone in its own commit
- Do not batch spike code and final production code into one commit
- If M1 or M2 fails, revert that milestone immediately instead of patching forward
- If M3 fails visually, revert to the last successful spike commit
- If M4 fails, keep the single-guide implementation only if it is independently acceptable; otherwise revert

---

*Last updated: 2026-06-26*
