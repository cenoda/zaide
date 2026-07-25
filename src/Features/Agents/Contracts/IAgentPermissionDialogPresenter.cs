using System;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Features.Agents.Contracts;

/// <summary>
/// Abstraction over the visible permission review dialog surface.
/// The implementation owns dialog creation, ViewModel binding, modal
/// display, and deny-on-dismiss behavior.  Registered as a singleton
/// so the shell can set the owner window after DI build.
/// </summary>
internal interface IAgentPermissionDialogPresenter
{
    /// <summary>
    /// Shows the permission review dialog modally and returns <c>true</c>
    /// when the user explicitly allowed the action, or <c>false</c> when
    /// the user denied or dismissed the dialog.
    /// </summary>
    Task<bool> ShowAsync(
        AgentActionRequest request,
        AgentActionDisplaySummary displaySummary,
        WorkspaceActionScope? workspaceScope,
        CancellationToken cancellationToken);
}
