namespace Zaide.Models;

/// <summary>
/// Represents a git branch with a display name and current-branch flag.
/// Projected from live repository state by the read-only git seam.
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