# Phase Plans

Phase implementation plans are grouped by roadmap version. Phase numbers remain
global across versions so historical references stay unambiguous.

## Versions

| Version | Scope | Status |
|---------|-------|--------|
| [`v1/`](v1/) | Original roadmap, Phase 0 through Phase 7.4 | Complete |
| [`v2/`](v2/) | IDE Core Upgrade, Phase 8 through Phase 13 | **Complete** (2026-07-16) — Phase 8–12 feature phases closed; Phase 13 Release Hardening closed with explicit limitations ([M5 evidence](v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md)) |
| [`v3/`](v3/) | AI-Native Orchestration, Phase 14 onward | **Phase 14 accepted and closed** (2026-07-21; accepted baseline `67da1394`) — **Phase 15 complete and closed** (2026-07-22) — **Phase 16 M0/M1/M2a/M2b complete**; Qwen Code eligible for later observational M3 qualification; latest M3 smoke **NO-GO** (`m3q-20260724T054307Z-481ad1de`: rename verified, `qwen_exit=55` wall; not qualified) |

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
[Phase 16 M0](v3/phase-16/IMPLEMENTATION_PLAN.md) was explicitly human-accepted
on 2026-07-22 for controlled Native Harness evaluation infrastructure and
campaign. **M1 was explicitly human-accepted on 2026-07-23** with an all-blocked
initial eligibility lock. Its **human-accepted M1 amendment** subsequently made
**Qwen Code eligible for later M3 qualification on a single-candidate
observational path only**; OpenCode and Grok Build remain blocked at M1, and no
comparative or quality claim is authorized. **M2a was explicitly human-accepted
on 2026-07-23** (standalone offline runner contract and fake-candidate core; no
production behavior or upstream execution). **M2b was completed and accepted on
2026-07-23** (repository-owned isolation, lifecycle, mutation, cancellation, and
cleanup evidence; no production behavior, DI, public types, upstream acquisition,
network access, or real candidate execution). M3a/egress/DNS gates completed; auth/12-turn/write-capable remediations complete;
**latest M3 qualification smoke** (`m3q-20260724T054307Z-481ad1de`) **NO-GO**:
write-capable yolo verified TC-T01 rename but `qwen_exit=55` (60s wall); spend
balance delta USD 0.00; candidate still **not qualified**
(`M3_QUALIFICATION_EVIDENCE.md`).

## Archive Policy

- Keep completed version folders as historical implementation and closeout records.
- Update archived content only to correct factual errors or broken references.
- Put every new phase plan under the current roadmap version.
- Continue the existing phase numbering instead of restarting at Phase 1.
