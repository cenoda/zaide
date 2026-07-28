using System;
using System.IO;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Converts ACP absolute paths into validated workspace-relative paths.
/// </summary>
internal static class AcpWorkspaceAbsolutePathConverter
{
    public static bool TryConvert(
        string absolutePath,
        string workspaceRoot,
        out AgentWorkspaceRelativePath? relativePath,
        out string? failureReason)
    {
        relativePath = null;
        failureReason = null;

        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            failureReason = "ACP filesystem path is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            failureReason = "Workspace root is required for ACP filesystem mediation.";
            return false;
        }

        string fullAbsolutePath;
        string fullWorkspaceRoot;
        try
        {
            fullAbsolutePath = Path.GetFullPath(absolutePath);
            fullWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            return false;
        }

        var rootedWorkspace = fullWorkspaceRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullWorkspaceRoot
            : fullWorkspaceRoot + Path.DirectorySeparatorChar;

        if (!fullAbsolutePath.StartsWith(rootedWorkspace, StringComparison.Ordinal)
            && !string.Equals(fullAbsolutePath, fullWorkspaceRoot, StringComparison.Ordinal))
        {
            failureReason = "ACP filesystem path is outside the workspace root.";
            return false;
        }

        var relative = Path.GetRelativePath(fullWorkspaceRoot, fullAbsolutePath);
        if (string.IsNullOrWhiteSpace(relative) || relative == ".")
        {
            failureReason = "ACP filesystem path must reference a file.";
            return false;
        }

        try
        {
            relativePath = AgentWorkspaceRelativePath.Normalize(relative);
            return true;
        }
        catch (ArgumentException ex)
        {
            failureReason = ex.Message;
            return false;
        }
    }
}
