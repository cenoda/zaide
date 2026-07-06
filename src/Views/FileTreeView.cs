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
using Zaide.Styles;

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
    private readonly Control _headerIcon;
    private IDisposable? _openFolderSubscription;

    public FileTreeView()
    {
        Padding = new Thickness(16);

        // --- Header (clickable folder path) ---
        _headerIcon = IconFactory.Create(
            "Icon.Folder",
            (IBrush?)Application.Current!.Resources["SecondaryAccentBrush"],
            14);

        _headerText = TextStyles.Caption("Open Folder...");
        _headerText.Cursor = new Cursor(StandardCursorType.Hand);
        _headerText.Margin = new Thickness(6, 0, 0, 8);
        _headerText.VerticalAlignment = VerticalAlignment.Center;

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
                    var icon = IconFactory.Create(
                        FileIconKeyResolver.GetIconKey(node.Name, node.IsDirectory),
                        (IBrush?)Application.Current!.Resources[
                            node.IsDirectory ? "SecondaryAccentBrush" : "TextSecondaryBrush"],
                        14);

                    var tb = new TextBlock
                    {
                        Text = node.Name,
                        Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var row = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children = { icon, tb }
                    };

                    // M3: Bind TreeViewItem.IsExpanded ↔ FileTreeNode.IsExpanded (two-way)
                    row.AttachedToVisualTree += (_, _) =>
                    {
                        var tvi = row.FindAncestorOfType<TreeViewItem>();
                        if (tvi is not null)
                        {
                            var binding = new Binding(nameof(FileTreeNode.IsExpanded))
                            {
                                Mode = BindingMode.TwoWay
                            };
                            tvi.Bind(TreeViewItem.IsExpandedProperty, binding);
                        }
                    };

                    return row;
                },
                itemsSelector: node => node.Children
            )
        };

        // M1.5: Helper to create a MenuItem with SurfaceRaisedBrush background.
        MenuItem CreateStyledMenuItem(string header)
        {
            return new MenuItem
            {
                Header = header,
                Background = (IBrush?)Application.Current!.Resources["SurfaceRaisedBrush"]
            };
        }

        // M3: Context Menu for tree nodes
        // M1.5: All MenuItems get SurfaceRaisedBrush background so the popup reads
        // as an elevated layer against SurfacePanelBrush underneath.
        // MenuFlyout popup blur is platform-dependent and out of scope; the
        // solid-background MenuItems ensure the menu looks intentional without blur.
        var contextMenu = new MenuFlyout();
        var openItem = CreateStyledMenuItem("Open");
        openItem.Click += (_, _) =>
        {
            if (_treeView.SelectedItem is FileTreeNode selected && !selected.IsDirectory)
                ViewModel!.RequestOpenFileCommand.Execute(selected).Subscribe();
        };

        var expandAllItem = CreateStyledMenuItem("Expand All");
        expandAllItem.Click += (_, _) => ViewModel!.ExpandAllCommand.Execute().Subscribe();

        var collapseAllItem = CreateStyledMenuItem("Collapse All");
        collapseAllItem.Click += (_, _) => ViewModel!.CollapseAllCommand.Execute().Subscribe();

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(expandAllItem);
        contextMenu.Items.Add(collapseAllItem);

        // M1: New File / New Folder
        contextMenu.Items.Add(new Separator());
        var newFileItem = CreateStyledMenuItem("New File");
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

        var newFolderItem = CreateStyledMenuItem("New Folder");
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
        var showHiddenItem = CreateStyledMenuItem("Show Hidden Files");
        showHiddenItem.ToggleType = MenuItemToggleType.CheckBox;
        // Bind IsChecked to ShowHiddenFiles via WhenActivated below
        showHiddenItem.Click += (_, _) => ViewModel!.ToggleHiddenFilesCommand.Execute().Subscribe();
        contextMenu.Items.Add(showHiddenItem);

        // M3: Copy Path / Copy Relative Path
        contextMenu.Items.Add(new Separator());
        var copyPathItem = CreateStyledMenuItem("Copy Path");
        copyPathItem.Click += (_, _) =>
        {
            if (_treeView.SelectedItem is FileTreeNode selected)
                ViewModel!.CopyPathCommand.Execute(selected).Subscribe();
        };
        contextMenu.Items.Add(copyPathItem);

        var copyRelativePathItem = CreateStyledMenuItem("Copy Relative Path");
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
        var headerBorder = new Border
        {
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _headerIcon, _headerText }
            },
            [DockPanel.DockProperty] = Dock.Top,
            Padding = new Thickness(0, 0, 0, 4),
            BorderBrush = (IBrush?)Application.Current!.Resources["SecondaryAccentBrush"],
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        Background = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"];

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
