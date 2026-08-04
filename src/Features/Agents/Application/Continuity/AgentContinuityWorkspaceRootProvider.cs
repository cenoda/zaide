using System;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Application.Continuity;

/// <summary>
/// Resolves continuity checkpoint and reconcile workspace roots from the
/// production workspace authority. New checkpoints must never use incidental
/// process CWD when an opened workspace scope exists.
/// </summary>
internal static class AgentContinuityWorkspaceRootProvider
{
    public static Func<string?> CreateOpenedWorkspaceProvider(IWorkspaceActionAuthority? workspaceAuthority) =>
        () =>
        {
            if (workspaceAuthority?.TryCaptureCurrentScope(out var scope) != true
                || string.IsNullOrWhiteSpace(scope.RootPath))
            {
                return null;
            }

            return scope.RootPath;
        };

    public static Func<string?> CreateLegacyProcessCwdProvider() =>
        () => Environment.CurrentDirectory;
}
