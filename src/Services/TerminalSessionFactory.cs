namespace Zaide.Services;

using Zaide.ViewModels;

/// <summary>
/// Default implementation of <see cref="ITerminalSessionFactory"/> that creates
/// a <see cref="LinuxTerminalService"/> and wraps it in a <see cref="TerminalViewModel"/>.
/// Each session gets its own PTY, shell process, and screen state.
/// </summary>
public sealed class TerminalSessionFactory : ITerminalSessionFactory
{
    /// <inheritdoc/>
    public TerminalViewModel CreateSession()
    {
        var service = new LinuxTerminalService();
        return new TerminalViewModel(service);
    }
}
