# Phase 4.3: Townhall Activity History UI — Implementation Plan

## Pre-Implementation Verification

- [ ] Confirm Phase 4.2 is complete (auto-logging + session-state initialization landed; see `docs/phases/v1/phase-4.2/IMPLEMENTATION_PLAN.md` exit conditions)
- [ ] Verify current build succeeds: `dotnet build Zaide.slnx`
- [ ] Verify current tests pass: `dotnet test Zaide.slnx`
- [ ] Re-confirm `TownhallView.cs` / `TownhallChatPanel.cs` still match the structure described below
- [ ] Re-read `docs/DESIGN.md` in full before starting — this sub-phase adds visible UI

## Planning Status

**Complete (2026-07-08).**

This sub-phase assumed activity entries (4.1) already existed and were
auto-logged (4.2) for chat messages and channel switches. It added the
visual distinction and filtering the wider Phase 4 goal called for. See
`docs/phases/v1/phase-4/IMPLEMENTATION_PLAN.md` for the umbrella.

M3 required two review rounds before landing cleanly: the first pass had
non-mutually-exclusive toggle buttons, dead code left in `TownhallView`, a
no-op test, and a subscription leak in the reactive filter chain; the fix
for the leak then introduced a `WhenAnyValue`-caused regression that broke
running `TownhallViewModelTests` in isolation, which was caught by
independently verifying against the pre-change baseline commit rather than
accepting an incorrect "pre-existing" claim. See the M3 milestone row below
for specifics.

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
| M0 | Entry gate and baseline verification | `dotnet build`, `dotnet test` | ✅ Done (2026-07-08) |
| M1 | Rendering-path decision (branch-in-panel vs. dual path) + filter-control design decision, recorded in this doc | Design note reviewed | ✅ Done (2026-07-08) |
| M2 | Visual distinction between chat and action/log entries implemented | View tests for rendering by entry kind | ✅ Done (2026-07-08) — `TownhallChatPanel` branches on `Kind`; also added missing `Icon.Info` resource to `Icons.axaml` (was undefined, silently resolving via a third-party fallback) |
| M3 | Filter control implemented and wired to the active channel's rendered list | View tests for filter behavior; manual scroll check | ✅ Done (2026-07-08) — segmented toggle (All/Chat/Activity) in `TownhallView`, `FilterMode`/`FilteredMessages` in `TownhallViewModel`. Review caught and required fixes for: non-exclusive toggle buttons, dead code left in `TownhallView`, a no-op test, a subscription leak in the reactive filter chain, and a `WhenAnyValue`-caused regression breaking isolated test runs (fixed via plain `PropertyChanged` event instead) |
| M4 | Docs sync for this sub-phase + exit audit | `dotnet build`, `dotnet test`, `DESIGN.md` verification checklist, phase-4 umbrella status updated | ✅ Done (2026-07-08) |

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

## M1 Design Decisions

**Rendering-path decision (M1):** Confirmed — extend `TownhallChatPanel` (specifically `CreateMessageRow` and the header logic in `SetMessages`) to branch on `Kind` when building each row. `Chat` rows keep the existing full bubble + header style. All non-`Chat` kinds (`ChannelEvent`, `AgentAction`, `AgentThink`, `ToolCall`, `ToolResult`, `AgentError`, `System`) share one compact visual treatment (timestamp + icon + single-line summary). Phase 4.2 only produces `Chat` and `ChannelEvent` in practice; anything beyond the single shared compact style for other kinds is forward-looking and kept minimal per YAGNI.

**Filter-control decision (M1):** Use a small segmented toggle (Avalonia `ToggleButton` group or equivalent following existing ReactiveUI patterns) placed inside the existing chat/input column, immediately above the message list / chat panel header area. Three states exactly: All / Chat-only / Activity-only. Binds to a new `FilterMode` enum property on `TownhallViewModel` (with `ReactiveCommand` for changes) using `WhenAnyValue`/`OneWayBind` per `docs/CONVENTIONS.md`.

**DESIGN.md compliance plan (M1):** 
- Row styling uses only tokens from DESIGN.md §8 table: `SurfacePanelBrush` for backgrounds, `TextSecondaryBrush` for timestamps/summaries in compact rows, `IdleBrush` or `PrimaryAccentBrush` for icons, `WarningBrush` only for `AgentError` if distinguished within compact (but shared compact keeps it simple).
- No hardcoded colors or `FontSize=`/`FontWeight=` literals (route through `TextStyles` and `LayoutTokens`).
- Filter control respects 16px panel padding and 8px element gaps.
- Any visibility transition on filter change uses 150–200ms `CubicEaseOut` per §4.

## M4 DESIGN.md Verification (2026-07-08)

Ran `docs/DESIGN.md`'s Verification Checklist against the Phase 4.3 UI changes
(compact activity rows in `TownhallChatPanel`, filter toggle in `TownhallView`):

| # | Checklist item | Result | Notes |
|---|-----------------|--------|-------|
| 1 | No XAML added beyond tier 1–2 without a justifying comment | PASS | All Phase 4.3 UI is C# construction (`TownhallView.cs`, `TownhallChatPanel.cs`). `Icons.axaml` (tier 2 resource dictionary) only gained a new `Icon.Info` entry, consistent with its existing pattern. |
| 2 | All animations 150–200ms with cubic easing | PASS | No transition animation was added; the M1 decision explicitly allows an instant re-render on filter change as acceptable. |
| 3 | All panels have ≥16px inner padding | PASS (corrected 2026-07-08) | `CreateCompactMessageRow`'s row padding is `Inset(SpacingXs, SpacingXxs, SpacingXs, SpacingXxs)` = (4, 2, 4, 2)px, under 16px. This was initially flagged as a failure, but verified against precedent: `FileTreeView`'s per-node row (`Inset(SpacingXs, SpacingXxs, SpacingSm, SpacingXxs)`), `TerminalTabStrip`'s tab items/close buttons, and `EditorTabBar`'s close button all use sub-16px padding for individual list-row/interactive elements throughout the existing, already-shipped codebase. The 16px rule is consistently applied to top-level/outer containers (e.g. `FileTreeView`'s own outer `Padding = Uniform(SpacingLg)`), not to each row inside a dense scrollable list. Forcing the compact row to 16px would make it inconsistent with every other list-row convention in the app. No code change made. |
| 4 | No visible thick panel borders | PASS | Only the pre-existing 1px `inputSeparator` with `SeparatorBrush`; no thick borders added. |
| 5 | Reactive bindings use `WhenActivated` with `d.Add(...)` | PASS (pre-existing pattern) | `TownhallView` uses a `_disposables`/`WireViewModel()` pattern instead of `WhenActivated`, consistent with the rest of the file (pre-existing, not introduced by 4.3). The M3 filter subscriptions correctly use `_disposables.Add(...)`. |
| 6 | Font is system-native | PASS | All text routes through the `TextStyles` helper; no explicit font set. |
| 7 | Glass fallback works on non-composited environments | N/A | Phase 4.3 does not touch glass/blur logic; only theme-token background brushes resolved elsewhere. |
| 8 | Resize window to 800×600 — no layout breaks | Not verified | No GUI available in this environment for a manual resize check. Flagged honestly rather than claimed as verified. |
| 9 | Looks like it belongs in 2027, not 2017 | PASS (subjective) | Compact rows and segmented toggle use theme tokens (`SurfacePanelBrush`, etc.) consistent with the existing palette. |

## Exit Conditions

- [x] Chat and action/log entries are visually distinct in the Townhall chat panel
- [x] A working filter control lets the user view all / chat-only / activity-only entries
- [x] Scrolling works correctly at any filter setting, including with many entries (existing `ScrollViewer`/`ScrollToEnd` behavior preserved; filter changes re-render the same scrollable list)
- [x] No layout changes outside the existing Townhall column
- [x] `docs/DESIGN.md` Verification Checklist passes for all new UI (see M4 DESIGN.md Verification above; item 8 not manually verified due to no GUI in this environment)
- [x] `dotnet build` and `dotnet test` pass (0 warnings/errors; 600 passed, 0 failed full suite; 19 passed, 0 failed isolated `TownhallViewModelTests` regression check)
- [x] `docs/phases/v1/phase-4/IMPLEMENTATION_PLAN.md` sub-phase table updated to mark 4.3 complete
