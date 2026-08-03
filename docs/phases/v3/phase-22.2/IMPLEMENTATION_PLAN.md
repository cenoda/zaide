# Phase 22.2: User Backend-Binding Workflow — Implementation Plan

## Status and Authorization

**Phase 22.2 complete; package-2 PASS restored against the live ACP baseline.**
M0–M4 delivered durable store, Native Townhall workflow, M3 ACP
probe/authenticate/logout bridge, and M4 restart/A3 re-smoke closeout. A
post-closeout full package-2 audit identified a blocking runtime-invalidation
defect; corrective implementation through HEAD `d4a0f34d` closed it and residual
epoch/cache TOCTOU gaps (historical package PASS head). Intervening
`AcpStdioProcessHost` lifecycle hardening (`9c4bb94f`) was independently reviewed
with no product defect; targeted ACP `A1-AC-02` evidence was re-smoked against
live HEAD `dfe2bf14` (16/16 **WORKS**). See [CLOSEOUT.md](./CLOSEOUT.md).

Phase 22 critical path remains in progress. Phase 22.3–22.5, G5, and V4 remain
unauthorized from this package and still require separate human decisions.

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

- [x] Reconcile the current `AgentBackendBindingPanel` and
  `AgentBackendBindingPresenter` with A3's finding that no supported user
  configure/bind/unbind/persist workflow was reachable.
- [x] Verify Townhall reachability, selection state, empty/unbound state,
  keyboard/focus/accessibility bounds, and production DI.
- [x] Verify `IAgentActorBackendBindingStore`,
  `IAgentActorBackendSelectionService`, current in-memory/persistence truth,
  Actor/workspace/backend/runtime identity rules, and revalidation boundaries.
- [x] Verify Native Harness configuration inputs and capability truth without
  inventing provider or model support.
- [x] Verify ACP executable/runtime configuration, initialize/authenticate/
  logout support, credential ownership, and current `authenticate` bridge
  behavior.
- [x] Define supported secret storage and ensure ordinary settings/logs never
  receive plaintext credentials.
- [x] Define bind, update, unbind, restart, stale-runtime, disconnect, auth
  failure, and partial-write outcomes.
- [x] Inventory focused production-composition and integration tests, then
  replace command placeholders.
- [x] Lock migration and rollback behavior.
- [x] Receive explicit human G2 / M0 acceptance.

Detailed live findings, outcome contracts, test inventory, and the locked
re-smoke producer are in
[M0_SEAM_VERIFICATION.md](./M0_SEAM_VERIFICATION.md). The verified production
names are current. M0 corrected the earlier assumption that the binding status
panel and local selection auth API formed a user workflow: the panel is
status-only, and local `RequestAuthenticateAsync` does not call ACP
`authenticate`.

## M1 — Durable schema-v1 binding store

- [x] Schema-v1 DTO + serializer/validator under Agents.
- [x] Path resolver under the Zaide configuration directory (same roots as
  settings via `SettingsPathResolver`):
  - primary: `agent-backend-bindings.json`
  - temp: `agent-backend-bindings.json.tmp`
  - LKG: `agent-backend-bindings.json.lastknowngood`
- [x] Durable store with atomic temp → LKG backup → replace primary writes.
- [x] Typed bind/update/unbind mutation results, revisions, conflict rejection.
- [x] Busy rejection for update/unbind via `IAgentActorActiveRunQuery`
  implemented by `AgentSessionService.HasActiveRun(ActorId)` and resolved
  lazily through `LazyAgentActorActiveRunQuery` in production DI.
- [x] Reactive `BindingChanged` events after durable success only (store and
  selection).
- [x] Startup load + corrupt/unknown-schema recovery (fail closed; LKG when
  valid; no silent rewrite).
- [x] Idle update clears runtime auth/capability cache (advertised methods +
  auth state). Runtime auth remains in-memory only via
  `SetRuntimeAuthentication`.
- [x] Focused M1 tests and composition/identity preservation gates.
- [x] Human M1 audit / acceptance (PASS; nits fixed in M2).

M1 delivered the durable schema-v1 store and selection/store contracts. The
user-facing Townhall configure workflows shipped in M2/M3. Package 2 positive
`A1-AC-02` **WORKS** (both backends) is claimed only via retained re-smoke
evidence under [evidence/](./evidence/) and [CLOSEOUT.md](./CLOSEOUT.md) —
not from this M1 block alone.

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
| M1 | A schema-v1 durable backend-neutral store with revisions, atomic bind/update/unbind, recovery, and reactive state supports truthful mutation outcomes | Focused binding store/service and persistence tests — **done** |
| M2 | Native Harness workflow is user-reachable and reports configured/available/usable state truthfully | Focused Native Harness UI/composition tests — **done** |
| M3 | ACP workflow is user-reachable, including runtime identity probe, real `authenticate` bridge, capability-gated logout, and explicit failure behavior | Focused ACP UI/auth/composition tests — **done** |
| M4 | Restart/revalidation, accessibility, regression, and affected A3 re-smoke gates pass | Build, fast/serial gates, isolated binding smoke — **done** |

## Affected Re-Smoke

The local closeout must re-run `A1-AC-02` and the newly reachable backend-bound
sub-paths of `A1-AS-02`, `A1-TH-05`, `A1-MR-03`, `A1-TC-01`, and
`A1-TP-01`…`A1-TP-03`. Dependent gaps remain owned by 22.3/22.4; this smoke
records reachability and honest remaining outcomes rather than claiming those
later packages complete.

## Verification Commands

M0 verified the existing class filters with `--no-build --list-tests`. The
exact `Phase22*` classes below are required additions in their owning later
milestone; they do not exist at M0.

### M0 existing composition and identity seams

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.App.Composition.AgentsRegistrationModuleTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Integration.Phase20IntegrationTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Integration.Phase20IdentityBindingTests"
```

### M1 binding state and persistence

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22BackendBindingStoreTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22BackendBindingPersistenceTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22BackendBindingSelectionServiceTests"
```

### M2 Native Harness workflow

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22NativeHarnessBindingWorkflowTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Phase19IntegrationTests|FullyQualifiedName~Zaide.Tests.Features.Settings.Infrastructure.SecretStoreTests|FullyQualifiedName~Zaide.Tests.Features.Settings.Infrastructure.FileSecretStorePermissionTests|FullyQualifiedName~Zaide.Tests.App.Composition.AgentsRegistrationModuleTests"
```

### M3 ACP workflow and authentication

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22AcpBindingWorkflowTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22AcpAuthenticationBridgeTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Protocol.Phase20ProtocolCapabilityTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Transport.Phase20TransportLifecycleTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Transport.Phase20TransportStderrBoundaryTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Integration.Phase20IdentityBindingTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Phase20AdversarialTests"
```

### M4 restart, accessibility, and preservation

```bash
dotnet build Zaide.slnx
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22BackendBindingTownhallTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22BackendBindingRestartTests|FullyQualifiedName~Zaide.Tests.Features.Townhall.Presentation.Phase15TownhallParityTests|FullyQualifiedName~Zaide.Tests.Features.Townhall.Presentation.TownhallDirectSendTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Continuity.Phase21RestartTests"
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
git diff --check
```

Run the fast suite interactively and use the serial command if it fails or
hangs. The exact out-of-tree M4 producer and its two-backend eight-row scenario
matrix are locked in
[M0_SEAM_VERIFICATION.md](./M0_SEAM_VERIFICATION.md#locked-local-re-smoke-set).
Re-smoke follows the umbrella
[contract](../phase-22/IMPLEMENTATION_PLAN.md#re-smoke-contract).

## Exit Conditions

- [x] M0 and implementation approvals are recorded separately (M0 accepted;
  M1–M4 implementation authorized and completed).
- [x] A user can configure, bind, inspect, restart/revalidate, and unbind both
  backend types through supported product entry points (Townhall panel).
- [x] Secrets, identity, capability, availability, authentication, disconnect,
  and failure states remain explicit and truthful.
- [x] Focused, build, and suite gates pass for M1–M4.
- [x] The affected local A3 re-smoke is recorded under `evidence/` and
  [CLOSEOUT.md](./CLOSEOUT.md).
- [x] 22.2 completion is explicitly recorded before 22.3 or 22.4 begins.
  Package-2 PASS restored after corrective re-audit. A1-AC-02 WORKS both
  backends (ACP evidence refreshed at `d4a0f34d`); dependent rows remain
  WORKS_WITH_FRICTION for 22.3/22.4-owned residuals. G5 and V4 remain blocked.

## Rollback Note

Use one independently revertible commit per accepted milestone. M1 owns the
additive schema-v1 binding document and its atomic/LKG store; M2 owns Native
Harness Townhall workflow; M3 owns ACP runtime/auth/logout workflow; M4 owns
restart/revalidation, regression, re-smoke evidence, and closeout docs.

Pre-22.2 code ignores the additive binding file, so rollback preserves it as
recoverable user data rather than deleting or rewriting it. Revert only the
owning Phase 22.2 milestone. Never remove or reinterpret an existing binding
silently, and never roll back Phase 22.1 or historical Phase 19-21 commits to
undo Phase 22.2.
