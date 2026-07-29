using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application.Memory;

internal static class AgentMemoryProjectionEngine
{
    public static IReadOnlyDictionary<AgentMemoryId, AgentMemoryRecord> ProjectLatest(
        IReadOnlyList<AgentDurableRecordEnvelope> envelopes)
    {
        var revisions = new Dictionary<AgentMemoryId, List<(AgentDurableRecordEnvelope Envelope, AgentMemoryPayload Payload)>>();

        foreach (var envelope in envelopes.OrderBy(e => e.OrderingSequence))
        {
            var payload = AgentMemoryPayloadSerializer.Deserialize(envelope.PayloadJson);
            if (payload is null || string.IsNullOrWhiteSpace(payload.MemoryId))
            {
                continue;
            }

            var memoryId = AgentMemoryId.FromValue(payload.MemoryId);
            if (!revisions.TryGetValue(memoryId, out var list))
            {
                list = new List<(AgentDurableRecordEnvelope, AgentMemoryPayload)>();
                revisions[memoryId] = list;
            }

            list.Add((envelope, payload));
        }

        var projected = new Dictionary<AgentMemoryId, AgentMemoryRecord>();
        foreach (var pair in revisions)
        {
            var latest = pair.Value[^1];
            if (TryProjectRecord(latest.Envelope, latest.Payload, out var record))
            {
                projected[pair.Key] = record;
            }
        }

        return projected;
    }

    public static bool TryProjectRecord(
        AgentDurableRecordEnvelope envelope,
        AgentMemoryPayload payload,
        out AgentMemoryRecord record)
    {
        record = null!;
        try
        {
            var scopeTarget = BuildScopeTarget(payload);
            var provenance = new AgentMemoryProvenance(
                ActorId.FromValue(payload.AuthorActorId),
                payload.SourceRevision,
                payload.SourceKind,
                payload.SourceDescription);

            record = new AgentMemoryRecord(
                AgentMemoryId.FromValue(payload.MemoryId),
                envelope.RecordId,
                envelope.OrderingSequence,
                envelope.WorkspaceKey,
                scopeTarget,
                payload.Content,
                provenance,
                payload.Status,
                payload.SchemaVersion,
                payload.CreatedAtUtc,
                payload.UpdatedAtUtc,
                payload.LastValidatedAtUtc,
                string.IsNullOrWhiteSpace(payload.SupersededByMemoryId)
                    ? null
                    : AgentMemoryId.FromValue(payload.SupersededByMemoryId),
                string.IsNullOrWhiteSpace(payload.SupersedesMemoryId)
                    ? null
                    : AgentMemoryId.FromValue(payload.SupersedesMemoryId),
                payload.ConflictKind,
                payload.IsPoisoningSuspect,
                payload.IsStaleFact,
                envelope.RecordedAtUtc);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static AgentMemoryScopeTarget BuildScopeTarget(AgentMemoryPayload payload) =>
        payload.Scope switch
        {
            AgentMemoryScope.Session => new AgentMemoryScopeTarget(
                AgentMemoryScope.Session,
                sessionId: payload.SessionId),
            AgentMemoryScope.Agent => new AgentMemoryScopeTarget(
                AgentMemoryScope.Agent,
                actorId: ActorId.FromValue(payload.ActorId!)),
            AgentMemoryScope.Conversation => new AgentMemoryScopeTarget(
                AgentMemoryScope.Conversation,
                conversationId: ConversationId.FromValue(payload.ConversationId!)),
            AgentMemoryScope.ProjectShared => new AgentMemoryScopeTarget(
                AgentMemoryScope.ProjectShared,
                projectId: payload.ProjectId),
            _ => throw new InvalidOperationException($"Unsupported memory scope: {payload.Scope}"),
        };
}
