using Zaide.Features.SourceControl.Domain;

namespace Zaide.Features.SourceControl.Application;

/// <summary>
/// Derives the Source Control panel primary action from live repository
/// projection state. Push is only offered when the working tree is clean,
/// an upstream exists, and local commits are ahead of the remote.
/// </summary>
internal static class SourceControlActionDeriver
{
    /// <summary>
    /// Returns <see cref="SourceControlPrimaryAction.Push"/> only when
    /// <paramref name="hasRepository"/>, the tree is clean, an upstream
    /// is configured, and <paramref name="aheadBy"/> is greater than zero.
    /// Any staged or unstaged change forces <see cref="SourceControlPrimaryAction.Commit"/>.
    /// </summary>
    public static SourceControlPrimaryAction Derive(
        int unstagedCount,
        int stagedCount,
        int aheadBy,
        bool hasUpstream,
        bool hasRepository)
    {
        if (!hasRepository)
            return SourceControlPrimaryAction.Commit;

        if (unstagedCount > 0 || stagedCount > 0)
            return SourceControlPrimaryAction.Commit;

        if (hasUpstream && aheadBy > 0)
            return SourceControlPrimaryAction.Push;

        return SourceControlPrimaryAction.Commit;
    }
}