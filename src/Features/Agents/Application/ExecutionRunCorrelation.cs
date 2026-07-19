using System;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Maps agent execution-run identity into the agent-neutral conversation-entry
/// correlation seam without introducing a Conversations -&gt; Agents dependency.
/// </summary>
internal static class ExecutionRunCorrelation
{
    public static ConversationEntryCorrelationId ToEntryCorrelation(ExecutionRunId runId)
    {
        if (runId == default)
        {
            throw new ArgumentException("Execution run id is required.", nameof(runId));
        }

        return ConversationEntryCorrelationId.FromValue(runId.Value);
    }
}
