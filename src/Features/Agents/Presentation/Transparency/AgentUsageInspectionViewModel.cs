using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Presentation.Transparency;

/// <summary>
/// Presentation owner for the usage and cost inspection surface.
/// Reads only through <see cref="AgentUsageCoordinator"/>; missing evidence is
/// never presented as verified zero or invoice facts.
/// </summary>
internal sealed class AgentUsageInspectionViewModel
{
    public const int MaxRetryAttempts = 3;

    private readonly AgentUsageCoordinator _coordinator;
    private readonly AgentUsageAvailabilityProjection _availability;
    private readonly Func<string?> _workspaceRootProvider;
    private readonly object _gate = new();
    private int _loadGeneration;
    private int _retryAttempts;
    private AgentUsageSurfaceState _surfaceState = AgentUsageSurfaceState.Loading;
    private string _statusCaption = "Loading usage evidence…";
    private string? _failureReason;
    private long? _selectedOrderingSequence;
    private IReadOnlyList<AgentUsageRecord> _records = Array.Empty<AgentUsageRecord>();
    private AgentUsageInspectionSummary? _summary;

    public AgentUsageInspectionViewModel(
        AgentUsageCoordinator coordinator,
        AgentUsageAvailabilityProjection availability,
        IWorkspaceActionAuthority? workspaceAuthority = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _availability = availability ?? throw new ArgumentNullException(nameof(availability));
        _workspaceRootProvider = AgentContinuityWorkspaceRootProvider
            .CreateOpenedWorkspaceProvider(workspaceAuthority);
    }

    public AgentUsageAvailabilityState Availability => _availability.CurrentState;

    public string AvailabilityCaption => Availability.FormatStatusCaption();

    public AgentUsageSurfaceState SurfaceState
    {
        get
        {
            lock (_gate)
            {
                return _surfaceState;
            }
        }
    }

    public string StatusCaption
    {
        get
        {
            lock (_gate)
            {
                return _statusCaption;
            }
        }
    }

    public string? FailureReason
    {
        get
        {
            lock (_gate)
            {
                return _failureReason;
            }
        }
    }

    public bool CanRetry
    {
        get
        {
            lock (_gate)
            {
                return _surfaceState == AgentUsageSurfaceState.Failed
                    && _retryAttempts < MaxRetryAttempts;
            }
        }
    }

    public int RetryAttempts
    {
        get
        {
            lock (_gate)
            {
                return _retryAttempts;
            }
        }
    }

    public IReadOnlyList<AgentUsageRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return _records;
            }
        }
    }

    public AgentUsageRecord? SelectedRecord
    {
        get
        {
            lock (_gate)
            {
                if (_selectedOrderingSequence is not { } selected)
                {
                    return null;
                }

                foreach (var record in _records)
                {
                    if (record.OrderingSequence == selected)
                    {
                        return record;
                    }
                }

                return null;
            }
        }
    }

    public AgentUsageInspectionSummary? Summary
    {
        get
        {
            lock (_gate)
            {
                return _summary;
            }
        }
    }

    public void SelectRecord(long? orderingSequence)
    {
        lock (_gate)
        {
            _selectedOrderingSequence = orderingSequence;
        }
    }

    public void EnableCapture()
    {
        _coordinator.EnableCapture();
        _availability.Refresh(force: true);
    }

    public void DisableCapture()
    {
        _coordinator.DisableCapture();
        _availability.Refresh(force: true);
    }

    public void Refresh() => _availability.Refresh(force: true);

    public Task ReloadAsync()
    {
        ReloadCore(isRetry: false, observeLoading: true);
        return Task.CompletedTask;
    }

    public Task RetryAsync()
    {
        lock (_gate)
        {
            if (_surfaceState != AgentUsageSurfaceState.Failed
                || _retryAttempts >= MaxRetryAttempts)
            {
                return Task.CompletedTask;
            }

            _retryAttempts++;
        }

        ReloadCore(isRetry: true, observeLoading: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Synchronous reload used by tests and mutation-free refresh paths.
    /// </summary>
    public void ReloadNow() => ReloadCore(isRetry: false, observeLoading: false);

    public Task<AgentUsageInspectionSummary> LoadSummaryAsync()
    {
        if (!TryResolveWorkspaceKey(out var workspaceKey, out var denial))
        {
            throw new InvalidOperationException(denial ?? "Workspace is unavailable.");
        }

        return Task.FromResult(_coordinator.Inspector.GetSummary(workspaceKey));
    }

    public Task<IReadOnlyList<AgentUsageRecord>> LoadRecordsAsync(
        long afterOrderingSequence,
        int maxRecords)
    {
        if (!TryResolveWorkspaceKey(out var workspaceKey, out var denial))
        {
            throw new InvalidOperationException(denial ?? "Workspace is unavailable.");
        }

        return Task.FromResult(_coordinator.Inspector.GetRecords(
            workspaceKey,
            afterOrderingSequence,
            maxRecords));
    }

    private void ReloadCore(bool isRetry, bool observeLoading)
    {
        int generation;
        lock (_gate)
        {
            generation = ++_loadGeneration;
            if (observeLoading)
            {
                _surfaceState = AgentUsageSurfaceState.Loading;
                _statusCaption = isRetry
                    ? $"Retrying usage load ({_retryAttempts}/{MaxRetryAttempts})…"
                    : "Loading usage evidence…";
            }

            _failureReason = null;
        }

        if (!TryResolveWorkspaceKey(out var workspaceKey, out var denial))
        {
            CommitLoad(
                generation,
                AgentUsageSurfaceState.Unavailable,
                denial ?? "Opened workspace is required.",
                failureReason: denial,
                records: Array.Empty<AgentUsageRecord>(),
                summary: null,
                clearSelection: true);
            return;
        }

        try
        {
            var summary = _coordinator.Inspector.GetSummary(workspaceKey);
            var records = _coordinator.Inspector.GetRecords(
                workspaceKey,
                afterOrderingSequence: 0,
                maxRecords: AgentUsageCaptureLimits.DefaultMaxRecordsPerPage);

            if (records.Count == 0)
            {
                CommitLoad(
                    generation,
                    AgentUsageSurfaceState.Empty,
                    "No usage or cost evidence for the opened workspace.",
                    failureReason: null,
                    records,
                    summary,
                    clearSelection: true);
                return;
            }

            CommitLoad(
                generation,
                AgentUsageSurfaceState.Ready,
                FormatReadyCaption(summary),
                failureReason: null,
                records,
                summary,
                clearSelection: false);
        }
        catch (Exception ex)
        {
            CommitLoad(
                generation,
                AgentUsageSurfaceState.Failed,
                "Usage evidence load failed.",
                failureReason: ex.Message,
                records: Array.Empty<AgentUsageRecord>(),
                summary: null,
                clearSelection: true);
        }
    }

    private void CommitLoad(
        int generation,
        AgentUsageSurfaceState state,
        string statusCaption,
        string? failureReason,
        IReadOnlyList<AgentUsageRecord> records,
        AgentUsageInspectionSummary? summary,
        bool clearSelection)
    {
        lock (_gate)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            _surfaceState = state;
            _statusCaption = statusCaption;
            _failureReason = failureReason;
            _records = records;
            _summary = summary;
            if (clearSelection)
            {
                _selectedOrderingSequence = null;
            }
            else if (_selectedOrderingSequence is { } selected
                     && !ContainsOrdering(records, selected))
            {
                _selectedOrderingSequence = null;
            }

            if (state is AgentUsageSurfaceState.Ready or AgentUsageSurfaceState.Empty)
            {
                _retryAttempts = 0;
            }
        }

        _availability.Refresh(force: true);
    }

    private bool TryResolveWorkspaceKey(
        out AgentDurableWorkspaceStorageKey workspaceKey,
        out string? denial)
    {
        var workspaceRoot = _workspaceRootProvider();
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            workspaceKey = default;
            denial = "Opened workspace is required for usage inspection.";
            return false;
        }

        workspaceKey = _coordinator.ResolveWorkspaceKey(workspaceRoot);
        if (string.Equals(
                workspaceKey.Value,
                PathDerivedAgentDurableWorkspaceStorageKeyResolver.UnboundWorkspaceKey,
                StringComparison.Ordinal))
        {
            denial = "Opened workspace is required for usage inspection.";
            return false;
        }

        denial = null;
        return true;
    }

    private static bool ContainsOrdering(IReadOnlyList<AgentUsageRecord> records, long orderingSequence)
    {
        foreach (var record in records)
        {
            if (record.OrderingSequence == orderingSequence)
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatReadyCaption(AgentUsageInspectionSummary summary)
    {
        var costPart = summary.HasVerifiedTotalCost && summary.TotalCostCurrency is not null
            ? $"{summary.TotalCostValue:F4} {summary.TotalCostCurrency} verified aggregate (not an invoice)"
            : "verified cost unavailable";
        return $"{summary.TotalRecords} usage record(s); {costPart}.";
    }
}
