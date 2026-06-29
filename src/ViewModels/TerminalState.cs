namespace Zaide.ViewModels;

/// <summary>
/// Lifecycle state of the embedded terminal session, surfaced to the view so
/// the user can see whether the shell is alive, has exited, or failed to start.
/// </summary>
public enum TerminalState
{
    /// <summary>No start has been attempted yet.</summary>
    NotStarted,

    /// <summary>The shell process is alive.</summary>
    Running,

    /// <summary>The shell process exited (e.g. the user typed <c>exit</c>).</summary>
    Exited,

    /// <summary>The last start attempt failed; see <c>StartupError</c>.</summary>
    Error
}
