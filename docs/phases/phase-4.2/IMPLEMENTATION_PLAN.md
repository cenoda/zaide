# Phase 4.2: Auto-Logging and Session State — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 4.1 is complete (activity entry model + agent-format decision landed; see `docs/phases/phase-4.1/IMPLEMENTATION_PLAN.md` exit conditions)
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [ ] Re-confirm `TownhallViewModel`/`TownhallState` still match the shape 4.1 left them in

## Planning Status

**Draft — depends on 4.1.**

This sub-phase assumes 4.1's activity entry model already exists (kind
taxonomy, `Role`/`Content`/`Metadata` shape, no `TownhallMessage`-only
assumption). It does not redesign that model; it wires real-world actions
into it. See `docs/phases/phase-4/IMPLEMENTATION_PLAN.md` for the umbrella.

Known constraints going in (from 4.1 and prior verification):

- `TownhallViewModel.InitializeSampleData()` hardcodes 3 sample channels, 2
  sample agents, and a handful of sample messages. This sub-phase is where
  that sample-only assumption should actually be replaced with explicit,
  minimal session-state initialization — not deferred further.
- `SelectChannelCommand` and `SendMessageCommand` are the only two
  user-triggered actions today. Both are the natural first sources of
  auto-logged entries.
- No real agent execution engine exists yet (explicitly out of scope for all
  of Phase 4), so "agent-originated" entries in this sub-phase are limited to
  what a human can trigger manually or what a stub/test harness produces —
  not live model output.

## Scope

**Goal:** Make Townhall produce real, timestamped activity entries
automatically from user actions, and replace sample-only ViewModel state with
explicit session-state initialization — without adding new UI or filtering.

**In scope:**

- Auto-generate a timestamped activity entry (using the 4.1 model) when:
  - a user sends a chat message
  - a user switches the active channel
- Define and implement classification rules: what makes an entry "chat" vs.
  "action/log" per the 4.1 kind taxonomy
- Replace `InitializeSampleData()` with explicit session-state
  initialization: still seeds an initial channel/agent list (Zaide is not
  useful with zero channels), but the seeding is presented as real initial
  state, not demo filler mixed into the same method as business logic
- Keep `TownhallState`/`TownhallViewModel` as the only affected layers

**Out of scope:**

- Any Townhall UI or rendering change, including filtering (Phase 4.3)
- Real agent execution producing live agent-originated entries
- Persisting activity history across app restarts
- Changing the 4.1 entry-model shape itself (only construct/consume it)

## Milestones

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate and baseline verification | `dotnet build`, `dotnet test` | ⬜ |
| M1 | Classification rules defined (chat vs. action/log) | Design note in this doc reviewed | ⬜ |
| M2 | Auto-log on send-message and channel-switch | ViewModel tests asserting generated entries per action | ⬜ |
| M3 | Sample-only initialization replaced with explicit session-state seeding | ViewModel tests confirming no behavior regression | ⬜ |
| M4 | Docs sync for this sub-phase + exit audit | `dotnet build`, `dotnet test`, phase-4 umbrella status updated | ⬜ |

## Planned Change Shape

1. Add a small internal helper (e.g. on `TownhallViewModel` or a focused
   collaborator) that appends a classified activity entry to the active
   channel's collection — used by both the send-message and channel-switch
   paths so classification logic isn't duplicated.
2. Wire `SendMessageCommand` to log the chat entry it already creates (no
   behavior change to the message itself, just confirms it's tagged
   correctly under the 4.1 taxonomy).
3. Wire `SelectChannelCommand`/`ActiveChannelId` setter to append a
   channel-switch action entry to the newly active channel.
4. Replace `InitializeSampleData()` with a renamed, narrower
   `InitializeSessionState()` (or similar) that seeds only what's needed for
   the app to be usable on first run, documented as intentional seed data
   rather than demo filler.
5. Update `TownhallViewModelTests.cs` to assert on generated entries.

## Exit Conditions

- [ ] Sending a message and switching channels both produce timestamped, correctly-classified activity entries automatically
- [ ] `TownhallViewModel` no longer relies on `InitializeSampleData()`; initialization is explicit session-state seeding
- [ ] Classification rules (chat vs. action/log) are documented and implemented consistently
- [ ] `TownhallViewModelTests.cs` covers auto-logging behavior
- [ ] `dotnet build` and `dotnet test` pass
- [ ] `docs/phases/phase-4/IMPLEMENTATION_PLAN.md` sub-phase table updated to mark 4.2 complete
