# Phase 14 M8 — Manual panel retirement evidence

**Milestone:** Retire dedicated Agent Panel shell chrome; Townhall is the sole user-facing DM workflow  
**Date:** 2026-07-21  
**Environment:** Linux desktop; automated retirement + parity suites green; interactive GUI rows **not validated on display** in agent session (same limitation as M3–M7).

## Retirement parity checklist (automated proxy)

| Checklist row | Result |
|---------------|--------|
| Open/select Human↔Agent direct from Townhall navigation | Pass — M7 `Phase14M7ParityBridgeTests` + M8 `DmOnlyWorkflow_*` |
| Send; user + assistant/error on owning conversation only | Pass — M7 direct-send + privacy asserts |
| Busy/disable input per conversation; restore on completion | Pass — M7 `NavigationDuringWork_*`, `IsConversationBusy` |
| Cancel/exception paths truthful | Pass — M7 `ErrorAndCancel_*` |
| Re-send after failure (no retry chrome) | Pass — M7 `ReSend_*` |
| Draft retained per conversation across selection | Pass — M8 `Draft_TownhallAndThinHostShare*` + M7/M5 suites |
| Routing failure visible on source conversation | Pass — M7 `RoutingFailure_*` |
| `@mention` without open target panel tab | Pass — M7 routing tests |
| Restart restores history + draft + unread; no auto LLM | Pass — M6 persistence matrix (unchanged) |
| No implicit public-channel DM copy | Pass — M4 + M7 privacy asserts |
| In-flight work attached to conversation, not panel chrome | Pass — M7 panel-close + navigation tests |
| Agent Panel / right-column chrome removed | Pass — M8 `AgentPanelPresentationViews_*`, `RightColumnHost_*`, `MainWindow_Source*` |
| Panel send seam removed from shell | Pass — M8 `MainWindowViewModel_DoesNotExpose*` |

## Interactive smoke checklist (not run in agent session)

1. Launch at default window size; confirm **no Agent Panel** under the editor (right column is editor-only).
2. Launch at minimum practical width; Townhall nav + center chat + editor remain usable; no orphaned panel tabs.
3. Open DM with Alpha from Townhall People; send; observe user + assistant in Townhall only.
4. While a slow request runs, switch channel and return — busy/input disable reappears for that DM only.
5. Type draft in DM; switch conversation and back — draft retained; restart app — draft/history/unread restore.
6. Force routing failure (`@Ghost`); error visible in owning DM.
7. After error, send again (re-send) — no dedicated Retry control.

## Explicit not-validated rows

| Row | Status |
|-----|--------|
| Interactive GUI on display | **Not run** (agent session) |
| Keyboard focus / a11y polish | Deferred to M9 |
| Narrow / wide / high-DPI screenshots | Deferred to M9 |
| Minimum-window pixel-perfect layout | **Not run** (structural source ratchets only) |

## Residual seams (intentional, non-visual)

- `IAgentPanelHost` / `AgentPanelHost` remain as a **thin execution host** (conversation provisioning, output projection, draft sync). No user-facing panel chrome.
- `AgentExecutionCoordinator` / `AgentRouter` still accept `panelId` as the thin-host lookup key; in-flight state is conversation-keyed (M7 contract preserved).
