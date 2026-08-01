# Phase 22.2 M0 Live-Seam Verification

## Status and Boundary

This is a read-only M0 record against `master` at
`35ebf6832c99b1db6cd270064308c710362f6de8`. Before documentation edits,
`HEAD` and `origin/master` were equal and the worktree was clean.

No application, backend, test, or A3 scenario was executed; only test discovery
was listed with `--no-build --list-tests`. Historical A2 and A3 evidence remains owned by
`docs/audits/v1-v3-product-reality/evidence/` and was not rewritten. No
production code, tests, packages, audit evidence, Phase 22.1 files, Phase
22.3-22.5 files, V4 files, or `.claude/` files are part of this M0 change.

M0 documentation is complete and awaits **human G2 acceptance**. It does not
authorize implementation. Phase 22.2 implementation requires a later explicit
implementation prompt after M0 acceptance.

## M0 Verdict

A3 remains accurate against the live checkout:

- `AgentBackendBindingPanel` is a read-only status row containing text only.
- `AgentBackendBindingPresenter` can call the two bind methods and emit its own
  `BindingChanged` event, but production code neither consumes the presenter nor
  subscribes to that event.
- Townhall reads `IAgentActorBackendSelectionService.GetSnapshot` only when the
  active conversation display context refreshes. It has no bind, update,
  authenticate, reconnect, logout, or unbind command.
- Settings exposes shared Native Harness provider inputs only. It has no agent
  binding or ACP section, and the command registry has no backend-binding entry.
- The binding store is an app-lifetime dictionary keyed only by `ActorId`.
  `SetBinding` overwrites; no remove, durable load/save, revision, or mutation
  result exists.
- Real ACP `authenticate(methodId)` exists on `IAcpSessionClient` and
  `AcpProtocolSession`, but the selection service's same-named request only
  rewrites local in-memory state. No production bridge joins the two paths.
  `logout` is a method-name constant only; no logout API, capability projection,
  caller, or test exists.

The supported user configure/bind/unbind/persist workflow required by
`A1-AC-02`, BL-06, and `A1-XX-01` is therefore absent. Production DI and
read-only status visibility are infrastructure, not onboarding.

## User Reachability and Ownership Map

| Surface | Live entry point | Live behavior | M0 disposition |
|---------|------------------|---------------|----------------|
| Townhall direct conversation | People -> agent DM | Shows `AgentBackendBindingPanel`; pulls backend/auth captions from the active peer | Preserve as the primary per-agent binding workflow location, but make the later workflow interactive and reactive. |
| Townhall binding panel | Status row only | Two `TextBlock` values; no focusable control or command | M2/M3 own keyboard-accessible configure, inspect, retry/auth, and unbind controls. |
| `AgentBackendBindingPresenter` | DI singleton only | Bind wrappers plus an event with no production subscriber | Do not count as user reachability. M1/M2 may replace or extend this seam only with tests and production ownership. |
| Settings | Status-bar settings overlay | Editor, Terminal, and shared LLM endpoint/model/API-key inputs | Retain shared Native Harness provider configuration. It is not an actor binding. No ACP credential field is allowed. |
| Command registry | Command palette and keybindings | No agent/backend/bind/auth command | No command-palette command is required for 22.2. The Townhall controls are the locked entry point and must be keyboard/focus/automation accessible. |
| Production DI | `Program.ConfigureServices` -> `AddZaideAgents` / `AddZaideTownhall` | Registers both sibling backends, binding store/selection/presenter, ACP factory, and Townhall read-only selection dependency | Preserve sibling registration and constructor-owned composition. Registration alone is not onboarding. |

Phase 14's retired Agent Panel stays retired. Phase 22.2 does not restore
`A1-AC-01`; Townhall remains the sole direct-agent re-entry surface.

## Binding, Persistence, and Identity Truth

### Live binding contract

| Concern | Verified live rule |
|---------|--------------------|
| Key | `ActorId` only; duplicate display names do not participate in routing. |
| Backend identity | Typed `AgentBackendId`; Native Harness and ACP are independent siblings. |
| Native Harness binding | Backend ID plus `NotRequired` authentication state. Provider configuration is resolved separately. |
| ACP runtime identity | Absolute normalized executable path, ordered arguments, optional registry/provenance evidence, expected agent name, and expected agent version. |
| Update | `SetBinding` replaces the actor's current in-memory value under one lock. There is no revision/conflict result. |
| Unbind | Unsupported: the interface and store have no remove method. |
| Persistence | None in `SettingsModel`, `ConversationPersistenceService`, or another production binding file. Restart starts empty. |
| Change notification | Presenter-local event only; store and selection interfaces publish no change event. Townhall is pull-based. |
| Authentication metadata | Selected method, connection state, and advertised method IDs are in memory. Negotiated ACP methods are not copied into selection state. |

### Locked Phase 22.2 identity contract

- Durable binding identity remains **per `ActorId`**, matching the V3
  per-Agent-Identity rule. Display name is presentation only.
- Workspace is **not** part of the durable binding key. The current workspace
  remains an execution/context/action/continuity boundary captured at admission.
  A binding must never authorize session reuse or action authority across a
  workspace change.
- A persisted Native Harness binding stores only the actor/backend choice. Its
  endpoint and model remain shared `Llm` settings; its API key remains outside
  ordinary settings through `ISecretStore` or `AGENT_API_KEY`.
- A persisted ACP binding stores the actor ID, backend ID, normalized executable
  path, ordered non-secret arguments, expected agent name/version, and optional
  provenance fields. ACP credentials, authenticated state, advertised auth
  methods, capabilities, process/session IDs, and account state are runtime
  facts and must not be persisted as binding truth.
- Every admitted run captures a backend identity. Bind update or unbind while an
  actor has an active run is rejected with an actionable busy result; it never
  silently switches or invalidates an in-flight run.
- An idle binding update advances a durable binding revision and invalidates
  cached auth, capability, runtime, and continuity eligibility. A stale editor
  revision is rejected as a conflict rather than overwriting newer state.

### Locked persistence contract

M1 owns a dedicated schema-v1 binding document under the Zaide configuration
directory rather than adding binding state to conversation snapshots. File
absence means an empty store and is the only migration from the current
non-persistent implementation.

The writer must use a same-directory temporary file, atomic replacement, and a
last-known-good copy. It validates the entire candidate, prepares the backup of
the current primary before replacing that primary, and publishes the new
in-memory snapshot/change event only after durable success. On validation,
serialization, permission, disk, backup, or rename failure, the old primary and
in-memory binding remains authoritative and the UI receives a typed failure. A
leftover temporary file is never loaded as current state. One actor mutation
cannot leave UI, store, and disk on different binding revisions.

Unknown schema versions fail closed without rewriting the file. Invalid or
corrupt primary data uses the last-known-good document when valid; otherwise the
store starts unbound and exposes a recovery error. No implementation may
silently reinterpret, delete, or auto-repair a binding identity.

## Native Harness Configuration and Capability Truth

`NativeHarnessProviderOptionsSource` resolves live options through
`AgentExecutionService.BuildEffectiveOptions()`:

| Input | Precedence / persistence |
|-------|--------------------------|
| Base URL | `AGENT_API_URL` -> saved `SettingsModel.Llm.BaseUrl` |
| Model | `AGENT_MODEL` -> saved `SettingsModel.Llm.Model` |
| API key | `AGENT_API_KEY` -> `ISecretStore["llm.apiKey"]` -> empty |

A Native Harness actor binding does not copy these values. The backend reports
six-fact capability rows for message completion, tools, permissions, IDE
context, streaming, and cancellation. Configuration is usable only when API
key, base URL, and model are non-empty. Workspace capture separately controls
tools/permissions availability; context-manifest presence separately controls
IDE-context usability. The provider transport requests OpenAI-compatible SSE
streaming and redacts the configured API key from returned failure text.

Current Townhall does not project these capability rows. M2 must show configured,
available, and currently-usable truth without treating a saved endpoint/model,
an actor binding, workspace capture, permission, or successful provider access
as interchangeable facts. It must not invent provider names, model catalogs,
entitlement, cost, or network success.

## ACP Runtime, Authentication, and Logout Truth

### Live runtime path

1. An ACP binding requires a rooted `AcpRuntimeIdentity`, expected agent name,
   and expected version.
2. `AcpProductionSessionClientFactory` re-reads the target actor binding,
   requires the executable to exist, launches it without shell interpolation,
   and supplies only `PATH` and `DOTNET_ENVIRONMENT` through the explicit
   environment allowlist.
3. `AcpAgentSessionAdapter` calls `initialize`, validates negotiated
   `agentInfo` against the expected name/version, creates a session, and then
   prompts. Identity mismatch fails closed.
4. Negotiated capabilities/auth methods stay on the session client. No
   production caller forwards them to the selection service or binding panel.

ACP's process working directory currently comes from
`Environment.CurrentDirectory`; it is not proven to be a live workspace-root
provider. M3 must use the production workspace authority/root seam for
configuration probes and admitted sessions, and fail closed when no valid
workspace is available. This is a correction within Phase 22.2, not authority
to change Phase 18 context or Phase 17 action rules.

### Live auth/logout split

| Operation | Live truth |
|-----------|------------|
| Initialize | Implemented; returns agent capabilities, `authMethods`, and `agentInfo`. |
| Protocol authenticate | `IAcpSessionClient.AuthenticateAsync` -> `AcpProtocolSession.AuthenticateAsync` sends `authenticate` with a method ID after initialize. |
| Selection authenticate | `RequestAuthenticateAsync` validates an in-memory advertised list, then writes local `Authenticated`/`Failed`; it never creates a client or sends ACP `authenticate`. |
| Authenticate bridge | Absent. No production caller invokes either selection auth or protocol auth. |
| Credential owner | The ACP agent owns credentials, browser/terminal flow, tokens, and provider account state. Zaide sends only an explicitly selected advertised method ID. |
| Logout | `AcpMethodNames.Logout` exists, but no client/session method, parsed support flag, user control, caller, or test exists. |

M3 owns one bounded onboarding connection service: launch from the durable ACP
runtime identity, initialize, verify `agentInfo`, publish negotiated methods and
capabilities, invoke real `authenticate(methodId)` only after explicit user
selection, and capability-gate real `logout` after explicit user selection. It
must not create a prompt session during configuration. Authentication success
means only that the agent accepted the method; it does not prove entitlement,
model access, cost, or future-process authentication.

## Secret and Diagnostic Boundary

- Ordinary `settings.json`, the binding document, conversation data,
  continuity records, Townhall entries, logs, exceptions, status captions, and
  A3 evidence must not contain plaintext credentials or tokens.
- Native Harness API keys remain in `ISecretStore` or the process environment.
  Current `FileSecretStore` uses atomic replacement and Linux `0600`; settings
  retain only `ApiKeySource`.
- ACP has no credential input. Runtime arguments are explicitly non-secret
  launch configuration; the UI must state that authentication secrets belong
  to the ACP agent and must not be entered as arguments.
- ACP child processes receive a cleared environment plus the current explicit
  allowlist. Secret-like environment keys are denied. Captured stderr is
  bounded and redacts bearer and key/value secret shapes.
- Executable paths and non-secret arguments may be shown only where needed for
  inspection; neither is evidence of authentication or trust.

## Locked Mutation and Failure Outcomes

| Outcome | Required user-visible and state result |
|---------|----------------------------------------|
| Bind | Validate actor/backend/runtime/config candidate, persist atomically, then publish one new revision and reactive status. Failure leaves the prior state unchanged. No implicit fallback. |
| Update | Idle actor only; expected-revision conflict detection; persist then publish; clear runtime auth/capability caches. Active-run update is rejected. |
| Unbind | Idle actor only; atomically remove the durable record, clear runtime state, publish `Unbound`, and remain unbound after restart. Active-run unbind is rejected until the run is cancelled/terminal. |
| Restart | Rehydrate only durable identity/config. Never restore `Authenticated`, advertised methods, capabilities, ACP process/session IDs, or a run. Revalidate Native options/workspace and ACP executable/initialize/identity before claiming availability. |
| Stale ACP runtime | Missing/unreadable executable, changed expected `agentInfo`, changed arguments, or invalid workspace fails closed as unavailable/stale. Preserve the binding for user repair; never switch backend or overwrite expected identity. |
| Disconnect/process exit | Project a reactive disconnected/unavailable runtime state without rewriting durable binding identity. Retry/reconfigure/unbind are explicit user actions. |
| Authentication failure | Project failed authentication and retain agent-provided bounded error text after redaction. Do not persist authenticated/failed account state as durable truth and do not start a prompt. |
| Logout | Only when advertised and explicitly selected. Clear local runtime auth/method/capability state; existing sessions become indeterminate and are not silently reused. Durable runtime binding remains until separately unbound. |
| Partial write | Return typed persistence failure; keep the old durable and in-memory revision; never claim bind/update/unbind success; ignore orphan temp state. |
| Corrupt/unsupported persistence | Load valid last-known-good or fail closed to unbound with a recovery error. Never silently reinterpret or delete the file. |

## Focused Test Inventory and Exact Filters

M0 used `--no-build --list-tests` only. The existing assembly contains
composition, identity, capability, transport, redaction, send/routing, and
continuity tests, but no direct tests for selection-service bind/auth mutation,
presenter reactivity, user binding controls, unbind, binding persistence,
protocol logout, or the authenticate bridge.

### Existing seam inventory

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --list-tests --filter "FullyQualifiedName~Zaide.Tests.App.Composition.AgentsRegistrationModuleTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Integration.Phase20IntegrationTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Integration.Phase20IdentityBindingTests"

dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --list-tests --filter "FullyQualifiedName~Zaide.Tests.Features.Agents.Phase19IntegrationTests|FullyQualifiedName~Zaide.Tests.Features.Settings.Infrastructure.SecretStoreTests|FullyQualifiedName~Zaide.Tests.Features.Settings.Infrastructure.FileSecretStorePermissionTests"

dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --list-tests --filter "FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Protocol.Phase20ProtocolCapabilityTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Protocol.Phase20ProtocolSessionTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Transport.Phase20TransportLifecycleTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Transport.Phase20TransportStderrBoundaryTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Phase20AdversarialTests"

dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --list-tests --filter "FullyQualifiedName~Zaide.Tests.Features.Townhall.Presentation.Phase15TownhallParityTests|FullyQualifiedName~Zaide.Tests.Features.Townhall.Presentation.TownhallDirectSendTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Continuity.Phase21RestartTests"
```

### Exact later milestone filters

The `Phase22*` classes below are plan-required tests and do not exist at M0.
Each owning milestone must add its named classes before it can pass.

```bash
# M1 binding store, persistence, identity revision, and mutation outcomes
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22BackendBindingStoreTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22BackendBindingPersistenceTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22BackendBindingSelectionServiceTests"

# M2 Native Harness user workflow, capability truth, production composition, secrets
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22NativeHarnessBindingWorkflowTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Phase19IntegrationTests|FullyQualifiedName~Zaide.Tests.Features.Settings.Infrastructure.SecretStoreTests|FullyQualifiedName~Zaide.Tests.Features.Settings.Infrastructure.FileSecretStorePermissionTests|FullyQualifiedName~Zaide.Tests.App.Composition.AgentsRegistrationModuleTests"

# M3 ACP runtime/auth/logout workflow and preserved transport/security/identity rules
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22AcpBindingWorkflowTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22AcpAuthenticationBridgeTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Protocol.Phase20ProtocolCapabilityTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Transport.Phase20TransportLifecycleTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Transport.Phase20TransportStderrBoundaryTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Integration.Phase20IdentityBindingTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Acp.Phase20AdversarialTests"

# M4 reactive Townhall/restart/accessibility and dependent-path preservation
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22BackendBindingTownhallTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Binding.Phase22BackendBindingRestartTests|FullyQualifiedName~Zaide.Tests.Features.Townhall.Presentation.Phase15TownhallParityTests|FullyQualifiedName~Zaide.Tests.Features.Townhall.Presentation.TownhallDirectSendTests|FullyQualifiedName~Zaide.Tests.Features.Agents.Continuity.Phase21RestartTests"
```

Unit and composition tests cannot substitute for user-observable A3 re-smoke.

## Locked Local Re-Smoke Set

M4 must recreate an out-of-tree Avalonia.Headless runner at
`/tmp/zaide-a3-backend-binding/`. No retained producer exists at M0. The runner
must use production `Program.ConfigureServices`, disposable profile/workspace
roots, a loopback deterministic Native Harness provider, and the repository-
owned ACP fake agent. It must exercise the shipped Townhall controls; internal
`SetBinding`, `BindNativeHarness`, or `BindAcpRuntime` injection cannot count as
onboarding success.

Run both `native-harness` and `acp` for this exact row set:

- `A1-AC-02`: configure, bind, inspect/react, restart/revalidate, and unbind;
- backend-bound sub-path of `A1-AS-02`: admission and one terminal backend
  outcome only;
- backend-bound routed sub-paths of `A1-TH-05` and `A1-MR-03`;
- backend-bound context-manifest sub-path of `A1-TC-01`;
- backend-bound preflight/reachability sub-paths of `A1-TP-01`, `A1-TP-02`,
  and `A1-TP-03`.

The dependent rows record newly reachable behavior and honest remaining
failures. They do **not** close Phase 22.3 tools/permissions/send/routing work or
Phase 22.4 context/trace/memory/usage work, and they do not upgrade historical
A3 files.

After the later runner is prepared, the exact producer command is:

```bash
test -f /tmp/zaide-a3-backend-binding/runner/Zaide.Tests.csproj
dotnet restore /tmp/zaide-a3-backend-binding/runner/Zaide.Tests.csproj
dotnet publish /tmp/zaide-a3-backend-binding/runner/Zaide.Tests.csproj --no-restore -c Release -o /tmp/zaide-a3-backend-binding/out/Release/net10.0
dotnet build tests/fixtures/acp-fake-agent/AcpFakeAgent.csproj -o /tmp/zaide-a3-backend-binding/acp-fixture
test -f /tmp/zaide-a3-backend-binding/out/Release/net10.0/Zaide.Tests.dll
test -f /tmp/zaide-a3-backend-binding/acp-fixture/AcpFakeAgent.dll
test -d /tmp/zaide-a3-backend-binding/fixtures/workspace
mkdir -p /tmp/zaide-a3-backend-binding/evidence
for backend in native-harness acp; do
  for scenario in A1-AC-02 A1-AS-02 A1-TH-05 A1-MR-03 A1-TC-01 A1-TP-01 A1-TP-02 A1-TP-03; do
    profile_root="$(mktemp -d /tmp/zaide-a3-backend-binding-profile-XXXXXXXX)"
    mkdir -p "$profile_root/home" "$profile_root/config" "$profile_root/data" "$profile_root/state" "$profile_root/cache"
    cp -a /tmp/zaide-a3-backend-binding/fixtures/workspace "$profile_root/workspace"
    env HOME="$profile_root/home" \
      XDG_CONFIG_HOME="$profile_root/config" \
      XDG_DATA_HOME="$profile_root/data" \
      XDG_STATE_HOME="$profile_root/state" \
      XDG_CACHE_HOME="$profile_root/cache" \
      dotnet /tmp/zaide-a3-backend-binding/out/Release/net10.0/Zaide.Tests.dll \
      --scenario "$scenario" \
      --backend "$backend" \
      --profile "$profile_root" \
      --workspace "$profile_root/workspace" \
      --acp-executable "$(command -v dotnet)" \
      --acp-argument /tmp/zaide-a3-backend-binding/acp-fixture/AcpFakeAgent.dll \
      --acp-argument healthy \
      --evidence "/tmp/zaide-a3-backend-binding/evidence/$scenario-$backend.json" \
      --repo-head "$(git rev-parse HEAD)"
  done
done
```

M4 must retain evidence before deleting disposable profile state. Any producer
change requires a renewed human decision; source wiring, unit tests, real
profiles, real providers, and external ACP candidates are not substitutes.

## Milestone and Rollback Lock

- **M1** owns the durable backend-neutral store, schema-v1 serialization,
  revisions, bind/update/unbind mutation results, startup load/recovery, and
  reactive change boundary. It is one independently revertible commit.
- **M2** owns only the Townhall Native Harness workflow, shared-settings link,
  capability projection, focus/accessibility behavior, and focused tests.
- **M3** owns only the ACP runtime workflow, configuration probe, real
  authenticate bridge, capability-gated logout, reactive failures, and focused
  tests. It must not reuse or wrap Native Harness.
- **M4** owns restart/revalidation integration, regression gates, out-of-tree
  re-smoke evidence, and closeout docs; it does not combine M1-M3 code.

Rollback is by reverting only the owning milestone commit. The schema-v1
binding file is additive: pre-22.2 code ignores it, so rollback preserves it as
recoverable user data rather than deleting or rewriting it. A later schema
change must create a backup and remain backward-readable before publication.
Never roll back Phase 22.1 or historical Phase 19-21 commits to undo Phase
22.2.

## Stop Gate

This M0 record is ready for human review only. No implementation, test change,
package change, runtime smoke, backend execution, Phase 22.3-22.5 work, Phase
22.1 reopen, or V4 work is authorized until a later prompt explicitly accepts
M0 and authorizes the next named Phase 22.2 implementation milestone.
