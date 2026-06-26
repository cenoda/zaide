# Phase 2: Editor — Implementation Plan

## Pre-Implementation Verification

- [ ] `dotnet build Zaide.slnx` passes with 0 warnings (Phase 1 baseline)
- [ ] `dotnet test Zaide.slnx` passes: 22 tests, 0 failures
- [ ] Phase 1 TOFIX.md items are all resolved
- [ ] AvaloniaEdit + TextMate packages verified compatible with Avalonia 12

---

## Scope

**Goal:** Replace center panel placeholder with a tabbed code editor. Open files from tree, edit, save, with syntax highlighting.

**Boundaries (NOT building):**
- No multiple cursor editing
- No find/replace (Ctrl+F deferred to Phase 2 polish)
- No drag-and-drop tab reordering
- No file encoding detection (assume UTF-8)
- No auto-save (manual Ctrl+S only)

---

## Milestones (Incremental)

| Milestone | Description | Test |
|-----------|-------------|------|
| M0 | Entry gate: Phase 1 clean build + tests pass | `dotnet build Zaide.slnx && dotnet test Zaide.slnx` |
| M1 | AvaloniaEdit package integrated, basic editor renders in center | Unit test: `EditorView` instantiates without error |
| M2 | Tab bar with hover close button, auto-shrink, ellipsis | Visual: long tab names show `...`, hover shows × |
| M3 | Open file from tree → editor tab | Click .cs file in tree → opens in new tab |
| M4 | Edit + dirty indicator (●), Ctrl+S saves | Type in editor → tab shows ●, Ctrl+S saves |
| M5 | Close tab with unsaved prompt: [Save] [Don't Save] [Cancel] | Close dirty tab → dialog appears |
| M6 | Syntax highlighting for C#, JSON, Markdown | C# shows keywords blue, strings red, comments green |

---

### M1: AvaloniaEdit Integration

**Packages to add** (verify in Directory.Packages.props first):
- `AvaloniaEdit` — text editor widget
- `AvaloniaEdit.TextMate` — TextMate grammar support
- `TextMateSharp.Grammars` — bundled grammars (C#, JSON, Markdown, etc.)

**Files to create:**

- `src/Views/EditorView.cs`
  - Inherits `ReactiveUserControl<EditorViewModel>`
  - Contains `TextEditor` from AvaloniaEdit (not Avalonia's TextBox)
  - Binds to `EditorViewModel.TextContent` (two-way)
  - Binds to `EditorViewModel.IsDirty` for indicator

- `src/ViewModels/EditorViewModel.cs`
  - Inherits `ReactiveObject`
  - `string FilePath` — open file path
  - `string FileName` — display name (derived from FilePath)
  - `string TextContent` — file contents (two-way bound to editor)
  - `bool IsDirty` — modified since last save
  - `bool IsSaved` — read-only flag

**Files to modify:**

- `src/Zaide.csproj` — add package references
- `src/Program.cs` — register `EditorViewModel` (Transient — one per tab)

**Tests to add:**

- `tests/Zaide.Tests/ViewModels/EditorViewModelTests.cs`
  - `FileName_DerivedFromPath`
  - `IsDirty_DefaultsToFalse`
  - `MarkDirty_WhenTextChanges`
  - `MarkClean_AfterSave`

---

### M2: Tab Bar with Hover Close

**Files to create:**

- `src/Views/EditorTabBar.cs`
  - `ItemsControl` with horizontal `StackPanel`
  - Each tab: `Border` containing:
    - `TextBlock` (tab name, ellipsis truncation)
    - `Button` (× close, visibility: `IsVisible = false`, `IsVisible = true` on PointerEnter)
  - Tab width: auto-shrink to content (no fixed width)
  - Long names: `TextTrimming = TextTrimming.CharacterEllipsis`

**Files to modify:**

- `src/ViewModels/EditorTabViewModel.cs` (new, see M3)
  - Add `EditorViewModel` per tab

**No unit tests (UI-only milestone).**

---

### M3: Open File from Tree → Editor Tab

**Files to create:**

- `src/ViewModels/EditorTabViewModel.cs`
  - Manages collection of open editor tabs
  - `ObservableCollection<EditorViewModel> OpenTabs`
  - `EditorViewModel? ActiveTab`
  - `OpenFileCommand` — takes path, creates/activates tab
  - `CloseTabCommand` — closes tab, handles dirty check

- `src/ViewModels/MainWindowViewModel.cs`
  - Add `EditorTabViewModel EditorTabs` property
  - Subscribe to `FileTreeViewModel.SelectedFile`:
    - If supported extension → `EditorTabs.OpenFileCommand.Execute(path)`
    - Else → ignore (show "Unsupported file type" in status?)

**Files to modify:**

- `src/MainWindow.axaml.cs`
  - Replace center `TextBlock` with `TabControl` or custom tab bar + `EditorView`
  - Bind `EditorTabs.OpenTabs` to tab items
  - Bind `EditorTabs.ActiveTab` to current editor

**Tests to add:**

- `tests/Zaide.Tests/ViewModels/EditorTabViewModelTests.cs`
  - `OpenFile_CreatesNewTab`
  - `OpenFile_ActivatesExisting`
  - `CloseTab_RemovesFromCollection`

---

### M4: Edit + Dirty Indicator + Ctrl+S

**Files to modify:**

- `src/ViewModels/EditorViewModel.cs`
  - In constructor, subscribe to `this.WhenAnyValue(x => x.TextContent)`
    - On change: if not from load → `IsDirty = true`
  - Add `SaveCommand`:
    - Write `TextContent` to `FilePath`
    - Set `IsDirty = false`

- `src/Views/EditorView.cs`
  - Add KeyBinding for Ctrl+S → `ViewModel!.SaveCommand`

- `src/Views/EditorTabBar.cs`
  - Bind tab header to `EditorViewModel.IsDirty`:
    - Show `●` next to filename when dirty

**Tests to add:**

- `tests/Zaide.Tests/ViewModels/EditorViewModelTests.cs`
  - `SaveCommand_WritesFile`
  - `SaveCommand_ClearsDirty`

---

### M5: Close Tab with Unsaved Prompt

**Files to modify:**

- `src/ViewModels/EditorTabViewModel.cs`
  - Modify `CloseTabCommand`:
    - If `tab.IsDirty` → show dialog
    - Dialog: "Save changes to {filename}?"
      - [Save] → call `tab.SaveCommand.Execute()`, then close
      - [Don't Save] → close without saving
      - [Cancel] → do nothing, return

- `src/Views/EditorTabBar.cs`
  - Close button click → `CloseTabCommand.Execute(tab)`

**Dialog implementation:**
- Use `MessageBox.Avalonia` or native `Window` dialog
- Buttons: [Save] [Don't Save] [Cancel] — text, no icons (icons Phase 6+)

**No new tests (UI + integration).**

---

### M6: Syntax Highlighting

**Files to modify:**

- `src/Views/EditorView.cs`
  - In constructor, initialize TextMate:
    ```csharp
    _textEditor.Theme = new DarkTheme();
    _textEditor.Grammar = LoadGrammar(Path.GetExtension(FilePath));
    ```
  - `LoadGrammar`: map extension → TextMate grammar
    - `.cs` → C#
    - `.json` → JSON
    - `.md` → Markdown
    - Default → none (plain text)

- `src/Models/GrammarRegistry.cs` (new)
  - Static mapping: extension → grammar name
  - Pre-loaded: C#, JSON, Markdown (from TextMateSharp.Grammars)
  - Abstraction: allow runtime grammar loading (future)

**Grammar loading:**
- TextMateSharp.Grammars bundles 100+ grammars
- No need to download external `.tmLanguage` files in Phase 2

**No new tests (integration).**

---

## Supported File Extensions

Phase 2 supported for syntax highlighting:
- `.cs` — C#
- `.json` — JSON
- `.md` — Markdown
- `.txt` — Plain text (fallback)

All other files open as plain text (no highlighting).

---

## Limitations (by design)

- **No find/replace** — Ctrl+F deferred to Phase 2 polish.
- **No drag-and-drop tab reordering** — tabs stay in open order.
- **No auto-save** — manual Ctrl+S only.
- **No file encoding detection** — assumes UTF-8.
- **No icons** — dirty indicator is unicode `●`, buttons are text.
- **No multiple tabs in single panel** — one editor view, multiple tabs switch.