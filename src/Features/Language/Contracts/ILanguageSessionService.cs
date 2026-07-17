using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.ProjectSystem.Contracts;
using Zaide.Features.Language.Application;
using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;

namespace Zaide.Features.Language.Contracts;

/// <summary>
/// Singleton service that owns the csharp-ls process and StreamJsonRpc transport.
/// Consumes only <see cref="IProjectContextService"/> snapshots; never discovers projects.
/// </summary>
public interface ILanguageSessionService : IDisposable, IAsyncDisposable
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

    /// <summary>
    /// Returns the live server session when <paramref name="generation"/> matches
    /// <see cref="LanguageSessionSnapshot.Generation"/> and state is
    /// <see cref="LanguageSessionState.Ready"/>; otherwise <c>null</c>.
    /// </summary>
    ILanguageServerSession? TryGetReadySession(long generation);
}
