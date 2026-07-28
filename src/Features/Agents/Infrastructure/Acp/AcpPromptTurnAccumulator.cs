using System;
using System.Collections.Generic;
using System.Text;

namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Bounded accumulation of session/update notifications during one prompt turn.
/// </summary>
internal sealed class AcpPromptTurnAccumulator
{
    private readonly List<AcpSessionUpdate> _updates = new();
    private readonly StringBuilder _agentMessage = new();
    private int _updateCount;

    public IReadOnlyList<AcpSessionUpdate> Updates => _updates;

    public string AgentMessageText => _agentMessage.ToString();

    public void Add(AcpSessionUpdateNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (++_updateCount > AcpProtocolLimits.MaxSessionUpdatesPerPrompt)
        {
            throw new AcpProtocolException("ACP session update count exceeded the configured limit.");
        }

        _updates.Add(notification.Update);

        switch (notification.Update.Kind)
        {
            case AcpSessionUpdateKind.AgentMessageChunk:
                if (notification.Update.ContentChunk?.Content.Text is { } text)
                {
                    _agentMessage.Append(text);
                }

                break;
            case AcpSessionUpdateKind.AgentThoughtChunk:
                // Phase 20: bounded validation only; never project as assistant answer.
                break;
        }
    }
}
