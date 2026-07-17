namespace Zaide.Features.SourceControl.Domain;

/// <summary>
/// Primary action shown on the Source Control panel commit/push button.
/// Derived from working-tree cleanliness and upstream ahead status.
/// </summary>
public enum SourceControlPrimaryAction
{
    Commit,
    Push,
}