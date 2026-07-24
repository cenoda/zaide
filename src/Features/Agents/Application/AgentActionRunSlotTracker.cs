using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Tracks the single non-terminal action slot for one run.
/// </summary>
internal sealed class AgentActionRunSlotTracker
{
    private AgentActionId? _activeActionId;

    public bool TryReserve(AgentActionId actionId)
    {
        if (actionId == default)
        {
            throw new ArgumentException("Action id is required.", nameof(actionId));
        }

        if (_activeActionId is not null)
        {
            return false;
        }

        _activeActionId = actionId;
        return true;
    }

    public void Release(AgentActionId actionId)
    {
        if (actionId == default)
        {
            throw new ArgumentException("Action id is required.", nameof(actionId));
        }

        if (_activeActionId == actionId)
        {
            _activeActionId = null;
        }
    }

    public bool HasActiveAction => _activeActionId is not null;

    public AgentActionId? ActiveActionId => _activeActionId;
}
