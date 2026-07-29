using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Memory;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Application.Memory;

internal sealed class AgentMemoryInfluenceRecorder : IAgentMemoryInfluenceRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IAgentDurableRecordStore _store;

    public AgentMemoryInfluenceRecorder(IAgentDurableRecordStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void RecordInfluence(
        AgentDurableWorkspaceStorageKey workspaceKey,
        ExecutionRunId runId,
        AgentSessionId sessionId,
        AgentMemoryInfluenceState state,
        IReadOnlyList<AgentMemoryInfluenceRevision> revisions,
        string? unavailableReason = null)
    {
        if (runId == default)
        {
            throw new ArgumentException("Run id is required.", nameof(runId));
        }

        if (sessionId == default)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(revisions);

        var payload = new AgentMemoryInfluencePayload
        {
            PayloadKind = AgentMemoryInfluencePayload.Kind,
            RunId = runId.Value,
            SessionId = sessionId.Value,
            State = state,
            UnavailableReason = unavailableReason,
            Revisions = revisions
                .Select(revision => new AgentMemoryInfluenceRevisionPayload
                {
                    MemoryId = revision.MemoryId.Value,
                    OrderingSequence = revision.OrderingSequence,
                    SchemaVersion = revision.SchemaVersion,
                    IsStaleFact = revision.IsStaleFact,
                })
                .ToArray(),
        };

        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);
        var idempotencyKey = $"influence:{runId.Value}";
        var request = new AgentDurableRecordAppendRequest(
            workspaceKey,
            AgentDurableRecordClass.Memory,
            idempotencyKey,
            payloadJson,
            new AgentDurableRecordScopeReferences(
                conversationId: null,
                sessionId: sessionId.Value,
                runId: runId.Value,
                backendId: null),
            DateTimeOffset.UtcNow);

        _store.TryAppend(request);
    }

    private sealed class AgentMemoryInfluencePayload
    {
        public const string Kind = "memory-influence";

        public string PayloadKind { get; set; } = Kind;

        public string RunId { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        public AgentMemoryInfluenceState State { get; set; }

        public string? UnavailableReason { get; set; }

        public AgentMemoryInfluenceRevisionPayload[] Revisions { get; set; } = Array.Empty<AgentMemoryInfluenceRevisionPayload>();
    }

    private sealed class AgentMemoryInfluenceRevisionPayload
    {
        public string MemoryId { get; set; } = string.Empty;

        public long OrderingSequence { get; set; }

        public int SchemaVersion { get; set; }

        public bool IsStaleFact { get; set; }
    }
}
