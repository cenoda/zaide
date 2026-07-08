# Phase 6.1 Audit TOFIX

Non-blocking issues found while auditing `IMPLEMENTATION_PLAN.md` against live
code before M2. None block M2/M3; recorded here so they are not lost.

---

## Issue: Test helper builds MainWindowViewModel with a different AgentPanelHost than the router

`CreateViewModel(IFileService)` and `CreateViewModel(ITerminalHost)` in
`tests/Zaide.Tests/MainWindowViewModelTests.cs` construct the router against a
local `panelHost` variable, but pass a **separate** `new AgentPanelHost()` into
the `MainWindowViewModel` constructor:

```csharp
var panelHost = new AgentPanelHost();
var parser = new MentionParser(panelHost);
var router = new AgentRouter(parser, panelHost, coordinator);
var vm = new MainWindowViewModel(..., new AgentPanelHost(), coordinator, router, ...);
//                                     ^^^^^^^^^^^^^^^^^^^^ different instance
```

### Impact

The view model's `AgentPanelHost` and the router's panel host are two different
objects with independent panel collections. Any future test that relies on the
VM and router sharing panel state through these two helpers would observe
inconsistent state. The Phase 6.1 mirror tests are unaffected because they use
`CreateMirrorTestViewModel` / `CreateTwoPanelMirrorTestViewModel`, which correctly
share a single `agentHost`.

### Proposed Fix

Pass the same `panelHost` instance into the `MainWindowViewModel` constructor in
both `CreateViewModel` overloads (and the inline construction inside
`TerminalStartupError_UpdatesStatusText`).

### Status

- [ ] Not fixed — out of Phase 6.1 scope (test-helper hygiene, pre-existing)

---

## Issue: Plan rollback reference labels an M0 docs commit as the "M3 rollback hash"

`IMPLEMENTATION_PLAN.md` (M0 section) records
`67a393d6757d285c567db1633b0edd693c43e5dd` as the "Rollback hash recorded for M3",
but that commit is the M0 plan-refinement docs commit
(`docs: refine phase-6.1 plan …`), not a code anchor. The Rollback Plan section
separately uses `9fe780f` (last commit before M1).

### Impact

Documentation-only. Both hashes are valid commits; only the M3 label is loosely
applied. No effect on code or tests.

### Proposed Fix

When M2 starts, set the M3 rollback anchor to the last green commit before M2
(currently HEAD `e5f426b`, the M1 feature commit) rather than the M0 docs commit.

### Status

- [ ] Not fixed — update when M2 begins

---

## Note: M2 coverage assertions to include (guidance, not a defect)

The M2 test list ("direct-send dispatch, routed-send dispatch, all failure
cases") implies but does not spell out two assertions worth encoding in
`AgentRouterTests.cs`:

1. On any parse failure, `IAgentExecutionCoordinator.SendAsync` is **never**
   called (`AgentRouter.RouteAndExecuteAsync` returns the failure result before
   dispatch).
2. On a valid mention, `SendAsync` is called with the **target** panel's
   `PanelId`, not the source panel's.

This is guidance for the M2 implementation, not a pre-existing defect.

### Status

- [ ] To be satisfied by M2 `AgentRouterTests.cs`
