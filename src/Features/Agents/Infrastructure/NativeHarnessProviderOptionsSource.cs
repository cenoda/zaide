using System;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Resolves live provider options from <see cref="AgentExecutionService"/>.
/// </summary>
internal sealed class NativeHarnessProviderOptionsSource : INativeHarnessProviderOptionsSource
{
    private readonly AgentExecutionService _executionService;

    public NativeHarnessProviderOptionsSource(AgentExecutionService executionService)
    {
        _executionService = executionService
            ?? throw new ArgumentNullException(nameof(executionService));
    }

    public AgentExecutionOptions? ResolveOptions()
    {
        try
        {
            return _executionService.BuildEffectiveOptions();
        }
        catch
        {
            return null;
        }
    }
}
