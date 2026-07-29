# Phase 20 M6 — Adversarial Closeout Evidence

## Publication

| Item | Value |
|------|-------|
| Milestone | M6 — Adversarial coverage and final Phase 20 verification |
| Published commit | This commit (`test(phase-20): close adversarial ACP verification`); post-push verify `HEAD == origin/master` |
| Depends on | M5 at `84469cea40c554a9c306fff056985a5abec0dec4`, publication-record correction `64e672fe75e3b263282dbd6a295663fab574cfd8` |
| Production surfaces | None (M1–M5 surfaces only) |
| Test surfaces | `tests/Zaide.Tests/Features/Agents/Acp/Phase20AdversarialTests.cs`, existing Phase 20 regression suites, architecture ratchets |

## Adversarial coverage map

| Area | Regression anchor |
|------|-------------------|
| M1 schema/framing and bounded parsing | `Phase20ProtocolSchemaConformanceTests`, `Phase20ProtocolFramingTests`, `Phase20ProtocolCancellationTests`, `Phase20ProtocolCapabilityTests`, `Phase20ProtocolBypassTests` |
| M2 process ownership, cancellation, timeout, malformed output, stderr bounds, late completion, process-tree cleanup | `Phase20ProcessLifecycleOwnershipTests`, `Phase20TransportTimeoutCancellationTests`, `Phase20TransportLifecycleTests`, `Phase20TransportStderrBoundaryTests` |
| M3 session correlation, completion/failure normalization, context-manifest consumption, redaction, exclusions, capability truthfulness, identity mismatch, no-fallback | `Phase20BackendTests`, `Phase20ContextTests`, `Phase20CapabilitiesTests`, `Phase20IdentityBindingTests` |
| M4 broker mediation, stale-base before `TryConsume()`, permission denial/revocation, path traversal, malformed arguments, cancellation, terminal rejection | `Phase20ActionBridgeTests`, `Phase20PermissionTests`, `Phase20ActionBridgeBypassTests`, `Phase20AdversarialTests` |
| M5 explicit binding, production composition, Townhall projection, authentication boundary, no credential handling | `Phase20IntegrationTests`, `Phase20TownhallProjectionTests`, `Phase20IdentityBindingTests`, `Phase20AdversarialTests` |
| Direct external activity never labelled Zaide-mediated | `Phase20CapabilitiesTests`, `Phase20TownhallProjectionTests`, `Phase20AdversarialTests` |
| Prompt-injection containment and bounded results | `Phase20ContextTests`, `Phase20AdversarialTests` |
| No Townhall/conversation-store bypass | `Phase20AdversarialTests`, `Phase18ContextBypassRatchetTests` |
| No Native Harness fallback or ACP terminal advertisement | `Phase20IdentityBindingTests`, `Phase20ActionBridgeTests`, `Phase20AdversarialTests` |
| No Phase 21 persistence/resume/raw trace/memory/continuity | `Phase20CapabilitiesTests`, `Phase20AdversarialTests` |

`Phase20Adversarial_M1ThreatModel_RequiredRegressionTestExists` maps 42 named threat rows to live regression tests.

## External candidate smoke

**Not executed:** separate authorization was not provided for registry candidate acquisition, execution, authentication, network, account, or cost. Automated conformance gates were not weakened because external evidence is absent.

## Verification commands and results

Staged verification (interactive terminal; redirected output can reproduce the known parallel-runner hang):

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Adversarial"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Integration|FullyQualifiedName~Phase20TownhallProjection|FullyQualifiedName~Phase20ActionBridge"
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
git diff --check
```

| Gate | Discovery | Result |
|------|-----------|--------|
| `Phase20Adversarial` | 55 | 55 passed, 0 failed |
| `Phase20Integration` / `Phase20TownhallProjection` / `Phase20ActionBridge` | 16 | 16 passed, 0 failed |
| `Architecture` | 43 | 43 passed, 0 failed |
| Full fast suite | 3416 | 3416 passed, 0 failed (~53s) |
| Full serial suite (`slow.runsettings`) | 3416 | 3416 passed, 0 failed (~81s) |
| `git diff --check` | — | clean (recorded at publish) |

## Architecture inventory ratchet

| Baseline | M6 delta |
|----------|----------|
| 794 total top-level types | unchanged |
| 351 public | unchanged |
| 443 internal | unchanged |

## Limitations retained

- Repository-owned fake ACP process only; no registry candidate execution.
- Authentication presentation is bounded; no credential collection, persistence, or real login.
- ACP `session/request_permission` remains an external permission choice, not Phase 17 broker authorization.
- Phase 21 persistence, resume, raw trace, memory, and continuity behavior remain out of scope.

## Stop boundary

M6 implementation and automated verification are complete at publish. **Phase 20 final human acceptance remains a separate gate.** Phase 21 has not started.
