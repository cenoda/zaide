# Phase 20: ACP Agent Backend — TOFIX

## Status

**M0–M6 complete, published, and accepted. Final closeout recorded in `docs(phase-20): accept final closeout`. External candidate smoke remains not executed (separate authorization not provided). Phase 21 later completed and Roadmap V3 is closed.**

Phase 19 remains complete, published, accepted, and closed. Phase 20 is an
independent sibling backend, not a Native Harness wrapper or fallback. M1
delivered ACP v1 and `schema-v1.20.0` codec lock, frozen schema fixtures,
threat-model artifacts, and pure protocol session plumbing only — no Process,
production DI, broker bridge, Townhall/UI, authentication, network provider
execution, Native Harness reference, or new dependency. M2 delivered bounded stdio
process hosting, JSON-RPC lifecycle ownership, deterministic fake-process
transport tests, and `ApplicationShutdown` host teardown. M3 delivered the ACP
backend/session adapter, Phase 18 manifest encoding, backend activity
normalization, and six-fact capability mapping behind deterministic fake
transport only. M4 delivered broker-mediated client filesystem actions, a
separate ACP permission boundary, and `AcpActionCapableAgentBackend` behind
deterministic fake transport only. M5 delivered explicit Actor/backend/runtime
binding, production composition for both backends, equal Townhall placement
through the existing projection path, and a truthful non-credential-handling
authentication boundary behind the repository-owned fake ACP process only. M6
delivered adversarial coverage, `Phase20AdversarialTests`, and
`M6_CLOSEOUT_EVIDENCE.md` with full fast/serial verification. Phase 20 final
human acceptance: **accepted** (recorded in `docs(phase-20): accept final closeout`).
Phase 21 later completed its independent outcome without changing the accepted
Phase 20 sibling-backend boundary.

## M5 work board

- [x] Add typed `AcpRuntimeIdentity`, `AgentActorBackendBinding`, and binding store/selection services.
- [x] Integrate per-actor backend resolution in `AgentExecutionCoordinator` with fail-closed unbound behavior.
- [x] Register ACP production services in `AgentsServiceCollectionExtensions` without silent Native Harness fallback.
- [x] Project backend activity through `AgentConversationEventProjection` and `TownhallEntryProjection`.
- [x] Add bounded backend/auth presentation in the existing Townhall conversation surface.
- [x] Add `Phase20Integration`, `Phase20IdentityBinding`, and `Phase20TownhallProjection` tests.
- [x] Add `M5_INTEGRATION_EVIDENCE.md` and architecture inventory ratchets.
- [x] Publish one reviewable M5 commit to `origin/master` at `84469cea40c554a9c306fff056985a5abec0dec4`, publication-record correction `64e672fe75e3b263282dbd6a295663fab574cfd8`.
- [x] Verify post-push clean state and `HEAD == origin/master`.

## M4 work board

- [x] Add `AcpClientActionBridge`, `AcpClientPermissionBridge`, and `AcpActionCapableAgentBackend`.
- [x] Add workspace absolute-path conversion and truthful filesystem capability profiles.
- [x] Wire inbound handler/capability advertisement through `IAcpSessionClient`.
- [x] Add `Phase20ActionBridge`, `Phase20Permission`, and action-bridge bypass tests.
- [x] Add `M4_ACTION_MEDIATION_EVIDENCE.md` and architecture inventory ratchets.
- [x] Publish one reviewable M4 commit to `origin/master` at `63880c53c2317a4e4d85ade2088c96764c510b6f`.
- [x] Verify post-push clean state and `HEAD == origin/master`.

## M3 work board

- [x] Add `AcpAgentBackend` and `AgentBackendIds.Acp`.
- [x] Add `AcpAgentSessionAdapter`, context encoder, update normalizer, and capability mapper.
- [x] Add additive backend/session activity payload and event normalization in `AgentSessionService`.
- [x] Add `AcpFakeSessionClient` deterministic fake transport tests.
- [x] Add `Phase20Backend`, `Phase20Context`, and `Phase20Capabilities` tests.
- [x] Add `M3_BACKEND_CONTRACT_EVIDENCE.md` and architecture/context bypass ratchets.
- [x] Publish one reviewable M3 commit to `origin/master` at `2d90604991dd9b87cb6e22a2c8c9a7b771504de6`, with publication-record correction `04831f1c`.
- [x] Verify post-push clean state and `HEAD == origin/master`.

## M2 work board

- [x] Implement `AcpStdioProcessHost` with bounded stdin/stdout/stderr and process-tree cleanup.
- [x] Add `IAcpChildProcess` / `IAcpProcessLauncher` contracts and system launcher.
- [x] Add lifecycle failure taxonomy, timeouts, cancellation, and late-response counting.
- [x] Add repository-owned fake child-process fixture under `tests/fixtures/acp-fake-agent/`.
- [x] Add `Phase20Transport` / `Phase20ProcessLifecycle` tests and architecture inventory ratchets.
- [x] Add `M2_PROCESS_LIFECYCLE_EVIDENCE.md`.
- [x] Wire `ApplicationShutdown` to `AcpProcessHostShutdownRegistry.ShutdownAll()`.
- [x] Publish one reviewable M2 commit to `origin/master` at
      `880b4524c9c53190687aee0cc10843900191b8ce`.
- [x] Verify post-push clean state and `HEAD == origin/master`.

## M1 work board

- [x] Add pinned schema fixtures and digest conformance tests.
- [x] Implement ACP v1 JSON-RPC envelopes, wire DTOs, codec, and newline framing.
- [x] Implement pure protocol session plumbing (`initialize`, `session/new`,
      `session/prompt`, `session/cancel`, `session/update`, `$/cancel_request`).
- [x] Implement truthful M1 client capability profile (`terminal: false`,
      filesystem flags false).
- [x] Add inbound client request router rejecting unsupported methods.
- [x] Add `M1_SCHEMA_CONFORMANCE.md` and `M1_THREAT_MODEL.md`.
- [x] Add focused `Phase20Protocol` tests and architecture inventory ratchets.
- [x] Publish one reviewable M1 commit at
      `314076ebc8dcf2c9910baecc5ef96c461910cb1b`.
- [x] Verify post-push clean state and `HEAD == origin/master`.

## M0 work board

- [x] Read all required repository guidance and current Phase 19 plan/work
      board completely.
- [x] Verify clean `master` planning baseline at
      `4e0c89162b33547e3461aa4b2f845bb7cbbb1314`, synchronized with
      `origin/master`.
- [x] Inspect live Phase 15 session/event seams.
- [x] Inspect live Phase 17 broker, permission, stale-revision, audit, and
      workspace-action seams.
- [x] Confirm `AgentPermissionDecision.TryConsume()` remains the final
      authorization step and stale proposals do not consume a published
      decision.
- [x] Inspect live Phase 18 context-manifest, exclusion, redaction, budget, and
      disclosure seams.
- [x] Inspect Phase 19 Native Harness boundaries without modifying or reopening
      Phase 19.
- [x] Trace the sole Townhall projection path through
      `AgentConversationEventProjection`.
- [x] Inspect production dependencies and architecture ratchets.
- [x] Confirm no Phase 20 ACP production, test, tool, dependency, DI, or UI
      implementation exists.
- [x] Research public primary official ACP sources without credentials.
- [x] Pin stable ACP wire version `1` and schema artifact
      `schema-v1.20.0` at commit
      `5e89c71497fe07dd4ae633c181a17224f4a8956d`.
- [x] Record stable schema and metadata digests.
- [x] Lock local stdio transport and no-SDK/no-new-dependency decision.
- [x] Record exact method/update/content/tool/capability/authentication profile
      and the ACP v2/website-schema ambiguity.
- [x] Record official-registry candidate versions as provenance research only;
      no candidate was installed, executed, initialized, authenticated, or
      prompted.
- [x] Define M1-M6 allowed/forbidden surfaces, artifacts, gates, rollback, and
      stop conditions without starting them.
- [x] Stage exactly the intended M0 documentation/status files.
- [x] Run the requested staged verification gates.
- [x] Publish one reviewable M0 documentation commit at
      `0bb44c85b743dee9dc1c8f18553097fd4d4a8ca7`.
- [x] Verify clean post-push state and `HEAD == origin/master`.

## Locked M0 decisions

| Concern | Decision |
|---------|----------|
| Protocol | ACP stable wire version `1` only |
| Schema | `schema-v1.20.0`; stable `schema.json` SHA-256 `92c1dfcda10dd47e99127500a3763da2b471f9ac61e12b9bf0430c32cf953796` |
| Metadata | stable `meta.json` SHA-256 `e0bf36f8123b2544b499174197fdc371ec49a1b4572a35114513d56492741599` |
| SDK | None; no official .NET SDK exists and community SDKs are not adopted |
| Transport | Local child-process stdio; UTF-8 newline-delimited JSON-RPC 2.0 |
| Drafts | ACP v2, unstable schema, Streamable HTTP, WebSocket, and custom transports excluded |
| Actions | `fs/read_text_file` and `fs/write_text_file` may be advertised only after Phase 17 broker mediation passes |
| Terminal | `terminal: false`; current broker command result cannot represent the full ACP terminal lifecycle |
| Authentication | Agent-owned; stable agent auth only; explicit user action required; Zaide never handles credentials/tokens |
| Identity | Explicit Actor/backend/runtime binding; ACP/process/provider IDs never become Actor IDs |
| Townhall | Equal existing conversation placement; sole write path remains `AgentConversationEventProjection` |
| Fallback | None; ACP failure never silently selects Native Harness |
| Continuity | load/list/delete/resume/close product behavior deferred to Phase 21 |

## Live M0 findings

- `AgentSessionService` can index multiple backends, but
  `AgentExecutionCoordinator` still owns one fixed backend ID and defaults to
  the legacy backend ID. Phase 20 M5 must add an explicit per-Actor backend
  binding rather than treating a second DI registration as product selection.
- ACP `session/request_permission` lacks the canonical workspace/action
  fingerprint and revision binding needed by Phase 17. It is an external-agent
  permission choice, not Zaide action authorization.
- ACP `terminal: true` promises create/output/wait/kill/release. Phase 17's
  bounded synchronous `ExecuteCommand` is not equivalent, so terminal support
  is excluded instead of overstated.
- The ACP website on 2026-07-28 documents features absent from the latest
  stable released schema asset. The stable asset is authoritative for Phase
  20; later stable releases require explicit amendment.
- An external ACP process has the OS rights of that process. Direct actions may
  be backend-reported, externally observed, or unobservable even when Zaide
  offers mediated client filesystem methods.

## Candidate provenance snapshot

Read-only official registry snapshot on 2026-07-28:

- `codex-acp` `1.1.7` — Apache-2.0 registry entry.
- `claude-acp` `0.63.0` — registry marks proprietary.
- `gemini` `0.52.0` — Apache-2.0 registry entry.

These are not compatibility results. No acquisition, execution, login,
credential use, network provider request, subscription check, or paid call
occurred.

## M5 verification evidence

Publication commit `84469cea40c554a9c306fff056985a5abec0dec4` (publication-record correction `64e672fe75e3b263282dbd6a295663fab574cfd8`) on `origin/master`:

- `git diff --cached --check`: passed with no output at publication time.
- `dotnet build Zaide.slnx --no-restore`: passed; 0 warnings, 0 errors.
- `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Integration"`: passed; 2 discovered, 0 failed.
- `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20TownhallProjection|FullyQualifiedName~Phase20IdentityBinding"`: passed; 6 discovered, 0 failed (serial settings when fast parallel hangs).
- `dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"`: passed; 42 discovered, 0 failed.
- `git diff --check`: passed with no output after gates.
- Post-push: working tree clean; `HEAD == origin/master`.

## M6 work board

- [x] Add `Phase20AdversarialTests` covering M1–M5 threat-model rows and boundary ratchets.
- [x] Add `M6_CLOSEOUT_EVIDENCE.md` with coverage map, limitations, and external smoke not executed.
- [x] Run M6 verification gates (adversarial, integration/Townhall/action-bridge, architecture, full fast, full serial).
- [x] Publish one reviewable commit: `test(phase-20): close adversarial ACP verification`.
- [x] Verify post-push clean state and `HEAD == origin/master`.
- [x] Record explicit human acceptance: `docs(phase-20): accept final closeout`.

## Next task

Phase 20 M0–M6 is complete, published, and accepted. Phase 21 later completed,
and Roadmap V3 is complete and closed. No successor roadmap is authorized.
