# Phase 22.4 M0 — Trace / Memory / Usage Live-Seam Verification

## Status and authorization

**M0 documentation is accepted at baseline
`66cfb11443791cd4e3bb7cf9599cee29ef3ea79d`.** Human acceptance and separate
M1-only implementation authorization were granted under the user's standing
GO direction on 2026-08-04. M2–M4, Phase 22.5, G5, and V4 remain unauthorized.

This was a read-only production-seam audit. No production code or tests were
changed, no build or test suite was run, and no runtime smoke was executed.

## Baseline and dependency gate

| Check | Verified result |
|-------|-----------------|
| Branch | `master` |
| M0 baseline `HEAD` | `66cfb11443791cd4e3bb7cf9599cee29ef3ea79d` |
| M0 baseline `origin/master` | `66cfb11443791cd4e3bb7cf9599cee29ef3ea79d` |
| Working tree before M0 | Clean (`## master...origin/master`) |
| Phase 22.2 dependency | Complete; package-2 PASS restored at live evidence head `dfe2bf14` |
| Phase 22.3 relationship | Complete and outside this sub-phase's implementation scope |
| Native Harness / ACP | Independently registered `IAgentBackend` siblings; no wrapper or fallback relationship |
| Audit rows owned here | `A1-TC-02`, `A1-TC-03`, `A1-TC-08`; A4 BL-09…BL-11 and `A1-XX-03` |
| Runtime smoke in M0 | Not executed |

The current baseline post-dates the Phase 22.2 evidence head. Review of
`dfe2bf14..HEAD` found later Phase 22.3 changes to Townhall/session continuity,
but no trace, memory, usage, durable-store, or sibling-backend ownership
transfer. The dependency remains satisfied for ordering; it does not authorize
22.4 implementation.

## Evidence read

- [Roadmap V3](../../../roadmap/V3.md)
- [Phase 22 umbrella plan](../phase-22/IMPLEMENTATION_PLAN.md)
- [Phase 22 umbrella work board](../phase-22/TOFIX.md)
- [Phase 22.4 plan](./IMPLEMENTATION_PLAN.md)
- [Phase 22.4 work board](./TOFIX.md)
- [A2 trace/memory/usage wiring evidence](../../../audits/v1-v3-product-reality/evidence/A2_TRACE_MEMORY_USAGE_TERMINATION.md)
- [A3 trace/memory/usage preflight](../../../audits/v1-v3-product-reality/evidence/A3_TRACE_MEMORY_USAGE_TERMINATION_PREFLIGHT.md)
- [A3 clean-profile closeout](../../../audits/v1-v3-product-reality/evidence/A3_CLEAN_PROFILE_SMOKE.md)
- [A4 gap report](../../../audits/v1-v3-product-reality/A4_GAP_REPORT.md)
- [Phase 21 plan](../phase-21/IMPLEMENTATION_PLAN.md) and its M1–M7 evidence
- [Phase 22.2 closeout](../phase-22.2/CLOSEOUT.md)

Historical A2/A3 evidence is an input, not current proof. Every conclusion
below was rechecked against the live checkout.

## Live product reachability

### Shell, Townhall, commands, and Views

The production shell creates one `TownhallView` in `MainLayoutBuilder` and
assigns `MainWindowViewModel.TownhallViewModel` in `MainWindow`. The current
Townhall visual tree hosts chat, backend binding, context policy, and input
controls. It does not host a trace, memory, usage, or integrated transparency
View.

The command registry receives commands from shell/editor/workflow/debug/source
control owners. No command descriptor with a trace, memory, usage, cost, or
transparency ID exists. `App.OnFrameworkInitializationCompleted` does not
resolve or attach `AgentTransparencyManagementViewModel` or any of its child
inspection ViewModels.

| Surface | DI registered | Production View | Command | User reachable | Live result |
|---------|---------------|-----------------|---------|----------------|-------------|
| Integrated management | Yes | No | No | No | Registered-only `AgentTransparencyManagementViewModel` |
| Trace inspection | Yes | No | No | No | `AgentTraceInspectionViewModel` has read APIs only |
| Memory management | Yes | No | No | No | CRUD methods exist on `AgentMemoryInspectionViewModel` only |
| Usage/cost inspection | Yes | No | No | No | `AgentUsageInspectionViewModel` has read APIs only |

The prior A3 `Missing` / `UNWIRED` classifications therefore remain accurate
at this baseline. Phase 22.2 made backend binding user-reachable, and Phase
22.3 added termination/outcome paths, but neither change made these three
surfaces reachable.

### Selection, loading, empty, unavailable, and failure states

The current presentation seams do not provide a complete user-state model:

- trace, usage, and memory availability projections default their workspace
  provider to `null`, so production resolution observes `ws:unbound` instead
  of the opened workspace;
- the inspection ViewModels accept a cursor/page size but own no selected
  record, active scope, loading flag, retry command, or visible failure state;
- projection refresh exceptions are swallowed, so storage/read failure is
  indistinguishable from stale presentation state;
- trace and usage captions distinguish capture-disabled from enabled, and
  memory has a truthful empty caption, but none is rendered;
- `AgentUsageInspectionSummary.Empty` necessarily carries numeric zero while
  `IsEmpty == true` and currency is absent. A future View must render this as
  unavailable evidence, never as verified zero usage or cost.

## Production ownership and data boundaries

### Trace

The authoritative flow remains:

```text
backend-specific evidence source
  -> AgentTraceBackendEvidenceSourceWriter
  -> AgentTraceCoordinator
  -> AgentTraceCaptureSink
  -> mandatory AgentTraceRedactionProcessor
  -> bounded nonblocking queue
  -> IAgentDurableRecordStore Trace partition
  -> IAgentTraceInspector
  -> presentation
```

Verified invariants:

- redaction occurs before durable admission; failure retains only a bounded
  failure marker;
- capture states remain distinct: disabled, unavailable, captured, redacted,
  sampled, truncated, summarized, and failed;
- the default payload bound is owned by `AgentTraceCaptureLimits`; queue
  backpressure rejects rather than blocking the agent event pipeline;
- Trace retention metadata is 30 days and remains owned by the Trace record
  class; display verbosity does not change retention;
- export reads the already-redacted durable envelope through
  `AgentTransparencyLifecycleCoordinator`; the View must never receive the
  pre-redaction payload;
- security audit, conversation entries, and optional trace records remain
  separate owners.

Live gaps:

- capture is application-memory state, disabled by default, with no production
  caller of `EnableCapture` or `DisableCapture`;
- Native Harness and ACP trace sources are registered independently but have
  no execution-path `Submit` caller;
- the Native source is limited to its admitted public loop evidence; the ACP
  source is limited to public protocol envelope facts and opaque body markers.
  Neither source may claim hidden reasoning or chain-of-thought.

M1 locks an explicit user action to enable or disable capture for the current
application lifetime only. Restart always returns to disabled. This is not a
display-verbosity toggle, is not silently enabled by binding or opening the
View, and adds no persisted policy or schema. A request for a persisted default
is a plan-amendment stop condition.

### Memory

`AgentMemoryCoordinator` is the application owner for lifecycle mutations and
routes every append through `AgentMemoryStoreWriter`; `AgentMemoryInspector`
owns replay/projection; `AgentMemoryLifecycleService` owns memory export and
backup projection. The conversation store remains authoritative history and is
not a memory store.

The lifecycle contract already supports:

- create, correct, disable, supersede, and tombstone delete;
- Session, Agent, Conversation, and Project/Shared scopes;
- workspace isolation, provenance, author/source revision, created/updated/
  validation times, schema version, conflicts, poisoning/stale markers, and
  supersession links;
- export/backup/replay/idempotency without rewriting conversation or audit
  history.

Production `AgentSessionService` independently performs budgeted retrieval and
records influence as `Recorded`, `NoneEligible`, or `Unavailable`. Influence
payloads are not lifecycle `AgentMemoryRecord` revisions and must not appear as
editable memory records.

Live gaps:

- no production user path creates or manages a lifecycle memory record;
- no View exposes scope, provenance, conflict, status, or influence evidence;
- current read methods can resolve `ws:unbound` instead of the opened workspace;
- the existing APIs do not supply user-form provenance automatically.

M2 must derive the workspace from `IWorkspaceActionAuthority`, the author from
`IActorCatalog.CanonicalHuman`, and Session/Agent/Conversation targets from the
selected Townhall direct-conversation context. Project/Shared uses the opened
workspace identity. User edits receive `AgentMemorySourceKind.User` provenance
and a new operation revision/idempotency identity; they never write a
conversation entry. Missing required scope context disables submission with a
visible reason.

### Usage and cost

The authoritative flow remains:

```text
backend-specific evidence source
  -> AgentUsageBackendEvidenceSourceWriter
  -> AgentUsageCoordinator / AgentUsageCaptureSink
  -> IAgentDurableRecordStore Usage partition
  -> IAgentUsageInspector
  -> presentation
```

The ledger preserves metric name, unit, value, origin, backend, optional model,
conversation/session/run scope, timestamps, evidence description, currency,
pricing source/version/effective time/formula, rounding, and uncertainty.
`Reported`, `Measured`, `Calculated`, `Estimated`, `Invoiced`, `Unavailable`,
and `Disputed` remain distinct. Missing cost must use `Unavailable`; a
non-unavailable zero cost is rejected.

Live gaps:

- capture is disabled and no Native Harness or ACP execution path submits a
  usage request;
- Native Harness provider responses retain no token or cost payload;
- ACP retains a raw stable `usage_update` envelope and only upgrades capability
  observation; it does not admit the values to the ledger;
- current summary aggregation cannot by itself prove whether a backend value
  is a delta or a cumulative session snapshot;
- no user surface exposes origin, unit, attribution, pricing, unavailable,
  estimated, or disputed state.

M3 must never invent token counts, prices, or invoice facts. Native Harness may
publish only Zaide-measured request count/latency and explicit unavailable
token/cost markers unless its real provider response exposes more. ACP may map
the stable public `usage_update` fields as backend-`Reported` session evidence:
`used` and `size` are point-in-time context-token values, while optional
`cost.amount` is cumulative session cost in `cost.currency`. They are not
input/output token counts or per-run deltas.

M3 locks one additive `AgentUsageAggregationSemantics` payload field with
`Unknown`, `Delta`, `Cumulative`, and `PointInTime` values. Existing records
decode as `Unknown` without a partition migration. Request count is `Delta`,
latency is `PointInTime`, ACP `used`/`size` are `PointInTime`, and ACP cost is
`Cumulative`. Summary totals sum only `Delta` cost records; for `Cumulative`
cost they select the latest record per backend/session/currency and then sum
those latest values. `Unknown` cost is listed but excluded from a verified
aggregate, which must display unavailable rather than zero. Pricing source
remains unavailable unless separately supplied; reported cost is not an
invoice.

### Independent sibling backends

Production DI registers `AcpActionCapableAgentBackend` and
`NativeHarnessAgentBackend` as separate `IAgentBackend` instances. It also
registers separate trace sources, usage sources, and continuity adapters for
the two backend IDs. Selection remains explicit through the Phase 22.2 binding
store. Phase 22.4 may add only narrow evidence hooks to each owning backend
path; it may not wrap one backend in the other, share backend-private state, or
fall back silently.

## Locked user-reachability design

The smallest coherent production surface is one Townhall-owned management
panel, not a new window or settings page:

1. `TownhallView` hosts an Agents presentation control for Trace, Memory, and
   Usage tabs alongside the existing direct-agent workflow.
2. A visible Townhall button and command-palette entries open the same panel.
   Canonical command IDs are `agent.trace.open`, `agent.memory.open`, and
   `agent.usage.open`; all are category `Agent` and have no default gesture.
3. Command registration occurs in App composition against commands owned by
   `AgentTransparencyManagementViewModel`; feature code does not depend on
   App composition contracts.
4. The panel uses the opened workspace and active Townhall direct conversation
   as context. Switching workspace/conversation cancels stale loads, clears
   selection, and reloads the new scope. Channel selection may show
   workspace-wide records but cannot infer an Agent/Session scope.
5. Every tab has explicit `Loading`, `Ready`, `Empty`, `Unavailable`, and
   `Failed` presentation states, a bounded retry, and one selected record.
   Stale or failed reads never masquerade as empty.
6. Keyboard traversal, visible focus, named tabs/controls, screen-reader value
   text, and bounded paging are verified against the real View. Constant-only
   tests do not prove accessibility.
7. Presentation calls application coordinators only. It never writes durable
   files, backend transports, or conversation history directly.

## Exact later milestone boundaries

| Milestone | Authorized outcome after later approval | Exact boundary |
|-----------|-----------------------------------------|----------------|
| M1 — Trace | User-reachable trace tab, application-lifetime explicit capture control, opened-workspace inspection, independent Native/ACP truthful producer hooks, redacted export/status projection | No persistent capture setting; no hidden thought; no retention/schema change; no audit/conversation write |
| M2 — Memory | User-reachable scoped lifecycle list/create/correct/disable/supersede/delete with provenance/conflict/influence distinction | Existing `AgentMemoryCoordinator` only; no automatic memory creation/import, new retrieval algorithm, prompt injection, or conversation rewrite |
| M3 — Usage/cost | User-reachable records with origin, unit, scope, backend/model attribution, pricing/currency/uncertainty, locked aggregation semantics, and explicit unavailable/estimated/disputed states; narrow truthful producer hooks | No price catalog/network lookup; no inferred tokens/cost; no cumulative-as-delta aggregation; no invoice claim |
| M4 — Integration and re-smoke | Townhall commands/View reachability, real accessibility/failure-state coverage, lifecycle export/backup safety, regression gates, and isolated `A1-TC-02`, `A1-TC-03`, `A1-TC-08` evidence for both sibling backends | No new product behavior beyond M1–M3; no restore/migrate UI; no G5 or V4 claim |

M1–M3 are independently reviewable and revertible. M4 is integration and
evidence closeout, not a place to add missing product semantics.

## Exact later test gates

Every filter must discover all named future test classes and at least one test.
`No test matches` is failure.

### M1 — Trace

```bash
dotnet build Zaide.slnx --no-incremental
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Phase21Trace|FullyQualifiedName~Phase21Redaction|FullyQualifiedName~Phase22TraceSurfaceTests|FullyQualifiedName~Phase22TraceProducerTests|FullyQualifiedName~Phase21TraceRatchetTests|FullyQualifiedName~Phase21StorageOwnershipRatchetTests'
```

### M2 — Memory

```bash
dotnet build Zaide.slnx --no-incremental
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Phase21MemoryStore|FullyQualifiedName~Phase21MemoryPolicy|FullyQualifiedName~Phase21MemoryLifecycle|FullyQualifiedName~Phase21MemoryRetrieval|FullyQualifiedName~Phase21MemoryInfluence|FullyQualifiedName~Phase22MemorySurfaceTests|FullyQualifiedName~Phase21MemoryRatchetTests'
```

### M3 — Usage/cost

```bash
dotnet build Zaide.slnx --no-incremental
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Phase21Usage|FullyQualifiedName~Phase21Cost|FullyQualifiedName~Phase22UsageSurfaceTests|FullyQualifiedName~Phase22UsageProducerTests|FullyQualifiedName~Phase21UsageRatchetTests'
```

### M4 — Integrated production reachability and regression

```bash
dotnet build Zaide.slnx --no-incremental
dotnet test tests/Zaide.Tests/Zaide.Tests.csproj --no-build \
  --filter 'FullyQualifiedName~Phase22TransparencyReachabilityTests|FullyQualifiedName~Phase22TransparencyAccessibilityTests|FullyQualifiedName~Phase22TransparencyFailureStateTests|FullyQualifiedName~Phase22TransparencyBackupTests|FullyQualifiedName~Phase21TransparencyIntegration|FullyQualifiedName~Phase21Export|FullyQualifiedName~Phase21Backup|FullyQualifiedName~AgentsRegistrationModuleTests|FullyQualifiedName~TownhallRegistrationModuleTests|FullyQualifiedName~Architecture'
dotnet test Zaide.slnx --no-build
dotnet test Zaide.slnx --no-build \
  --settings tests/Zaide.Tests/slow.runsettings
git diff --check
```

Run fast commands interactively. The serial suite is a fallback only if the
fast suite fails or hangs, except that final M4 closeout must record which full
suite mode supplied the accepted result.

## Locked isolated A3 re-smoke procedure

M4 must create a Phase 22.4 producer source under
`tests/a3-transparency/runner/`, copy/publish it out of tree at
a unique `/tmp/zaide-a3-transparency-producer-*` root, and execute only after
separate M4 authorization. M0 does not create or run it.

```bash
test -f tests/a3-transparency/runner/Zaide.Tests.csproj
test -f tests/a3-transparency/runner/Program.cs
producer_root="$(mktemp -d /tmp/zaide-a3-transparency-producer-XXXXXXXX)"
mkdir -p "$producer_root/runner"
cp tests/a3-transparency/runner/Zaide.Tests.csproj \
  tests/a3-transparency/runner/Program.cs \
  "$producer_root/runner/"
dotnet restore "$producer_root/runner/Zaide.Tests.csproj"
dotnet publish "$producer_root/runner/Zaide.Tests.csproj" \
  --no-restore -c Release -o "$producer_root/out/Release/net10.0"
dotnet build tests/fixtures/acp-fake-agent/AcpFakeAgent.csproj \
  -o "$producer_root/acp-fixture"

for backend_id in native-harness acp; do
  scenario_root="$(mktemp -d /tmp/zaide-a3-transparency-${backend_id}-XXXXXXXX)"
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
    dotnet "$producer_root/out/Release/net10.0/Zaide.Tests.dll" \
      --backend "$backend_id" \
      --profile "$profile_root" \
      --workspace "$workspace_root" \
      --acp-fixture "$producer_root/acp-fixture/AcpFakeAgent" \
      --scenario-matrix A1-TC-02,A1-TC-03,A1-TC-08 \
      --evidence "$scenario_root/evidence.json"
done
```

The producer must configure isolation before `Program.ConfigureServices`, use
the shipped Townhall controls and command registry, and bind each backend in a
separate process. Native Harness may use the already accepted deterministic
loopback provider; ACP uses the repository fake agent. Neither backend may
wrap or fall back to the other. The runner must not call inspection/CRUD/
capture source methods directly to manufacture success.

Required evidence per backend:

- repo HEAD, backend ID, binding fingerprint/revision, disposable workspace
  key, command IDs, active conversation/session/run IDs, record sequences,
  step times, assertion counts, and cleanup result;
- trace: explicit capture opt-in, real admitted run, selected record, evidence
  level/source, capture/redaction state, size/retention/export labels, and a
  fixture-secret absence assertion;
- memory: create/list/select/correct/disable/supersede/delete through shipped
  controls, all four scope labels where context permits, provenance/conflict
  state, and proof that conversation history is unchanged;
- usage: real backend path, metric/unit/origin/backend/session/run attribution,
  truthful currency/pricing state, and unavailable/estimated/disputed labels.
  Native unavailable cost and ACP reported cumulative cost are expected to
  differ without changing product status semantics;
- both backends: loading, empty, unavailable, failure/retry, keyboard focus,
  screen-reader names, bounded paging, and zero real-profile/repository writes.

Evidence must be retained before disposable state is removed. Real credentials,
network/provider calls, the real user profile, and the Zaide repository as a
runtime workspace are forbidden.

## Rollback, backup, and migration boundary

- **No schema or data migration is required or authorized.** Surface state is
  presentation/application state over existing schema-v1 partitions.
- Existing Trace, Usage, Memory, Audit, SessionRecovery, conversation, and
  binding data must be preserved through every milestone and rollback.
- M1 rollback: disable trace capture, drain/reject pending queue work, revert
  only M1, and leave Trace partitions readable/exportable.
- M2 rollback: stop new lifecycle writes, export/quarantine readable memory if
  needed, revert only M2, and leave memory plus influence records unchanged.
- M3 rollback: disable usage capture, revert only M3, preserve Usage records,
  and never rewrite unavailable/estimated/reported values as zero or verified.
- M4 rollback: remove only the integrated host/command/test-producer changes;
  preserve accepted M1–M3 owners and data.
- If implementation later needs a store schema change, destructive retention,
  automatic migration, or downgrade path, stop and amend M0 with an exact
  backup digest, compatibility matrix, quarantine, restore, and rollback test
  before changing production data.

The live lifecycle coordinator has an existing clean-profile failure-state
defect: `Backup` returns an empty path for missing/unavailable partitions while
`AgentTransparencyBackupPackage` rejects an empty path. M4 must cover and
correct this failure state before exposing Backup. Restore and Migrate remain
application-only and are not user surfaces in Phase 22.4.

## Stop conditions for later implementation

Stop and request a plan amendment if any milestone would:

1. move record ownership into Townhall, a ViewModel, a backend, or the
   conversation store;
2. persist unredacted trace input or let display state silently enable capture;
3. require a new capture-policy/settings schema rather than the locked
   application-lifetime opt-in;
4. invent usage values, pricing, invoice certainty, or aggregate a cumulative
   snapshot as a delta;
5. auto-create/import/inject memory, cross workspaces, or rewrite conversation
   or audit history;
6. add a database, dependency, network pricing service, credential, provider
   call, destructive migration, or real-profile access;
7. wrap, merge, or silently fall back between Native Harness and ACP;
8. weaken the Phase 17 broker/final `TryConsume()` boundary or another Phase
   21/22 accepted invariant;
9. discover zero tests for a required filter or fail a required gate.

## M0 disposition

- Production user reachability for all three owned rows is currently absent.
- Existing application/domain ownership is sufficient; no ownership transfer
  or new persistence engine is required.
- No schema migration is required; the only backup issue found is a bounded
  existing failure-state defect assigned to M4 before user exposure.
- The M1–M4 scope, exact commands, A3 isolation, rollback, and stop boundaries
  are locked for human review.
- No runtime behavior or A3 classification was changed by this M0.

**M0 is accepted. M1-only implementation is authorized; M2–M4, Phase 22.5,
G5, and V4 remain unauthorized.**
