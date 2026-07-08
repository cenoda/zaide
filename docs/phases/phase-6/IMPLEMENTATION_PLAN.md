# Phase 6: Agent-to-Agent Router — Implementation Plan

## Pre-Implementation Verification

- [x] Confirm Phase 5 is complete in live docs/code, not just roadmap wording
- [x] Verify current build succeeds: `dotnet build Zaide.slnx --no-restore`
- [x] Verify current tests pass: `dotnet test Zaide.slnx --no-build`
- [x] Re-check `src/ViewModels/MainWindowViewModel.cs`, `src/ViewModels/AgentPanelHost.cs`, `src/Views/AgentPanelHostView.cs`, `src/ViewModels/TownhallViewModel.cs`, `src/ViewModels/AgentExecutionCoordinator.cs`, and `src/Models/AgentPanelState.cs`
- [x] Confirm direct execution remains intentionally narrow: one OpenAI-compatible, non-streaming request path only
- [x] Confirm routing is still absent from live code and must be introduced as a new seam outside `MainWindowViewModel`
- [x] Confirm new panel creation still uses generic identity values in `AgentPanelHostView`
- [x] Confirm Townhall remains the shared activity ledger and Phase 6 should route visibility through Townhall rather than new shell chrome

## Implementation Status

**Implemented with documented limitations (closed 2026-07-08 via M6).**

This plan was written against the live Phase 5 checkout on 2026-07-08 and was
implemented across M0–M5. M6 is the doc-sync and regression-sweep milestone.

The Phase 5 base proved viable for routing. The four constraints the plan had
to respect were handled as follows (verified against live code at M6 closeout):

- `AgentPanelHostView` no longer creates generic identity panels — `AgentPanelHost.CreatePanel()`
  now seeds stable, distinguishable identities (`alpha/Alpha`, `beta/Beta`, …) and
  falls back to `agent-N` naming once the seed list is exhausted.
- `MainWindowViewModel.SendAgentMessageAsync(...)` remains a thin delegating seam: it
  mirrors the user request into Townhall, delegates to `IAgentRouter`, then mirrors the
  source panel's post-execution state. It does **not** own routing policy.
- `AgentExecutionCoordinator` still knows nothing about Townhall or routing.
- `AgentExecutionService` is still the single-provider, non-streaming path; no
  provider/platform abstraction was added.

See **Exit Conditions** and **Limitations (by design)** for what is and is not
truthfully shipped in M6.

## Live Baseline (as implemented at M6 closeout)

Verified against the live checkout at `42063fb` on 2026-07-08:

- `docs/roadmap/PHASES.md` defines Phase 6 as agent-to-agent routing and keeps
  Git integration in Phase 7. At M6 it is marked implemented with limitations.
- `docs/architecture/OVERVIEW.md` previously listed Agent Router as a planned
  Phase 6 layer; at M6 it is updated to reflect the implemented seam.
- `src/ViewModels/MainWindowViewModel.cs` owns the thin app-level send seam:
  mirrors the user request into Townhall, delegates to `IAgentRouter`
  (`AgentRouter.RouteAndExecuteAsync`), then mirrors the source panel's outcome.
  It does **not** own mention parsing, target resolution, or route policy.
- `src/ViewModels/AgentPanelHost.cs` owns panel collection and active selection
  only; `CreatePanel()` seeds stable identities and falls back to `agent-N`.
- `src/Views/AgentPanelHostView.cs` retains panel views and has an explicit
  `DetachHost()` cleanup path that detaches host-level and per-panel
  subscriptions and releases retained views/tabs; `SetHost(...)` rebinds safely
  through it. It does not reference routing, execution, or Townhall types.
- `src/Services/MentionParser.cs` is the narrow deterministic mention parser
  (zero or one `@AgentName`, case-insensitive exact match against visible
  `AgentName`, mention stripped on success, explicit failure otherwise).
- `src/Models/RouteRequest.cs` / `src/Models/RouteResult.cs` are the narrow
  route request/result records used by the parser and router.
- `src/ViewModels/AgentRouter.cs` is the dedicated routing orchestration seam
  outside `MainWindowViewModel`. It parses, decides direct vs routed, resolves
  the target panel by `AgentName`, and invokes `IAgentExecutionCoordinator`.
- `src/ViewModels/TownhallViewModel.cs` remains the shared activity ledger.
- `src/ViewModels/AgentExecutionCoordinator.cs` still owns only per-panel
  execution state and output history; no Townhall or parsing logic.
- `src/Models/AgentPanelState.cs` still has stable identity fields and no route
  or debate-thread state.

## Scope

**Goal:** Add the first real agent-to-agent routing slice so a user can direct a
request to a specific agent, allow that agent to request another agent's help
through a narrow routing seam, and surface the resulting flow visibly in
Townhall without widening the execution platform or redesigning the shell.

**In scope:**

- Stable agent identity and non-generic panel creation for routing
- A tiny route parsing and route result model
- A dedicated routing orchestration seam outside `MainWindowViewModel`
- Minimal `@mention` routing from one panel context to another
- Townhall-visible route/debate activity at a summary level
- Explicit cleanup for agent-panel host view subscriptions before routing adds
  more long-lived events
- Focused tests for parse/orchestration/Townhall visibility behavior

**Out of scope:**

- Multi-provider execution architecture
- Streaming responses
- Tool calling
- Full transcript persistence
- Arbitrary multi-hop routing graphs
- Deep thread trees or rich debate visualization chrome
- Whole-shell redesign or new top-level layout regions
- Git integration

## Phase-Level Decisions

### 1. Phase 6 stays routing-first, not platform-first

The first routing slice must build the minimum behavior needed for visible
agent-to-agent handoff. It must not introduce `IAgentProvider`,
`AgentRegistry`, streaming, tool-calling, or provider-picker work unless live
implementation proves Phase 6 is impossible without it.

### 2. Routing ownership moves into a dedicated seam

`MainWindowViewModel` is currently a valid composition point, but it must not
become the routing engine. Phase 6 should introduce a narrow routing seam that:

- accepts a route-capable request
- resolves source and target agent/panel identities
- invokes the existing execution path in a controlled order
- returns enough result information for Townhall mirroring

`MainWindowViewModel` may still delegate into this seam, but it should not own
mention parsing, route policy, or debate sequencing itself.

### 3. Agent identity must become explicit before routing

Routing semantics are not trustworthy while `AgentPanelHostView` still creates
panels with generic values. Before mention routing lands, Phase 6 must lock a
small identity policy for newly created panels:

- each panel receives a stable, distinguishable `AgentId`
- each panel receives a distinct visible `AgentName`
- avatar selection remains narrow and may still use existing resource keys

The implementation can seed from a small built-in identity list. It does not
need dynamic registration or a general agent catalog yet.

### 4. Route parsing/result types should exist before orchestration logic grows

The temporal report is correct here: Phase 6 should add a tiny route
request/result model before `@mention` behavior spreads across the shell.

That model should stay intentionally narrow:

- parse direct user text for zero or one explicit mention target
- represent the resolved source panel/agent, optional target panel/agent, and
  the content body to execute
- represent simple outcomes such as direct send, routed send, missing target,
  and visible routed failure

This is enough to keep orchestration testable without inventing a future debate
graph model now.

### 5. Townhall remains the primary visibility surface

Phase 6 should make routing visible primarily through Townhall activity rather
than through new persistent shell chrome. The shared workspace should show the
important user-visible routing events:

- user asked agent A something
- agent A requested agent B review/help
- agent B responded or failed visibly
- disagreement/review outcomes are surfaced at a useful summary level

This keeps the product direction aligned with the Townhall-first architecture.

### 6. Agent-panel host cleanup should land early

`AgentPanelHostView` already subscribes to host collection/property changes and
per-panel send events, but it has no explicit cleanup/dispose path. Phase 6
should fix this early, before router-related subscriptions or events grow that
surface.

## Proposed Implementation Shape

The current intended Phase 6 shape is:

- `src/Models/` add one narrow route request model and one narrow route result
  model
- `src/Services/` add one parser seam for route/mention parsing if the logic is
  large enough to deserve isolation; otherwise keep parsing in a focused
  dedicated routing component
- `src/ViewModels/` add a dedicated router/orchestrator seam that composes:
  `IAgentPanelHost`, `IAgentExecutionCoordinator`, and the current Townhall
  mirroring path
- `src/ViewModels/MainWindowViewModel.cs` remain a thin composition/delegation
  seam rather than the router owner
- `src/Views/AgentPanelHostView.cs` stop creating generic anonymous panels and
  gain an explicit cleanup path for retained subscriptions/views

This phase should prefer the smallest number of new types that still keeps the
routing behavior legible and testable.

## Mention Parsing Decision

The first routing slice should support one explicit narrow mention form only.

Recommended baseline:

- user sends normal text with no mention -> direct send to the active panel
- user prefixes or includes one `@AgentName` mention -> route target resolves to
  the matching visible panel/agent identity

Do not support multiple mentions, fuzzy matching, nested thread syntax, or
free-form routing grammars in the first slice.

Before implementation begins, Phase 6 should lock:

- exact matching rule (`AgentName`, normalized case, or another small policy)
- behavior for unknown mention targets
- whether the mention token is stripped before the target agent receives the
  content

Recommendation for the first slice:

- case-insensitive exact match against visible `AgentName`
- one target only
- strip the matched mention token from the routed content
- unknown mention becomes an explicit visible failure/result, not silent fallback

## Routing Orchestration Rule

The router added in this phase should own only routing behavior, not execution
transport behavior.

Expected responsibility split:

- `AgentExecutionService`: HTTP request/response only
- `AgentExecutionCoordinator`: per-panel execution state and output history only
- Phase 6 router/orchestrator: parse route intent, resolve target/source panel,
  choose direct vs routed flow, and provide Townhall-visible outcome data
- `MainWindowViewModel`: thin entrypoint that delegates into the router seam

This preserves the good Phase 5 boundaries while creating the new Phase 6 seam
in the right place.

## Debate Visibility Constraint

Phase 6 should surface disagreement/review in the smallest useful form.

The first slice does not need a full debate system. It only needs a user-visible
summary flow that proves routing works:

- source agent asked target agent for review/help
- target agent returned feedback or disagreement
- Townhall shows those visible routing events clearly

If implementation reveals that a richer thread model is necessary, stop and
document the evidence before widening the plan.

## M0 Locked Decisions

The following decisions are explicitly locked for `M0 only`. `M1+` work must
not begin until these decisions are reflected in this plan and the live-code
seam audit still supports them.

### 1. Agent Identity Policy

**Decision:** New agent panels must stop using generic placeholder identity
values. Phase 6 will use a small built-in seeded identity list, and each new
panel will receive:

- a stable seeded `AgentId`
- a distinct visible `AgentName`
- an existing avatar resource key

The first implementation does not need dynamic registration, persistence, or a
general agent catalog.

**Locked baseline shape:**

- identity assignment remains narrow and deterministic
- seeded identities may be reused only after the seeded list is exhausted and
  the fallback naming rule is explicitly documented in `M1`
- `PanelId` remains the unique per-panel instance identifier; routing identity
  resolves primarily through seeded agent identity, not through display text
  alone

**Rationale:**

- `AgentPanelHostView.OnNewPanelClick(...)` currently creates panels with
  `("agent", "Agent", "Icon.Avatar")`, which makes routing ambiguous as soon
  as more than one panel exists.
- `AgentPanelState` already has the right narrow identity fields
  (`AgentId`, `AgentName`, `AvatarResourceKey`), so Phase 6 does not need a new
  broad identity model to become routing-safe.

**Live-code evidence:**

- `src/Views/AgentPanelHostView.cs` currently hardcodes generic identity values
  in `OnNewPanelClick(...)`.
- `src/ViewModels/AgentPanelHost.cs` already accepts explicit identity values in
  `CreatePanel(...)`.
- `src/Models/AgentPanelState.cs` already stores the required narrow identity
  shape.

### 2. Mention Syntax Policy

**Decision:** The first routing slice supports one explicit mention target only,
using case-insensitive exact matching against visible `AgentName`.

**Locked syntax behavior:**

- no mention present -> direct send to the source panel
- exactly one recognized `@AgentName` mention -> routed send to that target
- multiple mentions in one message -> explicit visible failure in the first
  slice, not best-effort routing
- fuzzy matching, aliases, nested thread syntax, and free-form routing grammar
  remain out of scope

**Rationale:**

- This is the smallest rule set that proves routing works without turning Phase
  6 into a speculative parser-design problem.
- The UI already exposes visible agent names in the tab strip and panel header,
  so exact-name matching is understandable to the user.

**Live-code evidence:**

- No routing parser exists in the current checkout.
- `AgentPanelHost` already exposes the live panel collection needed for exact
  visible-name lookup.

### 3. Unknown-Target Behavior

**Decision:** Unknown, ambiguous, or unsupported mention targets must produce an
explicit visible failure/result. They must not silently fall back to direct send.

**Locked failure cases:**

- unknown `@AgentName`
- duplicate visible names if implementation accidentally permits them
- multiple mentions in one message during the first slice

**Expected user-visible behavior:**

- the source panel shows a visible error/result
- Townhall receives a truthful visible failure summary

**Rationale:**

- Silent fallback would make routing appear to work while actually sending the
  request to the wrong execution target.
- The temporal report explicitly pushes stable identity first because ambiguous
  routing is the key current risk.

### 4. Mention-Stripping Rule

**Decision:** When a recognized `@AgentName` target is parsed successfully, the
matched mention token is stripped before the target agent receives the routed
content.

**Locked behavior:**

- strip the routed target token only
- preserve the remaining message text as closely as possible
- if stripping leaves no meaningful content, treat that as an explicit visible
  failure/result instead of sending an empty request

**Rationale:**

- The mention token is routing metadata, not user intent that the target model
  needs to see.
- Keeping the send payload free of routing markup keeps the execution seam
  narrow and easier to test.

### 5. Router Ownership Seam

**Decision:** `MainWindowViewModel` remains a thin entrypoint only. Phase 6 must
introduce a dedicated routing seam outside `MainWindowViewModel` that owns:

- mention parsing
- target resolution
- direct-vs-routed decision-making
- routed execution ordering
- Townhall-visible routing outcome data

`MainWindowViewModel` may delegate into that seam, but must not absorb route
policy.

**Locked boundary split:**

- `AgentExecutionService` keeps HTTP transport only
- `AgentExecutionCoordinator` keeps per-panel execution state only
- the new router seam owns routing behavior only
- `MainWindowViewModel` owns shell composition/delegation only

**Rationale:**

- `MainWindowViewModel.SendAgentMessageAsync(...)` already handles direct-send
  Townhall mirroring. If routing policy is added there directly, it will become
  the de facto router and collapse the intended boundary.
- `AgentExecutionCoordinator` is intentionally free of Townhall and parsing
  logic; preserving that is a useful existing architecture constraint.

**Live-code evidence:**

- `src/ViewModels/MainWindowViewModel.cs` currently owns the direct-send
  composition seam and Townhall mirroring.
- `src/ViewModels/AgentExecutionCoordinator.cs` owns only panel execution
  state/output mutation and contains no Townhall or parsing logic.

### 6. M0 Deliverable Rule

**Decision:** `M0` is planning lock-in only. It may update plan/docs and record
explicit live-code evidence, but it must not implement routing behavior, parser
types, host cleanup, or identity creation changes yet.

**Allowed M0 work:**

- update `docs/phases/phase-6/IMPLEMENTATION_PLAN.md`
- update closely related docs if they need truth-sync to reflect the locked
  decisions
- add exact implementation constraints for `M1`

**Not allowed in M0:**

- code changes in `src/` or `tests/`
- introducing parser models or router classes
- changing panel creation behavior
- changing Townhall behavior

## Milestones

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Lock routing decisions against the live codebase: identity policy, mention syntax, unknown-target behavior, routed-content stripping, router ownership seam, and strict `M0-only` boundary | Plan truth-sync + focused seam audit |
| M1 | Normalize agent identity and panel creation so new panels are distinguishable and routing-safe | Model/host/view tests for stable identity creation |
| M2 | Add the tiny route request/result model and mention parsing seam | Focused parser/model tests |
| M3 | Introduce the dedicated routing orchestration seam outside `MainWindowViewModel` and keep direct-send behavior working through delegation | Router/orchestration tests | ✅ Complete (M3) |
| M4 | Add the first routed agent-to-agent flow and Townhall-visible routing/debate summary behavior | ViewModel/router/Townhall tests | ✅ Complete (M4) |
| M5 | Add explicit cleanup for `AgentPanelHostView` subscriptions and verify routing-related view-host lifetime safety | View tests + focused lifetime assertions | ✅ Complete (M5) |
| M6 | Docs sync, regression sweep, and honest status closeout for direct send vs routed send behavior | `dotnet build`, `dotnet test`, doc truth-sync | ✅ Complete (M6) |

## Test Budget

At minimum, budget tests for:

- stable non-generic agent identity creation
- mention parsing success/failure cases
- unknown mention handling
- direct-send fallback when no mention exists
- routed-send behavior from source panel to target panel
- Townhall visibility for routed request/response/failure events
- proof that `MainWindowViewModel` remains a delegating seam rather than the
  router owner
- proof that `AgentExecutionCoordinator` still has no Townhall or parsing logic
- `AgentPanelHostView` cleanup/lifetime behavior

Likely files to extend or add:

- `tests/Zaide.Tests/Models/` new route request/result tests
- `tests/Zaide.Tests/ViewModels/` new router/orchestration tests
- `tests/Zaide.Tests/ViewModels/AgentPanelHostTests.cs`
- `tests/Zaide.Tests/ViewModels/MainWindowViewModelTests.cs`
- `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs`
- `tests/Zaide.Tests/Views/` new `AgentPanelHostView` lifetime/cleanup tests

## Risks To Watch

- `MainWindowViewModel` quietly absorbing routing policy because it already owns
  the current direct-send seam
- widening mention parsing into a speculative language/problem instead of a tiny
  deterministic parser
- introducing provider/platform abstractions while solving routing
- creating route visibility only in panel-local output and not in Townhall
- keeping generic panel identities long enough that routing behavior becomes
  ambiguous or flaky
- growing long-lived view subscriptions without an explicit cleanup path

## Limitations (by design)

- First-slice routing may support only one explicit mention target
- Routing may remain in-memory only
- Debate visibility may stay summary-level in Townhall rather than full thread
  rendering
- Agent identity may come from a small seeded set rather than dynamic
  registration
- Routed execution still uses the existing single OpenAI-compatible,
  non-streaming request path

## Known Gaps At M6 Closeout (honest caveats — not smoothed away)

These are real limitations in the shipped M0–M5 behavior. They are recorded
here rather than hidden; none of them were fixed in M6 because M6 is doc-sync
and regression verification only.

- **Unknown/ambiguous/multi-mention failures are detected but not surfaced as a
  visible error.** `MentionParser` returns an explicit `RouteResult` failure and
  `AgentRouter` does not fall back to direct send, so no wrong-target execution
  happens. However, `MainWindowViewModel.SendAgentMessageAsync` discards the
  returned `RouteResult` (`_ = ...` in `MainWindow.axaml.cs`), and the thin seam
  only mirrors the source panel's post-execution `OutputHistory`. A failed route
  therefore leaves the source panel untouched and emits **no** dedicated
  Townhall error/agent-failure entry. The user's raw text is still mirrored into
  Townhall as a normal user chat line, but there is no visible "routing failed"
  signal. This means exit condition "unknown mention targets produce explicit
  *visible* failure behavior" is **not** satisfied by the live code.
- **Routed responses are not surfaced in Townhall.** For a routed send,
  `AgentRouter` executes the (stripped) content on the *target* panel, but
  `MainWindowViewModel.SendAgentMessageAsync` mirrors only the *source* panel's
  `OutputHistory` afterward. The source panel is unchanged by a routed send, so
  no assistant/agent response is mirrored into Townhall for the routed flow.
  Routing is therefore visible to the target panel locally, but not as a distinct
  Townhall routing/review event. "Townhall remains the primary shared visibility
  surface for routing/debate activity" is **only** satisfied for direct send
  today.
- **No dedicated `AgentRouter` orchestration test file exists.** The plan's
  Test Budget listed `tests/Zaide.Tests/ViewModels/` router/orchestration tests
  and `tests/Zaide.Tests/Models/` route request/result tests. Neither file was
  added. The router's `RouteAndExecuteAsync` path is only covered indirectly via
  `MentionParserTests` (parse logic) and `MainWindowViewModelTests` (direct-send
  mirroring). Routed-send execution and its Townhall behavior are **not** covered
  by a focused unit test.
- **No specialized debate/disagreement surfacing in Townhall.** Phase 6 routes
  content to a target panel and mirrors generic chat/error entries, but does not
  emit a distinct "agent A requested review from agent B" or "disagreement"
  Townhall entry. The `docs/roadmap/PHASES.md` "Debate model: disagreements
  surfaced in Townhall" item is **not** implemented as a specialized feature.

## Exit Conditions

Met conditions (verified against live code at `42063fb`):

- [x] `M0` locked decisions are recorded with live-code evidence before `M1`
      implementation begins
- [x] New agent panels receive stable, distinguishable identities suitable for routing
- [x] A narrow route parsing/result model exists (covered indirectly: parsing is
      unit-tested in `MentionParserTests`; the records are exercised via
      `MainWindowViewModelTests` — see Known Gaps for test-coverage caveat)
- [x] A dedicated routing orchestration seam exists outside `MainWindowViewModel` (M3)
- [x] Direct-send behavior still works when no mention target is present (M3 verified)
- [x] A routed send can target another visible agent panel via the locked
      mention syntax (mechanically: `AgentRouter` resolves and executes on the target panel)
- [x] `AgentExecutionCoordinator` remains free of Townhall and parsing policy
- [x] No provider registry/platform abstraction was added
- [x] `AgentPanelHostView` has an explicit cleanup path for routing-related subscriptions
- [x] `docs/roadmap/PHASES.md`, `docs/architecture/OVERVIEW.md`, and `README.md`
      now match the implemented Phase 6 state (M6)
- [x] Build succeeds: `dotnet build Zaide.slnx --no-restore` (0 warnings, 0 errors)
- [x] Tests pass: `dotnet test Zaide.slnx --no-build` (721 passed, 0 failed)

Not met by live code at M6 (see Known Gaps):

- [ ] Unknown mention targets produce *explicit visible* failure behavior — the
      failure is detected (no silent fallback) but not surfaced as a visible
      Townhall/panel error in the current thin seam
- [ ] Townhall visibility of the *routed* flow — only direct send is mirrored
      today; routed responses land in the target panel but not in Townhall
- [x] Manual smoke (direct send, routed send, unknown mention, Townhall
      visibility, panel-switch) was **run and confirmed** during Phase 6 smoke
      test (2026-07-08); all TOFIX items verified and closed

## Exact Next Step

Phase 6 is closed. All TOFIX items from smoke testing have been fixed, verified
by automated tests, and confirmed by manual smoke. Two real routing visibility
gaps remain (unknown-mention visible failure, and routed-flow Townhall surfacing)
and are documented in Known Gaps above. The next phase is **Phase 7: Git
Integration** (`git` status in the left sidebar, basic diff view, commit from the
IDE, branch display). If the team wants the routing visibility gaps closed first,
that work belongs in a Phase 6 follow-up (a Phase 6.1 seam that consumes
`RouteResult` and mirrors routed/debate events into Townhall), not in Phase 7.

## Rollback Plan

- Commit hash to revert to: `42063fb` (last Phase 6 implementation commit:
  `feat: add DetachHost cleanup path and register MentionParser`)
