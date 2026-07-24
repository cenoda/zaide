using System;
using System.Collections.Generic;
using System.Linq;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Normalized workspace-relative path accepted by the action control plane.
/// </summary>
internal sealed class AgentWorkspaceRelativePath
{
    private AgentWorkspaceRelativePath(string normalizedPath)
    {
        NormalizedPath = normalizedPath;
    }

    public string NormalizedPath { get; }

    public static AgentWorkspaceRelativePath Normalize(string workspaceRelativePath)
    {
        if (string.IsNullOrWhiteSpace(workspaceRelativePath))
        {
            throw new ArgumentException("Workspace-relative path is required.", nameof(workspaceRelativePath));
        }

        var trimmed = workspaceRelativePath.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Workspace-relative path is required.", nameof(workspaceRelativePath));
        }

        if (PathIsAbsolute(trimmed))
        {
            throw new ArgumentException(
                "Absolute paths are not allowed.",
                nameof(workspaceRelativePath));
        }

        var segments = trimmed
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            throw new ArgumentException(
                "Workspace-relative path must reference a file.",
                nameof(workspaceRelativePath));
        }

        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                throw new ArgumentException(
                    "Path traversal is not allowed.",
                    nameof(workspaceRelativePath));
            }
        }

        var normalized = string.Join('/', segments);
        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                "Workspace-relative path must reference a file.",
                nameof(workspaceRelativePath));
        }

        return new AgentWorkspaceRelativePath(normalized);
    }

    private static bool PathIsAbsolute(string path)
    {
        if (path.StartsWith('/'))
        {
            return true;
        }

        return path.Length >= 2
            && char.IsAsciiLetter(path[0])
            && path[1] == ':';
    }

    public override string ToString() => NormalizedPath;
}
