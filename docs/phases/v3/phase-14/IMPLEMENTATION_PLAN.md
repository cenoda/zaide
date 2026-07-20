# Phase 14: Unified Conversation Workspace — Implementation Plan

## Status and authorization

**Phase 14 status:** **M0 accepted (2026-07-20).** **M1 authorized only
(2026-07-20)** — store navigation seams. Human acceptance of M0 and
authorization of M1 are recorded in this plan.

**M2 and later milestones remain unauthorized** until a human explicitly
authorizes the next named milestone only. M1 does **not** authorize DM
navigation UI, privacy changes, persistence implementation, Agent Panel
retirement, or any later milestone scope.

**Dependency status:**

| Predecessor | Status | Evidence |
|-------------|--------|----------|
| Roadmap V2 (Phases 8–13) | Complete | `docs/roadmap/V2.md`; Phase 13 M5 closeout |
| Refactor 6.1–6.3 | Accepted and closed | Feature-first tree, composition modules, lifetime map |
| Refactor 7 | Complete and closed at `a7d2887` | Typed Actor/Conversation domain, run correlation, projection cutover, attribution fix |
| Refactor 8 | Complete and closed (M1–M8) | Shell hosts, Townhall/Agent presentation maintainability |
| Native Harness / ACP production | Out of scope | Later V3 phases unless a later Phase 14 milestone proves a narrow backend seam is required |

**Roadmap source:** V3 §5 (Unified Conversation Model), §18 (Phase 14 —
Unified Conversation Workspace), DF-001.

**Audit baseline:** `1dc4cf8` (`master`, clean working tree; Refactor 8 M8
accepted and closed). Live paths re-verified for this M0 on 2026-07-20.

---

## Pre-Implementation Verification (M0)

- [x] Read `docs-rules.md`, `docs/CONVENTIONS.md`, `docs/DESIGN.md`,
      `docs/roadmap/V3.md` §5 / §16 / §18, Refactor 7 and Refactor 8 plans,
      and open DF-001.
- [x] Verify live ownership of Conversations, Townhall, Agents, shell layout,
      mirror orchestration, DI registration modules, and architecture baselines
      against `src/` (not stale design text).
- [x] Confirm no production change is required for M0.
- [x] Lock scope, boundaries, persistence/recovery policy, milestone order,
      verification commands, and rollback points in this plan.
- [x] Confirm concrete test/build commands and current architecture baselines.

### M0 library / stack notes

Phase 14 reuses the live Avalonia + ReactiveUI + Microsoft DI stack. No new
NuGet package is authorized at M0. A persistence engine choice is locked below
as **file-based versioned store** (no SQLite requirement for Phase 14 exit).
If a later milestone proves a query need only SQLite can satisfy, stop and open
a named plan amendment rather than silently adding a package.

---

## M0 live-code audit

### Current ownership map

| Concern | Live owner / path | M0 finding |
|---------|-------------------|------------|
| Actor identity | `IActorCatalog` / `ActorCatalog` + `CanonicalActorSeeds`; DI via `AddZaideConversations()` | Canonical Human, Townhall Agent, panel seeds alpha–delta, fallback/custom panel actors. Display names are not keys. |
| Conversation aggregate | `Conversation` + `ConversationId` + `ConversationKind` {`Channel`, `Direct`} + `ConversationParticipants` + ordered `ConversationEntry` | Authoritative in-memory model from Refactor 7. Channel ids are stable (`channel:{channelId}`). Direct ids are random (`direct:{guid}`) at create time. |
| Conversation store | `IConversationStore` / `ConversationStore` (singleton) | Create channel/direct, `TryGet`, `TryGetChannelConversation`, `AppendEntry`, `EntryAppended`. **No enumerate, no find-direct-by-participants, no remove/archive, no persistence.** |
| Entry kinds | `ConversationEntryKind` | `UserChat`, `AssistantResponse`, `RoutingFailure`, `ExecutionFailure`, `ChannelEvent`, `SystemNotification`. Tool/think/raw-trace kinds intentionally absent. |
| Townhall presentation state | `TownhallState` + `TownhallViewModel` | Channels list, `ActiveChannelId`, per-channel `ChannelMessages` compatibility collections, single global `DraftText`, people roster (`WorkspaceAgent`). Seeds three channels on first run. |
| Townhall dual-write | Public: `TownhallViewModel.AddMirroredActivityToConversation`; private helper `AppendMirroredActivity` | Writes typed entry to `IConversationStore`, then projects into `TownhallState.ChannelMessages` via `TownhallEntryProjection`. Selection still uses string channel id, not a general conversation selector. |
| Agent panel lifetime | `IAgentPanelHost` / `AgentPanelHost` | Creates panel → `CreateDirectConversation(Human, actor)` → output projection. Close disposes projection only; **does not cancel in-flight work**; conversation remains orphaned in store with no UI re-entry path. |
| Panel presentation | `AgentPanelState` | `PanelId` (presentation), `ConversationId` (domain), `ActorId` projections, `Status`/`IsBusy` strings, `DraftInput`, read-only `OutputHistory` lines. |
| Direct output projection | `AgentPanelOutputHistoryProjection` + `AgentPanelEntryProjection` | Subscribes to `EntryAppended`; maps typed entries to legacy `"User: "` / `"Assistant: "` / `"Error: "` strings. |
| Execution | `AgentExecutionCoordinator` → `IAgentExecutionService` | **At most one in-flight send per panel** (`_inFlightPanels` is a `HashSet` of panel ids). Concurrent sends on different panels are not globally serialized. Appends user/assistant/failure to **panel** `ConversationId`; clears draft; non-streaming only. Cancel via `CancellationToken` yields cancelled outcome. **No first-class retry command** — product path is re-send. |
| Routing | `MentionParser` + `AgentRouter` | Resolves `@Name` against **visible open panel display names**, then routes by panel id / typed actor. Routing failures produce coordinator results without appending a direct-conversation entry in the router path; mirror may still publish to public Townhall. |
| Public mirror | `AgentTownhallMirrorCoordinator` (constructed by `MainWindowViewModel`, not DI) | At send admission, captures **active public channel** `ConversationId`, writes user activity there, awaits route/execute, mirrors terminal outcome to the **captured** channel. Privacy-violating product behavior relative to V3 locked direction; intentional until Phase 14 privacy milestone. |
| Shell layout | `MainLayoutBuilder` + `RightColumnHost` | Townhall is center star column; agent panel lives under editor in right column (`AgentPanelHostView` row 2). Dedicated panel is still first-class chrome. |
| DI modules | `AddZaideConversations`, `AddZaideTownhall`, `AddZaideAgents` | Conversations: catalog + store singletons. Townhall: `TownhallState` + `TownhallViewModel`. Agents: panel host, execution, router, `HttpClient`. No conversation navigation or persistence services. |
| Architecture baselines | `PublicProductionTypeBaseline` / inventory reader | **339** public / **111** internal / **450** total top-level production types. Prefer `internal` for new types. |
| Persistence | Settings feature only for app settings/secrets | Conversation history, drafts, unread, participant pairs on directs, active selection, and agent panel tabs are **in-memory only**. App restart loses them. **No durable V2 conversation document exists** on disk. |
| Channel “membership” | `ConversationParticipants.ForChannel()` is empty; people roster is `WorkspaceAgent` on `TownhallState` | Channel multi-user membership/roles are **not** a live domain model. Directs carry exactly two `ActorId` participants. |

### Live structural measurements (audit-time)

**Counting rule:** `find <path> -name '*.cs' | xargs wc -l` total lines (all
`.cs` under the feature root, including blank lines and comments; no test
sources). Shell rows are single-file `wc -l`. Figures re-measured at M0
amendment on baseline `1dc4cf8`.

| Surface | Path | LOC |
|---------|------|-----|
| Conversations feature | `src/Features/Conversations/**/*.cs` | **810** |
| Townhall feature | `src/Features/Townhall/**/*.cs` | **2,117** |
| Agents feature | `src/Features/Agents/**/*.cs` | **2,191** |
| Shell mirror coordinator | `src/App/Shell/AgentTownhallMirrorCoordinator.cs` | 123 |
| Right column (editor + agent) | `src/App/Shell/RightColumnHost.cs` | 110 |
| Main window | `src/App/Shell/MainWindow.axaml.cs` | 486 |
| Full suite at Refactor 8 closeout | docs record only — **not re-run for this M0** | **2523** passed (historical) |

### Current behavior that must remain until a named milestone changes it

These are **not** permanent product law. They are the live surface M0 freezes
as the regression baseline. Each intentional product change must land in the
milestone that owns it, with tests and (when UI-visible) manual evidence.

1. **Dedicated Agent Panel remains visible** until M8 retirement is authorized
   and parity evidence is accepted.
2. **Public mirror of agent-panel sends into the active/captured Townhall
   channel remains** until the privacy milestone (M4) is authorized.
3. **Direct send, `@mention` routing, busy/idle/error presentation, draft
   clear-on-send, and panel close-does-not-cancel** remain until their owning
   milestones redefine them with parity tests.
4. **Townhall channel list, filter All/Chat/Activity, seeded channels, and
   channel send** remain usable across the phase.
5. **Typed entries remain authoritative**; do not reintroduce string-protocol
   ownership for orchestration results.
6. **Non-streaming single-message execution** remains the only backend
   capability represented unless a later milestone proves a narrow seam is
   required for truthful DM UX (not to build the Native Harness).

### Gaps Phase 14 must close (verified missing)

| Gap | Evidence | Owning milestone |
|-----|----------|------------------|
| No Townhall navigation for direct conversations | `TownhallChannelPanel` / `TownhallViewModel` expose channels only | M2 |
| No stable find-or-create direct by participant pair | `ConversationStore.CreateDirectConversation` always `NewDirect()` | M1 |
| No conversation enumeration API | `IConversationStore` lacks list/query | M1 |
| Single global Townhall draft | `TownhallState.DraftText` not keyed by conversation | M5 |
| Panel drafts not conversation-owned | `AgentPanelState.DraftInput` is panel-local | M5 |
| No unread/read cursor | Absent in domain and UI | M5 |
| Implicit public mirror of private agent activity | `AgentTownhallMirrorCoordinator` | M4 |
| Panel is only re-entry path to direct history | Close panel → orphan conversation in store | M2–M3, M7 |
| `@mention` depends on open panel names | `AgentRouter` uses `_panelHost.Panels` display names | M7 (identity-based roster) |
| No persistence/recovery | In-memory store + panel host | M6 |
| Dual presentation truth for Townhall | `ChannelMessages` compatibility collections still required by UI | M3 gradual; full retirement optional at closeout |
| Agent Panel retirement not proven | DF-001 open; shell still hosts `AgentPanelHostView` | M7 parity → M8 retire |

### M0 product and architecture decisions

| ID | Decision | Locked Phase 14 rule |
|----|----------|----------------------|
| D01 | Phase goal | Public channels and Agent direct conversations share one conversation system under Townhall. The dedicated Agent Panel may be retired only after documented parity. |
| D02 | Visibility | Direct conversations are **private by default**. No implicit copy of DM content into a public channel. Explicit share/cross-post is out of Phase 14 unless a later milestone amendment adds it. |
| D03 | Selection vs ownership | UI selection is presentation state. Every admitted entry has one owning `ConversationId` captured at admission (R7 rule retained). |
| D04 | Conversation kinds | Phase 14 ships `Channel` and `Direct` only. Agent↔Agent and Human↔Human direct kinds may exist as participant combinations on `Direct` without new kind enums. No group-DM product surface required. |
| D05 | Stable direct identity | Direct conversations are find-or-create by unordered participant pair `{ActorId, ActorId}` (exactly two distinct actors). Random `direct:{guid}` values remain valid ids but **must not** be the only lookup key for “open DM with agent X.” **M1 must define pair keying** (e.g. sorted ordinal `ActorId` pair index): live `ConversationParticipants` stores constructor order and has no equality/normalization helper today. |
| D06 | Panel vs conversation | `PanelId` is temporary presentation chrome. Domain recovery keys are `ConversationId` and `ActorId`. Closing a panel must not destroy conversation history once DMs are navigable. |
| D07 | Agent Session (`R61-LT02`) | Still **deferred**. Do not invent `AgentSession` / resume tokens / session scopes in Phase 14. Interrupted runs are terminal after process death (see D12). |
| D08 | Execution capability | Keep existing non-streaming `IAgentExecutionService` path. Do not implement Native Harness, ACP, tools, streaming, or multi-provider registries. UI may reserve honest “unsupported” presentation for later capabilities but must not fake them. |
| D09 | Routing evolution | Until M7, preserve visible-name `@mention` against open panels. M7 must move resolution to typed `ActorId` / catalog roster so routing does not require a dedicated panel tab strip. |
| D10 | Compatibility projections | `TownhallMessage` / `ChannelMessages` and panel `OutputHistory` strings may remain as projections during migration. Orchestration must not re-parse them for truth. Prefer deleting dual-write only when the unified surface no longer needs it. |
| D11 | Persistence engine | **Versioned file store** under application data (not `settings.json`, not secrets file). Atomic write + last-known-good pattern modeled on Settings. **SQLite is not required for Phase 14 exit.** |
| D12 | Recovery / side effects | After restart or crash: restore durable conversation records, drafts, unread, direct participant pairs, and last active conversation selection when present. **Never auto-resume side-effecting execution.** In-flight runs become terminal interrupted/cancelled/failed records (or are absent if never durable). User must explicitly **re-send** (no first-class retry command in Phase 14). |
| D13 | What is durable (minimum) | Conversation metadata (id, kind, participants), ordered entries for channel + direct, per-conversation draft text, per-conversation unread/read cursor (or last-read entry id), channel list seed/custom list as applicable, last active `ConversationId`. **“Membership” for Phase 14 means:** directs = durable two-actor participant pair; channels = no multi-user membership product (live channel participants are empty; people roster stays presentation). Do not invent channel roles/ACL. Panel tab layout is **not** required durable once panel is retired. |
| D14 | What stays ephemeral | Live `IsBusy` / “Thinking”, HTTP in-flight, UI scroll position (may restore best-effort later; not exit-critical), filter mode may be session-local unless cheap to persist. |
| D15 | Workspace isolation | Conversation store is application-lifetime for Phase 14 unless M6 proves workspace-root scoping is required for multi-folder safety. M6 must record the chosen isolation rule and tests. Do not invent multi-window sync. |
| D16 | Design | Discord-like composition is reference scaffolding, not the visual target. Phase 14 requires a short design brief + comparison screenshots against `docs/DESIGN.md` before retirement chrome lands (M2/M3 UI milestones may ship incremental chrome; M9 records design evidence). Prefer DesignSystem tokens; C# view construction default. |
| D17 | DF-001 | Resolved only when M8 retirement (or an accepted documented deferral of retirement with DM parity complete) closes with evidence. |
| D18 | Authorization | One named milestone at a time. No “while we are here” harness, settings redesign, or DF-002/003/004 scope. |
| D19 | Public API | New production types default to `internal`. Public baseline updates only with same-milestone rationale. |
| D20 | Tests-first gates | Domain/application changes require automated tests in the same milestone. UI milestones require focused tests plus manual smoke evidence recorded in the plan or a sibling evidence file. |
| D21 | Retry product shape | Cancel exists via `CancellationToken` / cancelled outcome. Phase 14 does **not** add dedicated retry chrome or automatic retry. Parity = user can re-send a message. |

### Persistence and recovery contract (M0 lock; implement in M6)

This section satisfies V3 §18’s requirement that Phase 14 M0 lock the
persistence/recovery contract needed to retire the dedicated Agent Panel.

| Topic | Locked contract |
|-------|-----------------|
| Store location | Application data directory owned by a Conversations (or App) infrastructure type; path resolution tested; not mixed into Settings schema v3. |
| Format | Versioned JSON document or JSON snapshot + append journal — exact shape is an M6 implementation choice, but version field and migration function are mandatory. |
| Atomicity | Write temp → flush → replace; keep last-known-good on failure (Settings pattern). Corrupt / unsupported-future / interrupted writes must not wipe known-good data. |
| Load failure | Start with empty in-memory state + user-visible status when recovery fails; never crash the shell. |
| History | Persist admitted `ConversationEntry` records (ids, kind, author, timestamp, content, correlation id). |
| Drafts | Persist per-`ConversationId` draft strings (including empty omission). |
| Unread | Persist last-read entry id or equivalent cursor per conversation. |
| Active selection | Persist last active `ConversationId` when valid. |
| Runs after restart | Do not re-invoke LLM or tools. Do not mark busy. Historical entries remain; any incomplete attempt is terminal. |
| Ordering / idempotency | Entry ids are durable primary keys; reload must not duplicate entries with the same id. Append is ordered by admission sequence. |
| Rollback | Disabling persistence or deleting the store file returns the product to empty seeded session behavior without breaking Settings. |
| Secrets | Conversation store must not write API keys. Execution still uses existing Settings/secret/env configuration. |
| V2 conversation migration | **N/A — empty migration surface.** Live conversation state is in-memory only; no durable V2 conversation document, schema, or export exists. M6 introduces schema version **1** (or equivalent first version) from empty/seed state. “Migration” means forward version bumps of the new store only; rollback = delete/ignore store file → seed session. |

### UI acceptance lock (V3 §18)

V3 §18 lists a UI acceptance boundary for growing conversations and a11y.
M0 adopts or defers each item so M3/M8/M9 scope is not ambiguous:

| UI acceptance item | Phase 14 disposition | Owning milestone / note |
|--------------------|----------------------|-------------------------|
| Incremental or virtualized rendering for growing history | **In Phase 14 if needed for correctness/perf on realistic history**; otherwise document measured limit | M3 baseline list rendering; virtualization only if M3/M9 evidence shows need — not a blank-check redesign |
| Stable scroll anchoring | **In Phase 14** (do not jump scroll position on append when user is not following bottom) | M3 |
| Near-bottom auto-follow | **In Phase 14** (follow new messages when already near bottom) | M3 |
| New-message affordance when scrolled away | **In Phase 14** (minimal control/badge to jump to latest) | M3 (may be simple; polish in M9) |
| Semantic controls | **In Phase 14** for new nav/list/input chrome (accessible names on primary controls) | M2–M3; evidence in M9 |
| Keyboard-only navigation | **In Phase 14** for conversation list → select → input → send / newline | M2–M3 parity; M8 retirement must not regress; M9 evidence |
| Visible focus | **In Phase 14** for new focusable chrome (match existing shell patterns) | M2–M3; M9 evidence |
| Screen-reader naming | **Best-effort in Phase 14** on new controls; not WCAG certification | M9 records what was named; gaps → limitations |
| Narrow / wide / high-DPI evidence | **In Phase 14 closeout** (Linux desktop screenshots: default + narrow; high-DPI if available) | M9; missing environment rows → explicit not-validated |
| Design brief + comparison vs `docs/DESIGN.md` | **In Phase 14** | M9 (incremental chrome allowed earlier) |

Items marked **In Phase 14** are phase exit criteria unless M9 records an
accepted limitation with evidence of residual risk.

### Parity checklist for Agent Panel retirement (M8 gate)

All items must be proven before removing `AgentPanelHostView` from the shell:

- [ ] Open/select a Human↔Agent direct conversation from Townhall navigation
- [ ] Send message; see user + assistant/error entries in that conversation only
- [ ] Busy/disable input while in-flight **for that conversation/panel**; restore on completion (per-panel concurrency — not global single-flight)
- [ ] Cancel/exception paths remain truthful (existing coordinator semantics)
- [ ] Re-send works after failure/cancel (**no** dedicated retry command required)
- [ ] Draft retained per conversation across selection changes
- [ ] Routing failure visible in the owning conversation (and not lost)
- [ ] `@mention` / multi-agent routing works without requiring the retired tab strip **or** an accepted interim limitation is documented and does not block DM primary path
- [ ] Closing/reopening the app restores history + draft + unread per D11–D13
- [ ] No implicit public-channel copy of DM content (M4)
- [ ] Keyboard: focus conversation list, select, focus input, send (Enter), newline (Shift+Enter) parity with current Townhall/panel paths where applicable
- [ ] Scroll: stable anchoring + near-bottom auto-follow + new-message affordance when scrolled away (UI acceptance lock)
- [ ] In-flight work is not silently dropped without status when navigating away (define: stay attached to conversation, not to panel chrome)
- [ ] Automated regression suite green; manual smoke evidence recorded

---

## Scope

**Goal:** Make public Townhall channels and Agent direct conversations one
conversation workspace: navigable, private-by-default for DMs, durable enough
to retire the dedicated Agent Panel after parity, without building the Native
Harness or ACP platform.

**In scope:**

- conversation store query/find-or-create seams for unified navigation;
- Townhall navigation for channels **and** direct conversations;
- unified conversation reading/sending surface keyed by `ConversationId`;
- removal of implicit public mirroring of agent DM activity;
- per-conversation draft and unread/read cursor;
- versioned file persistence and recovery for the durable set in D13;
- migration of agent execution UX from panel chrome to conversation workspace;
- retirement of dedicated Agent Panel shell chrome after parity;
- design brief + keyboard/focus evidence for new conversation chrome;
- UI acceptance items locked **In Phase 14** in the UI acceptance table above;
- DF-001 resolution on successful retirement (or explicit documented residual);
- docs/architecture/roadmap truth-sync at closeout.

**Out of scope (YAGNI):**

- Native Harness implementation, tool calling, permissions engine, memory system;
- ACP integration / multi-backend product surface;
- streaming, raw model I/O traces, backend session resume;
- first-class retry command / automatic retry loops (re-send only);
- channel multi-user membership roles, ACL, or people-as-conversation-members model;
- Human↔Human product flows beyond not hard-coding impossibility;
- SQLite or general plugin persistence framework;
- migration of a non-existent durable V2 conversation document;
- multi-window / multi-workspace realtime sync;
- redesign of Settings, Editor, Terminal, Git, LSP, DAP, Build/Run/Test;
- DF-002 Korean IBUS, DF-003/DF-004 settings polish unless a pure touch forces
  behavior preservation in the same file;
- visual “final Zaide brand” overhaul beyond the Phase 14 design brief needs;
- removing typed domain owners introduced by Refactor 7;
- WCAG certification or full screen-reader parity (best-effort naming only).

---

## Milestones (Incremental)

| Milestone | Description | Test / gate | Status |
|-----------|-------------|-------------|--------|
| **M0** | Planning gate: live audit, decisions D01–D21, persistence contract, UI acceptance lock, milestones, commands, rollback. **Docs only.** | Plan review; no production diff required | **Accepted (2026-07-20)** |
| **M1** | **Store navigation seams:** enumerate conversations; find-or-create direct by participant pair with **explicit pair key** (sorted `ActorId` ordinal key or equivalent; document rule in tests); optional title/metadata needed by navigation; keep existing panel create path working via find-or-create; tests for stability and **per-panel** concurrent sends (not global single-flight). No DM UI yet. | `dotnet build`; Conversations tests; Architecture; full suite | **Authorized (2026-07-20)** — not complete |
| **M2** | **Townhall navigation UI** for channels + directs (list, select, create/open DM with known agents). Dedicated Agent Panel still present. No privacy change yet. Semantic list controls + keyboard select path. | Build; Townhall + Conversations tests; Architecture; full suite; manual nav smoke | Unauthorized |
| **M3** | **Unified conversation surface** for selected `ConversationId` (history + input + busy/error for directs using existing coordinator path). Channel send remains. Prefer projecting store entries over dual ownership growth. Deliver UI acceptance: scroll anchoring, near-bottom auto-follow, new-message affordance; virtualize only if proven necessary. | Build; Townhall + Agents tests; Architecture; full suite; manual channel+DM send + scroll smoke | Unauthorized |
| **M4** | **Privacy:** remove implicit public Townhall mirror of agent sends; ensure DM entries stay on owning direct conversation; update/remove `AgentTownhallMirrorCoordinator` behavior; keep R7 attribution lessons for any remaining explicit cross-post (none required). | Build; Shell mirror tests; Agents + Townhall tests; Architecture; full suite; manual privacy smoke | Unauthorized |
| **M5** | **Per-conversation draft + unread/read cursor** with selection switches preserving drafts; basic unread affordance in navigation. | Build; focused domain/UI tests; Architecture; full suite; manual draft/unread smoke | Unauthorized |
| **M6** | **Persistence + recovery** implementing the M0 contract (load/save, corrupt/LKG, no auto-resume). First schema version only (no V2 conversation document to migrate). | Build; persistence tests + recovery matrix; Architecture; full suite; manual restart smoke | Unauthorized |
| **M7** | **Parity bridge:** agent execution + routing work from conversation workspace; panel becomes redundant projection or thin host; complete automated parity tests against retirement checklist (re-send, not retry chrome). | Build; Agents + Townhall + Shell tests; Architecture; full suite; manual parity checklist | Unauthorized |
| **M8** | **Retire dedicated Agent Panel** from shell layout (`RightColumnHost` / wiring); remove or internalize dead panel chrome; DF-001 close or residual note; layout smoke. | Build; Shell + Architecture; full suite; manual layout + DM-only workflow smoke | Unauthorized |
| **M9** | **Closeout:** design brief evidence; keyboard/focus/visible-focus/screen-reader naming notes; narrow/wide/(high-DPI if available) screenshots; UI acceptance table rows closed or limited; docs truth-sync; baselines; `git diff --check`. | Build; Architecture; full suite; evidence review | Unauthorized |

If a milestone exceeds one agent session, split into `MNa` / `MNb` slices with
separate verification — do **not** invent sub-phases for session sizing.

### Suggested implementation dependency graph

```text
M0 (plan)
 └─ M1 store seams
     └─ M2 DM navigation UI
         └─ M3 unified surface
             ├─ M4 privacy (after DM surface can show private history)
             ├─ M5 draft/unread (may start after M2; must complete before M8)
             └─ M6 persistence (after durable field set is known; may parallel M5)
                 └─ M7 parity bridge
                     └─ M8 retire panel
                         └─ M9 closeout
```

M4 must not land before users can observe private DM history somewhere (M2/M3).
M8 must not land before M4, M5, M6, and M7 parity evidence.

---

## Verification commands

Record exact pass/fail/skip counts and warning counts at each milestone.

```bash
# Build (zero errors; record warning count)
dotnet build Zaide.slnx

# Focused filters (use as applicable per milestone)
dotnet test Zaide.slnx --filter 'FullyQualifiedName~Zaide.Tests.Features.Conversations'
dotnet test Zaide.slnx --filter 'FullyQualifiedName~Zaide.Tests.Features.Townhall'
dotnet test Zaide.slnx --filter 'FullyQualifiedName~Zaide.Tests.Features.Agents'
dotnet test Zaide.slnx --filter 'FullyQualifiedName~Zaide.Tests.App.Shell'
dotnet test Zaide.slnx --filter 'FullyQualifiedName~Zaide.Tests.Architecture'

# Full regression gate
dotnet test Zaide.slnx

# Whitespace / conflict markers
git diff --check
```

Manual smoke (minimum, expand per milestone evidence):

1. Launch app; Townhall channels still seed and send.
2. Open/select Agent DM; send; observe private history only (post-M4).
3. Switch conversations; drafts preserved (post-M5).
4. Restart app; history/draft/unread restore; no auto LLM call (post-M6).
5. Post-M8: no Agent Panel chrome; DM workflow complete from Townhall.

---

## Entry conditions

### For accepting M0

- [x] Refactors 6.1–6.3, 7, and 8 are closed.
- [x] Live audit completed at `1dc4cf8` (amended: concurrency, LOC rule, membership).
- [x] Persistence/recovery contract written (including V2 migration N/A).
- [x] UI acceptance lock table written (V3 §18).
- [x] Milestones and non-goals explicit.
- [x] Human accepts this amended M0 plan (2026-07-20).

### For authorizing M1

- [x] M0 accepted by human.
- [x] Human explicitly authorizes **M1 only** (2026-07-20).
- [x] Working tree ready on `master` at M0 acceptance (`0d80e4d` + this auth).

### For authorizing M8 (retirement)

- [ ] M1–M7 accepted with evidence.
- [ ] Retirement parity checklist complete.
- [ ] Human explicitly authorizes **M8 only**.

---

## Exit conditions (phase)

- [ ] Channels and Agent DMs share one conversation system navigable from Townhall.
- [ ] Direct conversations are private by default (no implicit public mirror).
- [ ] Per-conversation draft and unread/read behavior work.
- [ ] Persistence/recovery contract implemented and tested; no blind run resume.
- [ ] Dedicated Agent Panel retired **or** an accepted closeout limitation explains
      residual chrome with user-visible DM parity already complete.
- [ ] DF-001 closed or explicitly re-scoped with residual rationale.
- [ ] Design brief + keyboard/focus evidence recorded.
- [ ] UI acceptance lock items marked **In Phase 14** closed or accepted as
      limitations with residual risk.
- [ ] Architecture baselines truthful; full suite green; docs truth-synced.
- [ ] Limitations section lists honest environment or deferred items.

---

## Limitations (by design)

- Native Harness, ACP, tools, streaming, and session resume are not Phase 14.
- SQLite and multi-window sync are not Phase 14 requirements.
- Human↔Human messaging UX is not delivered; participant model must not forbid it.
- Existing non-streaming backend remains the only live execution capability.
- **At most one in-flight send per panel/conversation target** — not a global
  single-flight gate across all agents.
- **No first-class retry** — cancel exists; recovery is user re-send (D21).
- Channel multi-user membership/roles are not a Phase 14 product; durable
  “membership” is the direct participant pair only (D13).
- No durable V2 conversation store to migrate; M6 starts at schema v1 / seed.
- Virtualization is not mandatory if non-virtualized history stays correct and
  usable under recorded sizes; must not silently OOM without a limitation note.
- Screen-reader work is best-effort naming, not certification.
- Linux remains the primary validation platform; other OS rows default to not
  validated unless evidence is added.
- Historical public mirror entries already in memory from pre-M4 sessions are
  not rewritten by privacy work.
- Profile versioning / historical author snapshots beyond current Actor rows
  remain later concerns unless a milestone proves display breakage.
- Full suite total **2523** is the Refactor 8 closeout record and was not
  re-run as part of this M0 docs gate.

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Retiring panel before DM parity | Hard gate: M8 after checklist; dual-run panel+DM during M7 |
| Privacy change surprises users mid-migration | M4 only after M2/M3; document behavior change in milestone notes |
| Persistence corrupts session | Atomic write + LKG; load-failure empty seed; tests for corrupt/future version |
| `@mention` breaks without panels | M7 identity roster before M8; keep interim limitation explicit if needed |
| Scope creeps into harness | D08 / out-of-scope list; stop and amend plan rather than expand silently |
| Dual-write drift Townhall vs store | Prefer store-authoritative projections; tests for append/order/id stability |
| Layout regression after panel removal | Shell construction tests + manual default/min window smoke in M8 |

---

## Rollback plan

- **M0 baseline:** `1dc4cf8` (`master`, synchronized with origin at audit time).
- Prefer one commit per accepted milestone or milestone slice.
- Revert the current milestone commit(s) to restore the last accepted boundary;
  do not roll back unrelated user work.
- M1–M3 must keep Agent Panel functional until M8.
- If privacy (M4) is reverted, restore mirror coordinator behavior and tests.
- If persistence (M6) is reverted, delete or ignore the new store file; Settings
  must remain intact.
- Structural feature rollback requires
  `docs/phases/v3/phase-14/REVERT_LOG.md` per `docs-rules.md`.

---

## Exact next step

1. ~~Human accepts this **amended** M0 plan.~~ **Done (2026-07-20).**
2. ~~Human authorizes **M1 only**.~~ **Done (2026-07-20).**
3. **Implement M1** store navigation seams with tests (including sorted pair
   key); no DM UI, no panel retirement, no M2+.

M2+ remains unauthorized until explicitly approved.

---

## M0 amendment log

| Date | Change |
|------|--------|
| 2026-07-20 | Initial M0 plan written (docs only). |
| 2026-07-20 | Audit amendments: per-panel concurrency; UI acceptance lock (V3 §18); LOC recount + counting rule; membership vs channel participants; V2 migration N/A; M1 pair-key handoff; re-send vs retry (D21); dual-write public API naming; SQLite prose; suite not re-run note. |
| 2026-07-20 | Human accepted amended M0. Production milestones still unauthorized. |
| 2026-07-20 | Human authorized **M1 only**. M2+ remains unauthorized. |

---

*Last updated: 2026-07-20 (Phase 14 M0 accepted; M1 authorized only; M2+ unauthorized)*
