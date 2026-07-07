using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// Default implementation of <see cref="ITerminalHost"/>. Owns a single
/// active terminal session (M1). Creates the session eagerly via the
/// provided factory and proxies lifecycle operations to it.
/// </summary>
public sealed class TerminalHost : ITerminalHost
{
    private readonly TerminalViewModel _activeSession;
    private readonly IObservable<string?> _startupError;
    private bool _disposed;

    /// <inheritdoc/>
    public TerminalViewModel ActiveSession => _activeSession;

    /// <inheritdoc/>
    public IObservable<string?> StartupError => _startupError;

    /// <summary>
    /// Creates the host and eagerly builds the first (and for M1, only)
    /// terminal session via <paramref name="factory"/>.
    /// </summary>
    public TerminalHost(ITerminalSessionFactory factory)
    {
        _activeSession = factory.CreateSession();
        // Project the active session's StartupError reactive property as an observable.
        _startupError = _activeSession
            .WhenAnyValue(vm => vm.StartupError);
    }

    /// <inheritdoc/>
    public async Task EnsureActiveSessionStartedAsync()
    {
        await _activeSession.EnsureStartedAsync();
    }

    /// <inheritdoc/>
    public void FocusActiveSession()
    {
        // M1: placeholder — actual terminal focus requires a TerminalPanel
        // reference which lives only in the view layer. M2/M3 will wire this
        // through the tab host to focus the active tab's terminal surface.
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _activeSession.Dispose();
    }
}
