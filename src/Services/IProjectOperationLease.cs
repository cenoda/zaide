using System;

namespace Zaide.Services;

/// <summary>
/// Exclusive lease held for one admitted project operation. Dispose to release admission.
/// </summary>
public interface IProjectOperationLease : IDisposable
{
    /// <summary>The admitted operation kind.</summary>
    ProjectOperationKind Kind { get; }
}