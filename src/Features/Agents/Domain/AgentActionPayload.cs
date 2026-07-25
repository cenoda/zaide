using System;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Base type for one concrete Phase 17 action payload.
/// </summary>
internal abstract class AgentActionPayload
{
    public abstract AgentActionKind Kind { get; }

    internal static bool MatchesKind(AgentActionKind kind, AgentActionPayload payload) =>
        kind switch
        {
            AgentActionKind.ReadFile => payload is AgentReadFileActionPayload,
            AgentActionKind.CreateFile => payload is AgentCreateFileActionPayload,
            AgentActionKind.ReplaceFile => payload is AgentReplaceFileActionPayload,
            AgentActionKind.DeleteFile => payload is AgentDeleteFileActionPayload,
            AgentActionKind.ExecuteCommand => payload is AgentExecuteCommandActionPayload,
            _ => false,
        };
}
