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
/// ViewModel for the Source Control panel. Requests truthful repository snapshots
/// from the read-only refresh seam (<see cref="ISourceControlSnapshotOrchestrator"/>);
/// it never seeds demo data. Commands update UI state but do not execute real git
/// operations (those are later milestones). The panel can request a fresh snapshot
/// for the current workspace via <see cref="RefreshCommand"/>.
/// </summary>
public class SourceControlViewModel : ReactiveObject
{
    private readonly SourceControlState _state;
    private readonly ISourceControlSnapshotOrchestrator _orchestrator;
    private readonly Workspace _workspace;
    private string _commitMessage = string.Empty;
    private GitBranch? _selectedBranch;
    private string _currentBranchName = "master";
    private SnapshotRefreshStatus _lastRefreshStatus = SnapshotRefreshStatus.NotARepository;
    private string? _lastRefreshError;

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

    public string CurrentBranchName
    {
        get => _currentBranchName;
        private set => this.RaiseAndSetIfChanged(ref _currentBranchName, value);
    }

    /// <summary>The projected outcome of the most recent snapshot refresh request.</summary>
    public SnapshotRefreshStatus LastRefreshStatus
    {
        get => _lastRefreshStatus;
        private set => this.RaiseAndSetIfChanged(ref _lastRefreshStatus, value);
    }

    /// <summary>Human-readable error from the most recent refresh, when it failed.</summary>
    public string? LastRefreshError
    {
        get => _lastRefreshError;
        private set => this.RaiseAndSetIfChanged(ref _lastRefreshError, value);
    }

    public int UnstagedCount => UnstagedChanges.Count;
    public int StagedCount => StagedChanges.Count;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<FileChange, Unit> StageFileCommand { get; }
    public ReactiveCommand<FileChange, Unit> UnstageFileCommand { get; }
    public ReactiveCommand<Unit, Unit> CommitCommand { get; }
    public ReactiveCommand<GitBranch, Unit> SelectBranchCommand { get; }

    public SourceControlViewModel(SourceControlState state, ISourceControlSnapshotOrchestrator orchestrator, Workspace workspace)
    {
        _state = state;
        _orchestrator = orchestrator;
        _workspace = workspace;

        // Load a truthful snapshot from the refresh seam (the source of truth).
        // When no workspace is open or it is not inside a repository, the
        // collections stay empty — never seeded with demo data.
        ApplyResult(_orchestrator.Refresh(workspace.WorkspacePath));

        _commitMessage = state.CommitMessageDraft;

        RefreshCommand = ReactiveCommand.Create(() => ApplyResult(_orchestrator.Refresh(_workspace.WorkspacePath)));

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

    private void ApplyResult(SnapshotRefreshResult result)
    {
        LastRefreshStatus = result.Status;
        LastRefreshError = result.ErrorMessage;

        // Truthful empty/disabled state for any non-success: no fake data is
        // projected. The git read seam owns the truth; on non-repo or failure we
        // surface only the status/error and clear any prior projection.
        if (result.Status != SnapshotRefreshStatus.Success || result.Snapshot is null)
        {
            Branches.Clear();
            UnstagedChanges.Clear();
            StagedChanges.Clear();
            _state.Snapshot = null;
            _selectedBranch = null;
            _currentBranchName = "master";
            this.RaisePropertyChanged(nameof(UnstagedCount));
            this.RaisePropertyChanged(nameof(StagedCount));
            this.RaisePropertyChanged(nameof(SelectedBranch));
            this.RaisePropertyChanged(nameof(CurrentBranchName));
            return;
        }

        _state.Snapshot = result.Snapshot;

        Branches.Clear();
        UnstagedChanges.Clear();
        StagedChanges.Clear();

        foreach (var branch in result.Snapshot.Branches)
            Branches.Add(branch);

        foreach (var change in result.Snapshot.Changes)
        {
            if (change.IsStaged)
                StagedChanges.Add(change);
            else
                UnstagedChanges.Add(change);
        }

        _selectedBranch = result.Snapshot.Branches.FirstOrDefault(b => b.IsCurrent);
        _currentBranchName = result.Snapshot.CurrentBranchName;
        this.RaisePropertyChanged(nameof(UnstagedCount));
        this.RaisePropertyChanged(nameof(StagedCount));
        this.RaisePropertyChanged(nameof(SelectedBranch));
        this.RaisePropertyChanged(nameof(CurrentBranchName));
    }
}
