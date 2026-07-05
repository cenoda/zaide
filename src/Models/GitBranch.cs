namespace Zaide.Models;

/// <summary>
/// Represents a git branch with a display name and current-branch flag.
/// Used for static/demo data in the Source Control panel — no real git.
/// </summary>
public class GitBranch
{
    public string Name { get; }
    public bool IsCurrent { get; }

    public GitBranch(string name, bool isCurrent = false)
    {
        Name = name;
        IsCurrent = isCurrent;
    }
}