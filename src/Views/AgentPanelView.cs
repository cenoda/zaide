using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Avalonia;
using Zaide.Models;
using Zaide.Styles;

namespace Zaide.Views;

/// <summary>
/// View for a single agent panel. Displays agent name/status, output history,
/// and a draft input area.
///
/// View-layer only — no ViewModel. Binds directly to <see cref="AgentPanelState"/>
/// via its observable properties.
/// </summary>
public sealed class AgentPanelView : ReactiveUserControl<AgentPanelState>
{
    private readonly TextBlock _headerText;
    private readonly TextBlock _statusText;
    private readonly ListBox _outputList;
    private readonly TextBox _inputBox;

    public AgentPanelView()
    {
        var resources = Application.Current!.Resources;

        // --- Header: agent name + status ---
        _headerText = TextStyles.Header("");
        _headerText.VerticalAlignment = VerticalAlignment.Center;

        _statusText = TextStyles.Caption("");
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        _statusText.Margin = LayoutTokens.Inset(LayoutTokens.SpacingSm, 0, 0, 0);

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _headerText, _statusText }
        };

        var headerBorder = new Border
        {
            Background = (IBrush?)resources["SurfaceBaseBrush"],
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm),
            Child = headerPanel
        };

        // --- Output history ---
        _outputList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingSm)
        };

        // Item template for output entries
        _outputList.ItemTemplate = new FuncDataTemplate<string>((entry, _) =>
        {
            if (entry is null) return null;
            return TextStyles.Body(entry);
        });

        // --- Input area ---
        _inputBox = new TextBox
        {
            FontSize = 13,
            PlaceholderText = "Type a message...",
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXs),
            BorderThickness = new Thickness(1),
            CornerRadius = LayoutTokens.RadiusSm,
            AcceptsReturn = false,
            Margin = LayoutTokens.Inset(LayoutTokens.SpacingMd, 0, LayoutTokens.SpacingMd, LayoutTokens.SpacingMd)
        };

        var inputBorder = new Border
        {
            Background = (IBrush?)resources["SurfaceBaseBrush"],
            BorderThickness = new Thickness(0),
            Child = _inputBox
        };

        // --- Layout ---
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            },
            Background = (IBrush?)resources["SurfacePanelBrush"],
            Children = { headerBorder, _outputList, inputBorder }
        };
        Grid.SetRow(headerBorder, 0);
        Grid.SetRow(_outputList, 1);
        Grid.SetRow(inputBorder, 2);

        Content = root;

        // --- Bindings ---
        this.WhenActivated(d =>
        {
            d.Add(this.OneWayBind(ViewModel, vm => vm.AgentName, v => v._headerText.Text));
            d.Add(this.OneWayBind(ViewModel, vm => vm.Status, v => v._statusText.Text));
            d.Add(this.OneWayBind(ViewModel, vm => vm.OutputHistory, v => v._outputList.ItemsSource));
            d.Add(this.Bind(ViewModel, vm => vm.DraftInput, v => v._inputBox.Text));
        });
    }
}