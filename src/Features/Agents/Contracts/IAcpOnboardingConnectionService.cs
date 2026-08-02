using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Bounded ACP configuration probe, authenticate, and capability-gated logout.
/// Does not create a prompt session during configuration.
/// </summary>
internal interface IAcpOnboardingConnectionService
{
    Task<AcpOnboardingProbeResult> ProbeAsync(ActorId actorId, CancellationToken cancellationToken);

    Task<AcpOnboardingAuthResult> AuthenticateAsync(
        ActorId actorId,
        string methodId,
        CancellationToken cancellationToken);

    Task<AcpOnboardingLogoutResult> LogoutAsync(ActorId actorId, CancellationToken cancellationToken);

    bool IsLogoutSupported(ActorId actorId);

    IReadOnlyList<string> GetNegotiatedAuthMethodIds(ActorId actorId);
}
