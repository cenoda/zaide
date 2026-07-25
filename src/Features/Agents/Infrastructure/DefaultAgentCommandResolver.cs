using System;
using System.Collections.Generic;
using System.IO;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Infrastructure;

/// <summary>
/// Production resolver that binds canonical executable identity, symlink metadata,
/// and denylist results before permission review.
/// </summary>
internal sealed class DefaultAgentCommandResolver : IAgentCommandResolver
{
    public bool TryResolve(
        AgentExecuteCommandActionPayload payload,
        out AgentResolvedCommand? resolvedCommand,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(payload);
        resolvedCommand = null;

        var rawExecutable = payload.Executable.Trim();
        if (string.IsNullOrWhiteSpace(rawExecutable))
        {
            error = "Executable is required.";
            return false;
        }

        if (!TryLocateCandidateExecutable(rawExecutable, out var candidatePath, out error))
        {
            return false;
        }

        if (!AgentCommandPathSupport.TryRealpath(candidatePath, out var canonicalPath))
        {
            error = "Executable could not be resolved to a canonical absolute path.";
            return false;
        }

        IReadOnlyList<string> symlinkChain = Array.Empty<string>();
        var resolutionSource = Path.IsPathRooted(rawExecutable)
            ? AgentCommandResolutionSource.AbsolutePath
            : AgentCommandResolutionSource.PathResolution;

        if (!string.Equals(candidatePath, canonicalPath, StringComparison.Ordinal))
        {
            symlinkChain = new[] { candidatePath };
            resolutionSource = AgentCommandResolutionSource.SymlinkChain;
        }

        if (!AgentCommandPathSupport.IsRegularExecutableFile(canonicalPath))
        {
            error = "Executable is missing or is not a regular executable file.";
            return false;
        }

        var denylistResult = AgentCommandDenylist.Classify(canonicalPath);
        resolvedCommand = AgentResolvedCommand.CreateResolved(
            rawExecutable: rawExecutable,
            canonicalAbsoluteExecutablePath: canonicalPath,
            denylistResult: denylistResult,
            resolutionSource: resolutionSource,
            symlinkChain: symlinkChain,
            arguments: payload.Arguments,
            workingDirectory: payload.WorkingDirectory);
        error = null;
        return true;
    }

    private static bool TryLocateCandidateExecutable(
        string rawExecutable,
        out string candidatePath,
        out string? error)
    {
        candidatePath = string.Empty;
        error = null;

        if (Path.IsPathRooted(rawExecutable))
        {
            candidatePath = Path.GetFullPath(rawExecutable);
            return true;
        }

        if (rawExecutable.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || rawExecutable.Contains(Path.AltDirectorySeparatorChar))
        {
            error = "Relative executable paths are not supported.";
            return false;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            error = "Executable could not be resolved via PATH.";
            return false;
        }

        foreach (var directory in pathValue.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, rawExecutable);
            if (File.Exists(candidate))
            {
                candidatePath = Path.GetFullPath(candidate);
                return true;
            }
        }

        error = "Executable could not be resolved via PATH.";
        return false;
    }
}
