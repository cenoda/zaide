using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Assembled IDE context output for one admitted run.
/// </summary>
internal sealed class AgentContextManifest
{
    public AgentContextManifest(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentContextPolicyLevel policyLevelApplied,
        IEnumerable<AgentContextItem> items,
        AgentContextTokenBudget tokenBudget,
        IEnumerable<AgentContextTruncationDecision> truncationDecisions,
        IEnumerable<AgentContextExclusionDecision> exclusionDecisions,
        DateTimeOffset assembledAtUtc)
    {
        if (sessionId == default)
        {
            throw new ArgumentException("Session id is required.", nameof(sessionId));
        }

        if (runId == default)
        {
            throw new ArgumentException("Run id is required.", nameof(runId));
        }

        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (!Enum.IsDefined(policyLevelApplied))
        {
            throw new ArgumentOutOfRangeException(
                nameof(policyLevelApplied),
                policyLevelApplied,
                "Policy level is invalid.");
        }

        if (assembledAtUtc == default)
        {
            throw new ArgumentException("Assembly timestamp is required.", nameof(assembledAtUtc));
        }

        if (assembledAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Assembly timestamp must be UTC.");
        }

        if (items == null)
        {
            throw new ArgumentException("Items collection cannot be null.");
        }
        if (truncationDecisions == null)
        {
            throw new ArgumentException("Truncation decisions cannot be null.");
        }
        if (exclusionDecisions == null)
        {
            throw new ArgumentException("Exclusion decisions cannot be null.");
        }
        if (tokenBudget == null)
        {
            throw new ArgumentException("Token budget cannot be null.");
        }

        var normalizedItems = items.ToArray();
        if (normalizedItems.Any(item => item is null))
        {
            throw new ArgumentException(
                "Context items cannot contain null entries.",
                nameof(items));
        }

        var normalizedTruncations = truncationDecisions.ToArray();
        if (normalizedTruncations.Any(decision => decision is null))
        {
            throw new ArgumentException(
                "Truncation decisions cannot contain null entries.",
                nameof(truncationDecisions));
        }

        var normalizedExclusions = exclusionDecisions.ToArray();
        if (normalizedExclusions.Any(decision => decision is null))
        {
            throw new ArgumentException(
                "Exclusion decisions cannot contain null entries.",
                nameof(exclusionDecisions));
        }

        SessionId = sessionId;
        RunId = runId;
        ConversationId = conversationId;
        PolicyLevelApplied = policyLevelApplied;
        Items = Array.AsReadOnly(normalizedItems);
        TotalEstimatedTokenCount = checked(normalizedItems.Sum(item => item.EstimatedTokenCount));
        TokenBudget = tokenBudget;
        TruncationDecisions = Array.AsReadOnly(normalizedTruncations);
        ExclusionDecisions = Array.AsReadOnly(normalizedExclusions);
        AssembledAtUtc = assembledAtUtc;
    }

    public AgentSessionId SessionId { get; }

    public ExecutionRunId RunId { get; }

    public ConversationId ConversationId { get; }

    public AgentContextPolicyLevel PolicyLevelApplied { get; }

    public IReadOnlyList<AgentContextItem> Items { get; }

    public int TotalEstimatedTokenCount { get; }

    public AgentContextTokenBudget TokenBudget { get; }

    public IReadOnlyList<AgentContextTruncationDecision> TruncationDecisions { get; }

    public IReadOnlyList<AgentContextExclusionDecision> ExclusionDecisions { get; }

    public DateTimeOffset AssembledAtUtc { get; }
}
