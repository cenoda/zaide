# Phase 3.5: Terminal UI Normalization — TOFIX

Track code quality issues found during Phase 3.5 review.

---

## Open Issues

_(None yet — add entries as issues are discovered during implementation.)_

## Resolved Issues

_(None yet.)_

## Deferred to Future Phases

| Issue | Reason | Target Phase |
|-------|--------|--------------|
| Full ANSI/VT100 rendering | Phase 3.5 only normalizes a small raw-output subset | Future renderer phase |
| Clear-screen sequence (`clear` / Ctrl+L) not honored | M4 leaves all CSI sequences verbatim (Option 1). `clear` emits `\x1B[H\x1B[2J`, which shows as raw text; the toolbar Clear button is the supported substitute. Honoring it properly needs the ANSI renderer. | Future renderer phase |
| Terminal tabs and splits | Single terminal session remains the MVP boundary | Future terminal phase |
| Windows terminal backend | Phase 3.5 keeps Linux MVP scope | Phase 3.1 |
| macOS terminal backend | Phase 3.5 keeps Linux MVP scope | Phase 3.2 |
| **Modifier+arrow/home/end keys** | The M2 `modifiers != KeyModifiers.None` guard intentionally drops Shift+Home/End (selection) and Ctrl+Left/Right (word-jump) to keep modifier combinations available for View-level actions. These are common readline shortcuts that would need explicit escape-sequence mappings in `TerminalKeyMapper` if a use-case appears. | Future key-mapping phase |
| **MeasureCellWidth caches first non-zero result permanently** | If the first `FormattedText` measurement happens before the font resolves and returns a fallback-glyph width, that wrong value sticks for the session. Low risk because the font family and size are static, but worth revisiting if column counts ever look off. | Future terminal-renderer phase |
| **Large paste is not chunked** | `TerminalPanel.PasteAsync` (Ctrl+Shift+V) writes the whole clipboard to the PTY in one `WriteAsync` call. A very large paste could briefly block on the synchronous PTY write. Acceptable for the MVP; chunk the input or write off the UI thread if it becomes a problem. | Future terminal phase |
| **Restart only respawns after exit** | `RestartCommand` is disabled while the shell is running and only starts a new session after a clean exit. There is no kill-and-respawn of a live shell, because the service exposes no `Stop` separate from `Dispose`. Add an explicit stop/restart path if a live restart is needed. | Future terminal phase |
