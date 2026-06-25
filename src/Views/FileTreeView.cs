using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI.Avalonia;
using Zaide.Models;
using Zaide.ViewModels;

namespace Zaide.Views;

/// <summary>
/// File tree sidebar view. Builds a TreeView bound to FileTreeViewModel.RootNodes.
/// Colors pulled from App.axaml resources. Built in C# per DESIGN.md §1.
/// </summary>
public partial class FileTreeView : ReactiveUserControl<FileTreeViewModel>
{
    public FileTreeView()
    {
        Padding = new Thickness(16);

        var treeView = new TreeView
        {
            [!TreeView.ItemsSourceProperty] = new Binding("RootNodes"),
            ItemTemplate = new FuncTreeDataTemplate<FileTreeNode>(
                match: _ => true,
                build: (node, _) => new TextBlock
                {
                    [!TextBlock.TextProperty] = new Binding("Name"),
                    Foreground = (IBrush?)Application.Current!.Resources["TextActive"]
                },
                itemsSelector: node => node.Children
            )
        };

        treeView.SelectionChanged += (_, e) =>
        {
            if (e.AddedItems.Count > 0)
                ViewModel!.SelectedFile = e.AddedItems[0] as FileTreeNode;
            else
                ViewModel!.SelectedFile = null;
        };

        Background = (IBrush?)Application.Current!.Resources["DeepBase"];

        Content = treeView;
    }
}
