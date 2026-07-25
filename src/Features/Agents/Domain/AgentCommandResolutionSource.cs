namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Describes how Zaide resolved a raw command executable to its canonical identity.
/// </summary>
internal enum AgentCommandResolutionSource
{
    /// <summary>
    /// The executable was already an absolute path and was used without further
    /// infrastructure resolution. Symlink status is unverified.
    /// </summary>
    AbsolutePath,

    /// <summary>
    /// The executable was resolved via PATH search. The canonical target may
    /// differ from the raw token.
    /// </summary>
    PathResolution,

    /// <summary>
    /// The executable path traversed one or more symlinks before reaching the
    /// canonical target. The full chain is recorded.
    /// </summary>
    SymlinkChain,

    /// <summary>
    /// The executable was provided by the backend as a fully-resolved absolute
    /// path and was accepted without further resolution.
    /// </summary>
    BackendProvided,
}
