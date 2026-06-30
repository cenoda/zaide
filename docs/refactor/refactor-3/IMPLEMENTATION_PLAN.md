# Refactor 3: Townhall-Centric Layout — Implementation Plan

## Pre-Implementation Verification

- [ ] Current build succeeds: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] Current layout matches `docs/architecture/OVERVIEW.md` (Tree | Editor | AgentArea + Bottom)
- [ ] **BLOCKED on refactor-2**: refactor-2 (`docs/refactor/refactor-2/`) must be fully implemented and all `TOFIX.md` items resolved before starting refactor-3. The layer boundary cleanup in refactor-2 is a prerequisite — starting refactor-3 without it risks coupling Phase 4 work to the current leaky boundaries.
- [ ] No new NuGet packages needed for milestones M1–M4
- [ ] Avalonia 12 tab control API verified (for Townhall tab switching)
- [ ] ReactiveUI tab activation pattern verified (`ObservableCollection` + `ActiveTab` pattern)

## Scope

**Goal:**
Restructure the main window layout and introduce the Townhall subsystem. This refactor has two halves:

1. **Layout swap** — Move Editor to the left, File Tree to the right, and insert Townhall as the center panel (replacing the Agent Area placeholder).
2. **Townhall foundation** — Introduce the Townhall data model and ViewModel so the center panel is a real agent conversation transcript (not a placeholder), with support for both group (Townhall) and 1:1 (Agent DM) chat views.

This refactor also kickstarts **Phase 4 (Townhall)** from the roadmap — the Townhall view becomes the center of attention, not a placeholder.

**Current layout (ref):**
```
┌──────────┬──────────┬──────────────────┐
│ TreeView │  Editor  │  Agent Area      │
│ (col 0)  │ (col 2)  │  (col 3)         │  ← placeholder
├──────────┴──────────┴──────────────────┤
│ Terminal (bottom, col 0-3 span)        │
└────────────────────────────────────────┘
```

**Target layout:**
```
┌──────────┬────────────────┬────────────┐
│  Editor  │   Townhall     │  FileTree  │
│ (col 0)  │ (col 2/center) │ (col 3)    │
│          │  ═══════════   │            │
│          │  Townhall tab  │            │
│          │  ── or ──      │            │
│          │  Agent DM tab  │            │
├──────────┴────────────────┴────────────┤
│ Terminal / Agent Log (bottom, col 0-3) │
└────────────────────────────────────────┘
```

**Boundaries (NOT in scope):**
- ❌ No actual AI agent integration — this refactor builds the UI + data model, not the agent network
- ❌ No agent-to-agent routing (Phase 6 concern)
- ❌ No Git integration (Phase 7 concern)
- ❌ No actual agent process management
- ❌ No file tree rework — TreeView moves but keeps its exact behavior
- ❌ No editor rework — Editor moves but keeps its exact behavior

## Milestones (Incremental)

| Milestone | Description | Test | Status |
|-----------|-------------|------|--------|
| M0 | **Entry gate**: build + tests pass, verify current layout, commit current state | `dotnet build && dotnet test` — zero failures | ⬜ |
| M1 | **Layout swap**: Reorder `MainWindow.axaml.cs` columns from `Tree | Splitter | Editor | AgentArea` to `Editor | Splitter | Townhall | FileTree`. FileTree moves from col 0 → col 3, Editor moves from col 2 → col 0. AgentArea placeholder replaced with Townhall placeholder. Column widths: Editor=star min 300px, Splitter=4px, Townhall=3star, Tree=260px min 200 max 400. Bottom panel still spans all 4 columns. | Visual: open window → Editor on left, empty center, FileTree on right; Ctrl+` toggles terminal; open folder → tree populates on right; open file → editor appears on left | ⬜ |
| M2 | **Agent data models**: Create `Models/AgentIdentity.cs` and `Models/AgentMessage.cs`. Plain models — no ReactiveObject, no UI dependency. | `AgentIdentityTests`, `AgentMessageTests` — construct, properties round-trip | ⬜ |
| M3 | **TownhallViewModel + wiring**: Create `ViewModels/TownhallViewModel.cs`. Wire into `MainWindowViewModel`. Register in DI (`Program.cs`). | `TownhallViewModelTests` — add message, switch channels, send command enqueues message | ⬜ |
| M4 | **TownhallView (center panel)**: Create `Views/TownhallView.cs` Avalonia UserControl. Tab control with Townhall + Chat tabs. Scrolling message transcript, message input + send. Wire to TownhallViewModel. | TownhallView renders in center on launch; type message → appears in transcript; switch tabs → layout stays stable | ⬜ |
| M5 | **Agent DM tab**: Create `Views/AgentChatView.cs` — 1:1 conversation panel. Agent selector, DM transcript, message input. Wire to TownhallViewModel. | Select agent → type message → appears in DM view only; switch back to Townhall → DM messages not visible in group view | ⬜ |
| M6 | **Update architecture docs**: Update `docs/architecture/OVERVIEW.md` with new layout. Update `docs/roadmap/PHASES.md` — mark Phase 4 items in progress. Create `docs/refactor/refactor-3/TOFIX.md`. | `docs/` files render correctly | ⬜ |
| M7 | **Regression sweep**: Full manual regression. All existing tests pass. No behavioral changes to editor, file tree, or terminal. | See manual test matrix below | ⬜ |

## Detailed Milestone Plans

### M1: Layout Swap

**File to modify:** `src/MainWindow.axaml.cs`

**Column definition changes (in `BuildLayout()`):**

```csharp
// BEFORE (current):
// col 0: TreeView (260px, min 180, max 500)
// col 1: GridSplitter (4px)
// col 2: Editor (star)
// col 3: AgentArea (320px)

// AFTER (target):
new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 300 },  // col 0: Editor
new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) },                  // col 1: Splitter
new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) },                    // col 2: Townhall
new ColumnDefinition { Width = new GridLength(260), MinWidth = 200, MaxWidth = 400 }     // col 3: FileTree
```

**Specific changes:**

1. **Editor (col 0):** Move the center panel construction (editor tab bar + editor view + welcome text) from `Grid.SetColumn(center, 2)` → `Grid.SetColumn(center, 0)`. Move `grid.Children.Add(center)` to be added before the splitter.

2. **GridSplitter (col 1):** Already col 1 — no `Grid.SetColumn` change. But move the splitter `grid.Children.Add(...)` call to be added between editor and townhall.

3. **Townhall (col 2):** Replace `BuildPanel("Agent Area", "DeepBase", 1, 0, 0, 0)` with `BuildPanel("Townhall", "DeepBase", 0, 0, 1, 0)`. Change `Grid.SetColumn(agentArea, 3)` → `Grid.SetColumn(townhall, 2)`. The real TownhallView replaces this in M4.

4. **FileTree (col 3):** Change `Grid.SetColumn(sidebar, 0)` → `Grid.SetColumn(sidebar, 3)`. Move `grid.Children.Add(sidebar)` to be added last.

5. **Bottom panel span:** `Grid.SetColumnSpan(bottomPanel, 4)` — already spans all 4, no change.

6. **Margin adjustments:** The AgentArea had margin `(1, 0, 0, 0)` (left border). The new Townhall should have margin `(0, 0, 1, 0)` (right border) since it's now left of the tree. The Editor should have no margin (it's the leftmost panel). The FileTree should have no margin (it's the rightmost panel).

**M1 exit checks:**
- [ ] Window renders with Editor on left, empty center, FileTree on right
- [ ] Ctrl+` toggles terminal at the bottom
- [ ] Open folder (Ctrl+O) → tree populates on the right
- [ ] Click file in tree → editor opens on the left
- [ ] Edit file → dirty flag shows on tab
- [ ] Save (Ctrl+S) → dirty flag clears
- [ ] File tree context menus still work
- [ ] All grid splitters work (left between Editor/Townhall, bottom for terminal)
- [ ] No visual regression — same themes, same colors, same fonts

### M2: Agent Data Models

**New files:**
- `src/Models/AgentIdentity.cs`
- `src/Models/AgentMessage.cs`

**AgentIdentity.cs:**
```csharp
namespace Zaide.Models;

public sealed class AgentIdentity
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Color { get; }       // hex color for UI bubble tint
    public string ModelProvider { get; } // e.g., "Claude", "Codex", "GPT"

    public AgentIdentity(string id, string displayName, string color, string modelProvider)
    {
        Id = id;
        DisplayName = displayName;
        Color = color;
        ModelProvider = modelProvider;
    }
}
```

**AgentMessage.cs:**
```csharp
namespace Zaide.Models;

public enum MessageChannel
{
    Townhall,
    DirectMessage
}

public sealed class AgentMessage
{
    public Guid Id { get; }
    public string SenderId { get; }        // AgentIdentity.Id, or "user"
    public string Content { get; }
    public DateTime Timestamp { get; }
    public MessageChannel Channel { get; }
    public string? RecipientId { get; }    // null for Townhall, agent ID for DM

    public AgentMessage(
        string senderId,
        string content,
        MessageChannel channel,
        string? recipientId = null)
    {
        Id = Guid.NewGuid();
        SenderId = senderId;
        Content = content;
        Timestamp = DateTime.UtcNow;
        Channel = channel;
        RecipientId = recipientId;
    }
}
```

**New tests:**
- `tests/Zaide.Tests/Models/AgentIdentityTests.cs`:
  - `Constructor_SetsProperties`
  - `TwoInstances_WithSameId_AreNotSameReference`
- `tests/Zaide.Tests/Models/AgentMessageTests.cs`:
  - `Constructor_SetsProperties`
  - `Constructor_TownhallChannel_RecipientIdIsNull`
  - `Constructor_DirectMessage_SetsRecipientId`
  - `TwoInstances_HaveDifferentIds`

### M3: TownhallViewModel + Wiring

**New file:** `src/ViewModels/TownhallViewModel.cs`

**TownhallViewModel API:**
```csharp
namespace Zaide.ViewModels;

public class TownhallViewModel : ReactiveObject
{
    // Message collections
    public ObservableCollection<AgentMessage> Messages { get; }
    public ObservableCollection<AgentMessage> TownhallMessages { get; }
    public ObservableCollection<AgentMessage> DirectMessages { get; }

    // Channel switching
    private bool _isTownhallActive = true;
    public bool IsTownhallActive
    {
        get => _isTownhallActive;
        set => this.RaiseAndSetIfChanged(ref _isTownhallActive, value);
    }

    // DM recipient selection
    private AgentIdentity? _selectedDmRecipient;
    public AgentIdentity? SelectedDmRecipient
    {
        get => _selectedDmRecipient;
        set => this.RaiseAndSetIfChanged(ref _selectedDmRecipient, value);
    }

    // Available agents for DM
    public ObservableCollection<AgentIdentity> AvailableAgents { get; }

    // Draft message
    private string _draftMessage = "";
    public string DraftMessage
    {
        get => _draftMessage;
        set => this.RaiseAndSetIfChanged(ref _draftMessage, value);
    }

    // Commands
    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; }
    public ReactiveCommand<Unit, Unit> SwitchToTownhallCommand { get; }
    public ReactiveCommand<AgentIdentity, Unit> SwitchToDmCommand { get; }

    public TownhallViewModel()
    {
        Messages = new ObservableCollection<AgentMessage>();
        TownhallMessages = new ObservableCollection<AgentMessage>();
        DirectMessages = new ObservableCollection<AgentMessage>();
        AvailableAgents = new ObservableCollection<AgentIdentity>
        {
            new("claude", "Claude",  "#7B61FF", "Claude"),
            new("codex",  "Codex",   "#00BFFF", "Codex"),
            new("gpt",    "GPT-4",   "#00C853", "GPT"),
        };

        SendMessageCommand = ReactiveCommand.Create(SendMessage);
        SwitchToTownhallCommand = ReactiveCommand.Create(() => IsTownhallActive = true);
        SwitchToDmCommand = ReactiveCommand.Create<AgentIdentity>(agent =>
        {
            SelectedDmRecipient = agent;
            IsTownhallActive = false;
        });

        Messages.CollectionChanged += (_, _) => RebuildFilteredCollections();
    }

    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(DraftMessage)) return;
        var channel = IsTownhallActive ? MessageChannel.Townhall : MessageChannel.DirectMessage;
        var msg = new AgentMessage("user", DraftMessage.Trim(), channel,
            IsTownhallActive ? null : SelectedDmRecipient?.Id);
        Messages.Add(msg);
        DraftMessage = "";
    }

    private void RebuildFilteredCollections()
    {
        TownhallMessages.Clear();
        DirectMessages.Clear();
        foreach (var msg in Messages)
        {
            if (msg.Channel == MessageChannel.Townhall)
                TownhallMessages.Add(msg);
            else
                DirectMessages.Add(msg);
        }
    }
}
```

**MainWindowViewModel changes:**
```csharp
// ADD property:
public TownhallViewModel TownhallViewModel { get; }

// UPDATE constructor:
public MainWindowViewModel(
    FileTreeViewModel fileTreeViewModel,
    EditorTabViewModel editorTabViewModel,
    TerminalViewModel terminalViewModel,
    TownhallViewModel townhallViewModel)  // NEW param
{
    TownhallViewModel = townhallViewModel;
    // ... rest unchanged
}
```

**Program.cs changes:**
```csharp
// ADD registration before MainWindowViewModel:
services.AddSingleton<TownhallViewModel>();
```

**New tests:** `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs`:
- `Constructor_InitializesCollections`
- `SendMessage_AddsToMessages`
- `SendMessage_EmptyDraft_DoesNotAdd`
- `SendMessage_TownhallChannel_FiltersCorrectly`
- `SendMessage_DmChannel_FiltersCorrectly`
- `SwitchToTownhall_SetsIsTownhallActiveTrue`
- `SwitchToDm_SetsIsTownhallActiveFalseAndSelectsRecipient`

### M4: TownhallView (Center Panel)

**New file:** `src/Views/TownhallView.cs` — Avalonia `UserControl`, C# code-behind (no XAML, per DESIGN.md policy).

**Layout structure:**
```
┌────────────────────────────────────────────────┐
│  [Townhall]  [Chat]           ← Tab bar        │
├────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────┐  │
│  │  🟣 Claude: I'd use a factory here       │  │
│  │  🟢 Codex: No, DI is cleaner             │  │
│  │  🔵 You: Let's go with DI but add...     │  │
│  │  (scrollable message list)               │  │
│  └──────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────┐  │
│  │  [Type a message...]          [Send]     │  │
│  └──────────────────────────────────────────┘  │
└────────────────────────────────────────────────┘
```

**Key implementation details:**
1. **Tab bar:** Two `Button` elements styled as tabs. Active tab (bound to `IsTownhallActive`) gets a bottom-border accent color. Clicking switches `IsTownhallActive` via `SwitchToTownhallCommand` / `SwitchToDmCommand`.
2. **Message list:** An `ItemsControl` bound to `TownhallMessages` (when `IsTownhallActive`) or `DirectMessages` (when not). Each item is a `Border`-wrapped layout with sender color dot, name, timestamp, and content.
3. **Input area:** A `TextBox` bound to `DraftMessage` + a `Button` bound to `SendMessageCommand`. Enter key also triggers send via `KeyBinding`.
4. **Chat tab state:** When `IsTownhallActive == false` and `SelectedDmRecipient == null`, show an agent selector (button list from `AvailableAgents`). When an agent is selected, show the DM transcript + input.

**MainWindow.axaml.cs changes (M4):**
- Replace `BuildPanel("Townhall", ...)` with `new TownhallView()`. Set its `DataContext` or ViewModel via `Grid.SetColumn` + `Grid.SetRow`.
- Wire in `WhenActivated`: `d.Add(this.WhenAnyValue(x => x.ViewModel!.TownhallViewModel).Subscribe(vm => townhallView.ViewModel = vm));`

**M4 exit checks:**
- [ ] TownhallView renders in center panel on launch
- [ ] Two tabs visible: "Townhall" and "Chat"
- [ ] Type message in input → appears in Townhall transcript
- [ ] Switch to Chat tab → agent selector appears
- [ ] Select agent → DM transcript shows with "No messages yet"
- [ ] Send message → appears in DM transcript
- [ ] Switch back to Townhall → DM messages not visible in group view
- [ ] Message bubbles are color-coded by sender (placeholder agent colors)
- [ ] All existing tests still pass

**M4 test additions (TownhallViewModelTests can cover):**
- No new test file needed in M4 — TownhallViewModel tests from M3 cover the behavior.
- Add manual verification to the regression matrix (M7): visual rendering, tab switching, send flow.

### M5: AgentChatView (DM Detail Panel)

**New file:** `src/Views/AgentChatView.cs` — Avalonia `UserControl`, C# code-behind.

**Purpose:** Content of the "Chat" tab when an agent is selected. Shows a 1:1 conversation between the user and a specific agent.

**Layout:**
```
┌────────────────────────────────────────────────┐
│  🟣 Claude          [Back to agents]           │
├────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────┐  │
│  │  🟣 Claude: I reviewed the factory       │  │
│  │     pattern. Here's my analysis...       │  │
│  │  🔵 You: Can you elaborate on the        │  │
│  │     DI alternative?                      │  │
│  │  🟣 Claude: Sure. With DI you...         │  │
│  └──────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────┐  │
│  │  [Type a message...]          [Send]     │  │
│  └──────────────────────────────────────────┘  │
└────────────────────────────────────────────────┘
```

**Key implementation details:**
1. **Header bar:** Shows agent name with color dot, plus a "Back" button that sets `SelectedDmRecipient = null` (returns to agent selector).
2. **Message list:** `ItemsControl` bound to `DirectMessages` filtered by `SelectedDmRecipient.Id`.
3. **Input area:** Shared `DraftMessage` / `SendMessageCommand` from TownhallViewModel.
4. **AgentChatView lifecycle:** Only visible in the Chat tab when `SelectedDmRecipient != null`. Hidden when agent selector is shown.

**Integration with TownhallView:**
- The Chat tab in TownhallView contains a conditional: if `SelectedDmRecipient == null`, show an agent selector; else show `AgentChatView`.
- This can be handled via a `ContentControl` whose content switches based on `SelectedDmRecipient != null`.

**M5 exit checks:**
- [ ] Select agent → AgentChatView renders with agent name, color, and empty transcript
- [ ] Send message → appears in DM transcript only
- [ ] Back button → returns to agent selector
- [ ] Switch to Townhall tab → DM messages not visible
- [ ] All existing tests pass

### M6: Architecture Docs Update

**Files to update:**
1. `docs/architecture/OVERVIEW.md` — New layout diagram, add TownhallViewModel/View, Townhall layer description
2. `docs/roadmap/PHASES.md` — Mark Phase 4 items as in-progress
3. Create `docs/refactor/refactor-3/TOFIX.md` — Start with empty checklist

**Architecture OVERVIEW.md changes:**
- Replace the current `Tree | Editor | AgentArea` diagram with the new `Editor | Townhall | FileTree` layout
- Add TownhallViewModel to the component list
- Update the "Planned layers" section — Phase 4 is now in progress

**PHASES.md changes:**
```
## Phase 4: Townhall (Agent Transparency)
- [~] Townhall view in center area (tab alongside editor)  ← "~" = in progress
- [ ] Auto-log: timestamped entries for agent actions
- [ ] Scrollable, filterable log
- [ ] Clear distinction between agent actions and user actions
```

### M7: Regression Sweep

**Automated:**
- [ ] `dotnet build` — zero warnings
- [ ] `dotnet test` — zero regressions

**Manual test matrix:**
- [ ] Open folder → tree populates on right
- [ ] Open file → editor opens on left
- [ ] Edit file → dirty flag shows
- [ ] Save → dirty flag clears
- [ ] Close dirty tab → dialog shows
- [ ] New file via tree context menu → file created, tree updates
- [ ] Rename file via tree → tree updates
- [ ] Delete file via tree → tree updates
- [ ] Terminal start (Ctrl+`) → shell runs
- [ ] Terminal stop → process exits
- [ ] Terminal restart → new shell starts
- [ ] Toggle bottom panel → terminal shows/hides
- [ ] Townhall: type message → appears in transcript
- [ ] Townhall: switch to Chat tab → agent selector shows
- [ ] Townhall: select agent → DM view shows
- [ ] Townhall: send DM → appears only in DM, not Townhall
- [ ] Townhall: back button → returns to agent selector
- [ ] Multiple messages → transcript scrolls correctly
- [ ] Window resize → all panels resize proportionally
- [ ] Ctrl+O → folder picker opens
- [ ] Ctrl+S → saves current file

## Limitations (by design)

- **No real agent integration** — `AvailableAgents` are hardcoded placeholders. Real agent connectivity comes in Phase 5 (Agent Panels) and Phase 6 (Agent-to-Agent Router).
- **No message persistence** — Messages live in memory only. Closing and reopening the app clears the transcript.
- **No message editing or deletion** — Once sent, messages are immutable in the transcript.
- **No markdown rendering** — Agent messages display as plain text.
- **No agent response simulation** — Sending a message adds it to the transcript but no agent responds. Agent response is Phase 5.
- **TownhallView is C# code-behind** — No XAML, consistent with existing pattern per DESIGN.md.
- **Editor column ratio** — Editor gets 1 star, Townhall gets 3 stars. This is a starting point; user can adjust via GridSplitter.

## Exit Conditions

- [ ] Build succeeds: `dotnet build` — zero warnings
- [ ] All existing tests pass: `dotnet test` — zero regressions
- [ ] New tests exist: `AgentIdentityTests`, `AgentMessageTests`, `TownhallViewModelTests`
- [ ] Layout is `Editor | Townhall | FileTree` (verified visually)
- [ ] File tree and editor behave identically to pre-refactor (moved, not changed)
- [ ] TownhallView shows in center with Townhall/Chat tabs
- [ ] Messages can be sent and displayed in both Townhall and DM channels
- [ ] `docs/architecture/OVERVIEW.md` reflects new layout
- [ ] `docs/roadmap/PHASES.md` marks Phase 4 items in progress
- [ ] No behavioral regressions in editor, file tree, or terminal

## Rollback Plan

- Commit hash to revert to: (fill before starting M1)
- Fallback strategy:
  - Revert `MainWindow.axaml.cs` column layout to `Tree | Splitter | Editor | AgentArea`
  - Remove `TownhallViewModel` from MainWindowViewModel constructor and DI registration
  - Remove `TownhallView.cs`, `AgentChatView.cs` files
  - Remove `AgentIdentity.cs`, `AgentMessage.cs` model files
  - Restore `BuildPanel("Agent Area", ...)` call in MainWindow.axaml.cs
  - Revert `docs/architecture/OVERVIEW.md` and `docs/roadmap/PHASES.md`
