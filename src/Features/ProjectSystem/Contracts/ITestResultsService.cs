using System;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Contracts;

/// <summary>
/// Owns structured test outcomes from the project workflow output stream.
/// Does not replace raw Output lines.
/// </summary>
public interface ITestResultsService : IDisposable
{
    /// <summary>The current immutable test-results snapshot.</summary>
    TestResultsSnapshot Current { get; }

    /// <summary>
    /// Emits each new <see cref="TestResultsSnapshot"/> on the calling thread.
    /// </summary>
    IObservable<TestResultsSnapshot> WhenChanged { get; }
}
