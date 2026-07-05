using System.Collections.Generic;

namespace Zaide.Models;

/// <summary>
/// Holds the current Source Control session state with static/demo data.
/// No real git operations are performed.
/// </summary>
public class SourceControlState
{
    public List<GitBranch> Branches { get; } = new();
    public List<FileChange> UnstagedChanges { get; } = new();
    public List<FileChange> StagedChanges { get; } = new();
    public string CommitMessageDraft { get; set; } = string.Empty;
    public GitBranch? CurrentBranch { get; set; }

    public SourceControlState()
    {
        // Populate with static/demo data
        Branches.Add(new GitBranch("master", isCurrent: true));
        Branches.Add(new GitBranch("feature/agent-ui"));
        Branches.Add(new GitBranch("fix/terminal-logging"));
        CurrentBranch = Branches[0];

        UnstagedChanges.Add(new FileChange("src/ViewModels/MainWindowViewModel.cs", GitChangeType.Modified));
        UnstagedChanges.Add(new FileChange("src/Views/EditorView.cs", GitChangeType.Modified));
        UnstagedChanges.Add(new FileChange("src/Models/Workspace.cs", GitChangeType.Modified));
        UnstagedChanges.Add(new FileChange("src/Models/GitBranch.cs", GitChangeType.Added));
        UnstagedChanges.Add(new FileChange("src/Models/FileChange.cs", GitChangeType.Added));

        StagedChanges.Add(new FileChange("src/Program.cs", GitChangeType.Modified, isStaged: true));
        StagedChanges.Add(new FileChange("src/ViewModels/SourceControlViewModel.cs", GitChangeType.Added, isStaged: true));
    }
}