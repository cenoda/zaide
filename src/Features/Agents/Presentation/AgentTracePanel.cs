using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.UI.DesignSystem;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Townhall-hosted, read-side trace surface. It only invokes the established
/// transparency presentation owner; it never reads durable files or backend
/// transports directly.
/// </summary>
internal sealed class AgentTracePanel : Panel, IDisposable
{
    private readonly TextBlock _statusCaption;
    private readonly TextBlock _summaryCaption;
    private readonly TextBlock _recordsCaption;
    private readonly Button _captureButton;
    private readonly Button _refreshButton;
    private readonly Button _closeButton;
    private AgentTransparencyManagementViewModel? _viewModel;

    public AgentTracePanel()
    {
        _statusCaption = TextStyles.Caption("Trace capture disabled.");
        _statusCaption.Foreground = Brushes.Gray;
        AutomationProperties.SetName(_statusCaption, "Trace capture status");

        _summaryCaption = TextStyles.Caption("No trace evidence is available for the opened workspace.");
        _summaryCaption.Foreground = Brushes.Gray;
        _summaryCaption.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_summaryCaption, "Trace evidence summary");

        _recordsCaption = TextStyles.Caption("No records.");
        _recordsCaption.Foreground = Brushes.Gray;
        _recordsCaption.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_recordsCaption, "Trace evidence records");

        _captureButton = CreateButton("Enable capture", "Enable or disable trace capture");
        _captureButton.Click += async (_, _) => await ToggleCaptureAsync();
        _refreshButton = CreateButton("Refresh", "Refresh trace evidence");
        _refreshButton.Click += async (_, _) => await RefreshAsync();
        _closeButton = CreateButton("Close", "Close trace panel");
        _closeButton.Click += (_, _) => _viewModel?.CloseTraceCommand.Execute().Subscribe();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            Children = { _captureButton, _refreshButton, _closeButton },
        };

        var body = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingXs,
            Children =
            {
                TextStyles.Caption("Trace evidence"),
                _statusCaption,
                _summaryCaption,
                _recordsCaption,
                actions,
            },
        };

        var container = new Border
        {
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingXs),
            Child = body,
        };
        AutomationProperties.SetName(container, "Agent trace evidence panel");
        Focusable = true;
        Children.Add(container);
        IsVisible = false;
    }

    public void SetViewModel(AgentTransparencyManagementViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyProjection();
        _ = RefreshAsync();
    }

    public Button CaptureButton => _captureButton;

    public Button RefreshButton => _refreshButton;

    public Button CloseButton => _closeButton;

    public void Dispose()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private async Task ToggleCaptureAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ToggleTraceCaptureCommand.Execute().Subscribe();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.RefreshTracePresentation();
        var summary = await _viewModel.LoadTraceSummaryAsync();
        var records = await _viewModel.LoadTraceRecordsAsync(
            afterOrderingSequence: 0,
            AgentTransparencyManagementViewModel.DefaultPageSize);

        _summaryCaption.Text = summary.IsEmpty
            ? "No trace evidence is available for the opened workspace."
            : $"{summary.TotalRecords} redacted record(s), {summary.TotalPayloadBytes} byte(s).";
        _recordsCaption.Text = records.Count == 0
            ? "No records."
            : string.Join(
                Environment.NewLine,
                records.Select(record =>
                    $"{record.OrderingSequence}: {record.BackendId} {record.Kind} · {record.CaptureState}"));
        ApplyProjection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(AgentTransparencyManagementViewModel.IsTracePanelOpen)
            or nameof(AgentTransparencyManagementViewModel.TraceStatusCaption))
        {
            ApplyProjection();
        }
    }

    private void ApplyProjection()
    {
        IsVisible = _viewModel?.IsTracePanelOpen == true;
        _statusCaption.Text = _viewModel?.TraceStatusCaption ?? "Trace unavailable.";
        var enabled = _viewModel?.TraceAvailability.CurrentState.CaptureEnabled == true;
        _captureButton.Content = TextStyles.Caption(enabled ? "Disable capture" : "Enable capture");
        _captureButton.IsEnabled = _viewModel is not null;
        _refreshButton.IsEnabled = _viewModel is not null;
        _closeButton.IsEnabled = _viewModel is not null;
    }

    private static Button CreateButton(string content, string automationName)
    {
        var button = new Button
        {
            Content = TextStyles.Caption(content),
            Focusable = true,
            IsTabStop = true,
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingSm, LayoutTokens.SpacingXs),
        };
        AutomationProperties.SetName(button, automationName);
        return button;
    }
}
