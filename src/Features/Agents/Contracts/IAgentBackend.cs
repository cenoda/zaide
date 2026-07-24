using System.Collections.Generic;
using System.Threading;

namespace Zaide.Features.Agents.Contracts;

using Zaide.Features.Agents.Domain;

/// <summary>
/// Backend-neutral execution boundary for one admitted run attempt.
/// </summary>
internal interface IAgentBackend
{
    AgentBackendId BackendId { get; }

    string BackendVersion { get; }

    AgentCapabilitySnapshot CapabilitySnapshot { get; }

    IAsyncEnumerable<AgentBackendEvent> ExecuteAsync(
        AgentBackendExecutionContext context,
        CancellationToken cancellationToken);
}
