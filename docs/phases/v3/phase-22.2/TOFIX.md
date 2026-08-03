# Phase 22.2: User Backend-Binding Workflow — TOFIX

## Status

**Phase 22.2 complete; package-2 PASS restored against the live ACP baseline.**
M0–M4 delivered the durable schema-v1 store, Native Harness and ACP Townhall
workflows, restart gates, and out-of-tree A3 re-smoke. A subsequent full
package-2 audit found a blocking ACP runtime-invalidation defect. Corrective
implementation closed that defect and residual epoch/cache TOCTOU gaps.
Historical package-2 evidence previously cited HEAD `d4a0f34d`; intervening
commit `9c4bb94f` then hardened `AcpStdioProcessHost` process-exit cancellation
before `dfe2bf14`. Independent lifecycle review of that delta found no product
defect. Targeted ACP `A1-AC-02` evidence was re-smoked against live HEAD
`dfe2bf14` and package-2 PASS is restored on that baseline.

### Phase and package status

| Item | Status |
|------|--------|
| 22.1 Language intelligence / LSP | **Complete** (separate package-1 closeout) |
| 22.2 User backend-binding workflow | **Package PASS restored** against live ACP baseline `dfe2bf14` |
| Phase 22 critical path | **In progress** |
| 22.3 Agent-path enablement | **Pending** — separate authorization required |
| 22.4 Trace / memory / usage surfaces | **Pending** — separate authorization required |
| G5 full affected re-smoke | **Blocked** — packages 3–7 and 22.3/22.4 still open |
| V4 / successor-roadmap planning | **Blocked** — G5 not passed; separate human decision required |

### Evidence refresh and re-audit (green)

| Gate | Result |
|------|--------|
| Corrective commits through `d4a0f34d` (historical read-only review) | PASS — binding epoch, fingerprint, onboarding connection, advertised-method cache, authenticate/logout, disposal race |
| Intervening ACP host lifecycle delta `d4a0f34d`…`9c4bb94f` (independent review) | PASS — process-exit cancellation, sticky terminal states, exit-over-timeout classification, dispose races; no product defect |
| Retained A3 runner rebuild (producer unchanged) | PASS |
| ACP `A1-AC-02` re-smoke (disposable HOME/XDG, production DI, Townhall controls, repo ACP fake) | **16/16 pass**, classification **WORKS**, RepoHead `dfe2bf14` |
| `dotnet build Zaide.slnx --no-incremental` | 0 warnings, 0 errors |
| Phase22 binding filter | **71/71** passed |
| Full suite fast (`dotnet test Zaide.slnx --no-build`) | **3849/3849** passed |
| Serial fallback (`slow.runsettings`) | Not required (fast suite green) |
| `git diff --check` | clean |

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
- [x] Corrective-only: atomic probe-start snapshot for launch; empty-method
  fail-closed before invalid-method mutation; deterministic initialize/
  record/clear race tests; architecture source-file baselines 866/821.
- [x] Targeted ACP `A1-AC-02` evidence refresh and independent package-2
  re-audit — **package PASS restored**.
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
- [x] Corrective-only residual finish: single atomic probe-start snapshot
  (launch runtime derived only from captured fingerprint; no independent
  `TryGetBinding` on the launch path); empty advertised-method list rejected
  before the invalid-method Failed mutation; deterministic concurrency tests
  via `InitializeDelayAsync` and selection-lock race seams for record/clear;
  architecture production source-file baselines updated to **866** total /
  **821** Features.
- [x] Targeted ACP `A1-AC-02` evidence refresh against HEAD `d4a0f34d` and
  independent package-2 re-audit (historical PASS at that head).
- [x] Live-baseline provenance restore: independent review of intervening
  `AcpStdioProcessHost` lifecycle changes through `9c4bb94f`, retained A3
  producer rebuild (unchanged), ACP `A1-AC-02` re-smoke against live HEAD
  `dfe2bf14` — **16/16 WORKS**. Evidence:
  [evidence/A1-AC-02-acp.json](./evidence/A1-AC-02-acp.json).

## Independent package-2 re-audit (PASS)

Historical read-only review of corrective commits `e8ccd1c0`…`d4a0f34d` against
live code at that head:

1. **Binding epoch** — durable bind/update/unbind bumps a monotonic per-actor
   epoch; probe/auth publication validates the capture-time pair.
2. **Fingerprint** — `AcpRuntimeBindingFingerprint` is a genuine snapshot
   (defensive argument copy, content equality); launch runtime is derived only
   from the probe-start fingerprint.
3. **Onboarding connection** — `OnBindingChanged` detaches cached clients on
   bind/update/unbind; authenticate/logout revalidate fingerprint+epoch before
   protocol use.
4. **Advertised-method cache** — validate-and-publish under selection lock
   (selection → store lock order); empty lists fail closed before invalid-method
   mutation; conditional clear compares stored fingerprint+epoch.
5. **Authenticate / logout** — real `IAcpSessionClient` protocol path; failure
   and binding-changed paths never rewrite replacement runtime state.
6. **Disposal race** — detach starts tracked disposal immediately; probe and
   auth await tracked disposals before reusing actor connections.

No corrective-only finding set remains open for package 2. Dependent A3 rows
stay **WORKS_WITH_FRICTION** under 22.3/22.4 ownership and are not upgraded here.

### Live-baseline provenance restore (2026-08-03)

Independent review of production ACP host changes from `d4a0f34d` through
`9c4bb94f` concentrated on process-exit cancellation linked into op timeouts,
sticky `ProcessExited`/`Disposed` lifecycle ordering, initialize/operation
failure classification that prefers process-exit over timeout, and dispose/
event-handler races on `_processExitCts`. Interaction with
`AcpOnboardingConnectionService` probe/auth paths remains fail-closed earlier
on child exit. No product defect was found. ACP `A1-AC-02` was re-smoked against
live HEAD `dfe2bf14abec719bf3774aa2538d4ee911f4f7d0` with production
`Program.ConfigureServices`, shipped Townhall binding controls, disposable
HOME/XDG/workspace roots, and the repository ACP fake agent: **16/16**,
classification **WORKS**, producer source unchanged.

## Remaining (not Phase 22.2)

- Phase 22.3: send/routing outcome polish, tools/permissions positive paths
  (pending separate authorization).
- Phase 22.4: context/trace/memory/usage surfaces (pending separate
  authorization).
- Phase 22.5 / V4 / G5: not authorized from this package; G5 and V4 remain
  blocked.
