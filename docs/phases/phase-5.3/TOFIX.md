# Phase 5.3 TOFIX

Code quality and planning issues found during the Phase 5.3 plan audit.

- [ ] Make the panel execution state seam reactive before relying on `Status`,
  `DraftInput`, or busy-state UI updates.
  Current live code binds `AgentPanelView` directly to `AgentPanelState`, but
  `AgentPanelState` still uses plain auto-properties for `Status` and
  `DraftInput`. `OutputHistory` updates through `ObservableCollection`, but
  status-driven UI and input enable/disable behavior are not reliably observable
  yet.

- [ ] Keep direct-agent execution ownership out of `MainWindow` code-behind.
  View event wiring in `MainWindow.axaml.cs` is acceptable as a thin connection
  point, but execution logic, panel lookup, request dispatch, and state mutation
  should live in a narrow app/ViewModel seam such as
  `IAgentExecutionCoordinator`.

- [ ] Add dedicated ViewModel tests for the direct-execution seam instead of
  relying mainly on `MainWindowViewModelTests.cs`.
  This slice needs focused coverage for success, explicit failure, missing
  configuration, one-in-flight behavior, and panel state/output updates.

- [ ] Keep interface and implementation files split per repo convention.
  If execution coordination is added, `IAgentExecutionCoordinator` and
  `AgentExecutionCoordinator` should live in separate files to match the
  one-class-per-file rule.
