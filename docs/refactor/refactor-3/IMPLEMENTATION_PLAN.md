# Refactor 3: Agent-First Layout Transition — Implementation Plan

## Status

**Planned.**

This plan implements the already-decided drift recorded in
`docs/refactor/refactor-3/DRIFT_DECISION.md`.

It is written to be easy for text-only agents to follow without relying on
mockups or visual interpretation.

---

## Purpose

Refactor 3 changes Zaide's primary screen from an editor-first IDE layout to an
agent-first workspace layout.

The goal is not to remove the editor or reduce the importance of direct coding.
The goal is to change the default center of attention.

After this refactor:

- Townhall is the main workspace in the center
- The editor remains always visible on the right
- The file tree remains on the left
- The terminal/log area remains at the bottom

In short:

- before: file tree | editor | agent area
- after: file tree | townhall | editor

---

## Product Decision Carried Into This Plan

The team has already decided to "go for it."

That means this plan does **not** spend effort defending the old editor-first
 layout or preserving it as the default direction.

It also means the known "left side feels heavy" concern is **not** a blocker for
Refactor 3.

That concern is deferred to a later UX phase, where Zaide may add:

- panel switching
- window switching
- more dynamic workspace composition
- collapsible or mode-based side surfaces

Refactor 3 should not stall waiting for those future mechanisms.

---

## Layout Definition

The target layout should be understood in plain text like this:

```text
┌──────┬──────────┬──────────────────────────────┬────────────────────┐
│      │          │                              │                    │
│ Nav  │ Explorer │     Townhall                 │   Editor           │
│ Bar  │  /       │  channels | chat | people    │   focused code     │
│      │ Source   │  + message input             │   + status bar     │
│      │ Control  │                              │                    │
├──────┴──────────┴──────────────────────────────┴────────────────────┤
│                     Terminal / Logs                                 │
└─────────────────────────────────────────────────────────────────────┘
```

Column breakdown:

1. **Far-left nav bar** (~40px): Icon-only vertical strip for switching
   between Explorer, Source Control, and future left-panel modes. App logo
   at top, settings at bottom.

2. **Left content panel** (~260px): Single-slot panel controlled by nav bar mode:
   - Explorer mode: file tree and project navigation
   - Source Control mode: Source Control placeholder (reserved layout space,
     no behavior in Refactor 3)

3. **Center column** (widest): Townhall — the primary attention surface.
   Internal structure:
   - Left sub-panel: Channels list (thread/channel navigation)
   - Center: Chat message area (messages with sender, content, timestamps)
   - Right sub-panel: People list (agents/users with status)
   - Bottom: Message input area

4. **Right column**: Editor — always visible for direct code inspection and
   editing. Header shows a link to the active Townhall thread ("Shared in
   #townhall").

5. **Bottom row**: Terminal and logs, spanning the content area (under center
   and right columns, not under the left panel). Reads as shared runtime
   output, not editor-owned space.

The center column must feel more important than the right column.
The editor must remain strong, usable, and always present.
The file tree must keep working as a first-class navigation tool.

This is not "chat added to the IDE."
This is "agent workspace with editor support."

---

## Scope

### In Scope

- Rebuild the main window layout so Townhall becomes the center column
- Keep the editor visible as the right column
- Add a far-left icon-only nav bar for panel switching affordance
- Keep the file tree on the left via nav-driven panel mode switching with a
  Source Control placeholder mode
- Keep the terminal/log row on the bottom (content-area width, not full width)
- Replace the placeholder agent area with a real Townhall surface
- Build Townhall with three internal sections: channels sidebar, chat area,
  people sidebar
- Add the Townhall view-model/model structure including channel list,
  `TownhallMessage` model with timestamps, and `WorkspaceAgent` status
- Add a minimal editor-to-townhall link indicator in the editor header
- Update root docs and refactor docs to reflect the new agent-first baseline

### Out of Scope

- Real agent execution
- Agent-to-agent routing
- Full persistence
- Git workflow behavior (branching, staging, committing — no logic changes)
- Dynamic panel switching beyond left-panel mode toggle (nav bar switches
  Explorer/SC only; no arbitrary panel routing)
- Floating windows
- Solving every layout-balance concern in this refactor
- Focused File section below the editor (reserved for a later phase)
- Log categorization with colored tags (bottom panel remains a raw terminal)
- Message reactions, audio indicators, or inline agent warning badges
- Status bar customization (app name, version, Live Share, notifications)

---

## Non-Goals

This plan does not try to:

- make the final forever UI
- solve advanced workspace customization
- remove the editor from prominence entirely
- turn Townhall into a complete production chat system
- redesign file tree or terminal behavior beyond what layout transition requires
- implement full Source Control panel behavior
- add categorized log output to the bottom panel

---

## Entry Conditions

- `dotnet build Zaide.slnx` passes
- `dotnet test Zaide.slnx --no-build` passes
- Root docs already describe the agent-first direction
- Refactor 2 remains complete and is not reopened here

---

## Measurable Layout Acceptance Criteria

These constraints are objective pass/fail gates for the layout:

| ID | Criterion | How to Verify |
|----|-----------|---------------|
| LC-1 | Center column visible width ≥ 40% of total content area (excluding nav bar) at default window size | Resize to default → measure |
| LC-2 | Right column (editor) visible width < center column at default window size | Visual comparison; center must be wider |
| LC-3 | At minimum supported window width (960 px), both center and right columns remain visible (no overflow to zero) | Resize to 960 px → both columns visible |
| LC-4 | Bottom panel spans exactly under center + right columns; left panel extends full height below it | Visual inspection; no bottom bar under left panel |
| LC-5 | Bottom panel minimum height ≥ 120 px at default window size; resizes with window drag | Resize bottom edge |
| LC-6 | Left panel (Explorer + SC) does not exceed 320 px at default window size | Measure or read code constants |
| LC-7 | Nav bar width is exactly ~40 px; icon-only, no text labels visible | Visual inspection |
| LC-8 | Townhall has visible channels sub-panel (~120 px), chat area (fills remaining), and people sub-panel (~120 px) | Visual inspection of sub-panels |

---

## Window Size Behavior

| Window Width | Behavior |
|-------------|----------|
| ≥ 1200 px | All panels at comfortable default sizes |
| 960–1199 px | Left panel and nav bar unchanged; center and right columns shrink proportionally but both remain visible (LC-3) |
| < 960 px | Below minimum supported width; no guarantee of layout integrity |

Collapse priority if content-area width < 700 px: shrink people sub-panel first, then channels sub-panel, then editor. Townhall chat area is never fully hidden.

---

## Verification Artifacts (required at M6 completion)

Each milestone's exit check should produce the following artifacts where applicable:

1. **Screenshot** — capture at default window size and at minimum (960 px). File naming: `docs/refactor/refactor-3/verification/m{N}-default.png`, `m{N}-min.png`.
2. **Build log** — full `dotnet build Zaide.slnx` output saved or quoted in PR/commit description.
3. **Test log** — full `dotnet test Zaide.slnx --no-build` output.
4. **Checklist sign-off** — copy the Manual Verification Checklist (below) into the PR description and check every item.
5. **No-new-behavior check** — for deferred areas (Source Control, rich Townhall features, log categorization), state explicitly in the PR description that no behavior was added beyond what the milestone specifies.

---

## Design Rules For This Refactor

1. Do not reopen the product-direction debate inside the implementation work.
2. Do not preserve editor-first visual hierarchy by accident.
3. Do not break file-tree, editor, or terminal workflows in the name of purity.
4. Do not block on future panel/window switching ideas.
5. Prefer stable incremental code changes over a giant rewrite.
6. Reserve layout space for concept elements that are behaviorally deferred
   (e.g., Source Control placeholder) so the layout structure matches the
   concept even when the feature is not yet functional.

---

## Milestones

| Milestone | Description | Exit Signal |
|-----------|-------------|-------------|
| M0 | Confirm baseline health and current layout assumptions | Build/tests green and current shell understood |
| M1 | Main window layout transition with nav bar | Main window reads as nav bar \| left-panel mode slot (Explorer/SC) \| townhall \| editor |
| M2 | Townhall domain/view-model foundation | Townhall has channel list, message model, agent status, and state model |
| M3 | Townhall view integration | Center panel shows channels sidebar, chat area, people sidebar, and input |
| M4 | Editor/right-column adaptation | Editor visible with townhall link; right column reads as focused code surface |
| M5 | Terminal/logs alignment | Bottom area works under center+right, reads as shared runtime surface |
| M6 | Regression sweep and doc sync | Existing workflows stable, docs match reality |

---

## File-Level Implementation Map

Each milestone lists the concrete files and classes that must be created or
modified. This map prevents scope creep and makes PR reviews easier.

| Milestone | Files Created | Files Modified |
|-----------|---------------|----------------|
| M0 | — | — (verification only) |
| M0.5 | — | `src/App.axaml`, `src/Views/EditorTabBar.cs`, any view with hardcoded colors |
| M1 | `src/Views/NavBar.cs`, `src/Views/SourceControlPlaceholder.cs` | `src/MainWindow.axaml` (layout), `src/MainWindow.axaml.cs` (wiring only) |
| M2 | `src/Models/Channel.cs`, `src/Models/TownhallMessage.cs`, `src/Models/WorkspaceAgent.cs`, `src/Models/TownhallState.cs`, `src/ViewModels/TownhallViewModel.cs` | — |
| M3 | `src/Views/TownhallView.cs`, `src/Views/TownhallChannelPanel.cs`, `src/Views/TownhallChatPanel.cs`, `src/Views/TownhallPeoplePanel.cs`, `src/Views/TownhallInputArea.cs` | `src/MainWindow.axaml.cs` (wire Townhall into center column) |
| M4 | — | `src/Views/EditorTabBar.cs` (add townhall link label), `src/Views/EditorView.cs` (adjust quietness) |
| M5 | — | `src/MainWindow.axaml.cs` (bottom panel span), `src/Views/TerminalPanel.cs` (relabel header) |
| M6 | — | Docs only: `docs/DESIGN.md`, `docs/architecture/OVERVIEW.md`, `docs/roadmap/PHASES.md` |

All new files follow one-class-per-file naming per `docs/CONVENTIONS.md`.

---

## Milestone Details

### M0: Baseline Verification

Verify the following before changing code:

- current build passes
- current tests pass
- current `MainWindow` layout is still file tree on the left, editor in the
  center, placeholder agent area on the right
- no new root-level doc drift was introduced after the earlier direction update

This milestone exists so later regressions are measured against reality, not
memory.

### M0.5: Palette Rematch

The concept.png uses a different color palette than the current DESIGN.md
"Ayaka Violet" system. Refactor 3 includes a palette update to match the
concept.

| Token | Old (Ayaka Violet) | New (concept.png) | Usage |
|-------|-------------------|-------------------|-------|
| Primary Accent | `#8FA5DE` Periwinkle Violet | `#066ADB` Bright Blue | Active tabs, primary buttons, focus, links |
| Soft Accent | `#C2C2E5` Lavender | `#3ED3E4` Cyan Teal | Code types, secondary indicators |
| Warning | *(not defined)* | `#FCBB47` Amber | Warnings, modified indicators |
| Success | *(not defined)* | `#28A745` Green | Added indicators, active dots, sync |
| Deep Base | `#142043` Blue-Purple | `#0A0F19` Near-Black Navy | Window/panel backgrounds |
| Panel Surface | *(same as base)* | `#0B121D` Lighter Navy | Elevated panels, code areas |
| Text Active | `#E3E4F4` | `#E3E4F4` (unchanged) | High-contrast text |
| Text Secondary | *(not defined)* | `#8B95A5` Muted Blue-Gray | Timestamps, line numbers |
| Separator | *(not defined)* | `#070C16` Darkest | 1px panel separators |

This palette change is already applied in `docs/DESIGN.md` section 7.

Implementation:

- Update `App.axaml` resource dictionary with new accent brushes
- Update any hardcoded color values in view files to use the new tokens
- Verify the overall darkness matches the concept (near-black backgrounds,
  not blue-purple)

**Token-migration checklist (do before marking M0.5 complete):**

1. Run `grep -rn '#[0-9A-Fa-f]\{6\}' src/Views/ src/App.axaml` to find all hex literals.
2. Cross-reference against the palette table above; any hex that does not
   match a token value or a deliberate transparent variant (e.g., `#22` prefix)
   is a migration candidate.
3. Verify no legacy `#8FA5DE`, `#C2C2E5`, `#142043`, or `#1A2847` values
   remain anywhere in `src/`.
4. Verify every view file references palette tokens by resource key name
   (e.g., `DynamicResource SurfaceBaseBrush`) rather than hardcoded values.
5. Run `dotnet build Zaide.slnx` to confirm no breakage.
6. Capture a screenshot at default window size for visual spot-check.

### M1: Main Window Layout Transition

Change `src/MainWindow.axaml` so the shell grid/topology becomes (with
`src/MainWindow.axaml.cs` limited to wiring/composition):

- far-left: icon-only nav bar (~40px)
- left: single-slot content panel driven by nav mode (Explorer or Source
  Control placeholder)
- center: Townhall
- right: editor
- bottom: terminal/logs (spanning content area under center + right only)

Implementation intent:

- Add a narrow vertical nav bar on the far left with icons for Explorer
  (active) and Source Control. This provides the navigation affordance shown
  in the concept and reserves the structural slot for future panel switching.
- Keep the file tree in the left content panel in Explorer mode.
- Add Source Control placeholder mode in the same left-panel slot (empty panel
  with "Source Control" header — no commit logic, no branch selector, no
  change lists).
- Move the editor from center to right.
- Replace the current right-side placeholder model with a real center
  Townhall surface (initially a simple container; M2/M3 add content).
- Change the bottom area to span under center + right only, not the full
  window width. The left panel extends full height.

Important constraint:

- the center column must be visually dominant over the right editor column

Safe implementation rule:

- preserve as much existing editor/file-tree/terminal wiring as possible while
  changing only their position and composition

### M2: Townhall Domain/ViewModel Foundation

Add the domain and view-model structure so Townhall is not just a label.

Models needed:

- `Channel` — represents a named channel (e.g., "townhall-main", "ai-status").
  Fields: id, name, isActive.
- `TownhallMessage` — a single message in a channel. Fields: id, senderId,
  content, timestamp (DateTimeOffset). Reactions, audio, and inline warning
  indicators are explicitly deferred.
- `WorkspaceAgent` — represents a person or agent in the workspace. Fields:
  id, name, role (user/agent), status (active/busy/idle). Warning badges are
  deferred.
- `TownhallState` — current session state. Holds: list of channels, active
  channel id, list of messages for active channel, list of agents, current
  draft text.

ViewModels needed:

- `TownhallViewModel` — exposes observable properties for channels,
  `TownhallMessage` items, `WorkspaceAgent` items, and draft. Commands:
  selectChannel, sendMessage. Uses reactive bindings per project conventions.

This milestone is about making the center column feel like a real workspace.
It is not about building final agent infrastructure.

### M3: Townhall View Integration

Add a real Townhall view into the center column with its internal structure.

The Townhall view should contain:

- **Channels sub-panel** (left, ~120px): vertical list of channel names.
  Active channel is highlighted. Tapping a channel switches the chat view.
  Rendered as a simple ItemsControl bound to the channel list.

- **Chat message area** (center): scrollable list of messages. Each message
  shows: sender name, content text, and timestamp. Messages are displayed
  newest-at-bottom. No reactions, no audio indicators, no inline warning
  badges in Refactor 3.

- **People sub-panel** (right, ~120px): vertical list of agents/users with
  name and status label (active/busy/idle). No avatar images, no warning
  badges — just text labels with color-coded status dots.

- **Input area** (bottom): text input with placeholder text and a send
  button. Attachment buttons and advanced input features are deferred.

Priority order:

1. visible center-stage workspace with channels, chat, and people
2. coherent data flow between view and view-model
3. stable layout that resizes correctly
4. future extensibility

### M4: Editor Right-Column Adaptation

Adapt the editor to its new role without weakening it.

Requirements:

- the editor stays visible at all times
- open/save/tab workflows continue to work
- the right column reads as a focused implementation surface
- the editor should feel slightly quieter than Townhall, not broken or hidden.
  Objective styling constraint: reduce editor-header visual emphasis using
  `SurfacePanelBrush` + `TextSecondaryBrush` for auxiliary labels, while
  preserving editor text readability with unchanged `TextPrimaryBrush` in the
  code surface.
- add a minimal "Shared in #townhall" label in the editor tab/header area
  to signal the conceptual link between editor content and the active
  townhall thread. This is a text label only — no bidirectional data flow,
  no file-context panel, no diff integration in Refactor 3.

The Focused File section (file name dropdown + Open Diff button) shown in the
concept is explicitly deferred to a later phase.

This milestone is successful if users can still code comfortably while the UI no
longer reads as editor-first.

### M5: Terminal/Logs Alignment

Preserve the bottom area as a shared runtime/log surface.

Requirements:

- terminal toggle still works
- restart/clear/state controls still work
- the bottom area spans the content area width (under center + right columns,
  not under the left panel — matching the concept layout)
- implementation contract: define the shell grid so terminal row starts at the
  center column and spans center+right columns only; left panel remains full
  height in its own column
- the area should read as shared operational output, not as editor-owned space

The concept shows categorized log output with colored tags ([BUILD], [AGENT],
[LOG]). This is explicitly deferred. For Refactor 3, the bottom panel remains
the existing terminal with current behavior. The panel header may be relabeled
to "TERMINAL / LOGS" to match the concept.

### M6: Regression Sweep And Doc Sync

After the layout transition is complete:

- rerun build and tests
- manually verify file tree workflows
- manually verify editor workflows
- manually verify terminal workflows
- confirm Townhall is the visual center with channels, chat, and people
  sections visible
- confirm the nav bar renders correctly on the far left
- confirm the Source Control placeholder is visible when Source Control mode is active
- confirm the editor shows the townhall link indicator
- confirm the bottom panel spans under center + right only
- confirm root docs and refactor docs still match reality

---

## Manual Verification Checklist

- open app: Townhall is visually central
- nav bar icons are visible on the far left
- file tree still opens folders and files correctly
- Source Control placeholder is visible when Source Control mode is selected
- Townhall shows channels list, chat messages, and people list
- switching channels changes the displayed messages
- typing and sending a message adds it to the chat
- agent status labels (active/busy/idle) are visible in the people panel
- editor tabs still open, switch, save, and close correctly
- editor header shows "Shared in #townhall" indicator
- terminal still opens and behaves correctly
- bottom panel spans under center + right, not under the left panel
- Townhall does not feel like a side widget
- editor remains visible and comfortable to use
- no obvious layout break at minimum window size

---

## Deferred Concerns

These are acknowledged but intentionally deferred:

- left side feels heavy
- explorer + source-control style density may compete with Townhall
- future need for panel switching (nav bar icons beyond Explorer/SC)
- future need for window switching
- future need for more flexible workspace composition
- Source Control panel behavior (branching, staging, committing)
- Focused File section below the editor
- Editor-to-Townhall bidirectional integration (file highlighting, diff sync)
- Log categorization with colored tags
- Message reactions, audio indicators, inline warning badges
- Agent avatars and warning badge icons
- Status bar customization (app name, version, Live Share, notifications)
- Multi-channel creation/management UI
- Channel-level permissions or access control

Those concerns are real, but they belong to a later UX/system phase.
They are not reasons to stop Refactor 3.

---

## Interaction Contracts

These contracts resolve ambiguities identified in the plan audit.

### Nav Bar Interaction

- Explorer icon is active by default on first launch.
- Clicking the Explorer icon shows the file tree in the left content panel.
  If it is already showing, clicking has no effect (no toggle-off).
- Clicking the Source Control icon shows the SC placeholder in the left
  content panel.
- Only one left-panel mode is visible at a time; the other is hidden
  (not collapsed to zero, just replaced in the same layout slot).
- Clicking the currently active icon does nothing (no empty state).

### Source Control Placeholder

- Renders a panel with a "SOURCE CONTROL" header and empty body.
- No commit input, no branch selector, no change list, no git commands.
- The placeholder exists solely to reserve layout space so the left column
  structure matches the concept and does not need reshuffling when real SC
  behavior arrives later.

### Townhall Message Ordering

- Messages are displayed newest-at-bottom (chronological, ascending).
- The message list is append-only within a session; messages are not
  reordered or inserted mid-list.
- Channel switching loads the message list for the selected channel;
  the previous channel's messages are replaced (no cross-channel merge).
- Draft text is per-channel; switching channels saves and restores draft.
- In-memory model is sufficient for Refactor 3; no persistence.

### Editor "Shared in #townhall" Label Placement

- The label appears in the editor tab bar area, to the right of the active
  tab's close button. It is a plain text read-only label styled with
  TextSecondary color.
- It is static text (not a clickable link, not a toggle) in Refactor 3.
- If no tab is open, the label is hidden.

---

## Failure Modes To Avoid

- Moving panels but keeping the editor visually dominant
- Making Townhall large in geometry but weak in hierarchy
- Breaking file tree resizing or file opening
- Breaking editor save/tab behavior
- Treating Townhall as a placeholder again
- Letting future customization ideas delay the current pivot
- Omitting the nav bar and then needing a second layout pass to add it
- Omitting Source Control layout space and then needing to reshuffle the
  left panel when it arrives
- Implementing a flat Townhall (just thread + input) without the channels
  and people sub-panels that give it visual density and coherence

---

## Exit Conditions

- Nav bar visible on the far left
- Left column supports mode-switched Explorer and Source Control placeholder views
- Main window layout is nav bar | left-panel mode slot (Explorer/SC) | townhall | editor
- Townhall shows channels sidebar, chat area, and people sidebar
- Townhall is the clear primary attention surface
- Editor remains always visible and usable with townhall link indicator
- File tree remains functional
- Terminal/logs remain functional (content-area width only)
- Build passes
- Tests pass
- Docs match the new reality

---

## Milestone Testing Expectations

These expectations define minimum validation depth per milestone.

- **M1 (layout transition):**
  - Build passes.
  - Manual layout validation against LC-1..LC-8 at default width and 960 px.
  - Confirm nav bar mode switching (Explorer ↔ Source Control placeholder).

- **M2 (Townhall models/viewmodel):**
  - Add/extend unit tests for `TownhallViewModel` and state behavior:
    - select channel updates active channel
    - send message appends newest-at-bottom
    - per-channel draft save/restore on channel switch

- **M3 (Townhall view integration):**
  - Build passes with no binding/runtime errors in output.
  - Manual verify channels/chat/people/input render and resize behavior.

- **M4 (editor adaptation):**
  - Regression-check tab open/switch/save/close workflows.
  - Verify "Shared in #townhall" visibility rules (shown only with active tab).

- **M5 (terminal/log alignment):**
  - Regression-check terminal toggle/restart/clear/state controls.
  - Verify terminal panel span under center+right only.

- **M6 (final sweep):**
  - `dotnet build Zaide.slnx`
  - `dotnet test Zaide.slnx --no-build`
  - Manual checklist fully signed off with artifacts.

## Follow-Up After Refactor 3

After this refactor is stable, the next work can continue with:

- richer Townhall behavior
- dedicated agent surfaces
- agent routing
- Source Control panel implementation (branching, staging, commit UI)
- Focused File section below the editor
- Editor ↔ Townhall bidirectional integration (agent highlights files, user
  diffs from thread context)
- Log categorization with colored tags
- Message reactions and rich content
- Agent avatars and status badges
- Status bar customization
- later workspace/panel/window switching to address layout density concerns

Refactor 3 is the pivot.
It is not the end state.

---

*Last updated: 2026-07-01*