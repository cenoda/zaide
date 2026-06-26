# Phase 2: Editor — Implementation Plan

## Pre-Implementation Verification

- [ ] `dotnet build Zaide.slnx` passes with 0 warnings (Phase 1 baseline)
- [ ] `dotnet test Zaide.slnx` passes: 22 tests, 0 failures
- [ ] Phase 1 TOFIX.md items are all resolved
- [ ] AvaloniaEdit + TextMate packages verified compatible with Avalonia 12
- [ ] `Interaction<T,U>` pattern confirmed working (used for M5 dialog)

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
  - Syncs `EditorViewModel.TextContent` via `TextChanged` event handler
    (AvaloniaEdit's `TextEditor` doesn't support standard Avalonia binding —
    use event-based sync: `TextChanged` writes to ViewModel, `WhenAnyValue(ViewModel)`
    loads content on tab switch. Never use both `Bind` and event handler.)
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
  - **Do NOT use `ItemsControl`** — Avalonia 12's `ItemsPanelTemplate` API is
    missing. Use a manual horizontal `StackPanel` inside a `ScrollViewer`, with
    `NotifyCollectionChanged` handler on `OpenTabs` to add/remove tab items.
  - Each tab: `Border` containing a `Grid` with:
    - `TextBlock` (tab name, ellipsis truncation, `Margin` right 24px reserves space)
    - `Button` (× close, overlays via Grid, `IsVisible` toggled on `PointerEnter`/`PointerExited`)
    - **Close button must use a Grid overlay, not inline StackPanel** — inline
      buttons increase tab height when they appear.
  - Tab width: auto-shrink to content (no fixed width)
  - Long names: `TextTrimming = TextTrimming.CharacterEllipsis`

**Files to modify:**

_None. M2 is UI-only. EditorTabViewModel is created in M3._

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
  - **AvaloniaEdit's TextEditor intercepts Ctrl+S internally.** Either override
    TextEditor input handling, or place the keybinding on `MainWindow` (simpler).
    `MainWindow` approach: add `KeyBinding` to `MainWindow.KeyBindings` in
    `WhenActivated`, reading `EditorTabViewModel.ActiveTab.SaveCommand`.

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
  - Add `Interaction<EditorViewModel, bool> ConfirmClose` property
  - Modify `CloseTabCommand` (use `ReactiveCommand.CreateFromTask`):
    - If `tab.IsDirty` → `var shouldSave = await ConfirmClose.Handle(tab)`
    - `shouldSave == true` → call `tab.SaveCommand.Execute().Subscribe()`, then close
    - `shouldSave == false` → close without saving
    - User cancels → interaction returns no output, do nothing
  - **ViewModel must NOT reference Views.** No `Func<>` callbacks, no `using Avalonia.Controls`.

- `src/Views/EditorTabBar.cs`
  - Close button click → `CloseTabCommand.Execute(tab).Subscribe()`

**Dialog implementation:**
- Create `src/Views/UnsavedDialog.axaml` + `.axaml.cs` — a `ReactiveWindow`
  with [Save] [Don't Save] [Cancel] buttons, returns `bool?`.
- In `MainWindow.WhenActivated`, register the interaction handler:
  ```csharp
  d.Add(ViewModel.EditorTabViewModel.ConfirmClose.RegisterHandler(async ctx =>
  {
      var dialog = new UnsavedDialog { DataContext = ctx.Input };
      var result = await dialog.ShowDialog<bool?>(this);
      if (result.HasValue) ctx.SetOutput(result.Value);
  }));
  ```
- **Use `ShowDialog` (blocking), never `Show` (non-blocking).** `Show()` returns
  immediately before the user clicks anything.
- Buttons: [Save] [Don't Save] [Cancel] — text, no icons (icons Phase 6+).

**No new tests (UI + integration).**

---

### M6: Syntax Highlighting

**Files to modify:**

- `src/Views/EditorView.cs`
  - In constructor, initialize TextMate:
    ```csharp
    var registry = new RegistryOptions(ThemeName.DarkPlus);
    var installation = _textEditor.InstallTextMate(registry);
    installation.SetGrammar("source.cs"); // default, overridden on file load
    ```
  - `LoadGrammar`: map extension → TextMate scope name via switch expression
    - `.cs` → `"source.cs"`
    - `.json` → `"source.json"`
    - `.md` → `"text.html.markdown"`
    - Default → null (plain text)
  - Call `_textMate.SetGrammar(scope)` when scope is not null.
  - **The API is `InstallTextMate(registry)` returning an installation object,
    then `.SetGrammar(scope)` on that object. Not `_textEditor.Grammar = ...`.**

**No `GrammarRegistry.cs`** — a switch expression in `EditorView.LoadGrammar`
is sufficient for Phase 2. YAGNI. Defer to Phase 3+ if needed.

**Grammar loading:**
- TextMateSharp.Grammars bundles 100+ grammars
- No need to download external `.tmLanguage` files in Phase 2
- `RegistryOptions(ThemeName.DarkPlus)` matches the app's dark theme

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

---

## Known Pitfalls

These are from the first attempt (reverted at `0971113`, see `REVERT_LOG.md`).

### Activation ordering

`EditorTabBar.WhenActivated` and `EditorView.WhenActivated` may fire
**before** `MainWindow.WhenActivated` sets their ViewModels. Both views
must handle `ViewModel == null` gracefully. One approach: use
`this.WhenAnyValue(x => x.ViewModel).Subscribe(...)` inside `WhenActivated`
instead of reading `ViewModel` directly — this fires on the initial set
and every subsequent change.

### Binding feedback loop

A two-way `Bind` on `TextEditor.Text` ↔ `EditorViewModel.TextContent`
creates an infinite feedback loop because `TextEditor` fires `TextChanged`
on programmatic changes. The working pattern:

```csharp
this.WhenActivated(d =>
{
    // ViewModel → editor: load content on tab switch
    d.Add(this.WhenAnyValue(x => x.ViewModel)
        .Subscribe(vm =>
        {
            if (vm is null) return;
            _textEditor.Text = vm.TextContent;
        }));

    // Editor → ViewModel: sync on user edits
    _textEditor.TextChanged += OnTextChanged;
    d.Add(Disposable.Create(() => _textEditor.TextChanged -= OnTextChanged));
});
```

### Close button hover race

`PointerExited` on the tab `Border` can fire **before** the user clicks
the close button, causing the button to vanish mid-click. Mitigations:
- Use `Opacity` transitions instead of `IsVisible` (button stays in layout)
- Add a small delay before hiding
- Make the close button's hit target extend outside the tab border

### Non-blocking Show()

Avalonia's `Window.Show()` is non-blocking — the method returns immediately.
For dialogs that need a result, use `ShowDialog<T>(owner)` which blocks
until the window closes. This is critical for the unsaved-changes prompt.

### Subscription leaks

Every `.Subscribe()` inside `WhenActivated` must be wrapped in `d.Add()`.
This includes `ReactiveCommand.Execute().Subscribe()`. If the subscription
can't go through `d.Add()`, use an async alternative like
`Observable.StartAsync` or reconsider the pattern.

### AvaloniaEdit API

The correct TextMate initialization:
```csharp
var registry = new RegistryOptions(ThemeName.DarkPlus);
var installation = _textEditor.InstallTextMate(registry);
installation.SetGrammar("source.cs");
```

Not `_textEditor.Grammar = ...` or `_textEditor.Theme = new DarkTheme()`.
`InstallTextMate` returns a concrete type — do not use `dynamic`.