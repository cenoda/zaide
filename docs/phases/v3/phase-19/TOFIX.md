# Phase 19: Zaide Native Harness Backend — TOFIX

## Status

M0 was accepted by the user on 2026-07-27. M1 research/provenance is **complete
with limitation** (the full-corpus benchmark gate was retired by explicit
user-directed plan amendment on 2026-07-27). **M2 harness contracts and
architecture lock is complete** (read-only audit gate). **M3 tool-calling
execution loop is complete** (read-only audit gate). **M4 production wiring and
capability truthfulness is complete** at a corrective closeout (read-only audit
gate). **M5 Townhall structured activity projection is complete** (read-only
audit gate). **M6 adversarial closeout is complete and published** (commits
`4b4e1914`, `ca896498`). **Phase 19 final human acceptance: accepted.**
Phase 19 is complete, published, and closed.
Phases 20 and 21 later completed their independent outcomes, and Roadmap V3 is
complete and closed. Publication is not pending.

### M6 corrective closeout publication (2026-07-28)

Commit `4b4e1914` (`fix(phase-19): close M6 test lifecycle leak`) is published
on `origin/master`. Commit `ca896498`
(`docs(phase-19): record M6 corrective closeout publication`) records the
publication. The corrective change tracks and disposes `SettingsService`
instances before temporary-directory cleanup, resolving the full-suite hang
caused by accumulated undisposed `LongRunning` background writer tasks.

Post-push verification confirmed:
- `HEAD == origin/master`
- Clean working tree; no unrelated production, test, or tool changes
- `dotnet build Zaide.slnx --no-restore` — 0 errors, 0 warnings
- `Phase19Adversarial` — 40 discovered, 40/40 passed
- `Phase19Integration` — 5 discovered, 5/5 passed
- `Phase19TownhallProjection` — 4 discovered, 4/4 passed
- `Architecture` — 41 discovered, 41/41 passed
- Full fast suite — 3292/3292 passed
- Full serial suite — 3292/3292 passed
- No Phase 20 or Phase 21 files exist

**Phase 19 M0–M6 implementation is complete. M6 is published. Phase 19
final human acceptance is accepted.**

### M4 corrective closeout (2026-07-27)

The first M4 commit wired `AddZaideAgents` with
`AddSingleton<IService, Concrete>(sp => (Concrete)sp.GetService(typeof(Concrete))!)`
for both `IAgentExecutionService → AgentExecutionService` and
`IAgentBackend → NativeHarnessAgentBackend`. The production container crashed
at startup because that registration shape did not honor the
`BuildServiceProvider` capture-context indirection and the cast threw during
composition. The corrective commit:

- keeps the concrete `AgentExecutionService` Singleton registration;
- rewrites the `IAgentExecutionService` mapping as a factory that resolves
  the same concrete instance;
- rewrites the `IAgentBackend` mapping as a factory that constructs
  `NativeHarnessAgentBackend` from its declared dependencies;
- adds the explicit production-container resolution regression test
  `Program_ConfigureServices_ResolvesExecutionCoordinatorAndNativeHarnessDependenciesWithoutTestReplacementsOrNetwork`
  in `AgentsRegistrationModuleTests` (zero test-replacement fakes, zero network
  egress);
- does not re-register `LegacyOpenAiCompatibleAgentBackend`;
- does not change Phase 17 or Phase 18 contracts.

The recorded totals at the corrective closeout are:

- `dotnet build Zaide.slnx --no-restore` — succeeded, 0 errors, 0 warnings
- `Phase19Integration` list-tests — 5 tests discovered; run — 5/5 passed
- `Architecture` list-tests — 37 tests discovered; run — 37/37 passed
- Full fast suite — 3244/3244 passed
- Serial fallback — 3244/3244 passed

M4 corrective closeout work is complete. **M5 is complete at a read-only audit
gate. M6 adversarial closeout is complete and published** (commits `4b4e1914`,
`ca896498`); Phase 19 final human acceptance: accepted.

## Amendment — M1 full-corpus benchmark gate retired (2026-07-27)

The original M1 requirement that at least two candidates complete the entire
common corpus is retired by explicit user-directed plan amendment. The local
model capability is insufficient for this campaign to produce meaningful
architectural evidence. M1 closes as a research/provenance milestone. The
retained evidence satisfies the research/provenance gate. No architecture winner
was selected. Do not perform another full-corpus chase.

## Retained evidence status

M1 research/provenance evidence is retained in:
- `M1_RESEARCH_RECORD.md` — candidate identities, task-loop/context/search/
  edit/tool/recovery/compaction observations, candidate-selection rules, frozen
  comparable repository-task corpus, held-out tasks, exact commands, isolation/
  reset evidence, execution results, blockers, rejections, evidence references,
  and the gate-verdict table confirming the research/provenance criteria are met.
- `M1_PROVENANCE.md` — provenance for every code, dependency, binary, asset,
  prompt, fixture, corpus, generated, translated, or adapted material considered
  for reuse, including source URL, exact commit/release, paths or ranges,
  license, NOTICE/copyright obligations, modifications, and reuse decision.
- Evidence under `/var/tmp/zaide-m1-reconstruct/evidence/{qwen,opencode,grok}/`.

## Retained limitations

The following limitations remain visible:
- Qwen, OpenCode, and Grok did not complete all eight tasks green; the full-
  corpus campaign is a documented limitation of the local model/execution
  environment.
- No failed, timed-out, malformed, or runner-defective evidence is rewritten as
  successful.
- The initial Qwen/OpenCode held-out hash rows and the retry runner's
  post-OpenCode-H3 syntax error are recorded as runner/evidence defects, not
  misconduct or candidate qualification.
- No architecture winner was selected at M1. M1 runtime observations are
  informative research evidence only and do not select a winning external
  architecture.

## M2 artifacts

- `M2_ARCHITECTURE_LOCK.md` — resolved M2-owned decisions, internal contract
  boundary, six-fact capability rows, prior-conversation replay seam, event-
  surface decision (reuse broker-event path), provider/protocol lock.
- `M2_THREAT_MODEL.md` — production threat model gating M3 tool execution.

## M2 contract/domain types (production)

Under `src/Features/Agents/Contracts/`:
- `INativeHarnessPriorConversationReader`
- `NativeHarnessPriorConversationReplayRequest`
- `INativeHarnessProviderTransport`
- `INativeHarnessProviderOptionsSource`

Under `src/Features/Agents/Domain/`:
- `AgentBackendIds.NativeHarness` identity
- In-run loop history: `NativeHarnessLoopHistory`, record hierarchy
- Tool-call representation: `NativeHarnessToolCallId`, `NativeHarnessToolCallDescriptor`,
  `NativeHarnessToolCallRecord`, `NativeHarnessToolResultRecord`
- Provider transport: `NativeHarnessChatMessage`, `NativeHarnessProviderRequest`,
  `NativeHarnessProviderResponse`, `NativeHarnessProviderToolCall`
- Termination/cancellation: `NativeHarnessRunOutcome`, `NativeHarnessRunTerminationKind`,
  `NativeHarnessCancellationState`, `NativeHarnessLateCompletionDisposition`,
  `NativeHarnessTurnBudget`, `NativeHarnessTurnPhase`
- Prior replay: `NativeHarnessPriorConversationReplayEntry`,
  `NativeHarnessPriorConversationReplayPolicy`
- Capability rows: `NativeHarnessCapabilityRows`
- Protocol constants: `NativeHarnessProviderProtocol`

## M3 implementation (production)

Under `src/Features/Agents/Application/`:
- `NativeHarnessLoopRunner` — turn loop, broker dispatch, cancellation, turn budget
- `NativeHarnessSystemPromptBuilder` — Phase 18 manifest consumption
- `NativeHarnessPriorConversationReader` — bounded replay seam
- `NativeHarnessToolArgumentMapper` — OpenAI tool JSON → Phase 17 payloads
- `NativeHarnessToolResultFormatter` — bounded/sanitized tool-result summaries

Under `src/Features/Agents/Infrastructure/`:
- `NativeHarnessAgentBackend` — `IAgentActionRequestCapableBackend`
- `NativeHarnessProviderClient` — SSE `/chat/completions` transport
- `NativeHarnessSseReader` — incremental SSE parsing
- `NativeHarnessProviderOptionsSource` — live provider options resolution

## M4 implementation (production)

Under `src/App/Composition/Registration/`:
- `AgentsServiceCollectionExtensions` — registers `NativeHarnessAgentBackend` as
  the sole production `IAgentBackend`; registers `INativeHarnessProviderTransport`,
  `INativeHarnessProviderOptionsSource`, and `INativeHarnessPriorConversationReader`

Production activation:
- Action-capable runs resolve `ContractAgentActionBroker` (not
  `UnavailableAgentActionBroker`)
- Six-fact capability rows truthful for `Tools`, `Permissions`, `IdeContext`,
  `Streaming`, `Cancellation`, and `MessageCompletion`
- `LegacyOpenAiCompatibleAgentBackend` remains in source for tests/reference but
  is not production-registered

## Baseline

- Pre-plan commit: `8eed91d3` (Phase 18 M6 closeout, 2026-07-27).
- Published M2 baseline (historical): 667 total / 350 public / 317 internal
  top-level types; 606 source files / 561 Features files.
- Post-M3/M4 architecture inventory: 682 total / 350 public / 332 internal
  top-level types; 621 source files / 576 Features files (unchanged by M4 wiring).

## Current work

- [x] M0 plan creation and live-seam verification.
- [x] M0 audit corrective pass and user acceptance (2026-07-27).
- [x] M1 research/provenance complete with limitation.
- [x] M2 architecture lock document (`M2_ARCHITECTURE_LOCK.md`).
- [x] M2 production threat model (`M2_THREAT_MODEL.md`).
- [x] M2 contract/domain types and `Phase19Contracts` tests.
- [x] Architecture inventory ratchet update for M2 types.
- [x] M3 — tool-calling execution loop.
- [x] Architecture inventory ratchet update for M3 types (682/350/332, 621/576).
- [x] M4 — production wiring and capability truthfulness.
- [x] M4 corrective closeout — production-container resolution crash fixed; explicit
      production-container resolution regression test added; full fast suite
      3244/3244 passed; serial suite 3244/3244 passed.
- [x] M5 — Townhall structured activity projection through the existing
      broker-event path; honest evidence-level presentation; no new
      `AgentEventKind` or direct Townhall bypass.
- [x] M6 — adversarial closeout exercising `M2_THREAT_MODEL.md`; final
      architecture and bypass ratchet state; full fast and serial suite
      verification; real-repository evaluation evidence without comparative
      benchmark claims.

## Next task

**Phase 19 M0–M6 implementation is complete. M6 is published** (commits
`4b4e1914`, `ca896498`). **Phase 19 final human acceptance: accepted.**
Phase 19 is complete, published, and closed.
Phases 20 and 21 later completed their independent outcomes. Roadmap V3 is
complete and closed; no successor roadmap is authorized.

## M2 acceptance and publication

M2 artifacts are **accepted and published**. Commits `c23f9666`, `a59d7105`, and
`19b2ce27` are published; master is synchronized with origin/master. M2 document
acceptance is complete.

## M3 verification (2026-07-27)

Run in an interactive terminal:

```bash
git status --short --branch
git diff --name-only -- src tests tools
dotnet build Zaide.slnx --no-restore
git diff --check
dotnet test Zaide.slnx --no-build --list-tests \
  --filter 'FullyQualifiedName~Phase19ToolLoop'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Phase19ToolLoop'
dotnet test Zaide.slnx --no-build --list-tests \
  --filter 'FullyQualifiedName~Phase19BrokerDispatch'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Phase19BrokerDispatch'
dotnet test Zaide.slnx --no-build --list-tests \
  --filter 'FullyQualifiedName~Phase19ContextConsumption'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Phase19ContextConsumption'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Phase19ToolLoop|FullyQualifiedName~Phase19BrokerDispatch|FullyQualifiedName~Phase19ContextConsumption'
```

| Command | Result (2026-07-27) |
|---------|---------------------|
| `dotnet build Zaide.slnx --no-restore` | Succeeded, 0 errors, 0 warnings |
| `git diff --check` | Clean |
| `Phase19ToolLoop` list-tests | 8 tests discovered |
| `Phase19ToolLoop` test run | 8/8 passed |
| `Phase19BrokerDispatch` list-tests | 6 tests discovered |
| `Phase19BrokerDispatch` test run | 6/6 passed |
| `Phase19ContextConsumption` list-tests | 5 tests discovered |
| `Phase19ContextConsumption` test run | 5/5 passed |
| Combined M3 filter | 19/19 passed |

## M3 architecture verification (2026-07-27)

```bash
dotnet test Zaide.slnx --no-build --list-tests \
  --filter 'FullyQualifiedName~Architecture'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Architecture'
```

| Command | Result (2026-07-27) |
|---------|---------------------|
| `Architecture` list-tests | 37 tests discovered |
| `Architecture` test run | 37/37 passed |

## M4 verification (2026-07-27)

```bash
git add <M4-files>
git diff --cached --check
dotnet test Zaide.slnx --no-build --list-tests \
  --filter 'FullyQualifiedName~Phase19Integration'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Phase19Integration'
dotnet test Zaide.slnx --no-build --list-tests \
  --filter 'FullyQualifiedName~Architecture'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Architecture'
dotnet build Zaide.slnx --no-restore
```

| Command | Result (2026-07-27) |
|---------|---------------------|
| `Phase19Integration` list-tests | 5 tests discovered |
| `Phase19Integration` test run | 5/5 passed |
| `Architecture` list-tests | 37 tests discovered |
| `Architecture` test run | 37/37 passed |
| `dotnet build Zaide.slnx --no-restore` | Succeeded, 0 errors, 0 warnings |

## M4 corrective closeout verification (2026-07-27)

The corrective commit fixed the production-container resolution crash by
rewriting the two M4 wiring registrations as factories and adding the
explicit production-container resolution regression test. The same M4
verification commands above now run on the production container with zero
test-replacement fakes and zero network egress. The full fast and serial
suites are also recorded for the corrective closeout:

| Command | Result (2026-07-27) |
|---------|---------------------|
| `dotnet test Zaide.slnx --no-build` (fast suite) | 3244/3244 passed |
| `dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings` (serial suite) | 3244/3244 passed |

## M5 verification (2026-07-28)

```bash
git add <M5-files>
git diff --cached --check
dotnet test Zaide.slnx --no-build --list-tests \
  --filter 'FullyQualifiedName~Phase19TownhallProjection'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Phase19TownhallProjection'
dotnet test Zaide.slnx --no-build --list-tests \
  --filter 'FullyQualifiedName~Architecture'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Architecture'
dotnet build Zaide.slnx --no-restore
```

| Command | Result (2026-07-28) |
|---------|---------------------|
| `Phase19TownhallProjection` list-tests | 4 tests discovered |
| `Phase19TownhallProjection` test run | 4/4 passed |
| `Architecture` list-tests | 37 tests discovered |
| `Architecture` test run | 37/37 passed |
| `dotnet build Zaide.slnx --no-restore` | Succeeded, 0 errors, 0 warnings |

## M6 verification (2026-07-28)

```bash
git add tests/Zaide.Tests/Features/Agents/Phase19AdversarialTests.cs \
  tests/Zaide.Tests/Architecture/Phase17BypassRatchetTests.cs \
  tests/Zaide.Tests/Architecture/Phase18ContextBypassRatchetTests.cs \
  docs/phases/v3/phase-19/IMPLEMENTATION_PLAN.md \
  docs/phases/v3/phase-19/TOFIX.md \
  README.md docs/phases/README.md docs/architecture/OVERVIEW.md docs/roadmap/V3.md
git diff --cached --check
dotnet test Zaide.slnx --no-build --list-tests \
  --filter 'FullyQualifiedName~Phase19Adversarial'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Phase19Adversarial'
dotnet test Zaide.slnx --no-build --list-tests \
  --filter 'FullyQualifiedName~Architecture'
dotnet test Zaide.slnx --no-build \
  --filter 'FullyQualifiedName~Architecture'
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build \
  --settings tests/Zaide.Tests/slow.runsettings
```

| Command | Result (2026-07-28) |
|---------|---------------------|
| `Phase19Adversarial` list-tests | 40 tests discovered |
| `Phase19Adversarial` test run | 40/40 passed |
| `Architecture` list-tests | 41 tests discovered |
| `Architecture` test run | 41/41 passed |
| Full fast suite | 3292/3292 passed |
| Serial fallback | 3292/3292 passed |

## Unresolved decisions (post-M6)

## M6 corrective closeout — full-suite hang (2026-07-28)

The original M6 adversarial closeout passed all targeted gates
(Phase19Adversarial: 40/40, Phase19Integration: 5/5,
Phase19TownhallProjection: 4/4, Architecture: 41/41) but the full fast and
serial suites both failed to complete.

### Root cause

`SettingsService` (`src/Features/Settings/Infrastructure/SettingsService.cs`)
implements `IDisposable` and starts a `LongRunning` background writer loop task
on construction. `Phase19HarnessTestFactory.CreateExecutionService()` creates a
new `SettingsService` for each call. The four Phase19 test files that use this
factory — `Phase19AdversarialTests` (2 call sites),
`Phase19ToolLoopTests` (1 call site), `Phase19BrokerDispatchTests` (1 call site),
and `Phase19ContextConsumptionTests` (1 call site) — never disposed the created
`SettingsService` instances. Each test class's `Dispose()` only deleted the temp
directory, leaving the background writer loops running with references to
now-deleted file paths.

Across the full ~3290+ test suite, these undisposed `LongRunning` tasks
accumulated and exhausted the thread pool, causing the test runner to hang.

### Corrective change

- `Phase19TestSupport.cs`: `CreateExecutionService` now accepts an optional
  `IList<IDisposable>? disposableTracker` parameter. When non-null, the
  created `SettingsService` is added to the tracker before returning.
- `Phase19AdversarialTests.cs`, `Phase19ToolLoopTests.cs`,
  `Phase19BrokerDispatchTests.cs`, `Phase19ContextConsumptionTests.cs`: each
  test class now stores a `List<IDisposable>`, passes it as the
  `disposableTracker` to every `CreateExecutionService` call, and disposes all
  tracked instances in `Dispose()` **before** deleting the temp directory.

No test assertions, test count, or threat-model coverage was changed. The
existing `SettingsService` disposal contract (`IDisposable`) is now honored.

### Scope guard

Changed files: `Phase19TestSupport.cs`, `Phase19AdversarialTests.cs`,
`Phase19ToolLoopTests.cs`, `Phase19BrokerDispatchTests.cs`,
`Phase19ContextConsumptionTests.cs`, and this `TOFIX.md`. No production code,
Phase 15/17/18 contracts, architecture ratchets, or dependencies were modified.
No tests were removed, weakened, or skipped. No parallelism was disabled.

### Recorded totals at M6 corrective closeout (2026-07-28)

| Command | Result |
|---------|--------|
| `git diff --cached --check` | Clean |
| `git diff --cached --name-only` | 6 files (see scope guard above) |
| `dotnet build Zaide.slnx --no-restore` | Succeeded, 0 errors, 0 warnings |
| `Phase19Adversarial` list-tests | 40 tests discovered |
| `Phase19Adversarial` test run | 40/40 passed |
| `Phase19Integration` list-tests | 5 tests discovered |
| `Phase19Integration` test run | 5/5 passed |
| `Phase19TownhallProjection` list-tests | 4 tests discovered |
| `Phase19TownhallProjection` test run | 4/4 passed |
| `Architecture` list-tests | 41 tests discovered |
| `Architecture` test run | 41/41 passed |
| `Phase19ToolLoop\|Phase19BrokerDispatch\|Phase19ContextConsumption` test run | 19/19 passed |
| Full fast suite | 3292/3292 passed (14 s) |
| Serial fallback | 3292/3292 passed (49 s) |

All M2-owned open decisions are resolved in `M2_ARCHITECTURE_LOCK.md`. Phase 19
M0–M6 implementation is complete. M6 is published. Phase 19 final human
acceptance: accepted.

## M1 authorization (2026-07-27)

Closed. See `M1_RESEARCH_RECORD.md` and historical TOFIX sections in git history.
