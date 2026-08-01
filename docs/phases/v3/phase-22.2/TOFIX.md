# Phase 22.2: User Backend-Binding Workflow — TOFIX

## Status

**M0 live-seam verification documented; not implemented.** A4 package 2 remains
assigned here. Human G2 / M0 acceptance and a later, separate implementation
prompt are pending. 22.3 and 22.4 remain dependent and must not start
implementation.

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
- [ ] Record explicit M0 acceptance.
- [ ] Obtain separate implementation approval.
- [ ] Implement and re-smoke only the accepted package 2 scope.

## M0 Findings

- A3 remains accurate: `AgentBackendBindingPanel` is status-only;
  `AgentBackendBindingPresenter` has no production consumer; Townhall pulls one
  snapshot only when display context refreshes.
- Binding state is an app-lifetime `ActorId` dictionary. `SetBinding` replaces;
  remove, revisions, durable load/save, and binding change notifications are
  absent. Restart returns to unbound.
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

## Next Task

Human review of the M0 record is next. Do not begin M1 until M0 is explicitly
accepted and a later prompt authorizes Phase 22.2 M1. Do not implement or
release Phase 22.3, 22.4, 22.5, or V4 work from this gate.
