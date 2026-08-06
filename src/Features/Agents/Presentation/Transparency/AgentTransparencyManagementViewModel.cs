using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Presentation.Memory;
using Zaide.Features.Agents.Presentation.Transparency;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Agents.Presentation.Transparency;

/// <summary>
/// Integrated transparency and memory management surface for Townhall/Agents UI.
/// </summary>
internal sealed class AgentTransparencyManagementViewModel : ReactiveObject
{
    public const string AutomationName = "Agent transparency and memory management";
    public const string AutomationHelpText =
        "Inspect trace evidence, usage and cost, session continuity, and durable memory. " +
        "Keyboard navigation and screen-reader-compatible bounded history paging are supported.";

    public const int DefaultPageSize = 64;
    public const int MaxPageSize = 256;

    private readonly ISettingsService _settings;
    private readonly AgentTraceInspectionViewModel _traceInspection;
    private readonly AgentUsageInspectionViewModel _usageInspection;
    private readonly AgentSessionContinuityInspectionViewModel _continuityInspection;
    private readonly AgentMemoryInspectionViewModel _memoryInspection;
    private readonly IAgentTransparencyLifecycleCoordinator _lifecycleCoordinator;
    private readonly AgentMemoryCoordinator _memoryCoordinator;
    private readonly AgentTraceAvailabilityProjection _traceAvailabilityProjection;
    private readonly AgentUsageAvailabilityProjection _usageAvailabilityProjection;
    private readonly AgentSessionContinuityAvailabilityProjection _continuityAvailabilityProjection;
    private readonly AgentMemoryAvailabilityProjection _memoryAvailabilityProjection;
    private bool _isTracePanelOpen;
    private bool _isMemoryPanelOpen;
    private bool _isUsagePanelOpen;
    private string _traceStatusCaption = AgentTraceAvailabilityState.Initial.FormatStatusCaption();
    private string _memoryStatusCaption = "Durable memory closed.";
    private string _usageStatusCaption = "Usage evidence closed.";

    public AgentTransparencyManagementViewModel(
        AgentTraceInspectionViewModel traceInspection,
        AgentUsageInspectionViewModel usageInspection,
        AgentSessionContinuityInspectionViewModel continuityInspection,
        AgentMemoryInspectionViewModel memoryInspection,
        IAgentTransparencyLifecycleCoordinator lifecycleCoordinator,
        AgentMemoryCoordinator memoryCoordinator,
        AgentTraceAvailabilityProjection traceAvailabilityProjection,
        AgentUsageAvailabilityProjection usageAvailabilityProjection,
        AgentSessionContinuityAvailabilityProjection continuityAvailabilityProjection,
        AgentMemoryAvailabilityProjection memoryAvailabilityProjection,
        ISettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _traceInspection = traceInspection ?? throw new ArgumentNullException(nameof(traceInspection));
        _usageInspection = usageInspection ?? throw new ArgumentNullException(nameof(usageInspection));
        _continuityInspection = continuityInspection
            ?? throw new ArgumentNullException(nameof(continuityInspection));
        _memoryInspection = memoryInspection ?? throw new ArgumentNullException(nameof(memoryInspection));
        _lifecycleCoordinator = lifecycleCoordinator
            ?? throw new ArgumentNullException(nameof(lifecycleCoordinator));
        _memoryCoordinator = memoryCoordinator ?? throw new ArgumentNullException(nameof(memoryCoordinator));
        _traceAvailabilityProjection = traceAvailabilityProjection
            ?? throw new ArgumentNullException(nameof(traceAvailabilityProjection));
        _usageAvailabilityProjection = usageAvailabilityProjection
            ?? throw new ArgumentNullException(nameof(usageAvailabilityProjection));
        _continuityAvailabilityProjection = continuityAvailabilityProjection
            ?? throw new ArgumentNullException(nameof(continuityAvailabilityProjection));
        _memoryAvailabilityProjection = memoryAvailabilityProjection
            ?? throw new ArgumentNullException(nameof(memoryAvailabilityProjection));

        OpenTraceCommand = ReactiveCommand.Create(OpenTraceSurface);
        CloseTraceCommand = ReactiveCommand.Create(CloseTraceSurface);
        ToggleTraceCaptureCommand = ReactiveCommand.Create(ToggleTraceCapture);
        OpenMemoryCommand = ReactiveCommand.CreateFromTask(OpenMemorySurfaceAsync);
        CloseMemoryCommand = ReactiveCommand.Create(CloseMemorySurface);
        RefreshMemoryCommand = ReactiveCommand.CreateFromTask(RefreshMemorySurfaceAsync);
        RetryMemoryCommand = ReactiveCommand.CreateFromTask(RetryMemorySurfaceAsync);
        OpenUsageCommand = ReactiveCommand.CreateFromTask(OpenUsageSurfaceAsync);
        CloseUsageCommand = ReactiveCommand.Create(CloseUsageSurface);
        RefreshUsageCommand = ReactiveCommand.CreateFromTask(RefreshUsageSurfaceAsync);
        RetryUsageCommand = ReactiveCommand.CreateFromTask(RetryUsageSurfaceAsync);
        ToggleUsageCaptureCommand = ReactiveCommand.Create(ToggleUsageCapture);

        ToggleTraceCommand = ReactiveCommand.Create(ToggleTraceSurface);
        ToggleMemoryCommand = ReactiveCommand.CreateFromTask(ToggleMemorySurfaceAsync);
        ToggleUsageCommand = ReactiveCommand.CreateFromTask(ToggleUsageSurfaceAsync);
    }

    public string AccessibilityName => AutomationName;

    public string AccessibilityHelpText => AutomationHelpText;

    public ReactiveCommand<Unit, Unit> OpenTraceCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseTraceCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleTraceCaptureCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenMemoryCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseMemoryCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshMemoryCommand { get; }

    public ReactiveCommand<Unit, Unit> RetryMemoryCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenUsageCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseUsageCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshUsageCommand { get; }

    public ReactiveCommand<Unit, Unit> RetryUsageCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleUsageCaptureCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleTraceCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleMemoryCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleUsageCommand { get; }

    public bool IsTracePanelOpen
    {
        get => _isTracePanelOpen;
        private set => this.RaiseAndSetIfChanged(ref _isTracePanelOpen, value);
    }

    public bool IsMemoryPanelOpen
    {
        get => _isMemoryPanelOpen;
        private set => this.RaiseAndSetIfChanged(ref _isMemoryPanelOpen, value);
    }

    public bool IsUsagePanelOpen
    {
        get => _isUsagePanelOpen;
        private set => this.RaiseAndSetIfChanged(ref _isUsagePanelOpen, value);
    }

    public string TraceStatusCaption
    {
        get => _traceStatusCaption;
        private set => this.RaiseAndSetIfChanged(ref _traceStatusCaption, value);
    }

    public string MemoryStatusCaption
    {
        get => _memoryStatusCaption;
        private set => this.RaiseAndSetIfChanged(ref _memoryStatusCaption, value);
    }

    public string UsageStatusCaption
    {
        get => _usageStatusCaption;
        private set => this.RaiseAndSetIfChanged(ref _usageStatusCaption, value);
    }

    public AgentTraceAvailabilityProjection TraceAvailability => _traceAvailabilityProjection;

    public AgentUsageAvailabilityProjection UsageAvailability => _usageAvailabilityProjection;

    public AgentSessionContinuityAvailabilityProjection ContinuityAvailability =>
        _continuityAvailabilityProjection;

    public AgentMemoryAvailabilityProjection MemoryAvailability => _memoryAvailabilityProjection;

    public AgentMemoryInspectionViewModel MemoryInspection => _memoryInspection;

    public AgentUsageInspectionViewModel UsageInspection => _usageInspection;

    public Task<AgentTransparencyExportPackage> ExportAllAsync(string? workspaceRoot = null)
    {
        var workspaceKey = _memoryCoordinator.ResolveWorkspaceKey(workspaceRoot);
        return Task.FromResult(_lifecycleCoordinator.Export(workspaceKey));
    }

    public Task<AgentTransparencyBackupPackage> BackupAsync(string? workspaceRoot = null)
    {
        var workspaceKey = _memoryCoordinator.ResolveWorkspaceKey(workspaceRoot);
        return Task.FromResult(_lifecycleCoordinator.Backup(workspaceKey));
    }

    public int EffectiveDefaultPageSize => _settings.Current.Agents.TracePageSize;

    public int EffectiveMaxPageSize => _settings.Current.Agents.TraceMaxPageSize;

    public int ClampPageSize(int requestedPageSize) =>
        requestedPageSize <= 0
            ? EffectiveDefaultPageSize
            : Math.Min(requestedPageSize, EffectiveMaxPageSize);

    public Task<AgentTraceInspectionSummary> LoadTraceSummaryAsync() =>
        _traceInspection.LoadSummaryAsync();

    public Task<IReadOnlyList<AgentTraceRecord>> LoadTraceRecordsAsync(
        long afterOrderingSequence,
        int requestedPageSize) =>
        _traceInspection.LoadRecordsAsync(
            afterOrderingSequence,
            ClampPageSize(requestedPageSize));

    public void RefreshTracePresentation()
    {
        _traceInspection.ReloadPresentation(EffectiveDefaultPageSize);
        TraceStatusCaption = _traceInspection.AvailabilityCaption;
        this.RaisePropertyChanged(nameof(TraceInspection));
    }

    public AgentTraceInspectionViewModel TraceInspection => _traceInspection;

    public void SelectTraceRecord(long? orderingSequence)
    {
        _traceInspection.SelectRecord(orderingSequence);
        this.RaisePropertyChanged(nameof(TraceInspection));
    }

    public Task<AgentUsageInspectionSummary> LoadUsageSummaryAsync() =>
        _usageInspection.LoadSummaryAsync();

    public Task<IReadOnlyList<AgentUsageRecord>> LoadUsageRecordsAsync(
        long afterOrderingSequence,
        int requestedPageSize) =>
        _usageInspection.LoadRecordsAsync(
            afterOrderingSequence,
            ClampPageSize(requestedPageSize));

    public Task RefreshUsageSurfaceAsync()
    {
        return ReloadUsageAndPublishAsync(() => _usageInspection.ReloadAsync());
    }

    public Task RetryUsageSurfaceAsync()
    {
        return ReloadUsageAndPublishAsync(() => _usageInspection.RetryAsync());
    }

    public void SelectUsageRecord(long? orderingSequence)
    {
        _usageInspection.SelectRecord(orderingSequence);
        PublishUsagePresentation();
    }

    /// <summary>
    /// Townhall pushes selected direct-conversation context. Switching context
    /// clears selection and reloads when the memory panel is open.
    /// </summary>
    public Task BindMemoryTownhallContextAsync(AgentMemoryInspectionViewModel.TownhallContext context)
    {
        _memoryInspection.BindTownhallContext(context);
        PublishMemoryPresentation();
        if (_isMemoryPanelOpen)
        {
            return RefreshMemorySurfaceAsync();
        }

        return Task.CompletedTask;
    }

    public Task RefreshMemorySurfaceAsync()
    {
        return ReloadMemoryAndPublishAsync(() => _memoryInspection.ReloadAsync());
    }

    public Task RetryMemorySurfaceAsync()
    {
        return ReloadMemoryAndPublishAsync(() => _memoryInspection.RetryAsync());
    }

    public AgentMemoryOperationResult CreateMemoryFromDraft()
    {
        // Inspection owner reloads after accepted mutations.
        var result = _memoryInspection.CreateFromDraft();
        PublishMemoryPresentation();
        return result;
    }

    public AgentMemoryOperationResult CorrectSelectedMemory(string content)
    {
        var result = _memoryInspection.CorrectSelected(content);
        PublishMemoryPresentation();
        return result;
    }

    public AgentMemoryOperationResult DisableSelectedMemory()
    {
        var result = _memoryInspection.DisableSelected();
        PublishMemoryPresentation();
        return result;
    }

    public AgentMemoryOperationResult SupersedeSelectedMemory(string content)
    {
        var result = _memoryInspection.SupersedeSelected(content);
        PublishMemoryPresentation();
        return result;
    }

    public AgentMemoryOperationResult DeleteSelectedMemory()
    {
        var result = _memoryInspection.DeleteSelected();
        PublishMemoryPresentation();
        return result;
    }

    public void SelectMemoryRecord(AgentMemoryId? memoryId)
    {
        _memoryInspection.SelectRecord(memoryId);
        PublishMemoryPresentation();
    }

    private async Task OpenMemorySurfaceAsync()
    {
        // Inspect surfaces are mutually exclusive: opening one closes the others.
        CloseSiblingInspectSurfaces(keepMemory: true);
        IsMemoryPanelOpen = true;
        await RefreshMemorySurfaceAsync().ConfigureAwait(false);
    }

    private void CloseMemorySurface()
    {
        IsMemoryPanelOpen = false;
        MemoryStatusCaption = "Durable memory closed.";
    }

    private Task ToggleMemorySurfaceAsync()
    {
        if (IsMemoryPanelOpen)
        {
            CloseMemorySurface();
            return Task.CompletedTask;
        }

        return OpenMemorySurfaceAsync();
    }

    private async Task ReloadMemoryAndPublishAsync(Func<Task> reload)
    {
        await reload().ConfigureAwait(false);
        PublishMemoryPresentation();
    }

    private void PublishMemoryPresentation()
    {
        _memoryInspection.Refresh();
        MemoryStatusCaption = _memoryInspection.StatusCaption;
        this.RaisePropertyChanged(nameof(MemoryInspection));
    }

    private void OpenTraceSurface()
    {
        // Inspect surfaces are mutually exclusive: opening one closes the others.
        CloseSiblingInspectSurfaces(keepTrace: true);
        IsTracePanelOpen = true;
        RefreshTracePresentation();
    }

    private void CloseTraceSurface() => IsTracePanelOpen = false;

    private void ToggleTraceSurface()
    {
        if (IsTracePanelOpen)
        {
            CloseTraceSurface();
        }
        else
        {
            OpenTraceSurface();
        }
    }

    private void ToggleTraceCapture()
    {
        if (_traceInspection.Availability.CaptureEnabled)
        {
            _traceInspection.DisableCapture();
        }
        else
        {
            _traceInspection.EnableCapture();
        }

        RefreshTracePresentation();
    }

    private async Task OpenUsageSurfaceAsync()
    {
        // Inspect surfaces are mutually exclusive: opening one closes the others.
        CloseSiblingInspectSurfaces(keepUsage: true);
        IsUsagePanelOpen = true;
        await RefreshUsageSurfaceAsync().ConfigureAwait(false);
    }

    private void CloseUsageSurface()
    {
        IsUsagePanelOpen = false;
        UsageStatusCaption = "Usage evidence closed.";
    }

    private Task ToggleUsageSurfaceAsync()
    {
        if (IsUsagePanelOpen)
        {
            CloseUsageSurface();
            return Task.CompletedTask;
        }

        return OpenUsageSurfaceAsync();
    }

    /// <summary>
    /// Closes inspect surfaces other than the one being opened. Toggle-close of the
    /// active surface does not call this; only open paths enforce exclusivity.
    /// </summary>
    private void CloseSiblingInspectSurfaces(
        bool keepTrace = false,
        bool keepMemory = false,
        bool keepUsage = false)
    {
        if (!keepTrace && IsTracePanelOpen)
            CloseTraceSurface();
        if (!keepMemory && IsMemoryPanelOpen)
            CloseMemorySurface();
        if (!keepUsage && IsUsagePanelOpen)
            CloseUsageSurface();
    }

    private async Task ReloadUsageAndPublishAsync(Func<Task> reload)
    {
        await reload().ConfigureAwait(false);
        PublishUsagePresentation();
    }

    private void PublishUsagePresentation()
    {
        _usageInspection.Refresh();
        UsageStatusCaption = _usageInspection.StatusCaption;
        this.RaisePropertyChanged(nameof(UsageInspection));
    }

    private void ToggleUsageCapture()
    {
        if (_usageInspection.Availability.CaptureEnabled)
        {
            _usageInspection.DisableCapture();
        }
        else
        {
            _usageInspection.EnableCapture();
        }

        PublishUsagePresentation();
    }
}
