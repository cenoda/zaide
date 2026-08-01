# Phase 22.1 — UI projection Post + dual-slot coalesce

Short evidence note for the post-M4 responsiveness hot fix. Does not replace
or rewrite historical A0–A3 evidence under `evidence/`.

## Problem

`EditorLanguageUiProjection` marshaled every language snapshot with
`IEditorUiDispatcher.Invoke` → Avalonia `Dispatcher.UIThread.Invoke`
(synchronous). Under scroll/caret and language activity (including hover
Idle on every caret move, and Ready after `ConfigureAwait(false)`), blocking
Invoke could stall publishers and contribute to UI slow-motion.

## Fix

| Surface | Change |
| --- | --- |
| `IEditorUiDispatcher` | Added `Post(Action)` for non-blocking UI work |
| `AvaloniaEditorUiDispatcher` | `Post` → always `Dispatcher.UIThread.Post` |
| `EditorLanguageUiProjection` | `Post` + dual-slot coalesce (predecessor + latest) |
| `WorkspaceEditorDocumentReconciler` | Unchanged — still uses synchronous `Invoke` |
| `LanguageHoverService` | Skips redundant Idle→Idle `OnNext` |
| Tests | Projection Post/coalesce/terminal-then-idle; test dispatchers implement `Post` |

**Dual-slot (not pure single-slot latest-wins):** `PublishTerminal` emits a
feedback terminal (Empty/Failed/…) then Idle on the same publisher stack.
Single-slot latest-wins would drop the terminal and lose status-bar feedback
(A1-FN-11). Dual-slot keeps at most one superseded predecessor plus the
latest; identical floods (repeated Idle) still collapse to one apply.

Thread-affinity for A1-FN-09…13 is preserved: apply still runs only via the
editor UI dispatcher, never raw off-thread `Subscribe(Apply*)`.

## Verification (this fix)

- `dotnet build Zaide.slnx` — succeeded
- Focused: `EditorLanguageUiProjectionTests`,
  `EditorLanguageInputRoutingTests`, language completion/hover/navigation/
  symbol/command-availability (+ Phase17 reconciler compile surface) —
  77 passed
- Fast suite: `dotnet test Zaide.slnx --no-build` — 3748 passed
- `git diff --check` — clean
- Out-of-tree A3 re-smoke (`/tmp/zaide-a3-lang/`) for `A1-FN-09`…`A1-FN-13`:
  all `WORKS` / exit 0. Fresh JSON only under
  `/tmp/zaide-a3-lang/evidence/`; historical
  `docs/phases/v3/phase-22.1/evidence/A1-FN-*.json` not rewritten.

## Manual check (optional)

On a Ready C# file: rapid scroll remains responsive; completion, hover,
definition, and symbols still work.
