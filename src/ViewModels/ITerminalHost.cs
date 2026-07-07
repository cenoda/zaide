using System;
using System.Threading.Tasks;

namespace Zaide.ViewModels;

/// <summary>
/// Host-level seam that owns terminal sessions and provides lifecycle operations
/// (start, focus, error projection) without exposing concrete session wiring to
/// the view layer. M1 owns exactly one session; M2/M3 will manage multiple tabs.
/// </summary>
public interface ITerminalHost : IDisposable
{
    /// <summary>Currently active terminal session. Never null after construction.</summary>
    TerminalViewModel ActiveSession { get; }

    /// <summary>
    /// Observable that fires when the active session's <see cref="TerminalViewModel.StartupError"/>
    /// changes. The view layer can subscribe to project terminal errors into the
    /// status bar without reaching into the session directly.
    /// </summary>
    IObservable<string?> StartupError { get; }

    /// <summary>
    /// Ensures the active terminal session is started (lazy-start seam).
    /// Replaces direct <c>TerminalViewModel.EnsureStartedAsync()</c> calls from the view.
    /// </summary>
    Task EnsureActiveSessionStartedAsync();

    /// <summary>
    /// Host-level focus seam. For M1 this is a placeholder — the view layer still
    /// calls <c>TerminalPanel.FocusTerminal()</c> directly. M2/M3 will use this
    /// to focus the active tab's terminal surface.
    /// </summary>
    void FocusActiveSession();
}
