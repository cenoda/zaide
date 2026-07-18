using Zaide.Features.Terminal.Contracts;

namespace Zaide.Features.Terminal.Infrastructure;

/// <summary>
/// Default <see cref="ITerminalServiceFactory"/> that creates a fresh
/// <see cref="LinuxTerminalService"/> per call.
/// </summary>
internal sealed class LinuxTerminalServiceFactory : ITerminalServiceFactory
{
    /// <inheritdoc/>
    public ITerminalService Create() => new LinuxTerminalService();
}
