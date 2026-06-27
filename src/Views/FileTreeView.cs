using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
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
    private IDisposable? _openFolderSubscription;

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
                _openFolderSubscription = ViewModel!.OpenFolderCommand
                    .Execute(folders[0].Path.LocalPath)
                    .Subscribe(_ => { });
        };

        // --- TreeView with IsExpanded binding and Context Menu (M3) ---
        _treeView = new TreeView
        {
            ItemTemplate = new FuncTreeDataTemplate<FileTreeNode>(
                match: _ => true,
                build: (node, _) =>
                {
                    var tb = new TextBlock
                    {
                        Text = node.Name,
                        Foreground = (IBrush?)Application.Current!.Resources["TextActive"]
                    };

                    // M3: Bind TreeViewItem.IsExpanded ↔ FileTreeNode.IsExpanded (two-way)
                    tb.AttachedToVisualTree += (_, _) =>
                    {
                        var tvi = tb.FindAncestorOfType<TreeViewItem>();
                        if (tvi is not null)
                        {
                            var binding = new Binding(nameof(FileTreeNode.IsExpanded))
                            {
                                Mode = BindingMode.TwoWay
                            };
                            tvi.Bind(TreeViewItem.IsExpandedProperty, binding);
                        }
                    };

                    return tb;
                },
                itemsSelector: node => node.Children
            )
        };

        // M3: Context Menu for tree nodes
        var contextMenu = new MenuFlyout();
        var openItem = new MenuItem { Header = "Open" };
        openItem.Click += (_, _) =>
        {
            if (_treeView.SelectedItem is FileTreeNode selected && !selected.IsDirectory)
                ViewModel!.RequestOpenFileCommand.Execute(selected).Subscribe();
        };

        var expandAllItem = new MenuItem { Header = "Expand All" };
        expandAllItem.Click += (_, _) => ViewModel!.ExpandAllCommand.Execute().Subscribe();

        var collapseAllItem = new MenuItem { Header = "Collapse All" };
        collapseAllItem.Click += (_, _) => ViewModel!.CollapseAllCommand.Execute().Subscribe();

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(expandAllItem);
        contextMenu.Items.Add(collapseAllItem);
        _treeView.ContextFlyout = contextMenu;

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

            // M5: Dispose of event handlers and subscriptions
            d.Add(Disposable.Create(() => _openFolderSubscription?.Dispose()));
        });

        _treeView.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            var selected = ViewModel!.SelectedFile;
            if (selected is null || selected.IsDirectory) return;
            e.Handled = true;
            ViewModel!.RequestOpenFileCommand.Execute(selected).Subscribe();
        }, handledEventsToo: true);

        // M4: Double-click to open file (VS Code convention)
        _treeView.DoubleTapped += (_, _) =>
        {
            var selected = ViewModel!.SelectedFile;
            if (selected is null || selected.IsDirectory) return;
            ViewModel!.RequestOpenFileCommand.Execute(selected).Subscribe();
        };
    }
}