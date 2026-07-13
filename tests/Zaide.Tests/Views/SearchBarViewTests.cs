using System;
using Avalonia;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Views;

namespace Zaide.Tests.Views;

/// <summary>
/// Phase 9 M3: View-level regression tests for the <see cref="SearchBar"/>.
/// Proves that the search surface becomes visible when Find/Replace commands
/// execute, and that Dismiss hides it. These are the defects that were not
/// caught by ViewModel-only tests.
/// </summary>
public sealed class SearchBarViewTests
{
    static SearchBarViewTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static ICommandRegistry CreateRegistry() => CommandRegistryFactory.Create();

    // ── Visibility binding ───────────────────────────────────────────────

    [Fact]
    public void SearchBar_InitiallyHidden_BecauseViewModelIsVisibleIsFalse()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        var bar = new SearchBar(vm);

        Assert.False(vm.IsVisible);
        Assert.False(bar.IsVisible);
    }

    [Fact]
    public void FindCommand_MakesSearchBarVisible()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new FakeEditorOps("hello world");
        var bar = new SearchBar(vm);

        Assert.False(bar.IsVisible);

        vm.FindCommand.Execute(null);

        Assert.True(vm.IsVisible);
        Assert.True(bar.IsVisible);
    }

    [Fact]
    public void ReplaceCommand_MakesSearchBarVisible()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new FakeEditorOps("hello world");
        var bar = new SearchBar(vm);

        Assert.False(bar.IsVisible);

        vm.ReplaceCommand.Execute(null);

        Assert.True(vm.IsVisible);
        Assert.True(bar.IsVisible);
    }

    [Fact]
    public void Dismiss_HidesSearchBar()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new FakeEditorOps("hello world");
        var bar = new SearchBar(vm);

        vm.FindCommand.Execute(null);
        Assert.True(bar.IsVisible);

        vm.Dismiss();

        Assert.False(vm.IsVisible);
        Assert.False(bar.IsVisible);
    }

    // ── Focus management ─────────────────────────────────────────────────

    [Fact]
    public void FindCommand_RaisesFocusRequested()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new FakeEditorOps("hello world");
        var bar = new SearchBar(vm);

        var focusRaised = false;
        vm.FocusRequested += () => focusRaised = true;

        vm.FindCommand.Execute(null);

        Assert.True(focusRaised);
    }

    [Fact]
    public void ReplaceCommand_RaisesFocusRequested()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new FakeEditorOps("hello world");
        var bar = new SearchBar(vm);

        var focusRaised = false;
        vm.FocusRequested += () => focusRaised = true;

        vm.ReplaceCommand.Execute(null);

        Assert.True(focusRaised);
    }

    [Fact]
    public void FocusQuery_DoesNotThrow_WhenBarIsNotInVisualTree()
    {
        // FocusQuery calls _queryBox.Focus() and _queryBox.SelectAll().
        // Without a visual tree, Focus() is a no-op but must not throw.
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        var bar = new SearchBar(vm);

        var exception = Record.Exception(() => bar.FocusQuery());
        Assert.Null(exception);
    }

    // ── Replace panel visibility ─────────────────────────────────────────

    [Fact]
    public void ReplacePanel_Hidden_InFindMode()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new FakeEditorOps("hello world");
        var bar = new SearchBar(vm);

        vm.FindCommand.Execute(null);

        Assert.True(bar.IsVisible);
        Assert.False(vm.IsReplaceMode);
    }

    [Fact]
    public void ReplacePanel_Shown_InReplaceMode()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new FakeEditorOps("hello world");
        var bar = new SearchBar(vm);

        vm.ReplaceCommand.Execute(null);

        Assert.True(bar.IsVisible);
        Assert.True(vm.IsReplaceMode);
    }

    // ── Tab switch hides search bar ──────────────────────────────────────

    [Fact]
    public void TabSwitch_HidesSearchBar_ViaActiveDocumentIdReset()
    {
        var registry = CreateRegistry();
        var vm = new EditorSearchViewModel(registry);
        vm.ActiveDocument = new FakeEditorOps("hello world");
        vm.ActiveDocumentId = "/file1.cs";
        var bar = new SearchBar(vm);

        vm.FindCommand.Execute(null);
        Assert.True(bar.IsVisible);

        // Simulate tab switch: change ActiveDocumentId
        vm.ActiveDocumentId = "/file2.cs";

        Assert.False(vm.IsVisible);
        Assert.False(bar.IsVisible);
    }

    // ── Stubs ────────────────────────────────────────────────────────────

    private sealed class FakeEditorOps : IEditorTextOperations
    {
        private string _text;
        private int _selOffset;
        private int _selLength;

        public FakeEditorOps(string text) => _text = text;

        public string GetText() => _text;
        public void SetText(string text) => _text = text;
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
            _text = newText;
            return SearchEngine.FindAll(_text, replacement, caseSensitive).Count;
        }
    }
}
