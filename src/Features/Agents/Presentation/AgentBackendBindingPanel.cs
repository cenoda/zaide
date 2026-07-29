using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using Zaide.UI.DesignSystem;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Minimal backend binding status panel for direct agent conversations.
/// </summary>
public sealed class AgentBackendBindingPanel : Panel
{
    private readonly TextBlock _backendLabel;
    private readonly TextBlock _authStatusCaption;

    public AgentBackendBindingPanel()
    {
        _backendLabel = TextStyles.Caption("Unbound");
        _backendLabel.VerticalAlignment = VerticalAlignment.Center;

        _authStatusCaption = TextStyles.Caption(string.Empty);
        _authStatusCaption.Foreground = Brushes.Gray;
        _authStatusCaption.VerticalAlignment = VerticalAlignment.Center;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                TextStyles.Caption("Backend:"),
                _backendLabel,
                _authStatusCaption,
            },
        };

        var container = new Border
        {
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingXs),
            Child = row,
        };

        AutomationProperties.SetName(container, "Agent backend binding status");
        Children.Add(container);
    }

    public bool IsPanelVisible
    {
        get => IsVisible;
        set => IsVisible = value;
    }

    public void SetBindingProjection(
        string backendLabel,
        string authStatusCaption,
        bool isDisconnected)
    {
        _backendLabel.Text = backendLabel;
        _authStatusCaption.Text = authStatusCaption;
        _authStatusCaption.IsVisible = !string.IsNullOrEmpty(authStatusCaption);
        _authStatusCaption.Foreground = isDisconnected ? Brushes.IndianRed : Brushes.Gray;
    }
}
