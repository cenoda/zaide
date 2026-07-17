using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// Phase 9 M3: End-to-end integration tests for <see cref="EditorSearchViewModel"/>
/// using a mock <see cref="IEditorTextOperations"/> that simulates AvaloniaEdit behavior
/// including undo grouping, dirty state, and selection tracking.
/// </summary>
public sealed class EditorSearchIntegrationTests
{
    static EditorSearchIntegrationTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static ICommandRegistry CreateRegistry() => CommandRegistryFactory.Create();

    // ── Find Next / Previous navigation ──────────────────────────────────

    [Fact]
    public void FindNext_AdvancesThroughMatches()
    {
        var ops = new TrackingEditorOps("aaa bbb aaa bbb aaa");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "aaa";

        Assert.Equal(3, vm.MatchCount);
        Assert.Equal(0, vm.CurrentMatchIndex);

        vm.FindNextCommand.Execute(null);
        Assert.Equal(1, vm.CurrentMatchIndex);
        Assert.Equal(8, ops.GetSelectionOffset());
        Assert.Equal(3, ops.GetSelectionLength());

        vm.FindNextCommand.Execute(null);
        Assert.Equal(2, vm.CurrentMatchIndex);
        Assert.Equal(16, ops.GetSelectionOffset());
    }

    [Fact]
    public void FindNext_WrapsFromLastToFirst()
    {
        var ops = new TrackingEditorOps("aaa bbb aaa");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "aaa";

        vm.FindNextCommand.Execute(null); // index 1
        vm.FindNextCommand.Execute(null); // wraps to 0

        Assert.Equal(0, vm.CurrentMatchIndex);
        Assert.Equal(0, ops.GetSelectionOffset());
    }

    [Fact]
    public void FindPrevious_GoesBackward()
    {
        var ops = new TrackingEditorOps("aaa bbb aaa bbb aaa");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "aaa";

        // Start at index 0, go previous → wraps to last
        vm.FindPreviousCommand.Execute(null);
        Assert.Equal(2, vm.CurrentMatchIndex);
        Assert.Equal(16, ops.GetSelectionOffset());

        vm.FindPreviousCommand.Execute(null);
        Assert.Equal(1, vm.CurrentMatchIndex);
        Assert.Equal(8, ops.GetSelectionOffset());
    }

    [Fact]
    public void FindPrevious_WrapsFromFirstToLast()
    {
        var ops = new TrackingEditorOps("aaa bbb aaa");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "aaa";

        // At index 0, go previous → wraps to last
        vm.FindPreviousCommand.Execute(null);
        Assert.Equal(1, vm.CurrentMatchIndex);
    }

    // ── Selected-match replacement ───────────────────────────────────────

    [Fact]
    public void ReplaceNext_ReplacesWhenSelectionMatches()
    {
        var ops = new TrackingEditorOps("hello world hello");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "hello";
        vm.ReplacementText = "hi";
        vm.IsReplaceMode = true;

        // Simulate the initial selection that ExecuteReplace would set
        // via PerformSearchWithSelection. Query set alone no longer auto-selects
        // (to avoid stealing editor focus during continuous typing).
        ops.SetSelection(0, 5);

        vm.ReplaceNextCommand.Execute(null);

        Assert.Equal("hi world hello", ops.Text);
        Assert.Equal(1, vm.MatchCount); // only one "hello" left
    }

    [Fact]
    public void ReplaceNext_AdvancesWithoutReplacingWhenSelectionDoesNotMatch()
    {
        var ops = new TrackingEditorOps("hello world hello");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "hello";
        vm.ReplacementText = "hi";
        vm.IsReplaceMode = true;

        // Move selection away from the current match
        ops.SetSelection(6, 5); // select "world"

        vm.ReplaceNextCommand.Execute(null);

        // Text should be unchanged
        Assert.Equal("hello world hello", ops.Text);
        // Should have advanced to next match
        Assert.Equal(1, vm.CurrentMatchIndex);
    }

    // ── Replace All ──────────────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_ReplacesAllMatches()
    {
        var ops = new TrackingEditorOps("aaa bbb aaa bbb aaa");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "aaa";
        vm.ReplacementText = "ccc";
        vm.IsReplaceMode = true;

        vm.ReplaceAllCommand.Execute(null);

        Assert.Equal("ccc bbb ccc bbb ccc", ops.Text);
        Assert.Equal(0, vm.MatchCount); // no more matches after replace
    }

    [Fact]
    public void ReplaceAll_CaseInsensitive()
    {
        var ops = new TrackingEditorOps("Hello hello HELLO");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "hello";
        vm.ReplacementText = "x";
        vm.IsReplaceMode = true;
        vm.CaseSensitive = false;

        vm.ReplaceAllCommand.Execute(null);

        Assert.Equal("x x x", ops.Text);
    }

    [Fact]
    public void ReplaceAll_StatusMessageReportsCount()
    {
        var ops = new TrackingEditorOps("aaa bbb aaa");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "aaa";
        vm.ReplacementText = "ccc";
        vm.IsReplaceMode = true;

        vm.ReplaceAllCommand.Execute(null);

        Assert.Contains("2", vm.StatusMessage);
    }

    // ── Undo grouping ────────────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_IsOneUndoGroup()
    {
        var ops = new TrackingEditorOps("aaa bbb aaa bbb aaa");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "aaa";
        vm.ReplacementText = "ccc";
        vm.IsReplaceMode = true;

        vm.ReplaceAllCommand.Execute(null);

        // The ReplaceAllMatches call should have been wrapped in one undo group
        Assert.Equal(1, ops.UndoGroupCount);
    }

    [Fact]
    public void ReplaceAll_UndoRestoresOriginalText()
    {
        var original = "aaa bbb aaa bbb aaa";
        var ops = new TrackingEditorOps(original);
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "aaa";
        vm.ReplacementText = "ccc";
        vm.IsReplaceMode = true;

        vm.ReplaceAllCommand.Execute(null);
        Assert.Equal("ccc bbb ccc bbb ccc", ops.Text);

        ops.Undo();
        Assert.Equal(original, ops.Text);
    }

    // ── Dirty state ──────────────────────────────────────────────────────

    [Fact]
    public void ReplaceAll_MarksDocumentDirty()
    {
        var ops = new TrackingEditorOps("hello world hello");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "hello";
        vm.ReplacementText = "hi";
        vm.IsReplaceMode = true;

        Assert.False(ops.IsDirty);

        vm.ReplaceAllCommand.Execute(null);

        Assert.True(ops.IsDirty);
    }

    [Fact]
    public void FindNext_DoesNotMutateDocument()
    {
        var ops = new TrackingEditorOps("hello world");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "hello";

        vm.FindNextCommand.Execute(null);

        Assert.False(ops.IsDirty);
        Assert.Equal("hello world", ops.Text);
    }

    // ── Caret/selection outcomes ─────────────────────────────────────────

    [Fact]
    public void FindNext_SelectsMatchRange()
    {
        var ops = new TrackingEditorOps("aaa bbb aaa");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "aaa";

        // Query set updates match index; selection is deferred until explicit
        // navigation to avoid stealing editor focus during continuous typing.
        Assert.Equal(0, vm.CurrentMatchIndex);

        vm.FindNextCommand.Execute(null);
        Assert.Equal(1, vm.CurrentMatchIndex);
        Assert.Equal(8, ops.GetSelectionOffset());
        Assert.Equal(3, ops.GetSelectionLength());
    }

    /// <summary>
    /// Phase 9 M5c regression: sequential character entry in the search query
    /// must not call <see cref="IEditorTextOperations.SetSelection"/> (which
    /// can steal editor focus on Linux). Only explicit navigation or initial
    /// open should update the editor selection.
    /// </summary>
    [Fact]
    public void ContinuousTyping_DoesNotCallSetSelection()
    {
        var ops = new TrackingEditorOps("Avalonia is a cross-platform UI framework. Avalonia rocks.");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        // ActiveDocument set calls Dismiss → SetSelection(0,0). Reset the counter
        // so we only track SetSelection calls during query entry.
        ops.ResetSelectionCallCount();

        // Simulate typing "Avalonia" character by character
        vm.Query = "A";
        vm.Query = "Av";
        vm.Query = "Ava";
        vm.Query = "Aval";
        vm.Query = "Avalo";
        vm.Query = "Avalon";
        vm.Query = "Avaloni";
        vm.Query = "Avalonia";

        // 1. Final query is exactly "Avalonia"
        Assert.Equal("Avalonia", vm.Query);

        // 2. Match results update for the final query
        Assert.True(vm.MatchCount > 0);

        // 3. SetSelection was NOT called during query entry (no SelectCurrentMatch).
        //    The mock counter verifies that no SetSelection was invoked during typing.
        Assert.Equal(0, ops.SelectionCallCount);

        // 4. Active editor text is unchanged
        Assert.Equal("Avalonia is a cross-platform UI framework. Avalonia rocks.", ops.GetText());
    }

    [Fact]
    public void Dismiss_DoesNotMutateDocument()
    {
        var ops = new TrackingEditorOps("hello world");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "hello";
        vm.IsReplaceMode = true;

        vm.Dismiss();

        Assert.Equal("hello world", ops.Text);
        Assert.False(ops.IsDirty);
    }

    // ── Tab switching / closing ──────────────────────────────────────────

    [Fact]
    public void SwitchingTabs_ClearsSearchState()
    {
        var ops1 = new TrackingEditorOps("hello world");
        var ops2 = new TrackingEditorOps("other text");
        var vm = new EditorSearchViewModel(CreateRegistry());

        vm.ActiveDocument = ops1;
        vm.Query = "hello";
        vm.IsVisible = true;
        vm.IsReplaceMode = true;
        Assert.True(vm.MatchCount > 0);

        // Simulate tab switch
        vm.ActiveDocument = ops2;

        Assert.Equal(string.Empty, vm.Query);
        Assert.Equal(0, vm.MatchCount);
        Assert.False(vm.IsVisible);
        Assert.False(vm.IsReplaceMode);
        Assert.Equal(-1, vm.CurrentMatchIndex);
    }

    [Fact]
    public void ClosingAllTabs_ClearsSearchState()
    {
        var ops = new TrackingEditorOps("hello world");
        var vm = new EditorSearchViewModel(CreateRegistry());

        vm.ActiveDocument = ops;
        vm.Query = "hello";
        vm.IsVisible = true;

        // Simulate closing all tabs
        vm.ActiveDocument = null;

        Assert.Equal(string.Empty, vm.Query);
        Assert.Equal(0, vm.MatchCount);
        Assert.False(vm.IsVisible);
    }

    // ── Stale-document mutation prevention ───────────────────────────────

    [Fact]
    public void StaleDocument_CannotBeMutatedAfterTabSwitch()
    {
        var ops1 = new TrackingEditorOps("hello world hello");
        var ops2 = new TrackingEditorOps("other text");
        var vm = new EditorSearchViewModel(CreateRegistry());

        vm.ActiveDocument = ops1;
        vm.Query = "hello";
        vm.IsReplaceMode = true;
        vm.ReplacementText = "hi";

        // Switch to different document
        vm.ActiveDocument = ops2;

        // The old document should not have been mutated
        Assert.Equal("hello world hello", ops1.Text);
        Assert.False(ops1.IsDirty);
    }

    // ── Cancellation / dismissal ─────────────────────────────────────────

    [Fact]
    public void Dismiss_DoesNotReplaceOrSelect()
    {
        var ops = new TrackingEditorOps("hello world");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "hello";
        vm.ReplacementText = "hi";
        vm.IsReplaceMode = true;

        vm.Dismiss();

        Assert.Equal("hello world", ops.Text);
        Assert.Equal(0, ops.GetSelectionLength());
    }

    // ── Keybinding registration ──────────────────────────────────────────

    [Fact]
    public void Commands_ParticipateInKeybindingResolution()
    {
        var registry = CreateRegistry();
        _ = new EditorSearchViewModel(registry);

        var settings = new Mock<ISettingsService>();
        var emptySettings = SettingsModel.Defaults;
        settings.Setup(s => s.Current).Returns(emptySettings);

        var bindings = registry.ResolveKeyBindings(settings.Object);

        Assert.Contains(bindings, b => b.CommandId == "editor.find" && b.Gesture == "Ctrl+F");
        Assert.Contains(bindings, b => b.CommandId == "editor.replace" && b.Gesture == "Ctrl+H");
        Assert.Contains(bindings, b => b.CommandId == "editor.findNext" && b.Gesture == "F3");
        Assert.Contains(bindings, b => b.CommandId == "editor.findPrevious" && b.Gesture == "Shift+F3");
        // replaceNext and replaceAll are unbound — should NOT appear in resolved bindings
        Assert.DoesNotContain(bindings, b => b.CommandId == "editor.replaceNext");
        Assert.DoesNotContain(bindings, b => b.CommandId == "editor.replaceAll");
    }

    // ── Coexistence with palette commands ────────────────────────────────

    [Fact]
    public void SearchCommands_CoexistWithPaletteCommands()
    {
        var registry = CreateRegistry();
        _ = new CommandPaletteViewModel(registry);
        _ = new EditorSearchViewModel(registry);

        Assert.NotNull(registry.GetById("palette.open"));
        Assert.NotNull(registry.GetById("editor.find"));
        Assert.NotNull(registry.GetById("editor.replace"));
    }

    // ── Regression: Replace propagates to Document model (defect #1) ─────

    [Fact]
    public void ReplaceAll_UpdatesDocumentContent_AndSetsDirty()
    {
        // Simulates the real EditorView behavior: SetText pushes back to
        // EditorViewModel.TextContent, which sets Document.Content → IsDirty.
        var document = new Zaide.Features.Editor.Domain.Document("", "hello world hello");
        var editorVm = new EditorViewModel(document, new FakeFileService());
        var ops = new ViewModelSyncingEditorOps(editorVm, "hello world hello");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "hello";
        vm.ReplacementText = "hi";
        vm.IsReplaceMode = true;

        Assert.False(document.IsDirty);

        vm.ReplaceAllCommand.Execute(null);

        Assert.Equal("hi world hi", document.Content);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void ReplaceNext_UpdatesDocumentContent_AndSetsDirty()
    {
        var document = new Zaide.Features.Editor.Domain.Document("", "hello world hello");
        var editorVm = new EditorViewModel(document, new FakeFileService());
        var ops = new ViewModelSyncingEditorOps(editorVm, "hello world hello");
        var vm = new EditorSearchViewModel(CreateRegistry());
        vm.ActiveDocument = ops;
        vm.Query = "hello";
        vm.ReplacementText = "hi";
        vm.IsReplaceMode = true;

        // Simulate the initial selection that ExecuteReplace would set
        // via PerformSearchWithSelection.
        ops.SetSelection(0, 5);

        vm.ReplaceNextCommand.Execute(null);

        Assert.Equal("hi world hello", document.Content);
        Assert.True(document.IsDirty);
    }

    // ── Regression: Tab switch resets search state (defect #2) ───────────

    [Fact]
    public void ActiveDocumentId_Change_ResetsSearchState()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        var ops = new TrackingEditorOps("hello world");
        vm.ActiveDocument = ops;
        vm.Query = "hello";
        vm.IsVisible = true;
        vm.IsReplaceMode = true;
        Assert.True(vm.MatchCount > 0);

        // Simulate tab switch: same EditorView (same ops reference) but different document ID
        vm.ActiveDocumentId = "/other/file.cs";

        Assert.Equal(string.Empty, vm.Query);
        Assert.Equal(0, vm.MatchCount);
        Assert.False(vm.IsVisible);
        Assert.False(vm.IsReplaceMode);
        Assert.Equal(-1, vm.CurrentMatchIndex);
    }

    [Fact]
    public void ActiveDocumentId_SameValue_DoesNotReset()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        var ops = new TrackingEditorOps("hello world");
        vm.ActiveDocument = ops;
        vm.ActiveDocumentId = "/some/file.cs";
        vm.Query = "hello";
        Assert.True(vm.MatchCount > 0);

        // Set same ID again — should NOT reset
        vm.ActiveDocumentId = "/some/file.cs";

        Assert.Equal("hello", vm.Query);
        Assert.True(vm.MatchCount > 0);
    }

    [Fact]
    public void ActiveDocumentId_SetNull_ResetsSearchState()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        var ops = new TrackingEditorOps("hello world");
        vm.ActiveDocument = ops;
        vm.ActiveDocumentId = "/some/file.cs";
        vm.Query = "hello";
        vm.IsVisible = true;

        vm.ActiveDocumentId = null;

        Assert.Equal(string.Empty, vm.Query);
        Assert.Equal(0, vm.MatchCount);
        Assert.False(vm.IsVisible);
    }

    // ── Tracking mock ────────────────────────────────────────────────────

    /// <summary>
    /// Mock <see cref="IEditorTextOperations"/> with undo tracking and dirty state.
    /// Simulates AvaloniaEdit behavior for integration tests.
    /// </summary>
    private sealed class TrackingEditorOps : IEditorTextOperations
    {
        private string _text;
        private int _selOffset;
        private int _selLength;
        private int _selectionCallCount;
        private readonly List<string> _undoSnapshots = new();
        private int _undoGroupCount;

        public TrackingEditorOps(string text)
        {
            _text = text;
        }

        public string Text => _text;
        public bool IsDirty => _undoSnapshots.Count > 0;
        public int UndoGroupCount => _undoGroupCount;
        public int SelectionCallCount => _selectionCallCount;

        /// <summary>
        /// Resets the selection-call counter to zero. Used in tests that need to
        /// isolate SetSelection calls during a specific phase of execution.
        /// </summary>
        public void ResetSelectionCallCount() => _selectionCallCount = 0;

        public string GetText() => _text;

        public void SetText(string text)
        {
            _undoSnapshots.Add(_text);
            _text = text;
        }

        public void SetSelection(int offset, int length)
        {
            _selOffset = offset;
            _selLength = length;
            _selectionCallCount++;
        }

        public int GetSelectionOffset() => _selOffset;
        public int GetSelectionLength() => _selLength;

        public int ReplaceAllMatches(string query, string replacement, bool caseSensitive)
        {
            _undoGroupCount++;
            var matches = SearchEngine.FindAll(_text, query, caseSensitive);
            if (matches.Count == 0) return 0;

            _undoSnapshots.Add(_text);

            // Replace from end to start to preserve offsets
            var chars = _text.ToCharArray();
            var result = _text;
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                var m = matches[i];
                result = result.Substring(0, m.Offset) + replacement + result.Substring(m.Offset + m.Length);
            }

            _text = result;
            _selOffset = 0;
            _selLength = 0;
            return matches.Count;
        }

        public void Undo()
        {
            if (_undoSnapshots.Count > 0)
            {
                _text = _undoSnapshots[^1];
                _undoSnapshots.RemoveAt(_undoSnapshots.Count - 1);
            }
        }
    }

    /// <summary>
    /// Mock <see cref="IEditorTextOperations"/> that simulates the real EditorView
    /// behavior: when SetText is called, it pushes the new text back to
    /// EditorViewModel.TextContent, which sets Document.Content and IsDirty.
    /// This is the key seam that was broken in defect #1.
    /// </summary>
    private sealed class ViewModelSyncingEditorOps : IEditorTextOperations
    {
        private readonly EditorViewModel _editorVm;
        private string _text;
        private int _selOffset;
        private int _selLength;

        public ViewModelSyncingEditorOps(EditorViewModel editorVm, string text)
        {
            _editorVm = editorVm;
            _text = text;
        }

        public string GetText() => _text;

        public void SetText(string text)
        {
            _text = text;
            // Simulate the real EditorView behavior: push back to ViewModel
            if (_editorVm.TextContent != text)
                _editorVm.TextContent = text;
        }

        public void SetSelection(int offset, int length)
        {
            _selOffset = offset;
            _selLength = length;
        }

        public int GetSelectionOffset() => _selOffset;
        public int GetSelectionLength() => _selLength;

        public int ReplaceAllMatches(string query, string replacement, bool caseSensitive)
        {
            var matches = SearchEngine.FindAll(_text, query, caseSensitive);
            if (matches.Count == 0) return 0;

            var result = _text;
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                var m = matches[i];
                result = result.Substring(0, m.Offset) + replacement + result.Substring(m.Offset + m.Length);
            }

            SetText(result);
            return matches.Count;
        }
    }

    /// <summary>
    /// Minimal fake IFileService for tests that need an EditorViewModel.
    /// </summary>
    private sealed class FakeFileService : Zaide.Features.Editor.Contracts.IFileService
    {
        public System.Threading.Tasks.Task<string> ReadAllTextAsync(string path)
            => System.Threading.Tasks.Task.FromResult(string.Empty);

        public System.Threading.Tasks.Task WriteAllTextAsync(string path, string content)
            => System.Threading.Tasks.Task.CompletedTask;
    }
}
