using Zaide.Features.Workspace.Contracts;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Test-only workspace action authority. Reports the captured scope as current
/// until <see cref="IsStale"/> is set, simulating a workspace close/switch that
/// bumps the workspace generation between capture and execution.
/// </summary>
internal sealed class FakeWorkspaceActionAuthority : IWorkspaceActionAuthority
{
    private readonly WorkspaceActionScope _scope;

    public FakeWorkspaceActionAuthority(WorkspaceActionScope scope)
    {
        _scope = scope;
    }

    public bool IsStale { get; set; }

    public bool TryCaptureCurrentScope(out WorkspaceActionScope scope)
    {
        scope = _scope;
        return !IsStale;
    }

    public bool IsCurrent(WorkspaceActionScope scope) =>
        !IsStale && _scope.Equals(scope);
}
