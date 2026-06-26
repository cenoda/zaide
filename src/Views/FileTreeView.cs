using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Models;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// File tree sidebar view. Builds a TreeView bound to FileTreeViewModel.RootNodes.
/// Header shows current folder path; click to change folder. No "Open Folder" button.
/// Colors pulled from App.axaml resources. Built in C# per DESIGN.md §1.
/// </summary>
public partial class FileTreeView : ReactiveUserControl<FileTreeViewModel>
{
    private readonly TreeView _treeView;
    private readonly TextBlock _headerText;

    public FileTreeView()
    {
        Padding = new Thickness(16);

        // --- Header (clickable folder path) ---
        _headerText = new TextBlock
        {
            Text = "Open Folder...",
            FontSize = 13,
            Foreground = (IBrush?)Application.Current!.Resources["SoftAccent"],
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(0, 0, 0, 8)
        };

        _headerText.PointerPressed += async (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { AllowMultiple = false });

            if (folders.Count > 0)
                ViewModel!.OpenFolderCommand.Execute(folders[0].Path.LocalPath).Subscribe(_ => { });
        };

        // --- TreeView ---
        _treeView = new TreeView
        {
            ItemTemplate = new FuncTreeDataTemplate<FileTreeNode>(
                match: _ => true,
                build: (node, _) => new TextBlock
                {
                    Text = node.Name,
                    Foreground = (IBrush?)Application.Current!.Resources["TextActive"]
                },
                itemsSelector: node => node.Children
            )
        };

        _treeView.SelectionChanged += (_, e) =>
        {
            if (e.AddedItems.Count > 0)
                ViewModel!.SelectedFile = e.AddedItems[0] as FileTreeNode;
            else
                ViewModel!.SelectedFile = null;
        };

        // --- Layout ---
        var headerBorder = new Border
        {
            Child = _headerText,
            [DockPanel.DockProperty] = Dock.Top,
            Padding = new Thickness(0, 0, 0, 4),
            BorderBrush = (IBrush?)Application.Current!.Resources["SoftAccent"],
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        Background = (IBrush?)Application.Current!.Resources["DeepBase"];

        Content = new DockPanel
        {
            Children = { headerBorder, _treeView }
        };

        // --- Reactive bindings ---
        this.WhenActivated(d =>
        {
            d.Add(this.OneWayBind(ViewModel, vm => vm.RootNodes, v => v._treeView.ItemsSource));

            // Header: show path when set, fall back to "Open Folder..."
            d.Add(this.WhenAnyValue(x => x.ViewModel!.RootPath)
                .Subscribe(path =>
                {
                    _headerText.Text = string.IsNullOrEmpty(path)
                        ? "Open Folder..."
                        : path;
                }));
        });
    }
}
