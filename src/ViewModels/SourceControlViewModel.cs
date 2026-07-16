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
/// It never seeds demo data. Stage/unstage/commit commands call the real mutation
/// seam (<see cref="IGitMutationService"/>) and unconditionally refresh afterward so
/// the UI returns to repo truth. The panel can request a fresh snapshot for the
/// current workspace via <see cref="RefreshCommand"/>.
/// </summary>
public class SourceControlViewModel : ReactiveObject
{
    private readonly ISourceControlSnapshotOrchestrator _orchestrator;
    private readonly ISourceControlDiffTabService _diffTabService;
    private readonly IGitMutationService _mutationService;
    private readonly IGitRepositoryService _gitRepositoryService;
    private readonly Workspace _workspace;
    private string _commitMessage = string.Empty;
    private string? _commitError;
    private string? _pushError;
    private string? _actionNotice;
    private SourceControlPrimaryAction _primaryAction = SourceControlPrimaryAction.Commit;
    private int _aheadBy;
    private bool _hasUpstream;
    private GitBranch? _selectedBranch;
    private FileChange? _selectedFileChange;
    private string? _selectedFilePath;
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

    /// <summary>
    /// Commit-specific error message. Set when a commit attempt fails (empty
    /// message, nothing staged, missing git identity, repo/IO error). Cleared
    /// on a successful commit and on a successful refresh. Distinct from
    /// <see cref="StatusMessage"/> (which covers refresh-state and
    /// stage/unstage notices) and from <see cref="LastRefreshError"/> (which
    /// is reserved for refresh failures only).
    /// </summary>
    public string? CommitError
    {
        get => _commitError;
        private set => this.RaiseAndSetIfChanged(ref _commitError, value);
    }

    /// <summary>
    /// Push-specific error message. Set when a push attempt fails. Cleared on a
    /// successful push and on a successful refresh. Distinct from
    /// <see cref="CommitError"/>.
    /// </summary>
    public string? PushError
    {
        get => _pushError;
        private set => this.RaiseAndSetIfChanged(ref _pushError, value);
    }

    /// <summary>
    /// Brief success notice for the latest primary action (e.g. push completed).
    /// Cleared when the user retries commit/push or a new error is surfaced.
    /// </summary>
    public string? ActionNotice
    {
        get => _actionNotice;
        private set => this.RaiseAndSetIfChanged(ref _actionNotice, value);
    }

    /// <summary>
    /// Primary action derived from working-tree cleanliness and ahead/upstream
    /// status. Never relies on a cached post-commit flag.
    /// </summary>
    public SourceControlPrimaryAction PrimaryAction
    {
        get => _primaryAction;
        private set
        {
            var changed = !EqualityComparer<SourceControlPrimaryAction>.Default.Equals(_primaryAction, value);
            this.RaiseAndSetIfChanged(ref _primaryAction, value);
            if (changed)
                this.RaisePropertyChanged(nameof(PrimaryActionLabel));
        }
    }

    /// <summary>User-facing label for the primary action button.</summary>
    public string PrimaryActionLabel =>
        PrimaryAction == SourceControlPrimaryAction.Push
            ? $"Push ({AheadBy})"
            : "Commit";

    /// <summary>Local commits ahead of the tracked upstream branch.</summary>
    public int AheadBy
    {
        get => _aheadBy;
        private set
        {
            var changed = _aheadBy != value;
            this.RaiseAndSetIfChanged(ref _aheadBy, value);
            if (changed)
                this.RaisePropertyChanged(nameof(PrimaryActionLabel));
        }
    }

    /// <summary>Whether the current branch tracks an upstream remote branch.</summary>
    public bool HasUpstream
    {
        get => _hasUpstream;
        private set => this.RaiseAndSetIfChanged(ref _hasUpstream, value);
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

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<FileChange, Unit> StageFileCommand { get; }
    public ReactiveCommand<Unit, Unit> StageAllCommand { get; }
    public ReactiveCommand<FileChange, Unit> UnstageFileCommand { get; }
    public ReactiveCommand<Unit, Unit> CommitCommand { get; }
    public ReactiveCommand<Unit, Unit> PushCommand { get; }
    public ReactiveCommand<Unit, Unit> PrimaryActionCommand { get; }
    public ReactiveCommand<GitBranch, Unit> SelectBranchCommand { get; }
    public ReactiveCommand<FileChange, Unit> SelectFileCommand { get; }

    public SourceControlViewModel(
        ISourceControlSnapshotOrchestrator orchestrator,
        Workspace workspace,
        IGitMutationService mutationService,
        IGitRepositoryService gitRepositoryService,
        ISourceControlDiffTabService? diffTabService = null,
        ICommandRegistry? commandRegistry = null)
    {
        _orchestrator = orchestrator;
        _workspace = workspace;
        _diffTabService = diffTabService ?? NullSourceControlDiffTabService.Instance;
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

        // Disabled when there are no unstaged files. ReactiveCommand also
        // disables while executing so a second click cannot re-enter.
        var canStageAll = this.WhenAnyValue(x => x.UnstagedCount, count => count > 0);
        StageAllCommand = ReactiveCommand.CreateFromTask(ExecuteStageAllAsync, canStageAll);

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

        CommitCommand = ReactiveCommand.CreateFromTask(ExecuteCommitAsync);

        PushCommand = ReactiveCommand.CreateFromTask(ExecutePushAsync);

        PrimaryActionCommand = ReactiveCommand.CreateFromTask(ExecutePrimaryActionAsync);

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
                return;

            _diffTabService.OpenOrUpdateDiff(file);
        });

        // Phase 8.2 M8a: register the canonical source-control commands with stable
        // IDs (D6a) after the ReactiveCommand properties above are initialized.
        commandRegistry?.Register(new CommandDescriptor(
            "sourcecontrol.commit", "Commit", "Source Control", Array.Empty<string>(), CommitCommand));
        commandRegistry?.Register(new CommandDescriptor(
            "sourcecontrol.refresh", "Refresh", "Source Control", Array.Empty<string>(), RefreshCommand));
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
            if (previouslySelectedPath != null)
                _diffTabService.RefreshOpenDiff(previouslySelectedPath, change: null);
            StatusMessage = result.Status == SnapshotRefreshStatus.Failed
                ? $"Source Control unavailable: {result.ErrorMessage ?? "unknown error"}"
                : "No repository — open a folder inside a git repository";
            _currentBranchName = result.Status == SnapshotRefreshStatus.Failed
                ? "—"
                : "no repo";
            AheadBy = 0;
            HasUpstream = false;
            UpdatePrimaryAction(hasRepository: false);
            this.RaisePropertyChanged(nameof(UnstagedCount));
            this.RaisePropertyChanged(nameof(StagedCount));
            this.RaisePropertyChanged(nameof(SelectedBranch));
            this.RaisePropertyChanged(nameof(CurrentBranchName));
            this.RaisePropertyChanged(nameof(StatusMessage));
            return;
        }

        StatusMessage = null;
        CommitError = null;
        PushError = null;

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

        // Resolve from the live Branches collection so the ComboBox can match
        // SelectedItem by reference after each refresh repopulates the list.
        _selectedBranch = Branches.FirstOrDefault(b => b.IsCurrent)
            ?? Branches.FirstOrDefault(b => b.Name == result.Snapshot.CurrentBranchName);
        _currentBranchName = result.Snapshot.CurrentBranchName;
        AheadBy = result.Snapshot.AheadBy;
        HasUpstream = result.Snapshot.HasUpstream;
        this.RaisePropertyChanged(nameof(UnstagedCount));
        this.RaisePropertyChanged(nameof(StagedCount));
        this.RaisePropertyChanged(nameof(SelectedBranch));
        this.RaisePropertyChanged(nameof(CurrentBranchName));
        UpdatePrimaryAction(hasRepository: true);

        // Re-select file by path across refresh.
        if (previouslySelectedPath != null)
        {
            var match = UnstagedChanges.Concat(StagedChanges)
                .FirstOrDefault(c => c.FilePath == previouslySelectedPath);
            if (match != null)
            {
                SelectedFileChange = match;
                SelectedFilePath = match.FilePath;
                _diffTabService.RefreshOpenDiff(match.FilePath, match);
            }
            else
            {
                SelectedFileChange = null;
                SelectedFilePath = null;
                _diffTabService.RefreshOpenDiff(previouslySelectedPath, change: null);
            }
        }
    }

    private async Task ExecuteStageAllAsync()
    {
        var discovery = _gitRepositoryService.Discover(_workspace.WorkspacePath ?? string.Empty);
        if (!discovery.IsRepository || discovery.RepositoryRoot is null)
        {
            StatusMessage = "No repository - open a folder inside a git repository";
            return;
        }

        // Snapshot paths before the mutation so a concurrent refresh cannot
        // shrink the collection mid-iteration.
        var paths = UnstagedChanges.Select(c => c.FilePath).ToList();
        if (paths.Count == 0)
            return;

        var repoRoot = discovery.RepositoryRoot;
        var result = await Task.Run(() => _mutationService.StageAll(repoRoot, paths));

        // Always refresh from repo truth — partial stage may have succeeded
        // before a failure, and the UI must not assume every path staged.
        RefreshCommand.Execute().Subscribe();

        if (!result.IsSuccess)
        {
            StatusMessage = result.ErrorMessage;
        }
    }

    private async Task ExecutePrimaryActionAsync()
    {
        ActionNotice = null;
        var action = PrimaryAction;
        if (action == SourceControlPrimaryAction.Push)
        {
            if (UnstagedCount > 0 || StagedCount > 0)
                return;

            await ExecutePushAsync();
            return;
        }

        await ExecuteCommitAsync();
    }

    private async Task ExecuteCommitAsync()
    {
        // Empty-message guard: no service call, no repository access.
        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            CommitError = "Commit message cannot be empty.";
            return;
        }

        var discovery = _gitRepositoryService.Discover(_workspace.WorkspacePath ?? string.Empty);
        if (!discovery.IsRepository || discovery.RepositoryRoot is null)
        {
            CommitError = "No repository — open a folder inside a git repository";
            return;
        }

        // Nothing-staged guard: no service call. The service also checks
        // this (using repo truth), but the VM guard avoids the cost of
        // opening the repository when the staged list is visibly empty.
        if (StagedChanges.Count == 0)
        {
            CommitError = "Nothing staged to commit.";
            return;
        }

        var repoRoot = discovery.RepositoryRoot;
        var message = CommitMessage;
        var result = await Task.Run(() => _mutationService.Commit(repoRoot, message));

        // Refresh unconditionally so the post-commit state is truthful.
        RefreshCommand.Execute().Subscribe();

        if (result.IsSuccess)
        {
            CommitMessage = string.Empty;
            CommitError = null;
        }
        else
        {
            CommitError = result.ErrorMessage;
            // Do NOT set StatusMessage here — StatusMessage is reserved for
            // refresh-state notices (non-repo, failure). CommitError has its
            // own dedicated surface in SourceControlPanel.
            // Do NOT set LastRefreshError/LastRefreshStatus — those are for
            // refresh failures only.
        }
    }

    private async Task ExecutePushAsync()
    {
        if (UnstagedCount > 0 || StagedCount > 0)
        {
            ActionNotice = null;
            PushError = "Cannot push with uncommitted changes.";
            return;
        }

        var discovery = _gitRepositoryService.Discover(_workspace.WorkspacePath ?? string.Empty);
        if (!discovery.IsRepository || discovery.RepositoryRoot is null)
        {
            ActionNotice = null;
            PushError = "No repository — open a folder inside a git repository";
            return;
        }

        var branchName = CurrentBranchName;
        var repoRoot = discovery.RepositoryRoot;
        var result = await Task.Run(() => _mutationService.Push(repoRoot));

        if (result.IsSuccess)
        {
            ApplyResult(_orchestrator.Refresh(_workspace.WorkspacePath));
            PushError = null;
            ActionNotice = string.IsNullOrEmpty(branchName)
                ? "Push completed."
                : $"Pushed {branchName}.";
        }
        else
        {
            RefreshCommand.Execute().Subscribe();
            ActionNotice = null;
            PushError = result.ErrorMessage;
        }
    }

    private void UpdatePrimaryAction(bool hasRepository)
    {
        PrimaryAction = SourceControlActionDeriver.Derive(
            UnstagedCount,
            StagedCount,
            AheadBy,
            HasUpstream,
            hasRepository);
    }
}
