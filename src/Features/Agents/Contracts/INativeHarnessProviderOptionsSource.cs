using Zaide.Features.Agents.Application;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Resolves effective provider options for one Native Harness model round.
/// </summary>
internal interface INativeHarnessProviderOptionsSource
{
    AgentExecutionOptions? ResolveOptions();
}
