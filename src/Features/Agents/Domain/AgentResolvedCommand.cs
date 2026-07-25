using System;
using System.Collections.Generic;
using System.IO;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable syntactically resolved command identity used for fingerprints and display.
/// </summary>
internal sealed class AgentResolvedCommand
{
    private AgentResolvedCommand(
        string canonicalAbsoluteExecutablePath,
        AgentCommandDenylistResult denylistResult,
        IReadOnlyList<string> arguments,
        AgentWorkspaceRelativePath workingDirectory)
    {
        CanonicalAbsoluteExecutablePath = canonicalAbsoluteExecutablePath;
        DenylistResult = denylistResult;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
    }

    public string CanonicalAbsoluteExecutablePath { get; }

    public AgentCommandDenylistResult DenylistResult { get; }

    public IReadOnlyList<string> Arguments { get; }

    public AgentWorkspaceRelativePath WorkingDirectory { get; }

    public static bool TryCreate(
        AgentExecuteCommandActionPayload payload,
        out AgentResolvedCommand? resolvedCommand,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!TryNormalizeAbsoluteExecutable(payload.Executable, out var canonicalExecutable, out error))
        {
            resolvedCommand = null;
            return false;
        }

        var denylistResult = AgentCommandDenylist.Classify(canonicalExecutable);
        resolvedCommand = new AgentResolvedCommand(
            canonicalExecutable,
            denylistResult,
            payload.Arguments,
            payload.WorkingDirectory);
        error = null;
        return true;
    }

    private static bool TryNormalizeAbsoluteExecutable(
        string executable,
        out string canonicalExecutable,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            canonicalExecutable = string.Empty;
            error = "Executable is required.";
            return false;
        }

        var trimmed = executable.Trim();
        if (!Path.IsPathRooted(trimmed))
        {
            canonicalExecutable = string.Empty;
            error = "Executable must be an absolute path before permission review.";
            return false;
        }

        canonicalExecutable = Path.GetFullPath(trimmed);
        error = null;
        return true;
    }
}
