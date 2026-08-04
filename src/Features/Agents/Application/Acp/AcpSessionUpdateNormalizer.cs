using System;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Maps ACP session/update notifications into Zaide backend activity payloads.
/// </summary>
internal static class AcpSessionUpdateNormalizer
{
    public static bool TryNormalizeActivity(
        AcpSessionUpdate update,
        out AgentBackendActivityReportedPayload? payload)
    {
        ArgumentNullException.ThrowIfNull(update);

        switch (update.Kind)
        {
            case AcpSessionUpdateKind.AgentMessageChunk:
            case AcpSessionUpdateKind.AgentThoughtChunk:
            case AcpSessionUpdateKind.UserMessageChunk:
                payload = null;
                return false;

            case AcpSessionUpdateKind.ToolCall:
                payload = new AgentBackendActivityReportedPayload(
                    AcpBackendActivityKind.ToolCall,
                    BuildToolCallSummary(update.ToolCall),
                    update.ToolCall?.ToolCallId);
                return true;

            case AcpSessionUpdateKind.ToolCallUpdate:
                payload = new AgentBackendActivityReportedPayload(
                    AcpBackendActivityKind.ToolCallUpdate,
                    BuildToolCallUpdateSummary(update.ToolCallUpdate),
                    update.ToolCallUpdate?.ToolCallId);
                return true;

            case AcpSessionUpdateKind.Plan:
                payload = new AgentBackendActivityReportedPayload(
                    AcpBackendActivityKind.Plan,
                    "ACP plan update reported by backend.");
                return true;

            case AcpSessionUpdateKind.UsageUpdate:
                payload = new AgentBackendActivityReportedPayload(
                    AcpBackendActivityKind.UsageUpdate,
                    "ACP usage update reported by backend.",
                    usageUpdateJson: update.Raw?.GetRawText());
                return true;

            case AcpSessionUpdateKind.AvailableCommandsUpdate:
            case AcpSessionUpdateKind.CurrentModeUpdate:
            case AcpSessionUpdateKind.ConfigOptionUpdate:
            case AcpSessionUpdateKind.SessionInfoUpdate:
                payload = new AgentBackendActivityReportedPayload(
                    AcpBackendActivityKind.SessionControlUpdate,
                    $"ACP session control update: {update.Kind}.");
                return true;

            case AcpSessionUpdateKind.Unknown:
                payload = new AgentBackendActivityReportedPayload(
                    AcpBackendActivityKind.UnknownUpdate,
                    "ACP unknown session update preserved for diagnostics.");
                return true;

            default:
                payload = null;
                return false;
        }
    }

    private static string BuildToolCallSummary(AcpToolCallWire? toolCall)
    {
        if (toolCall is null || string.IsNullOrWhiteSpace(toolCall.Title))
        {
            return "ACP tool call reported by backend.";
        }

        return $"ACP tool call reported by backend: {toolCall.Title}.";
    }

    private static string BuildToolCallUpdateSummary(AcpToolCallUpdateWire? toolCallUpdate)
    {
        if (toolCallUpdate is null)
        {
            return "ACP tool call update reported by backend.";
        }

        var status = string.IsNullOrWhiteSpace(toolCallUpdate.Status)
            ? "unknown"
            : toolCallUpdate.Status;
        return $"ACP tool call update reported by backend ({status}).";
    }
}
