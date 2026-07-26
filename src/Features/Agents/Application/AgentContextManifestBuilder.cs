using System;
using System.Collections.Generic;
using System.Linq;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Assembles attributed IDE context manifests from policy evaluation and snapshots.
/// </summary>
internal sealed class AgentContextManifestBuilder
{
    private readonly AgentContextPolicyEvaluationService _policyEvaluationService = new();

    public AgentContextManifest Build(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentContextPolicy policy,
        IAgentContextSnapshotSources snapshots,
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

        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(snapshots);

        if (assembledAtUtc == default)
        {
            throw new ArgumentException("Assembly timestamp is required.", nameof(assembledAtUtc));
        }

        if (assembledAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Assembly timestamp must be UTC.", nameof(assembledAtUtc));
        }

        var policyEvaluation = _policyEvaluationService.Evaluate(policy);
        var exclusionDecisions = new List<AgentContextExclusionDecision>(policyEvaluation.PolicyExclusionDecisions);
        var candidates = new List<AgentContextManifestCandidate>();

        if (policyEvaluation.EffectiveLevel == AgentContextPolicyLevel.Off)
        {
            return CreateManifest(
                sessionId,
                runId,
                conversationId,
                policyEvaluation.EffectiveLevel,
                Array.Empty<AgentContextItem>(),
                requestedBudget: 0,
                actualTokenCount: 0,
                Array.Empty<AgentContextTruncationDecision>(),
                exclusionDecisions,
                assembledAtUtc);
        }

        foreach (var sourceId in policyEvaluation.IncludedSources
                     .OrderBy(source => AgentContextSourcePriority.GetPriority(source))
                     .ThenBy(source => source.Value, StringComparer.Ordinal))
        {
            var rawContent = AgentContextContentComposer.TryCompose(sourceId, snapshots);
            if (rawContent is null)
            {
                continue;
            }

            switch (rawContent.Status)
            {
                case AgentContextRawContentStatus.HardExcluded:
                    exclusionDecisions.Add(
                        CreateHardExclusionDecision(
                            rawContent.HardExclusionId!.Value,
                            rawContent.ScopeDescriptor));
                    continue;

                case AgentContextRawContentStatus.Unavailable:
                    exclusionDecisions.Add(
                        new AgentContextExclusionDecision(
                            sourceId: sourceId,
                            hardExclusionId: null,
                            reason: "Source capability is unavailable.",
                            isHardExclusion: false));
                    continue;

                case AgentContextRawContentStatus.NoAttachableContent:
                    exclusionDecisions.Add(
                        new AgentContextExclusionDecision(
                            sourceId: sourceId,
                            hardExclusionId: null,
                            reason: "Source had no attachable content.",
                            isHardExclusion: false));
                    continue;
            }

            var redactionOutcome = AgentContextRedactionProcessor.Apply(rawContent.Content!);
            if (redactionOutcome.DidProcessingFail)
            {
                exclusionDecisions.Add(
                    CreateHardExclusionDecision(
                        AgentContextHardExclusionId.RedactionPatternMatch,
                        rawContent.ScopeDescriptor));
                continue;
            }

            var provenance = new AgentContextProvenance(
                rawContent.SourceServiceIdentity,
                rawContent.SnapshotGeneration,
                wasLiveSnapshot: true,
                redactionApplied: redactionOutcome.State != AgentContextRedactionState.None,
                redactionOutcome.Reason);

            var item = new AgentContextItem(
                sourceId,
                redactionOutcome.Content,
                rawContent.ScopeDescriptor,
                fingerprint: $"gen:{rawContent.SnapshotGeneration}",
                redactionOutcome.State,
                AgentContextTokenEstimator.Estimate(redactionOutcome.Content),
                provenance,
                redactionOutcome.Reason);

            candidates.Add(
                new AgentContextManifestCandidate(
                    item,
                    AgentContextSourcePriority.GetPriority(sourceId)));
        }

        var requestedBudget = AgentContextSourcePolicyMatrix.GetDefaultTokenBudget(
            policyEvaluation.EffectiveLevel);
        var budgetResult = AgentContextBudgetEnforcer.Apply(candidates, requestedBudget);

        return CreateManifest(
            sessionId,
            runId,
            conversationId,
            policyEvaluation.EffectiveLevel,
            budgetResult.Items,
            requestedBudget,
            budgetResult.ActualTokenCount,
            budgetResult.TruncationDecisions,
            exclusionDecisions,
            assembledAtUtc);
    }

    private static AgentContextExclusionDecision CreateHardExclusionDecision(
        AgentContextHardExclusionId hardExclusionId,
        string scopeDescriptor) =>
        new(
            sourceId: null,
            hardExclusionId: hardExclusionId,
            reason: $"{AgentContextHardExclusionRegistry.Describe(hardExclusionId)} ({scopeDescriptor})",
            isHardExclusion: true);

    private static AgentContextManifest CreateManifest(
        AgentSessionId sessionId,
        ExecutionRunId runId,
        ConversationId conversationId,
        AgentContextPolicyLevel policyLevel,
        IReadOnlyList<AgentContextItem> items,
        int requestedBudget,
        int actualTokenCount,
        IReadOnlyList<AgentContextTruncationDecision> truncationDecisions,
        IReadOnlyList<AgentContextExclusionDecision> exclusionDecisions,
        DateTimeOffset assembledAtUtc)
    {
        var tokenBudget = new AgentContextTokenBudget(
            policyLevel,
            requestedBudget,
            actualTokenCount);

        return new AgentContextManifest(
            sessionId,
            runId,
            conversationId,
            policyLevel,
            items,
            tokenBudget,
            truncationDecisions,
            exclusionDecisions,
            assembledAtUtc);
    }
}
