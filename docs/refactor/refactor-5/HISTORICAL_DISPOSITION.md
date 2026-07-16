# Refactor 5 Historical Disposition

## Status

**Audit date:** 2026-07-16

**Outcome:** **Historically fulfilled by Phase 6 and Phase 6.1; no Refactor 5
implementation remains.**

**Scope:** Read-only comparison of the Refactor 5 temporal recommendations with
the current checkout and historical Phase 6 records. This audit does not
authorize Refactor 6.1 or any production change.

## Why this audit exists

`TEMPORAL_REPORT.md` was created after Phase 5 as a pause-point direction check
before Phase 6. It was never an `IMPLEMENTATION_PLAN.md`, and Refactor 5 was
never an independently executed refactor. V3 preserves the identifier and
needs a truthful final disposition before assigning new refactor numbers.

## Sources checked

- `docs/refactor/refactor-5/TEMPORAL_REPORT.md`
- `docs/phases/v1/phase-6/IMPLEMENTATION_PLAN.md`
- `docs/phases/v1/phase-6.1/IMPLEMENTATION_PLAN.md`
- `docs/phases/v1/phase-6/TOFIX.md`
- `docs/phases/v1/phase-6.1/TOFIX.md`
- `docs/roadmap/PHASES.md`
- current agent-panel, router, Townhall, and execution code under `src/`
- focused tests for panel identity, mention parsing, routing, mirroring, and
  view-host lifetime under `tests/Zaide.Tests/`
- Git history for the Phase 6 implementation sequence

## Recommendation-by-recommendation disposition

| Temporal recommendation | Historical delivery | Current evidence | Disposition |
|-------------------------|---------------------|------------------|-------------|
| Normalize agent identity and panel creation | Phase 6 M1 (`ceb3839`) | `AgentPanelHost.CreatePanel()` assigns `alpha`, `beta`, `gamma`, `delta`, then stable fallback names; host tests cover identity creation | Fulfilled for the narrow routing slice |
| Keep routing orchestration out of `MainWindowViewModel` | Phase 6 M3/M4 (`a676d57`, `069cbe2`) | `AgentRouter` owns parse, resolution, and dispatch; `MainWindowViewModel` delegates and owns Townhall projection only | Fulfilled with later composition debt noted below |
| Add a tiny route request/result model before `@mention` behavior | Phase 6 M2 (`4b4ebe0`) | `RouteRequest`, `RouteResult`, and `MentionParser`; parser and router tests cover success and failure paths | Fulfilled |
| Add explicit `AgentPanelHostView` subscription cleanup | Phase 6 M5 (`42063fb`) | `DetachHost()` detaches host and per-panel handlers and clears retained views/tabs; lifetime tests cover detach and rebind | Fulfilled |
| Defer provider abstractions | Phase 6 and Phase 6.1 boundaries | No `IAgentProvider`, `AgentRegistry`, or provider registry exists in current production code; execution remains one narrow configured service | Fulfilled for Phase 6; V3 may introduce different backend seams only through later M0s |
| Keep Townhall as the shared activity ledger | Phase 6 direct visibility; Phase 6.1 routed visibility follow-up | `SendAgentMessageAsync` mirrors user input, routing failures, and resolved target outcomes through `TownhallViewModel`; Phase 6.1 added focused router and mirroring tests | Fulfilled for the shipped routing behavior, not a unified conversation domain |

## Suggested exit-condition result

- [x] New agent panels receive stable, distinguishable routing identities.
- [x] A route parsing/result model exists and is covered by focused tests.
- [x] A routing orchestration seam exists outside `MainWindowViewModel`.
- [x] Agent-panel view-host subscriptions have an explicit cleanup path.
- [x] No provider registry or broad execution platform was introduced during
      the routing slice.
- [x] Townhall remained the shared visibility surface; Phase 6.1 closed the
      routed-flow and routing-failure visibility gaps recorded at Phase 6
      closeout.

## Preserved limitations

Historical fulfillment does not make the Phase 6 model sufficient for V3:

- Panel identities are deterministic seeds, not a durable Agent Identity
  registry. Townhall seeds `user-1`/`agent-1`, panels seed
  `alpha`/`beta`/`gamma`/`delta`, and execution uses one global provider
  configuration.
- `MainWindowViewModel` does not own routing policy, but it still coordinates
  mirroring and interprets panel output strings. Refactor 7 must replace this
  projection protocol without hiding a feature inside structural movement.
- Agent-panel output and Townhall messages remain separate stores; neither is
  an authoritative unified conversation record.
- Mirroring targets the active Townhall channel at each write, so a channel
  switch during execution can split request and response attribution.
- Phase 6 did not deliver a specialized debate/disagreement model. It delivered
  deterministic one-target routing and generic Townhall outcome visibility.
- The dedicated Agent Panel remains visible until Phase 14 proves retirement
  parity in the unified conversation workspace.

These limitations are V3 forcing functions, not unfinished Refactor 5 work.

## Final disposition

Refactor 5 is **historically fulfilled**. Do not create a retroactive
`IMPLEMENTATION_PLAN.md`, reopen its temporal recommendations as a new
implementation project, or reuse the identifier for V3 source restructuring.
The original report and this disposition remain historical evidence.

The V3 order and this disposition were accepted by the user on 2026-07-16.
Refactor 6.1 M0 planning is authorized as the next task. That M0 must reverify
the current dependency graph and does not authorize later production
milestones.

## Verification

Run against the current checkout after this documentation audit:

```bash
dotnet build Zaide.slnx --no-restore
dotnet test Zaide.slnx --no-build
git diff --check
```

Results on 2026-07-16:

- `dotnet build Zaide.slnx --no-restore` — succeeded with 0 errors and 1
  existing `CS0067` warning in
  `ProjectDebugTargetResolverTests.FakeManagedProcessRunner.ProcessStarted`;
- `dotnet test Zaide.slnx --no-build` — 2,172 passed, 0 failed, 0 skipped;
- `git diff --check` — clean.

These results verify the current checkout but do not freeze future suite totals
or convert this document into an implementation plan.
