# Phase 20: ACP Agent Backend — TOFIX

## Status

**M1 complete and published at `314076ebc8dcf2c9910baecc5ef96c461910cb1b`. M2
and all later Phase 20 milestones are not started and not authorized. Phase 21
has not started.**

Phase 19 remains complete, published, accepted, and closed. Phase 20 is an
independent sibling backend, not a Native Harness wrapper or fallback. M1
delivered ACP v1 and `schema-v1.20.0` codec lock, frozen schema fixtures,
threat-model artifacts, and pure protocol session plumbing only — no Process,
production DI, broker bridge, Townhall/UI, authentication, network provider
execution, Native Harness reference, or new dependency. M2 owns bounded stdio
process hosting. M2 and all later Phase 20 milestones are not authorized.
Phase 21 has not started.

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

## M0 verification evidence

Final staged scope:

- `README.md`
- `docs/architecture/OVERVIEW.md`
- `docs/phases/README.md`
- `docs/phases/v3/phase-20/IMPLEMENTATION_PLAN.md`
- `docs/phases/v3/phase-20/TOFIX.md`
- `docs/roadmap/V3.md`

Verification on 2026-07-28:

- `git diff --cached --check`: passed with no output.
- `git diff --cached --name-only`: the six documentation files above.
- `git diff --cached --name-only -- src tests tools`: empty.
- `dotnet build Zaide.slnx --no-restore`: passed; 0 warnings, 0 errors.
- `dotnet test Zaide.slnx --no-build --filter
  "FullyQualifiedName~Architecture"`: passed; 41 discovered, 41 passed, 0
  failed, 0 skipped.
- Default fast suite: not run because this documentation-only plan does not
  require it and the targeted gate exposed no regression.

M0 publication and post-push verification are complete at
`0bb44c85b743dee9dc1c8f18553097fd4d4a8ca7`. M1 publication and post-push
verification are complete at `314076ebc8dcf2c9910baecc5ef96c461910cb1b`.

## M1 verification evidence

Publication commit `314076ebc8dcf2c9910baecc5ef96c461910cb1b` on `origin/master`:

- `git diff --cached --check`: passed with no output at publication time.
- `dotnet build Zaide.slnx --no-restore`: passed; 0 warnings, 0 errors.
- `dotnet test Zaide.slnx --no-build --filter
  "FullyQualifiedName~Phase20Protocol"`: passed; at least one test discovered,
  zero failures.
- `dotnet test Zaide.slnx --no-build --filter
  "FullyQualifiedName~Architecture"`: passed; at least one test discovered,
  zero failures.
- Post-push: working tree clean; `HEAD == origin/master`.

## Next task

Stop at the read-only corrective re-audit gate. M2 and all later Phase 20
milestones are not authorized. Do not begin M2.
