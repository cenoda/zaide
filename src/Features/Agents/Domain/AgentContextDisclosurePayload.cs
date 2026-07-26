using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Metadata-only payload for context disclosure audit events. Emission belongs to
/// Phase 18 M4. This payload must never carry raw context item content.
/// </summary>
internal sealed class AgentContextDisclosurePayload : AgentEventPayload
{
    public AgentContextDisclosurePayload(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentContextPolicyLevel policyLevelApplied,
        IEnumerable<AgentContextSourceId> disclosedSourceIds,
        int itemCount,
        int estimatedTokenCount,
        AgentContextDisclosureRedactionSummary redactionSummary,
        AgentContextDisclosureBoundarySummary boundarySummary)
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

        ArgumentNullException.ThrowIfNull(disclosedSourceIds);
        ArgumentNullException.ThrowIfNull(redactionSummary);
        ArgumentNullException.ThrowIfNull(boundarySummary);

        if (itemCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(itemCount),
                itemCount,
                "Item count cannot be negative.");
        }

        if (estimatedTokenCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(estimatedTokenCount),
                estimatedTokenCount,
                "Estimated token count cannot be negative.");
        }

        var normalizedSourceIds = disclosedSourceIds
            .Where(sourceId => sourceId != default)
            .Distinct()
            .OrderBy(sourceId => sourceId.Value, StringComparer.Ordinal)
            .ToArray();

        if (normalizedSourceIds.Length > itemCount)
        {
            throw new ArgumentException(
                "Disclosed source identifiers cannot exceed item count.",
                nameof(disclosedSourceIds));
        }

        SessionId = sessionId;
        RunId = runId;
        ConversationId = conversationId;
        PolicyLevelApplied = policyLevelApplied;
        DisclosedSourceIds = Array.AsReadOnly(normalizedSourceIds);
        ItemCount = itemCount;
        EstimatedTokenCount = estimatedTokenCount;
        RedactionSummary = redactionSummary;
        BoundarySummary = boundarySummary;
    }

    public AgentSessionId SessionId { get; }

    public ExecutionRunId RunId { get; }

    public ConversationId ConversationId { get; }

    public AgentContextPolicyLevel PolicyLevelApplied { get; }

    public IReadOnlyList<AgentContextSourceId> DisclosedSourceIds { get; }

    public int ItemCount { get; }

    public int EstimatedTokenCount { get; }

    public AgentContextDisclosureRedactionSummary RedactionSummary { get; }

    public AgentContextDisclosureBoundarySummary BoundarySummary { get; }
}
