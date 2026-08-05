using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.UI.DesignSystem;

namespace Zaide.Features.Agents.Presentation;

/// <summary>
/// Townhall-hosted durable memory lifecycle surface. Mutations go only through
/// the transparency presentation owner; the panel never writes durable files
/// or conversation history directly.
/// </summary>
internal sealed class AgentMemoryPanel : Panel, IDisposable
{
    private readonly TextBlock _statusCaption;
    private readonly TextBlock _summaryCaption;
    private readonly TextBlock _recordsCaption;
    private readonly TextBlock _selectionCaption;
    private readonly TextBlock _influenceCaption;
    private readonly TextBlock _submitDenialCaption;
    private readonly TextBox _draftInput;
    private readonly ComboBox _scopeSelector;
    private readonly ComboBox _recordSelector;
    private bool _suppressRecordSelection;
    private readonly Button _createButton;
    private readonly Button _correctButton;
    private readonly Button _disableButton;
    private readonly Button _supersedeButton;
    private readonly Button _deleteButton;
    private readonly Button _refreshButton;
    private readonly Button _retryButton;
    private readonly Button _closeButton;
    private AgentTransparencyManagementViewModel? _viewModel;

    public AgentMemoryPanel()
    {
        _statusCaption = TextStyles.Caption("Durable memory closed.");
        _statusCaption.Foreground = Brushes.Gray;
        AutomationProperties.SetName(_statusCaption, "Memory surface status");

        _summaryCaption = TextStyles.Caption("No durable memory records.");
        _summaryCaption.Foreground = Brushes.Gray;
        _summaryCaption.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_summaryCaption, "Memory summary");

        _recordsCaption = TextStyles.Caption("No records.");
        _recordsCaption.Foreground = Brushes.Gray;
        _recordsCaption.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_recordsCaption, "Memory records");

        _selectionCaption = TextStyles.Caption("No record selected.");
        _selectionCaption.Foreground = Brushes.Gray;
        _selectionCaption.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_selectionCaption, "Selected memory record");

        _influenceCaption = TextStyles.Caption(
            "Influence evidence is attribution-only and is not editable lifecycle memory.");
        _influenceCaption.Foreground = Brushes.Gray;
        _influenceCaption.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_influenceCaption, "Memory influence evidence");

        _submitDenialCaption = TextStyles.Caption(string.Empty);
        _submitDenialCaption.Foreground = Brushes.Gray;
        _submitDenialCaption.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(_submitDenialCaption, "Memory create denial reason");

        _draftInput = new TextBox
        {
            PlaceholderText = "New or corrected memory content",
            AcceptsReturn = true,
            MinHeight = 48,
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetName(_draftInput, "Memory content draft");

        _scopeSelector = new ComboBox
        {
            ItemsSource = new[]
            {
                AgentMemoryScope.ProjectShared,
                AgentMemoryScope.Conversation,
                AgentMemoryScope.Agent,
                AgentMemoryScope.Session,
            },
            SelectedItem = AgentMemoryScope.ProjectShared,
            MinWidth = 140,
        };
        AutomationProperties.SetName(_scopeSelector, "Memory create scope");
        _scopeSelector.SelectionChanged += (_, _) =>
        {
            if (_viewModel is null || _scopeSelector.SelectedItem is not AgentMemoryScope scope)
            {
                return;
            }

            _viewModel.MemoryInspection.SelectedScope = scope;
            ApplyProjection();
        };

        _recordSelector = new ComboBox
        {
            MinWidth = 220,
            PlaceholderText = "Select a memory record",
        };
        AutomationProperties.SetName(_recordSelector, "Memory record selection");
        _recordSelector.SelectionChanged += (_, _) =>
        {
            if (_suppressRecordSelection || _viewModel is null)
            {
                return;
            }

            if (_recordSelector.SelectedItem is MemoryRecordOption option)
            {
                _viewModel.SelectMemoryRecord(option.MemoryId);
            }
            else
            {
                _viewModel.SelectMemoryRecord(null);
            }

            ApplyProjection();
        };

        _createButton = CreateButton("Create", "Create durable memory record");
        _createButton.Click += (_, _) => CreateFromDraft();
        _correctButton = CreateButton("Correct", "Correct selected memory record");
        _correctButton.Click += (_, _) => CorrectSelected();
        _disableButton = CreateButton("Disable", "Disable selected memory record");
        _disableButton.Click += (_, _) => DisableSelected();
        _supersedeButton = CreateButton("Supersede", "Supersede selected memory record");
        _supersedeButton.Click += (_, _) => SupersedeSelected();
        _deleteButton = CreateButton("Delete", "Delete selected memory record");
        _deleteButton.Click += (_, _) => DeleteSelected();
        _refreshButton = CreateButton("Refresh", "Refresh durable memory");
        _refreshButton.Click += async (_, _) => await RefreshAsync();
        _retryButton = CreateButton("Retry", "Retry failed durable memory load");
        _retryButton.Click += async (_, _) => await RetryAsync();
        _closeButton = CreateButton("Close", "Close memory panel");
        _closeButton.Click += (_, _) => _viewModel?.CloseMemoryCommand.Execute().Subscribe();

        var scopeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            Children =
            {
                TextStyles.Caption("Scope"),
                _scopeSelector,
                TextStyles.Caption("Record"),
                _recordSelector,
            },
        };

        var lifecycleActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            Children =
            {
                _createButton,
                _correctButton,
                _disableButton,
                _supersedeButton,
                _deleteButton,
            },
        };

        var surfaceActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = LayoutTokens.SpacingSm,
            Children = { _refreshButton, _retryButton, _closeButton },
        };

        var body = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = LayoutTokens.SpacingXs,
            Children =
            {
                TextStyles.Caption("Durable memory"),
                _statusCaption,
                _summaryCaption,
                _recordsCaption,
                _selectionCaption,
                _influenceCaption,
                scopeRow,
                _draftInput,
                _submitDenialCaption,
                lifecycleActions,
                surfaceActions,
            },
        };

        var container = new Border
        {
            Padding = LayoutTokens.Symmetric(LayoutTokens.SpacingMd, LayoutTokens.SpacingXs),
            Child = body,
        };
        AutomationProperties.SetName(container, "Agent durable memory panel");
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

    public Button CreateButtonControl => _createButton;

    public Button CorrectButton => _correctButton;

    public Button DisableButton => _disableButton;

    public Button SupersedeButton => _supersedeButton;

    public Button DeleteButton => _deleteButton;

    public Button RefreshButton => _refreshButton;

    public Button RetryButton => _retryButton;

    public Button CloseButton => _closeButton;

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

    private void CreateFromDraft()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.MemoryInspection.DraftContent = _draftInput.Text ?? string.Empty;
        if (_scopeSelector.SelectedItem is AgentMemoryScope scope)
        {
            _viewModel.MemoryInspection.SelectedScope = scope;
        }

        _viewModel.CreateMemoryFromDraft();
        _draftInput.Text = _viewModel.MemoryInspection.DraftContent;
        ApplyProjection();
    }

    private void CorrectSelected()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.CorrectSelectedMemory(_draftInput.Text ?? string.Empty);
        ApplyProjection();
    }

    private void DisableSelected()
    {
        _viewModel?.DisableSelectedMemory();
        ApplyProjection();
    }

    private void SupersedeSelected()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.SupersedeSelectedMemory(_draftInput.Text ?? string.Empty);
        ApplyProjection();
    }

    private void DeleteSelected()
    {
        _viewModel?.DeleteSelectedMemory();
        ApplyProjection();
    }

    private async Task RefreshAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.RefreshMemorySurfaceAsync();
        ApplyProjection();
    }

    private async Task RetryAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.RetryMemorySurfaceAsync();
        ApplyProjection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(AgentTransparencyManagementViewModel.IsMemoryPanelOpen)
            or nameof(AgentTransparencyManagementViewModel.MemoryStatusCaption)
            or nameof(AgentTransparencyManagementViewModel.MemoryInspection))
        {
            ApplyProjection();
        }
    }

    private void ApplyProjection()
    {
        IsVisible = _viewModel?.IsMemoryPanelOpen == true;
        var inspection = _viewModel?.MemoryInspection;
        _statusCaption.Text = _viewModel?.MemoryStatusCaption ?? "Memory unavailable.";
        _influenceCaption.Text = inspection?.InfluenceEvidenceCaption
            ?? "Influence evidence is attribution-only and is not editable lifecycle memory.";

        if (inspection is null)
        {
            _summaryCaption.Text = "Memory surface is not available.";
            _recordsCaption.Text = "No records.";
            _selectionCaption.Text = "No record selected.";
            _submitDenialCaption.Text = string.Empty;
            SetActionsEnabled(enabled: false, canRetry: false, canCreate: false, hasSelection: false);
            return;
        }

        switch (inspection.SurfaceState)
        {
            case AgentMemorySurfaceState.Loading:
                _summaryCaption.Text = "Loading durable memory for the opened workspace…";
                _recordsCaption.Text = "Loading…";
                break;
            case AgentMemorySurfaceState.Unavailable:
                _summaryCaption.Text = inspection.FailureReason
                    ?? "Opened workspace is required.";
                _recordsCaption.Text = "Unavailable.";
                break;
            case AgentMemorySurfaceState.Failed:
                _summaryCaption.Text = inspection.FailureReason is { } reason
                    ? $"Load failed: {reason}"
                    : "Load failed.";
                _recordsCaption.Text = "Failed — not empty.";
                break;
            case AgentMemorySurfaceState.Empty:
                // Status carries the primary empty fact from the inspection ViewModel.
                _summaryCaption.Text = string.Empty;
                _recordsCaption.Text = "No records.";
                break;
            case AgentMemorySurfaceState.Ready:
                var summary = inspection.Summary;
                _summaryCaption.Text = summary is null
                    ? string.Empty
                    : $"{summary.ActiveRecords} active / {summary.TotalRecords} total · conflicts {summary.ConflictRecords}";
                _recordsCaption.Text = FormatRecords(inspection);
                break;
        }

        SyncRecordSelector(inspection);

        var selected = inspection.SelectedRecord;
        _selectionCaption.Text = selected is null
            ? "No record selected."
            : FormatSelected(selected);

        _submitDenialCaption.Text = inspection.CanSubmitCreate
            ? string.Empty
            : inspection.SubmitDenialReason ?? "Create is unavailable.";

        var hasSelection = selected is not null;
        SetActionsEnabled(
            enabled: inspection.SurfaceState is AgentMemorySurfaceState.Ready
                or AgentMemorySurfaceState.Empty
                or AgentMemorySurfaceState.Failed
                or AgentMemorySurfaceState.Unavailable,
            canRetry: inspection.CanRetry,
            canCreate: inspection.CanSubmitCreate,
            hasSelection: hasSelection
                && inspection.SurfaceState is AgentMemorySurfaceState.Ready or AgentMemorySurfaceState.Empty);
    }

    private void SyncRecordSelector(AgentMemoryInspectionViewModel inspection)
    {
        _suppressRecordSelection = true;
        try
        {
            var options = inspection.Records
                .Select(record => new MemoryRecordOption(record.MemoryId, FormatOption(record)))
                .ToArray();
            _recordSelector.ItemsSource = options;
            if (inspection.SelectedRecord is { } selected)
            {
                _recordSelector.SelectedItem = options.FirstOrDefault(
                    option => option.MemoryId.Equals(selected.MemoryId));
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

    private static string FormatOption(AgentMemoryRecord record) =>
        $"{record.OrderingSequence}: {record.ScopeTarget.Scope} · {record.Status}";

    private void SetActionsEnabled(bool enabled, bool canRetry, bool canCreate, bool hasSelection)
    {
        _createButton.IsEnabled = enabled && canCreate;
        _correctButton.IsEnabled = enabled && hasSelection;
        _disableButton.IsEnabled = enabled && hasSelection;
        _supersedeButton.IsEnabled = enabled && hasSelection;
        _deleteButton.IsEnabled = enabled && hasSelection;
        _refreshButton.IsEnabled = _viewModel is not null;
        _retryButton.IsEnabled = canRetry;
        _closeButton.IsEnabled = _viewModel is not null;
        _draftInput.IsEnabled = enabled;
        _scopeSelector.IsEnabled = enabled;
    }

    private static string FormatRecords(AgentMemoryInspectionViewModel inspection)
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
                var marker = inspection.SelectedRecord?.MemoryId.Equals(record.MemoryId) == true
                    ? "* "
                    : "  ";
                return $"{marker}{record.OrderingSequence}: {record.ScopeTarget.Scope} · {record.Status} · {record.Provenance.SourceKind} · conflict {record.ConflictKind}";
            }));
    }

    private static string FormatSelected(AgentMemoryRecord record) =>
        $"{record.MemoryId.Value} · scope {record.ScopeTarget.Scope} · status {record.Status} · " +
        $"source {record.Provenance.SourceKind}/{record.Provenance.SourceRevision} · " +
        $"conflict {record.ConflictKind} · content: {Truncate(record.Content, 120)}";

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";

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

    private sealed class MemoryRecordOption
    {
        public MemoryRecordOption(AgentMemoryId memoryId, string label)
        {
            MemoryId = memoryId;
            Label = label;
        }

        public AgentMemoryId MemoryId { get; }

        public string Label { get; }

        public override string ToString() => Label;
    }
}
