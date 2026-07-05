using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using Zaide.Models;

namespace Zaide.ViewModels;

/// <summary>
/// ViewModel for the Source Control panel. Uses static/demo data only.
/// Commands update UI state but do not execute real git operations.
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

    public SourceControlViewModel(SourceControlState state)
    {
        _state = state;

        // Copy demo data into observable collections
        foreach (var branch in state.Branches)
            Branches.Add(branch);

        foreach (var change in state.UnstagedChanges)
            UnstagedChanges.Add(change);

        foreach (var change in state.StagedChanges)
            StagedChanges.Add(change);

        _selectedBranch = state.CurrentBranch;
        _currentBranchName = state.CurrentBranch?.Name ?? "master";
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
}