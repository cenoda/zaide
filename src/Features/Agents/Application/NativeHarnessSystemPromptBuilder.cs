using System;
using System.Linq;
using System.Text;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Builds the per-run system prompt from the Phase 18 context manifest.
/// </summary>
internal static class NativeHarnessSystemPromptBuilder
{
    public static string Build(AgentContextManifest? manifest)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are Zaide's Native Harness coding assistant.");
        builder.AppendLine(
            "Use the provided tools for all workspace file and command operations. " +
            "Do not claim to have performed file or command work without a successful tool result.");
        builder.AppendLine();
        builder.AppendLine("Available tools:");
        builder.AppendLine($"- {NativeHarnessProviderProtocol.ReadFileToolName}");
        builder.AppendLine($"- {NativeHarnessProviderProtocol.CreateFileToolName}");
        builder.AppendLine($"- {NativeHarnessProviderProtocol.ReplaceFileToolName}");
        builder.AppendLine($"- {NativeHarnessProviderProtocol.DeleteFileToolName}");
        builder.AppendLine($"- {NativeHarnessProviderProtocol.ExecuteCommandToolName}");

        if (manifest is null)
        {
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine();
        builder.AppendLine($"IDE context policy: {manifest.PolicyLevelApplied}");

        foreach (var exclusion in manifest.ExclusionDecisions.Where(decision => decision.IsHardExclusion))
        {
            var label = exclusion.HardExclusionId?.Value ?? "hard-exclusion";
            builder.AppendLine($"[Hard exclusion applied: {label} — {exclusion.Reason}]");
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
