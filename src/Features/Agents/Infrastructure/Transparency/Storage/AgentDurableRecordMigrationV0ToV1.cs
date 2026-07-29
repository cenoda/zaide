using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Zaide.Features.Agents.Infrastructure.Transparency.Storage;

/// <summary>
/// Synthetic v0 → v1 migration used to prove backup-before-migration behavior.
/// Production partitions are created at schema version 1.
/// </summary>
internal sealed class AgentDurableRecordMigrationV0ToV1 : Contracts.Transparency.IAgentDurableRecordMigration
{
    public int FromVersion => 0;

    public int ToVersion => 1;

    public string MigrateIndexJson(string indexJson)
    {
        var node = JsonNode.Parse(indexJson)
            ?? throw new InvalidOperationException("Partition index migration received invalid JSON.");

        node["schemaVersion"] = 1;

        if (node["classState"] is null && node["sequences"] is JsonObject sequences)
        {
            var classState = new JsonObject();
            foreach (var property in sequences)
            {
                classState[property.Key] = new JsonObject
                {
                    ["nextOrderingSequence"] = property.Value?.GetValue<long>() ?? 1,
                    ["idempotencyKeys"] = new JsonArray(),
                };
            }

            node["classState"] = classState;
            node.AsObject().Remove("sequences");
        }

        return node.ToJsonString();
    }
}
