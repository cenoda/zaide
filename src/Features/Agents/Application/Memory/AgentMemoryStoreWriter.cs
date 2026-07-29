using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Application.Memory;

internal static class AgentMemoryPayloadSerializer
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(AgentMemoryPayload payload) =>
        JsonSerializer.Serialize(payload, Options);

    public static AgentMemoryPayload? Deserialize(string json) =>
        JsonSerializer.Deserialize<AgentMemoryPayload>(json, Options);
}

internal sealed class AgentMemoryPayload
{
    public string MemoryId { get; set; } = string.Empty;

    public AgentMemoryOperationKind Operation { get; set; }

    public int SchemaVersion { get; set; }

    public AgentMemoryScope Scope { get; set; }

    public string? SessionId { get; set; }

    public string? ActorId { get; set; }

    public string? ConversationId { get; set; }

    public string? ProjectId { get; set; }

    public string Content { get; set; } = string.Empty;

    public string AuthorActorId { get; set; } = string.Empty;

    public string SourceRevision { get; set; } = string.Empty;

    public AgentMemorySourceKind SourceKind { get; set; }

    public string? SourceDescription { get; set; }

    public AgentMemoryStatus Status { get; set; }

    public string? SupersededByMemoryId { get; set; }

    public string? SupersedesMemoryId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? LastValidatedAtUtc { get; set; }

    public AgentMemoryConflictKind ConflictKind { get; set; }

    public bool IsPoisoningSuspect { get; set; }

    public bool IsStaleFact { get; set; }
}

internal sealed class AgentMemoryStoreWriter
{
    private readonly IAgentDurableRecordStore _store;

    public AgentMemoryStoreWriter(IAgentDurableRecordStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AgentDurableRecordAppendResult Append(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string idempotencyKey,
        AgentMemoryPayload payload,
        AgentDurableRecordScopeReferences scopeReferences,
        DateTimeOffset recordedAtUtc)
    {
        var payloadJson = AgentMemoryPayloadSerializer.Serialize(payload);
        var request = new AgentDurableRecordAppendRequest(
            workspaceKey,
            AgentDurableRecordClass.Memory,
            idempotencyKey,
            payloadJson,
            scopeReferences,
            recordedAtUtc);

        return _store.TryAppend(request);
    }
}
