namespace Zaide.Features.Terminal.Contracts;

/// <summary>
/// Factory that creates a terminal process owner (<see cref="ITerminalService"/>).
/// Each call produces an independent PTY and shell process. Presentation owns
/// pairing each service with a <c>TerminalViewModel</c>.
/// </summary>
public interface ITerminalServiceFactory
{
    /// <summary>
    /// Creates a new terminal service. The caller owns the returned instance
    /// and must dispose it when the session ends.
    /// </summary>
    ITerminalService Create();
}
