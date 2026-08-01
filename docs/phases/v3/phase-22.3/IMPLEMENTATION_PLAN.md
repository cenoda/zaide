# Phase 22.3: Agent-Path Enablement — Implementation Plan

## Status and Authorization

**Planning only; not implemented.** This sub-phase is blocked on completed and
re-smoked Phase 22.2. M0 is not accepted. Implementation requires explicit
human M0 acceptance and a separate implementation approval.

## A4 Ownership and Dependency

Phase 22.3 owns:

- package 3 — send/routing failure projection (`A1-AS-02`, `A1-TH-05`,
  `A1-MR-03`; BL-07, HI-04, HI-08);
- package 5 — explicit session termination UI (`A1-TC-09`; BL-14);
- package 6 — tools/permissions smoke path (`A1-TP-01`…`A1-TP-03`; BL-08,
  HI-09, HI-10);
- package 7 — interrupted-run positive smoke (`A1-TC-05`; BL-13, MD-13).

It depends on Phase 22.2. Backend binding must be complete, locally re-smoked,
and accepted before this sub-phase can receive implementation approval.

## M0 — Live-Seam Verification and Plan Acceptance

- [ ] Confirm the Phase 22.2 dependency is complete and its binding evidence is
  reusable without hidden test-only injection.
- [ ] Trace direct send and catalog routing from Townhall input through
  admission, backend selection, `AgentSessionService`, event projection, and
  the sole conversation/Townhall writer.
- [ ] Identify every rejection/failure/outcome path that is not projected as
  actionable conversation state, including pre-admission rejection.
- [ ] Verify `EndAsync`, continuity termination contracts, current production
  callers, terminal-state projection, late completion, and restart behavior.
- [ ] Verify the Phase 17 action broker, permission review UI, capability/policy
  evaluation, mutation/rollback seams, and backend dispatch path.
- [ ] Preserve stale-proposal behavior: stale base revision returns
  `Revoked/StaleBaseRevision` without consuming a `Published` decision, and
  `TryConsume()` remains the final authorization step.
- [ ] Define the smallest safe user-reachable mediated actions needed to smoke
  packages 6 and 7 without expanding tool capability or using the Zaide repo.
- [ ] Verify continuity checkpoint/reconcile ownership and the force-quit
  harness procedure; no silent side-effecting resume.
- [ ] Replace focused and A3 command placeholders; lock rollback; receive
  explicit human M0 acceptance.

## Scope

**Goal:** Make the bound agent path truthfully observable and exercisable from
Townhall through send/routing outcomes, explicit termination, mediated
tools/permissions, and interrupted-run recovery smoke.

**Boundaries:** Reuse the accepted Phase 14/15/17/21 ownership model. Townhall
remains a projection, backend outcomes do not become Zaide-verified facts, and
all material actions pass through current capability, policy, permission,
authorization, execution, audit, and reconciliation boundaries.

## Non-Goals

- Backend configuration or binding implementation; 22.2 owns it.
- Trace, memory, or usage surfaces; 22.4 owns them.
- New tool categories, autonomous execution, silent resume, or replay of
  previously approved actions.
- Historical Agent Panel send/routing restoration (`A1-AS-01`, `A1-MR-01`).
- Package 9 UI/friction work.

## Milestones

| Milestone | Outcome | Verification gate |
|-----------|---------|-------------------|
| M0 | Dependency, send/routing, projection, termination, broker, continuity, harness, and rollback seams are verified; plan accepted | Read-only checklist + human acceptance |
| M1 | Send/routing rejection and terminal outcomes are actionable and correctly attributed in Townhall | Focused send/routing/projection tests |
| M2 | Explicit session termination is user-reachable and projects intent, acknowledgement, terminal state, and late outcomes truthfully | Focused session/continuity/termination tests |
| M3 | A bound backend can trigger a safe mediated action and exercise permission plus mutation/rollback UX without bypass | Focused broker/permission/mutation tests |
| M4 | An admitted run can be interrupted in the isolated harness and restart reconciliation is projected without silent resume | Focused continuity tests + positive A3 producer |
| M5 | All owned affected rows pass local re-smoke and regression gates | Build, fast/serial suites, full 22.3 A3 slice |

## Verification Command Placeholders

```bash
dotnet build Zaide.slnx
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<Townhall send/routing/outcome filter>"
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<session continuity/termination filter>"
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --filter "<Phase17 broker/permission/mutation filter>"
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
<out-of-tree A3 agent-path producer with disposable HOME/XDG/workspace and force-quit control>
git diff --check
```

M0 must replace placeholders and define safe disposable action fixtures. Run
the fast suite interactively and fall back to serial mode on failure/hang.

## Exit Conditions

- [ ] Phase 22.2 dependency and both approval gates are recorded.
- [ ] Send/routing failures and outcomes are visible, actionable, ordered, and
  attributed without duplicate conversation writers.
- [ ] Explicit termination is reachable and never overclaims provider state.
- [ ] Mediated action, permission, rollback, stale-base, and final-consumption
  invariants pass.
- [ ] Interrupted-run smoke proves classification and no silent resume.
- [ ] `A1-AS-02`, `A1-TH-05`, `A1-MR-03`, `A1-TP-01`…`A1-TP-03`,
  `A1-TC-05`, and `A1-TC-09` have current isolated re-smoke evidence.

## Rollback Note

Prefer separate reversible commits for M1–M4. A rollback must preserve the
Phase 17 broker and Phase 21 durable records and must not leave a user entry
point connected to a partial path. Persistence/schema changes require an
M0-approved migration and restore procedure before implementation.
