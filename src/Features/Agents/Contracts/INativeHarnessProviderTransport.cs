using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Backend-owned provider transport for Native Harness model rounds.
/// Does not flow through <see cref="IAgentActionBroker"/>.
/// </summary>
internal interface INativeHarnessProviderTransport
{
    Task<NativeHarnessProviderResponse> CompleteChatAsync(
        AgentExecutionOptions options,
        NativeHarnessProviderRequest request,
        CancellationToken cancellationToken);
}
