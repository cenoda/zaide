using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Zaide.Models;
using Zaide.Services;

namespace Zaide.ViewModels;

/// <summary>
/// ViewModel for the Source Control panel. Requests truthful repository snapshots
/// from the read-only refresh seam (<see cref="ISourceControlSnapshotOrchestrator"/>).
/// It never seeds demo data. Stage/unstage commands call the real mutation seam
/// (<see cref="IGitMutationService"/>) and unconditionally refresh afterward so the
/// UI returns to repo truth. Commit is still a visual-only placeholder (later
/// milestone). The panel can request a fresh snapshot for the current workspace
/// via <see cref="RefreshCommand"/>.
/// </summary>
public class SourceControlViewModel : ReactiveObject
{
    private readonly ISourceControlSnapshotOrchestrator _orchestrator;
    private readonly IFileDiffService _fileDiffService;
    private readonly IGitMutationService _mutationService;
    private readonly IGitRepositoryService _gitRepositoryService;
    private readonly Workspace _workspace;
    private string _commitMessage = string.Empty;
    private GitBranch? _selectedBranch;
    private FileChange? _selectedFileChange;
    private string? _selectedFilePath;
    private FileDiffResult? _currentDiff;
    private string _currentBranchName = "no repo";
    private string? _statusMessage;
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

    /// <summary>
    /// Surface-level message for the Source Control panel. Null on a successful
    /// refresh (the change lists and branch selector are the truth). Set to a
    /// human-readable notice on non-repo or failure so the user sees why the
    /// lists are empty.
    /// </summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public int UnstagedCount => UnstagedChanges.Count;
    public int StagedCount => StagedChanges.Count;

    public FileChange? SelectedFileChange
    {
        get => _selectedFileChange;
        set => this.RaiseAndSetIfChanged(ref _selectedFileChange, value);
    }

    public string? SelectedFilePath
    {
        get => _selectedFilePath;
        set => this.RaiseAndSetIfChanged(ref _selectedFilePath, value);
    }

    public FileDiffResult? CurrentDiff
    {
        get => _currentDiff;
        private set => this.RaiseAndSetIfChanged(ref _currentDiff, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<FileChange, Unit> StageFileCommand { get; }
    public ReactiveCommand<FileChange, Unit> UnstageFileCommand { get; }
    public ReactiveCommand<Unit, Unit> CommitCommand { get; }
    public ReactiveCommand<GitBranch, Unit> SelectBranchCommand { get; }
    public ReactiveCommand<FileChange, Unit> SelectFileCommand { get; }

    public SourceControlViewModel(
        ISourceControlSnapshotOrchestrator orchestrator,
        Workspace workspace,
        IFileDiffService fileDiffService,
        IGitMutationService mutationService,
        IGitRepositoryService gitRepositoryService)
    {
        _orchestrator = orchestrator;
        _workspace = workspace;
        _fileDiffService = fileDiffService;
        _mutationService = mutationService;
        _gitRepositoryService = gitRepositoryService;

        // Load a truthful snapshot from the refresh seam (the source of truth).
        // When no workspace is open or it is not inside a repository, the
        // collections stay empty — never seeded with demo data.
        ApplyResult(_orchestrator.Refresh(workspace.WorkspacePath));

        RefreshCommand = ReactiveCommand.Create(() => ApplyResult(_orchestrator.Refresh(_workspace.WorkspacePath)));

        StageFileCommand = ReactiveCommand.CreateFromTask<FileChange>(async file =>
        {
            var discovery = _gitRepositoryService.Discover(_workspace.WorkspacePath ?? string.Empty);
            if (!discovery.IsRepository || discovery.RepositoryRoot is null)
            {
                StatusMessage = "No repository - open a folder inside a git repository";
                return;
            }

            var repoRoot = discovery.RepositoryRoot;
            var result = await Task.Run(() => _mutationService.Stage(repoRoot, file.FilePath));

            // Refresh unconditionally so the UI returns to repo truth even when
            // the mutation failed (e.g. file removed externally).
            RefreshCommand.Execute().Subscribe();

            if (!result.IsSuccess)
            {
                StatusMessage = result.ErrorMessage;
            }
        });

        UnstageFileCommand = ReactiveCommand.CreateFromTask<FileChange>(async file =>
        {
            var discovery = _gitRepositoryService.Discover(_workspace.WorkspacePath ?? string.Empty);
            if (!discovery.IsRepository || discovery.RepositoryRoot is null)
            {
                StatusMessage = "No repository - open a folder inside a git repository";
                return;
            }

            var repoRoot = discovery.RepositoryRoot;
            var result = await Task.Run(() => _mutationService.Unstage(repoRoot, file.FilePath));

            // Refresh unconditionally so the UI returns to repo truth even when
            // the mutation failed (e.g. file removed externally).
            RefreshCommand.Execute().Subscribe();

            if (!result.IsSuccess)
            {
                StatusMessage = result.ErrorMessage;
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

        SelectFileCommand = ReactiveCommand.Create<FileChange>(file =>
        {
            SelectedFileChange = file;
            SelectedFilePath = file?.FilePath;
            if (file == null || string.IsNullOrEmpty(_workspace.WorkspacePath))
            {
                CurrentDiff = null;
                return;
            }

            CurrentDiff = _fileDiffService.GetDiff(_workspace.WorkspacePath, file);
        });
    }

    private void ApplyResult(SnapshotRefreshResult result)
    {
        LastRefreshStatus = result.Status;
        LastRefreshError = result.ErrorMessage;

        // Preserve the previously selected file path before clearing collections.
        string? previouslySelectedPath = _selectedFilePath;

        // Truthful empty/disabled state for any non-success: no fake data is
        // projected. The git read seam owns the truth; on non-repo or failure we
        // surface only the status/error and clear any prior projection.
        if (result.Status != SnapshotRefreshStatus.Success || result.Snapshot is null)
        {
            Branches.Clear();
            UnstagedChanges.Clear();
            StagedChanges.Clear();
            _selectedBranch = null;
            SelectedFileChange = null;
            SelectedFilePath = null;
            CurrentDiff = null;
            StatusMessage = result.Status == SnapshotRefreshStatus.Failed
                ? $"Source Control unavailable: {result.ErrorMessage ?? "unknown error"}"
                : "No repository — open a folder inside a git repository";
            _currentBranchName = result.Status == SnapshotRefreshStatus.Failed
                ? "—"
                : "no repo";
            this.RaisePropertyChanged(nameof(UnstagedCount));
            this.RaisePropertyChanged(nameof(StagedCount));
            this.RaisePropertyChanged(nameof(SelectedBranch));
            this.RaisePropertyChanged(nameof(CurrentBranchName));
            this.RaisePropertyChanged(nameof(StatusMessage));
            return;
        }

        StatusMessage = null;

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

        // Re-select file by path across refresh.
        if (previouslySelectedPath != null)
        {
            var match = UnstagedChanges.Concat(StagedChanges)
                .FirstOrDefault(c => c.FilePath == previouslySelectedPath);
            if (match != null)
            {
                SelectedFileChange = match;
                SelectedFilePath = match.FilePath;
                if (!string.IsNullOrEmpty(_workspace.WorkspacePath))
                {
                    CurrentDiff = _fileDiffService.GetDiff(_workspace.WorkspacePath, match);
                }
            }
            else
            {
                SelectedFileChange = null;
                SelectedFilePath = null;
                CurrentDiff = null;
            }
        }
    }
}
