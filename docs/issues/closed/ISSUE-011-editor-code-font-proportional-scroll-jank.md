# ISSUE-011: C# editor slow-motion when code font is not monospaced

**Label:** BUG  
**Status:** closed  
**Priority:** high  
**Related:** Separate from Phase 22.1 UI projection Post/coalesce and from
ISSUE-010 indent-guide paint cost.

## Description

Scrolling or caret movement on large `.cs` files felt slow-motion / multi-hundred
milliseconds per step. Markdown editing stayed smooth. A Phase 22.1 Post/coalesce
hot fix did not remove the symptom. ISSUE-010 optimized indent-guide painting
and remains necessary for `.cs`-only guide cost, but on the current tree the
remaining severe jank tracked the **code font family**, not guides or LSP.

## Steps to Reproduce

1. Set **Settings → Code Font Family** to a non-monospaced or legacy bitmap face
   that AvaloniaEdit still applies (repro on this host: `B&H LucidaBright`).
2. Leave **Prose Font Family** as a normal monospaced or light face
   (e.g. `Adwaita Mono`).
3. Open a large, multi-level indented `.cs` file (~10k+ lines).
4. Move the caret repeatedly (Page Down, click-to-position, or
   navigate-to-offset) or scroll while the caret is updated.
5. Open a large `.md` file and perform the same navigation.

**Expected:** Responsive caret/scroll on both.  
**Actual:** `.cs` steps take ~500–800 ms each (slow-motion); `.md` stays ~10 ms.

## Hypothesis confirmation (current HEAD)

| Hypothesis | Result |
|------------|--------|
| Phase 22.1 projection `Invoke` / Post coalesce primary cause | Ruled out for this residual — Post/coalesce already on HEAD; `.md` fine |
| Indent guides (`IndentGuideRenderer`) primary residual | Ruled out for residual — optimized path ~0.03 ms/draw after ISSUE-010; guides disabled for `.md` |
| Show whitespace alone | Ruled out as primary — mono + whitespace still ~2 s for 80 navigations |
| **Code font not monospaced / pathological face on `.cs` only** | **Proven** — `B&H LucidaBright` ~15.7 s / 30 caret navigations; Cascadia stack / Adwaita Mono / Noto Sans Mono ~0.34–0.37 s |

Why `.cs` vs `.md`: `EditorView.ApplyFileMode` selects **code** font for
non-markdown and **prose** font for `.md`. User settings can make those faces
radically different.

## Resolution

- **Root cause:** Live `TextEditor.FontFamily` for code used the settings code
  font string as-is. A proportional or legacy face (e.g. X11 bitmap
  `B&H LucidaBright`) makes AvaloniaEdit caret/selection layout extremely
  expensive on large documents, so `.cs` feels slow-motion while `.md` (prose
  font) does not.
- **Fix:** `CodeFontResolver` walks the code font stack and applies the first
  **fixed-pitch** family (`FontManager` / `Metrics.IsFixedPitch`), else
  `monospace`. Wired through `EditorView.ProjectSettings` so all apply paths
  share the resolution. Settings still store the user’s preferred name; the
  live code editor refuses non-mono faces.
- **Not changed:** Indent guides (ISSUE-010), language projection Post/coalesce
  (Phase 22.1), Phase 22.2+ scope.
- **Tests:** `CodeFontResolverTests`; settings projection tests updated for mono
  fallback; architecture inventory +1 internal type/file.

## Manual verification

1. With code font set to `B&H LucidaBright` (or any proportional face), open a
   large `.cs` file and Page Down / caret-move — should feel comparable to mono
   defaults (not multi-hundred-ms steps).
2. Large `.md` still smooth.
3. With a real monospaced code font (e.g. Adwaita Mono / JetBrains Mono), editor
   still uses that face.
4. Indent guides still draw on `.cs` only; language Ready completion/hover smoke
   still works when LSP is available.

## Closed date

2026-08-02
