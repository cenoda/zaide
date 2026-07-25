using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Run-scoped action broker created and owned by Zaide for one admitted run.
/// </summary>
internal interface IAgentActionBroker
{
    ValueTask<AgentActionResult> RequestAsync(
        AgentActionPayload payload,
        string? correlationKey,
        CancellationToken cancellationToken);
}
