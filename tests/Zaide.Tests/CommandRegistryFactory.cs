using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Zaide.Services;

namespace Zaide.Tests;

/// <summary>
/// Builds a real <see cref="ICommandRegistry"/> for tests that verify canonical
/// command registration. Mirrors the production singleton wiring (no-op logger,
/// <see cref="CommandRegistry"/>) without creating a second registration path.
/// </summary>
internal static class CommandRegistryFactory
{
    public static ICommandRegistry Create()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<CommandRegistry>>(_ => NullLogger<CommandRegistry>.Instance);
        services.AddSingleton<ICommandRegistry, CommandRegistry>();
        return services.BuildServiceProvider().GetRequiredService<ICommandRegistry>();
    }
}
