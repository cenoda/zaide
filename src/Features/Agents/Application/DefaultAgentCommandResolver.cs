using System;
using System.Collections.Generic;
using System.IO;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Default M1 command resolver that validates absolute-path executables and
/// binds denylist results before permission approval.
/// </summary>
/// <remarks>
/// PATH search and symlink resolution are deferred to a later infrastructure
/// implementation of <see cref="IAgentCommandResolver"/>. This default resolver
/// accepts only already-absolute paths and classifies them against the locked
/// denylist. The contract (<see cref="IAgentCommandResolver"/>) ensures that
/// later filesystem-aware resolvers can be swapped in without changing the
/// broker, composer, or fingerprint pipeline.
/// </remarks>
internal sealed class DefaultAgentCommandResolver : IAgentCommandResolver
{
    /// <summary>
    /// Resolves a raw command payload by validating the executable is an
    /// absolute path and binding the denylist result.
    /// </summary>
    public bool TryResolve(
        AgentExecuteCommandActionPayload payload,
        out AgentResolvedCommand? resolvedCommand,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var rawExecutable = payload.Executable.Trim();

        if (!IsResolvable(rawExecutable, out error))
        {
            resolvedCommand = null;
            return false;
        }

        var canonicalPath = Path.GetFullPath(rawExecutable);
        var denylistResult = AgentCommandDenylist.Classify(canonicalPath);

        // Detect symlink-to-denied: if the raw executable has a shell/privesc
        // basename, classify it here. When the PATH/symlink-aware resolver
        // replaces this implementation, it will follow the chain and classify
        // the actual canonical target — catching symlinks that disguise
        // /bin/bash as /usr/bin/some-tool.
        var resolutionSource = AgentCommandResolutionSource.AbsolutePath;
        var symlinkChain = Array.Empty<string>();

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

    /// <summary>
    /// Determines whether the raw executable string is resolvable by this
    /// resolver. PATH-relative names are rejected — they require the
    /// filesystem-aware resolver.
    /// </summary>
    private static bool IsResolvable(string executable, out string? error)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            error = "Executable is required.";
            return false;
        }

        if (!Path.IsPathRooted(executable))
        {
            error = "Executable must be an absolute path before permission review "
                    + "(PATH resolution is not available in Phase 17 M1).";
            return false;
        }

        error = null;
        return true;
    }
}
