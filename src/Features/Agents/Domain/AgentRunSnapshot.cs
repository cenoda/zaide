using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Read-only observation of one admitted run at a point in time.
/// </summary>
internal sealed class AgentRunSnapshot
{
    public AgentRunSnapshot(
        ExecutionRunId runId,
        AgentSessionId sessionId,
        ConversationId conversationId,
        ConversationEntryCorrelationId correlationId,
        AgentRunStatus status)
    {
        if (runId == default)
        {
            throw new ArgumentException("Run id is required.", nameof(runId));
        }

        if (sessionId == default)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (correlationId == default)
        {
            throw new ArgumentException("Correlation id is required.", nameof(correlationId));
        }

        if (!string.Equals(runId.Value, correlationId.Value, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Correlation id must match the execution run id value.",
                nameof(correlationId));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Run status is invalid.");
        }

        RunId = runId;
        SessionId = sessionId;
        ConversationId = conversationId;
        CorrelationId = correlationId;
        Status = status;
    }

    public ExecutionRunId RunId { get; }

    public AgentSessionId SessionId { get; }

    public ConversationId ConversationId { get; }

    public ConversationEntryCorrelationId CorrelationId { get; }

    public AgentRunStatus Status { get; }
}
