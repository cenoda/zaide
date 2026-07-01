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

2. **Left content panel** (~260px): Stacked vertically:
   - Top: Explorer (file tree and project navigation)
   - Bottom: Source Control placeholder (reserved layout space, no behavior
     in Refactor 3)

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
- Keep the file tree on the left, stacked above a Source Control placeholder
- Keep the terminal/log row on the bottom (content-area width, not full width)
- Replace the placeholder agent area with a real Townhall surface
- Build Townhall with three internal sections: channels sidebar, chat area,
  people sidebar
- Add the Townhall view-model/model structure including channel list, message
  model with timestamps, and agent status
- Add a minimal editor-to-townhall link indicator in the editor header
- Update root docs and refactor docs to reflect the new agent-first baseline

### Out of Scope

- Real agent execution
- Agent-to-agent routing
- Full persistence
- Git workflow behavior (branching, staging, committing — no logic changes)
- Dynamic panel switching (nav bar icons switch Explorer/SC only; no
  arbitrary panel routing)
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
| M1 | Main window layout transition with nav bar | Main window reads as nav bar \| explorer+SC \| townhall \| editor |
| M2 | Townhall domain/view-model foundation | Townhall has channel list, message model, agent status, and state model |
| M3 | Townhall view integration | Center panel shows channels sidebar, chat area, people sidebar, and input |
| M4 | Editor/right-column adaptation | Editor visible with townhall link; right column reads as focused code surface |
| M5 | Terminal/logs alignment | Bottom area works under center+right, reads as shared runtime surface |
| M6 | Regression sweep and doc sync | Existing workflows stable, docs match reality |

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

### M1: Main Window Layout Transition

Change `src/MainWindow.axaml.cs` so the shell becomes:

- far-left: icon-only nav bar (~40px)
- left: file tree (top) + Source Control placeholder (bottom)
- center: Townhall
- right: editor
- bottom: terminal/logs (spanning content area under center + right only)

Implementation intent:

- Add a narrow vertical nav bar on the far left with icons for Explorer
  (active) and Source Control. This provides the navigation affordance shown
  in the concept and reserves the structural slot for future panel switching.
- Keep the file tree in the left content panel, now stacked above a Source
  Control placeholder (empty panel with "Source Control" header — no commit
  logic, no branch selector, no change lists).
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
- `Message` — a single message in a channel. Fields: id, senderId, content,
  timestamp (DateTimeOffset). Reactions, audio, and inline warning indicators
  are explicitly deferred.
- `Agent` — represents a person or agent in the workspace. Fields: id, name,
  role (user/agent), status (active/busy/idle). Warning badges are deferred.
- `TownhallState` — current session state. Holds: list of channels, active
  channel id, list of messages for active channel, list of agents, current
  draft text.

ViewModels needed:

- `TownhallViewModel` — exposes observable properties for channels, messages,
  agents, and draft. Commands: selectChannel, sendMessage. Uses reactive
  bindings per project conventions.

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
- the editor should feel slightly quieter than Townhall, not broken or hidden
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
- confirm the Source Control placeholder is visible below Explorer
- confirm the editor shows the townhall link indicator
- confirm the bottom panel spans under center + right only
- confirm root docs and refactor docs still match reality

---

## Manual Verification Checklist

- open app: Townhall is visually central
- nav bar icons are visible on the far left
- file tree still opens folders and files correctly
- Source Control placeholder is visible below the file tree
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
- Left column shows Explorer (top) and Source Control placeholder (bottom)
- Main window layout is nav bar | explorer+SC | townhall | editor
- Townhall shows channels sidebar, chat area, and people sidebar
- Townhall is the clear primary attention surface
- Editor remains always visible and usable with townhall link indicator
- File tree remains functional
- Terminal/logs remain functional (content-area width only)
- Build passes
- Tests pass
- Docs match the new reality

---

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