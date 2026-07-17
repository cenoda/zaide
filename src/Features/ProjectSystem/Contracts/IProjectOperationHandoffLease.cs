using Zaide.Features.ProjectSystem.Domain;
namespace Zaide.Features.ProjectSystem.Contracts;

/// <summary>
/// Debug-start handoff lease held across build, target resolution, and adapter launch.
/// </summary>
public interface IProjectOperationHandoffLease : IProjectOperationLease
{
    /// <summary>Returns whether the handoff lease is still active.</summary>
    bool IsActive { get; }
}