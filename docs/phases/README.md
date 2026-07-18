# Phase Plans

Phase implementation plans are grouped by roadmap version. Phase numbers remain
global across versions so historical references stay unambiguous.

## Versions

| Version | Scope | Status |
|---------|-------|--------|
| [`v1/`](v1/) | Original roadmap, Phase 0 through Phase 7.4 | Complete |
| [`v2/`](v2/) | IDE Core Upgrade, Phase 8 through Phase 13 | **Complete** (2026-07-16) — Phase 8–12 feature phases closed; Phase 13 Release Hardening closed with explicit limitations ([M5 evidence](v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md)) |

Roadmap V2 is complete. Its Phase 8–13 plans remain the historical
implementation record under [`v2/`](v2/). The
[Roadmap V3](../roadmap/V3.md) implementation order is accepted. No V3 phase
plan exists and Phase 14 implementation is not active. Refactor 6.1 is closed,
and Refactor 6.2 M1–M12 is accepted closed; optional M13 root admissions are
declined. Refactor 6.3 M0 is **accepted** at
[`../refactor/refactor-6.3/IMPLEMENTATION_PLAN.md`](../refactor/refactor-6.3/IMPLEMENTATION_PLAN.md);
**M1** is complete at `e590a79`, **M2** at `d9799ad`, **M3** at `22b869e`
(manual terminal smoke not run), **M4** at `698b094` (manual agent-panel
routing smoke not run), **M5** at `273cc56` (manual verification not
required), **M6a** at `c59ad7b` (AppCore DI registration module; first
completed M6 slice; automated verification green; manual verification not
required), **M6b** at `43b8e85` (Settings DI registration module; second
completed M6 slice; automated verification green; manual verification not
required), **M6c** at `1ad3625` (Workspace DI registration module; third
completed M6 slice; automated verification green; manual verification not
required), and **M6d** at `234a38f` (Editor DI registration module; fourth
completed M6 slice; automated verification green; manual verification not
required). **Refactor 6.3 M1–M5 and M6a–M6k are complete** as individually
completed slices; M6k is complete at `df262ac` (`AddZaideDebugging`), and the
M6 series is complete. **M7** is complete at `554552f` (composition-root store;
public `App.Services` removed; internal `CompositionRoot.Services` residual
retained). **M8** is complete at `874aa79` (ordered shutdown owner / V12), and
**M9a** is complete at `172f2a3` (Agent send / Townhall mirror extraction), and
**M9b** is complete at `33a1806` (panel navigation extraction). Production work
still requires separate authorization for each implementation milestone;
**M9c** is next eligible and has not started.

## Archive Policy

- Keep completed version folders as historical implementation and closeout records.
- Update archived content only to correct factual errors or broken references.
- Put every new phase plan under the current roadmap version.
- Continue the existing phase numbering instead of restarting at Phase 1.
