using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Application.Memory;

internal sealed class AgentMemoryRetriever : IAgentMemoryRetrievalService
{
    private readonly AgentMemoryInspector _inspector;
    private readonly IAgentDurableRecordStore _store;

    public AgentMemoryRetriever(AgentMemoryInspector inspector, IAgentDurableRecordStore store)
    {
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AgentMemoryRetrievalResult Retrieve(AgentMemoryRetrievalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var load = _store.LoadWorkspace(request.WorkspaceKey);
        if (load == AgentDurableRecordLoadOutcome.UnsupportedVersion
            || load == AgentDurableRecordLoadOutcome.Quarantined)
        {
            return AgentMemoryRetrievalResult.Unavailable(
                $"Memory partition unavailable: {load}");
        }

        var all = _inspector.ReplayAll(request.WorkspaceKey, includeDeleted: false);
        var eligible = all
            .Where(record => IsEligible(record, request.Context))
            .OrderBy(record => GetScopePriority(record.ScopeTarget.Scope))
            .ThenBy(record => record.IsStaleFact)
            .ThenByDescending(record => record.UpdatedAtUtc)
            .ThenByDescending(record => record.OrderingSequence)
            .ThenBy(record => record.MemoryId.Value, StringComparer.Ordinal)
            .ToArray();

        return new AgentMemoryRetrievalResult(eligible, isUnavailable: false);
    }

    private static bool IsEligible(AgentMemoryRecord record, AgentMemoryRetrievalContext context)
    {
        if (!record.IsRetrievable)
        {
            return false;
        }

        if (!WorkspaceScopeMatches(record, context))
        {
            return false;
        }

        return true;
    }

    private static bool WorkspaceScopeMatches(AgentMemoryRecord record, AgentMemoryRetrievalContext context)
    {
        return record.ScopeTarget.Scope switch
        {
            AgentMemoryScope.Session =>
                record.ScopeTarget.SessionId is not null
                && record.ScopeTarget.SessionId == context.SessionId,
            AgentMemoryScope.Conversation =>
                record.ScopeTarget.ConversationId is not null
                && record.ScopeTarget.ConversationId == context.ConversationId,
            AgentMemoryScope.Agent =>
                record.ScopeTarget.ActorId is not null
                && record.ScopeTarget.ActorId == context.TargetActorId,
            AgentMemoryScope.ProjectShared =>
                !string.IsNullOrWhiteSpace(record.ScopeTarget.ProjectId)
                && !string.IsNullOrWhiteSpace(context.ProjectId)
                && string.Equals(
                    record.ScopeTarget.ProjectId,
                    context.ProjectId,
                    StringComparison.Ordinal),
            _ => false,
        };
    }

    private static int GetScopePriority(AgentMemoryScope scope) =>
        scope switch
        {
            AgentMemoryScope.Session => 0,
            AgentMemoryScope.Conversation => 1,
            AgentMemoryScope.Agent => 2,
            AgentMemoryScope.ProjectShared => 3,
            _ => int.MaxValue,
        };
}
