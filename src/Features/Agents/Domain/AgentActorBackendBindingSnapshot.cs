using System.Collections.Generic;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Read-only binding snapshot for presentation.
/// </summary>
internal sealed class AgentActorBackendBindingSnapshot
{
    public AgentActorBackendBindingSnapshot(
        ActorId actorId,
        bool isBound,
        AgentBackendId backendId,
        string backendLabel,
        string statusCaption,
        bool isDisconnected,
        AgentAuthenticationConnectionState authenticationState,
        IReadOnlyList<string> advertisedAuthMethodIds)
    {
        ActorId = actorId;
        IsBound = isBound;
        BackendId = backendId;
        BackendLabel = backendLabel;
        StatusCaption = statusCaption;
        IsDisconnected = isDisconnected;
        AuthenticationState = authenticationState;
        AdvertisedAuthMethodIds = advertisedAuthMethodIds;
    }

    public ActorId ActorId { get; }

    public bool IsBound { get; }

    public AgentBackendId BackendId { get; }

    public string BackendLabel { get; }

    public string StatusCaption { get; }

    public bool IsDisconnected { get; }

    public AgentAuthenticationConnectionState AuthenticationState { get; }

    public IReadOnlyList<string> AdvertisedAuthMethodIds { get; }
}
