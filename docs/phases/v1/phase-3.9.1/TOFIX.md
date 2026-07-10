# Phase 3.9.1: Terminal Tabs — TOFIX

## Resolved Items

### TOFIX-001: TerminalTabStrip Cursor setting missing in test harness
**Severity:** Low  
**Status:** Resolved (2026-07-08)  
**Fix applied:** `CursorHelper.TryCreateHand()` helper added to `TerminalTabStrip.cs`. The method catches `InvalidOperationException` when `ICursorFactory` is unavailable (test environments without headless platform) and returns `null`. In production, the Hand cursor is set correctly. The three interactive elements (tab item borders, close buttons, new-tab button) all use the helper.

### TOFIX-002: View-layer TerminalTabHost tests require Avalonia headless
**Severity:** Low  
**Status:** Re-evaluated (2026-07-08) — not actionable with current test infra  
**Description:**  
Full view-layer tests for `TerminalTabHost` (panel cache retention, active panel switching, focus seam) would require the Avalonia headless platform, which needs STA-thread `AppBuilder` initialization and `ICursorFactory`/`AvaloniaActivationForViewFetcher` registration. Adding this infrastructure is disproportionately heavy for the two remaining low-severity gaps.

**Current coverage:**  
- Cursor behavior: fixed via `CursorHelper.TryCreateHand()`  
- Panel cache, active-tab switching, focus seam: verified at the host/ViewModel seam level in `TerminalHostTests` (565 tests pass)  
- Tab strip rendering/interaction: verified through the existing `MainWindowViewModelTests` integration surface  

**Decision:** Accept the current test coverage as sufficient. If a general-purpose Avalonia headless test infrastructure is added for a future phase, `TerminalTabHost`/`TerminalTabStrip` view-layer tests should be added at that point.

