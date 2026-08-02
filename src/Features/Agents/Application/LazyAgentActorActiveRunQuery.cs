using System;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Defers resolution of the active-run gate so the durable binding store can be
/// constructed before <see cref="IAgentSessionService"/> without a DI cycle.
/// </summary>
internal sealed class LazyAgentActorActiveRunQuery : IAgentActorActiveRunQuery
{
    private readonly Func<IAgentActorActiveRunQuery?> _resolver;
    private readonly object _sync = new();
    private IAgentActorActiveRunQuery? _resolved;
    private bool _attempted;

    public LazyAgentActorActiveRunQuery(Func<IAgentActorActiveRunQuery?> resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public bool HasActiveRun(ActorId actorId)
    {
        var query = Resolve();
        return query?.HasActiveRun(actorId) == true;
    }

    private IAgentActorActiveRunQuery? Resolve()
    {
        if (_attempted)
        {
            return _resolved;
        }

        lock (_sync)
        {
            if (_attempted)
            {
                return _resolved;
            }

            _resolved = _resolver();
            _attempted = true;
            return _resolved;
        }
    }
}
