# A2 Wiring Audit — `A2_AGENT_CREATION_AND_BACKEND_ONBOARDING`

**Audit name:** `v1-v3-product-reality`
**Slice:** `A2_AGENT_CREATION_AND_BACKEND_ONBOARDING` (sixth A2 slice; prior:
`A2_AGENT_SEND`, `A2_MULTI_AGENT_ROUTING`,
`A2_TRACE_MEMORY_USAGE_TERMINATION`,
`A2_RESTART_RECOVERY_AND_CONTEXT`,
`A2_TOOLS_PERMISSIONS`)
**Evidence date:** 2026-07-31
**Baseline:** branch `master`, HEAD
`726c163395b1970f941ec14f54f607b64bfd2b5a` (matches `origin/master`)
**Method:** read-only source inspection (`rg`, file reads). No app launch,
build, test execution, production-code edits, commits, or pushes.

---

## 1. Audit identity, baseline, and safety boundary

| Item | Value |
|------|-------|
| Audit | `v1-v3-product-reality` (see [AUDIT_PLAN.md](../AUDIT_PLAN.md)) |
| Slice name | `A2_AGENT_CREATION_AND_BACKEND_ONBOARDING` |
| Prior A2 slices | [A2_AGENT_SEND.md](./A2_AGENT_SEND.md), [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md), [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./A2_TRACE_MEMORY_USAGE_TERMINATION.md), [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md), [A2_TOOLS_PERMISSIONS.md](./A2_TOOLS_PERMISSIONS.md) |
| Goal rows to verdict | `A1-AC-01`, `A1-AC-02` (per [GOAL_MATRIX.md §10](../GOAL_MATRIX.md#10-agent-creation-and-backend-onboarding)) |
| Scoped disposition only | `A1-XX-01` (not a user-goal verdict; per [GOAL_MATRIX.md §15](../GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior)) |
| Phase sources | [Phase 5 plan](../../../phases/v1/phase-5/IMPLEMENTATION_PLAN.md), [Phase 14 plan](../../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md), [Phase 19 plan](../../../phases/v3/phase-19/IMPLEMENTATION_PLAN.md), [Phase 20 plan](../../../phases/v3/phase-20/IMPLEMENTATION_PLAN.md), Phase 20 M2–M6 evidence |
| Deferred / issues | [DF-008](../../../deferred/open/DF-008-multiple-agent-connections.md), [DF-009](../../../deferred/open/DF-009-real-acp-integrations.md), [ISSUE-008](../../../issues/open/ISSUE-008-agent-response-not-showing.md) |
| Verdict categories | `Wired`, `Wired-with-gap`, `Missing`, `Ambiguous` (per [AUDIT_PLAN.md §2](../AUDIT_PLAN.md#2-audit-phase-decomposition)) |
| Method constraint | Inspection only; no production-code edits, no test edits, no app launch, no build, no test execution, no A3 smoke, no real user profile, no secret-value inspection, no agent backend process execution, no commit or push |

### Baseline and safety confirmation

| Check | Result |
|-------|--------|
| Branch | `master` |
| `git rev-parse HEAD` | `726c163395b1970f941ec14f54f607b64bfd2b5a` |
| `git rev-parse origin/master` | `726c163395b1970f941ec14f54f607b64bfd2b5a` |
| Working tree at start | Clean (`## master...origin/master`) |
| Prior five A2 evidence files present | Yes |
| This slice evidence file at start | Absent (created by this slice) |
| Production code modified | No |
| Tests modified | No |
| `AUDIT_PLAN.md` / `GOAL_MATRIX.md` modified | No |
| Issue / deferred-finding files modified | No |
| Real user profile read/written | No |
| Secret values inspected | No |
| App launched | No |
| Build or tests run | No |
| External backend / A3 smoke | No |
| Native Harness / ACP process execution | No |

---

## 2. Sources inspected

### Audit and goal documents

- [docs-rules.md](../../../../docs-rules.md)
- [AUDIT_PLAN.md](../AUDIT_PLAN.md)
- [GOAL_MATRIX.md](../GOAL_MATRIX.md) (§10, §15, §17.5 progress table)
- [A1_ACCEPTANCE.md](../A1_ACCEPTANCE.md)
- Prior evidence: [A2_AGENT_SEND.md](./A2_AGENT_SEND.md), [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md), [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./A2_TRACE_MEMORY_USAGE_TERMINATION.md), [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md), [A2_TOOLS_PERMISSIONS.md](./A2_TOOLS_PERMISSIONS.md)
- [DF-008](../../../deferred/open/DF-008-multiple-agent-connections.md), [DF-009](../../../deferred/open/DF-009-real-acp-integrations.md)
- [ISSUE-008](../../../issues/open/ISSUE-008-agent-response-not-showing.md)

### Phase documents (corroboration only)

- [Phase 5 IMPLEMENTATION_PLAN.md](../../../phases/v1/phase-5/IMPLEMENTATION_PLAN.md)
- [Phase 14 IMPLEMENTATION_PLAN.md](../../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md) (M8 retirement, D17)
- [Phase 19 IMPLEMENTATION_PLAN.md](../../../phases/v3/phase-19/IMPLEMENTATION_PLAN.md)
- [Phase 20 IMPLEMENTATION_PLAN.md](../../../phases/v3/phase-20/IMPLEMENTATION_PLAN.md) and M2–M6 evidence under `docs/phases/v3/phase-20/`

### Production code (verdict authority)

| Area | Paths |
|------|-------|
| Binding store / selection | [AgentActorBackendBindingStore.cs](../../../../src/Features/Agents/Application/AgentActorBackendBindingStore.cs), [AgentActorBackendSelectionService.cs](../../../../src/Features/Agents/Application/AgentActorBackendSelectionService.cs), [IAgentActorBackendSelectionService.cs](../../../../src/Features/Agents/Contracts/IAgentActorBackendSelectionService.cs), [IAgentActorBackendBindingStore.cs](../../../../src/Features/Agents/Contracts/IAgentActorBackendBindingStore.cs), [AgentActorBackendBinding.cs](../../../../src/Features/Agents/Domain/AgentActorBackendBinding.cs), [AgentActorBackendBindingSnapshot.cs](../../../../src/Features/Agents/Domain/AgentActorBackendBindingSnapshot.cs) |
| Binding presentation | [AgentBackendBindingPresenter.cs](../../../../src/Features/Agents/Presentation/AgentBackendBindingPresenter.cs), [AgentBackendBindingPanel.cs](../../../../src/Features/Agents/Presentation/AgentBackendBindingPanel.cs) |
| Townhall surface | [TownhallView.cs](../../../../src/Features/Townhall/Presentation/TownhallView.cs), [TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs), [TownhallPeoplePanel.cs](../../../../src/Features/Townhall/Presentation/TownhallPeoplePanel.cs) |
| Actor catalog | [ActorCatalog.cs](../../../../src/Features/Conversations/Application/ActorCatalog.cs), [IActorCatalog.cs](../../../../src/Features/Conversations/Contracts/IActorCatalog.cs), [CanonicalActorSeeds.cs](../../../../src/Features/Conversations/Application/CanonicalActorSeeds.cs) |
| Panel host (dormant chrome) | [AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs), [IAgentPanelHost.cs](../../../../src/Features/Agents/Presentation/IAgentPanelHost.cs), [RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs) |
| DI composition | [AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs), [TownhallServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs) |
| Execution / backends | [AgentExecutionCoordinator.cs](../../../../src/Features/Agents/Application/AgentExecutionCoordinator.cs), [AgentExecutionService.cs](../../../../src/Features/Agents/Infrastructure/AgentExecutionService.cs), [NativeHarnessProviderOptionsSource.cs](../../../../src/Features/Agents/Infrastructure/NativeHarnessProviderOptionsSource.cs), [NativeHarnessAgentBackend.cs](../../../../src/Features/Agents/Infrastructure/NativeHarnessAgentBackend.cs), [AcpProductionSessionClientFactory.cs](../../../../src/Features/Agents/Application/Acp/AcpProductionSessionClientFactory.cs), [AcpRuntimeIdentity.cs](../../../../src/Features/Agents/Domain/AcpRuntimeIdentity.cs) |
| Settings / secrets boundary (source only) | [SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs), [SettingsViewModel.cs](../../../../src/Features/Settings/Presentation/SettingsViewModel.cs), [SettingsPanelView.cs](../../../../src/Features/Settings/Presentation/SettingsPanelView.cs) |

### Tests (corroboration only — not production reachability)

- Binding setup helpers under `tests/Zaide.Tests/Features/Agents/` (`SetBinding`, identity/integration fixtures)
- DI module inventory: `tests/Zaide.Tests/App/Composition/AgentsRegistrationModuleTests.cs`

---

## 3. Two-row verdict table

| id | Verdict | One-line basis |
|----|---------|----------------|
| `A1-AC-01` | **Missing** | Historical Phase 5 “add agent panel / dedicated panel surface” path is retired from the shell; production has no user-reachable create/rename/remove/configure-agent command and no dedicated Agent Panel chrome. |
| `A1-AC-02` | **Wired-with-gap** | Native Harness and ACP are independently composed sibling backends with in-memory per-actor binding, pull-based status projection, and (for Native Harness) shared LLM settings/options; the user cannot initiate bind/configure/unbind/persist onboarding, and no production path bridges selection-service auth state to real ACP `authenticate`. |

Verdicts are independent of earlier partial notes in
[A2_AGENT_SEND.md](./A2_AGENT_SEND.md) and were re-checked against the live
production graph at this baseline.

---

## 4. `A1-XX-01` scoped disposition (not a user-goal verdict)

| Aspect | Disposition |
|--------|-------------|
| Row type | Document ambiguity / deferred-finding bridge only ([GOAL_MATRIX.md §15](../GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior)) |
| Document claim | Missing user-facing workflow for binding Native Harness or ACP; in-memory infrastructure exists |
| Production finding | **Confirmed:** infrastructure exists; **no** supported production user entry point for bind/configure |
| What is present | Status-only caption on direct conversations (pull-based `GetSnapshot`); DI-registered selection service + presenter; store keyed by `ActorId`; real ACP protocol auth surface on `IAcpSessionClient` / `AcpProtocolSession` |
| What is absent | Any production caller of `BindNativeHarness`, `BindAcpRuntime`, `RequestAuthenticateAsync`, or `RecordAdvertisedAuthMethods`; any settings schema section for agent/backend/ACP bindings; any unbind/rebind UI; any production bridge from selection service/UI to `IAcpSessionClient.AuthenticateAsync` |
| `RequestAuthenticateAsync` boundary | **Local binding-state transition only** — validates method ID against in-memory advertised list (if any), then replaces the store binding with `Authenticated` or `Failed`. Does **not** resolve an ACP session client, does **not** call `IAcpSessionClient.AuthenticateAsync`, and does **not** send the ACP `authenticate` protocol request. No production caller. |
| Advertised auth methods | ACP initialize negotiates auth method data inside the ACP session path; **no** production caller copies negotiated method IDs into `AgentActorBackendSelectionService.RecordAdvertisedAuthMethods`, so `GetAdvertisedAuthMethodIds` stays empty on the production selection-service path unless a non-production caller injects values |
| Relation to user-goal rows | Explains the **gap** half of `A1-AC-02`; does **not** receive `Wired` / `Wired-with-gap` / `Missing` / `Ambiguous` as a third goal verdict |

---

## 5. Historical Phase 5 Agent Panel evolution

| Era | Documented intent | Live production |
|-----|-------------------|-----------------|
| Phase 5 | Dedicated agent panels in the right column; host-owned collection; one OpenAI-compatible direct-execution path; Townhall mirroring ([Phase 5 plan](../../../phases/v1/phase-5/IMPLEMENTATION_PLAN.md)) | Historical: `AgentPanelHost`, `AgentPanelState`, execution seams remain in tree |
| Phase 14 M8 | Retire dedicated Agent Panel chrome after DM parity; Townhall sole re-entry ([Phase 14 plan](../../../phases/v3/phase-14/IMPLEMENTATION_PLAN.md) D17 / M8) | [RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs) L14–15: “dedicated Agent Panel chrome removed; Townhall is the sole DM workflow”; editor-only right column |
| Current | Goal matrix keeps `A1-AC-01` as historical V1 evidence | No shell surface hosts `AgentPanelHostView` or an “add panel” control |

`IAgentPanelHost` / `AgentPanelHost` remain registered and used **internally**
as a send/session host (`GetOrCreatePanelForActor` from Townhall direct send
and `AgentRouter`), not as user-facing panel chrome. That is **not** the
Phase 5 “add a new agent panel” product path.

---

## 6. Canonical-agent versus user-created-agent analysis

### Explicit distinctions

| Concept | Production reality |
|---------|-------------------|
| 1. Seeded / catalog-listed agent identity | [CanonicalActorSeeds.cs](../../../../src/Features/Conversations/Application/CanonicalActorSeeds.cs): Human, Zaide Agent (`agent-1`), Alpha–Delta panel seeds. [ActorCatalog](../../../../src/Features/Conversations/Application/ActorCatalog.cs) loads all seeds at construction. |
| 2. User-created agent identity | **No** production UI path. `RegisterOrGetCustomPanelActor` / `GetOrRegisterPanelFallbackActor` exist on the catalog and are only reached via `AgentPanelHost.CreatePanel(...)` APIs that have **zero** production View/command callers (`CreatePanel` definitions only in host/interface). |
| 3. Dedicated Agent Panel | **Retired** from shell ([RightColumnHost.cs](../../../../src/App/Shell/RightColumnHost.cs)). Host object is dormant chrome + internal panel-state bag. |
| 4. Townhall direct conversation | User-reachable: People row → `OpenDirectConversationCommand` → find-or-create DM ([TownhallViewModel.cs](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L619–631). Roster seed is **only** User + Zaide Agent ([SeedWorkspaceAgents](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L1000–1020), not Alpha–Delta. |

### Catalog mutability

| Question | Answer |
|----------|--------|
| Mutation APIs on catalog? | Register/get custom panel actor; get-or-register fallback; **no** remove/rename/update-profile API on [IActorCatalog](../../../../src/Features/Conversations/Contracts/IActorCatalog.cs) |
| Production callers of add/remove/update? | **None** for user-driven create. `GetOrCreatePanelForActor` only materializes panels for **already catalogued** actors |
| Persistence of user-defined identity? | Catalog is application-lifetime in-memory; conversation snapshot may retain participant pairs for known actors but does not define a user agent-profile store |
| Townhall roster dynamic refresh? | `SeedWorkspaceAgents` no-ops if `_state.Agents.Count > 0`; initial seed is two fixed rows; no subscription that adds catalog agents dynamically |
| Binding identity stability | Bindings key by `ActorId` ([AgentActorBackendBindingStore.cs](../../../../src/Features/Agents/Application/AgentActorBackendBindingStore.cs) L14); store has no remove API |
| Unknown / removed actor | `CreatePanelForActor` throws if catalog miss ([AgentPanelHost.cs](../../../../src/Features/Agents/Presentation/AgentPanelHost.cs) L87–94); binding lookup fails closed to unbound |

**Finding:** production supports **canonical seeded actors** (and catalog presence of Alpha–Delta for routing) plus **Townhall DM navigation** to seeded peers. It does **not** support user-defined agent identity creation as a product workflow.

### `A1-AC-01` checklist (historical goal)

| Sub-claim | Result |
|-----------|--------|
| User-reachable add/create agent action | **No** |
| Mutable agent identity/profile model | **No** (seed + register APIs without product callers / persistence) |
| Create / rename / remove / configure-agent commands | **No** production commands |
| Dedicated Agent Panel or equivalent surface | **No** (retired) |
| Direct execution path from newly created agent | N/A — no creation path |
| Agent-specific output/status visibility | Direct conversation chat + busy tracking only for existing DMs (covered under send/Townhall slices) |
| Townhall mirroring for created agent | N/A for user-created agents |
| Missing endpoint / invalid response / cancellation / provider failure | Not reachable as Phase 5 panel path; send-path failure projection audited in [A2_AGENT_SEND.md](./A2_AGENT_SEND.md) |

**Verdict `A1-AC-01` = Missing.**

---

## 7. Native Harness onboarding map

```
[Settings LLM UI / env vars / secret store]  ──options only──►  AgentExecutionService.BuildEffectiveOptions
                                                                    │
                                                                    ▼
INativeHarnessProviderOptionsSource  ──►  NativeHarnessAgentBackend  (IAgentBackend sibling)
                                                                    ▲
                                                                    │ requires binding
User UI bind action  ✗ MISSING  ──►  BindNativeHarness(actorId)
                                          │
                                          ▼
                              AgentActorBackendBindingStore (in-memory)
                                          │
                                          ▼
                              AgentExecutionCoordinator.GetRequiredBackendId
                                          │
                                          ▼
                              AgentSessionService → NativeHarnessAgentBackend
                                          │
                                          ▼
                              (admitted send / tools path — prior A2 slices)
```

| Question | Source-proven answer |
|----------|----------------------|
| Can a normal user initiate `BindNativeHarness`? | **No.** Method exists on selection service and presenter; **no** production View/command caller. Presenter is DI-registered only ([AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L53); not injected into Townhall. |
| Configure endpoint / model / authentication for Native Harness? | **Partial, shared, not per-actor binding:** Settings LLM (`BaseUrl`, `Model`) + secret `llm.apiKey` + env `AGENT_API_URL` / `AGENT_MODEL` / `AGENT_API_KEY` ([AgentExecutionService.cs](../../../../src/Features/Agents/Infrastructure/AgentExecutionService.cs) L417–449; [SettingsPanelView.cs](../../../../src/Features/Settings/Presentation/SettingsPanelView.cs) L107–109). This configures the **provider options source**, not actor↔backend binding. |
| Binding persisted? | **No** — in-memory dictionary only ([AgentActorBackendBindingStore.cs](../../../../src/Features/Agents/Application/AgentActorBackendBindingStore.cs) L9–14). |
| Runtime/backend identity recorded? | Binding records `AgentBackendIds.NativeHarness` and auth `NotRequired` on bind ([AgentActorBackendSelectionService.cs](../../../../src/Features/Agents/Application/AgentActorBackendSelectionService.cs) L58–64). Exposed via snapshot label `"Native Harness"`. |
| Capability honesty | Native Harness maintains `AgentCapabilitySnapshot` data and admitted sessions can emit normalized `CapabilitySnapshotChanged` events. `AgentConversationEventProjection.OnEvent` does **not** handle `AgentEventKind.CapabilitySnapshotChanged`. Townhall binding panel shows backend/auth captions only — **no** capability matrix/control. Capability changes are internally modeled/emitted, not user-projected. |
| Unbind / rebind / switch | Store `SetBinding` overwrites; **no** remove API; **no** UI. |
| After binding, send/tools path? | **Yes (source):** coordinator requires binding then dispatches by backend id — same path audited in [A2_AGENT_SEND.md](./A2_AGENT_SEND.md) / [A2_TOOLS_PERMISSIONS.md](./A2_TOOLS_PERMISSIONS.md). Binding itself is not user-reachable. |

---

## 8. ACP onboarding map

```
User UI: pick executable/args/auth  ✗ MISSING
          │
          ▼
BindAcpRuntime(actorId, AcpRuntimeIdentity, expectedName, expectedVersion)
          │
          ▼
AgentActorBackendBindingStore  (AcpRuntime absolute path + args; auth Disconnected)
          │
          ▼
AcpProductionSessionClientFactory.CreateAsync
  → File.Exists(executable) → AcpStdioProcessHost.StartAsync
  → throws on missing binding/executable; does NOT mutate binding auth state
          │
          ▼
AcpActionCapableAgentBackend / AcpAgentBackend  (IAgentBackend sibling)
  → ACP initialize negotiates capabilities/auth methods inside session path
  → no production copy into AgentActorBackendSelectionService
          │
          ▼
(admitted send / action mediation — prior A2 slices)

Separate surfaces (not bridged in production):
  RequestAuthenticateAsync  → local store Authenticated/Failed only (no ACP client)
  IAcpSessionClient.AuthenticateAsync / AcpProtocolSession  → real ACP authenticate RPC
```

| Question | Source-proven answer |
|----------|----------------------|
| Can a normal user initiate `BindAcpRuntime`? | **No.** Same absence as Native Harness: API on selection service + presenter; no production caller. |
| Select/configure ACP executable and arguments? | **No** product surface. `AcpRuntimeIdentity` requires rooted absolute executable path ([AcpRuntimeIdentity.cs](../../../../src/Features/Agents/Domain/AcpRuntimeIdentity.cs) L13–29). Values would come only from a bind call. |
| Settings schema for ACP? | **No.** [SettingsModel.cs](../../../../src/Features/Settings/Domain/SettingsModel.cs) L24–29: Editor, Llm, Keybindings, Debug only. |
| Local auth-state API vs real ACP auth | `RequestAuthenticateAsync` ([AgentActorBackendSelectionService.cs](../../../../src/Features/Agents/Application/AgentActorBackendSelectionService.cs) L106–137) has **no** production caller. It is a **local binding-state transition API**: validates the method ID against the in-memory advertised list (if non-empty), then `SetBinding` with `Authenticated` or `Failed`. It does **not** resolve an ACP session client, does **not** call `IAcpSessionClient.AuthenticateAsync`, and does **not** send the ACP `authenticate` protocol request. The real ACP protocol authentication surface is `IAcpSessionClient.AuthenticateAsync` → `AcpProtocolSession.AuthenticateAsync` (sends `authenticate`); **no** production bridge connects the selection service or UI to that protocol call. |
| Advertised ACP auth methods → selection state | ACP initialization negotiates auth method data inside the ACP session path (`AcpNegotiatedCapabilities.AuthMethods`). `RecordAdvertisedAuthMethods` exists ([AgentActorBackendSelectionService.cs](../../../../src/Features/Agents/Application/AgentActorBackendSelectionService.cs) L98–104) but has **no** production caller. Production therefore does **not** copy negotiated method IDs into the selection service; `GetAdvertisedAuthMethodIds(actorId)` remains empty unless a non-production/internal caller injects values. Townhall binding panel does **not** expose advertised method choices. |
| Missing executable / process launch failure | `AcpProductionSessionClientFactory.CreateAsync` throws when binding or executable is missing ([AcpProductionSessionClientFactory.cs](../../../../src/Features/Agents/Application/Acp/AcpProductionSessionClientFactory.cs) L38–51). The factory does **not** mutate `AgentActorBackendBindingStore` and does **not** set authentication state to `Failed`. `GetSnapshot(...).IsDisconnected` is true only when the stored ACP binding’s authentication state is **already** `Failed` ([AgentActorBackendSelectionService.cs](../../../../src/Features/Agents/Application/AgentActorBackendSelectionService.cs) L44–46). Launch/missing-executable failure rides the ACP backend/session failure path; it is **not** source-proven to update the binding status caption to red disconnected. User recovery still lacks bind/rebind UI. |
| Capability reporting | ACP maintains `AgentCapabilitySnapshot`; admitted sessions emit `CapabilitySnapshotChanged`. Projection/UI do not surface a capability matrix (see §11). |
| Sibling, not wrapper | ACP and Native Harness are separate `IAgentBackend` singleton registrations ([AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L63–89). Neither is composed as a fallback for the other. |

ACP and Native Harness are **independent sibling backends**. Neither is described here as a substitute for the other.

---

## 9. Binding/status UI reachability analysis

| Surface | Interactive? | What it does |
|---------|--------------|--------------|
| [AgentBackendBindingPanel](../../../../src/Features/Agents/Presentation/AgentBackendBindingPanel.cs) | **Status-only** | Captions: backend label + auth text; `isDisconnected` → IndianRed; no buttons/commands; no advertised-method picker; no capability matrix |
| [TownhallView](../../../../src/Features/Townhall/Presentation/TownhallView.cs) L66–70, L351–362 | Binds visibility/labels | Projects VM properties into the panel |
| [TownhallViewModel.RefreshActiveBackendBindingProjection](../../../../src/Features/Townhall/Presentation/TownhallViewModel.cs) L1103–1131 | Read-only, **pull-based** | Invoked from `UpdateActiveConversationDisplayContext`; pulls one `GetSnapshot(peer)` for the active direct conversation; unbound → `"Unbound"`; no bind commands |
| [IAgentActorBackendSelectionService](../../../../src/Features/Agents/Contracts/IAgentActorBackendSelectionService.cs) | Snapshot API only | **No** change event on the interface |
| [AgentBackendBindingPresenter](../../../../src/Features/Agents/Presentation/AgentBackendBindingPresenter.cs) | Has bind methods + `BindingChanged` | **No** production consumer; DI singleton only; Townhall does **not** subscribe to `BindingChanged` |
| Context policy selector (adjacent chrome) | Interactive | **Not** backend onboarding (covered under restart/context slice) |

**Treat read-only binding/status caption as visibility, not onboarding.**

**Non-reactive boundary (gap, not absence):** status projection is pull-based. A binding/auth change while the same DM remains active is **not** source-proven to refresh immediately; captions may stay stale until another display-context refresh runs `RefreshActiveBackendBindingProjection`. This is an additional gap on top of the missing onboarding controls — it does **not** prove the status panel is absent.

Default clean-profile direct DM with Zaide Agent: snapshot unbound → labels `"Unbound"` / disconnected auth caption path; send rejects with missing binding ([AgentExecutionCoordinator.cs](../../../../src/Features/Agents/Application/AgentExecutionCoordinator.cs) L109–123) — consistent with [ISSUE-008](../../../issues/open/ISSUE-008-agent-response-not-showing.md) source hypothesis (runtime sole-cause still A3).

---

## 10. Configuration, authentication, and persistence matrix

| Concern | Native Harness | ACP | Persistence |
|---------|----------------|-----|-------------|
| Provider endpoint / model | Settings `Llm` + env override | N/A (process executable) | LLM settings: yes (settings.json). Env: process environment. |
| API key / secrets | Env `AGENT_API_KEY` → secret store `llm.apiKey` → empty; settings hold `ApiKeySource` only, not key plaintext ([SettingsModel LlmSettings](../../../../src/Features/Settings/Domain/SettingsModel.cs) L172–194; [SettingsViewModel](../../../../src/Features/Settings/Presentation/SettingsViewModel.cs) L19, L119–120) | No Zaide settings secret path for ACP agent credentials in this slice | Secret store separate from settings.json (source design); **values not inspected** |
| Actor↔backend binding | `BindNativeHarness` (no production caller) | `BindAcpRuntime` + `AcpRuntimeIdentity` (no production caller) | **In-memory only**; lost on process exit ([A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md) binding cold-start finding) |
| Auth state | `NotRequired` on bind | Store starts `Disconnected` on bind. `RequestAuthenticateAsync` is a **local** Authenticated/Failed binding rewrite only (no production caller; not ACP protocol auth). Real ACP `authenticate` lives on `IAcpSessionClient` / `AcpProtocolSession` with **no** production UI/selection bridge. | In-memory with binding |
| Advertised auth methods | N/A | Negotiated inside ACP initialize; **not** copied into selection service in production (`RecordAdvertisedAuthMethods` has no production caller); panel exposes no method choices | In-memory dictionary on selection service only |
| Missing/invalid config feedback | Options resolve null / empty key can fail provider calls after admission; unbound send rejects before backend | Missing executable → factory throws (`AcpProcessLifecycleException`); factory does **not** set binding auth to `Failed` or flip `IsDisconnected`. Launch failure is session/backend failure path, not a proven red disconnected binding caption. | Unbound rejection **not** projected to chat (send slice); status caption is pull-based when DM display context refreshes |
| User unbind / switch | No API/UI remove | No API/UI remove | N/A |

**No real credentials, profile paths, or secret file contents were read.**

---

## 11. Capability / disconnect / failure projection analysis

| Stage | Native Harness | ACP |
|-------|----------------|-----|
| User surface bind command | Absent | Absent |
| Selection service | Present | Present |
| Binding store | Present (empty by default) | Present (empty by default) |
| Backend construction | DI singleton always constructed | DI singleton always constructed |
| Capability state (internal) | `AgentCapabilitySnapshot` on backend; admitted sessions can emit normalized `AgentEventKind.CapabilitySnapshotChanged` | Same pattern via ACP adapters/backends |
| Capability → conversation/UI | `AgentConversationEventProjection.OnEvent` does **not** handle `CapabilitySnapshotChanged`. `AgentBackendBindingPanel` shows backend + auth captions only. **No** Townhall capability matrix/control. Internally modeled/emitted; **not** user-projected. | Same |
| Auth state → UI | “Auth not required” style captions when bound (pull refresh) | Captions reflect **stored** authentication state when display context refreshes: disconnected / failed / authenticated. Local `RequestAuthenticateAsync` would rewrite store state only; it is not ACP protocol login and has no production caller. Advertised methods are not wired into selection state in production. |
| Status refresh boundary | Pull via `RefreshActiveBackendBindingProjection` on display-context update; no selection-service change event; presenter `BindingChanged` has no production subscriber | Same |
| Send admission | Requires binding | Requires binding + ACP runtime on factory |
| Success/failure projection | Prior A2 send slice (admitted path) | Prior A2 send slice (admitted path) |
| Disconnect / launch failure | Session/continuity seams (prior slices); binding not restored on restart | `IsDisconnected` only when stored ACP auth state is already `Failed`. Missing-executable/process-launch throws in factory without mutating binding auth. Not source-proven to paint red disconnected binding status from launch failure alone. Process host lifecycle remains in ACP stack. |
| Restart | Binding store empty; not in conversation snapshot | Same |

---

## 12. DI registration and production-caller analysis

| Type | Registered? | Production caller beyond composition/tests? |
|------|-------------|-----------------------------------------------|
| `IAgentActorBackendBindingStore` → `AgentActorBackendBindingStore` | Yes ([AgentsServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/AgentsServiceCollectionExtensions.cs) L51) | Coordinator, ACP factory/adapters, continuity — **consumers**, not bind initiators. Factory reads bindings; does **not** write auth-Failed on launch failure. |
| `IAgentActorBackendSelectionService` → `AgentActorBackendSelectionService` | Yes (L52) | Townhall VM: **GetSnapshot only** ([TownhallServiceCollectionExtensions.cs](../../../../src/App/Composition/Registration/TownhallServiceCollectionExtensions.cs) L42; VM L1127). Interface exposes **no** change event. |
| `BindNativeHarness` / `BindAcpRuntime` | On interface + service + presenter | **No** production caller |
| `RequestAuthenticateAsync` | On interface + service | **No** production caller. Local store Authenticated/Failed only — **not** `IAcpSessionClient.AuthenticateAsync` / ACP `authenticate`. |
| `RecordAdvertisedAuthMethods` | Internal on selection service | **No** production caller. Negotiated ACP auth methods are **not** copied into selection state in production. |
| `GetAdvertisedAuthMethodIds` | On interface + service | Read by `GetSnapshot` / local auth API; production dictionary stays empty without injection |
| `AgentBackendBindingPresenter` | Yes (L53) | **None** outside DI/tests. `BindingChanged` has **no** production subscriber (Townhall does not consume presenter). |
| `IAcpSessionClient` / `AcpProtocolSession.AuthenticateAsync` | Via ACP session client stack | Real ACP protocol auth RPC exists; **no** production bridge from selection service or UI |
| `NativeHarnessAgentBackend` + `AcpActionCapableAgentBackend` as `IAgentBackend` | Yes (L63–89) | Session dispatch when bound |
| `IAcpSessionClientFactory` → `AcpProductionSessionClientFactory` | Yes (L55–62) | ACP backend create path when bound; throws on missing binding/executable without binding-store mutation |
| `IAgentPanelHost` → `AgentPanelHost` | Yes (L41) | Townhall/router `GetOrCreatePanelForActor`; **not** user `CreatePanel` |
| `AgentBackendBindingPanel` | Constructed in `TownhallView` | Status display only (backend + auth captions) |

**Rule applied:** DI registration ≠ user-reachable onboarding.

`rg` over `src/` for `BindNativeHarness` / `BindAcpRuntime` / `RequestAuthenticateAsync` / `RecordAdvertisedAuthMethods`: definitions (and presenter wrappers for bind) only — **no** other production call sites. Test code uses `bindingStore.SetBinding(...)` helpers, not a production UI path.

---

## 13. Source-proven versus runtime-unproven findings

### Source-proven

1. Agent Panel shell chrome is retired; Townhall is the sole DM surface.
2. No production user path creates agents or calls `CreatePanel`.
3. People roster seeds User + Zaide Agent only; Alpha–Delta remain catalog/routing seeds.
4. Binding store starts empty; no persistence; no production callers of `BindNativeHarness`, `BindAcpRuntime`, `RequestAuthenticateAsync`, or `RecordAdvertisedAuthMethods`.
5. Status panel is read-only visibility; refresh is **pull-based** via `RefreshActiveBackendBindingProjection` on display-context update — not reactive to binding changes while the same DM stays active.
6. Native Harness and ACP are independently registered sibling backends.
7. Native Harness provider options resolve from settings/env/secret-store keys (not values inspected).
8. ACP launch requires explicit bound `AcpRuntimeIdentity` with absolute executable path; factory throws on missing binding/executable **without** mutating binding auth to `Failed`.
9. `RequestAuthenticateAsync` is a local binding-state API only; real ACP `authenticate` is on `IAcpSessionClient` / `AcpProtocolSession` with no production selection/UI bridge.
10. Negotiated ACP auth methods are not copied into selection-service advertised-method state in production.
11. Capability snapshots are internal; `CapabilitySnapshotChanged` is not handled by conversation projection and has no Townhall capability surface.
12. Unbound send is rejected before session admission (coordinator).
13. After a binding exists, send and tools mediation enter the graphs already audited in prior A2 slices.

### Runtime-unproven (requires A3 / external systems — not claimed here)

1. Whether clean-profile UI literally shows “Unbound” captions as expected.
2. Whether Settings LLM save + secret store + env produce a successful Native Harness completion **after** a non-UI bind.
3. ACP process handshake with any named candidate (Claude Code, OpenCode, etc.).
4. Live auth caption transitions if store auth state were mutated out-of-band; whether captions stay stale until display-context refresh.
5. Live session-failure UI after missing-executable launch (not claimed to flip binding `Failed` / red disconnected).
6. ISSUE-008 sole-cause confirmation on a disposable profile.

### Classification buckets

| Bucket | Items |
|--------|-------|
| Source-proven wiring | Backend composition; binding store/selection; pull-based status projection; LLM options path; panel host internal use; ACP protocol auth RPC surface (unbridged) |
| Test-only wiring | `SetBinding` / identity fixtures; presenter inventory asserts; any advertised-method injection |
| Registered-but-unbound services | `AgentBackendBindingPresenter` bind methods + unused `BindingChanged`; `RequestAuthenticateAsync` (local only); `RecordAdvertisedAuthMethods` |
| Default-user reachability | Open DM; see unbound status on display-context refresh; send → silent rejection (per send slice); configure global LLM settings |
| Runtime behavior requiring A3 | All live bind/send/cleanup observations |
| External candidate/provider | Named ACP agents; live OpenAI-compatible provider |

---

## 14. Reconciliation with earlier A2 evidence and DF-008/DF-009

| Prior evidence | This slice reconciliation |
|----------------|---------------------------|
| [A2_AGENT_SEND.md](./A2_AGENT_SEND.md) §7 / partial `A1-AC-*` | Confirmed: infrastructure present; UI bind absent; status-only panel; unbound reject. Full-row verdicts now assigned here (`A1-AC-01` Missing; `A1-AC-02` Wired-with-gap). |
| [A2_MULTI_AGENT_ROUTING.md](./A2_MULTI_AGENT_ROUTING.md) | Catalog seeds + `ListAgents` remain routing roster; not user creation. People panel still not a multi-agent configuration surface. |
| [A2_TOOLS_PERMISSIONS.md](./A2_TOOLS_PERMISSIONS.md) | “No user-reachable backend-binding workflow” remains accurate; tools path still depends on admitted bound runs. |
| [A2_RESTART_RECOVERY_AND_CONTEXT.md](./A2_RESTART_RECOVERY_AND_CONTEXT.md) | Binding non-persistence and empty cold-start store reconfirmed; no production restore path found. |
| [A2_TRACE_MEMORY_USAGE_TERMINATION.md](./A2_TRACE_MEMORY_USAGE_TERMINATION.md) | No change; admitted backend runs still prerequisite for evidence producers. |
| Phase 20 auth/capability seams | Phase 20 composed ACP session/capability negotiation and protocol `authenticate` on the session client. This slice confirms those seams remain **unbridged** to selection-service advertised methods, local `RequestAuthenticateAsync`, Townhall method UI, and user-facing capability projection. Does not reopen Phase 20 milestone claims; records product onboarding gap only. |

### DF-008 / DF-009 disposition (record only — files not edited)

| Deferred | Disposition | Wording accuracy |
|----------|-------------|------------------|
| [DF-008](../../../deferred/open/DF-008-multiple-agent-connections.md) | **Still accurate / confirmed** | Store multi-actor capable; no production UI for configure/connect/persist; settings lack agent/backend section |
| [DF-009](../../../deferred/open/DF-009-real-acp-integrations.md) | **Still accurate / confirmed** | ACP stack registered; no `BindAcpRuntime` UI; no persisted ACP settings; no production bridge to ACP `authenticate`; external candidate smoke not a product path |
| [A1-XX-01](../GOAL_MATRIX.md#15-promises-that-cannot-yet-be-translated-into-user-behavior) | **Confirmed** within this slice | Infrastructure present; user bind/configure entry point absent; local auth-state API ≠ ACP login |
| Narrowing needed? | Optional future wording: distinguish **status visibility** (pull-based, present) from **onboarding workflow** (still absent); multi-agent storage capacity vs single visible People seed; local binding auth vs protocol auth | Record here only |

### ISSUE-008

Source hypothesis (unbound default + silent rejection) remains consistent; this slice does not close the issue and does not claim runtime sole-cause.

---

## 15. A3 disposable-profile smoke constraints (described only — not started)

A3 for this slice must use a **disposable isolated profile only** and must not
touch the real user profile, settings, secrets, or conversation store.

### Negative-path (default clean profile — expected reachable)

1. Launch; open People → Zaide Agent DM.
2. Observe backend binding status region: unbound / disconnected-style captions after display-context projection.
3. Send a message; observe absence of assistant response and of projected rejection (send-slice gap).
4. Confirm no UI control binds Native Harness or ACP, requests authentication, or lists advertised ACP auth methods.
5. Confirm no UI creates a new agent identity or Agent Panel tab; no capability matrix control.
6. Open Settings; observe LLM fields exist; observe **no** ACP/agent-backend binding section.

### Positive-path bind/send (blocked on clean profile without non-product injection)

1. User-visible bind of Native Harness or ACP **cannot** be completed through shipped UI.
2. A3 must **not** silently edit the real profile or invent production UI.
3. If a future authorized harness injects a binding for smoke, it must be documented as **non-product** setup and must not be scored as user onboarding.
4. Do **not** treat `RequestAuthenticateAsync` as proof of ACP login — it only rewrites local binding auth state and has no production caller; real ACP `authenticate` remains unbridged.
5. Do **not** expect missing-executable/process-launch failure to flip binding auth to `Failed` or automatically paint red disconnected binding status; observe session/backend failure path separately from binding captions.
6. If binding/auth is mutated while the same DM stays active, check whether captions refresh only on a later display-context pull (non-reactive gap).
7. External ACP executable and live LLM provider behavior are out of band unless the disposable environment explicitly provisions them; named Claude Code / OpenCode compatibility remains unproven.

### Persistence smoke (when authorized)

1. Even if a binding were injected mid-session, restart must show unbound again (in-memory store) unless a new product persistence path ships.
2. Conversation history may restore; backend binding must not appear restored from conversation snapshot schema.
3. Injected advertised auth methods (if any) are non-product; production does not transfer ACP-negotiated methods into the selection service.

**A3 is not begun in this session.**

---

## 16. Recommended next A2 slice (explicitly not started)

**Next recommended slice:** `A2_TOWNHALL_AND_CONVERSATIONS`

| Item | Value |
|------|-------|
| Slice name | `A2_TOWNHALL_AND_CONVERSATIONS` |
| Scope rows | `A1-TH-01`, `A1-TH-02`, `A1-TH-04`, `A1-TH-05` |
| Rationale | Completes the conversation-surface journey cluster after agent send, routing, tools, restart/context, and creation/onboarding; prior slices only partially touched Townhall projection |
| Evidence file (when started) | `docs/audits/v1-v3-product-reality/evidence/A2_TOWNHALL_AND_CONVERSATIONS.md` |
| Status in this session | **Explicitly not started** — no evidence file created, no verdicts assigned |

---

## 17. Verification and working-tree closeout

| Check | Expected / result |
|-------|-------------------|
| Exactly one new untracked evidence file | `docs/audits/v1-v3-product-reality/evidence/A2_AGENT_CREATION_AND_BACKEND_ONBOARDING.md` |
| Tracked modifications | None |
| Whitespace | `git diff --no-index --check /dev/null <evidence-file>` (exit 1 from diff vs `/dev/null` is expected; no whitespace diagnostics) |
| Relative Markdown links | Repository-relative paths under `docs/` and `src/` resolve |
| Fragment links | Headings in this file and cited docs resolve |
| Primary verdict table | Exactly one verdict each for `A1-AC-01` and `A1-AC-02` |
| `A1-XX-01` | Scoped disposition only (§4); gap confirmed |
| Auth wording | No implication that `RequestAuthenticateAsync` calls ACP authentication |
| Advertised methods wording | No implication that production transfers negotiated auth methods into selection service |
| Capability wording | Internal event stream named; no implication of user-facing capability projection |
| Launch failure wording | No implication that missing executable sets binding auth to `Failed` |
| Status projection wording | Pull-based / non-reactive boundary recorded |
| Runtime claims | None beyond source inspection |
| Next slice | Named and **not** begun |
| Commit / push | Not performed |

### Corrected distinctions (Corrective Round 1)

1. **Local auth-state API ≠ ACP login:** `RequestAuthenticateAsync` rewrites in-memory binding Authenticated/Failed only; real ACP `authenticate` is on `IAcpSessionClient` / `AcpProtocolSession` and is unbridged.
2. **Negotiated auth methods ≠ selection-service state:** no production `RecordAdvertisedAuthMethods` caller; Townhall exposes no method choices.
3. **Capability snapshots ≠ user projection:** internal `CapabilitySnapshotChanged` exists; conversation projection and Townhall do not surface a capability UI.
4. **Status panel present but non-reactive:** pull `GetSnapshot` on display-context refresh; no selection change event; presenter `BindingChanged` unused in production.
5. **Launch failure ≠ Failed binding:** factory throws without mutating store auth; `IsDisconnected` requires stored ACP auth already `Failed`.

### Verdict consistency summary

- `A1-AC-01` = **Missing** throughout.
- `A1-AC-02` = **Wired-with-gap** throughout (composed sibling backends + pull-based status visibility + LLM options path; missing user bind/configure/persist onboarding; unbridged ACP protocol auth; non-reactive status; no user capability surface).
- `A1-XX-01` = confirmed document gap remains; **not** a third user-goal verdict.
- `DF-008`, `DF-009`, and `A1-XX-01` remain confirmed within this slice.

---

*End of evidence (Corrective Round 1). Baseline `726c163395b1970f941ec14f54f607b64bfd2b5a`. A3 not started. Next A2 slice `A2_TOWNHALL_AND_CONVERSATIONS` not begun. No commit or push.*
