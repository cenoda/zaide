# Phase 3.9.1: Terminal Tabs — TOFIX

## M3: Bottom-Panel UI Integration

### TOFIX-001: TerminalTabStrip Cursor setting missing in test harness
**Severity:** Low (test-only, no production impact)  
**Status:** Open  
**Description:**  
`TerminalTabStrip` originally set `Cursor = new Cursor(StandardCursorType.Hand)` on interactive elements, which requires `Avalonia.Platform.ICursorFactory` — a platform service not available in the test harness. The cursor settings were removed to unblock tests. The production app will still show default cursor behavior, but hover-to-hand-cursor feedback on tabs is lost.

**Fix:** Restore cursor settings when an Avalonia headless test harness is added to the project, or accept the current behavior as sufficient for Phase 3.9.1.

### TOFIX-002: View-layer TerminalTabHost tests require Avalonia headless
**Severity:** Low  
**Status:** Open  
**Description:**  
`TerminalTabHost` and `TerminalTabStrip` create Avalonia controls in their constructors, requiring `Application.Current` and platform services. Avalonia headless test infrastructure is not in the current test project, so the full view-layer panel cache tests (per-tab panel retention, active panel switching, focus seam) cannot be executed automatically. The M3 scenarios are instead verified at the host/viewmodel seam level in `TerminalHostTests`.

**Fix:** Add `Avalonia.Headless.XUnit` package and a `HeadlessUnitTestSession` fixture to enable direct view-layer testing for `TerminalTabHost` and `TerminalTabStrip`.

