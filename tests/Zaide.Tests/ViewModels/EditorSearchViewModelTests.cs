using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 9 M3: Tests for <see cref="SearchEngine"/> (pure logic) and
/// <see cref="EditorSearchViewModel"/> (state, commands, registration).
/// All tests verify behavior without Avalonia controls.
/// </summary>
public sealed class EditorSearchViewModelTests
{
    static EditorSearchViewModelTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static ICommandRegistry CreateRegistry() => CommandRegistryFactory.Create();

    // ── SearchEngine: FindAll ────────────────────────────────────────────

    [Fact]
    public void FindAll_EmptyQuery_ReturnsZeroMatches()
    {
        var result = SearchEngine.FindAll("hello world", "");
        Assert.Empty(result);
    }

    [Fact]
    public void FindAll_NullQuery_ReturnsZeroMatches()
    {
        var result = SearchEngine.FindAll("hello world", null!);
        Assert.Empty(result);
    }

    [Fact]
    public void FindAll_EmptyText_ReturnsZeroMatches()
    {
        var result = SearchEngine.FindAll("", "hello");
        Assert.Empty(result);
    }

    [Fact]
    public void FindAll_CaseSensitive_Default_FindsExactMatch()
    {
        var result = SearchEngine.FindAll("Hello hello HELLO", "hello");
        Assert.Single(result);
        Assert.Equal(6, result[0].Offset);
        Assert.Equal(5, result[0].Length);
    }

    [Fact]
    public void FindAll_CaseInsensitive_FindsAllCases()
    {
        var result = SearchEngine.FindAll("Hello hello HELLO", "hello", caseSensitive: false);
        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].Offset);
        Assert.Equal(6, result[1].Offset);
        Assert.Equal(12, result[2].Offset);
    }

    [Fact]
    public void FindAll_LiteralSpecialChars_DoesNotUseRegex()
    {
        var text = "foo.bar foo*bar foo[bar foo(bar foo)bar foo\\bar";
        var result = SearchEngine.FindAll(text, "foo.bar");
        Assert.Single(result);
        Assert.Equal(0, result[0].Offset);
    }

    [Fact]
    public void FindAll_RegexMetacharacters_TreatedAsLiterals()
    {
        var text = "a.b a*b a+b a?b a(b a)b";
        Assert.Single(SearchEngine.FindAll(text, "a.b"));
        Assert.Single(SearchEngine.FindAll(text, "a*b"));
        Assert.Single(SearchEngine.FindAll(text, "a+b"));
        Assert.Single(SearchEngine.FindAll(text, "a?b"));
        Assert.Single(SearchEngine.FindAll(text, "a(b"));
        Assert.Single(SearchEngine.FindAll(text, "a)b"));
    }

    [Fact]
    public void FindAll_ZeroMatches_ReturnsEmptyList()
    {
        var result = SearchEngine.FindAll("hello world", "xyz");
        Assert.Empty(result);
    }

    [Fact]
    public void FindAll_OneMatch_ReturnsSingleResult()
    {
        var result = SearchEngine.FindAll("hello world", "world");
        Assert.Single(result);
        Assert.Equal(6, result[0].Offset);
        Assert.Equal(5, result[0].Length);
    }

    [Fact]
    public void FindAll_ManyMatches_ReturnsAllNonOverlapping()
    {
        var result = SearchEngine.FindAll("aaa aaa aaa", "aaa");
        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].Offset);
        Assert.Equal(4, result[1].Offset);
        Assert.Equal(8, result[2].Offset);
    }

    [Fact]
    public void FindAll_AdjacentMatches()
    {
        var result = SearchEngine.FindAll("aaaa", "aa");
        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].Offset);
        Assert.Equal(2, result[1].Offset);
    }

    // ── SearchEngine: NextMatchIndex / PreviousMatchIndex ────────────────

    [Fact]
    public void NextMatchIndex_EmptyList_ReturnsNegativeOne()
    {
        Assert.Equal(-1, SearchEngine.NextMatchIndex(0, 0));
    }

    [Fact]
    public void NextMatchIndex_Advances()
    {
        Assert.Equal(1, SearchEngine.NextMatchIndex(0, 3));
        Assert.Equal(2, SearchEngine.NextMatchIndex(1, 3));
    }

    [Fact]
    public void NextMatchIndex_WrapsFromLastToFirst()
    {
        Assert.Equal(0, SearchEngine.NextMatchIndex(2, 3));
    }

    [Fact]
    public void NextMatchIndex_NegativeCurrent_ReturnsZero()
    {
        Assert.Equal(0, SearchEngine.NextMatchIndex(-1, 3));
    }

    [Fact]
    public void PreviousMatchIndex_EmptyList_ReturnsNegativeOne()
    {
        Assert.Equal(-1, SearchEngine.PreviousMatchIndex(0, 0));
    }

    [Fact]
    public void PreviousMatchIndex_GoesBack()
    {
        Assert.Equal(1, SearchEngine.PreviousMatchIndex(2, 3));
    }

    [Fact]
    public void PreviousMatchIndex_WrapsFromFirstToLast()
    {
        Assert.Equal(2, SearchEngine.PreviousMatchIndex(0, 3));
    }

    // ── SearchEngine: ReplaceAll ─────────────────────────────────────────

    [Fact]
    public void ReplaceAll_EmptyQuery_ReturnsOriginalText()
    {
        Assert.Equal("hello", SearchEngine.ReplaceAll("hello", "", "x"));
    }

    [Fact]
    public void ReplaceAll_ReplacesAllOccurrences()
    {
        var result = SearchEngine.ReplaceAll("aaa bbb aaa", "aaa", "ccc");
        Assert.Equal("ccc bbb ccc", result);
    }

    [Fact]
    public void ReplaceAll_CaseSensitive_DoesNotReplaceDifferentCase()
    {
        var result = SearchEngine.ReplaceAll("Hello hello HELLO", "hello", "x");
        Assert.Equal("Hello x HELLO", result);
    }

    [Fact]
    public void ReplaceAll_CaseInsensitive_ReplacesAllCases()
    {
        var result = SearchEngine.ReplaceAll("Hello hello HELLO", "hello", "x", caseSensitive: false);
        Assert.Equal("x x x", result);
    }

    [Fact]
    public void ReplaceAll_LiteralSpecialChars_DoesNotUseRegex()
    {
        var result = SearchEngine.ReplaceAll("a.b a*b a+b", "a.b", "x");
        Assert.Equal("x a*b a+b", result);
    }

    // ── EditorSearchViewModel: Command registration ──────────────────────

    [Fact]
    public void Constructor_RegistersAllSixCommands()
    {
        var registry = CreateRegistry();
        _ = new EditorSearchViewModel(registry);

        Assert.NotNull(registry.GetById("editor.find"));
        Assert.NotNull(registry.GetById("editor.replace"));
        Assert.NotNull(registry.GetById("editor.findNext"));
        Assert.NotNull(registry.GetById("editor.findPrevious"));
        Assert.NotNull(registry.GetById("editor.replaceNext"));
        Assert.NotNull(registry.GetById("editor.replaceAll"));
    }

    [Fact]
    public void Constructor_RegistersCommandsExactlyOnce()
    {
        var registry = CreateRegistry();
        _ = new EditorSearchViewModel(registry);

        Assert.Single(registry.GetAll(), d => d.Id == "editor.find");
        Assert.Single(registry.GetAll(), d => d.Id == "editor.replace");
    }

    [Fact]
    public void Constructor_DuplicateRegistration_Throws()
    {
        var registry = CreateRegistry();
        _ = new EditorSearchViewModel(registry);
        Assert.Throws<InvalidOperationException>(() => new EditorSearchViewModel(registry));
    }

    // ── EditorSearchViewModel: Command metadata ──────────────────────────

    [Fact]
    public void FindCommand_HasCorrectMetadata()
    {
        var registry = CreateRegistry();
        _ = new EditorSearchViewModel(registry);

        var d = registry.GetById("editor.find");
        Assert.NotNull(d);
        Assert.Equal("Find", d!.DisplayName);
        Assert.Equal("Editor", d.Category);
        Assert.Equal(new[] { "Ctrl+F" }, d.DefaultGestures);
    }

    [Fact]
    public void ReplaceCommand_HasCorrectMetadata()
    {
        var registry = CreateRegistry();
        _ = new EditorSearchViewModel(registry);

        var d = registry.GetById("editor.replace");
        Assert.NotNull(d);
        Assert.Equal("Replace", d!.DisplayName);
        Assert.Equal(new[] { "Ctrl+H" }, d.DefaultGestures);
    }

    [Fact]
    public void FindNextCommand_HasCorrectMetadata()
    {
        var registry = CreateRegistry();
        _ = new EditorSearchViewModel(registry);

        var d = registry.GetById("editor.findNext");
        Assert.NotNull(d);
        Assert.Equal("Find Next", d!.DisplayName);
        Assert.Equal(new[] { "F3" }, d.DefaultGestures);
    }

    [Fact]
    public void FindPreviousCommand_HasCorrectMetadata()
    {
        var registry = CreateRegistry();
        _ = new EditorSearchViewModel(registry);

        var d = registry.GetById("editor.findPrevious");
        Assert.NotNull(d);
        Assert.Equal("Find Previous", d!.DisplayName);
        Assert.Equal(new[] { "Shift+F3" }, d.DefaultGestures);
    }

    [Fact]
    public void ReplaceNextCommand_IsUnbound()
    {
        var registry = CreateRegistry();
        _ = new EditorSearchViewModel(registry);

        var d = registry.GetById("editor.replaceNext");
        Assert.NotNull(d);
        Assert.Empty(d!.DefaultGestures);
    }

    [Fact]
    public void ReplaceAllCommand_IsUnbound()
    {
        var registry = CreateRegistry();
        _ = new EditorSearchViewModel(registry);

        var d = registry.GetById("editor.replaceAll");
        Assert.NotNull(d);
        Assert.Empty(d!.DefaultGestures);
    }

    // ── EditorSearchViewModel: Availability ──────────────────────────────

    [Fact]
    public void FindCommand_UnavailableWithoutActiveDocument()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);

        Assert.False(vm.FindCommand.CanExecute(null));
    }

    [Fact]
    public void FindCommand_AvailableWithActiveDocument()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello");

        Assert.True(vm.FindCommand.CanExecute(null));
    }

    [Fact]
    public void FindNextCommand_UnavailableWithEmptyQuery()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world");

        Assert.False(vm.FindNextCommand.CanExecute(null));
    }

    [Fact]
    public void FindNextCommand_UnavailableWithZeroMatches()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world");
        vm.Query = "xyz";

        Assert.False(vm.FindNextCommand.CanExecute(null));
    }

    [Fact]
    public void FindNextCommand_AvailableWithMatches()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world");
        vm.Query = "hello";

        Assert.True(vm.FindNextCommand.CanExecute(null));
    }

    [Fact]
    public void ReplaceNextCommand_UnavailableOutsideReplaceMode()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world");
        vm.Query = "hello";
        vm.IsReplaceMode = false;

        Assert.False(vm.ReplaceNextCommand.CanExecute(null));
    }

    [Fact]
    public void ReplaceNextCommand_AvailableInReplaceModeWithMatches()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world");
        vm.Query = "hello";
        vm.IsReplaceMode = true;

        Assert.True(vm.ReplaceNextCommand.CanExecute(null));
    }

    [Fact]
    public void ReplaceAllCommand_UnavailableOutsideReplaceMode()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world");
        vm.Query = "hello";
        vm.IsReplaceMode = false;

        Assert.False(vm.ReplaceAllCommand.CanExecute(null));
    }

    // ── EditorSearchViewModel: State management ──────────────────────────

    [Fact]
    public void CaseSensitive_DefaultsToTrue()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);

        Assert.True(vm.CaseSensitive);
    }

    [Fact]
    public void IsVisible_DefaultsFalse()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);

        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void IsReplaceMode_DefaultsFalse()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);

        Assert.False(vm.IsReplaceMode);
    }

    [Fact]
    public void Dismiss_ClearsAllState()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world");
        vm.Query = "hello";
        vm.IsReplaceMode = true;
        vm.IsVisible = true;

        vm.Dismiss();

        Assert.False(vm.IsVisible);
        Assert.False(vm.IsReplaceMode);
        Assert.Equal(string.Empty, vm.Query);
        Assert.Equal(string.Empty, vm.ReplacementText);
        Assert.Equal(0, vm.MatchCount);
        Assert.Equal(-1, vm.CurrentMatchIndex);
        Assert.Empty(vm.Matches);
    }

    [Fact]
    public void ActiveDocument_Change_ResetsSearchState()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        var doc1 = new MockEditorOps("hello world");
        var doc2 = new MockEditorOps("other text");

        vm.ActiveDocument = doc1;
        vm.Query = "hello";
        vm.IsVisible = true;
        vm.IsReplaceMode = true;
        Assert.True(vm.MatchCount > 0);

        // Switch to a different document
        vm.ActiveDocument = doc2;

        Assert.Equal(string.Empty, vm.Query);
        Assert.Equal(0, vm.MatchCount);
        Assert.False(vm.IsVisible);
        Assert.False(vm.IsReplaceMode);
    }

    [Fact]
    public void ActiveDocument_SetNull_ResetsSearchState()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello");
        vm.Query = "hello";
        vm.IsVisible = true;

        vm.ActiveDocument = null;

        Assert.Equal(string.Empty, vm.Query);
        Assert.Equal(0, vm.MatchCount);
        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void ActiveDocument_SetSameReference_DoesNotReset()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        var doc = new MockEditorOps("hello world");
        vm.ActiveDocument = doc;
        vm.Query = "hello";
        Assert.True(vm.MatchCount > 0);

        // Set same reference again
        vm.ActiveDocument = doc;

        // State should be preserved
        Assert.Equal("hello", vm.Query);
        Assert.True(vm.MatchCount > 0);
    }

    // ── EditorSearchViewModel: Find opens search ─────────────────────────

    [Fact]
    public void FindCommand_SetsIsVisibleAndClearsReplaceMode()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world");
        vm.IsReplaceMode = true;

        vm.FindCommand.Execute(null);

        Assert.True(vm.IsVisible);
        Assert.False(vm.IsReplaceMode);
    }

    [Fact]
    public void ReplaceCommand_SetsIsVisibleAndReplaceMode()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world");

        vm.ReplaceCommand.Execute(null);

        Assert.True(vm.IsVisible);
        Assert.True(vm.IsReplaceMode);
    }

    // ── EditorSearchViewModel: Query triggers search ─────────────────────

    [Fact]
    public void SettingQuery_PerformsSearch()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world hello");

        vm.Query = "hello";

        Assert.Equal(2, vm.MatchCount);
        Assert.Equal(0, vm.CurrentMatchIndex);
    }

    [Fact]
    public void SettingQuery_NoMatches_SetsStatusMessage()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world");

        vm.Query = "xyz";

        Assert.Equal(0, vm.MatchCount);
        Assert.Equal("No matches found", vm.StatusMessage);
    }

    [Fact]
    public void SettingQuery_Empty_ClearsMatches()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("hello world");
        vm.Query = "hello";
        Assert.True(vm.MatchCount > 0);

        vm.Query = "";

        Assert.Equal(0, vm.MatchCount);
        Assert.Equal(-1, vm.CurrentMatchIndex);
    }

    [Fact]
    public void CaseSensitive_Change_ReSearches()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new MockEditorOps("Hello hello HELLO");
        vm.Query = "hello";

        Assert.Equal(1, vm.MatchCount); // case-sensitive, only "hello"

        vm.CaseSensitive = false;

        Assert.Equal(3, vm.MatchCount); // case-insensitive, all three
    }

    // ── Stubs ────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal mock of <see cref="IEditorTextOperations"/> for VM-level tests.
    /// </summary>
    private sealed class MockEditorOps : IEditorTextOperations
    {
        private string _text;
        private int _selOffset;
        private int _selLength;
        private readonly List<string> _undoStack = new();

        public MockEditorOps(string text)
        {
            _text = text;
        }

        public string Text => _text;
        public int UndoGroupCount => _undoStack.Count;

        public string GetText() => _text;

        public void SetText(string text)
        {
            _undoStack.Add(_text);
            _text = text;
            _selOffset = 0;
            _selLength = 0;
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
            var newText = SearchEngine.ReplaceAll(_text, query, replacement, caseSensitive);
            if (newText == _text) return 0;
            _undoStack.Add(_text);
            _text = newText;
            _selOffset = 0;
            _selLength = 0;
            return SearchEngine.FindAll(_text, replacement, caseSensitive).Count;
        }

        public void Undo()
        {
            if (_undoStack.Count > 0)
            {
                _text = _undoStack[^1];
                _undoStack.RemoveAt(_undoStack.Count - 1);
            }
        }
    }
}
