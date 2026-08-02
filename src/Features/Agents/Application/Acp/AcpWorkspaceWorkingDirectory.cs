using System;
using Zaide.Features.Workspace.Contracts;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Resolves ACP process/session working directory from the production workspace
/// authority. Fails closed when no valid workspace is open.
/// </summary>
internal static class AcpWorkspaceWorkingDirectory
{
    public static bool TryResolve(IWorkspaceActionAuthority? workspaceAuthority, out string rootPath)
    {
        rootPath = string.Empty;
        if (workspaceAuthority is null)
        {
            return false;
        }

        if (!workspaceAuthority.TryCaptureCurrentScope(out var scope)
            || string.IsNullOrWhiteSpace(scope.RootPath)
            || !System.IO.Path.IsPathRooted(scope.RootPath))
        {
            return false;
        }

        rootPath = scope.RootPath;
        return true;
    }

    public static Func<string> CreateProvider(IWorkspaceActionAuthority? workspaceAuthority) =>
        () =>
        {
            if (TryResolve(workspaceAuthority, out var rootPath))
            {
                return rootPath;
            }

            throw new InvalidOperationException(
                "No valid workspace is available for ACP backend operations.");
        };
}
