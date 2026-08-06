using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.UI.DesignSystem;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Townhall-hosted, read-side usage and cost surface. It only invokes the
/// established transparency presentation owner; it never invents tokens, prices,
/// or invoice facts.
/// </summary>
internal sealed class AgentUsagePanel : Panel, IDisposable
{
    private readonly TextBlock _statusCaption;
    private readonly TextBlock _summaryCaption;
    private readonly TextBlock _recordsCaption;
    private readonly TextBlock _selectionCaption;
    private readonly ComboBox _recordSelector;
    private bool _suppressRecordSelection;
    private readonly Button _refreshButton;
    private readonly Button _retryButton;
    private readonly Button _openSettingsButton;
    private readonly Button _closeButton;
    private AgentTransparencyManagementViewModel? _viewModel;

    public AgentUsagePanel()
    {
        _statusCaption = TextStyles.Caption("Usage capture disabled.");
        _statusCaption.Foreground = Brushes.Gray;
        AutomationProperties.SetName(_statusCaption, "Usage surface status");

        _summaryCaption = TextStyles.Caption("No usage or cost evidence is available for the opened workspace.");
        _summaryCaption.Foreground = Brushes.Gray;
        _summaryCaption.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_summaryCaption, "Usage evidence summary");

        _recordsCaption = TextStyles.Caption("No records.");
        _recordsCaption.Foreground = Brushes.Gray;
        _recordsCaption.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_recordsCaption, "Usage evidence records");

        _selectionCaption = TextStyles.Caption("No record selected.");
        _selectionCaption.Foreground = Brushes.Gray;
        _selectionCaption.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_selectionCaption, "Selected usage record");

        _recordSelector = new ComboBox
        {
            MinWidth = 260,
            PlaceholderText = "Select a usage record",
        };
        AutomationProperties.SetName(_recordSelector, "Usage record selection");
        _recordSelector.SelectionChanged += (_, _) =>
        {
            if (_suppressRecordSelection || _viewModel is null)
            {
                return;
            }

            if (_recordSelector.SelectedItem is UsageRecordOption option)
            {
                _viewModel.SelectUsageRecord(option.OrderingSequence);
            }
            else
            {
                _viewModel.SelectUsageRecord(null);
            }

            ApplyProjection();
        };

        _refreshButton = CreateButton("Refresh", "Refresh usage evidence");
        _refreshButton.Click += async (_, _) => await RefreshAsync();
        _retryButton = CreateButton("Retry", "Retry failed usage load");
        _retryButton.Click += async (_, _) => await RetryAsync();
        _openSettingsButton = CreateButton("Open Settings", "Open application settings");
        _openSettingsButton.Click += (_, _) => OpenSettingsRequested?.Invoke();
        _closeButton = CreateButton("Close", "Close usage panel");
        _closeButton.Click += (_, _) => _viewModel?.CloseUsageCommand.Execute().Subscribe();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            Children = { _refreshButton, _retryButton, _openSettingsButton, _closeButton },
        };

        var body = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingXs,
            Children =
            {
                TextStyles.Caption("Usage and cost evidence"),
                _statusCaption,
                _summaryCaption,
                _recordSelector,
                _recordsCaption,
                _selectionCaption,
                actions,
            },
        };

        var container = new Border
        {
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingXs),
            Child = body,
        };
        AutomationProperties.SetName(container, "Agent usage and cost evidence panel");
        Focusable = true;
        Children.Add(container);
        IsVisible = false;
    }

    public Action? OpenSettingsRequested { get; set; }

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

    public Button RefreshButton => _refreshButton;

    public Button RetryButton => _retryButton;

    public Button OpenSettingsButton => _openSettingsButton;

    public Button CloseButton => _closeButton;

    public ComboBox RecordSelector => _recordSelector;

    public TextBlock StatusCaptionControl => _statusCaption;

    public TextBlock SummaryCaptionControl => _summaryCaption;

    public void Dispose()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private async Task RefreshAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.RefreshUsageSurfaceAsync();
        ApplyProjection();
    }

    private async Task RetryAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.RetryUsageSurfaceAsync();
        ApplyProjection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(AgentTransparencyManagementViewModel.IsUsagePanelOpen)
            or nameof(AgentTransparencyManagementViewModel.UsageStatusCaption)
            or nameof(AgentTransparencyManagementViewModel.UsageInspection))
        {
            ApplyProjection();
        }
    }

    private void ApplyProjection()
    {
        IsVisible = _viewModel?.IsUsagePanelOpen == true;
        var inspection = _viewModel?.UsageInspection;
        _statusCaption.Text = _viewModel?.UsageStatusCaption ?? "Usage unavailable.";

        if (inspection is null)
        {
            _summaryCaption.Text = "Usage surface is not available.";
            _recordsCaption.Text = "No records.";
            _selectionCaption.Text = "No record selected.";
            ApplyChromeVisibility(minimalEmpty: false, captureEnabled: false);
            SetActionsEnabled(enabled: false, canRetry: false);
            return;
        }

        var captureEnabled = _viewModel?.UsageAvailability.CurrentState.CaptureEnabled == true;
        var minimalEmpty = inspection.SurfaceState == AgentUsageSurfaceState.Empty;

        switch (inspection.SurfaceState)
        {
            case AgentUsageSurfaceState.Loading:
                _summaryCaption.Text = "Loading usage evidence for the opened workspace…";
                _recordsCaption.Text = "Loading…";
                break;
            case AgentUsageSurfaceState.Unavailable:
                _summaryCaption.Text = string.Empty;
                _recordsCaption.Text = "Unavailable.";
                break;
            case AgentUsageSurfaceState.Failed:
                _summaryCaption.Text = inspection.FailureReason is { } reason
                    ? $"Load failed: {reason}"
                    : "Load failed.";
                _recordsCaption.Text = "Failed — not empty.";
                break;
            case AgentUsageSurfaceState.Empty:
                // Status carries the primary empty fact; summary holds policy help only.
                _summaryCaption.Text = "Missing evidence is not zero.";
                _recordsCaption.Text = "No records.";
                break;
            case AgentUsageSurfaceState.Ready:
                _summaryCaption.Text = FormatSummary(inspection);
                _recordsCaption.Text = FormatRecords(inspection);
                break;
        }

        SyncRecordSelector(inspection);
        _selectionCaption.Text = inspection.SelectedRecord is { } selected
            ? FormatSelected(selected)
            : "No record selected.";

        ApplyChromeVisibility(minimalEmpty, captureEnabled);
        var enabled = inspection.SurfaceState is AgentUsageSurfaceState.Ready
            or AgentUsageSurfaceState.Empty
            or AgentUsageSurfaceState.Failed
            or AgentUsageSurfaceState.Unavailable;
        SetActionsEnabled(enabled: enabled, canRetry: inspection.CanRetry);
    }

    private void ApplyChromeVisibility(bool minimalEmpty, bool captureEnabled)
    {
        var full = !minimalEmpty;
        var minimalCaptureOff = minimalEmpty && !captureEnabled;

        SetInteractiveVisible(_recordSelector, full);
        SetVisible(_recordsCaption, full);
        SetVisible(_selectionCaption, full);
        SetInteractiveVisible(_refreshButton, true);
        SetInteractiveVisible(_retryButton, full);
        SetInteractiveVisible(_openSettingsButton, minimalCaptureOff);
        SetInteractiveVisible(_closeButton, true);
    }

    private void SyncRecordSelector(AgentUsageInspectionViewModel inspection)
    {
        _suppressRecordSelection = true;
        try
        {
            var options = inspection.Records
                .Select(record => new UsageRecordOption(
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

    private void SetActionsEnabled(bool enabled, bool canRetry)
    {
        _refreshButton.IsEnabled = _viewModel is not null && enabled;
        if (_retryButton.IsVisible)
        {
            _retryButton.IsEnabled = canRetry;
        }

        _closeButton.IsEnabled = _viewModel is not null;
        if (_recordSelector.IsVisible)
        {
            _recordSelector.IsEnabled = enabled;
        }
    }

    private static void SetVisible(Control control, bool visible)
    {
        control.IsVisible = visible;
    }

    private static void SetInteractiveVisible(Control control, bool visible)
    {
        control.IsVisible = visible;
        control.IsTabStop = visible;
    }

    private static string FormatSummary(AgentUsageInspectionViewModel inspection)
    {
        var summary = inspection.Summary;
        if (summary is null)
        {
            return string.Empty;
        }

        var costPart = summary.HasVerifiedTotalCost && summary.TotalCostCurrency is not null
            ? $"{summary.TotalCostValue:F4} {summary.TotalCostCurrency} verified aggregate (not an invoice)"
            : "verified cost unavailable — missing evidence is not zero";
        return $"{summary.TotalRecords} record(s); {costPart}.";
    }

    private static string FormatOption(AgentUsageRecord record) =>
        $"{record.OrderingSequence}: {record.BackendId} · {record.Kind} · {record.Origin} · {record.AggregationSemantics}";

    private static string FormatRecords(AgentUsageInspectionViewModel inspection)
    {
        var records = inspection.Records;
        if (records.Count == 0)
        {
            return "No records.";
        }

        return string.Join(
            Environment.NewLine,
            records.Select(record =>
            {
                var marker = inspection.SelectedRecord?.OrderingSequence == record.OrderingSequence
                    ? "* "
                    : "  ";
                return $"{marker}{record.OrderingSequence}: {record.BackendId} {record.Kind} · "
                    + $"{record.Origin}/{record.AggregationSemantics} · "
                    + $"{record.Value} {record.Unit}"
                    + (record.Currency is { } currency ? $" {currency}" : string.Empty)
                    + (record.Model is { } model ? $" · {model}" : string.Empty);
            }));
    }

    private static string FormatSelected(AgentUsageRecord record)
    {
        var pricing = record.PricingSourceId is { } source
            ? $"pricing {source} v{record.PricingSourceVersion?.ToString() ?? "?"} · formula {record.PricingFormula ?? "n/a"}"
            : "pricing unavailable";
        var uncertainty = record.Uncertainty is { } u
            ? $"uncertainty {u}"
            : "uncertainty n/a";
        return $"{record.MetricName}={record.Value} {record.Unit}"
            + (record.Currency is { } c ? $" {c}" : string.Empty)
            + $" · origin {record.Origin} · aggregation {record.AggregationSemantics}"
            + $" · backend {record.BackendId}"
            + (record.Model is { } m ? $" · model {m}" : string.Empty)
            + $" · {pricing} · {uncertainty}"
            + $" · scope session={record.Scope.SessionId ?? "n/a"} run={record.Scope.RunId ?? "n/a"}"
            + (record.EvidenceSourceDescription is { } evidence ? $" · {evidence}" : string.Empty)
            + " · not an invoice claim";
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

    private sealed class UsageRecordOption
    {
        public UsageRecordOption(long orderingSequence, string label)
        {
            OrderingSequence = orderingSequence;
            Label = label;
        }

        public long OrderingSequence { get; }

        public string Label { get; }

        public override string ToString() => Label;
    }
}
