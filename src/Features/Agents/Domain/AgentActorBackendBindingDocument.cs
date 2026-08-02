using System.Collections.Generic;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Schema-v1 durable binding document root. Auth, capabilities, process/session
/// IDs, and secrets are never part of this document.
/// </summary>
internal sealed class AgentActorBackendBindingDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<AgentActorBackendBindingRecord> Bindings { get; set; } = new();
}

/// <summary>
/// One durable per-ActorId binding record.
/// </summary>
internal sealed class AgentActorBackendBindingRecord
{
    public string ActorId { get; set; } = string.Empty;

    public string BackendId { get; set; } = string.Empty;

    public long Revision { get; set; }

    public AgentActorBackendBindingAcpRuntimeRecord? AcpRuntime { get; set; }

    public string? ExpectedAgentName { get; set; }

    public string? ExpectedAgentVersion { get; set; }
}

/// <summary>
/// Durable ACP runtime identity fields only (non-secret launch configuration).
/// </summary>
internal sealed class AgentActorBackendBindingAcpRuntimeRecord
{
    public string ExecutablePath { get; set; } = string.Empty;

    public List<string> Arguments { get; set; } = new();

    public string? RegistryId { get; set; }

    public string? DistributionProvenance { get; set; }
}
