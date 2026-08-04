using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Presentation.Memory;

/// <summary>
/// Presentation owner for the scoped durable memory lifecycle surface.
/// Mutations route only through <see cref="AgentMemoryCoordinator"/>; influence
/// payloads never appear as editable lifecycle records.
/// </summary>
internal sealed class AgentMemoryInspectionViewModel
{
    public const int MaxRetryAttempts = 3;

    private readonly AgentMemoryCoordinator _coordinator;
    private readonly AgentMemoryAvailabilityProjection _availability;
    private readonly Func<string?> _workspaceRootProvider;
    private readonly IActorCatalog _actorCatalog;
    private readonly object _gate = new();
    private int _loadGeneration;
    private int _retryAttempts;
    private TownhallContext _townhallContext = TownhallContext.Empty;
    private AgentMemorySurfaceState _surfaceState = AgentMemorySurfaceState.Loading;
    private string _statusCaption = "Loading durable memory…";
    private string? _failureReason;
    private string? _submitDenialReason = "Select a scope and provide content.";
    private string _draftContent = string.Empty;
    private AgentMemoryScope _selectedScope = AgentMemoryScope.ProjectShared;
    private AgentMemoryId? _selectedMemoryId;
    private IReadOnlyList<AgentMemoryRecord> _records = Array.Empty<AgentMemoryRecord>();
    private AgentMemoryInspectionSummary? _summary;

    public AgentMemoryInspectionViewModel(
        AgentMemoryCoordinator coordinator,
        AgentMemoryAvailabilityProjection availability,
        IActorCatalog actorCatalog,
        IWorkspaceActionAuthority? workspaceAuthority = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _availability = availability ?? throw new ArgumentNullException(nameof(availability));
        _actorCatalog = actorCatalog ?? throw new ArgumentNullException(nameof(actorCatalog));
        _workspaceRootProvider = AgentContinuityWorkspaceRootProvider
            .CreateOpenedWorkspaceProvider(workspaceAuthority);
    }

    public AgentMemoryAvailabilityState Availability => _availability.CurrentState;

    public string AvailabilityCaption => Availability.FormatStatusCaption();

    public AgentMemorySurfaceState SurfaceState
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

    public string? SubmitDenialReason
    {
        get
        {
            lock (_gate)
            {
                return _submitDenialReason;
            }
        }
    }

    public bool CanSubmitCreate
    {
        get
        {
            lock (_gate)
            {
                return _submitDenialReason is null
                    && !string.IsNullOrWhiteSpace(_draftContent)
                    && TryResolveWorkspaceKey(out _)
                    && TryBuildScopeTarget(_selectedScope, out _);
            }
        }
    }

    public bool CanRetry
    {
        get
        {
            lock (_gate)
            {
                return _surfaceState == AgentMemorySurfaceState.Failed
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

    public string DraftContent
    {
        get
        {
            lock (_gate)
            {
                return _draftContent;
            }
        }
        set
        {
            lock (_gate)
            {
                _draftContent = value ?? string.Empty;
                RefreshSubmitDenialLocked();
            }
        }
    }

    public AgentMemoryScope SelectedScope
    {
        get
        {
            lock (_gate)
            {
                return _selectedScope;
            }
        }
        set
        {
            lock (_gate)
            {
                _selectedScope = value;
                RefreshSubmitDenialLocked();
            }
        }
    }

    public IReadOnlyList<AgentMemoryRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return _records;
            }
        }
    }

    public AgentMemoryRecord? SelectedRecord
    {
        get
        {
            lock (_gate)
            {
                if (_selectedMemoryId is not { } selectedId)
                {
                    return null;
                }

                foreach (var record in _records)
                {
                    if (record.MemoryId.Equals(selectedId))
                    {
                        return record;
                    }
                }

                return null;
            }
        }
    }

    public AgentMemoryInspectionSummary? Summary
    {
        get
        {
            lock (_gate)
            {
                return _summary;
            }
        }
    }

    public TownhallContext ActiveTownhallContext
    {
        get
        {
            lock (_gate)
            {
                return _townhallContext;
            }
        }
    }

    /// <summary>
    /// Influence evidence is recorded separately from lifecycle revisions and is
    /// never projected as an editable <see cref="AgentMemoryRecord"/>.
    /// </summary>
    public string InfluenceEvidenceCaption =>
        "Influence evidence is attribution-only and is not editable lifecycle memory.";

    public void BindTownhallContext(TownhallContext context)
    {
        lock (_gate)
        {
            _townhallContext = context;
            _selectedMemoryId = null;
            _retryAttempts = 0;
            RefreshSubmitDenialLocked();
        }
    }

    public void SelectRecord(AgentMemoryId? memoryId)
    {
        lock (_gate)
        {
            _selectedMemoryId = memoryId;
        }
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
            if (_surfaceState != AgentMemorySurfaceState.Failed
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
    /// Synchronous reload used after lifecycle mutations so the UI thread never
    /// blocks on a completed async yield.
    /// </summary>
    public void ReloadNow() => ReloadCore(isRetry: false, observeLoading: false);

    public Task<AgentMemoryInspectionSummary> LoadSummaryAsync(string? workspaceRoot = null)
    {
        if (!TryResolveWorkspaceKey(workspaceRoot, out var workspaceKey, out var denial))
        {
            throw new InvalidOperationException(denial ?? "Workspace is unavailable.");
        }

        return Task.FromResult(_coordinator.Inspector.GetSummary(workspaceKey));
    }

    public Task<IReadOnlyList<AgentMemoryRecord>> LoadRecordsAsync(
        string? workspaceRoot,
        long afterOrderingSequence,
        int maxRecords,
        bool includeDeleted = false)
    {
        if (!TryResolveWorkspaceKey(workspaceRoot, out var workspaceKey, out var denial))
        {
            throw new InvalidOperationException(denial ?? "Workspace is unavailable.");
        }

        return Task.FromResult(_coordinator.Inspector.GetRecords(
            workspaceKey,
            afterOrderingSequence,
            maxRecords,
            includeDeleted));
    }

    public Task<AgentMemoryOperationResult> CreateAsync(AgentMemoryCreateRequest request) =>
        Task.FromResult(_coordinator.Create(request));

    public Task<AgentMemoryOperationResult> CorrectAsync(AgentMemoryCorrectRequest request) =>
        Task.FromResult(_coordinator.Correct(request));

    public Task<AgentMemoryOperationResult> DisableAsync(AgentMemoryDisableRequest request) =>
        Task.FromResult(_coordinator.Disable(request));

    public Task<AgentMemoryOperationResult> SupersedeAsync(AgentMemorySupersedeRequest request) =>
        Task.FromResult(_coordinator.Supersede(request));

    public Task<AgentMemoryOperationResult> DeleteAsync(AgentMemoryDeleteRequest request) =>
        Task.FromResult(_coordinator.Delete(request));

    public AgentMemoryOperationResult CreateFromDraft()
    {
        if (!TryResolveWorkspaceKey(out var workspaceKey, out var workspaceDenial))
        {
            return Denied(AgentMemoryOperationKind.Create, workspaceDenial);
        }

        string? scopeDenial;
        AgentMemoryScopeTarget scopeTarget;
        string content;
        lock (_gate)
        {
            content = _draftContent;
            if (string.IsNullOrWhiteSpace(content))
            {
                return Denied(AgentMemoryOperationKind.Create, "Content is required.");
            }

            if (!TryBuildScopeTarget(_selectedScope, out scopeTarget, out scopeDenial))
            {
                return Denied(AgentMemoryOperationKind.Create, scopeDenial);
            }
        }

        var result = _coordinator.Create(new AgentMemoryCreateRequest(
            workspaceKey,
            scopeTarget,
            content.Trim(),
            CreateUserProvenance("create"),
            NewIdempotencyKey("create")));

        if (IsAccepted(result))
        {
            lock (_gate)
            {
                _draftContent = string.Empty;
                if (result.MemoryId is { } createdId)
                {
                    _selectedMemoryId = createdId;
                }

                RefreshSubmitDenialLocked();
            }

            ReloadNow();
        }

        return result;
    }

    public AgentMemoryOperationResult CorrectSelected(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Denied(AgentMemoryOperationKind.Correct, "Content is required.");
        }

        if (!TryResolveWorkspaceKey(out var workspaceKey, out var workspaceDenial))
        {
            return Denied(AgentMemoryOperationKind.Correct, workspaceDenial);
        }

        var selected = SelectedRecord;
        if (selected is null)
        {
            return Denied(AgentMemoryOperationKind.Correct, "Select a memory record first.");
        }

        var result = _coordinator.Correct(new AgentMemoryCorrectRequest(
            workspaceKey,
            selected.MemoryId,
            content.Trim(),
            CreateUserProvenance("correct"),
            NewIdempotencyKey("correct")));
        if (IsAccepted(result))
        {
            ReloadNow();
        }

        return result;
    }

    public AgentMemoryOperationResult DisableSelected()
    {
        if (!TryResolveWorkspaceKey(out var workspaceKey, out var workspaceDenial))
        {
            return Denied(AgentMemoryOperationKind.Disable, workspaceDenial);
        }

        var selected = SelectedRecord;
        if (selected is null)
        {
            return Denied(AgentMemoryOperationKind.Disable, "Select a memory record first.");
        }

        var result = _coordinator.Disable(new AgentMemoryDisableRequest(
            workspaceKey,
            selected.MemoryId,
            CreateUserProvenance("disable"),
            NewIdempotencyKey("disable")));
        if (IsAccepted(result))
        {
            ReloadNow();
        }

        return result;
    }

    public AgentMemoryOperationResult SupersedeSelected(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Denied(AgentMemoryOperationKind.Supersede, "Content is required.");
        }

        if (!TryResolveWorkspaceKey(out var workspaceKey, out var workspaceDenial))
        {
            return Denied(AgentMemoryOperationKind.Supersede, workspaceDenial);
        }

        var selected = SelectedRecord;
        if (selected is null)
        {
            return Denied(AgentMemoryOperationKind.Supersede, "Select a memory record first.");
        }

        var result = _coordinator.Supersede(new AgentMemorySupersedeRequest(
            workspaceKey,
            selected.MemoryId,
            selected.ScopeTarget,
            content.Trim(),
            CreateUserProvenance("supersede"),
            NewIdempotencyKey("supersede")));
        if (IsAccepted(result))
        {
            ReloadNow();
        }

        return result;
    }

    public AgentMemoryOperationResult DeleteSelected()
    {
        if (!TryResolveWorkspaceKey(out var workspaceKey, out var workspaceDenial))
        {
            return Denied(AgentMemoryOperationKind.Delete, workspaceDenial);
        }

        var selected = SelectedRecord;
        if (selected is null)
        {
            return Denied(AgentMemoryOperationKind.Delete, "Select a memory record first.");
        }

        var result = _coordinator.Delete(new AgentMemoryDeleteRequest(
            workspaceKey,
            selected.MemoryId,
            CreateUserProvenance("delete"),
            NewIdempotencyKey("delete")));
        if (IsAccepted(result))
        {
            ReloadNow();
        }

        return result;
    }

    private void ReloadCore(bool isRetry, bool observeLoading)
    {
        int generation;
        lock (_gate)
        {
            generation = ++_loadGeneration;
            if (observeLoading)
            {
                _surfaceState = AgentMemorySurfaceState.Loading;
                _statusCaption = isRetry
                    ? $"Retrying durable memory load ({_retryAttempts}/{MaxRetryAttempts})…"
                    : "Loading durable memory…";
            }

            _failureReason = null;
        }

        if (!TryResolveWorkspaceKey(out var workspaceKey, out var denial))
        {
            CommitLoad(
                generation,
                AgentMemorySurfaceState.Unavailable,
                denial ?? "Opened workspace is required.",
                failureReason: denial,
                records: Array.Empty<AgentMemoryRecord>(),
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
                maxRecords: AgentMemoryLimits.MaxRecordsPerPage,
                includeDeleted: false);

            if (records.Count == 0)
            {
                CommitLoad(
                    generation,
                    AgentMemorySurfaceState.Empty,
                    "No durable memory records for the opened workspace.",
                    failureReason: null,
                    records,
                    summary,
                    clearSelection: true);
                return;
            }

            CommitLoad(
                generation,
                AgentMemorySurfaceState.Ready,
                $"{summary.ActiveRecords} active / {summary.TotalRecords} total",
                failureReason: null,
                records,
                summary,
                clearSelection: false);
        }
        catch (Exception ex)
        {
            // Failed must never masquerade as Empty.
            CommitLoad(
                generation,
                AgentMemorySurfaceState.Failed,
                "Failed to load durable memory.",
                failureReason: ex.Message,
                records: Array.Empty<AgentMemoryRecord>(),
                summary: null,
                clearSelection: true);
        }
    }

    private void CommitLoad(
        int generation,
        AgentMemorySurfaceState state,
        string statusCaption,
        string? failureReason,
        IReadOnlyList<AgentMemoryRecord> records,
        AgentMemoryInspectionSummary? summary,
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
                _selectedMemoryId = null;
            }
            else if (_selectedMemoryId is { } selectedId
                && !ContainsRecord(records, selectedId))
            {
                _selectedMemoryId = null;
            }

            if (state is AgentMemorySurfaceState.Ready or AgentMemorySurfaceState.Empty)
            {
                _retryAttempts = 0;
            }

            RefreshSubmitDenialLocked();
        }

        if (state is AgentMemorySurfaceState.Ready or AgentMemorySurfaceState.Empty)
        {
            _availability.Refresh(force: true);
        }
    }

    private bool TryResolveWorkspaceKey(out AgentDurableWorkspaceStorageKey workspaceKey) =>
        TryResolveWorkspaceKey(workspaceRoot: null, out workspaceKey, out _);

    private bool TryResolveWorkspaceKey(
        out AgentDurableWorkspaceStorageKey workspaceKey,
        out string? denial) =>
        TryResolveWorkspaceKey(workspaceRoot: null, out workspaceKey, out denial);

    private bool TryResolveWorkspaceKey(
        string? workspaceRoot,
        out AgentDurableWorkspaceStorageKey workspaceKey,
        out string? denial)
    {
        workspaceKey = default;
        denial = null;

        var resolvedRoot = workspaceRoot ?? _workspaceRootProvider();
        if (string.IsNullOrWhiteSpace(resolvedRoot))
        {
            denial = "Opened workspace is required. Memory never defaults to ws:unbound.";
            return false;
        }

        workspaceKey = _coordinator.ResolveWorkspaceKey(resolvedRoot);
        if (string.Equals(
                workspaceKey.Value,
                PathDerivedAgentDurableWorkspaceStorageKeyResolver.UnboundWorkspaceKey,
                StringComparison.Ordinal))
        {
            denial = "Opened workspace is required. Memory never defaults to ws:unbound.";
            return false;
        }

        return true;
    }

    private bool TryBuildScopeTarget(
        AgentMemoryScope scope,
        out AgentMemoryScopeTarget scopeTarget) =>
        TryBuildScopeTarget(scope, out scopeTarget, out _);

    private bool TryBuildScopeTarget(
        AgentMemoryScope scope,
        out AgentMemoryScopeTarget scopeTarget,
        out string? denial)
    {
        scopeTarget = null!;
        denial = null;
        var context = _townhallContext;

        switch (scope)
        {
            case AgentMemoryScope.ProjectShared:
            {
                var projectId = context.ProjectId;
                if (string.IsNullOrWhiteSpace(projectId)
                    && TryResolveWorkspaceKey(out var workspaceKey, out _))
                {
                    // Opened workspace identity is the Project/Shared scope key.
                    projectId = workspaceKey.Value;
                }

                if (string.IsNullOrWhiteSpace(projectId))
                {
                    denial = "Project/shared scope requires an opened workspace identity.";
                    return false;
                }

                scopeTarget = new AgentMemoryScopeTarget(
                    AgentMemoryScope.ProjectShared,
                    projectId: projectId);
                return true;
            }

            case AgentMemoryScope.Conversation:
                if (context.ConversationId is not { } conversationId)
                {
                    denial = "Conversation scope requires a selected Townhall conversation.";
                    return false;
                }

                scopeTarget = new AgentMemoryScopeTarget(
                    AgentMemoryScope.Conversation,
                    conversationId: conversationId);
                return true;

            case AgentMemoryScope.Agent:
                if (context.AgentActorId is not { } agentActorId)
                {
                    denial = "Agent scope requires a selected Townhall direct-conversation agent.";
                    return false;
                }

                scopeTarget = new AgentMemoryScopeTarget(
                    AgentMemoryScope.Agent,
                    actorId: agentActorId);
                return true;

            case AgentMemoryScope.Session:
                if (string.IsNullOrWhiteSpace(context.SessionId))
                {
                    denial = "Session scope requires a live session on the selected Townhall conversation.";
                    return false;
                }

                scopeTarget = new AgentMemoryScopeTarget(
                    AgentMemoryScope.Session,
                    sessionId: context.SessionId);
                return true;

            default:
                denial = $"Unsupported memory scope: {scope}.";
                return false;
        }
    }

    private void RefreshSubmitDenialLocked()
    {
        if (string.IsNullOrWhiteSpace(_draftContent))
        {
            _submitDenialReason = "Content is required to create memory.";
            return;
        }

        if (!TryResolveWorkspaceKey(out _, out var workspaceDenial))
        {
            _submitDenialReason = workspaceDenial;
            return;
        }

        if (!TryBuildScopeTarget(_selectedScope, out _, out var scopeDenial))
        {
            _submitDenialReason = scopeDenial;
            return;
        }

        _submitDenialReason = null;
    }

    private AgentMemoryProvenance CreateUserProvenance(string operation) =>
        new(
            _actorCatalog.CanonicalHuman.Id,
            sourceRevision: $"user:{operation}:{Guid.NewGuid():N}",
            AgentMemorySourceKind.User,
            sourceDescription: $"Townhall memory {operation}");

    private static string NewIdempotencyKey(string operation) =>
        $"memory-ui:{operation}:{Guid.NewGuid():N}";

    private static AgentMemoryOperationResult Denied(
        AgentMemoryOperationKind operationKind,
        string? reason) =>
        new(
            AgentMemoryOperationStatus.InvalidRequest,
            operationKind,
            reason: reason ?? "Request denied.");

    private static bool IsAccepted(AgentMemoryOperationResult result) =>
        result.Status is AgentMemoryOperationStatus.Accepted
            or AgentMemoryOperationStatus.ConflictDetected
            or AgentMemoryOperationStatus.DuplicateIgnored;

    private static bool ContainsRecord(
        IReadOnlyList<AgentMemoryRecord> records,
        AgentMemoryId memoryId)
    {
        foreach (var record in records)
        {
            if (record.MemoryId.Equals(memoryId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Townhall-derived scope context. Channel selection may leave agent/session
    /// empty; missing required fields disable create with a visible reason.
    /// </summary>
    internal readonly struct TownhallContext
    {
        public TownhallContext(
            ConversationId? conversationId,
            ActorId? agentActorId,
            string? sessionId,
            string? projectId)
        {
            ConversationId = conversationId;
            AgentActorId = agentActorId;
            SessionId = sessionId;
            ProjectId = projectId;
        }

        public static TownhallContext Empty { get; } = new(
            conversationId: null,
            agentActorId: null,
            sessionId: null,
            projectId: null);

        public ConversationId? ConversationId { get; }

        public ActorId? AgentActorId { get; }

        public string? SessionId { get; }

        public string? ProjectId { get; }
    }
}
