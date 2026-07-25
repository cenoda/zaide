using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Broker whose action capability remains unavailable for legacy-compatible backends.
/// </summary>
internal sealed class UnavailableAgentActionBroker : IAgentActionBroker
{
    public ValueTask<AgentActionResult> RequestAsync(
        AgentActionPayload payload,
        string? correlationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        var actionId = AgentActionId.New();
        var attemptId = AgentActionAttemptId.New();
        var result = new AgentActionResult(
            actionId,
            attemptId,
            AgentActionResultKind.Denied,
            AgentActionFailureKind.BrokerUnavailable,
            "Action capability is unavailable for this backend.");

        return ValueTask.FromResult(result);
    }
}
