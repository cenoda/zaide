# Phase 22.2: User Backend-Binding Workflow — TOFIX

## Status

**Phase 22.2 M0–M4 delivered.** A subsequent full package-2 audit found a
blocking ACP runtime-invalidation defect (cached onboarding connections
surviving bind/update/unbind). Corrective implementation and focused regression
tests address that defect; targeted ACP `A1-AC-02` evidence refresh and
independent package re-audit remain pending before package-level PASS is
restored.

A follow-up corrective pass also closes the remaining ACP epoch/cache TOCTOU
gaps: probe-start fingerprint+epoch preservation across exact unbind/rebind,
conditional invalid-method failure, advertised-method cache lost-update
races, conditional cache invalidation, and genuine fingerprint snapshots.
The package-level `A1-AC-02` evidence refresh and independent re-audit
remain pending.

22.3 and 22.4 remain dependent and must not start from this gate as "already
done."

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
- [x] Implement M3 ACP workflow + authenticate bridge + logout.
- [x] Implement M4 restart/regression/A3 re-smoke + closeout.
- [x] Corrective: ACP onboarding connection invalidation on durable
  bind/update/unbind; binding-revision/identity validation before
  authenticate/logout; fail-closed empty advertised-method authenticate;
  focused regression tests (`Phase22AcpRuntimeInvalidationTests`).
- [x] Corrective: close remaining epoch/cache TOCTOU gaps (probe-start
  fingerprint+epoch preservation, conditional invalid-method mutation,
  advertised-method cache lost-update races, conditional cache
  invalidation, genuine fingerprint snapshots); focused regression tests
  (`Phase22AcpEpochCacheTocTouTests`).
- [ ] Targeted ACP `A1-AC-02` evidence refresh and independent package-2
  re-audit (package PASS not restored).
- Next critical work remains **Phase 22.3** when separately authorized.

## Remaining (not Phase 22.2)

- Phase 22.3: send/routing outcome polish, tools/permissions positive paths.
- Phase 22.4: context/trace/memory/usage surfaces.
- Phase 22.5 / V4 / G5: not authorized from this package.

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

## M3 Delivered

- ACP Townhall bind fields (absolute executable, non-secret args, expected
  name/version) with secrets ownership caption (no ACP credential field).
- `AcpOnboardingConnectionService`: workspace-rooted cwd (fail closed),
  launch + initialize + agentInfo verify without prompt session, publish
  negotiated auth methods, real `IAcpSessionClient.AuthenticateAsync` bridge,
  capability-gated logout protocol path, clear local runtime only.
- Production ACP factories use `AcpWorkspaceWorkingDirectory` instead of
  `Environment.CurrentDirectory`.
- Selection `RequestAuthenticateAsync` resolves onboarding lazily and calls
  the real bridge in production.
- Focused tests: `Phase22AcpBindingWorkflowTests`,
  `Phase22AcpAuthenticationBridgeTests`.

## M4 Delivered

- Restart/revalidation tests: durable rehydrate only; no auth zombies; unbind
  sticks (`Phase22BackendBindingRestartTests`).
- Townhall reactive/accessibility/both-backends surface tests
  (`Phase22BackendBindingTownhallTests`).
- Out-of-tree A3 re-smoke at `/tmp/zaide-a3-backend-binding/` with retained
  evidence under [evidence/](./evidence/).
- A1-AC-02 **WORKS** for both backends; dependent rows **WORKS_WITH_FRICTION**
  with honest 22.3/22.4 residual ownership.
- Closeout: [CLOSEOUT.md](./CLOSEOUT.md).

## Post-closeout audit fixes

- [x] F1–F4 post-M4 non-blocking audit findings addressed (docs drift, logout
  capability gate via `agentCapabilities.auth.logout` with auth-methods
  fallback, authenticate fails closed without onboarding bridge, ACP config
  row hidden when Native Harness is active).
- [x] Corrective: ACP onboarding connection invalidation on durable
  bind/update/unbind; binding-revision/identity validation before
  authenticate/logout; fail-closed empty advertised-method authenticate;
  focused regression tests (`Phase22AcpRuntimeInvalidationTests`).
- [x] Corrective: remaining ACP epoch/cache TOCTOU gaps closed
  (probe-start fingerprint+epoch preservation across exact unbind/rebind;
  conditional invalid-method mutation via `TrySetRuntimeAuthenticationIfFingerprintMatches`;
  advertised-method cache atomic validate-and-publish under selection lock;
  conditional cache invalidation that compares against the stored
  fingerprint+epoch; defensive argument snapshots in `AcpRuntimeIdentity`
  and `AcpRuntimeBindingFingerprint`); focused regression tests
  (`Phase22AcpEpochCacheTocTouTests`).
- [ ] Targeted ACP `A1-AC-02` evidence refresh and independent package-2
  re-audit (package PASS not restored).
- Next critical work remains **Phase 22.3** when separately authorized.

## Remaining (not Phase 22.2)

- Phase 22.3: send/routing outcome polish, tools/permissions positive paths.
- Phase 22.4: context/trace/memory/usage surfaces.
- Phase 22.5 / V4 / G5: not authorized from this package.
