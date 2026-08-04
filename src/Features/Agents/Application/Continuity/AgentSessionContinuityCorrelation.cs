using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application.Continuity;

internal static class AgentSessionContinuityCorrelation
{
    public static ConversationEntryCorrelationId ToEntryCorrelation(
        AgentSessionId sessionId,
        AgentSessionContinuityReconcileOrigin origin) =>
        ConversationEntryCorrelationId.FromValue(
            $"continuity:{origin}:{sessionId.Value}");
}
