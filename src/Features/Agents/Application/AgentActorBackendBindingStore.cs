using System;
using System.Collections.Generic;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// In-memory explicit actor/backend binding store.
/// </summary>
internal sealed class AgentActorBackendBindingStore : IAgentActorBackendBindingStore
{
    private readonly Dictionary<ActorId, AgentActorBackendBinding> _bindings = new();
    private readonly object _sync = new();

    public bool TryGetBinding(ActorId actorId, out AgentActorBackendBinding binding)
    {
        lock (_sync)
        {
            return _bindings.TryGetValue(actorId, out binding!);
        }
    }

    public bool HasBinding(ActorId actorId)
    {
        lock (_sync)
        {
            return _bindings.ContainsKey(actorId);
        }
    }

    public AgentBackendId GetRequiredBackendId(ActorId actorId)
    {
        lock (_sync)
        {
            if (!_bindings.TryGetValue(actorId, out var binding))
            {
                throw new InvalidOperationException(
                    "No explicit backend binding exists for this actor.");
            }

            return binding.BackendId;
        }
    }

    public void SetBinding(AgentActorBackendBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        lock (_sync)
        {
            _bindings[binding.ActorId] = binding;
        }
    }
}
