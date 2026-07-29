using System;
using System.Linq;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Application.Memory;

internal sealed class AgentMemoryCoordinator : IAgentMemoryCoordinator
{
    private readonly AgentMemoryStoreWriter _writer;
    private readonly AgentMemoryInspector _inspector;
    private readonly IAgentMemoryPolicyEvaluator _policyEvaluator;
    private readonly AgentDurableWorkspaceStorageKeyResolver _workspaceKeyResolver;
    private readonly IAgentDurableRecordStore _store;

    public AgentMemoryCoordinator(
        AgentMemoryStoreWriter writer,
        AgentMemoryInspector inspector,
        IAgentMemoryPolicyEvaluator policyEvaluator,
        AgentDurableWorkspaceStorageKeyResolver workspaceKeyResolver,
        IAgentDurableRecordStore store)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _policyEvaluator = policyEvaluator ?? throw new ArgumentNullException(nameof(policyEvaluator));
        _workspaceKeyResolver = workspaceKeyResolver
            ?? throw new ArgumentNullException(nameof(workspaceKeyResolver));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AgentMemoryInspector Inspector => _inspector;

    public AgentDurableWorkspaceStorageKey ResolveWorkspaceKey(string? workspaceRoot) =>
        _workspaceKeyResolver.Resolve(workspaceRoot);

    public AgentMemoryOperationResult Create(AgentMemoryCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureWorkspaceWritable(request.WorkspaceKey);

        var existing = _inspector.ReplayAll(request.WorkspaceKey, includeDeleted: false);
        var policy = _policyEvaluator.EvaluateCreate(request, existing);
        if (policy.ConflictKind == AgentMemoryConflictKind.ScopeConflict
            && policy.Reason?.Contains("maximum", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Rejected(
                AgentMemoryOperationKind.Create,
                AgentMemoryOperationStatus.InvalidRequest,
                policy.Reason,
                policy.ConflictKind);
        }

        var now = DateTimeOffset.UtcNow;
        var payload = BuildPayload(
            request.MemoryId,
            AgentMemoryOperationKind.Create,
            request.ScopeTarget,
            request.Content,
            request.Provenance,
            AgentMemoryStatus.Active,
            createdAtUtc: now,
            updatedAtUtc: now,
            lastValidatedAtUtc: request.LastValidatedAtUtc,
            policy: policy);

        var append = _writer.Append(
            request.WorkspaceKey,
            request.IdempotencyKey,
            payload,
            BuildScopeReferences(request.ScopeTarget),
            now);

        return MapAppendResult(append, AgentMemoryOperationKind.Create, request.MemoryId, policy.ConflictKind);
    }

    public AgentMemoryOperationResult Correct(AgentMemoryCorrectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureWorkspaceWritable(request.WorkspaceKey);

        var existing = _inspector.TryGetRecord(request.WorkspaceKey, request.MemoryId);
        if (existing is null)
        {
            return NotFound(AgentMemoryOperationKind.Correct);
        }

        if (!WorkspaceMatches(existing.WorkspaceKey, request.WorkspaceKey))
        {
            return WorkspaceDenied(AgentMemoryOperationKind.Correct);
        }

        var policy = _policyEvaluator.EvaluateCorrect(request, existing);
        if (policy.ConflictKind == AgentMemoryConflictKind.ScopeConflict)
        {
            return Rejected(
                AgentMemoryOperationKind.Correct,
                AgentMemoryOperationStatus.Rejected,
                policy.Reason,
                policy.ConflictKind);
        }

        var now = DateTimeOffset.UtcNow;
        var payload = BuildPayload(
            request.MemoryId,
            AgentMemoryOperationKind.Correct,
            existing.ScopeTarget,
            request.Content,
            request.Provenance,
            AgentMemoryStatus.Active,
            createdAtUtc: existing.CreatedAtUtc,
            updatedAtUtc: now,
            lastValidatedAtUtc: request.LastValidatedAtUtc ?? existing.LastValidatedAtUtc,
            policy: policy);

        var append = _writer.Append(
            request.WorkspaceKey,
            request.IdempotencyKey,
            payload,
            BuildScopeReferences(existing.ScopeTarget),
            now);

        return MapAppendResult(append, AgentMemoryOperationKind.Correct, request.MemoryId, policy.ConflictKind);
    }

    public AgentMemoryOperationResult Disable(AgentMemoryDisableRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureWorkspaceWritable(request.WorkspaceKey);

        var existing = _inspector.TryGetRecord(request.WorkspaceKey, request.MemoryId);
        if (existing is null)
        {
            return NotFound(AgentMemoryOperationKind.Disable);
        }

        if (!WorkspaceMatches(existing.WorkspaceKey, request.WorkspaceKey))
        {
            return WorkspaceDenied(AgentMemoryOperationKind.Disable);
        }

        var now = DateTimeOffset.UtcNow;
        var payload = BuildPayload(
            request.MemoryId,
            AgentMemoryOperationKind.Disable,
            existing.ScopeTarget,
            existing.Content,
            request.Provenance,
            AgentMemoryStatus.Disabled,
            createdAtUtc: existing.CreatedAtUtc,
            updatedAtUtc: now,
            lastValidatedAtUtc: existing.LastValidatedAtUtc,
            policy: AgentMemoryPolicyEvaluation.Clean(),
            supersededByMemoryId: existing.SupersededByMemoryId,
            supersedesMemoryId: existing.SupersedesMemoryId);

        var append = _writer.Append(
            request.WorkspaceKey,
            request.IdempotencyKey,
            payload,
            BuildScopeReferences(existing.ScopeTarget),
            now);

        return MapAppendResult(append, AgentMemoryOperationKind.Disable, request.MemoryId);
    }

    public AgentMemoryOperationResult Supersede(AgentMemorySupersedeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureWorkspaceWritable(request.WorkspaceKey);

        var superseded = _inspector.TryGetRecord(request.WorkspaceKey, request.SupersededMemoryId);
        if (superseded is null)
        {
            return NotFound(AgentMemoryOperationKind.Supersede);
        }

        if (!WorkspaceMatches(superseded.WorkspaceKey, request.WorkspaceKey))
        {
            return WorkspaceDenied(AgentMemoryOperationKind.Supersede);
        }

        var policy = _policyEvaluator.EvaluateSupersede(request, superseded);
        if (policy.ConflictKind == AgentMemoryConflictKind.ScopeConflict)
        {
            return Rejected(
                AgentMemoryOperationKind.Supersede,
                AgentMemoryOperationStatus.Rejected,
                policy.Reason,
                policy.ConflictKind);
        }

        var now = DateTimeOffset.UtcNow;

        var supersededPayload = BuildPayload(
            request.SupersededMemoryId,
            AgentMemoryOperationKind.Supersede,
            superseded.ScopeTarget,
            superseded.Content,
            request.Provenance,
            AgentMemoryStatus.Superseded,
            createdAtUtc: superseded.CreatedAtUtc,
            updatedAtUtc: now,
            lastValidatedAtUtc: superseded.LastValidatedAtUtc,
            policy: new AgentMemoryPolicyEvaluation(
                AgentMemoryConflictKind.Superseded,
                isPoisoningSuspect: superseded.IsPoisoningSuspect,
                isStaleFact: superseded.IsStaleFact),
            supersededByMemoryId: request.ReplacementMemoryId,
            supersedesMemoryId: superseded.SupersedesMemoryId);

        var supersededAppend = _writer.Append(
            request.WorkspaceKey,
            $"{request.IdempotencyKey}:superseded",
            supersededPayload,
            BuildScopeReferences(superseded.ScopeTarget),
            now);

        if (supersededAppend.Status == AgentDurableRecordAppendStatus.DuplicateIgnored)
        {
            return Duplicate(AgentMemoryOperationKind.Supersede, request.ReplacementMemoryId);
        }

        if (supersededAppend.Status != AgentDurableRecordAppendStatus.Appended)
        {
            return Rejected(
                AgentMemoryOperationKind.Supersede,
                AgentMemoryOperationStatus.Rejected,
                $"Store rejected supersede mark: {supersededAppend.Status}");
        }

        var replacementPayload = BuildPayload(
            request.ReplacementMemoryId,
            AgentMemoryOperationKind.Create,
            request.ScopeTarget,
            request.Content,
            request.Provenance,
            AgentMemoryStatus.Active,
            createdAtUtc: now,
            updatedAtUtc: now,
            lastValidatedAtUtc: request.LastValidatedAtUtc,
            policy: policy,
            supersedesMemoryId: request.SupersededMemoryId);

        var replacementAppend = _writer.Append(
            request.WorkspaceKey,
            request.IdempotencyKey,
            replacementPayload,
            BuildScopeReferences(request.ScopeTarget),
            now);

        return MapAppendResult(
            replacementAppend,
            AgentMemoryOperationKind.Supersede,
            request.ReplacementMemoryId,
            policy.ConflictKind);
    }

    public AgentMemoryOperationResult Delete(AgentMemoryDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureWorkspaceWritable(request.WorkspaceKey);

        var existing = _inspector.TryGetRecord(request.WorkspaceKey, request.MemoryId);
        if (existing is null)
        {
            return NotFound(AgentMemoryOperationKind.Delete);
        }

        if (!WorkspaceMatches(existing.WorkspaceKey, request.WorkspaceKey))
        {
            return WorkspaceDenied(AgentMemoryOperationKind.Delete);
        }

        var now = DateTimeOffset.UtcNow;
        var payload = BuildPayload(
            request.MemoryId,
            AgentMemoryOperationKind.Delete,
            existing.ScopeTarget,
            existing.Content,
            request.Provenance,
            AgentMemoryStatus.Deleted,
            createdAtUtc: existing.CreatedAtUtc,
            updatedAtUtc: now,
            lastValidatedAtUtc: existing.LastValidatedAtUtc,
            policy: AgentMemoryPolicyEvaluation.Clean(),
            supersededByMemoryId: existing.SupersededByMemoryId,
            supersedesMemoryId: existing.SupersedesMemoryId);

        var append = _writer.Append(
            request.WorkspaceKey,
            request.IdempotencyKey,
            payload,
            BuildScopeReferences(existing.ScopeTarget),
            now);

        return MapAppendResult(append, AgentMemoryOperationKind.Delete, request.MemoryId);
    }

    private void EnsureWorkspaceWritable(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        var load = _store.LoadWorkspace(workspaceKey);
        if (load == AgentDurableRecordLoadOutcome.UnsupportedVersion
            || load == AgentDurableRecordLoadOutcome.Quarantined)
        {
            throw new InvalidOperationException($"Workspace partition is not writable: {load}");
        }
    }

    private static AgentMemoryPayload BuildPayload(
        AgentMemoryId memoryId,
        AgentMemoryOperationKind operation,
        AgentMemoryScopeTarget scopeTarget,
        string content,
        AgentMemoryProvenance provenance,
        AgentMemoryStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? lastValidatedAtUtc,
        AgentMemoryPolicyEvaluation policy,
        AgentMemoryId? supersededByMemoryId = null,
        AgentMemoryId? supersedesMemoryId = null) =>
        new()
        {
            MemoryId = memoryId.Value,
            Operation = operation,
            SchemaVersion = AgentMemoryLimits.PayloadSchemaVersion,
            Scope = scopeTarget.Scope,
            SessionId = scopeTarget.SessionId?.Value,
            ActorId = scopeTarget.ActorId?.Value,
            ConversationId = scopeTarget.ConversationId?.Value,
            ProjectId = scopeTarget.ProjectId,
            Content = content,
            AuthorActorId = provenance.AuthorActorId.Value,
            SourceRevision = provenance.SourceRevision,
            SourceKind = provenance.SourceKind,
            SourceDescription = provenance.SourceDescription,
            Status = status,
            SupersededByMemoryId = supersededByMemoryId?.Value,
            SupersedesMemoryId = supersedesMemoryId?.Value,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
            LastValidatedAtUtc = lastValidatedAtUtc,
            ConflictKind = policy.ConflictKind,
            IsPoisoningSuspect = policy.IsPoisoningSuspect,
            IsStaleFact = policy.IsStaleFact,
        };

    private static AgentDurableRecordScopeReferences BuildScopeReferences(AgentMemoryScopeTarget scopeTarget) =>
        new(
            conversationId: scopeTarget.ConversationId?.Value,
            sessionId: scopeTarget.SessionId?.Value,
            runId: null,
            backendId: null);

    private static bool WorkspaceMatches(
        AgentDurableWorkspaceStorageKey left,
        AgentDurableWorkspaceStorageKey right) =>
        string.Equals(left.Value, right.Value, StringComparison.Ordinal);

    private static AgentMemoryOperationResult MapAppendResult(
        AgentDurableRecordAppendResult append,
        AgentMemoryOperationKind operationKind,
        AgentMemoryId memoryId,
        AgentMemoryConflictKind conflictKind = AgentMemoryConflictKind.None)
    {
        if (append.Status == AgentDurableRecordAppendStatus.DuplicateIgnored)
        {
            return Duplicate(operationKind, memoryId);
        }

        if (append.Status != AgentDurableRecordAppendStatus.Appended)
        {
            return Rejected(
                operationKind,
                AgentMemoryOperationStatus.Rejected,
                $"Store rejected append: {append.Status}",
                conflictKind);
        }

        var status = conflictKind == AgentMemoryConflictKind.ContentConflict
            ? AgentMemoryOperationStatus.ConflictDetected
            : AgentMemoryOperationStatus.Accepted;

        return new AgentMemoryOperationResult(
            status,
            operationKind,
            memoryId,
            append.Envelope?.OrderingSequence ?? 0,
            conflictKind: conflictKind);
    }

    private static AgentMemoryOperationResult Duplicate(
        AgentMemoryOperationKind operationKind,
        AgentMemoryId memoryId) =>
        new(
            AgentMemoryOperationStatus.DuplicateIgnored,
            operationKind,
            memoryId);

    private static AgentMemoryOperationResult NotFound(AgentMemoryOperationKind operationKind) =>
        new(AgentMemoryOperationStatus.NotFound, operationKind, reason: "Memory record not found.");

    private static AgentMemoryOperationResult WorkspaceDenied(AgentMemoryOperationKind operationKind) =>
        new(
            AgentMemoryOperationStatus.WorkspaceDenied,
            operationKind,
            reason: "Cross-workspace memory access is denied.");

    private static AgentMemoryOperationResult Rejected(
        AgentMemoryOperationKind operationKind,
        AgentMemoryOperationStatus status,
        string? reason,
        AgentMemoryConflictKind conflictKind = AgentMemoryConflictKind.None) =>
        new(status, operationKind, reason: reason, conflictKind: conflictKind);
}
