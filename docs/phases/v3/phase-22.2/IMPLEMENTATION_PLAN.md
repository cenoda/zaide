# Phase 22.2: User Backend-Binding Workflow — Implementation Plan

## Status and Authorization

**Planning only; not implemented.** M0 is not accepted. Implementation is not
authorized until explicit human M0 acceptance and a separate implementation
approval are recorded.

## A4 Ownership and Dependency

Phase 22.2 owns A4 package 2, BL-06, `A1-XX-01`, and the positive backend-
binding path of `A1-AC-02`. It has no dependency and may proceed in parallel
with 22.1. Phases 22.3 and 22.4 depend on its completed implementation and
local affected-row re-smoke.

Baseline evidence:

- [A4 package ledger](../../../audits/v1-v3-product-reality/A4_GAP_REPORT.md#9-corrective-work-required-before-v4-planning)
- [A2 backend-onboarding evidence](../../../audits/v1-v3-product-reality/evidence/A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)
- [A3 backend-onboarding evidence](../../../audits/v1-v3-product-reality/evidence/A3_AGENT_CREATION_AND_BACKEND_ONBOARDING.md)

## M0 — Live-Seam Verification and Plan Acceptance

- [ ] Reconcile the current `AgentBackendBindingPanel` and
  `AgentBackendBindingPresenter` with A3's finding that no supported user
  configure/bind/unbind/persist workflow was reachable.
- [ ] Verify Townhall reachability, selection state, empty/unbound state,
  keyboard/focus/accessibility bounds, and production DI.
- [ ] Verify `IAgentActorBackendBindingStore`,
  `IAgentActorBackendSelectionService`, current in-memory/persistence truth,
  Actor/workspace/backend/runtime identity rules, and revalidation boundaries.
- [ ] Verify Native Harness configuration inputs and capability truth without
  inventing provider or model support.
- [ ] Verify ACP executable/runtime configuration, initialize/authenticate/
  logout support, credential ownership, and current `authenticate` bridge
  behavior.
- [ ] Define supported secret storage and ensure ordinary settings/logs never
  receive plaintext credentials.
- [ ] Define bind, update, unbind, restart, stale-runtime, disconnect, auth
  failure, and partial-write outcomes.
- [ ] Inventory focused production-composition and integration tests, then
  replace command placeholders.
- [ ] Lock migration/rollback behavior and receive explicit human M0
  acceptance.

Candidate seams are planning pointers only. M0 must verify their current
reachability and ownership before any implementation decision.

## Scope

**Goal:** Provide a supported user workflow to configure, bind, inspect,
persist, revalidate, and unbind either a Native Harness or ACP backend for an
agent, with explicit identity and honest capability/availability state.

**Boundaries:** Native Harness and ACP remain independent sibling backends. The
workflow may share backend-neutral presentation and binding contracts, but it
must not hide backend-specific configuration, authentication, runtime, or
capability differences.

## Non-Goals

- Agent send/routing outcome projection, tools/permissions execution,
  termination, or interrupted-run smoke; those belong to 22.3.
- Trace, memory, or usage management; those belong to 22.4.
- Native Harness fallback for ACP or ACP fallback for Native Harness.
- Provider entitlement assumptions, automatic credential discovery, or
  plaintext secret persistence.
- Restoring the retired Agent Panel or implementing the historical `A1-AC-01`
  workflow.

## Milestones

| Milestone | Outcome | Verification gate |
|-----------|---------|-------------------|
| M0 | Live reachability, ownership, persistence, secret, identity, backend-specific, and rollback contracts are verified; plan accepted | Read-only checklist + human acceptance |
| M1 | Backend-neutral binding configuration and durable/revalidated state contract supports truthful bind/update/unbind outcomes | Focused binding store/service and persistence tests |
| M2 | Native Harness workflow is user-reachable and reports configured/available/usable state truthfully | Focused Native Harness UI/composition tests |
| M3 | ACP workflow is user-reachable, including supported authentication and explicit failure/logout behavior | Focused ACP UI/auth/composition tests |
| M4 | Restart/revalidation, accessibility, regression, and affected A3 re-smoke gates pass | Build, fast/serial gates, isolated binding smoke |

## Affected Re-Smoke

The local closeout must re-run `A1-AC-02` and the newly reachable backend-bound
sub-paths of `A1-AS-02`, `A1-TH-05`, `A1-MR-03`, `A1-TC-01`, and
`A1-TP-01`…`A1-TP-03`. Dependent gaps remain owned by 22.3/22.4; this smoke
records reachability and honest remaining outcomes rather than claiming those
later packages complete.

## Verification Command Placeholders

```bash
dotnet build Zaide.slnx
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<binding store/selection/persistence filter>"
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<Native Harness binding UI/composition filter>"
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<ACP binding/auth/UI/composition filter>"
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
<out-of-tree A3 backend-binding producer with disposable HOME/XDG/workspace>
git diff --check
```

M0 must replace every placeholder. Re-smoke follows the umbrella
[contract](../phase-22/IMPLEMENTATION_PLAN.md#re-smoke-contract).

## Exit Conditions

- [ ] M0 and implementation approvals are recorded separately.
- [ ] A user can configure, bind, inspect, restart/revalidate, and unbind both
  backend types through supported product entry points.
- [ ] Secrets, identity, capability, availability, authentication, disconnect,
  and failure states remain explicit and truthful.
- [ ] Focused, build, and suite gates pass.
- [ ] The affected local A3 re-smoke is recorded.
- [ ] 22.2 completion is explicitly recorded before 22.3 or 22.4 begins.

## Rollback Note

Binding persistence must be backward-compatible or use an M0-approved migration
with backup and restore. Revert UI, persistence, and composition changes as one
coherent milestone when partial rollback could leave unreadable or unsafe
binding state. Never remove or reinterpret an existing binding silently.
