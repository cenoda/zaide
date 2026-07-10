# Phase 5.3 TOFIX

Code quality and planning issues found during the Phase 5.3 plan audit.

- [x] Make the panel execution state seam reactive before relying on `Status`,
  `DraftInput`, or busy-state UI updates.
  Current live code binds `AgentPanelView` directly to `AgentPanelState`, but
  `AgentPanelState` still uses plain auto-properties for `Status` and
  `DraftInput`. `OutputHistory` updates through `ObservableCollection`, but
  status-driven UI and input enable/disable behavior are not reliably observable
  yet.
  **M0 resolution:** Locked in IMPLEMENTATION_PLAN.md §M0 Locked Decisions §1.
  `AgentPanelState` will extend `ReactiveObject`; `Status` and `DraftInput`
  become reactive properties. `OutputHistory` stays as `ObservableCollection<string>`.

- [x] Keep direct-agent execution ownership out of `MainWindow` code-behind.
  View event wiring in `MainWindow.axaml.cs` is acceptable as a thin connection
  point, but execution logic, panel lookup, request dispatch, and state mutation
  should live in a narrow app/ViewModel seam such as
  `IAgentExecutionCoordinator`.
  **M0 resolution:** Locked in IMPLEMENTATION_PLAN.md §M0 Locked Decisions §3.
  `MainWindow` remains a thin connector; `MainWindowViewModel` exposes a
  delegating command; `AgentExecutionCoordinator` owns execution/state logic.

- [x] Add dedicated ViewModel tests for the direct-execution seam instead of
  relying mainly on `MainWindowViewModelTests.cs`.
  This slice needs focused coverage for success, explicit failure, missing
  configuration, one-in-flight behavior, and panel state/output updates.
  **M4 resolution:** `AgentExecutionCoordinatorTests.cs` now covers all success,
  failure, missing-config (API key, base URL, model), one-in-flight, exception,
  and error-recovery paths with explicit Status/IsBusy/output assertions.

- [x] Keep interface and implementation files split per repo convention.
  If execution coordination is added, `IAgentExecutionCoordinator` and
  `AgentExecutionCoordinator` should live in separate files to match the
  one-class-per-file rule.
  **M4 resolution:** Confirmed — `IAgentExecutionCoordinator.cs` and
  `AgentExecutionCoordinator.cs` are separate files.

- [x] Add an explicit panel send trigger before wiring provider execution.
  The current `AgentPanelView` only exposes draft input. Phase 5.3 needs one
  real user action that starts a request, such as a send button or narrow
  Enter-to-send behavior, routed into the coordinator seam rather than handled
  as execution logic in the view.
  **M0 resolution:** Locked in IMPLEMENTATION_PLAN.md §M0 Locked Decisions §2.
  Narrow Enter-to-send via `KeyDown` handler on `_inputBox`. Send button
  deferred but additive if needed.

- [x] Decide and document the shared configuration source for the first provider
  slice.
  The current recommendation is a narrow `AgentExecutionOptions` type populated
  from `AGENT_API_URL`, `AGENT_API_KEY`, and `AGENT_MODEL` in `Program.cs`, then
  injected into `AgentExecutionService`.
  **M0 resolution:** Locked in IMPLEMENTATION_PLAN.md §M0 Locked Decisions §4.
  Environment variables → `AgentExecutionOptions` → DI injection.

- [x] Keep the execution service constructor-testable with injected `HttpClient`.
  Service tests should use a fake or mocked `HttpMessageHandler` so request
  construction, invalid JSON, endpoint failure, and missing-config behavior can
  be covered without real network calls.
  **M4 resolution:** `AgentExecutionService` accepts `HttpClient` via constructor.
  Tests use `FakeMessageHandler`, `CaptureMessageHandler`, and `FaultMessageHandler`
  to cover all paths without real network calls.

- [x] Leave a narrow forward seam for Phase 5.4 Townhall mirroring without
  coupling 5.3 directly to `TownhallViewModel`.
  If needed, prefer a small execution-event seam over a direct Townhall
  dependency so 5.4 can subscribe rather than refactor the core coordinator.
  **M4 resolution:** `AgentExecutionCoordinator` has no Townhall references.
  The `IAgentExecutionCoordinator` interface provides a clean seam for 5.4
  to subscribe to execution events without rewriting core logic.

- [x] Record the explicit reactive-state choice in the plan before coding starts.
  The current recommendation remains the smaller path: make `AgentPanelState`
  reactive for coordinator-mutated scalar properties while leaving
  `OutputHistory` on its existing `ObservableCollection<string>` seam.
  **M0 resolution:** Locked in IMPLEMENTATION_PLAN.md §M0 Locked Decisions §1.

- [x] Record the explicit send-trigger choice in the plan before coding starts.
  The current recommendation is narrow Enter-to-send because the live input
  already uses `AcceptsReturn = false`, unless implementation evidence shows a
  send button is cleaner.
  **M0 resolution:** Locked in IMPLEMENTATION_PLAN.md §M0 Locked Decisions §2.

- [x] Record the explicit shell composition path before coding starts.
  The plan should state who calls the coordinator from the UI path so
  `MainWindow` remains a thin connector and `MainWindowViewModel` only exposes a
  narrow delegating seam.
  **M0 resolution:** Locked in IMPLEMENTATION_PLAN.md §M0 Locked Decisions §3.

- [x] Treat panel view-interaction tests as conditional on practical existing
  Avalonia test support.
  If headless interaction tests are not already cheap in this repo, keep the UI
  trigger narrow and rely on service/ViewModel coverage plus manual smoke rather
  than widening Phase 5.3 into test-harness work.
  **M4 resolution:** The repo has no established headless Avalonia interaction
  test pattern. The Enter-to-send trigger in `AgentPanelView` is minimal and
  covered by service/ViewModel tests. Manual smoke is sufficient for the UI
  interaction detail.
