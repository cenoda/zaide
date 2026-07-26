# Refactor 9: Test Execution Structure — Implementation Plan

## Pre-Implementation Verification

- [x] Confirmed the clean baseline at `e1873ce8` on
      `perf/test-structure-phase1`.
- [x] Confirmed the fast suite is green at 3065/3065 and that the repository
      already has isolated Phase 16, PTY, and Avalonia collections.
- [x] Confirmed there is no shared slow-test trait or dedicated external
      resource boundary.

## Scope

**Goal:** Reduce test-run contention and make expensive external-resource
tests explicitly selectable without changing production behavior.

**Boundaries:** No CUDA/SIMD work, no production singleton redesign, no
impacted-test engine, and no broad `Task.Delay` rewrite in this refactor.
Those are later milestones and must retain independent verification gates.

## Milestones

| Milestone | Description | Test |
|---|---|---|
| M0 | Baseline and live ownership audit | `dotnet test Zaide.slnx --no-build` |
| M1 | Mark process/PTY/DAP proof tests as `SlowIntegration` and serialize them through one external-resource collection | filtered fast/slow commands plus full suite |
| M2 | Replace high-value polling waits with event/signal-based synchronization | focused affected tests, then full suite |
| M3 | Share Avalonia/ReactiveUI bootstrap with resettable test infrastructure | UI-focused tests, then full suite |
| M4 | Reuse immutable filesystem fixtures and isolate writable cases | project/language/filesystem-focused tests, then full suite |
| M5 | Measure and split remaining gate/singleton contention; document impacted-test feasibility | full suite plus timing/process evidence |

## M4 Verification Record

Added `TestFixturePaths`, `TestFilesystem.SharedReadOnlyWorkspaceRoot`, and
`TestTempDirectory` under `tests/Zaide.Tests/Infrastructure/`. Path-only tests
now reuse the committed `tests/fixtures` tree instead of creating per-class
temp roots; writable tests allocate isolated `TestTempDirectory` instances with
guaranteed disposal. Consolidated duplicate `tests/fixtures/workflow-console`
path literals onto `TestFixturePaths` and deduplicated five SourceControl
`CreateTempDir()` helpers.

| Category | Treatment |
|---|---|
| Shared read-only | `TestFilesystem.SharedReadOnlyWorkspaceRoot` → `tests/fixtures` (12 parser/resolution/language session tests, no disk writes) |
| Shared immutable fixtures | `TestFixturePaths.WorkflowConsole*` (13 existing workflow proof/command tests) |
| Per-test writable | `TestTempDirectory` via `IDisposable` test classes or `using var` (Language navigation/symbol/formatting, ProjectSystem presentation/debug resolver tests, Settings persistence, SourceControl repo tests) |

| Gate | Result |
|---|---|
| `dotnet build Zaide.slnx` | pass, 0 errors, 4 existing warnings |
| Focused ProjectSystem/Language/SourceControl/Settings/Workspace | pass, 839/839 in 5s |
| `Category!=SlowIntegration` | pass, 3029/3029 in 7s (M3 baseline 7s) |
| `Category=SlowIntegration` | pass, 36/36 in 9s (M3 baseline 9s) |
| Combined coverage | 3065/3065 with 0 failures |
| Testhost count before/after gates | 0 / 0 |
| `/tmp/zaide*` + `/tmp/Zaide*` count before/after full gates | 8550 / 8600 (+50; pre-existing host accumulation; M4 slice adds cleanup for refactored tests) |
| `git diff --check` | pass |

## M1 Entry and Exit Conditions

- External process, PTY, and production DAP proof tests carry the
  `SlowIntegration` trait.
- Those tests share a collection with parallelization disabled.
- The default test set remains behaviorally unchanged; filtered commands prove
  that the slow set is selectable and the ordinary set remains green.
- No testhost, fixture process, PTY child, or adapter process remains after the
  focused slow run beyond pre-existing processes measured before the run.

## M1 Verification Record

| Gate | Result |
|---|---|
| `dotnet build Zaide.slnx --no-restore` | pass, 0 errors, 4 existing warnings |
| `Category=SlowIntegration` | pass, 36/36 in 9.663s |
| `Category!=SlowIntegration` | pass, 3029/3029 in 9.127s |
| Full unfiltered run | blocked by pre-existing orphaned test sessions; the run started for this gate was cancelled after inspection, with testhost count unchanged at 7 and external-process count unchanged at 3 |
| `git diff --check` | pass |

## M2a Verification Record

The first polling slice replaced fixed-delay waits in
`ProjectContextServiceTests` and `ProjectContextServiceIntegrationTests` with
observable snapshot signals and task completion. The focused gate passed
48/48 in 0.569s. No production process is started by this slice; the existing
shared-host counts were not changed.

## M2b Verification Record

The agent-session cancellation and admission tests now wait on backend/read
start signals or typed session state instead of fixed delays. The focused agent
gate passed 94/94 in 7.598s. No production process is started by this slice.

## M2c Verification Record

The parity test wait helpers now subscribe to `IAgentSessionService.Events`
instead of polling with `Task.Delay`. Re-admission tests use explicit
completion signals for the moment the second run identity is assigned. The
focused parity gate passed 28/28 in 7s.

## M2d Verification Record

Language navigation/symbol cancellation and dismissal waits now use the
language service change stream. Stale-response cases that publish no terminal
snapshot retain their bounded legacy delay until a production request-complete
signal is available. The focused navigation/symbol gate passed 32/32.

## M2e Verification Record

Added `ILanguageNavigationService.WhenRequestCompleted` so definition requests
that discard stale LSP results without publishing a terminal snapshot still
signal completion. Navigation stale waits now subscribe before triggering
completion; symbol stale waits use the existing change stream because those
paths publish cancelled/idle snapshots. The focused navigation/symbol gate passed
32/32 in 1 second. Ordinary selection passed 3029/3029 in 11 seconds. Slow
integration selection passed 36/36 in 10 seconds. Combined coverage 3065/3065
with 0 failures. Testhost count remained 0 before and after; external-process
count remained 2 before and after. `git diff --check` passed.

## M3 Verification Record

Added `ReactiveUiTestBootstrap` with a module initializer that performs a
one-time `BuildApp()` plus shared `EnsureApplication()` and default Splat
registrations per testhost process. Moved `AvaloniaUiInitializationCollection`
to `tests/Zaide.Tests/Infrastructure/` with an `ICollectionFixture` and
`ReactiveUiMutableStateResetAttribute` for serialized UI tests. Replaced 81
duplicate `RxAppBuilder.CreateReactiveUIBuilder().BuildApp()` call sites
across 81 test files; the only remaining bootstrap call is in
`ReactiveUiTestBootstrap`. Consolidated 15 duplicated private
`EnsureApplication()` helpers into the shared bootstrap. Preserved non-bootstrap
static initialization (temp-directory setup) in 24 test classes.

| Gate | Result |
|---|---|
| `dotnet build Zaide.slnx` | pass, 0 errors, 4 existing warnings |
| `FullyQualifiedName~SettingsUiTests` | pass, 4/4 in 0.288s |
| `FullyQualifiedName~Zaide.Tests.App.Shell` | pass, 134/134 in 0.780s |
| `Category!=SlowIntegration` | pass, 3029/3029 in 7s (prior baseline 9–11s) |
| `Category=SlowIntegration` | pass, 36/36 in 9s (prior baseline 9–10s) |
| Combined coverage | 3065/3065 with 0 failures |
| Testhost count before/after gates | 0 / 0 |
| `git diff --check` | pass |

## M5 Verification Record

Analysis of production serialization gates, test singleton sharing, and
impacted-test feasibility. One test-contingent fix: removed unnecessary
`DisableParallelization` from `DapContentLengthTransportTests` (each test
creates an isolated in-memory harness with no shared static state).

### Serialization inventory

| Component | Mechanism | Classification |
|---|---|---|
| `ProjectOperationGate` | `_admissionMutex` + `_criticalSectionMutex` | Production-required (workflow/debug mutual exclusion) |
| `ProjectContextService` | `_gate` (`SemaphoreSlim`) | Production-required (overlapping load/reload sequence) |
| `LanguageSessionService` | `_gate` | Production-required (session lifecycle) |
| `LanguageDocumentBridge` | `_gate` | Production-required (document sync ordering) |
| Language feature services (completion/hover/navigation/symbol/formatting) | `_gate` locks + request IDs | Production-required (in-flight request coalescing) |
| `DebugSessionService` | `_gate` | Production-required (adapter session lifecycle) |
| `DapContentLengthTransport` | `_writeGate` | Production-required (DAP frame write integrity) |
| `SettingsService` | `_mutationGate` + queued writer | Production-required (concurrent mutation safety) |
| `ConversationStore` | `_sync` | Production-required (in-memory store correctness) |
| `ManagedProcessRunner` | `_sync` | Production-required (process ownership) |
| Agent session/coordinator/broker services | various `_sync` / `_gate` / `_admissionGate` | Production-required |
| `ReactiveUiTestBootstrap` | process-wide `lock(Sync)` + idempotent init | Test-contingent, intentional (M3); only `SettingsUiTests` uses serialized `AvaloniaUiInitialization` collection |
| `SlowExternalResources` / `Phase16Isolation` / `LinuxTerminalProcessIsolation` / `AvaloniaUiInitialization` collections | xUnit `DisableParallelization` | Test-contingent, justified (external resources or mutable global UI state) |
| ~~`DapContentLengthTransportTests` collection~~ | ~~removed~~ | Was test-contingent and unnecessary |

### Test singleton / shared-state audit

- Production DI singletons are not shared across parallel tests: unit and
  integration tests construct per-test `ServiceProvider` instances or direct
  `new` targets (`ConversationStore`, `ProjectOperationGate`, language fakes).
- `TestOperationGateFactory`, `TestProjectWorkflowFactory`, and feature test
  support classes allocate fresh instances per test class or method.
- `TestFilesystem.SharedReadOnlyWorkspaceRoot` is read-only committed fixture
  data (M4); writable cases use isolated `TestTempDirectory`.
- Process-wide shared state is limited to `ReactiveUiTestBootstrap` (by design)
  and xUnit testhost process lifetime; no evidence that ordinary parallel tests
  incorrectly share production singletons.

### Remaining language polling inventory (closeout note)

| File | Delay count | Nature |
|---|---|---|
| `LanguageDocumentSyncTests` | 9 | Bridge propagation / generation settle waits |
| `LanguageHoverTests` | 8 | Dwell/debounce policy + cancellation timing |
| `LanguageCompletionTests` | 7 | Automatic debounce + trigger policy waits |
| `LanguageFormattingTests` | 3 | Request settle waits |
| `LanguageSessionServiceTests` | 2 | Reconcile timing |
| `LanguageSymbolTests` | 2 | Workspace debounce + dismiss timing |
| `LanguageNavigationTests` | 1 | Residual bounded wait |

Replacing these requires production completion/propagation signals for
document-bridge sync, hover dwell, and completion debounce paths — not
test-only scope changes. Deferred to refactor closeout; not part of M5.

### Impacted-test selection feasibility

**Feasible at coarse tiers without a full dependency graph:**

1. **Feature-folder mirror** — `src/Features/{Feature}/**` maps to
   `tests/Zaide.Tests/Features/{Feature}/**` (11 features, 203 test files).
2. **Composition registration mirror** —
   `src/App/Composition/Registration/*ServiceCollectionExtensions.cs` maps to
   `tests/Zaide.Tests/App/Composition/*RegistrationModuleTests.cs`.
3. **Cross-feature import scan** — grep `using Zaide.Features.{Feature}.` across
   `tests/Zaide.Tests` catches tests outside the feature folder that depend on
   changed namespaces (e.g. Workspace referenced from 78 test files).
4. **Architecture ratchet** — existing `ArchitectureInventoryReader` and ratchet
   tests for structural/boundary changes.

**Metadata/index required (no speculative graph engine):**

- Changed-file path → feature name (directory rule).
- Optional: changed namespace → feature (namespace declaration regex, already in
  `ArchitectureInventoryReader`).
- Test-side index: test file path → feature folder; plus `using` scan cache for
  cross-feature references.
- Selection trait map: `SlowIntegration` (36 tests), serialized collections.

**Not justified without live evidence:** IL/symbol-level production→test
dependency graph, Roslyn reference closure, or MSBuild per-type test mapping
(tests reference single `Zaide.csproj`).

### Duplicate / all-pairs patterns

- No all-pairs cross-test duplication found.
- Intentional in-test stress loops remain in `DapContentLengthTransportTests`
  (50-attempt race loops) and agent broker admission tests; these are bounded
  single-test stress, not suite-wide duplication.

| Gate | Result |
|---|---|
| `dotnet build Zaide.slnx` | pass, 0 errors, 4 existing warnings |
| `FullyQualifiedName~DapContentLengthTransportTests` | pass, 5/5 in 57 ms |
| `Category!=SlowIntegration` | pass, 3029/3029 in 7 s |
| `Category=SlowIntegration` | pass, 36/36 in 9 s |
| Combined coverage | 3065/3065 with 0 failures |
| `git diff --check` | pass |

## Closeout Verification Record (2026-07-26)

Final verification on `perf/test-structure-phase1` after M2e (`4f9a963`), M3
(`e91b1cc`), M4 (`466c6b30`), and M5 (`c1cb051`).

| Gate | Result |
|---|---|
| `dotnet build Zaide.slnx` | pass, 0 errors, 4 existing warnings |
| `Category!=SlowIntegration` (interactive PTY) | pass, 3029/3029 in 9 s |
| `Category=SlowIntegration` (interactive PTY) | pass, 36/36 in 9 s |
| Full unfiltered run (interactive PTY) | pass, 3065/3065 in 16 s |
| Combined coverage | 3065/3065 with 0 failures, 0 skipped |
| Testhost count before/after | 3 / 3 (pre-existing stale; no new leaks) |
| VSTest parent-session count before/after | 3 / 3 (pre-existing stale) |
| Child external-process count before/after | 0 / 0 |
| `git diff --check` | pass |

Pre-existing stale testhost PIDs (Jul 25 orphaned runs): 1862025, 1895592,
2945477. Closeout did not kill unrelated processes.

**Deferred:** language completion/hover/document-sync polling (32 remaining
`Task.Delay` calls); optional unfiltered gate on a fully clean host.

## Limitations

- A trait is a selection boundary, not a performance fix by itself.
- Tests that use only in-memory fakes remain in the ordinary suite even when
  their production area also has integration proofs.
- Impacted-test selection is feasible at feature-folder and namespace-import
  tiers; a full dependency graph is not justified for this refactor.
- Language completion/hover/document-sync polling remains for closeout.

## Rollback Plan

Revert the M1 commit and return to `e1873ce8`.
