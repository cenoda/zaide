using System;
using System.Collections.Generic;
using System.IO;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents;

/// <summary>
/// Test-only fake resolver that accepts absolute-path executables and binds
/// denylist results. Used to exercise fingerprint, display-summary, and
/// denylist-binding contracts without a real filesystem-aware resolver.
/// The production <see cref="DefaultAgentCommandResolver"/> is fail-closed
/// in Phase 17 M1.
/// </summary>
internal sealed class FakeTrustedCommandResolver : IAgentCommandResolver
{
    public bool TryResolve(
        AgentExecuteCommandActionPayload payload,
        out AgentResolvedCommand? resolvedCommand,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var rawExecutable = payload.Executable.Trim();

        if (string.IsNullOrWhiteSpace(rawExecutable))
        {
            resolvedCommand = null;
            error = "Executable is required.";
            return false;
        }

        if (!Path.IsPathRooted(rawExecutable))
        {
            resolvedCommand = null;
            error = "Executable must be an absolute path before permission review.";
            return false;
        }

        var canonicalPath = Path.GetFullPath(rawExecutable);
        var denylistResult = AgentCommandDenylist.Classify(canonicalPath);

        resolvedCommand = AgentResolvedCommand.CreateResolved(
            rawExecutable: rawExecutable,
            canonicalAbsoluteExecutablePath: canonicalPath,
            denylistResult: denylistResult,
            resolutionSource: AgentCommandResolutionSource.AbsolutePath,
            symlinkChain: Array.Empty<string>(),
            arguments: payload.Arguments,
            workingDirectory: payload.WorkingDirectory);
        error = null;
        return true;
    }
}
