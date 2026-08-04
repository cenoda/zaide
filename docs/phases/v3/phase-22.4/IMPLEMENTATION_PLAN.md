# Phase 22.4: Trace / Memory / Usage User Surfaces — Implementation Plan

## Status and Authorization

**M0–M4 are complete.** This sub-phase depends on completed and re-smoked
Phase 22.2. The verified M0 report is
[M0_SEAM_VERIFICATION.md](./M0_SEAM_VERIFICATION.md). G5 and V4 remain
unauthorized.

## A4 Ownership and Dependency

Phase 22.4 owns A4 package 4, BL-09…BL-11, `A1-XX-03`, and affected rows
`A1-TC-02`, `A1-TC-03`, and `A1-TC-08`. It depends on 22.2 because its positive
paths require truthfully bound backend producers.

Baseline evidence:

- [A4 package ledger](../../../audits/v1-v3-product-reality/A4_GAP_REPORT.md#9-corrective-work-required-before-v4-planning)
- [A2 trace/memory/usage evidence](../../../audits/v1-v3-product-reality/evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md)
- [A3 trace/memory/usage preflight](../../../audits/v1-v3-product-reality/evidence/A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md)
- [Phase 21 plan](../phase-21/IMPLEMENTATION_PLAN.md)

## M0 — Live-Seam Verification and Plan Acceptance

- [x] Confirm Phase 22.2 is complete and that Native Harness and ACP producer
  availability is represented independently and truthfully.
- [x] Reconcile current trace, usage, continuity, and memory inspection view
  models plus `AgentTransparencyManagementViewModel` with A3's finding that no
  user-reachable commands/Views existed.
- [x] Verify Townhall/shell entry points, command registration, production DI,
  accessibility, selection, empty, unavailable, failed, and loading states.
- [x] Verify trace inspection, pre-admission redaction, capture-state,
  retention/export, and backend-evidence boundaries from Phase 21.
- [x] Verify usage/cost evidence origin, unit, attribution, pricing source,
  unavailable/estimated/disputed state, and no-default-zero rules.
- [x] Verify memory list/create/correct/disable/supersede/delete, scope,
  provenance, conflict, export/backup, and retrieval/influence separation.
- [x] Confirm user surfaces never write directly to conversation history or
  bypass the owning application coordinators.
- [x] Replace command placeholders and lock rollback, backup, and migration
  handling in the M0 report.
- [x] Record human M0 acceptance and separate M1-only implementation
  authorization under the user's standing GO direction (2026-08-04).

Candidate presentation and application types are planning pointers. M0 must
prove actual user reachability rather than treating DI registration as a user
entry point.

## Scope

**Goal:** Provide backend-neutral, user-reachable inspection and management
surfaces for the trace, durable memory, and usage/cost contracts already owned
by Phase 21.

**Boundaries:** The surface must label missing/unavailable evidence truthfully,
retain provenance and scope, and use existing application owners. Native
Harness and ACP may expose different evidence depths without different product
status semantics or backend-specific management silos.

## Non-Goals

- New trace, memory, usage, pricing, retention, recovery, or storage semantics
  beyond the accepted Phase 21 contracts unless M0 proves a blocking gap and a
  human approves an explicit plan amendment.
- Backend binding (22.2), agent-path tools/termination/recovery (22.3), or debug
  validation (22.5).
- Hidden reasoning or chain-of-thought exposure.
- Treating missing usage/cost as zero, backend reports as invoice facts, or
  persisted conversation history as durable memory.
- Package 9 visual or general settings backlog.

## Milestones

| Milestone | Outcome | Verification gate |
|-----------|---------|-------------------|
| M0 | Dependency, reachability, ownership, redaction, evidence, scope, accessibility, migration, and rollback seams are verified and accepted | [Read-only M0 report](./M0_SEAM_VERIFICATION.md) + recorded human acceptance |
| M1 | Complete — Townhall trace surface, opened-workspace inspection, explicit application-lifetime capture control, and independent Native/ACP evidence hooks | Build; `Phase21Trace*`, `Phase21Redaction*`, `Phase22TraceSurfaceTests`, `Phase22TraceProducerTests`, trace/storage ratchets; full fast suite |
| M2 | Complete — Townhall memory lifecycle surface, opened-workspace list/select/create/correct/disable/supersede/delete, provenance/conflict/influence separation, Loading/Ready/Empty/Unavailable/Failed | Build; `Phase21Memory*`, `Phase22MemorySurfaceTests`, memory ratchet; architecture inventory |
| M3 | Complete — Townhall usage/cost surface, opened-workspace inspection, locked aggregation semantics, explicit capture control, independent Native/ACP truthful producer hooks | Build; `Phase21Usage*`, `Phase21Cost*`, `Phase22UsageSurfaceTests`, `Phase22UsageProducerTests`, usage ratchet; full fast suite |
| M4 | Complete — integrated Townhall commands/View, real accessibility and failure states, backup safety, regression, and dual-backend A3 re-smoke | `Phase22Transparency*`, Phase 21 integration/export/backup, DI/architecture, full suite, isolated transparency smoke |

## Verification Commands

```bash
dotnet build Zaide.slnx --no-incremental
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter 'FullyQualifiedName~Phase21Trace|FullyQualifiedName~Phase21Redaction|FullyQualifiedName~Phase22TraceSurfaceTests|FullyQualifiedName~Phase22TraceProducerTests|FullyQualifiedName~Phase21TraceRatchetTests|FullyQualifiedName~Phase21StorageOwnershipRatchetTests'
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter 'FullyQualifiedName~Phase21MemoryStore|FullyQualifiedName~Phase21MemoryPolicy|FullyQualifiedName~Phase21MemoryLifecycle|FullyQualifiedName~Phase21MemoryRetrieval|FullyQualifiedName~Phase21MemoryInfluence|FullyQualifiedName~Phase22MemorySurfaceTests|FullyQualifiedName~Phase21MemoryRatchetTests'
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter 'FullyQualifiedName~Phase21Usage|FullyQualifiedName~Phase21Cost|FullyQualifiedName~Phase22UsageSurfaceTests|FullyQualifiedName~Phase22UsageProducerTests|FullyQualifiedName~Phase21UsageRatchetTests'
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter 'FullyQualifiedName~Phase22TransparencyReachabilityTests|FullyQualifiedName~Phase22TransparencyAccessibilityTests|FullyQualifiedName~Phase22TransparencyFailureStateTests|FullyQualifiedName~Phase22TransparencyBackupTests|FullyQualifiedName~Phase21TransparencyIntegration|FullyQualifiedName~Phase21Export|FullyQualifiedName~Phase21Backup|FullyQualifiedName~AgentsRegistrationModuleTests|FullyQualifiedName~TownhallRegistrationModuleTests|FullyQualifiedName~Architecture'
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
test -f tests/a3-transparency/runner/Zaide.Tests.csproj
# Publish to /tmp/zaide-a3-transparency and run A1-TC-02,A1-TC-03,A1-TC-08
# once per sibling backend exactly as locked in M0_SEAM_VERIFICATION.md.
git diff --check
```

Every filtered command must discover its named future Phase 22.4 classes and
at least one test. The exact producer command, evidence matrix, and safety
requirements are locked in [M0_SEAM_VERIFICATION.md](./M0_SEAM_VERIFICATION.md#locked-isolated-a3-re-smoke-procedure)
and use the umbrella
[re-smoke contract](../phase-22/IMPLEMENTATION_PLAN.md#re-smoke-contract).

## Exit Conditions

- [x] Phase 22.2 dependency and both approval gates are recorded.
- [x] Trace, memory, and usage/cost surfaces are user-reachable and
  accessibility-tested.
- [x] Phase 21 redaction, evidence, retention, scope, and ownership invariants
  remain protected.
- [x] Focused, build, and suite gates pass.
- [x] `A1-TC-02`, `A1-TC-03`, and `A1-TC-08` have current isolated re-smoke
  evidence for both Native Harness and ACP under
  [evidence/](./evidence/).

## Rollback Note

Prefer one reversible commit per M1–M3 surface and a separate M4 integration
closeout. No schema or data migration is authorized: rollback preserves all
durable Phase 21 partitions and disables/drains the owning capture path before
revert. The current clean-profile backup failure state must be corrected and
tested in M4 before Backup is user-reachable; Restore and Migrate remain
application-only. Any later schema or destructive lifecycle need is a stop
condition requiring an amended backup/downgrade/quarantine/restore plan.
