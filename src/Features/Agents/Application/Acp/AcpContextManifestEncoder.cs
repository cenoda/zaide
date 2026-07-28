using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Encodes Phase 18 context manifests into ACP prompt content blocks.
/// </summary>
internal static class AcpContextManifestEncoder
{
    public static IReadOnlyList<AcpContentBlock> BuildPrompt(
        string userMessage,
        AgentContextManifest? manifest,
        bool agentSupportsEmbeddedContext)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            throw new ArgumentException("User message is required.", nameof(userMessage));
        }

        var blocks = new List<AcpContentBlock>
        {
            AcpContentBlock.FromText(userMessage),
        };

        if (manifest is null)
        {
            return blocks;
        }

        var contextText = BuildContextText(manifest);
        if (contextText.Length == 0)
        {
            return blocks;
        }

        blocks.Add(AcpContentBlock.FromText(contextText));
        return blocks;
    }

    internal static string BuildContextText(AgentContextManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var builder = new StringBuilder();
        builder.AppendLine($"IDE context policy: {manifest.PolicyLevelApplied}");

        foreach (var exclusion in manifest.ExclusionDecisions.Where(decision => decision.IsHardExclusion))
        {
            var label = exclusion.HardExclusionId?.Value ?? "hard-exclusion";
            builder.AppendLine($"[Hard exclusion applied: {label} — {exclusion.Reason}]");
        }

        foreach (var exclusion in manifest.ExclusionDecisions.Where(decision => !decision.IsHardExclusion))
        {
            if (exclusion.SourceId is { } sourceId)
            {
                builder.AppendLine(
                    $"[Excluded source: {sourceId.Value} — {exclusion.Reason}]");
            }
        }

        foreach (var item in manifest.Items)
        {
            if (item.RedactionState == AgentContextRedactionState.ProcessingFailed)
            {
                continue;
            }

            if (string.IsNullOrEmpty(item.Content))
            {
                continue;
            }

            builder.AppendLine();
            builder.AppendLine($"## {item.SourceId.Value}");
            builder.AppendLine($"Scope: {item.ScopeDescriptor}");
            if (item.RedactionState is AgentContextRedactionState.Partial or AgentContextRedactionState.Full)
            {
                builder.AppendLine($"Redaction: {item.RedactionState}");
            }

            builder.AppendLine(item.Content);
        }

        return builder.ToString().TrimEnd();
    }
}
