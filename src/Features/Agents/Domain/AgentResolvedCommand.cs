using System;
using System.Collections.Generic;
using System.IO;

namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Immutable syntactically resolved command identity used for fingerprints and display.
/// Binds the raw agent request to a Zaide-resolved canonical executable identity
/// and denylist result before permission approval.
/// </summary>
internal sealed class AgentResolvedCommand
{
    private AgentResolvedCommand(
        string rawExecutable,
        string canonicalAbsoluteExecutablePath,
        AgentCommandDenylistResult denylistResult,
        AgentCommandResolutionSource resolutionSource,
        IReadOnlyList<string> symlinkChain,
        IReadOnlyList<string> arguments,
        AgentWorkspaceRelativePath workingDirectory)
    {
        RawExecutable = rawExecutable;
        CanonicalAbsoluteExecutablePath = canonicalAbsoluteExecutablePath;
        DenylistResult = denylistResult;
        ResolutionSource = resolutionSource;
        SymlinkChain = symlinkChain;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
    }

    /// <summary>
    /// The raw executable string exactly as received from the agent backend
    /// before any resolution or normalization.
    /// </summary>
    public string RawExecutable { get; }

    /// <summary>
    /// The fully-resolved canonical absolute executable path after PATH lookup
    /// and symlink resolution. This is the identity bound into fingerprints.
    /// </summary>
    public string CanonicalAbsoluteExecutablePath { get; }

    /// <summary>
    /// Denylist classification result bound to the canonical executable path.
    /// Computed during resolution so it is available before permission approval.
    /// </summary>
    public AgentCommandDenylistResult DenylistResult { get; }

    /// <summary>
    /// Describes how the resolver arrived at <see cref="CanonicalAbsoluteExecutablePath"/>
    /// from <see cref="RawExecutable"/>.
    /// </summary>
    public AgentCommandResolutionSource ResolutionSource { get; }

    /// <summary>
    /// Ordered symlink chain from the first symlink encountered to the final
    /// canonical target. Empty when <see cref="ResolutionSource"/> is not
    /// <see cref="AgentCommandResolutionSource.SymlinkChain"/>.
    /// Each entry is an absolute path.
    /// </summary>
    public IReadOnlyList<string> SymlinkChain { get; }

    /// <summary>
    /// Immutable argument vector.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Working directory expressed relative to the workspace root.
    /// </summary>
    public AgentWorkspaceRelativePath WorkingDirectory { get; }

    /// <summary>
    /// True when the canonical executable is a shell interpreter, regardless
    /// of whether it was reached directly or through a symlink chain.
    /// </summary>
    public bool IsShellInterpreter =>
        DenylistResult.Classification == AgentCommandDenylistClassification.DeniedShellInterpreter;

    /// <summary>
    /// True when the canonical executable is a privilege-escalation helper,
    /// regardless of whether it was reached directly or through a symlink chain.
    /// </summary>
    public bool IsPrivilegeEscalation =>
        DenylistResult.Classification == AgentCommandDenylistClassification.DeniedPrivilegeEscalation;

    /// <summary>
    /// Legacy TryCreate entrypoint used by code that does not yet have access
    /// to an <see cref="Contracts.IAgentCommandResolver"/>. Delegates to a
    /// basic absolute-path validation and denylist classification without
    /// PATH or symlink resolution.
    /// </summary>
    public static bool TryCreate(
        AgentExecuteCommandActionPayload payload,
        out AgentResolvedCommand? resolvedCommand,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!TryNormalizeAbsoluteExecutable(payload.Executable, out var canonicalPath, out error))
        {
            resolvedCommand = null;
            return false;
        }

        var denylistResult = AgentCommandDenylist.Classify(canonicalPath);
        resolvedCommand = new AgentResolvedCommand(
            rawExecutable: payload.Executable,
            canonicalAbsoluteExecutablePath: canonicalPath,
            denylistResult: denylistResult,
            resolutionSource: AgentCommandResolutionSource.AbsolutePath,
            symlinkChain: Array.Empty<string>(),
            arguments: payload.Arguments,
            workingDirectory: payload.WorkingDirectory);
        error = null;
        return true;
    }

    /// <summary>
    /// Creates a resolved command from a resolver implementation, binding the
    /// full resolution metadata into the immutable identity.
    /// </summary>
    internal static AgentResolvedCommand CreateResolved(
        string rawExecutable,
        string canonicalAbsoluteExecutablePath,
        AgentCommandDenylistResult denylistResult,
        AgentCommandResolutionSource resolutionSource,
        IReadOnlyList<string> symlinkChain,
        IReadOnlyList<string> arguments,
        AgentWorkspaceRelativePath workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(rawExecutable))
        {
            throw new ArgumentException("Raw executable is required.", nameof(rawExecutable));
        }

        if (string.IsNullOrWhiteSpace(canonicalAbsoluteExecutablePath))
        {
            throw new ArgumentException(
                "Canonical absolute executable path is required.",
                nameof(canonicalAbsoluteExecutablePath));
        }

        ArgumentNullException.ThrowIfNull(denylistResult);
        ArgumentNullException.ThrowIfNull(symlinkChain);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(workingDirectory);

        if (!Path.IsPathRooted(canonicalAbsoluteExecutablePath))
        {
            throw new ArgumentException(
                "Canonical executable path must be absolute.",
                nameof(canonicalAbsoluteExecutablePath));
        }

        if (resolutionSource == AgentCommandResolutionSource.SymlinkChain
            && symlinkChain.Count == 0)
        {
            throw new ArgumentException(
                "Symlink chain must be non-empty when resolution source is SymlinkChain.",
                nameof(symlinkChain));
        }

        return new AgentResolvedCommand(
            rawExecutable,
            canonicalAbsoluteExecutablePath,
            denylistResult,
            resolutionSource,
            symlinkChain,
            arguments,
            workingDirectory);
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
