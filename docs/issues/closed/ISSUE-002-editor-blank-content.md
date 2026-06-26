# ISSUE-002: Editor shows blank content after opening file from tree

**Label:** BUG
**Status:** open
**Priority:** critical
**Related:** Phase 2 M3, `EditorView.cs`, `MainWindow.axaml.cs`

## Description

When a file is opened from the file tree, the tab appears correctly (shows filename), the TextEditor widget renders, but the content area is blank — no text appears. The file content is loaded into `EditorViewModel.TextContent` (verified via `LoadFileContent`). Earlier attempts suspected it never reached `_textEditor.Text`; the current debug boundary now points more strongly at view layout/rendering.

## Steps to Reproduce

1. Open a folder in the file tree
2. Click a `.cs` or `.md` file
3. Tab appears with correct filename
4. Editor area shows line numbers but no text content

**Expected behavior:** File content renders in the editor.
**Actual behavior:** Editor is blank (line numbers visible, no text).

## Debug Log

### Attempt 1: WhenAnyValue
- **Hypothesis:** `WhenAnyValue(x => x.ViewModel)` resolves the expression tree to the base `UserControl.ViewModel` (typed `object?`) instead of `ReactiveUserControl<T>.ViewModel` (typed `T?`), so `PropertyChanged` from `SetValue(ViewModelProperty, value)` doesn't match and the observable never re-emits.
- **Action:** Replaced `this.WhenAnyValue(x => x.ViewModel)` with `this.GetObservable(ViewModelProperty)` — Avalonia's native StyledProperty observable.
- **Result:** Editor still shows blank content. Same symptom.
- **Error / Output:** None — no exceptions, no warnings. Content silently fails to appear.

### Attempt 2: GetObservable(ViewModelProperty)
- **Hypothesis:** `GetObservable` talks directly to Avalonia's property system, should fire on every `SetValue`. If this still fails, the problem is NOT in the ViewModel change detection — it's either in the data flow (TextContent is empty when the editor loads) or in timing (something resets `_textEditor.Text` after it's set).
- **Action:** Applied `GetObservable(ViewModelProperty)`. Code compiles and passes tests.
- **Result:** Same — blank editor.
- **Error / Output:** None.

### Attempt 3: View-layer boundary audit
- **Hypothesis:** The file-selection and tab ViewModel path may be fine; the blank area may be caused by the center layout or the `EditorView` visual surface rather than by missing file content.
- **Action:** Audited the live code from `FileTreeView.SelectionChanged` through `MainWindowViewModel`, `EditorTabViewModel.OpenFile`, `MainWindow.WhenActivated`, and `EditorView.WhenActivated`.
- **Result:** The data path is coherent:
  - `FileTreeView.SelectionChanged` assigns `FileTreeViewModel.SelectedFile`.
  - `MainWindowViewModel` observes `SelectedFile`, filters supported extensions, and executes `EditorTabs.OpenFileCommand`.
  - `EditorTabViewModel.OpenFile` reads the file, calls `tab.LoadFileContent(content)`, adds the tab, then assigns `ActiveTab`.
  - `MainWindow` observes `EditorTabs.ActiveTab` and assigns `_editorView.ViewModel = active`.
  - `EditorView` observes `ViewModelProperty` and assigns `_textEditor.Text = vm.TextContent`.
- **Additional guardrail:** Added `MainWindowViewModelTests.SelectingSupportedFile_OpensActiveTabWithContent` so the selected-file → active-tab → loaded-content path is covered by tests.
- **Most likely remaining cause:** Center layout. `MainWindow.BuildLayout` used a `DockPanel` with children `{ _editorTabBar, _editorView, _welcomeText }`, but only the last child fills by default. That made `_welcomeText`, not `_editorView`, the fill child. When the welcome text is hidden after opening a tab, `_editorView` is still a non-fill child, which can leave AvaloniaEdit measured/arranged incorrectly even though the editor control itself exists.
- **Patch applied:** Replaced the center `DockPanel` with a two-row `Grid`: tab bar in row 0, editor and welcome overlay in row 1. Also set the `TextEditor` alignment to stretch.
- **Verification:** `dotnet test Zaide.slnx` passes after the patch. Manual UI verification still needed to confirm rendered text appears.

### Attempt 4: AvaloniaEdit integration audit
- **Hypothesis:** The control itself is present and receiving text, but the app never loads AvaloniaEdit's required theme/style resource, so the editor surface renders incompletely. This fits the symptom better than the data-flow theories: line-number margin exists, `_textEditor.Text` can be non-empty, yet the text layer stays visually blank.
- **Action:** Compared the app bootstrap with AvaloniaEdit's upstream setup guidance. `App.axaml` loaded `SemiTheme.axaml` only; it did **not** include `avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml`, which AvaloniaEdit documents as required.
- **Result:** Partial. Adding the raw AvaloniaEdit Fluent style uncovered a second dependency: it expects Fluent theme resources such as `ControlContentThemeFontSize`, which were absent because the app was only loading Semi theme styles.
- **Error / Output:** None. `dotnet test Zaide.slnx` still passes after the patch.

### Attempt 5: Theme dependency follow-up
- **Hypothesis:** The blank editor fix is still style-related, but the direct AvaloniaEdit style include must be paired with Avalonia's base `FluentTheme` so its static resources resolve at runtime.
- **Action:** Updated `App.axaml` to load `<fluent:FluentTheme />` before `SemiTheme.axaml`, then keep `avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml` layered on top.
- **Result:** Build/test validation pending manual runtime confirmation.
- **Error / Output:** Previous runtime failure: `KeyNotFoundException: Static resource 'ControlContentThemeFontSize' not found.`

### Diagnostic trace status

Temporary file logging currently exists in `MainWindow.axaml.cs` and `EditorView.cs`, writing to `/tmp/zaide-debug.log`.

Expected log sequence after opening a file:

```text
[MainWindow] ActiveTab changed: <filename>
[EditorView] ViewModelProperty changed: <filename>
[EditorView] TextContent length=<non-zero>, preview='<file prefix>'
[EditorView] After set, _textEditor.Text length=<same non-zero length>
[MainWindow] _editorView.ViewModel is now: <filename>
```

Interpretation:
- If `ActiveTab changed` is missing, the break is before `MainWindow`.
- If `TextContent length=0`, the break is in file loading or tab creation.
- If `_textEditor.Text length` is non-zero but UI is blank, the break is rendering/layout/theme.
- If lengths are non-zero after the Grid patch and UI still blanks, inspect AvaloniaEdit text foreground/theme next.

### Previous next diagnostic steps

Add `Debug.WriteLine` tracing to narrow down where the chain breaks:

```csharp
// In EditorView.WhenActivated, after GetObservable subscription:
d.Add(this.GetObservable(ViewModelProperty)
    .Subscribe(obj =>
    {
        System.Diagnostics.Debug.WriteLine($"[EditorView] ViewModel changed: {obj?.GetType().Name ?? "null"}");
        if (obj is EditorViewModel vm)
        {
            System.Diagnostics.Debug.WriteLine($"[EditorView] TextContent length: {vm.TextContent.Length}");
            System.Diagnostics.Debug.WriteLine($"[EditorView] TextContent preview: {vm.TextContent.Substring(0, Math.Min(50, vm.TextContent.Length))}");
            _textEditor.Text = vm.TextContent;
            System.Diagnostics.Debug.WriteLine($"[EditorView] _textEditor.Text after set: '{_textEditor.Text.Substring(0, Math.Min(50, _textEditor.Text.Length))}'");
        }
    }));
```

Also trace in `MainWindow.axaml.cs`:
```csharp
// In the ActiveTab subscription:
.Subscribe(active =>
{
    System.Diagnostics.Debug.WriteLine($"[MainWindow] ActiveTab changed: {active?.FileName ?? "null"}");
    _editorView.ViewModel = active;
    System.Diagnostics.Debug.WriteLine($"[MainWindow] _editorView.ViewModel set to: {_editorView.ViewModel?.FileName ?? "null"}");
    ...
});
```

Possible theories to test:
1. `_editorView.ViewModel = active` is never called (ActiveTab subscription not firing)
2. `GetObservable(ViewModelProperty)` fires but `vm.TextContent` is empty
3. `_textEditor.Text` is set correctly but immediately overwritten by `TextChanged` → `OnTextChanged` feedback
4. `_textEditor.Text` is set but AvaloniaEdit doesn't render it (theme/font issue)
5. `EditorView.WhenActivated` fires AFTER MainWindow sets ViewModel (ordering), so the subscription isn't active yet, and the initial set is missed

## Resolution

- **Root cause:** Two issues combined:
  1. **Layout:** `DockPanel` gave fill space to `_welcomeText` (last child), collapsing `_editorView` to zero height. Verified by debug log: data flow was correct — `_textEditor.Text` was set to 11376 chars, but the editor had zero layout height.
  2. **Theme:** Missing `AvaloniaEdit.xaml` style include — TextEditor rendering was incomplete without its Fluent theme resources.
- **Fix:** Replaced `DockPanel` with 2-row `Grid` (row 0: Auto for tab bar, row 1: * for editor+welcome overlay). Added `FluentTheme` + `AvaloniaEdit.xaml` to `App.axaml`. Set `Stretch` alignment on TextEditor.
- **Commit:** `13dabbb`
- **Closed date:** 2026-06-26
