using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zaide.Services;

/// <summary>
/// A live language-server child process with an initialized StreamJsonRpc transport.
/// </summary>
public interface ILanguageServerSession : IAsyncDisposable
{
    /// <summary>Generation captured when this session was created.</summary>
    long Generation { get; }

    /// <summary>Child process id, or <c>null</c> when not launched.</summary>
    int? ProcessId { get; }

    /// <summary>Whether the child process has exited.</summary>
    bool HasExited { get; }

    /// <summary>
    /// Raised once when the child process exits. The argument is <see cref="Generation"/>.
    /// </summary>
    event Action<long>? ProcessExited;

    /// <summary>Graceful LSP <c>shutdown</c> followed by <c>exit</c>.</summary>
    Task ShutdownAsync(CancellationToken cancellationToken);

    /// <summary>Force-kill the process tree without protocol shutdown.</summary>
    Task ForceKillAsync();
}
