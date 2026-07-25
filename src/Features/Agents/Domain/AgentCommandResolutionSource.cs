namespace Zaide.Features.Agents.Domain;

/// <summary>
/// Describes how Zaide resolved a raw command executable to its canonical identity.
/// </summary>
internal enum AgentCommandResolutionSource
{
    /// <summary>
    /// The executable was already an absolute path and was used without further
    /// infrastructure resolution. Only available in test contexts; the
    /// production resolver is fail-closed in Phase 17 M1.
    /// </summary>
    AbsolutePath,

    /// <summary>
    /// The executable was resolved via PATH search. The canonical target may
    /// differ from the raw token. Requires a trusted filesystem-aware resolver.
    /// </summary>
    PathResolution,

    /// <summary>
    /// The executable path traversed one or more symlinks before reaching the
    /// canonical target. The full chain is recorded. Requires a trusted
    /// filesystem-aware resolver.
    /// </summary>
    SymlinkChain,
}
