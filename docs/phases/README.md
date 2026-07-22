# Phase Plans

Phase implementation plans are grouped by roadmap version. Phase numbers remain
global across versions so historical references stay unambiguous.

## Versions

| Version | Scope | Status |
|---------|-------|--------|
| [`v1/`](v1/) | Original roadmap, Phase 0 through Phase 7.4 | Complete |
| [`v2/`](v2/) | IDE Core Upgrade, Phase 8 through Phase 13 | **Complete** (2026-07-16) — Phase 8–12 feature phases closed; Phase 13 Release Hardening closed with explicit limitations ([M5 evidence](v2/phase-13/M5_RELEASE_CLOSEOUT_EVIDENCE.md)) |
| [`v3/`](v3/) | AI-Native Orchestration, Phase 14 onward | **Phase 14 accepted and closed** (2026-07-21; accepted baseline `67da1394`) — **Phase 15 complete and closed** (2026-07-22) — **Phase 16 M0 explicitly human-accepted** (2026-07-22; [plan](v3/phase-16/IMPLEMENTATION_PLAN.md)); **M1 explicitly human-accepted** (2026-07-23; all-blocked eligibility lock); **M2a explicitly human-accepted** (2026-07-23; runner contract and fake-candidate core); M2b+ unauthorized |

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
candidate eligibility lock (Qwen Code, OpenCode, and Grok Build blocked at M1;
no candidate eligible for later M3 qualification; no comparative or
single-candidate execution path authorized). **M2a was explicitly human-accepted on 2026-07-23** (standalone offline runner contract and fake-candidate core; no production behavior or upstream execution); **M2b and later milestones remain
unauthorized**; Native Harness production and ACP implementation remain
unauthorized.**

## Archive Policy

- Keep completed version folders as historical implementation and closeout records.
- Update archived content only to correct factual errors or broken references.
- Put every new phase plan under the current roadmap version.
- Continue the existing phase numbering instead of restarting at Phase 1.
