using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Explicit user-driven backend and authentication selection boundary.
/// </summary>
internal interface IAgentActorBackendSelectionService
{
    AgentActorBackendBindingSnapshot GetSnapshot(ActorId actorId);

    void BindNativeHarness(ActorId actorId);

    void BindAcpRuntime(
        ActorId actorId,
        AcpRuntimeIdentity runtimeIdentity,
        string expectedAgentName,
        string expectedAgentVersion);

    IReadOnlyList<string> GetAdvertisedAuthMethodIds(ActorId actorId);

    Task RequestAuthenticateAsync(ActorId actorId, string methodId, CancellationToken cancellationToken);
}
