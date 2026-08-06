using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.UI.DesignSystem;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Dedicated Townhall inspect side sheet for Trace / Memory / Usage.
/// Hosts evidence panels beside the chat message list so open flags can stay
/// independent without stacking Auto rows under the chat Star band (F1 option B).
/// </summary>
internal sealed class AgentInspectHost : Panel, IDisposable
{
    public const double DefaultSheetWidth = 320;

    private readonly AgentTracePanel _tracePanel;
    private readonly AgentMemoryPanel _memoryPanel;
    private readonly AgentUsagePanel _usagePanel;
    private readonly Border _chrome;
    private AgentTransparencyManagementViewModel? _viewModel;
    private Action? _openSettingsRequested;

    public AgentInspectHost()
    {
        _tracePanel = new AgentTracePanel
        {
            Background = PaletteTokens.SurfacePanelBrush,
        };
        _memoryPanel = new AgentMemoryPanel
        {
            Background = PaletteTokens.SurfacePanelBrush,
        };
        _usagePanel = new AgentUsagePanel
        {
            Background = PaletteTokens.SurfacePanelBrush,
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingSm,
            Children =
            {
                _tracePanel,
                _memoryPanel,
                _usagePanel,
            },
        };

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = stack,
        };

        var header = TextStyles.Caption("Inspect");
        header.Foreground = PaletteTokens.TextSecondaryBrush;
        AutomationProperties.SetName(header, "Inspect host title");

        var body = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
            Children = { header, scroll },
        };
        Grid.SetRow(scroll, 1);

        _chrome = new Border
        {
            // M5-allow: 1px left seam separates the inspect sheet from the message list.
            BorderBrush = PaletteTokens.SeparatorBrush,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Background = PaletteTokens.SurfacePanelBrush,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXs),
            Child = body,
        };
        AutomationProperties.SetName(_chrome, "Agent inspect host");

        Width = DefaultSheetWidth;
        MinWidth = DefaultSheetWidth;
        Children.Add(_chrome);
        IsVisible = false;
        Focusable = false;
        IsTabStop = false;
    }

    internal AgentTracePanel TracePanel => _tracePanel;

    internal AgentMemoryPanel MemoryPanel => _memoryPanel;

    internal AgentUsagePanel UsagePanel => _usagePanel;

    public Action? OpenSettingsRequested
    {
        get => _openSettingsRequested;
        set
        {
            _openSettingsRequested = value;
            _tracePanel.OpenSettingsRequested = value;
            _usagePanel.OpenSettingsRequested = value;
        }
    }

    public void SetViewModel(AgentTransparencyManagementViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        _tracePanel.SetViewModel(viewModel);
        _memoryPanel.SetViewModel(viewModel);
        _usagePanel.SetViewModel(viewModel);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyHostVisibility();
    }

    public void Dispose()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        _tracePanel.Dispose();
        _memoryPanel.Dispose();
        _usagePanel.Dispose();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(AgentTransparencyManagementViewModel.IsTracePanelOpen)
            or nameof(AgentTransparencyManagementViewModel.IsMemoryPanelOpen)
            or nameof(AgentTransparencyManagementViewModel.IsUsagePanelOpen)
            or null)
        {
            ApplyHostVisibility();
        }
    }

    private void ApplyHostVisibility()
    {
        var anyOpen = _viewModel is not null
            && (_viewModel.IsTracePanelOpen
                || _viewModel.IsMemoryPanelOpen
                || _viewModel.IsUsagePanelOpen);

        IsVisible = anyOpen;
        Width = anyOpen ? DefaultSheetWidth : 0;
        MinWidth = anyOpen ? DefaultSheetWidth : 0;
    }
}
