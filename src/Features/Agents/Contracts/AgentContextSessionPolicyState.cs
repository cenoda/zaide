using System;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Read-only observation of resolved IDE context policy for one conversation session.
/// </summary>
public sealed class AgentContextSessionPolicyState
{
    public AgentContextSessionPolicyState(
        ConversationId conversationId,
        AgentSessionContextPolicyLevel applicationDefaultLevel,
        AgentSessionContextPolicyLevel effectiveLevel,
        bool isOverrideActive,
        string statusCaption)
    {
        if (conversationId == default)
        {
            throw new ArgumentException("Conversation id is required.", nameof(conversationId));
        }

        if (!Enum.IsDefined(applicationDefaultLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationDefaultLevel),
                applicationDefaultLevel,
                "Application default level is invalid.");
        }

        if (!Enum.IsDefined(effectiveLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectiveLevel),
                effectiveLevel,
                "Effective level is invalid.");
        }

        if (string.IsNullOrWhiteSpace(statusCaption))
        {
            throw new ArgumentException("Status caption is required.", nameof(statusCaption));
        }

        ConversationId = conversationId;
        ApplicationDefaultLevel = applicationDefaultLevel;
        EffectiveLevel = effectiveLevel;
        IsOverrideActive = isOverrideActive;
        StatusCaption = statusCaption;
    }

    public ConversationId ConversationId { get; }

    public AgentSessionContextPolicyLevel ApplicationDefaultLevel { get; }

    public AgentSessionContextPolicyLevel EffectiveLevel { get; }

    public bool IsOverrideActive { get; }

    public string StatusCaption { get; }

    public static AgentContextSessionPolicyState CreateApplicationDefault(
        ConversationId conversationId,
        AgentSessionContextPolicyLevel applicationDefaultLevel) =>
        new(
            conversationId,
            applicationDefaultLevel,
            applicationDefaultLevel,
            isOverrideActive: false,
            statusCaption: FormatApplicationDefaultCaption(applicationDefaultLevel));

    internal static string FormatApplicationDefaultCaption(AgentSessionContextPolicyLevel level) =>
        $"Application default ({FormatLevelName(level)})";

    internal static string FormatOverrideCaption(AgentSessionContextPolicyLevel level) =>
        $"{FormatLevelName(level)} (session override)";

    internal static string FormatLevelName(AgentSessionContextPolicyLevel level) =>
        level switch
        {
            AgentSessionContextPolicyLevel.Off => "Off",
            AgentSessionContextPolicyLevel.Minimal => "Minimal",
            AgentSessionContextPolicyLevel.Standard => "Standard",
            AgentSessionContextPolicyLevel.Detailed => "Detailed",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Policy level is invalid."),
        };
}
