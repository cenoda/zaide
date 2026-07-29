using System.Text.Json;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Application.Memory;

internal static class AgentMemoryLifecycleSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string SerializeRecordSummary(AgentMemoryRecord record) =>
        JsonSerializer.Serialize(
            new
            {
                memoryId = record.MemoryId.Value,
                orderingSequence = record.OrderingSequence,
                scope = record.ScopeTarget.Scope.ToString(),
                status = record.Status.ToString(),
                schemaVersion = record.SchemaVersion,
                contentLength = record.Content.Length,
                provenanceRevision = record.Provenance.SourceRevision,
            },
            Options);
}
