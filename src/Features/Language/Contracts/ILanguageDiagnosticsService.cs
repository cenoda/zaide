using System;
using Zaide.Features.Language.Application;

namespace Zaide.Features.Language.Contracts;

/// <summary>
/// Owns structured per-document diagnostics from the live language session.
/// Does not own process/JSON-RPC lifecycle.
/// </summary>
public interface ILanguageDiagnosticsService : IDisposable
{
    /// <summary>The current immutable diagnostics snapshot.</summary>
    LanguageDiagnosticsSnapshot Current { get; }

    /// <summary>
    /// Emits each new <see cref="LanguageDiagnosticsSnapshot"/> on the calling thread.
    /// </summary>
    IObservable<LanguageDiagnosticsSnapshot> WhenChanged { get; }
}
