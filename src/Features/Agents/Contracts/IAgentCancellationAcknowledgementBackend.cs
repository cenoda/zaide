using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Optional sibling-backend capability: re-issue a bounded cancellation
/// acknowledgement for a live session whose prior cancel-ack was uncertain.
/// Independent of a completed run observer. Native Harness does not implement
/// this; ACP does when a pending cancel target is retained.
/// </summary>
internal interface IAgentCancellationAcknowledgementBackend : IAgentBackend
{
    /// <summary>
    /// Attempts a fresh, independently bounded cancellation acknowledgement for
    /// <paramref name="sessionId"/>. Must not claim provider deletion or use a
    /// previously cancelled run token.
    /// </summary>
    Task<AgentCancellationAcknowledgementResult> AcknowledgeCancellationAsync(
        AgentSessionId sessionId,
        CancellationToken cancellationToken = default);
}
