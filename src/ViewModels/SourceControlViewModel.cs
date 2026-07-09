using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// ViewModel for the Source Control panel. Reads a truthful repository snapshot
/// from the read-only git seam (<see cref="IGitRepositoryService"/>) at construction;
/// it never seeds demo data. Commands update UI state but do not execute real git
/// operations (those are later milestones).
/// </summary>
public class SourceControlViewModel : ReactiveObject
{
    private readonly SourceControlState _state;
    private string _commitMessage = string.Empty;
    private GitBranch? _selectedBranch;

    public ObservableCollection<GitBranch> Branches { get; } = new();
    public ObservableCollection<FileChange> UnstagedChanges { get; } = new();
    public ObservableCollection<FileChange> StagedChanges { get; } = new();

    public string CommitMessage
    {
        get => _commitMessage;
        set => this.RaiseAndSetIfChanged(ref _commitMessage, value);
    }

    public GitBranch? SelectedBranch
    {
        get => _selectedBranch;
        set => this.RaiseAndSetIfChanged(ref _selectedBranch, value);
    }

    private string _currentBranchName = "master";
    public string CurrentBranchName
    {
        get => _currentBranchName;
        private set => this.RaiseAndSetIfChanged(ref _currentBranchName, value);
    }

    public int UnstagedCount => UnstagedChanges.Count;
    public int StagedCount => StagedChanges.Count;

    public ReactiveCommand<FileChange, Unit> StageFileCommand { get; }
    public ReactiveCommand<FileChange, Unit> UnstageFileCommand { get; }
    public ReactiveCommand<Unit, Unit> CommitCommand { get; }
    public ReactiveCommand<GitBranch, Unit> SelectBranchCommand { get; }

    public SourceControlViewModel(SourceControlState state, IGitRepositoryService gitRepositoryService, Workspace workspace)
    {
        _state = state;

        // Load a truthful snapshot from the git seam (the source of truth).
        // When no workspace is open or it is not inside a repository, the
        // collections stay empty — never seeded with demo data.
        var snapshot = ReadSnapshot(gitRepositoryService, workspace.WorkspacePath);
        if (snapshot != null)
        {
            _state.Snapshot = snapshot;

            foreach (var branch in snapshot.Branches)
                Branches.Add(branch);

            foreach (var change in snapshot.Changes)
            {
                if (change.IsStaged)
                    StagedChanges.Add(change);
                else
                    UnstagedChanges.Add(change);
            }

            _selectedBranch = snapshot.Branches.FirstOrDefault(b => b.IsCurrent);
            _currentBranchName = snapshot.CurrentBranchName;
        }
        else
        {
            _currentBranchName = "master";
        }

        _commitMessage = state.CommitMessageDraft;

        StageFileCommand = ReactiveCommand.Create<FileChange>(file =>
        {
            if (UnstagedChanges.Remove(file))
            {
                file.IsStaged = true;
                StagedChanges.Add(file);
                this.RaisePropertyChanged(nameof(UnstagedCount));
                this.RaisePropertyChanged(nameof(StagedCount));
            }
        });

        UnstageFileCommand = ReactiveCommand.Create<FileChange>(file =>
        {
            if (StagedChanges.Remove(file))
            {
                file.IsStaged = false;
                UnstagedChanges.Add(file);
                this.RaisePropertyChanged(nameof(UnstagedCount));
                this.RaisePropertyChanged(nameof(StagedCount));
            }
        });

        CommitCommand = ReactiveCommand.Create(() =>
        {
            // Visual-only: clear staged changes and commit message
            StagedChanges.Clear();
            CommitMessage = string.Empty;
            this.RaisePropertyChanged(nameof(StagedCount));
        });

        SelectBranchCommand = ReactiveCommand.Create<GitBranch>(branch =>
        {
            SelectedBranch = branch;
            CurrentBranchName = branch.Name;
        });
    }

    /// <summary>
    /// Discovers the repository from the workspace path and reads a truthful
    /// status snapshot. Returns null when no workspace is open or the path is not
    /// inside a git repository, so live consumers never fall back to fake data.
    /// </summary>
    private static RepositoryStatusSnapshot? ReadSnapshot(IGitRepositoryService git, string? workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath))
            return null;

        var discovery = git.Discover(workspacePath);
        if (!discovery.IsRepository || discovery.RepositoryRoot is null)
            return null;

        return git.ReadStatus(discovery.RepositoryRoot);
    }
}