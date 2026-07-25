using System;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Workspace.Contracts;

/// <summary>
/// Authoritative source of the active workspace scope for the action control
/// plane. The broker captures a scope once at admission and re-resolves the
/// live state through this authority immediately before execution, so a
/// workspace close or switch (a generation change) invalidates stale action
/// authority instead of executing against a scope that is no longer current.
/// </summary>
internal interface IWorkspaceActionAuthority
{
    /// <summary>
    /// Raised when the active workspace scope is closed, switched, or otherwise
    /// invalidated. Subscribers should revoke pending action authority.
    /// </summary>
    event Action? ScopeInvalidated;

    /// <summary>
    /// Captures the current active workspace scope, or returns <c>false</c> when
    /// no workspace is open.
    /// </summary>
    bool TryCaptureCurrentScope(out WorkspaceActionScope scope);

    /// <summary>
    /// Returns <c>true</c> only when the supplied captured scope still matches
    /// the live workspace identity and generation.
    /// </summary>
    bool IsCurrent(WorkspaceActionScope scope);
}
