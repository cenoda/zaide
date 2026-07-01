using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
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
        Padding = new Thickness(0);

        // --- Section header: "EXPLORER" + close button (matches concept) ---
        var sectionLabel = new TextBlock
        {
            Text = "EXPLORER",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };

        var closeButton = new TextBlock
        {
            Text = "×",
            FontSize = 14,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Height = 32,
            Background = Brushes.Transparent
        };
        Grid.SetColumn(sectionLabel, 0);
        Grid.SetColumn(closeButton, 1);
        headerRow.Children.Add(sectionLabel);
        headerRow.Children.Add(closeButton);

        // --- Folder path (clickable) ---
        _headerText = new TextBlock
        {
            Text = "Open Folder...",
            FontSize = 12,
            Foreground = (IBrush?)Application.Current!.Resources["TextSecondary"],
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(12, 0, 12, 6)
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

        // M1: New File / New Folder
        contextMenu.Items.Add(new Separator());
        var newFileItem = new MenuItem { Header = "New File" };
        newFileItem.Click += async (_, _) =>
        {
            var parentDir = GetParentDirForCreation();
            if (parentDir is null) return;

            var name = await ShowNamePromptAsync("New File", "Enter file name:");
            if (string.IsNullOrWhiteSpace(name)) return;

            ViewModel!.CreateNodeCommand
                .Execute((parentDir, name, false))
                .Subscribe();
        };
        contextMenu.Items.Add(newFileItem);

        var newFolderItem = new MenuItem { Header = "New Folder" };
        newFolderItem.Click += async (_, _) =>
        {
            var parentDir = GetParentDirForCreation();
            if (parentDir is null) return;

            var name = await ShowNamePromptAsync("New Folder", "Enter folder name:");
            if (string.IsNullOrWhiteSpace(name)) return;

            ViewModel!.CreateNodeCommand
                .Execute((parentDir, name, true))
                .Subscribe();
        };
        contextMenu.Items.Add(newFolderItem);

        // M2: Show Hidden Files toggle
        contextMenu.Items.Add(new Separator());
        var showHiddenItem = new MenuItem
        {
            Header = "Show Hidden Files",
            ToggleType = MenuItemToggleType.CheckBox
        };
        // Bind IsChecked to ShowHiddenFiles via WhenActivated below
        showHiddenItem.Click += (_, _) => ViewModel!.ToggleHiddenFilesCommand.Execute().Subscribe();
        contextMenu.Items.Add(showHiddenItem);

        // M3: Copy Path / Copy Relative Path
        contextMenu.Items.Add(new Separator());
        var copyPathItem = new MenuItem { Header = "Copy Path" };
        copyPathItem.Click += (_, _) =>
        {
            if (_treeView.SelectedItem is FileTreeNode selected)
                ViewModel!.CopyPathCommand.Execute(selected).Subscribe();
        };
        contextMenu.Items.Add(copyPathItem);

        var copyRelativePathItem = new MenuItem { Header = "Copy Relative Path" };
        copyRelativePathItem.Click += (_, _) =>
        {
            if (_treeView.SelectedItem is FileTreeNode selected)
                ViewModel!.CopyRelativePathCommand.Execute(selected).Subscribe();
        };
        contextMenu.Items.Add(copyRelativePathItem);

        _treeView.ContextFlyout = contextMenu;

        _treeView.SelectionChanged += (_, e) =>
        {
            if (e.AddedItems.Count > 0)
                ViewModel!.SelectedFile = e.AddedItems[0] as FileTreeNode;
            else
                ViewModel!.SelectedFile = null;
        };

        // --- Layout ---
        var headerRowHost = new Border
        {
            Child = headerRow,
            [DockPanel.DockProperty] = Dock.Top
        };

        var folderPathHost = new Border
        {
            Child = _headerText,
            [DockPanel.DockProperty] = Dock.Top
        };

        var separator = new Border
        {
            Height = 1,
            [DockPanel.DockProperty] = Dock.Top,
            Background = (IBrush?)Application.Current!.Resources["SurfaceBorder"]
        };

        Background = (IBrush?)Application.Current!.Resources["GlassBase"];

        Content = new DockPanel
        {
            Children = { headerRowHost, separator, folderPathHost, _treeView }
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

            // M2: Bind Show Hidden Files menu item check state
            // Uses WhenAnyValue + Subscribe instead of OneWayBind because
            // showHiddenItem is a local variable, not a view member expression.
            d.Add(this.WhenAnyValue(x => x.ViewModel!.ShowHiddenFiles)
                .Subscribe(isChecked => showHiddenItem.IsChecked = isChecked));

            // M3: Register clipboard copy handler via Interaction
            d.Add(ViewModel!.CopyToClipboard.RegisterHandler(async interaction =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard is { } clipboard)
                {
                    await clipboard.SetTextAsync(interaction.Input);
                }
                interaction.SetOutput(Unit.Default);
            }));

            // M3: Disable Copy Relative Path when no folder is open
            d.Add(this.WhenAnyValue(x => x.ViewModel!.RootPath)
                .Subscribe(path => copyRelativePathItem.IsEnabled = path is not null));

            // M5: Dispose of event handlers and subscriptions
            d.Add(Disposable.Create(() => _openFolderSubscription?.Dispose()));
        });

        _treeView.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            // M2: Ctrl+Shift+H — toggle hidden files
            if (e.Key == Key.H && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                e.Handled = true;
                ViewModel!.ToggleHiddenFilesCommand.Execute().Subscribe();
                return;
            }

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

    /// <summary>
    /// Determines the parent directory for new file/folder creation.
    /// If right-clicked on a directory node, uses that; otherwise falls back to RootPath.
    /// Returns null when no folder is open.
    /// </summary>
    private string? GetParentDirForCreation()
    {
        if (ViewModel is null) return null;

        if (_treeView.SelectedItem is FileTreeNode selected && selected.IsDirectory)
            return selected.FullPath;

        return ViewModel.RootPath;
    }

    /// <summary>
    /// Shows a simple modal dialog with a TextBox for name input.
    /// Returns the entered text, or null if cancelled.
    /// </summary>
    private async Task<string?> ShowNamePromptAsync(string title, string prompt)
    {
        var textBox = new TextBox
        {
            PlaceholderText = prompt,
            MinWidth = 300,
            Margin = new Thickness(8)
        };

        var okButton = new Button { Content = "OK", Margin = new Thickness(4) };
        var cancelButton = new Button { Content = "Cancel", Margin = new Thickness(4) };
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 8, 8),
            Children = { okButton, cancelButton }
        };

        var stackPanel = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = prompt, Margin = new Thickness(8, 8, 8, 0) },
                textBox,
                buttonPanel
            }
        };

        var window = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = stackPanel,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var tcs = new TaskCompletionSource<string?>();
        okButton.Click += (_, _) =>
        {
            tcs.TrySetResult(textBox.Text);
            window.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            tcs.TrySetResult(null);
            window.Close();
        };

        // Allow Enter to confirm
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                tcs.TrySetResult(textBox.Text);
                window.Close();
            }
        };

        // Allow Escape to cancel
        window.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                tcs.TrySetResult(null);
                window.Close();
            }
        };

        // Prevent hang if user closes via title bar close button
        window.Closed += (_, _) => tcs.TrySetResult(null);

        textBox.AttachedToVisualTree += (_, _) => textBox.Focus();

        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        if (parentWindow is not null)
            await window.ShowDialog(parentWindow);
        else
            window.Show();

        return await tcs.Task;
    }
}