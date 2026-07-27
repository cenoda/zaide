# Phase 19: Zaide Native Harness Backend — TOFIX

## Status

M0 was accepted by the user on 2026-07-27. M1 research/provenance is **complete
with limitation** (the full-corpus benchmark gate was retired by explicit
user-directed plan amendment on 2026-07-27). **M2 harness contracts and
architecture lock is complete** (read-only audit gate). **M3 tool-calling
execution loop is complete** (read-only audit gate; M4 not started).

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
- `NativeHarnessAgentBackend` — `IAgentActionRequestCapableBackend` (not production-registered; M4)
- `NativeHarnessProviderClient` — SSE `/chat/completions` transport
- `NativeHarnessSseReader` — incremental SSE parsing
- `NativeHarnessProviderOptionsSource` — live provider options resolution

## Baseline

- Pre-plan commit: `8eed91d3` (Phase 18 M6 closeout, 2026-07-27).
- Architecture inventory updated for M2 types: 667 total / 350 public / 317 internal
  top-level types (git-tracked source file count unchanged until commit).

## Current work

- [x] M0 plan creation and live-seam verification.
- [x] M0 audit corrective pass and user acceptance (2026-07-27).
- [x] M1 research/provenance complete with limitation.
- [x] M2 architecture lock document (`M2_ARCHITECTURE_LOCK.md`).
- [x] M2 production threat model (`M2_THREAT_MODEL.md`).
- [x] M2 contract/domain types and `Phase19Contracts` tests.
- [x] Architecture inventory ratchet update for M2 types.
- [x] M3 — tool-calling execution loop.

## Next task

M3 is complete at a read-only audit gate. **M4 is next but has not started.**
Do not register the Native Harness backend in `AddZaideAgents` or implement
production capability truthfulness until M4 is authorized.

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

## Unresolved decisions (post-M3)

All M2-owned open decisions are resolved in `M2_ARCHITECTURE_LOCK.md`. Remaining
work is implementation-owned:

| Item | Owner |
|------|-------|
| Production DI registration | M4 |
| Capability truthfulness | M4 |
| Townhall richer rendering (if needed) | M5 |
| Adversarial threat exercises | M6 |
| Evaluation scope at closeout | M6 |

## M1 authorization (2026-07-27)

Closed. See `M1_RESEARCH_RECORD.md` and historical TOFIX sections in git history.
