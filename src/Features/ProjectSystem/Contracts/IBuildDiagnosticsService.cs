using System;
using Zaide.Features.ProjectSystem.Domain;
using Zaide.Features.Language.Contracts;

namespace Zaide.Features.ProjectSystem.Contracts;

/// <summary>
/// Owns parsed build diagnostics from the project workflow output stream.
/// Does not write into <see cref="ILanguageDiagnosticsService"/>.
/// </summary>
public interface IBuildDiagnosticsService : IDisposable
{
    /// <summary>The current immutable build-diagnostics snapshot.</summary>
    BuildDiagnosticsSnapshot Current { get; }

    /// <summary>
    /// Emits each new <see cref="BuildDiagnosticsSnapshot"/> on the calling thread.
    /// </summary>
    IObservable<BuildDiagnosticsSnapshot> WhenChanged { get; }
}
