# Phase 4.1: Townhall Activity Model — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm `docs/phases/phase-4/IMPLEMENTATION_PLAN.md` has been converted to an umbrella pointing at this sub-phase
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [ ] Re-confirm live seams below still match `src/Models/TownhallMessage.cs`, `src/Models/TownhallState.cs`, `src/ViewModels/TownhallViewModel.cs`, `src/Views/TownhallView.cs`

## Planning Status

**Complete (2026-07-08).**

Phase 4 was originally one wide plan covering data model, auto-logging, UI, and
docs sync. It is now split; this sub-phase covers only the data model and the
agent wire-format decision that the model depends on. See
`docs/phases/phase-4/IMPLEMENTATION_PLAN.md` for the umbrella and the other
sub-phases (4.2 auto-logging, 4.3 UI, 4.4 docs sync).

Verified live seams (2026-07-08):

- `src/Models/TownhallMessage.cs`
  - flat shape: `Id`, `SenderId`, `SenderName`, `SenderAvatar`, `Content`,
    `Timestamp`, `Type` (`Normal` / `Warning` / `System`). No concept of a
    non-chat event (channel switch, tool call, agent action).
- `src/Models/TownhallState.cs`
  - `ChannelMessages: Dictionary<string, ObservableCollection<TownhallMessage>>`
    — a single homogeneous collection type per channel. Any new entry kind
    must fit into this collection or a parallel one.
- `src/ViewModels/TownhallViewModel.cs`
  - `SendMessageCommand` constructs `TownhallMessage` directly and appends
    to `_state.ChannelMessages[activeChannelId]`. `ActiveChannelId` setter
    swaps which collection `Messages` points to.
  - `InitializeSampleData()` hardcodes 3 channels, 2 agents, and a handful of
    sample messages. This is what "sample-only" refers to; not touched in
    this sub-phase beyond what's needed to keep the model change compiling.
- `src/Views/TownhallView.cs`
  - `SetChatMessages` subscribes to `CollectionChanged` on whatever
    `ObservableCollection<TownhallMessage>` is active and forwards it, as a
    single homogeneous list, to `TownhallChatPanel.SetMessages`. Any new
    entry type must still be renderable through this same list unless the
    view is also changed (out of scope here — see 4.3).
- No `IAgent` interface or agent wire-format exists anywhere in `src/`.
  `IAgent` appears only as a naming-convention example in
  `docs/CONVENTIONS.md`. `docs/architecture/OVERVIEW.md` lists `IAgent`
  under "Future Technical Considerations — not yet implemented."
  `WorkspaceAgent` is a UI presence model only (`Id`, `Name`, `Avatar`,
  `Role`, `Status`, `HasWarning`) — no execution, no API client, no
  model/provider field.

## Scope

**Goal:** Decide and implement the Townhall activity data model — the shape
that will carry both chat messages and non-chat activity entries — and make
just enough of an agent-format decision that the model isn't guessing at
agent-event shape. No auto-logging, no new UI, no filtering in this sub-phase.

**In scope:**

- Decide: single mixed-type entry model vs. `TownhallMessage` +
  parallel `TownhallActivityEntry`. **Decision: single mixed-type entry
  model.** Keeps the existing `ChannelMessages` per-channel collection
  pattern; the view (4.3) branches on kind when rendering rather than
  merging two collections. Implement this shape.
- Define entry "kind" taxonomy as a stricter replacement for the current
  `Normal`/`Warning`/`System` triad. Include kinds that don't have a
  producer yet in Phase 4, as schema insurance against a later breaking
  change once agent execution lands (Phase 5/6):
  `Chat`, `ChannelEvent`, `AgentAction`, `AgentThink`, `ToolCall`,
  `ToolResult`, `AgentError`, `System`. Nothing in Phase 4 needs to produce
  `AgentThink`/`ToolCall`/`ToolResult`/`AgentError` — they exist only so the
  enum doesn't need to change shape later.
- Minimal agent-format decision: what fields an agent-originated entry needs
  — schema only, no live API client, no HTTP calls, no provider abstraction
  service (see Agent Format Decision below)
- Update `TownhallState` to hold the new entry shape per channel
- Update `TownhallViewModel` construction/send-path to compile against the
  new shape without changing its observable behavior
- Rewrite `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs` assertions
  that depend on the old `TownhallMessage`-only shape

**Out of scope:**

- Automatic activity logging for user/agent actions (Phase 4.2)
- Any new or changed UI, filtering, or rendering (Phase 4.3)
- Replacing sample data with real session-state initialization beyond what's
  needed to keep this sub-phase's code compiling (Phase 4.2/4.3)
- Real agent execution, live API calls, streaming, or provider SDKs
- Persistence of activity history to disk/DB
- Multi-provider agent architecture (`IAgentProvider`, `AgentRegistry`,
  per-agent provider/model configuration). This is real Phase 5/6 work per
  `docs/architecture/OVERVIEW.md`'s Future Technical Considerations table
  (`IAgent`/`IPlugin` explicitly deferred), not a Phase 4 gap. Phase 4.1
  only adds the two nullable fields below so that work doesn't force a
  breaking schema change later — it does not build the abstraction itself.
- Reply chains / conversation branching (`ReplyToId`). Only a flat
  `ThreadId` is added now; full threading is Phase 6 debate-model work.

## Agent Format Decision

This is the specific gap the wider audit flagged: no agent wire-format
(OpenAI-compatible or otherwise) is decided anywhere in the codebase or docs.
Phase 4.1 does not need to pick a provider or build a client — but the
activity-entry schema needs to not paint itself into a corner.

Decision to make and record here before M1 starts (revised after audit,
2026-07-08):

- Model an agent-originated entry with provider-agnostic fields:
  `Role` (`user` / `agent` / `system` — mirrors common chat-completion
  shapes without committing to one), free-text `Content`, and:
  - `SourceProvider` (`string?`) — free-text provider identifier
    (e.g. `"openai"`, `"anthropic"`, `"local"`). First-class field, not a
    metadata bag entry, so consumers don't need to know a bag schema to
    read it. Left as a free-text string rather than an enum so adding a
    provider later doesn't require a code change to this model.
  - `SourceModel` (`string?`) — free-text model identifier (e.g.
    `"gpt-4"`, `"claude-3.5-sonnet"`). Same rationale as `SourceProvider`.
  - `ThreadId` (`string?`) — flat grouping id for a conversation turn.
    No `ReplyToId` / branching in this sub-phase (see Out of Scope).
  - `Metadata` (`Dictionary<string, string>?`) — still included, but now
    only for genuinely provider-specific extras (token counts, raw
    tool-call ids) that don't warrant a first-class field. `SourceProvider`
    and `SourceModel` are pulled out of the bag because every consumer
    needs them (rendering, filtering, transparency), whereas bag contents
    are opt-in detail.
- Still do not add a `Provider` or `Model` *enum*, and do not add any
  provider abstraction (`IAgentProvider`, `AgentRegistry`, per-agent
  configuration). Those are Phase 5/6 concerns. `SourceProvider`/
  `SourceModel` being plain nullable strings here is deliberately the
  entire scope of the "agent format decision" for Phase 4 — enough to
  avoid a breaking migration, not enough to constitute an architecture.
- Nothing in Phase 4 populates `SourceProvider`, `SourceModel`, or
  `ThreadId` with real values — no agent execution exists yet. They exist
  on the model so Phase 5/6 can populate them without changing the shape.

## Milestones

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate and baseline verification | `dotnet build`, `dotnet test` | ✅ Done (2026-07-08) |
| M1 | Agent-format decision recorded + entry-kind taxonomy defined (including forward-looking kinds) | Design doc section above reviewed; no code yet | ✅ Done (2026-07-08) |
| M2 | New Townhall activity entry model implemented | Model tests for entry kinds, timestamps, `SourceProvider`/`SourceModel`/`ThreadId`/`Metadata` | ✅ Done (2026-07-08) |
| M3 | `TownhallState`/`TownhallViewModel` updated to compile against new shape | Existing + rewritten ViewModel tests pass, no behavior change | ✅ Done (2026-07-08) — no code changes needed; M2 mechanical fixes already covered M3 |
| M4 | Docs sync for this sub-phase + exit audit | `dotnet build`, `dotnet test`, phase-4 umbrella status updated | ✅ Done (2026-07-08) |

## Planned Change Shape

1. Add the new entry-kind taxonomy and the `SourceProvider`/`SourceModel`/
   `ThreadId`/`Metadata` fields to (or alongside) `TownhallMessage`, per
   the Agent Format Decision above.
2. Update `TownhallState.ChannelMessages` to the new entry type.
3. Update `TownhallViewModel.SendMessageCommand` and `InitializeSampleData`
   to construct the new shape without changing observed behavior (same
   channels, same sample messages, same commands).
4. Update `TownhallView`/`TownhallChatPanel` call sites only as needed to
   keep the build green — no rendering changes, no new visual treatment
   (that's 4.3).
5. Rewrite affected assertions in `TownhallViewModelTests.cs`.

## Exit Conditions

- [x] Agent-format decision is recorded in this document and matches what's implemented
- [x] `TownhallMessage`/replacement entry type supports a real kind taxonomy beyond `Normal`/`Warning`/`System`, including the forward-looking kinds with no producer yet
- [x] `SourceProvider`, `SourceModel`, and `ThreadId` exist as nullable first-class fields (unpopulated in Phase 4 is fine)
- [x] No provider abstraction (`IAgentProvider`, `AgentRegistry`, enums) was added — confirmed still deferred to Phase 5/6
- [x] `TownhallState` and `TownhallViewModel` compile and behave the same as before from a user-visible standpoint (no UI change yet)
- [x] `TownhallViewModelTests.cs` updated and passing (no edits needed; never referenced the old `TownhallMessageType`/`Type` shape)
- [x] `dotnet build` and `dotnet test` pass (589 passed, 0 failed, 0 warnings as of 2026-07-08)
- [x] `docs/phases/phase-4/IMPLEMENTATION_PLAN.md` sub-phase table updated to mark 4.1 complete
