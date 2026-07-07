# Phase 4.3: Townhall Activity History UI — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 4.2 is complete (auto-logging + session-state initialization landed; see `docs/phases/phase-4.2/IMPLEMENTATION_PLAN.md` exit conditions)
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [ ] Re-confirm `TownhallView.cs` / `TownhallChatPanel.cs` still match the structure described below
- [ ] Re-read `docs/DESIGN.md` in full before starting — this sub-phase adds visible UI

## Planning Status

**Draft — depends on 4.1 and 4.2.**

This sub-phase assumes activity entries (4.1) now exist and are auto-logged
(4.2) for chat messages and channel switches. It adds the actual visual
distinction and filtering the wider Phase 4 goal calls for. See
`docs/phases/phase-4/IMPLEMENTATION_PLAN.md` for the umbrella.

Known constraints going in:

- `TownhallView.SetChatMessages` currently treats the active channel's
  collection as a single homogeneous list and forwards it straight to
  `TownhallChatPanel.SetMessages`. Rendering chat vs. action/log entries
  differently means either branching inside `TownhallChatPanel` on entry
  kind, or introducing a second render path — this decision must be made
  explicitly in M1, not improvised mid-implementation.
- All new UI must follow `docs/DESIGN.md`: no hardcoded colors (theme tokens
  via `DynamicResource`/`Application.Current!.Resources[...]` only), no
  `FontSize=`/`FontWeight=` literals (route through `TextStyles`), 150–200ms
  animations with cubic easing, 16px minimum panel padding, no thick
  borders — separation by gap/shadow/opacity only.
- `TownhallView`'s layout is a fixed `Grid` (sidebar | splitter | chat area).
  Filtering UI should fit inside the existing chat/input column rather than
  reworking the outer layout (no large layout rewrites, per Phase 4 scope).

## Scope

**Goal:** Make chat vs. action/log entries visually and semantically
distinct in the Townhall chat panel, and add a way to filter the visible
history — without changing the outer Townhall layout or adding agent panels.

**In scope:**

- Visual treatment distinguishing chat entries from action/log entries
  (e.g. compact inline style for log-like entries vs. full message bubble
  for chat, using existing tokens per `DESIGN.md`)
- A filter control (exact control TBD at M1 — e.g. a small segmented toggle
  or dropdown near the chat panel header) to show: all / chat-only /
  activity-only
- Scroll behavior: history remains scrollable with the filter applied,
  including with a large number of entries
- Any `TownhallChatPanel`/`TownhallView` changes needed to render the 4.1
  entry-kind taxonomy correctly

**Out of scope:**

- Changing the activity entry model itself (4.1) or auto-logging rules (4.2)
- Per-agent panels or any layout change outside the existing Townhall column
- Persisting filter preference across app restarts (nice-to-have, not required)
- Search within activity history (distinct from existing terminal search;
  not part of this sub-phase)

## Milestones

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | Entry gate and baseline verification | `dotnet build`, `dotnet test` | ⬜ |
| M1 | Rendering-path decision (branch-in-panel vs. dual path) + filter-control design decision, recorded in this doc | Design note reviewed | ⬜ |
| M2 | Visual distinction between chat and action/log entries implemented | View tests for rendering by entry kind | ⬜ |
| M3 | Filter control implemented and wired to the active channel's rendered list | View tests for filter behavior; manual scroll check | ⬜ |
| M4 | Docs sync for this sub-phase + exit audit | `dotnet build`, `dotnet test`, `DESIGN.md` verification checklist, phase-4 umbrella status updated | ⬜ |

## Planned Change Shape

1. Decide and record the rendering approach: most likely extend
   `TownhallChatPanel` to branch on entry kind when building each row,
   rather than introducing a second panel — keeps one scrollable list, one
   filter surface.
2. Add row-building logic for action/log-kind entries (e.g. compact
   timestamp + icon + summary line) distinct from the existing chat bubble
   rendering, using only existing/added theme tokens.
3. Add a small filter control in the chat panel header area (all / chat /
   activity), wired via a `ReactiveCommand`/bound property on
   `TownhallViewModel` — filtering is a view-level projection over the
   existing per-channel collection, not a new data source.
4. Confirm scrolling remains correct when the filtered list changes length.
5. Run the `DESIGN.md` Verification Checklist explicitly before calling M4 done.

## Exit Conditions

- [ ] Chat and action/log entries are visually distinct in the Townhall chat panel
- [ ] A working filter control lets the user view all / chat-only / activity-only entries
- [ ] Scrolling works correctly at any filter setting, including with many entries
- [ ] No layout changes outside the existing Townhall column
- [ ] `docs/DESIGN.md` Verification Checklist passes for all new UI
- [ ] `dotnet build` and `dotnet test` pass
- [ ] `docs/phases/phase-4/IMPLEMENTATION_PLAN.md` sub-phase table updated to mark 4.3 complete
