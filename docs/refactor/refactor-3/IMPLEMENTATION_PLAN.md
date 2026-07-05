# Refactor 3: Agent-First Layout Transition — Implementation Plan

## Status

**In progress — M0, M0.5, and M1 complete.**

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
- Source Control panel is a real (minimal) panel, not a placeholder
- Status bar shows app info, cursor position, language, project, branch, and
  AI model

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
┌──────┬──────────┬──────────┬─────┬──────────────────────────────────┬────────────────────┐
│      │          │          │ P   │                                  │                    │
│ Nav  │ Explorer │  Source  │ e   │     Townhall                    │   Editor           │
│ Bar  │  /       │ Control  │ o   │     chat messages               │   focused code     │
│      │  SC      │          │ p   │     + input area                │   + status info    │
│      │          │          │ l   │                                  │                    │
│      │          │          │ e   │                                  │                    │
│      │          │          │ ─── │                                  │                    │
│      │          │          │ C   │                                  │                    │
│      │          │          │ h   │                                  │                    │
│      │          │          │ a   │                                  │                    │
│      │          │          │ n   │                                  │                    │
│      │          │          │ n   │                                  │                    │
│      │          │          │ e   │                                  │                    │
│      │          │          │ l   │                                  │                    │
│      │          │          │ s   │                                  │                    │
├──────┴──────────┴──────────┴─────┴──────────────────────────────────┴────────────────────┤
│                                        Terminal / Logs                                     │
├────────────────────────────────────────────────────────────────────────────────────────────┤
│  Zaide  Ln 38, Col 1  │  C#  │  Aero  │  master  │  powered by Avisnis 12              │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

Column breakdown:

1. **Far-left nav bar** (~40px): Icon-only vertical strip for switching
   between Explorer, Source Control, and future left-panel modes. App logo
   at top, settings at bottom.

2. **Left content panel** (~260px): Single-slot panel controlled by nav bar mode:
   - Explorer mode: file tree and project navigation
   - Source Control mode: full Source Control panel with branch selector,
     change list, staging area, and commit input

3. **Center column** (widest): Townhall — the primary attention surface.
   Internal structure:
   - Left vertical sidebar (~140px): People list (top half) and Channels
     list (bottom half), separated by a horizontal divider. This is a tall
     narrow column, NOT a horizontal strip.
   - Right: Chat message area filling the remaining width (messages with
     sender, content, timestamps) and message input at the bottom.

4. **Right column**: Editor — always visible for direct code inspection and
   editing. Header shows a link to the active Townhall thread ("Shared in
   #townhall"). Below the code, shows focused file info and diff/edit indicator.

5. **Bottom row**: Terminal and logs, spanning the content area (under center
   and right columns, not under the left panel). Reads as shared runtime
   output with categorized log entries.

6. **Status bar** (bottom): Thin bar showing app name, cursor position,
   language, project, branch, and AI model info.

All panel boundaries use `GridSplitter` controls so every column and row
boundary can be drag-resized by the user. Specifically:

- Left panel ↔ Townhall boundary (horizontal resize)
- Townhall ↔ Editor boundary (horizontal resize)
- Content area ↔ Terminal/Logs boundary (vertical resize)
- People sub-panel ↔ Channels sub-panel boundary within Townhall (vertical
  resize)

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
- Keep the file tree on the left via nav-driven panel mode switching
- Implement a real Source Control panel (branch selector, change list,
  staging area, commit input) in the left panel's SC mode
- Keep the terminal/log row on the bottom (content-area width, not full width)
- Replace the placeholder agent area with a real Townhall surface
- Build Townhall with a vertical people/channels sidebar on the left
  (~140px wide, full height) and chat area filling the remaining width
- Add the Townhall view-model/model structure including channel list,
  `TownhallMessage` model with timestamps, and `WorkspaceAgent` status
  with avatar placeholders
- Add categorized log output to the bottom terminal panel ([BUILD], [AGENT],
  [LOG] tags with colored indicators)
- Add a minimal editor-to-townhall link indicator in the editor header
- Add a status bar showing app name, cursor position, language, project,
  branch, and AI model
- Make all panel boundaries drag-resizable via GridSplitter (left ↔ townhall,
  townhall ↔ editor, content ↔ terminal, people ↔ channels within townhall)
- Update root docs and refactor docs to reflect the new agent-first baseline

### Out of Scope

- Real agent execution
- Agent-to-agent routing
- Full persistence
- Dynamic panel switching beyond left-panel mode toggle (nav bar switches
  Explorer/SC only; no arbitrary panel routing)
- Floating windows
- Solving every layout-balance concern in this refactor
- Full git workflow behavior (branching, committing — the SC panel is minimal
  static/demo data, not wired to real git)
- Message reactions, audio indicators, or inline agent warning badges
  (beyond what the concept shows)
- Multi-channel creation/management UI
- Channel-level permissions or access control

---

## Non-Goals

This plan does not try to:

- make the final forever UI
- solve advanced workspace customization
- remove the editor from prominence entirely
- turn Townhall into a complete production chat system
- redesign file tree or terminal behavior beyond what layout transition requires
- implement full git integration (branching, staging, committing with real repo)
- add full agent execution infrastructure

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
| LC-8 | Townhall has a vertical sidebar on the left (~140px) with People (top) and Channels (bottom), and chat area + input filling the remaining width | Visual inspection of sub-panels |
| LC-9 | Source Control panel shows branch selector, change list, staged section, and commit input when SC mode is active | Switch to SC mode → visual inspection |
| LC-10 | Terminal/Logs panel shows categorized log entries with [BUILD], [AGENT], [LOG] tags | Visual inspection of log output |
| LC-11 | Status bar visible at bottom showing app name, cursor position, language, project, branch, and AI model | Visual inspection |
| LC-12 | Editor tab header shows "Shared in #townhall" indicator | Visual inspection with open tab |
| LC-13 | All panel boundaries are drag-resizable (left↔townhall, townhall↔editor, content↔terminal, people↔channels) | Drag each splitter → panels resize |
| LC-14 | Panel sizes persist reasonable defaults after resize (no panel collapses to zero unless dragged there) | Resize panels, restart app |

---

## Window Size Behavior

| Window Width | Behavior |
|-------------|----------|
| ≥ 1200 px | All panels at comfortable default sizes |
| 960–1199 px | Left panel and nav bar unchanged; center and right columns shrink proportionally but both remain visible (LC-3) |
| < 960 px | Below minimum supported width; no guarantee of layout integrity |

Collapse priority if content-area width < 700 px: shrink the people/channels sidebar first (down to 100px min), then editor. Townhall chat area is never fully hidden.

---

## Verification Artifacts (required at M6 completion)

Each milestone's exit check should produce the following artifacts where applicable:

1. **Screenshot** — capture at default window size and at minimum (960 px). File naming: `docs/refactor/refactor-3/verification/m{N}-default.png`, `m{N}-min.png`.
2. **Build log** — full `dotnet build Zaide.slnx` output saved or quoted in PR/commit description.
3. **Test log** — full `dotnet test Zaide.slnx --no-build` output.
4. **Checklist sign-off** — copy the Manual Verification Checklist (below) into the PR description and check every item.
5. **No-new-behavior check** — for deferred areas (full git integration, rich Townhall features, agent execution), state explicitly in the PR description that no behavior was added beyond what the milestone specifies.

---

## Design Rules For This Refactor

1. Do not reopen the product-direction debate inside the implementation work.
2. Do not preserve editor-first visual hierarchy by accident.
3. Do not break file-tree, editor, or terminal workflows in the name of purity.
4. Do not block on future panel/window switching ideas.
5. Prefer stable incremental code changes over a giant rewrite.
6. Reserve layout space for concept elements that are behaviorally deferred
   (e.g., full git integration) so the layout structure matches the
   concept even when the feature is not yet functional.
7. Use "Zaide" as the app name everywhere (title bar, status bar, about).
   The concept mockup shows "Aero Studio" — that is the concept name only;
   the implementation uses "Zaide."

---

## Milestones

| Milestone | Description | Exit Signal | Status |
|-----------|-------------|-------------|--------|
| M0 | Confirm baseline health and current layout assumptions | Build/tests green and current shell understood | ✅ Complete |
| M0.5 | Palette rematch to concept.png colors | App.axaml tokens updated, no hardcoded legacy hex values remain | ✅ Complete |
| M1 | Main window layout transition with nav bar + MainWindowViewModel nav-mode state + tests | Main window reads as nav bar \| left-panel mode slot (Explorer/SC) \| townhall \| editor; VM exposes LeftPanelMode with switch commands; tests verify mode switching | ✅ Complete |
| M2 | Townhall domain/view-model foundation with per-channel messages and bindable channel states | Townhall has channel list, message model, agent status with avatars, state model with ChannelMessages dictionary; Channels implement INotifyPropertyChanged for IsActive; ViewModel raises PropertyChanged for Messages on switch | ✅ Complete |
| M3 | Townhall view integration | Center panel shows people sidebar, channels sidebar, chat area, and input | ⬜ Pending |
| M4 | Editor/right-column adaptation | Editor visible with townhall link; right column reads as focused code surface | ⬜ Pending |
| M5 | Terminal/logs alignment and categorization | Bottom area works under center+right, shows categorized [BUILD]/[AGENT]/[LOG] entries | ⬜ Pending |
| M6 | Source Control panel and status bar | SC panel shows branch selector, change list, staging, commit input; status bar shows app info | ⬜ Pending |
| M7 | Regression sweep and doc sync | Existing workflows stable, docs match reality | ⬜ Pending |

---

## File-Level Implementation Map

Each milestone lists the concrete files and classes that must be created or
modified. This map prevents scope creep and makes PR reviews easier.

| Milestone | Files Created | Files Modified |
|-----------|---------------|----------------|
| M0 | — | — (verification only) |
| M0.5 | — | `src/App.axaml`, `src/Views/EditorTabBar.cs`, any view with hardcoded colors |
| M1 | `src/Views/NavBar.cs` | `src/MainWindow.axaml.cs` (layout built in C# per DESIGN.md §1), `src/ViewModels/MainWindowViewModel.cs` (add LeftPanelMode enum, mode property, switch commands), `tests/Zaide.Tests/MainWindowViewModelTests.cs` (add mode-switching tests) |
| M2 | `src/Models/Channel.cs`, `src/Models/TownhallMessage.cs`, `src/Models/WorkspaceAgent.cs`, `src/Models/TownhallState.cs`, `src/ViewModels/TownhallViewModel.cs` | `src/Program.cs` (register TownhallViewModel in DI) |
| M2-T | `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs` | — |
| M3 | `src/Views/TownhallView.cs`, `src/Views/TownhallPeoplePanel.cs`, `src/Views/TownhallChannelPanel.cs`, `src/Views/TownhallChatPanel.cs`, `src/Views/TownhallInputArea.cs` | `src/MainWindow.axaml.cs` (wire Townhall into center column) |
| M4 | — | `src/Views/EditorTabBar.cs` (add townhall link label), `src/Views/EditorView.cs` (adjust quietness) |
| M5 | `src/ViewModels/LogEntry.cs`, `src/ViewModels/LogCategorizer.cs` | `src/Views/TerminalPanel.cs` (relabel header to "Terminal / Logs", categorize output) |
| M6 | `src/Models/GitBranch.cs`, `src/Models/FileChange.cs`, `src/Models/SourceControlState.cs`, `src/ViewModels/SourceControlViewModel.cs`, `src/Views/SourceControlPanel.cs`, `src/Views/StatusBar.cs` | `src/MainWindow.axaml.cs` (wire SC panel into left slot, add status bar), `src/ViewModels/EditorViewModel.cs` (add caret line/col properties), `src/Views/EditorView.cs` (project caret position to VM), `src/ViewModels/MainWindowViewModel.cs` (add SC/status-bar deps), `src/Models/Workspace.cs` (expose project name), `src/Program.cs` (register SC/townhall VMs) |
| M7 | — | Docs only: `docs/DESIGN.md`, `docs/architecture/OVERVIEW.md`, `docs/roadmap/PHASES.md`, `README.md` |

All new files follow one-class-per-file naming per `docs/CONVENTIONS.md`.

---

## Milestone Details

### M0: Baseline Verification ✅

Verify the following before changing code:

- current build passes
- current tests pass
- current `MainWindow` layout is still file tree on the left, editor in the
  center, placeholder agent area on the right
- no new root-level doc drift was introduced after the earlier direction update

This milestone exists so later regressions are measured against reality, not
memory.

### M0.5: Palette Rematch ✅

The concept.png uses a different color palette than the current DESIGN.md
"Ayaka Violet" system. Refactor 3 includes a palette update to match the
concept.

#### Global Token Definitions

| Token Key | Hex | Name | Usage |
|-----------|-----|------|-------|
| `PrimaryAccentBrush` | `#066ADB` | Bright Blue | Active tabs, primary buttons, focus rings, links, active channel highlight, "Commit Staged" button |
| `SecondaryAccentBrush` | `#3ED3E4` | Cyan Teal | Code type highlights, [AGENT] log tags, secondary indicators, "Shared in #townhall" label |
| `WarningBrush` | `#FCBB47` | Amber | Warning badges, modified file indicators (M), amber status dots, alert icons, [LOG] warning entries |
| `SuccessBrush` | `#28A745` | Green | Added file indicators (A), active status dots, sync indicators |
| `SurfaceBaseBrush` | `#0A0F19` | Near-Black Navy | Window background, nav bar background, deepest panel base |
| `SurfacePanelBrush` | `#0B121D` | Lighter Navy | Elevated panels (editor, townhall, SC, terminal), code areas, input fields |
| `TextPrimaryBrush` | `#E3E4F4` | Pale Ice Blue-White | All primary text: code content, names, channel names, message text, labels |
| `TextSecondaryBrush` | `#8B95A5` | Muted Blue-Gray | Timestamps, line numbers, placeholder text, auxiliary labels, status bar fields |
| `SeparatorBrush` | `#070C16` | Darkest | 1px panel separators, grid lines |
| `IdleBrush` | `#5A6070` | Muted Slate | Idle status dots, disabled/inactive elements |
| `BusyBrush` | `#FCBB47` | Amber | Busy status dots (same as Warning for visual consistency) |

This palette change is already applied in `docs/DESIGN.md` section 7.

#### Per-Component Color Assignments

Every view must use these tokens by resource key name (`DynamicResource`).
No hardcoded hex values in view code.

**Nav Bar (`NavBar.cs`)**

| Element | Token | Notes |
|---------|-------|-------|
| Background | `SurfaceBaseBrush` | Matches window background |
| Active icon | `PrimaryAccentBrush` | Bright blue when selected |
| Inactive icon | `TextSecondaryBrush` | Muted when not selected |
| Hover overlay | `#12FFFFFF` | 7% white, 24px round rect |
| Separator (right edge) | `SeparatorBrush` | 1px vertical line |

**Explorer / File Tree (`FileTreeView.cs`)**

| Element | Token | Notes |
|---------|-------|-------|
| Panel background | `SurfacePanelBrush` | Slightly lighter than nav bar |
| File/folder text | `TextPrimaryBrush` | Standard readable text |
| Hover row | `#0AFFFFFF` | 4% white highlight |
| Selected row | `#15066ADB` | 8% primary accent tint |
| Folder arrow icon | `TextSecondaryBrush` | Subtle expand/collapse indicator |

**Source Control Panel (`SourceControlPanel.cs`)**

| Element | Token | Notes |
|---------|-------|-------|
| Panel background | `SurfacePanelBrush` | Matches other panels |
| Header text | `TextPrimaryBrush` | "Source Control" label |
| Branch selector bg | `#12FFFFFF` | 7% white dropdown bg |
| Branch text | `TextPrimaryBrush` | Branch name |
| "M" status icon | `WarningBrush` | Amber for modified |
| "A" status icon | `SuccessBrush` | Green for added |
| "D" status icon | `#E05555` | Red for deleted |
| File path text | `TextPrimaryBrush` | File names |
| +/- stage buttons | `TextSecondaryBrush` | Subtle action icons |
| Staged header | `TextSecondaryBrush` | "Staged (N)" label |
| Commit input bg | `#0DFFFFFF` | 5% white input field |
| Commit placeholder | `TextSecondaryBrush` | "Commit message..." |
| "Commit Staged" button bg | `PrimaryAccentBrush` | Blue primary action |
| "Commit Staged" button text | `TextPrimaryBrush` | White on blue |

**Townhall — People Panel (`TownhallPeoplePanel.cs`)**

| Element | Token | Notes |
|---------|-------|-------|
| Panel background | `SurfacePanelBrush` | Matches other panels |
| Header "People" text | `TextPrimaryBrush` | Bold, 13px |
| Bell icon | `TextSecondaryBrush` | Notification icon |
| Avatar circle bg | `PrimaryAccentBrush` | Blue circle with white initials |
| Avatar initials | `TextPrimaryBrush` | White on blue |
| Name text | `TextPrimaryBrush` | Agent/user name |
| Active status dot | `SuccessBrush` | Green circle, 8px |
| Busy status dot | `BusyBrush` | Amber circle, 8px |
| Idle status dot | `IdleBrush` | Gray circle, 8px |
| Warning icon (⚠) | `WarningBrush` | Amber triangle next to name |
| "Relevant Files:" label | `TextSecondaryBrush` | Section divider text |

**Townhall — Channels Panel (`TownhallChannelPanel.cs`)**

| Element | Token | Notes |
|---------|-------|-------|
| Panel background | `SurfacePanelBrush` | Matches people panel |
| Channel name text | `TextPrimaryBrush` | "#channel-name" |
| Active channel highlight | `PrimaryAccentBrush` | Blue background tint or text color |
| Inactive channel | `TextSecondaryBrush` | Muted text |
| Pin icon | `TextSecondaryBrush` | Pin indicator on right |
| Hover row | `#0AFFFFFF` | 4% white highlight |

**Townhall — Chat Panel (`TownhallChatPanel.cs`)**

| Element | Token | Notes |
|---------|-------|-------|
| Panel background | `SurfacePanelBrush` | Matches other panels |
| Sender name | `TextPrimaryBrush` | Bold, 13px |
| Message content | `TextPrimaryBrush` | Standard 14px body text |
| Timestamp | `TextSecondaryBrush` | Right-aligned, 11px |
| Warning message bg | `#15FCBB47` | 8% amber tint background |
| Warning message icon | `WarningBrush` | ⚠ amber triangle |
| Normal message bg | transparent | No background |
| "Relevant Files:" inline | `TextSecondaryBrush` | Agent activity indicator |
| Scrollbar track | `SurfaceBaseBrush` | Dark track |
| Scrollbar thumb | `#20FFFFFF` | 12% white thumb |

**Townhall — Input Area (`TownhallInputArea.cs`)**

| Element | Token | Notes |
|---------|-------|-------|
| Input field bg | `#0DFFFFFF` | 5% white, rounded |
| Placeholder text | `TextSecondaryBrush` | "Message #townhall-main" |
| Input text | `TextPrimaryBrush` | User-typed text |
| "+" attachment button | `TextSecondaryBrush` | Left of input |
| Send button bg | `PrimaryAccentBrush` | Blue circle with arrow icon |
| Send button icon | `TextPrimaryBrush` | White arrow on blue |
| Action buttons (media, code) | `TextSecondaryBrush` | Subtle icons |

**Editor (`EditorView.cs`, `EditorTabBar.cs`)**

| Element | Token | Notes |
|---------|-------|-------|
| Editor background | `SurfacePanelBrush` | Code area background |
| Code text | `TextPrimaryBrush` | Syntax-highlighted code |
| Line numbers | `TextSecondaryBrush` | Gutter |
| Tab bar bg | `SurfaceBaseBrush` | Darker than code area |
| Active tab text | `TextPrimaryBrush` | White |
| Active tab underline | `PrimaryAccentBrush` | 2px blue bottom border |
| Inactive tab text | `TextSecondaryBrush` | Muted |
| "Shared in #townhall" | `SecondaryAccentBrush` | Cyan teal, read-only label |
| Focused file info | `TextSecondaryBrush` | Below code area |
| "diff/edit" indicator | `TextSecondaryBrush` | Static label |
| Indent guide lines | `SeparatorBrush` | Very subtle vertical lines |

**Terminal / Logs (`TerminalPanel.cs`)**

| Element | Token | Notes |
|---------|-------|-------|
| Panel background | `SurfacePanelBrush` | Matches other panels |
| Header "Terminal / Logs" | `TextPrimaryBrush` | Panel title |
| [BUILD] tag text | `TextPrimaryBrush` | White/light for build output |
| [AGENT] tag text | `SecondaryAccentBrush` | Cyan teal for agent state |
| [LOG] tag text | `TextSecondaryBrush` | Muted gray for runtime logs |
| Warning/exception ⚠ | `WarningBrush` | Amber icon prefix |
| Log timestamp | `TextSecondaryBrush` | Per-entry time |
| Terminal text (raw) | `TextPrimaryBrush` | Standard terminal output |
| Control buttons (restart, clear) | `TextSecondaryBrush` | Subtle action icons |

**Status Bar (`StatusBar.cs`)**

| Element | Token | Notes |
|---------|-------|-------|
| Background | `SurfaceBaseBrush` | Darkest, matches nav bar |
| App name "Zaide" | `PrimaryAccentBrush` | Blue, with app icon |
| Field text | `TextSecondaryBrush` | All fields (Ln, Col, lang, project, branch) |
| Field separators | `SeparatorBrush` | │ vertical dividers |
| "powered by" text | `TextSecondaryBrush` | AI model info |

**Unsaved Dialog (`UnsavedDialog.axaml`)**

| Element | Token | Notes |
|---------|-------|-------|
| Dialog background | `SurfacePanelBrush` | Elevated over window |
| Title text | `TextPrimaryBrush` | "Unsaved changes" |
| Body text | `TextPrimaryBrush` | File name and prompt |
| "Save" button bg | `PrimaryAccentBrush` | Blue primary action |
| "Don't Save" button bg | `#15FFFFFF` | 8% white secondary |
| "Cancel" button bg | `#15FFFFFF` | 8% white secondary |
| Button text | `TextPrimaryBrush` | White on all buttons |

#### Implementation

- Update `App.axaml` resource dictionary with all token brushes above
- Update any hardcoded color values in view files to use `DynamicResource`
  token key names
- Verify the overall darkness matches the concept (near-black backgrounds,
  not blue-purple)

#### Token-Migration Checklist (do before marking M0.5 complete)

1. Run `grep -rn '#[0-9A-Fa-f]\{6\}' src/Views/ src/App.axaml` to find all hex literals.
2. Cross-reference against the token table above; any hex that does not
   match a token value or a deliberate transparent variant (e.g., `#22` prefix)
   is a migration candidate.
3. Verify no legacy `#8FA5DE`, `#C2C2E5`, `#142043`, or `#1A2847` values
   remain anywhere in `src/`.
4. Verify every view file references palette tokens by resource key name
   (e.g., `DynamicResource SurfaceBaseBrush`) rather than hardcoded values.
5. Run `dotnet build Zaide.slnx` to confirm no breakage.
6. Capture a screenshot at default window size for visual spot-check.

#### Global Spacing & Rounding Tokens

Define these as shared constants in `App.axaml` resource dictionary so every
view references them by key. No hardcoded spacing or radius values in view code.

| Token Key | Value | Usage |
|-----------|-------|-------|
| `SpacingXxs` | `2px` | Tight gaps: icon-to-text within a single control |
| `SpacingXs` | `4px` | Compact gaps: status dot next to text, badge offsets |
| `SpacingSm` | `8px` | Default element gap: between adjacent controls, list item vertical padding |
| `SpacingMd` | `12px` | Section-internal padding: inside input fields, message body side padding |
| `SpacingLg` | `16px` | Panel inner padding: minimum distance from content to panel edge |
| `SpacingXl` | `20px` | Major section gaps: between people panel and channels panel, between chat and input |
| `SpacingXxl` | `24px` | Top-level layout padding: only used for the main window outer margin |
| `RadiusSm` | `4px` | Small rounded elements: status dots, inline code badges, tiny indicators |
| `RadiusMd` | `8px` | Medium rounded elements: input fields, buttons, hover highlights, file change rows |
| `RadiusLg` | `12px` | Large rounded elements: panel containers, dropdown menus, dialogs |
| `RadiusXl` | `16px` | Extra-large rounded elements: main chat bubbles (if used), oversized containers |
| `RadiusFull` | `9999px` | Full-circle: avatar circles, status dots, send button (circle shape) |

These values derive from DESIGN.md §2 (corner radius 10–14px on containers)
and §5 (8px element gap, 16px panel padding). The token keys above standardize
them across all Refactor 3 views.

#### Per-Component Padding & Rounding

Every view's elements must follow these concrete spacing and radius rules.
Views not listed here inherit from the global tokens above.

**Nav Bar (`NavBar.cs`)**

| Element | Padding | Radius | Notes |
|---------|---------|--------|-------|
| Nav bar container | `0` horizontal, `SpacingSm (4px)` vertical between icons | `0` | Full-height strip, no rounding on the bar itself |
| Icon button | `SpacingSm (8px)` all sides | `RadiusMd (8px)` | 32×32px hit target centered in 40px column |
| Active icon indicator | `0` | `RadiusSm (4px)` | 3px-wide vertical bar on left edge, 20px tall |
| Hover overlay | `0` | `RadiusMd (8px)` | Fills the icon button area |
| Separator (right edge) | `0` | `0` | 1px `SeparatorBrush` vertical line, full height |

**Explorer / File Tree (`FileTreeView.cs`)**

| Element | Padding | Radius | Notes |
|---------|---------|--------|-------|
| Panel inner | `SpacingLg (16px)` top/bottom, `SpacingMd (12px)` left/right | `0` | Panel itself has no rounding (sharp edges against splitter) |
| File/folder row | `SpacingSm (8px)` vertical, `SpacingMd (12px)` left (indented by depth × 16px) | `0` | Rows are flush, no per-row rounding |
| Hover row overlay | Same as row | `RadiusSm (4px)` | Subtle rounded highlight |
| Selected row overlay | Same as row | `RadiusSm (8px)` | Slightly more rounded for emphasis |
| Folder expand arrow | `0` padding, `SpacingXs (4px)` gap to label | `0` | 16px icon, no rounding |
| Tree indent guide | `0` | `0` | 1px vertical `SeparatorBrush` line per depth level |

**Source Control Panel (`SourceControlPanel.cs`)**

| Element | Padding | Radius | Notes |
|---------|---------|--------|-------|
| Panel inner | `SpacingLg (16px)` top/bottom, `SpacingMd (12px)` left/right | `0` | Matches explorer panel |
| "Source Control" header | `SpacingSm (8px)` bottom | `0` | 14px bold text |
| Branch selector | `SpacingSm (8px)` horizontal, `SpacingXs (4px)` vertical | `RadiusMd (8px)` | Dropdown button |
| Branch dropdown list | `SpacingXs (4px)` all sides | `RadiusLg (12px)` | Popup container |
| File change row | `SpacingSm (8px)` vertical, `SpacingMd (12px)` left/right | `0` | Flat row, no rounding |
| File change row hover | Same as row | `RadiusSm (4px)` | Hover highlight |
| Status icon (M/A/D) | `SpacingXs (4px)` gap to file path | `RadiusSm (4px)` | 16×16px icon area |
| Stage/unstage buttons (+/-) | `SpacingXs (4px)` all sides | `RadiusSm (4px)` | 20×20px hit target |
| Staged section header | `SpacingSm (8px)` top/bottom | `0` | Collapsible, 12px text |
| Commit input field | `SpacingMd (12px)` all sides | `RadiusMd (8px)` | Full-width text input |
| "Commit Staged" button | `SpacingSm (8px)` horizontal, `SpacingXs (4px)` vertical | `RadiusMd (8px)` | Primary action button |

**Townhall — People Panel (`TownhallPeoplePanel.cs`)**

| Element | Padding | Radius | Notes |
|---------|---------|--------|-------|
| Panel inner | `SpacingLg (16px)` top, `SpacingMd (12px)` left/right, `0` bottom | `0` | Top-padded, bottom flows into channels |
| "People" header | `SpacingSm (8px)` bottom, `SpacingXs (4px)` between icon and text | `0` | 13px bold + bell icon |
| Person row | `SpacingSm (8px)` vertical, `SpacingMd (12px)` horizontal | `0` | Flat row |
| Person row hover | Same as row | `RadiusSm (4px)` | Hover highlight |
| Avatar circle | `0` padding (text centered inside) | `RadiusFull` | 32×32px circle, `PrimaryAccentBrush` bg |
| Avatar initials | `0` | `0` | 13px `TextPrimaryBrush` centered in circle |
| Status dot | `0` | `RadiusFull` | 8×8px circle, placed bottom-right of avatar |
| Warning icon (⚠) | `SpacingXs (4px)` gap to name | `0` | 14px amber icon |
| "Relevant Files:" label | `SpacingMd (12px)` top, `0` bottom | `0` | 11px `TextSecondaryBrush` |

**Townhall — Channels Panel (`TownhallChannelPanel.cs`)**

| Element | Padding | Radius | Notes |
|---------|---------|--------|-------|
| Panel inner | `SpacingMd (12px)` all sides | `0` | Below people panel, separated by `SpacingXl (20px)` gap |
| Channel row | `SpacingSm (6px)` vertical, `SpacingMd (12px)` horizontal | `RadiusSm (4px)` | Subtle rounded highlight on active |
| Channel row hover | Same as row | `RadiusSm (4px)` | Hover highlight |
| "#" prefix | `SpacingXs (4px)` gap to name text | `0` | Part of same text block |
| Pin icon | `SpacingXs (4px)` gap left, aligned right | `0` | 14px icon |
| Channel name text | `0` padding (inside row padding) | `0` | 13px, `TextPrimaryBrush` active / `TextSecondaryBrush` inactive |

**Townhall — Chat Panel (`TownhallChatPanel.cs`)**

| Element | Padding | Radius | Notes |
|---------|---------|--------|-------|
| Panel inner | `0` (scrollable content fills area) | `0` | No panel-level padding; messages handle their own |
| Message container | `SpacingMd (12px)` vertical, `SpacingLg (16px)` horizontal | `0` | Each message row |
| Warning message container | `SpacingMd (12px)` vertical, `SpacingLg (16px)` horizontal | `RadiusMd (8px)` | Amber-tinted background with rounding |
| Avatar circle | `0` inside | `RadiusFull` | 28×28px (slightly smaller than people panel) |
| Sender name | `SpacingXs (4px)` gap below avatar top, `SpacingXs (4px)` gap to content | `0` | 13px bold |
| Message content | `SpacingMd (12px)` left (indented past avatar) | `0` | 14px body text, wraps |
| Timestamp | `SpacingXs (4px)` top, aligned right | `0` | 11px `TextSecondaryBrush` |
| Warning icon | `SpacingXs (4px)` gap to sender name | `0` | 14px amber |
| "Relevant Files:" inline | `SpacingSm (8px)` top/bottom, `SpacingMd (12px)` left | `RadiusMd (8px)` | Subtle container |
| Scrollbar track | `0` | `0` | 8px wide, `SurfaceBaseBrush` |
| Scrollbar thumb | `0` | `RadiusFull` | 8px wide, rounded ends |

**Townhall — Input Area (`TownhallInputArea.cs`)**

| Element | Padding | Radius | Notes |
|---------|---------|--------|-------|
| Container | `SpacingSm (8px)` all sides | `0` | Input row inside the bottom strip |
| Input field | `SpacingMd (12px)` horizontal, `SpacingSm (8px)` vertical | `RadiusMd (8px)` | Full-width, rounded pill-like shape |
| "+" button | `SpacingSm (8px)` all sides | `RadiusFull` | 28×28px circle, left of input |
| Send button | `SpacingSm (8px)` all sides | `RadiusFull` | 32×32px circle, right of input |
| Action buttons (media, code) | `SpacingXs (4px)` gap between each | `RadiusSm (4px)` | 20×20px icons |

**Editor (`EditorView.cs`, `EditorTabBar.cs`)**

| Element | Padding | Radius | Notes |
|---------|---------|--------|-------|
| Tab bar | `SpacingSm (8px)` vertical, `SpacingMd (12px)` horizontal | `0` | Horizontal strip |
| Tab item | `SpacingMd (12px)` horizontal, `SpacingSm (8px)` vertical | `RadiusMd (8px)` on top only | Top-rounded tab shape |
| Active tab underline | `0` | `0` | 2px `PrimaryAccentBrush` bottom border |
| "Shared in #townhall" label | `SpacingSm (8px)` left of last tab | `0` | Text-only, no background |
| Editor code area | `SpacingMd (12px)` all sides (inside scroll viewer) | `0` | Code starts after line number gutter |
| Line number gutter | `SpacingMd (12px)` right (gap to code) | `0` | Right-aligned text, 40px wide |
| Focused file info bar | `SpacingSm (8px)` vertical, `SpacingMd (12px)` horizontal | `0` | Below code area, above bottom edge |
| Indent guides | `0` | `0` | 1px `SeparatorBrush` vertical lines |

**Terminal / Logs (`TerminalPanel.cs`)**

| Element | Padding | Radius | Notes |
|---------|---------|--------|-------|
| Panel inner | `SpacingMd (12px)` all sides | `0` | Matches other panels |
| Header "Terminal / Logs" | `SpacingSm (8px)` bottom | `0` | 13px bold |
| Log entry | `SpacingXs (4px)` vertical, `SpacingMd (12px)` horizontal | `0` | Flat rows, no per-entry rounding |
| Log tag badge `[BUILD]` | `SpacingXs (2px)` horizontal, `SpacingXxs (2px)` vertical | `RadiusSm (4px)` | 11px text in subtle container |
| Warning ⚠ icon | `SpacingXs (4px)` gap to text | `0` | 14px amber |
| Log timestamp | `SpacingXs (4px)` gap right | `0` | 11px `TextSecondaryBrush` |
| Control buttons (restart, clear) | `SpacingXs (4px)` gap between each | `RadiusSm (4px)` | 20×20px icons |

**Status Bar (`StatusBar.cs`)**

| Element | Padding | Radius | Notes |
|---------|---------|--------|-------|
| Container | `SpacingXs (4px)` vertical, `SpacingMd (12px)` horizontal | `0` | Thin bar, ~24px total height |
| App icon | `SpacingXs (4px)` gap right | `0` | 14px icon |
| Field text | `SpacingXs (4px)` gap to separators | `0` | 12px `TextSecondaryBrush` |
| Field separators (│) | `SpacingSm (8px)` horizontal | `0` | 1px vertical `SeparatorBrush` line |

**Unsaved Dialog (`UnsavedDialog.axaml`)**

| Element | Padding | Radius | Notes |
|---------|---------|--------|-------|
| Dialog outer | `SpacingXxl (24px)` all sides | `RadiusLg (12px)` | Main dialog container |
| Dialog title | `SpacingSm (8px)` bottom | `0` | 14px bold |
| Dialog body text | `SpacingMd (12px)` bottom | `0` | 13px, wraps |
| Button row | `SpacingSm (8px)` gap between buttons | `0` | Horizontal row |
| "Save" button | `SpacingMd (12px)` horizontal, `SpacingSm (8px)` vertical | `RadiusMd (8px)` | Primary action |
| "Don't Save" button | `SpacingMd (12px)` horizontal, `SpacingSm (8px)` vertical | `RadiusMd (8px)` | Secondary |
| "Cancel" button | `SpacingMd (12px)` horizontal, `SpacingSm (8px)` vertical | `RadiusMd (8px)` | Secondary |

#### Rounding Rules Summary

| Context | Radius | Rationale |
|---------|--------|-----------|
| Window corners | None (OS handles) | Native window chrome per DESIGN.md §3 |
| Top-level panels (explorer, townhall, editor, terminal) | None (sharp edges) | Panels are flush grid cells; separators between them, not rounded |
| Elevated surfaces (dropdowns, popups, dialogs) | `RadiusLg (12px)` | Floating above the grid, need visual separation |
| Input fields | `RadiusMd (8px)` | Interactive, inviting touch target |
| Buttons (rectangular) | `RadiusMd (8px)` | Consistent with inputs |
| Buttons (circular — send, "+") | `RadiusFull` | Distinct circular action buttons |
| Tab items (top) | `RadiusMd (8px)` top corners only | Classic tab shape, flat bottom |
| Rows (file changes, channels, log entries) | None (sharp) | Flat list items, rounding only on hover |
| Row hover highlights | `RadiusSm (4px)` | Subtle rounded highlight, not a full container |
| Selected row highlights | `RadiusSm (8px)` | Slightly more prominent than hover |
| Avatar circles | `RadiusFull` | Perfect circles |
| Status dots | `RadiusFull` | Perfect circles |
| Tag badges (`[BUILD]`, etc.) | `RadiusSm (4px)` | Small inline badges |
| Scrollbar thumbs | `RadiusFull` | Rounded ends |
| Warning message containers | `RadiusMd (8px)` | Distinct from normal messages |

### M1: Main Window Layout Transition (In Progress)

Rewrite the layout in `src/MainWindow.axaml.cs` (`BuildLayout` method) so
the shell grid/topology becomes (`src/MainWindow.axaml` stays minimal per
DESIGN.md §1):

- far-left: icon-only nav bar (~40px)
- left: single-slot content panel driven by nav mode (Explorer or Source
  Control)
- center: Townhall
- right: editor
- bottom: terminal/logs (spanning content area under center + right only)
- status bar: thin bar at the very bottom, full width

#### MainWindowViewModel Changes

Add left-panel mode state to `MainWindowViewModel`:

- Define `LeftPanelMode` enum with values `Explorer` and `SourceControl`
- Add `_leftPanelMode` private field (default `Explorer`)
- Add `LeftPanelMode` reactive property
- Add `IsExplorerMode` and `IsSourceControlMode` computed properties
- Add `SwitchToExplorerCommand` and `SwitchToSourceControlCommand` reactive commands
- Wire commands to update `LeftPanelMode`

#### Tests

Add tests to `MainWindowViewModelTests`:

- `InitialState_LeftPanelModeIsExplorer` — verify default is Explorer
- `SwitchToSourceControl_SetsModeToSourceControl` — verify switching to SC
- `SwitchToExplorer_SetsModeToExplorer` — verify switching back to Explorer

#### Layout Implementation

- Add a narrow vertical nav bar on the far left with icons for Explorer
  (active) and Source Control. This provides the navigation affordance shown
  in the concept and reserves the structural slot for future panel switching.
- Keep the file tree in the left content panel in Explorer mode.
- Add Source Control mode in the same left-panel slot (full SC panel implemented
  in M6; M1 sets up the slot structure with a placeholder).
- Move the editor from center to right.
- Replace the current right-side placeholder model with a real center
  Townhall surface (initially a simple container; M2/M3 add content).
- Change the bottom area to span under center + right only, not the full
  window width. The left panel extends full height.
- Reserve the status bar slot at the very bottom (implemented in M6).

Important constraint:

- the center column must be visually dominant over the right editor column
- all panel boundaries must be drag-resizable via GridSplitter

Safe implementation rule:

- preserve as much existing editor/file-tree/terminal wiring as possible while
  changing only their position and composition
- `src/MainWindow.axaml` must remain minimal (just the Window tag). All layout
  is built in C# in `MainWindow.axaml.cs`. Do NOT add layout XAML.

### M2: Townhall Domain/ViewModel Foundation

Add the domain and view-model structure so Townhall is not just a label.

Models needed:

- `Channel` — represents a named channel (e.g., "townhall-main", "ai-status",
  "codebase-refactoring", "log-review"). Fields: id, name, isPinned, isActive.
- `TownhallMessage` — a single message in a channel. Fields: id, senderId,
  senderName, senderAvatar (string path or resource key), content,
  timestamp (DateTimeOffset), type (Normal, Warning, System). Warning messages
  have an amber alert icon. Reactions and audio are explicitly deferred.
- `WorkspaceAgent` — represents a person or agent in the workspace. Fields:
  id, name, avatar (string path or resource key), role (user/agent),
  status (active/busy/idle), hasWarning (boolean). Warning badges show an
  amber triangle icon next to the agent name.
- `TownhallState` — current session state. Holds: list of channels, active
  channel id, list of messages for active channel, list of agents, current
  draft text.

ViewModels needed:

- `TownhallViewModel` — exposes observable properties for channels,
  `TownhallMessage` items, `WorkspaceAgent` items, and draft. Commands:
  selectChannel, sendMessage. Uses reactive bindings per project conventions.

Register `TownhallViewModel` in `Program.cs` DI container so it is available
when M3 wires it into the layout.

This milestone is about making the center column feel like a real workspace.
It is not about building final agent infrastructure.

### M3: Townhall View Integration

Add a real Townhall view into the center column with its internal structure.

The Townhall view should contain:

- **People/Channels vertical sidebar** (left, ~140px wide, full height):
  A tall narrow column divided into two sections by a horizontal separator.

  - **People section** (top half): vertical list of agents/users with
    avatar placeholder (colored circle with initials), name, and status
    label (active/busy/idle). Status is color-coded: green dot for active,
    amber dot for busy, gray dot for idle. Agents with `hasWarning` show
    an amber triangle icon. Header shows "People" with a notification bell
    icon. Takes roughly the top 40–50% of the sidebar.

  - **Channels section** (bottom half): vertical list of channel names
    prefixed with "#". Active channel is highlighted. Pinned channels show
    a pin icon. Tapping a channel switches the chat view. Takes roughly
    the bottom 50–60% of the sidebar. Rendered as a simple ItemsControl
    bound to the channel list.

  The sidebar is a single vertical column — NOT a horizontal strip.
  People and Channels are stacked top-to-bottom within it.

- **Chat message area** (right of sidebar, fills remaining width):
  scrollable list of messages. Each message shows: avatar (colored circle
  with initials), sender name with status badge, content text, and
  timestamp. Warning-type messages show an amber alert icon. Messages are
  displayed newest-at-bottom. A "Relevant Files:" section may appear
  inline showing which agents are currently active.
  No reactions, no audio indicators in Refactor 3.

- **Input area** (bottom of chat area, full width below messages): text
  input with placeholder text ("Message #townhall-main"), attachment
  button (+ icon), and send button (arrow icon). Additional input action
  buttons (media, code snippet) may appear as icons next to the input
  field. Spans the full chat area width (not under the sidebar).

Priority order:

1. visible center-stage workspace with people, channels, chat, and input
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
- below the code area, show a "focused file" info line with the current
  file name and a "diff/edit" indicator. This is a static read-only label.

The Focused File section (file name dropdown + Open Diff button) shown in the
concept is explicitly deferred to a later phase. Only the static label is
implemented here.

This milestone is successful if users can still code comfortably while the UI no
longer reads as editor-first.

### M5: Terminal/Logs Alignment and Categorization

Preserve the bottom area as a shared runtime/log surface.

Requirements:

- terminal toggle still works
- restart/clear/state controls still work
- the bottom area spans the content area width (under center + right columns,
  not under the left panel — matching the concept layout)
- the panel header reads "Terminal / Logs"
- log output is categorized with colored tag prefixes:
  - `[BUILD]` — build system output (white/light text)
  - `[AGENT]` — agent task state updates (cyan/teal text)
  - `[LOG]` — runtime logs and exceptions (muted gray text)
- warning/exception entries show a warning icon (⚠) prefix
- the area should read as shared operational output, not as editor-owned space

Implementation:

- Add `LogEntry` model with fields: id, category (Build/Agent/Log), content,
  timestamp, hasWarning.
- Add `LogCategorizer` service that parses terminal output lines and assigns
  categories based on content heuristics (e.g., lines starting with "[BUILD]"
  get Build category).
- Update `TerminalPanel` to render categorized entries with appropriate color
  tags and warning icons.
- The categorizer is best-effort; uncategorized lines default to Log category.

### M6: Source Control Panel and Status Bar

Implement the Source Control panel and status bar to match the concept.

#### Source Control Panel

The Source Control panel replaces the M1 placeholder with a real (minimal)
panel structure. It uses static/demo data — not wired to real git — but
the visual structure matches the concept.

Structure:

- **Header**: "Source Control" label
- **Branch selector**: dropdown showing "Get - Master" (or current branch name).
  Clicking shows a dropdown list of branches. No actual git branch switching.
- **Changes tab**: Shows modified (M) and added (A) files with status icons.
  Each file entry shows the status letter (color-coded: green for A, amber for M)
  and the file path. +/- buttons next to each file for staging/unstaging
  (visual only, no real git operations).
- **Staged section**: Shows files that are staged. Collapsible section header
  with file count.
- **Commit message area**: Text input with placeholder "Commit message..."
  and a "Commit Staged" button. Button is styled with primary accent color.
  No real commit logic.

Models needed:

- `GitBranch` — fields: name, isCurrent.
- `FileChange` — fields: filePath, changeType (Added/Modified/Deleted),
  isStaged.
- `SourceControlState` — holds: list of branches, current branch, list of
  unstaged changes, list of staged changes, commit message draft.

ViewModel:

- `SourceControlViewModel` — exposes observable properties for branches,
  changes, staged items, and draft commit message. Commands:
  selectBranch, stageFile, unstageFile, commit (visual only).
  All commands update UI state but do not execute real git operations.

#### Status Bar

A thin bar at the very bottom of the window, full width.

Fields (left to right):

- App name: "Zaide" (with app icon)
- Cursor position: "Ln {line}, Col {col}"
- Language: current file language (e.g., "C#")
- Project: current project name (e.g., "Aero" or the actual project)
- Branch: current git branch (e.g., "master")
- AI model: "powered by {model name}" (e.g., "powered by Avisnis 12")

Implementation:

- Add `StatusBar` view as a horizontal panel at the bottom of the window
  grid, below the terminal/logs row.
- Add reactive `CaretLine` and `CaretColumn` properties to
  `EditorViewModel` (backed by AvaloniaEdit's caret offset, converted to
  1-based line/column).
- Update `EditorView` to project the `_textEditor.TextArea.Caret.CaretChanged`
  event back to the ViewModel's `CaretLine`/`CaretColumn` properties. This
  is a View→ViewModel push, matching the existing event-based sync pattern.
- Add `SourceControlViewModel` and `TownhallViewModel` dependencies to
  `MainWindowViewModel` so the status bar can read branch info and the
  layout can wire the SC panel.
- Expose project name from `Workspace` (e.g., the folder name) so the
  status bar can display it.
- Register `SourceControlViewModel` and `TownhallViewModel` in
  `Program.cs` DI container so they are available to `MainWindowViewModel`.
- The AI model label is a static string for now; real model integration
  is deferred.

### M7: Regression Sweep And Doc Sync

After the layout transition is complete:

- rerun build and tests
- manually verify file tree workflows
- manually verify editor workflows
- manually verify terminal workflows
- confirm Townhall is the visual center with people, channels, chat, and
  input sections visible
- confirm the nav bar renders correctly on the far left
- confirm the Source Control panel shows branch selector, changes, staged,
  and commit input
- confirm the editor shows the townhall link indicator
- confirm the bottom panel spans under center + right only
- confirm categorized log output appears in the terminal panel
- confirm the status bar shows all expected fields
- confirm root docs and refactor docs still match reality

---

## Manual Verification Checklist

- open app: Townhall is visually central
- nav bar icons are visible on the far left
- file tree still opens folders and files correctly
- Source Control panel is visible when Source Control mode is selected
- Source Control shows branch selector, change list, staged section, commit input
- Townhall shows vertical sidebar with People (top) and Channels (bottom) on the left, chat messages and input on the right
- people list shows avatars (colored circles with initials), names, and status dots
- agents with warnings show amber triangle icons
- switching channels changes the displayed messages
- typing and sending a message adds it to the chat
- warning-type messages show amber alert icons
- editor tabs still open, switch, save, and close correctly
- editor header shows "Shared in #townhall" indicator
- editor shows focused file info below the code area
- terminal still opens and behaves correctly
- terminal shows categorized [BUILD], [AGENT], [LOG] entries with colored tags
- bottom panel spans under center + right, not under the left panel
- status bar shows "Zaide", cursor position, language, project, branch, AI model
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
- Full git integration (branching, committing, real repo operations)
- Focused File section with dropdown and diff button (below editor)
- Editor-to-Townhall bidirectional integration (file highlighting, diff sync)
- Message reactions, audio indicators
- Agent avatars as real images (Refactor 3 uses colored circles with initials)
- Multi-channel creation/management UI
- Channel-level permissions or access control
- Real AI model integration in status bar
- Log search/filter functionality

Those concerns are real, but they belong to a later UX/system phase.
They are not reasons to stop Refactor 3.

---

## Interaction Contracts

These contracts resolve ambiguities identified in the plan audit.

### Nav Bar Interaction

- Explorer icon is active by default on first launch.
- Clicking the Explorer icon shows the file tree in the left content panel.
  If it is already showing, clicking has no effect (no toggle-off).
- Clicking the Source Control icon shows the SC panel in the left
  content panel.
- Only one left-panel mode is visible at a time; the other is hidden
  (not collapsed to zero, just replaced in the same layout slot).
- Clicking the currently active icon does nothing (no empty state).

### Source Control Panel

- The panel renders a branch selector, change list with status icons,
  staged section, and commit message input with "Commit Staged" button.
- All data is static/demo: pre-populated file changes and a fixed branch
  list. No real git operations are executed.
- The branch selector shows a dropdown but does not switch branches.
- The "Commit Staged" button shows a brief visual feedback (e.g., button
  press animation) but does not create a real commit.
- The +/- buttons next to files visually move items between staged and
  unstaged sections but do not run `git add` or `git reset`.
- The purpose is to reserve layout space and visual structure so the SC
  panel matches the concept and does not need reshuffling when real git
  behavior arrives later.

### Townhall People Panel

- Each person/agent entry shows a colored circle with initials (avatar
  placeholder), the name, and a status label with colored dot.
- Status colors: green for active, amber for busy, gray for idle.
- Agents with `hasWarning = true` show an amber triangle icon (⚠) next
  to their name.
- The people panel header shows "People" with a notification bell icon.
- A "Relevant Files:" label may appear below the list to indicate which
  agents are currently working on files.

### Townhall Channel Panel

- Channel names are prefixed with "#".
- The active channel is visually highlighted.
- Pinned channels show a pin icon to the right of the name.
- Clicking a channel switches the chat view to that channel's messages.
- Channels are listed vertically below the people panel.

### Townhall Message Ordering

- Messages are displayed newest-at-bottom (chronological, ascending).
- The message list is append-only within a session; messages are not
  reordered or inserted mid-list.
- Channel switching loads the message list for the selected channel;
  the previous channel's messages are replaced (no cross-channel merge).
- Draft text is per-channel; switching channels saves and restores draft.
- In-memory model is sufficient for Refactor 3; no persistence.
- Warning-type messages display with an amber alert icon and slightly
  different styling (amber-tinted background or border).

### Townhall Message Input

- The input area shows placeholder text matching the active channel
  (e.g., "Message #townhall-main").
- A "+" button to the left of the input field serves as an attachment
  affordance (visual only in Refactor 3).
- A send button (arrow icon) to the right sends the message.
- Additional icon buttons (media, code snippet) may appear between the
  input and send button.
- Pressing Enter sends the message; Shift+Enter inserts a newline.

### Editor "Shared in #townhall" Label Placement

- The label appears in the editor tab bar area, to the right of the active
  tab's close button. It is a plain text read-only label styled with
  TextSecondary color.
- It is static text (not a clickable link, not a toggle) in Refactor 3.
- If no tab is open, the label is hidden.

### Editor Focused File Info

- Below the code area, a "focused file" line shows the current file name
  and a "diff/edit" indicator.
- This is a static read-only label; no dropdown, no diff button in
  Refactor 3.

### Terminal Log Categorization

- Log entries are parsed and assigned categories: Build, Agent, or Log.
- Each category has a distinct color tag: [BUILD] in white, [AGENT] in
  cyan/teal, [LOG] in muted gray.
- Warning/exception entries show a ⚠ icon prefix.
- The categorizer is heuristic-based; unrecognized lines default to Log.
- Log entries include a timestamp.

### Panel Resize (GridSplitter)

- Every column and row boundary in the main window grid has a `GridSplitter`
  control for drag-resize.
- Boundaries that must be resizable:
  - Left panel ↔ Townhall sidebar (horizontal drag)
  - Townhall sidebar ↔ Chat area (horizontal drag)
  - Townhall ↔ Editor (horizontal drag)
  - Content area ↔ Terminal/Logs (vertical drag)
  - People section ↔ Channels section within sidebar (vertical drag)
- Minimum panel sizes: left panel ≥ 180px, editor ≥ 240px, terminal ≥ 120px,
  townhall sidebar ≥ 100px, chat area ≥ 300px, people section ≥ 80px,
  channels section ≥ 80px.
- Dragging a splitter adjusts the adjacent `ColumnDefinition` or
  `RowDefinition` Width/Height (star values). No panel collapses to zero
  unless the user explicitly drags it past the minimum.
- Splitter visual style: thin (4px wide/tall), transparent by default,
  subtle highlight on hover. Matches the 1px separator aesthetic.

### Status Bar

- The status bar is a thin horizontal strip at the very bottom of the
  window, spanning full width.
- Fields are laid out left-to-right: app name, cursor position, language,
  project, branch, AI model.
- Field separators use a subtle divider (│ character or 1px vertical line).
- The app name "Zaide" may include a small app icon to its left.
- Cursor position updates reactively as the user moves in the editor.
- Language updates when the active tab changes.

---

## Failure Modes To Avoid

- Moving panels but keeping the editor visually dominant
- Making Townhall large in geometry but weak in hierarchy
- Breaking file tree resizing or file opening
- Breaking editor save/tab behavior
- Treating Townhall as a placeholder again
- Letting future customization ideas delay the current pivot
- Omitting the nav bar and then needing a second layout pass to add it
- Implementing a flat Townhall (just thread + input) without the people
  and channels sub-panels that give it visual density and coherence
- Making the Source Control panel a simple empty placeholder when the
  concept clearly shows a structured panel with branch selector, changes,
  staging, and commit input
- Forgetting the status bar — it grounds the window as a real application
- Using "Aero Studio" or any name other than "Zaide" in the implementation
- Hard-coding panel sizes without GridSplitter — all panel boundaries must
  be drag-resizable by the user

---

## Exit Conditions

- Nav bar visible on the far left
- Left column supports mode-switched Explorer and Source Control views
- Source Control panel shows branch selector, change list, staged section,
  commit input (with demo/static data)
- Main window layout is nav bar | left-panel mode slot (Explorer/SC) | townhall | editor
- Townhall shows vertical sidebar (People top, Channels bottom) on the left, chat area and input on the right
- Townhall is the clear primary attention surface
- Editor remains always visible and usable with townhall link indicator
- Editor shows focused file info below the code area
- File tree remains functional
- Terminal/logs remain functional (content-area width only) with categorized output
- Status bar shows app name, cursor position, language, project, branch, AI model
- All panel boundaries are drag-resizable (left↔townhall, townhall↔editor, content↔terminal, people↔channels)
- No panel collapses to zero unless the user drags it there
- Build passes
- Tests pass
- Docs match the new reality
- App name is "Zaide" everywhere

---

## Milestone Testing Expectations

These expectations define minimum validation depth per milestone.

- **M1 (layout transition):**
  - Build passes.
  - Manual layout validation against LC-1..LC-12 at default width and 960 px.
  - Confirm nav bar mode switching (Explorer ↔ Source Control).
  - Unit tests verify `MainWindowViewModel.LeftPanelMode` initial state and switching.

- **M2 (Townhall models/viewmodel):**
  - Add/extend unit tests for `TownhallViewModel` and state behavior:
    - select channel updates active channel
    - send message appends newest-at-bottom
    - per-channel draft save/restore on channel switch

- **M3 (Townhall view integration):**
  - Build passes with no binding/runtime errors in output.
  - Manual verify people/channels/chat/input render and resize behavior.
  - Verify avatar circles, status dots, and warning icons render correctly.

- **M4 (editor adaptation):**
  - Regression-check tab open/switch/save/close workflows.
  - Verify "Shared in #townhall" visibility rules (shown only with active tab).
  - Verify focused file info line below code area.

- **M5 (terminal/log alignment):**
  - Regression-check terminal toggle/restart/clear/state controls.
  - Verify terminal panel span under center+right only.
  - Verify [BUILD], [AGENT], [LOG] categorized tags appear with correct colors.
  - Verify warning entries show ⚠ icon.

- **M6 (Source Control panel and status bar):**
  - Verify SC panel renders in left slot when SC mode is active.
  - Verify branch selector dropdown opens and shows branch list.
  - Verify change list shows file entries with status icons.
  - Verify staged section shows staged files.
  - Verify commit input accepts text and "Commit Staged" button is visible.
  - Verify status bar shows all expected fields.
  - Verify cursor position updates reactively.

- **M7 (final sweep):**
  - `dotnet build Zaide.slnx`
  - `dotnet test Zaide.slnx --no-build`
  - Manual checklist fully signed off with artifacts.

## Follow-Up After Refactor 3

After this refactor is stable, the next work can continue with:

- richer Townhall behavior
- dedicated agent surfaces
- agent routing
- Full git integration (branching, staging, committing with real repo)
- Focused File section with dropdown and diff button below the editor
- Editor ↔ Townhall bidirectional integration (agent highlights files, user
  diffs from thread context)
- Message reactions and rich content
- Real agent avatars (images instead of initials)
- Real AI model integration
- Log search/filter functionality
- Status bar customization and notifications
- later workspace/panel/window switching to address layout density concerns

Refactor 3 is the pivot.
It is not the end state.

---

*Last updated: 2026-07-05*