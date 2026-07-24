# Phase Plans

Phase implementation plans are grouped by roadmap version. Phase numbers remain
global across versions so historical references stay unambiguous.

## Versions

| Version | Scope | Status |
|---------|-------|--------|
| [`v1/`](v1/) | Original roadmap, Phase 0 through Phase 7.4 | Complete |
| [`v2/`](v2/) | IDE Core Upgrade, Phase 8 through Phase 13 | **Complete** (2026-07-16) — Phase 8–12 feature phases closed; Phase 13 Release Hardening closed with explicit limitations ([M5 evidence](v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md)) |
| [`v3/`](v3/) | AI-Native Orchestration, Phase 14 onward | **Phase 14 and Phase 15 closed** — **Phase 16 parked** — **Phase 17 M0 accepted; M1 next**; see each phase `TOFIX.md` for current work state |

Roadmap V2 is complete. Its Phase 8–13 plans remain the historical
implementation record under [`v2/`](v2/). The
[Roadmap V3](../roadmap/V3.md) implementation order is accepted. Refactors
6.1–6.3, 7, and 8 are complete and closed. Phase 14 lives at
[`v3/phase-14/IMPLEMENTATION_PLAN.md`](v3/phase-14/IMPLEMENTATION_PLAN.md)
with closeout evidence in
[`v3/phase-14/M9_MANUAL_EVIDENCE.md`](v3/phase-14/M9_MANUAL_EVIDENCE.md) and F1 evidence
[`v3/phase-14/M9_F1_MANUAL_EVIDENCE.md`](v3/phase-14/M9_F1_MANUAL_EVIDENCE.md).
**Phase 15 is complete and closed (2026-07-22) at
[`v3/phase-15/IMPLEMENTATION_PLAN.md`](v3/phase-15/IMPLEMENTATION_PLAN.md).
[Phase 16](v3/phase-16/IMPLEMENTATION_PLAN.md) is the controlled Native Harness
evaluation phase. Its implementation record and revert log remain in the phase
folder; its current work state is in
[`v3/phase-16/TOFIX.md`](v3/phase-16/TOFIX.md). [Phase
17](v3/phase-17/IMPLEMENTATION_PLAN.md) M0 was accepted on 2026-07-24; M1
contracts and deterministic state are the current bounded task, tracked in
[`v3/phase-17/TOFIX.md`](v3/phase-17/TOFIX.md).

## Archive Policy

- Keep completed version folders as historical implementation and closeout records.
- Update archived content only to correct factual errors or broken references.
- Put every new phase plan under the current roadmap version.
- Continue the existing phase numbering instead of restarting at Phase 1.
