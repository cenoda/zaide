using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Zaide.Features.Editor.Presentation;

namespace Zaide.App.Shell;

/// <summary>
/// View-side command-palette and editor-search focus wiring extracted from
/// <see cref="MainWindow"/> activation to reduce <c>WhenActivated</c> pressure.
/// </summary>
internal static class ShellOverlayFocusWiring
{
    public static void Wire(
        CompositeDisposable disposables,
        CommandPaletteViewModel paletteViewModel,
        CommandPaletteOverlay commandPaletteOverlay,
        EditorSearchViewModel searchViewModel,
        SearchBar searchBar,
        EditorView editorView,
        MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(disposables);
        ArgumentNullException.ThrowIfNull(paletteViewModel);
        ArgumentNullException.ThrowIfNull(commandPaletteOverlay);
        ArgumentNullException.ThrowIfNull(searchViewModel);
        ArgumentNullException.ThrowIfNull(searchBar);
        ArgumentNullException.ThrowIfNull(editorView);
        ArgumentNullException.ThrowIfNull(viewModel);

        void OnPaletteOpenRequested() => commandPaletteOverlay.Show();
        paletteViewModel.OpenRequested += OnPaletteOpenRequested;
        disposables.Add(Disposable.Create(() =>
            paletteViewModel.OpenRequested -= OnPaletteOpenRequested));

        void OnOverlayDismissed()
        {
            commandPaletteOverlay.Hide();
            RestoreFocusAfterPalette(viewModel, editorView);
        }

        commandPaletteOverlay.Dismissed += OnOverlayDismissed;
        disposables.Add(Disposable.Create(() =>
            commandPaletteOverlay.Dismissed -= OnOverlayDismissed));

        void OnSearchFocusRequested() => searchBar.FocusQuery();
        searchViewModel.FocusRequested += OnSearchFocusRequested;
        disposables.Add(Disposable.Create(() =>
            searchViewModel.FocusRequested -= OnSearchFocusRequested));

        void OnSearchSelectionUpdated() => searchBar.FocusQueryWithoutSelectAll();
        searchViewModel.SelectionUpdated += OnSearchSelectionUpdated;
        disposables.Add(Disposable.Create(() =>
            searchViewModel.SelectionUpdated -= OnSearchSelectionUpdated));

        disposables.Add(searchViewModel.WhenAnyValue(x => x.IsVisible)
            .Subscribe(visible =>
            {
                if (!visible && editorView.IsVisible)
                    editorView.Focus();
            }));
    }

    internal static void RestoreFocusAfterPalette(
        MainWindowViewModel? viewModel,
        EditorView editorView)
    {
        var activeTab = viewModel?.EditorTabs.ActiveTab;
        if (activeTab is not null && editorView.IsVisible)
            editorView.Focus();
    }
}
