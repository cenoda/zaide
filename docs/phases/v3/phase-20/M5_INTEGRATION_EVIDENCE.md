# Phase 20 M5 — Integration Evidence

## Publication

| Item | Value |
|------|-------|
| Milestone | M5 — Explicit identity binding, production composition, equal Townhall placement, authentication boundary |
| Published commit | `35aa2aac2badde681b6e7ab862d2c59547ce3407` |
| Depends on | M4 at `63880c53c2317a4e4d85ade2088c96764c510b6f` with publication-record correction `51ee2691` and status correction `f0e266c6` |
| Production surfaces | `IAgentActorBackendBindingStore`, `IAgentActorBackendSelectionService`, `IAcpSessionClientFactory`, `AgentsServiceCollectionExtensions`, `TownhallViewModel` / projection path, `AgentBackendBindingPanel` |
| Test surfaces | `tests/Zaide.Tests/Features/Agents/Acp/Integration/`, composition registration tests, architecture inventory ratchets |

## Explicit Actor/backend/runtime binding

| Concern | Implementation |
|---------|----------------|
| Zaide-owned identity | `ActorId`, `ConversationId`, `AgentSessionId`, and `ExecutionRunId` remain Zaide-owned; ACP session/request/message/tool/process IDs are correlation evidence only |
| Binding store | `AgentActorBackendBindingStore` fails closed when no explicit binding exists for an actor |
| User selection | `AgentActorBackendSelectionService` requires explicit Native Harness or ACP runtime selection before binding |
| ACP runtime identity | `AcpRuntimeIdentity` pins absolute executable path and arguments; duplicate display names route only by typed IDs |
| Reconnect mismatch | `AcpAgentSessionAdapter` fails closed on executable or `agentInfo` mismatch after reconnect/restart |
| No fallback | `AgentExecutionCoordinator` resolves backend per actor binding; unbound actors and ACP mismatch never silently select Native Harness |

## Production composition

- `AddZaideAgents` registers `NativeHarnessAgentBackend` and `AcpActionCapableAgentBackend` as sibling `IAgentBackend` implementations.
- `IAcpSessionClientFactory` production implementation is `AcpProductionSessionClientFactory`, launched from the actor binding via `IAcpProcessLauncher`.
- `ApplicationShutdown` retains exactly-once ACP process teardown through the existing host registry (M2 ownership preserved).
- Composition modules avoid new service-locator debt: factory lambdas use the approved `(Func<Type, object?>)sp.GetService` pattern.

## Townhall equal placement

- Backend activity reaches Townhall only through `AgentConversationEventProjection` → `zaide-backend-activity|v1|…` → `TownhallEntryProjection`.
- ACP tool/plan activity uses the same conversation kind, draft/unread semantics, and navigation path as other agent conversations.
- `TownhallViewModel` exposes bounded backend binding and authentication state captions in the existing direct-conversation surface via `AgentBackendBindingPanel`.
- No separate ACP conversation UI or side panel was added.

## Authentication boundary

| Allowed | Forbidden |
|---------|-----------|
| Bounded `authMethods` advertisement and connection-state display (`AgentAuthenticationConnectionState`) | Credential, token, secret collection, persistence, logging, export, or injection |
| Explicit user selection of an advertised auth method ID | Real login, browser flow, OAuth callback, provider request, or account operation |
| `IAcpSessionClient.AuthenticateAsync` protocol surface for conformance | Proxying or storing authentication material |

Capability rows remain truthful across Advertised, Available, Configured, Permitted, Degraded, and CurrentlyUsable. M4 broker mediation and evidence levels are preserved.

## Conformance proof fixture

Repository-owned fake ACP process: `tests/fixtures/acp-fake-agent/`. `Phase20IntegrationTests` exercises the real stdio child process for initialize/prompt conformance. `Phase20TownhallProjectionTests` drives the full session-service and projection path through deterministic `AcpFakeSessionClient` tool-activity updates to avoid multi-minute stdio timeouts in the test gate.

## Test commands and results

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Integration"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20TownhallProjection|FullyQualifiedName~Phase20IdentityBinding"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
git diff --check
```

| Gate | Result |
|------|--------|
| `Phase20Integration` | Passed (2 tests) |
| `Phase20TownhallProjection` / `Phase20IdentityBinding` | Passed (6 tests; serial settings when fast parallel hangs) |
| `Architecture` | Passed (42 tests) |
| `git diff --check` | Passed |

## Architecture inventory ratchet

| Baseline | M5 delta |
|----------|----------|
| 778 total top-level types | 794 (+16) |
| 350 public | 351 (+1 `AgentBackendBindingPanel`) |
| 428 internal | 443 (+15) |

## Stop boundary

M5 audit gate only. M6 adversarial closeout and optional candidate smoke are not started and not authorized. Phase 21 persistence, raw trace, memory, and continuity behavior remain out of scope.
