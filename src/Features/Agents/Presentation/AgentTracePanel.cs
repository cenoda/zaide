using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Features.Agents.Domain.Transparency.Trace;
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
    private readonly TextBlock _selectionCaption;
    private readonly TextBlock _pagingCaption;
    private readonly ComboBox _recordSelector;
    private bool _suppressRecordSelection;
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

        _selectionCaption = TextStyles.Caption("No record selected.");
        _selectionCaption.Foreground = Brushes.Gray;
        _selectionCaption.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_selectionCaption, "Selected trace record");

        _pagingCaption = TextStyles.Caption(
            $"Page size {AgentTransparencyManagementViewModel.DefaultPageSize} (max {AgentTransparencyManagementViewModel.MaxPageSize}).");
        _pagingCaption.Foreground = Brushes.Gray;
        AutomationProperties.SetName(_pagingCaption, "Trace bounded paging");

        _recordSelector = new ComboBox
        {
            MinWidth = 260,
            PlaceholderText = "Select a trace record",
            Focusable = true,
            IsTabStop = true,
        };
        AutomationProperties.SetName(_recordSelector, "Trace record selection");
        _recordSelector.SelectionChanged += (_, _) =>
        {
            if (_suppressRecordSelection || _viewModel is null)
            {
                return;
            }

            if (_recordSelector.SelectedItem is TraceRecordOption option)
            {
                _viewModel.SelectTraceRecord(option.OrderingSequence);
            }
            else
            {
                _viewModel.SelectTraceRecord(null);
            }

            ApplyProjection();
        };

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
                _recordSelector,
                _recordsCaption,
                _selectionCaption,
                _pagingCaption,
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
        IsTabStop = true;
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

    public ComboBox RecordSelector => _recordSelector;

    public TextBlock StatusCaptionControl => _statusCaption;

    public TextBlock SelectionCaptionControl => _selectionCaption;

    public TextBlock PagingCaptionControl => _pagingCaption;

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
        ApplyProjection();
        await Task.CompletedTask;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(AgentTransparencyManagementViewModel.IsTracePanelOpen)
            or nameof(AgentTransparencyManagementViewModel.TraceStatusCaption)
            or nameof(AgentTransparencyManagementViewModel.TraceInspection))
        {
            ApplyProjection();
        }
    }

    private void ApplyProjection()
    {
        IsVisible = _viewModel?.IsTracePanelOpen == true;
        var inspection = _viewModel?.TraceInspection;
        _statusCaption.Text = _viewModel?.TraceStatusCaption ?? "Trace unavailable.";
        AutomationProperties.SetHelpText(_statusCaption, _statusCaption.Text);

        if (inspection is null)
        {
            _summaryCaption.Text = "Trace surface is not available.";
            _recordsCaption.Text = "No records.";
            _selectionCaption.Text = "No record selected.";
            SetActionsEnabled(enabled: false);
            return;
        }

        var summary = inspection.Summary;
        var records = inspection.Records;
        var captureEnabled = inspection.Availability.CaptureEnabled;

        if (summary is null || summary.IsEmpty)
        {
            _summaryCaption.Text = captureEnabled
                ? "No trace evidence is available for the opened workspace."
                : "Trace capture disabled — missing evidence is not empty fabrication.";
            _recordsCaption.Text = "No records.";
        }
        else
        {
            _summaryCaption.Text =
                $"{summary.TotalRecords} redacted record(s), {summary.TotalPayloadBytes} byte(s).";
            _recordsCaption.Text = records.Count == 0
                ? "No records on this page."
                : string.Join(
                    Environment.NewLine,
                    records.Select(record =>
                    {
                        var marker = inspection.SelectedRecord?.OrderingSequence == record.OrderingSequence
                            ? "* "
                            : "  ";
                        return $"{marker}{record.OrderingSequence}: {record.BackendId} {record.Kind} · {record.CaptureState}";
                    }));
        }

        SyncRecordSelector(inspection);
        _selectionCaption.Text = inspection.SelectedRecord is { } selected
            ? FormatSelected(selected)
            : "No record selected.";
        AutomationProperties.SetHelpText(_selectionCaption, _selectionCaption.Text);
        AutomationProperties.SetHelpText(_summaryCaption, _summaryCaption.Text);
        AutomationProperties.SetHelpText(_recordsCaption, _recordsCaption.Text);

        var enabled = _viewModel is not null;
        SetActionsEnabled(enabled: enabled);
        var enabledCapture = captureEnabled;
        _captureButton.Content = TextStyles.Caption(enabledCapture ? "Disable capture" : "Enable capture");
    }

    private void SyncRecordSelector(AgentTraceInspectionViewModel inspection)
    {
        _suppressRecordSelection = true;
        try
        {
            var options = inspection.Records
                .Select(record => new TraceRecordOption(
                    record.OrderingSequence,
                    FormatOption(record)))
                .ToArray();
            _recordSelector.ItemsSource = options;
            if (inspection.SelectedRecord is { } selected)
            {
                _recordSelector.SelectedItem = options.FirstOrDefault(
                    option => option.OrderingSequence == selected.OrderingSequence);
            }
            else
            {
                _recordSelector.SelectedItem = null;
            }
        }
        finally
        {
            _suppressRecordSelection = false;
        }
    }

    private void SetActionsEnabled(bool enabled)
    {
        _captureButton.IsEnabled = enabled;
        _refreshButton.IsEnabled = enabled;
        _closeButton.IsEnabled = enabled;
        _recordSelector.IsEnabled = enabled;
    }

    private static string FormatOption(AgentTraceRecord record) =>
        $"{record.OrderingSequence}: {record.BackendId} · {record.Kind} · {record.CaptureState}";

    private static string FormatSelected(AgentTraceRecord record) =>
        $"{record.OrderingSequence}: backend {record.BackendId} · kind {record.Kind} · "
        + $"evidence {record.EvidenceLevel} · capture {record.CaptureState} · "
        + $"{record.PayloadByteCount} byte(s) redacted"
        + (record.RedactionReason is { } reason ? $" · {reason}" : string.Empty);

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

    private sealed class TraceRecordOption
    {
        public TraceRecordOption(long orderingSequence, string label)
        {
            OrderingSequence = orderingSequence;
            Label = label;
        }

        public long OrderingSequence { get; }

        public string Label { get; }

        public override string ToString() => Label;
    }
}
