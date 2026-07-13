using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// Singleton service that owns the csharp-ls process and StreamJsonRpc transport.
/// Consumes only <see cref="IProjectContextService"/> snapshots; never discovers projects.
/// </summary>
public interface ILanguageSessionService : IDisposable
{
    /// <summary>The current immutable language-session snapshot.</summary>
    LanguageSessionSnapshot Current { get; }

    /// <summary>
    /// Emits each new <see cref="LanguageSessionSnapshot"/> on the calling thread.
    /// </summary>
    IObservable<LanguageSessionSnapshot> WhenChanged { get; }

    /// <summary>
    /// Tears down the active session and starts a new generation when project
    /// context is eligible. Publishes unavailable/loading/failed state otherwise.
    /// </summary>
    Task RestartAsync(CancellationToken cancellationToken = default);
}
