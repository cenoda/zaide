using System;
using Zaide.Features.ProjectSystem.Domain;

namespace Zaide.Features.ProjectSystem.Contracts;

/// <summary>
/// Exclusive lease held for one admitted project operation. Dispose to release admission.
/// </summary>
public interface IProjectOperationLease : IDisposable
{
    /// <summary>The admitted operation kind.</summary>
    ProjectOperationKind Kind { get; }
}