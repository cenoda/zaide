using Zaide.Features.Terminal.Presentation;

namespace Zaide.Features.Terminal.Contracts;



/// <summary>
/// Factory that creates a terminal session — a paired <see cref="ITerminalService"/>
/// and <see cref="TerminalViewModel"/> — as a single unit. Each call produces an
/// independent session with its own PTY, shell process, screen state, and logs.
/// </summary>
public interface ITerminalSessionFactory
{
    /// <summary>
    /// Creates a new terminal session. The caller owns the returned
    /// <see cref="TerminalViewModel"/> and must dispose it when the session ends.
    /// </summary>
    TerminalViewModel CreateSession();
}
