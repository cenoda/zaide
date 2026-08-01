# Phase 22.4: Trace / Memory / Usage User Surfaces — Implementation Plan

## Status and Authorization

**Planning only; not implemented.** This sub-phase depends on completed and
re-smoked Phase 22.2. M0 is not accepted. Implementation requires explicit
human M0 acceptance and a separate implementation approval.

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

- [ ] Confirm Phase 22.2 is complete and that Native Harness and ACP producer
  availability is represented independently and truthfully.
- [ ] Reconcile current trace, usage, continuity, and memory inspection view
  models plus `AgentTransparencyManagementViewModel` with A3's finding that no
  user-reachable commands/Views existed.
- [ ] Verify Townhall/shell entry points, command registration, production DI,
  accessibility, selection, empty, unavailable, failed, and loading states.
- [ ] Verify trace inspection, pre-admission redaction, capture-state,
  retention/export, and backend-evidence boundaries from Phase 21.
- [ ] Verify usage/cost evidence origin, unit, attribution, pricing source,
  unavailable/estimated/disputed state, and no-default-zero rules.
- [ ] Verify memory list/create/correct/disable/supersede/delete, scope,
  provenance, conflict, export/backup, and retrieval/influence separation.
- [ ] Confirm user surfaces never write directly to conversation history or
  bypass the owning application coordinators.
- [ ] Replace command placeholders, lock rollback and migration handling, and
  receive explicit human M0 acceptance.

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
| M0 | Dependency, reachability, ownership, redaction, evidence, scope, accessibility, migration, and rollback seams are verified; plan accepted | Read-only checklist + human acceptance |
| M1 | User can inspect redacted trace records and explicit capture/retention/evidence states | Focused trace application/presentation tests |
| M2 | User can inspect and manage scoped durable memory through existing lifecycle contracts | Focused memory lifecycle/presentation tests |
| M3 | User can inspect usage and cost evidence with origin, units, attribution, pricing, and unavailable states preserved | Focused usage/cost application/presentation tests |
| M4 | Integrated reachability, accessibility, regression, and affected A3 re-smoke gates pass | Build, fast/serial suites, isolated transparency smoke |

## Verification Command Placeholders

```bash
dotnet build Zaide.slnx
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<Phase21 trace plus presentation filter>"
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<Phase21 memory plus presentation filter>"
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<Phase21 usage/cost plus presentation filter>"
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<Townhall/shell accessibility and architecture filter>"
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
<out-of-tree A3 trace/memory/usage producer with disposable HOME/XDG/workspace>
git diff --check
```

M0 must replace placeholders and use the umbrella
[re-smoke contract](../phase-22/IMPLEMENTATION_PLAN.md#re-smoke-contract).

## Exit Conditions

- [ ] Phase 22.2 dependency and both approval gates are recorded.
- [ ] Trace, memory, and usage/cost surfaces are user-reachable and
  accessibility-tested.
- [ ] Phase 21 redaction, evidence, retention, scope, and ownership invariants
  remain protected.
- [ ] Focused, build, and suite gates pass.
- [ ] `A1-TC-02`, `A1-TC-03`, and `A1-TC-08` have current isolated re-smoke
  evidence.

## Rollback Note

Prefer one reversible commit per M1–M3 surface and a separate integration
closeout. Rollback must preserve durable Phase 21 records and schema
compatibility. If M0 admits schema or migration work, it must define exact
backup, downgrade, quarantine, and restore procedures before implementation.
