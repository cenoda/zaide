using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using Zaide.UI.DesignSystem;

namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// Minimal session context policy selector for direct agent conversations.
/// </summary>
public sealed class TownhallContextPolicySelector : Panel
{
    private static readonly string[] SelectorLabels =
    {
        "Application default",
        "Off",
        "Minimal",
        "Standard",
        "Detailed",
    };

    private readonly ComboBox _policyCombo;
    private readonly Button _clearOverrideButton;
    private readonly TextBlock _statusCaption;
    private bool _suppressSelectionEvents;

    public event EventHandler<int>? PolicySelectionChanged;

    public event EventHandler? ClearOverrideRequested;

    public TownhallContextPolicySelector()
    {
        _statusCaption = TextStyles.Caption(string.Empty);
        _statusCaption.Foreground = Brushes.Gray;
        _statusCaption.VerticalAlignment = VerticalAlignment.Center;

        _policyCombo = new ComboBox
        {
            MinWidth = 160,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = SelectorLabels,
            SelectedIndex = 0,
        };
        AutomationProperties.SetName(_policyCombo, "Agent context policy");
        AutomationProperties.SetHelpText(
            _policyCombo,
            "Select the IDE context disclosure policy for this agent session. Application default uses the global policy.");

        _clearOverrideButton = new Button
        {
            Content = TextStyles.Caption("Use application default"),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };
        AutomationProperties.SetName(_clearOverrideButton, "Clear session context policy override");

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                TextStyles.Caption("Context policy:"),
                _policyCombo,
                _statusCaption,
                _clearOverrideButton,
            },
        };

        var container = new Border
        {
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingXs),
            Child = row,
        };

        Children.Add(container);

        _policyCombo.SelectionChanged += (_, _) =>
        {
            if (_suppressSelectionEvents)
            {
                return;
            }

            var index = _policyCombo.SelectedIndex;
            if (index >= 0)
            {
                PolicySelectionChanged?.Invoke(this, index);
            }
        };

        _clearOverrideButton.Click += (_, _) => ClearOverrideRequested?.Invoke(this, EventArgs.Empty);
    }

    public bool IsSelectorVisible
    {
        get => IsVisible;
        set => IsVisible = value;
    }

    public void SetPolicyProjection(
        int selectorIndex,
        string statusCaption,
        bool isOverrideActive)
    {
        _suppressSelectionEvents = true;
        try
        {
            if (selectorIndex >= 0 && selectorIndex < SelectorLabels.Length)
            {
                _policyCombo.SelectedIndex = selectorIndex;
            }

            _statusCaption.Text = statusCaption;
            _statusCaption.IsVisible = !string.IsNullOrEmpty(statusCaption);
            _clearOverrideButton.IsVisible = isOverrideActive;
        }
        finally
        {
            _suppressSelectionEvents = false;
        }
    }

    public void SetSelectorEnabled(bool isEnabled)
    {
        _policyCombo.IsEnabled = isEnabled;
        _clearOverrideButton.IsEnabled = isEnabled;
    }
}
