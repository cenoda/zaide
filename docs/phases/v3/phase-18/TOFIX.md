# Phase 18: Live IDE Context for Agent Runs — TOFIX

## Status

M0 planning document created and submitted for review. No milestone
implementation has started.

Phase 18 implementation is unauthorized until M0 is explicitly reviewed and
accepted by the user.

## Current work

- [ ] M0 review and acceptance by user.

## M0 findings (2026-07-26)

User audit returned NO-GO with 8 findings. All addressed in amended plan:

1. **Source-layer contradiction (High):** P18-D02 now identifies three
   verified gaps (Editor, Terminal, Source Control) requiring M1 contract
   seams. Verified live facts table updated with gap annotations.
2. **Shipped-inert semantics (High):** P18-D09 rewritten to define inert as
   "assembled and attached, but not consumed" — matching Phase 17's pattern.
3. **Security boundary (High):** P18-D07 expanded with redaction contract
   table (secret classes, patterns, fail-closed behavior, canonicalization,
   structured data handling). Hard exclusions no longer have an
   explicit-selection escape hatch.
4. **Policy determinism (High):** P18-D05 now includes a source-by-policy
   matrix. `Custom` level removed from Phase 18 scope.
5. **Budget contract (High):** New "Token budget contract" section in Locked
   Phase 18 Contracts defines budget owner/defaults, source priority order,
   truncation behavior, overflow handling, and manifest recording.
6. **Architecture test baseline (Medium):** Confirmed 32 passed, 0 failed.
   Audit's claim of 31 was incorrect.
7. **Rollback plan (Medium):** Corrected to commit-level reversal (Phase 18
   modifies existing types, not just additive files).
8. **Missing TOFIX (Medium):** This file created.

## Residual findings (2026-07-26)

User re-audit after first amendment. 5 residual issues addressed:

1. **Architecture test baseline:** Reconciled. 31 tests in
   `Zaide.Tests.Architecture` namespace + 1 cross-namespace match
   (`Phase17AdversarialCloseoutTests.ArchitectureInventory_...`).
   Authoritative baseline for Phase 18 ratchets: **31**.
2. **Viewport state:** Definitively excluded from Phase 18. No longer
   deferred to M1.
3. **Language ID:** Definitively excluded from Phase 18. No longer
   deferred to M1.
4. **Budget overflow wording:** Added explicit limitation: budget permits
   intentional overflow for oversized highest-priority items. Exit condition
   reworded to verify behavior, not a hard ceiling.
5. **Legacy backend context capability:** Explicitly defined in consumption
   boundary — capability row is `Unavailable`, assembly runs but manifest
   is never read.
6. **Architecture bypass ratchets:** Named test class
   `Phase18ContextBypassRatchetTests` with 3 specific tests and file
   acceptance criteria in required test layers table.

## Next task

- [ ] User reviews amended M0 plan and accepts or returns further findings.

## Scope boundaries observed

M0 planning does not implement production code, tests, UI, backend wiring,
persistence, memory, raw traces, provider-specific prompt tuning, or
Phase 19/20 work.
