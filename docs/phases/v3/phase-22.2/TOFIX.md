# Phase 22.2: User Backend-Binding Workflow — TOFIX

## Status

**M0 accepted. M1 + M2 implemented.** A4 package 2 remains assigned here. M1
shipped the durable schema-v1 binding store. M2 shipped the Native Harness
Townhall binding workflow (configure/bind/inspect/unbind with capability
truth). M3 (ACP auth bridge) and M4 (restart/A3 re-smoke) remain next. 22.3
and 22.4 remain dependent and must not start implementation.

## Work Board

- [x] Draft binding, identity, persistence, secret, backend-independence, and
  re-smoke boundaries.
- [x] Reconcile the live binding panel/presenter with A3 user-reachability
  evidence during M0.
- [x] Verify Townhall/settings/command reachability, production DI, binding
  store/selection, persistence, identity, restart, and partial-write seams.
- [x] Lock Native Harness and ACP workflow contracts independently, including
  the real ACP authenticate bridge and capability-gated logout boundary.
- [x] Inventory existing tests; lock exact M1-M4 filters, required new test
  classes, local two-backend re-smoke matrix, and rollback boundaries.
- [x] Record explicit M0 acceptance.
- [x] Obtain separate M1 implementation approval.
- [x] Implement M1 durable schema-v1 store (bind/update/unbind, revisions,
  atomic persist, recovery, reactive change, busy gate).
- [x] Human M1 audit / acceptance (PASS; nits fixed in M2).
- [x] Implement M2 Native Harness Townhall workflow.
- [ ] Implement M3 ACP workflow + authenticate bridge + logout.
- [ ] Implement M4 restart/regression/A3 re-smoke + closeout.

## M0 Findings

- A3 remains accurate: `AgentBackendBindingPanel` is status-only;
  `AgentBackendBindingPresenter` has no production consumer; Townhall pulls one
  snapshot only when display context refreshes.
- Binding state was an app-lifetime `ActorId` dictionary. `SetBinding` replaced;
  remove, revisions, durable load/save, and binding change notifications were
  absent. Restart returned to unbound.
- Native Harness uses shared LLM base URL/model plus env/secret-store API key;
  actor binding and provider configuration are distinct. Six-fact capability
  truth exists internally but is not projected by the binding panel.
- ACP launch uses an absolute executable and ordered arguments, initializes and
  checks expected `agentInfo`, and fails closed on missing/mismatched runtime.
  Production still uses `Environment.CurrentDirectory` as its working-directory
  provider.
- Selection `RequestAuthenticateAsync` is only a local in-memory state rewrite;
  it does not call `IAcpSessionClient.AuthenticateAsync`. Negotiated methods are
  not copied into selection state. `logout` is only a method-name constant.
- Existing tests cover composition, sibling identity, transport, redaction,
  capability, send/routing, and continuity seams, but not the user workflow,
  unbind/persistence, reactive status, auth bridge, or logout.
- Detailed evidence and locked outcomes are in
  [M0_SEAM_VERIFICATION.md](./M0_SEAM_VERIFICATION.md).

## M1 Delivered

- Durable paths (config dir via `SettingsPathResolver`):
  - primary: `agent-backend-bindings.json`
  - temp: `agent-backend-bindings.json.tmp`
  - LKG: `agent-backend-bindings.json.lastknowngood`
- Typed store mutations: `TryBind` / `TryUpdate` / `TryUnbind` with revision
  conflict, busy, validation, and persistence outcomes.
- Active-run busy seam: `IAgentActorActiveRunQuery` on `AgentSessionService`
  (`HasActiveRun(ActorId)`), wired through `LazyAgentActorActiveRunQuery` in
  production DI so the binding store has no constructor cycle.
- Reactive boundary: store and selection publish `BindingChanged` only after
  durable success.
- Runtime auth stays non-durable via `SetRuntimeAuthentication`; idle
  update/unbind clears advertised auth method cache.
- Focused tests:
  `Phase22BackendBindingStoreTests`,
  `Phase22BackendBindingPersistenceTests`,
  `Phase22BackendBindingSelectionServiceTests`.

## M2 Delivered

- Interactive Townhall `AgentBackendBindingPanel`: bind Native Harness, unbind,
  capability caption, Settings guidance, automation names, keyboard-focusable
  actions.
- Production-owned `AgentBackendBindingPresenter` wired into `TownhallViewModel`
  (DI + reactive `BindingChanged`).
- Capability projection via `NativeHarnessCapabilityRows` + options source +
  workspace authority: bound vs unbound, provider configured, workspace
  captured, context-manifest present (honest default absent).
- M1 nits: `TryBind` rejects already-bound and busy; workflow uses
  `TryBind` only when unbound and `TryUpdate`/`TryUnbind` otherwise; advertised
  methods clear on any successful durable mutation; presenter surfaces typed
  mutation errors.
- Focused tests: `Phase22NativeHarnessBindingWorkflowTests`.

## Next Task

Implement M3 ACP Townhall workflow, configuration probe, real
`authenticate(methodId)` bridge, and capability-gated logout. Do not implement
or release Phase 22.3, 22.4, 22.5, or V4 work from this gate. Do not claim
`A1-AC-02` WORKS or package 2 complete until M4 re-smoke.
