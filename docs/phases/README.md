# Phase Plans

Phase implementation plans are grouped by roadmap version. Phase numbers remain
global across versions so historical references stay unambiguous.

## Versions

| Version | Scope | Status |
|---------|-------|--------|
| [`v1/`](v1/) | Original roadmap, Phase 0 through Phase 7.4 | Complete |
| [`v2/`](v2/) | IDE Core Upgrade, Phase 8 through Phase 13 | In progress — Phase 8 (umbrella), Phase 9 (Editor UX), Phase 10 (C# LSP), Phase 11 (Project Workflow), and Phase 12 (C# Debugging DAP) complete; Phase 13 next |

Roadmap V2 planning has started. Phase 8 umbrella plan is complete at
[`v2/phase-8/IMPLEMENTATION_PLAN.md`](v2/phase-8/IMPLEMENTATION_PLAN.md).
Sub-phase 8.1 (Settings Foundation) is **complete** across its five
implementation slices (M1–M6 closeout 2026-07-11). Phase 8.2 (Command Registry
and Keybindings) is **complete** across its eight milestones (M7a–M10 closeout
2026-07-12); all seven canonical commands, gesture resolution, window binding
materialization, and settings-driven refresh are delivered. Phase 8.3 is
implemented through M4 with automated verification green; its bounded
implementation plan at
[`v2/phase-8/phase-8.3/IMPLEMENTATION_PLAN.md`](v2/phase-8/phase-8.3/IMPLEMENTATION_PLAN.md)
records the completed failed-state GUI smoke verification.

## Archive Policy

- Keep completed version folders as historical implementation and closeout records.
- Update archived content only to correct factual errors or broken references.
- Put every new phase plan under the current roadmap version.
- Continue the existing phase numbering instead of restarting at Phase 1.
