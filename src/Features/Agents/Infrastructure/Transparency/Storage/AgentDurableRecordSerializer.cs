using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Infrastructure.Transparency.Storage;

/// <summary>
/// On-disk partition index for one workspace durable-record partition.
/// </summary>
internal sealed class AgentDurableRecordPartitionIndex
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string WorkspaceKey { get; set; } = string.Empty;

    public Dictionary<string, AgentDurableRecordClassState> ClassState { get; set; } = new();

    public List<AgentDurableRecordIndexEntry> Records { get; set; } = new();
}

internal sealed class AgentDurableRecordClassState
{
    public long NextOrderingSequence { get; set; } = 1;

    public HashSet<string> IdempotencyKeys { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class AgentDurableRecordIndexEntry
{
    public string RecordId { get; set; } = string.Empty;

    public AgentDurableRecordClass RecordClass { get; set; }

    public long OrderingSequence { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
}

internal static class AgentDurableRecordSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string SerializeIndex(AgentDurableRecordPartitionIndex index) =>
        JsonSerializer.Serialize(index, Options);

    public static AgentDurableRecordPartitionIndex? DeserializeIndex(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AgentDurableRecordPartitionIndex>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string SerializeEnvelope(AgentDurableRecordEnvelope envelope) =>
        JsonSerializer.Serialize(new AgentDurableRecordEnvelopeDto
        {
            SchemaVersion = envelope.SchemaVersion,
            RecordId = envelope.RecordId.Value,
            RecordClass = envelope.RecordClass,
            WorkspaceKey = envelope.WorkspaceKey.Value,
            OrderingSequence = envelope.OrderingSequence,
            IdempotencyKey = envelope.IdempotencyKey,
            RecordedAtUtc = envelope.RecordedAtUtc,
            ConversationId = envelope.ScopeReferences.ConversationId,
            SessionId = envelope.ScopeReferences.SessionId,
            RunId = envelope.ScopeReferences.RunId,
            BackendId = envelope.ScopeReferences.BackendId,
            PayloadJson = envelope.PayloadJson,
        }, Options);

    public static AgentDurableRecordEnvelope? DeserializeEnvelope(string json)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<AgentDurableRecordEnvelopeDto>(json, Options);
            if (dto is null || string.IsNullOrWhiteSpace(dto.RecordId))
            {
                return null;
            }

            return new AgentDurableRecordEnvelope(
                dto.SchemaVersion,
                AgentDurableRecordId.FromValue(dto.RecordId),
                dto.RecordClass,
                AgentDurableWorkspaceStorageKey.FromValue(dto.WorkspaceKey),
                dto.OrderingSequence,
                dto.IdempotencyKey,
                dto.RecordedAtUtc,
                new AgentDurableRecordScopeReferences(
                    dto.ConversationId,
                    dto.SessionId,
                    dto.RunId,
                    dto.BackendId),
                dto.PayloadJson);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private sealed class AgentDurableRecordEnvelopeDto
    {
        public int SchemaVersion { get; set; }

        public string RecordId { get; set; } = string.Empty;

        public AgentDurableRecordClass RecordClass { get; set; }

        public string WorkspaceKey { get; set; } = string.Empty;

        public long OrderingSequence { get; set; }

        public string IdempotencyKey { get; set; } = string.Empty;

        public DateTimeOffset RecordedAtUtc { get; set; }

        public string? ConversationId { get; set; }

        public string? SessionId { get; set; }

        public string? RunId { get; set; }

        public string? BackendId { get; set; }

        public string PayloadJson { get; set; } = string.Empty;
    }
}
