# Refactor 9: Test Execution Structure — TOFIX

## Status

M0 live audit is complete. The clean baseline is commit `e1873ce8` on
`perf/test-structure-phase1`. M1, M2a, M2b, M2c, M2d, M2e, M3, M4, and M5 are
implemented. Final closeout verification (2026-07-26) passed all gates; Refactor
9 is ready for PR.

## Current findings

- Phase 16 and Linux PTY tests already have isolated collections, but their
  external-resource nature is not selectable through a common trait.
- Production DAP proof tests start adapter and fixture processes when their
  prerequisites are present, but are not isolated from one another by a shared
  collection.
- `Task.Delay` polling, repeated ReactiveUI bootstrap, filesystem setup, and
  gate contention remain separate follow-up slices.

## M1 result (2026-07-26)

- Added the `SlowIntegration` category to Phase 16 sandbox, Linux PTY, and
  production DAP proof tests.
- Added the shared `SlowExternalResources` collection with parallelization
  disabled for DAP/process proof classes.
- The slow selection passed 36/36 in 9.663 seconds.
- The ordinary selection passed 3029/3029 in 9.127 seconds.
- The unfiltered regression could not be completed because the shared host
  already contained seven stale testhost/vstest processes and three unrelated
  external processes; the attempted run left those counts unchanged.

The two filtered selections cover all 3065 discovered tests. The unfiltered
gate must be repeated on a clean test host before treating the refactor as
fully closed.

## M2a result (2026-07-26)

- Replaced fixed `Task.Delay` waits in `ProjectContextServiceTests` and
  `ProjectContextServiceIntegrationTests` with snapshot-state, snapshot-count,
  and actual-task completion signals.
- Focused project-system gate: 48/48 passed in 0.569 seconds.
- No fixed-delay polling remains in either project-context test file.

The remaining polling inventory is intentionally not part of M2a. The next
slice is the agent-session/application polling cluster, followed by language
service polling.

## M2b result (2026-07-26)

- Replaced the agent-session admission delay with the typed running-session
  signal.
- Replaced cancellation delays in agent execution and legacy backend tests
  with backend/read-start completion signals.
- Focused agent gate: 94/94 passed in 7.598 seconds.

## M2c result (2026-07-26)

- Converted the parity test `WaitUntilAsync` and `WaitForRunningAsync` helpers
  from fixed-interval polling to `IAgentSessionService.Events` signals.
- Added explicit second-run admission completion signals where the observed
  state change and test variable assignment had a race.
- Focused parity gate: 28/28 passed in 7 seconds.
- No `Task.Delay` remains in `AgentSessionCoordinatorParityTests`.

## M2d result (2026-07-26)

- Converted language navigation cancellation and language symbol dismissal
  waits to language-service change-stream signals.
- Focused language navigation/symbol gate: 32/32 passed.
- Stale-response tests retain bounded delays because those production paths can
  complete without publishing a terminal snapshot; replacing those waits
  requires a request-completion seam rather than a test-only polling trick.

## M2e result (2026-07-26)

- Added `ILanguageNavigationService.WhenRequestCompleted` and completion
  notification from `LanguageNavigationService.ExecuteRequestAsync` so silent
  stale discards still signal request completion without changing surface
  behavior.
- Replaced navigation stale-generation, stale-version, and active-tab stale
  `Task.Delay(150)` waits with request-completion subscriptions registered
  before triggering completion.
- Replaced symbol stale-generation and stale-version waits with
  `WaitForChangeAsync` on the existing idle/cancelled snapshot stream; kept
  workspace debounce delay in workspace-symbol replacement tests.
- Focused language navigation/symbol gate: 32/32 passed in 1 second.
- Ordinary selection: 3029/3029 passed in 11 seconds.
- Slow integration selection: 36/36 passed in 10 seconds.
- Combined coverage: 3065/3065 passed with 0 failures.
- Testhost count remained 0 before and after; external-process count remained
  2 before and after.
- `git diff --check` passed.

## M3 result (2026-07-26)

- Added `ReactiveUiTestBootstrap` (`ModuleInitializer` + idempotent
  `EnsureInitialized` / `EnsureApplication` / default Splat registration) under
  `tests/Zaide.Tests/Infrastructure/`.
- Moved `AvaloniaUiInitializationCollection` to Infrastructure with
  `AvaloniaUiInitializationFixture` and `ReactiveUiMutableStateResetAttribute`
  for serialized settings UI tests.
- Removed 81 duplicate `BuildApp()` call sites (82 → 1 deliberate bootstrap).
- Consolidated 15 private `EnsureApplication()` copies into the shared bootstrap.
- Preserved temp-directory static initialization in 24 classes that mixed
  bootstrap with fixture setup.
- Settings UI gate: 4/4 passed in 0.288 seconds.
- App.Shell gate: 134/134 passed in 0.780 seconds.
- Ordinary selection: 3029/3029 passed in 7 seconds (prior 9–11 seconds).
- Slow integration selection: 36/36 passed in 9 seconds (prior 9–10 seconds).
- Combined coverage: 3065/3065 passed with 0 failures.
- Testhost count remained 0 before and after; no leaked Avalonia/testhost
  processes observed after the gates.
- `git diff --check` passed.

## M4 result (2026-07-26)

- Added `TestFixturePaths`, `TestFilesystem.SharedReadOnlyWorkspaceRoot`, and
  `TestTempDirectory` under `tests/Zaide.Tests/Infrastructure/`.
- Replaced 24 per-class `TempRoot` + static constructor allocations: 12
  path-only suites now share the committed `tests/fixtures` root; 12 writable
  suites use per-test `TestTempDirectory` with `IDisposable` cleanup.
- Consolidated five SourceControl `CreateTempDir()` copies onto
  `TestTempDirectory`; removed redundant manual `Directory.Delete` finally
  blocks where disposal now owns cleanup.
- Pointed 13 workflow-console proof/command tests at `TestFixturePaths` instead
  of duplicated `AppContext.BaseDirectory` path math.
- Updated Settings persistence tests (`SettingsCoreTests`, `SecretStoreTests`,
  `FileSecretStorePermissionTests`, `ProjectSystemProofOfConceptTests`) to use
  `TestTempDirectory`.
- Focused ProjectSystem/Language/SourceControl/Settings/Workspace gate: 839/839
  passed in 5 seconds.
- Ordinary selection: 3029/3029 passed in 7 seconds (M3 baseline 7 seconds).
- Slow integration selection: 36/36 passed in 9 seconds (M3 baseline 9 seconds).
- Combined coverage: 3065/3065 passed with 0 failures.
- Testhost count remained 0 before and after; `/tmp/zaide*` +
  `/tmp/Zaide*` counts were 8550 before and 8600 after the full gates (+50 from
  pre-existing host accumulation and suites outside this slice).
- `git diff --check` passed.

## M5 result (2026-07-26)

- Audited production serialization gates (`ProjectOperationGate`,
  `ProjectContextService`, language services, DAP transport, `ConversationStore`,
  settings/debug/agent singletons). All production gates are required for
  correctness; no production concurrency weakening applied.
- Test singleton audit: parallel tests allocate fresh instances or per-test DI
  providers; no incorrect sharing of production singletons found. Process-wide
  `ReactiveUiTestBootstrap` remains intentional (M3).
- Removed unnecessary `DisableParallelization` from
  `DapContentLengthTransportTests` — each test owns an isolated in-memory
  harness; collection serialization was test-contingent and unjustified.
- Impacted-test selection is feasible at feature-folder mirror,
  composition-registration mirror, and cross-feature `using` scan tiers without
  building a speculative dependency graph. Full symbol-level mapping is not
  justified from live evidence.
- Language polling inventory documented for closeout: 32 remaining `Task.Delay`
  calls across completion (7), hover (8), document-sync (9), formatting (3),
  session (2), symbol (2), navigation (1). Replacement needs production
  completion/propagation signals for debounce, dwell, and bridge sync paths.
- Focused DAP gate: 5/5 passed in 57 ms.
- Ordinary selection: 3029/3029 passed in 7 seconds.
- Slow integration selection: 36/36 passed in 9 seconds.
- Combined coverage: 3065/3065 passed with 0 failures.
- `git diff --check` passed.

## Closeout verification (2026-07-26)

| Gate | Result |
|---|---|
| `dotnet build Zaide.slnx` | pass, 0 errors, 4 existing warnings |
| `Category!=SlowIntegration` (PTY) | pass, 3029/3029 in 9 s |
| `Category=SlowIntegration` (PTY) | pass, 36/36 in 9 s |
| Full unfiltered run (PTY) | pass, 3065/3065 in 16 s |
| Combined coverage | 3065/3065 with 0 failures, 0 skipped |
| Testhost count before/after gates | 3 / 3 (unchanged; all pre-existing stale) |
| VSTest parent-session count before/after | 3 / 3 (unchanged; all pre-existing stale) |
| Child external-process count before/after (csharp-ls, netcoredbg, Phase16) | 0 / 0 |
| `/tmp/zaide*` + `/tmp/Zaide*` after full gate | 8744 |
| `git diff --check` | pass |

Known stale processes already present before closeout (not killed; unrelated to
this refactor):

- PID 1862025 — orphaned testhost from `Phase17AdversarialCloseout` filter (Jul 25)
- PID 1895592 — orphaned testhost from `slow.runsettings` run (Jul 25)
- PID 2945477 — orphaned testhost from `Phase17ActionContractsBrokerTests` filter (Jul 25)

Closeout gates did not add testhost, VSTest parent, or child external-process
leaks. The unfiltered gate completed successfully despite the pre-existing stale
sessions; filtered selections remain the recommended default on shared hosts.

## Deferred follow-ups

- Remaining language polling (32 `Task.Delay` calls across
  completion/hover/document-sync/formatting/session/symbol/navigation) requires
  production completion/propagation signals; defer to a later refactor milestone.
- Repeat unfiltered gate on a fully clean host when stale Jul 25 sessions are
  cleared (optional hygiene; not blocking PR).
