using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
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
        Padding = LayoutTokens.Uniform(LayoutTokens.SpacingLg);

        // --- Header (clickable folder path) ---
        _headerIcon = IconFactory.Create(
            "Icon.Folder",
            (IBrush?)Application.Current!.Resources["SecondaryAccentBrush"],
            14);

        _headerText = TextStyles.Caption("Open Folder...");
        _headerText.Cursor = new Cursor(StandardCursorType.Hand);
        _headerText.Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs, 0, 0, LayoutTokens.SpacingSm);
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

        // M3.1: per-category brush mapping for the file tree.
        // Folders are kept muted (chrome). Each file category gets a
        // distinct accent so the tree reads as colored, not monochrome.
        // The fallback for unknown extensions is the muted TextSecondary.
        // Per M3.1, the resolver returns Icon.Unknown (no Icon.File key exists).
        IBrush? BrushForIconKey(string iconKey)
        {
            return iconKey switch
            {
                "Icon.Folder"  => (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
                "Icon.Code"    => (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"],
                "Icon.Text"    => (IBrush?)Application.Current!.Resources["SecondaryAccentBrush"],
                "Icon.Image"   => (IBrush?)Application.Current!.Resources["WarningBrush"],
                "Icon.Config"  => (IBrush?)Application.Current!.Resources["IdleBrush"],
                "Icon.Markup"  => (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"],
                "Icon.Project" => (IBrush?)Application.Current!.Resources["SuccessBrush"],
                _              => (IBrush?)Application.Current!.Resources["TextSecondaryBrush"] // Icon.Unknown + any future keys
            };
        }

        // M3.2 / M3.3: capture the active brushes and helper closure.
        // The row builder uses these to paint the active-file 2px left
        // border (M3.3), the subtle parent-folder background (M3.3),
        // and the hover background (M3.2). The selection state is
        // read directly from the view-model on each repaint, so it
        // stays in sync without a per-row subscription.
        var activeBrush = (IBrush?)Application.Current!.Resources["PrimaryAccentBrush"];
        var activeBgBrush = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.FromArgb(0x15, 0x06, 0x6A, 0xDB));
        var parentFolderBgBrush = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.FromArgb(0x08, 0x06, 0x6A, 0xDB));
        var defaultRowBrush = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"];
        var hoverBrush = (IBrush?)Application.Current!.Resources["SurfaceRaisedBrush"];

        // M3.3: local helper that paints or unpaints a row's active state
        // based on the current view-model selection. We expose it as a
        // closure so the row builder can call it from hover-exit and
        // attached-to-visual-tree handlers without re-implementing the
        // branch in two places.
        void PaintRowForSelection(Border row, Border activeStrip, FileTreeNode thisNode)
        {
            var selected = ViewModel?.SelectedFile;
            if (selected is null)
            {
                activeStrip.Background = Avalonia.Media.Brushes.Transparent;
                row.Background = defaultRowBrush;
                return;
            }

            // The active file row: 2px PrimaryAccent left strip + tinted bg.
            if (ReferenceEquals(selected, thisNode))
            {
                activeStrip.Background = activeBrush;
                row.Background = activeBgBrush;
                return;
            }

            // The parent folder of the active file: subtle tint, no left strip.
            if (thisNode.IsDirectory && !string.IsNullOrEmpty(selected.FullPath)
                && selected.FullPath.StartsWith(thisNode.FullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                activeStrip.Background = Avalonia.Media.Brushes.Transparent;
                row.Background = parentFolderBgBrush;
                return;
            }

            // Default: no decoration.
            activeStrip.Background = Avalonia.Media.Brushes.Transparent;
            row.Background = defaultRowBrush;
        }

        // M3.2 / M3.3 / M3.4: Build the row visual: indent guides (M3.4),
        // 2px accent left border on the active row (M3.3), and hover
        // background (M3.2). The previous row was a flat StackPanel with
        // a single child group; the new row is a Border that hosts the
        // icon+label content so we can layer the per-state decorations.
        // --- TreeView with IsExpanded binding and Context Menu (M3) ---
        _treeView = new TreeView
        {
            ItemTemplate = new FuncTreeDataTemplate<FileTreeNode>(
                match: _ => true,
                build: (node, _) =>
                {
                    var iconKey = FileIconKeyResolver.GetIconKey(node.Name, node.IsDirectory);
                    var iconBrush = BrushForIconKey(iconKey);
                    var icon = IconFactory.Create(iconKey, iconBrush, 14);

                    var tb = new TextBlock
                    {
                        Text = node.Name,
                        Foreground = (IBrush?)Application.Current!.Resources["TextPrimaryBrush"],
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = LayoutTokens.SpacingSm - LayoutTokens.SpacingXxs,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children = { icon, tb }
                    };

                    // M3.4: indent guide visual. We render one thin vertical
                    // 1px SeparatorBrush Border per level of nesting. They sit
                    // *inside* the row's left padding, not outside, so they
                    // shift with the row's Margin.Left (the Avalonia TreeView
                    // itself supplies the expand/collapse chevron indent).
                    var indentStrip = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Spacing = LayoutTokens.SpacingNone
                    };
                    // M3.4: pull the spacing token into a typed local so
                    // we do not unbox a possibly-null object.
                    var spacingSm = Application.Current!.Resources.TryGetValue("SpacingSm", out var sm)
                        ? Convert.ToDouble(sm)
                        : 8d;
                    for (var i = 0; i < node.Depth; i++)
                    {
                        indentStrip.Children.Add(new Border
                        {
                            Width = 1,
                            Background = (IBrush?)Application.Current!.Resources["SeparatorBrush"],
                            // M5-allow: The 1px compensation keeps the 1px guide centered inside an 8px depth slot.
                            Margin = LayoutTokens.Inset(0, LayoutTokens.SpacingXxs, spacingSm - 1, LayoutTokens.SpacingXxs)
                        });
                    }

                    // M3.3: 2px PrimaryAccent left border on the active file row.
                    // We use a Border (left strip) that is shown only when this
                    // node is the currently selected file. Subtle, not garish.
                    var activeStrip = new Border
                    {
                        Width = 2,
                        Background = Avalonia.Media.Brushes.Transparent,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };

                    var rowGrid = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Auto), // active strip
                            new ColumnDefinition(GridLength.Auto), // indent guides
                            new ColumnDefinition(GridLength.Star)  // icon + text
                        },
                        Margin = LayoutTokens.NoneThickness
                    };
                    Grid.SetColumn(activeStrip, 0);
                    Grid.SetColumn(indentStrip, 1);
                    Grid.SetColumn(content, 2);
                    rowGrid.Children.Add(activeStrip);
                    rowGrid.Children.Add(indentStrip);
                    rowGrid.Children.Add(content);

                    // M3.3: subtle parent-folder treatment — a slightly brighter
                    // background on the folder that contains the active file.
                    var rowBackground = (IBrush?)Application.Current!.Resources["SurfaceBaseBrush"];
                    var rowBorder = new Border
                    {
                        Background = rowBackground,
                        Padding = LayoutTokens.Inset(LayoutTokens.SpacingXs, LayoutTokens.SpacingXxs, LayoutTokens.SpacingSm, LayoutTokens.SpacingXxs),
                        Child = rowGrid
                    };
                    // M3.2: 150ms brush transition on the row's Background.
                    // Per the M3.2 spec, the hover fade must be 150ms
                    // (DESIGN.md §4 / M6.2 single animation budget). A
                    // BrushTransition on the Border's Background animates
                    // automatically when the hover handler swaps the brush.
                    // This is the standard Avalonia path — no custom
                    // animation helper is required, and it keeps the fade
                    // in the M6 budget when the helper lands in M6.
                    rowBorder.Transitions = new Transitions
                    {
                        new BrushTransition
                        {
                            Property = Border.BackgroundProperty,
                            Duration = TimeSpan.FromMilliseconds(150),
                            Easing = new CubicEaseOut()
                        }
                    };
                    // M3.3: tag the row so RepaintAllFileTreeRows can find
                    // it by walking the visual tree. The Tag holds the
                    // FileTreeNode so the repaint can map each row to its
                    // selection state without a separate data structure.
                    rowBorder.Tag = node;

                    // M3.2: PointerEntered / Exited handlers swap the row's
                    // Background brush. On enter, paint the hover background.
                    // On exit, restore the selection-aware paint.
                    rowBorder.PointerEntered += (_, _) =>
                    {
                        rowBorder.Background = hoverBrush;
                    };
                    rowBorder.PointerExited += (_, _) =>
                    {
                        // Restore the active-row paint: re-evaluates against
                        // the current view-model selection state.
                        PaintRowForSelection(rowBorder, activeStrip, node);
                    };

                    // M3.3 / M3.4: After the row is added to the visual tree,
                    // wire up the IsExpanded binding and the active-row paint.
                    // We re-evaluate the selection paint here so re-attached
                    // rows (after scroll) repaint with the current selection.
                    rowBorder.AttachedToVisualTree += (_, _) =>
                    {
                        var tvi = rowBorder.FindAncestorOfType<TreeViewItem>();
                        if (tvi is not null)
                        {
                            var binding = new Binding(nameof(FileTreeNode.IsExpanded))
                            {
                                Mode = BindingMode.TwoWay
                            };
                            tvi.Bind(TreeViewItem.IsExpandedProperty, binding);
                        }

                        // Apply the active-row paint now that the row is in
                        // the visual tree. Reads ViewModel.SelectedFile fresh.
                        PaintRowForSelection(rowBorder, activeStrip, node);
                    };

                    return rowBorder;
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
            Padding = LayoutTokens.Inset(0, 0, 0, LayoutTokens.SpacingXs),
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

            // M3.3: When the view-model selection changes, repaint every
            // visible row. The row paint reads ViewModel.SelectedFile
            // directly, so re-attached rows catch up automatically; this
            // subscription keeps the currently-visible rows in sync.
            d.Add(this.WhenAnyValue(x => x.ViewModel!.SelectedFile)
                .Subscribe(_ => RepaintAllFileTreeRows()));

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
    /// M3.3: Walks the visible tree, finds each row Border (tagged with
    /// its <see cref="FileTreeNode"/>), and re-applies the selection-aware
    /// paint. Called from the <c>SelectedFile</c> subscription in
    /// <see cref="WhenActivated"/> so the active-row decoration stays in
    /// sync when the selection changes from outside the view (e.g., an
    /// external file open).
    /// </summary>
    private void RepaintAllFileTreeRows()
    {
        if (_treeView is null) return;
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        var activeBrush = (IBrush?)resources["PrimaryAccentBrush"];
        var activeBg = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.FromArgb(0x15, 0x06, 0x6A, 0xDB));
        var parentBg = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.FromArgb(0x08, 0x06, 0x6A, 0xDB));
        var defaultBg = (IBrush?)resources["SurfaceBaseBrush"];

        var selected = ViewModel?.SelectedFile;

        foreach (var row in _treeView.GetVisualDescendants().OfType<Border>())
        {
            if (row.Tag is not FileTreeNode node) continue;
            // The row's first child is the rowGrid whose first column is activeStrip.
            if (row.Child is not Grid grid || grid.Children.Count < 3) continue;
            if (grid.Children[0] is not Border activeStrip) continue;

            if (selected is null)
            {
                activeStrip.Background = Avalonia.Media.Brushes.Transparent;
                row.Background = defaultBg;
                continue;
            }

            if (ReferenceEquals(selected, node))
            {
                activeStrip.Background = activeBrush;
                row.Background = activeBg;
                continue;
            }

            if (node.IsDirectory && !string.IsNullOrEmpty(selected.FullPath)
                && selected.FullPath.StartsWith(node.FullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                activeStrip.Background = Avalonia.Media.Brushes.Transparent;
                row.Background = parentBg;
                continue;
            }

            activeStrip.Background = Avalonia.Media.Brushes.Transparent;
            row.Background = defaultBg;
        }
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
            Margin = LayoutTokens.Uniform(LayoutTokens.SpacingSm)
        };

        var okButton = new Button { Content = "OK", Margin = LayoutTokens.Uniform(LayoutTokens.SpacingXs) };
        var cancelButton = new Button { Content = "Cancel", Margin = LayoutTokens.Uniform(LayoutTokens.SpacingXs) };
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = LayoutTokens.Inset(0, 0, LayoutTokens.SpacingSm, LayoutTokens.SpacingSm),
            Children = { okButton, cancelButton }
        };

        var stackPanel = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = prompt, Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, LayoutTokens.SpacingSm, LayoutTokens.SpacingSm, 0) },
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
