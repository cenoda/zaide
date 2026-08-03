# Phase 22.3: Agent-Path Enablement — Implementation Plan

## Status and Authorization

**M1 corrective implementation pushed pending independent re-audit; M2 not authorized.**

Human M0 acceptance was recorded on 2026-08-03 after the independent GO audit at
`c2904fb100d538b0bd080eab3002cfc3994b6889`. Separate Phase 22.3 M1
implementation authorization was granted in the same session. Independent M1
audit at `01a1f221a8a96c91be14f078321658e9d5582b50` returned **NO-GO** for two
navigation/race defects (F1, F2). Corrective-only authorization closed those
defects; M1 is **not accepted**. M2–M5 require explicit separate implementation
approval.

The live findings, test inventory, future A3 procedure, and recommendation are
recorded in [M0_SEAM_VERIFICATION.md](./M0_SEAM_VERIFICATION.md).

## A4 Ownership and Dependency

Phase 22.3 owns:

- package 3 — send/routing failure projection (`A1-AS-02`, `A1-TH-05`,
  `A1-MR-03`; BL-07, HI-04, HI-08);
- package 5 — explicit session termination UI (`A1-TC-09`; BL-14);
- package 6 — tools/permissions smoke path (`A1-TP-01`…`A1-TP-03`; BL-08,
  HI-09, HI-10);
- package 7 — interrupted-run positive smoke (`A1-TC-05`; BL-13, MD-13).

Phase 22.2 owns backend configuration and binding. Its completed, re-smoked
workflow is reused through shipped Townhall controls; Phase 22.3 must not add a
second binding path, wrap either backend, or fall back between Native Harness
and ACP.

## M0 — Live-Seam Verification and Plan Acceptance

- [x] Confirm Phase 22.2 is complete and the ordering dependency is satisfied.
- [x] Trace Townhall direct/channel input, `AgentRouter`,
  `AgentExecutionCoordinator`, binding selection, `AgentSessionService`, the
  event stream, `AgentConversationEventProjection`, and Townhall projection.
- [x] Classify admission, pre-admission rejection, routing rejection, terminal
  failure, cancellation, timeout, disconnect, indeterminate, and late
  completion visibility and ownership.
- [x] Verify `IAgentSessionService.EndAsync`, production callers, cancellation
  acknowledgement, terminal projection, ownership removal, late completion,
  and backend-state truthfulness.
- [x] Verify Phase 17 backend dispatch, broker, policy, permission review,
  workspace read/mutation, event/audit attribution, stale/conflict, and
  reconciliation seams.
- [x] Preserve pre-consume stale behavior exactly: `Revoked` /
  `StaleBaseRevision`, decision remains `Published`, no mutation; `TryConsume()`
  remains the final authorization step.
- [x] Verify Phase 21 checkpoint, startup reconciliation, backend
  revalidation, interrupted classification, explicit re-send, and no-silent-
  resume ownership.
- [x] Inventory existing tests, confirm exact filters with `--list-tests`, and
  run the focused filters.
- [x] Define the future isolated A3 producer without executing it.
- [x] Lock milestone, rollback, migration, and evidence boundaries.
- [x] Receive explicit human M0 acceptance.
- [x] Receive separate Phase 22.3 M1 implementation approval.
- [ ] Receive separate Phase 22.3 M2–M5 implementation approval.

## Scope

**Goal:** Make the bound agent path truthfully observable and exercisable from
shipped Townhall controls through direct send, catalog routing, explicit live-
session termination, safe mediated actions, and interrupted-run restart
classification.

**Boundaries:** Reuse the accepted Phase 14/15/17/21 owners. Townhall remains a
projection, backend outcomes do not become Zaide-verified facts, and all
material actions pass through the current capability, policy, permission,
final authorization, execution, audit, and reconciliation path.

## Locked Implementation Decisions

| ID | Decision |
|----|----------|
| `P223-D01` | `AgentConversationEventProjection` remains the sole normalized agent-event writer to `IConversationStore`. Routing helpers may call its typed static projection API; no ViewModel, backend, broker, or second projection writes agent outcomes directly. |
| `P223-D02` | A direct attempt belongs to its direct conversation. A valid mention executes in the target direct conversation. Parse/target resolution failure belongs to the source conversation. The active source receives bounded routed-status/navigation feedback without copying private assistant content; target entries and unread state remain authoritative. |
| `P223-D03` | Channel plain chat remains channel chat. A valid catalog mention from a channel must use a typed source-conversation route context and the same target direct execution path; it must not fabricate a panel identity or mirror private response content into the channel. |
| `P223-D04` | Pre-binding and session admission rejection become a correlated actionable `ExecutionFailure` in the attempted execution conversation. The reason must distinguish unbound, backend unavailable/mismatched, session ending/ended, identity mismatch, and concurrent-run rejection. Draft clearing occurs only after the attempt is either admitted or a visible correlated rejection is stored. |
| `P223-D05` | Accepted/running state may remain transient busy/status presentation, but rejection and every admitted terminal outcome must be durable conversation-visible state. `Queued` is not a current `AgentRunStatus` and must not be claimed. |
| `P223-D06` | Cancellation intent, backend/process acknowledgement, terminal run state, session ownership removal, and late completion are separate facts. A late completion after cancellation intent is retained and labelled; it never silently overwrites the intent. Local end must not claim provider termination or deletion. |
| `P223-D07` | Explicit live termination is a shipped Townhall direct-conversation command backed by `IAgentSessionService.EndAsync`. The Phase 21 continuity `Terminate` operation remains the durable interrupted-session record path; it is not a substitute for live `EndAsync`. |
| `P223-D08` | Termination uses bounded cancellation/acknowledgement handling. ACP cancellation must use an independent bounded token rather than the already-cancelled run token. Timeout leaves an explicit indeterminate/local-ownership result and a retryable user surface; it must not claim the backend stopped. |
| `P223-D09` | Native Harness and ACP remain independent `IAgentBackend` siblings. Each reaches its own adapter and the same run-scoped Phase 17 broker; no wrapping, fallback, or cross-backend retry is allowed. |
| `P223-D10` | Phase 17 `AgentPermissionDecision.TryConsume()` remains the final authorization step. A proposal known stale before consumption returns `Revoked/StaleBaseRevision` with the decision still `Published`; post-consume apply races may return `Conflict/StaleBaseRevision` with the decision already `Consumed`. |
| `P223-D11` | The safe M3 product path uses only the existing five action kinds. No network, Git, secrets, memory, multi-file transaction, autonomous execution, or new tool category is added. The absence of product rollback/change-set support remains a truthful `A1-TP-03` limitation; M3 must not rename atomic replace or conflict rejection as rollback. |
| `P223-D12` | Action facts retain session/run/conversation/backend/action/workspace attribution and add initiating/target actor attribution where the user goal requires it. Early broker denials that currently return before `ActionResultReported` must become bounded, correlated audit/event outcomes without weakening fail-closed behavior. |
| `P223-D13` | New continuity checkpoints use the opened disposable/product workspace root, not an incidental process CWD. Existing CWD-keyed records are never silently merged or deleted; any compatibility read is labelled legacy and read-only. Workspace-open reconciliation is distinct from application-start legacy reconciliation. |
| `P223-D14` | Startup/workspace reconciliation classifies only. Both accepted sibling backends currently report `ResumeCurrentlyUsable = false`; the user must explicitly re-send. No prior action proposal or permission decision is replayed. |

## Non-Goals

- Backend configuration or binding implementation; Phase 22.2 owns it.
- Trace, memory, or usage surfaces; Phase 22.4 owns them.
- New action kinds, autonomous execution, silent resume, retry/replay of a
  prior material action, or persistent permission grants.
- Multi-file transactions, change sets, or a new rollback subsystem. Their
  absence remains visible in `A1-TP-03` evidence.
- Historical Agent Panel send/routing restoration (`A1-AS-01`, `A1-MR-01`).
- Package 9 UI/friction work, Phase 22.4, Phase 22.5, G5, or V4.

## Milestones

| Milestone | Outcome | Required new tests | Verification gate |
|-----------|---------|--------------------|-------------------|
| M0 | Live dependency, send/routing, projection, termination, broker, continuity, test, harness, migration, and rollback seams verified | Documentation only | Existing focused filters + build/full-suite gates; human acceptance |
| M1 | Direct and channel routing are catalog-typed; routed ownership is discoverable; pre-admission/session rejection and all terminal outcomes are actionable in the correct conversation; routed drafts and inactive channel presentation are conversation-owned across navigation | `Phase22AgentOutcomeProjectionTests`, `Phase22TownhallRoutingOutcomeTests` | Send/routing/projection focused filter — **corrective implementation pushed pending re-audit; 29/29 M1 tests, 100/100 M0+M1 filter, 3878/3878 full suite** |
| M2 | Shipped Townhall termination records intent, bounded acknowledgement, terminal/late state, truthful backend state, and live ownership removal | `Phase22ExplicitSessionTerminationTests` | Session/continuity/termination focused filter |
| M3 | Independently bound Native Harness and ACP can trigger deterministic safe mediated reads/writes through Phase 17 permission, mutation/conflict, audit/actor attribution, and reconciliation seams | `Phase22MediatedActionPathTests`, `Phase22ActionAttributionTests` | Broker/permission/mutation focused filter |
| M4 | Workspace-owned checkpoints, force-quit reconciliation, terminal Townhall projection, and explicit re-send work without silent resume or permission replay | `Phase22InterruptedRunProjectionTests`, `Phase22ContinuityWorkspaceOwnershipTests` | Continuity focused filter + force-quit A3 scenarios |
| M5 | All owned rows have current isolated Native Harness and ACP evidence and all regression gates pass | No substitute unit test; A3 producer required | Full 22.3 A3 matrix + build + fast suite |

Each M1–M4 outcome should be one reviewable commit unless implementation reveals
an independently reversible boundary. Ordinary plan/TOFIX updates belong in the
owning implementation commit.

## Exact Existing Verification Commands

The filters below were confirmed against `--list-tests` and passed during M0.

```bash
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build --list-tests

dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~AgentRouterTests|FullyQualifiedName~AgentExecutionCoordinatorTests|FullyQualifiedName~AgentConversationEventProjectionTests|FullyQualifiedName~AgentSessionServiceTests|FullyQualifiedName~TownhallDirectSendTests|FullyQualifiedName~Phase19TownhallProjectionTests|FullyQualifiedName~Phase22AgentOutcomeProjectionTests|FullyQualifiedName~Phase22TownhallRoutingOutcomeTests'

dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Phase21RestartTests|FullyQualifiedName~Phase21RecoveryTests|FullyQualifiedName~Phase21TerminationTests'

dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Phase17PermissionLifecycleTests|FullyQualifiedName~Phase17PermissionReviewServiceTests|FullyQualifiedName~Phase17ProposalBrokerTests|FullyQualifiedName~Phase17WorkspaceMutationBrokerTests|FullyQualifiedName~Phase17WorkspaceReadBrokerTests|FullyQualifiedName~Phase17SessionEventIntegrationTests|FullyQualifiedName~Phase17DocumentReconciliationTests|FullyQualifiedName~Phase17WorkspaceMutationMutatorTests|FullyQualifiedName~Phase20ActionBridgeTests'

dotnet build Zaide.slnx --no-incremental
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build --settings tests/Zaide.Tests/slow.runsettings
git diff --check
```

Run the fast suite in an interactive terminal. The serial command is a fallback
only when fast mode fails or hangs.

After M1–M4 create their named tests, add their exact fully-qualified class
filters to the owning focused command; do not mark a milestone complete until
`--list-tests` shows every required class and the filter discovers nonzero
tests.

## Future Isolated A3 Producer Command

M4/M5 must create the out-of-tree producer at
`/tmp/zaide-a3-agent-path/runner/Zaide.Tests.csproj`. The accepted command
contract is exact; M0 does not execute it:

```bash
test -f /tmp/zaide-a3-agent-path/runner/Zaide.Tests.csproj
dotnet restore /tmp/zaide-a3-agent-path/runner/Zaide.Tests.csproj
dotnet publish /tmp/zaide-a3-agent-path/runner/Zaide.Tests.csproj \
  --no-restore -c Release -o /tmp/zaide-a3-agent-path/out/Release/net10.0
dotnet build tests/fixtures/acp-fake-agent/AcpFakeAgent.csproj \
  -o /tmp/zaide-a3-agent-path/acp-fixture

for backend_id in native-harness acp; do
  scenario_root="$(mktemp -d /tmp/zaide-a3-agent-path-${backend_id}-XXXXXXXX)"
  profile_root="$scenario_root/profile"
  workspace_root="$scenario_root/workspace"
  mkdir -p "$profile_root/home" "$profile_root/config" "$profile_root/data" \
    "$profile_root/state" "$profile_root/cache" "$workspace_root"

  env HOME="$profile_root/home" \
    XDG_CONFIG_HOME="$profile_root/config" \
    XDG_DATA_HOME="$profile_root/data" \
    XDG_STATE_HOME="$profile_root/state" \
    XDG_CACHE_HOME="$profile_root/cache" \
    timeout --signal=TERM --kill-after=10s 180s \
    dotnet /tmp/zaide-a3-agent-path/out/Release/net10.0/Zaide.Tests.dll \
      --backend "$backend_id" \
      --profile "$profile_root" \
      --workspace "$workspace_root" \
      --acp-fixture /tmp/zaide-a3-agent-path/acp-fixture/AcpFakeAgent \
      --scenario-matrix A1-AS-02,A1-TH-05,A1-MR-03,A1-TP-01,A1-TP-02,A1-TP-03,A1-TC-05,A1-TC-09 \
      --evidence "$scenario_root/evidence.json"
done
```

The producer owns its child process group, uses bounded per-step timeouts,
force-kills the complete child tree on failure, retains evidence before cleanup,
and never uses the Zaide repository as the runtime workspace. See the M0 report
for the scenario and evidence contract.

## Exit Conditions

- [x] Human M0 acceptance and separate M1 implementation approval are recorded.
- [x] Direct/channel routing and failure outcomes are visible, actionable,
  ordered, and attributed without a second conversation writer (M1 scope).
- [ ] Explicit termination is reachable and never overclaims provider state.
- [ ] Mediated action, permission, audit attribution, conflict, reconciliation,
  stale-base, and final-consumption invariants pass; rollback absence remains
  explicit.
- [ ] Interrupted-run smoke proves workspace ownership, terminal projection,
  explicit re-send, and no silent resume or permission replay.
- [ ] `A1-AS-02`, `A1-TH-05`, `A1-MR-03`, `A1-TP-01`…`A1-TP-03`,
  `A1-TC-05`, and `A1-TC-09` have current isolated evidence for Native Harness
  and ACP, with limitations truthfully classified.

## Rollback and Migration Boundaries

- Revert only the owning M1–M4 commit; never revert Phase 22.2 binding history,
  Phase 17 broker invariants, Phase 21 durable records, or historical A0–A4
  evidence.
- A projection rollback must leave exactly one writer and must not strand a UI
  command that calls a removed outcome path.
- A termination rollback must remove the shipped control and its command
  together; it must not leave an enabled control backed by partial teardown.
- A mediated-action rollback removes only the Phase 22.3 fixture/entry seam and
  actor attribution additions; it must not bypass or reorder Phase 17.
- A continuity rollback disables the new workspace-owned reconcile trigger and
  preserves old records read-only. No record deletion, key rewrite, or schema
  downgrade is allowed without a separately accepted migration/restore plan.
- No persistence schema change is currently approved. If M1–M4 discovers one is
  required, stop for a material plan decision before implementation.
