namespace Zaide.Features.Language.Application;

/// <summary>
/// Machine-readable failure categories for <see cref="LanguageSessionFailure"/>.
/// </summary>
public enum LanguageSessionFailureKind
{
    /// <summary>The csharp-ls binary could not be resolved on PATH or via configuration.</summary>
    MissingServerBinary,

    /// <summary>The child process failed to start.</summary>
    ProcessStartFailed,

    /// <summary>The LSP <c>initialize</c> request failed or returned an error.</summary>
    InitializeFailed,

    /// <summary>The language server process exited unexpectedly.</summary>
    ServerExited,

    /// <summary>Graceful shutdown or transport disposal failed.</summary>
    ShutdownFailed,
}
