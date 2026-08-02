using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Schema-v1 serialization and validation for the durable binding document.
/// </summary>
internal static class AgentActorBackendBindingSerializer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
    };

    public static string Serialize(AgentActorBackendBindingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateDocument(document);
        return JsonSerializer.Serialize(document, WriteOptions);
    }

    public static bool TryDeserialize(
        string json,
        out AgentActorBackendBindingDocument? document,
        out bool unsupportedSchema,
        out string? error)
    {
        document = null;
        unsupportedSchema = false;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Binding document is empty.";
            return false;
        }

        try
        {
            using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
            });

            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Binding document root must be an object.";
                return false;
            }

            if (!root.TryGetProperty("schemaVersion", out var schemaProperty)
                || schemaProperty.ValueKind != JsonValueKind.Number
                || !schemaProperty.TryGetInt32(out var schemaVersion))
            {
                error = "Binding document schemaVersion is missing or invalid.";
                return false;
            }

            if (schemaVersion != AgentActorBackendBindingDocument.CurrentSchemaVersion)
            {
                unsupportedSchema = true;
                error =
                    $"Unsupported binding document schema version {schemaVersion}; " +
                    $"expected {AgentActorBackendBindingDocument.CurrentSchemaVersion}.";
                return false;
            }

            var candidate = JsonSerializer.Deserialize<AgentActorBackendBindingDocument>(json, ReadOptions);
            if (candidate is null)
            {
                error = "Binding document deserialized to null.";
                return false;
            }

            ValidateDocument(candidate);
            document = candidate;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static AgentActorBackendBindingDocument FromBindings(
        IReadOnlyDictionary<ActorId, AgentActorBackendBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var document = new AgentActorBackendBindingDocument
        {
            SchemaVersion = AgentActorBackendBindingDocument.CurrentSchemaVersion,
            Bindings = bindings.Values
                .OrderBy(binding => binding.ActorId.Value, StringComparer.Ordinal)
                .Select(ToRecord)
                .ToList(),
        };

        ValidateDocument(document);
        return document;
    }

    public static IReadOnlyDictionary<ActorId, AgentActorBackendBinding> ToBindings(
        AgentActorBackendBindingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateDocument(document);

        var map = new Dictionary<ActorId, AgentActorBackendBinding>();
        foreach (var record in document.Bindings)
        {
            var binding = ToBinding(record);
            map[binding.ActorId] = binding;
        }

        return map;
    }

    public static void ValidateDocument(AgentActorBackendBindingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.SchemaVersion != AgentActorBackendBindingDocument.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported binding document schema version {document.SchemaVersion}.");
        }

        if (document.Bindings is null)
        {
            throw new InvalidOperationException("Binding document bindings collection is required.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in document.Bindings)
        {
            if (record is null)
            {
                throw new InvalidOperationException("Binding record cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(record.ActorId))
            {
                throw new InvalidOperationException("Binding actor id is required.");
            }

            if (!seen.Add(record.ActorId))
            {
                throw new InvalidOperationException(
                    $"Duplicate durable binding for actor '{record.ActorId}'.");
            }

            if (record.Revision < 1)
            {
                throw new InvalidOperationException(
                    $"Binding revision must be >= 1 for actor '{record.ActorId}'.");
            }

            // Materialize domain validation (backend rules, ACP runtime, etc.).
            _ = ToBinding(record);
        }
    }

    public static AgentActorBackendBindingRecord ToRecord(AgentActorBackendBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        return new AgentActorBackendBindingRecord
        {
            ActorId = binding.ActorId.Value,
            BackendId = binding.BackendId.Value,
            Revision = binding.Revision,
            ExpectedAgentName = binding.ExpectedAgentName,
            ExpectedAgentVersion = binding.ExpectedAgentVersion,
            AcpRuntime = binding.AcpRuntime is null
                ? null
                : new AgentActorBackendBindingAcpRuntimeRecord
                {
                    ExecutablePath = binding.AcpRuntime.ExecutablePath,
                    Arguments = binding.AcpRuntime.Arguments.ToList(),
                    RegistryId = binding.AcpRuntime.RegistryId,
                    DistributionProvenance = binding.AcpRuntime.DistributionProvenance,
                },
        };
    }

    public static AgentActorBackendBinding ToBinding(AgentActorBackendBindingRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.ActorId))
        {
            throw new ArgumentException("Actor id is required.", nameof(record));
        }

        if (string.IsNullOrWhiteSpace(record.BackendId))
        {
            throw new ArgumentException("Backend id is required.", nameof(record));
        }

        if (record.Revision < 1)
        {
            throw new ArgumentException("Revision must be >= 1.", nameof(record));
        }

        var actorId = ActorId.FromValue(record.ActorId);
        var backendId = AgentBackendId.FromValue(record.BackendId);
        AcpRuntimeIdentity? runtime = null;

        if (record.AcpRuntime is not null)
        {
            if (string.IsNullOrWhiteSpace(record.AcpRuntime.ExecutablePath)
                || !Path.IsPathRooted(record.AcpRuntime.ExecutablePath))
            {
                throw new ArgumentException(
                    "ACP executable path must be absolute.",
                    nameof(record));
            }

            runtime = new AcpRuntimeIdentity(
                record.AcpRuntime.ExecutablePath,
                record.AcpRuntime.Arguments ?? new List<string>(),
                record.AcpRuntime.RegistryId,
                record.AcpRuntime.DistributionProvenance);
        }

        // Durable load rehydrates identity/config only. Auth is always reset to
        // NotRequired (Native) or Disconnected (ACP) and never restored from disk.
        var authenticationState = backendId == AgentBackendIds.Acp
            ? AgentAuthenticationConnectionState.Disconnected
            : AgentAuthenticationConnectionState.NotRequired;

        return new AgentActorBackendBinding(
            actorId,
            backendId,
            runtime,
            record.ExpectedAgentName,
            record.ExpectedAgentVersion,
            selectedAuthMethodId: null,
            authenticationState,
            record.Revision);
    }
}
