# Phase 3.9: Terminal UX Polish — TOFIX

## Status as of M4 closeout (2026-07-07)

No open Phase 3.9 implementation findings. All automated tests pass and the code boundary is clean.

## Pending items (not Phase 3.9 work — carried forward or deferred)

- **Manual Linux smoke unchecked.** The four M0 smoke items (selection/copy, scrollback without live-bottom regression, alternate-screen isolation copy/scrollback block, log/terminal toggle focus), plus the M1/M2/M3-specific smoke checklists, were not executed in a live UI/PTTY session during this phase. These must be verified before Phase 3.9 is declared fully complete from a quality gate standpoint. They are blocked only by running the application interactively on Linux — no code changes are expected to surface from them.
- **Scroll affordance.** M2 planned a "visible but lightweight scroll affordance" for the terminal surface. It was not implemented; viewport math and keyboard/PageUp/PageDown/Home/End navigation are present. If a scrollbar or thumb is desired, it would be a narrow Phase 3.9.x follow-up, not terminal tabs.
- **Copy Visible UX clarity.** M1 planned an explicit `Copy Visible` fallback action to make copy semantics obvious. The current implementation consolidated copy behavior; if the UX review during manual smoke shows ambiguity, a dedicated `Copy Visible` command is the documented remediation.

## Deferred to Phase 3.9.1

All terminal-tab work (multi-session hosting, tab strip, active-tab switching, disposal) remains in `docs/phases/v1/phase-3.9.1/`.
