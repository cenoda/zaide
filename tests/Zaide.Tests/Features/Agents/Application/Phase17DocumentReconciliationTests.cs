using System;
using System.IO;
using System.Threading;
using Xunit;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Editor.Application;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 17 M6 — document reconciliation after confirmed disk mutation.
/// </summary>
public sealed class Phase17DocumentReconciliationTests : IDisposable
{
    private readonly string _workspaceRoot;
    private readonly WorkspaceActionScope _scope;
    private readonly global::Zaide.Features.Workspace.Domain.Workspace _workspace;
    private readonly FakeWorkspaceActionAuthority _workspaceAuthority;
    private readonly TestEditorUiDispatcher _uiDispatcher;
    private readonly WorkspaceEditorDocumentReconciler _reconciler;

    public Phase17DocumentReconciliationTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "zaide-p17-reconcile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);
        _scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_workspaceRoot);
        _workspaceAuthority = new FakeWorkspaceActionAuthority(_scope);
        _workspace = new global::Zaide.Features.Workspace.Domain.Workspace();
        _workspace.SetProjectFromPath(_workspaceRoot);
        _uiDispatcher = new TestEditorUiDispatcher();
        _reconciler = new WorkspaceEditorDocumentReconciler(
            _workspace,
            _uiDispatcher,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkspaceEditorDocumentReconciler>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workspaceRoot))
            {
                Directory.Delete(_workspaceRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void CleanOpenDocument_ReloadsConfirmedDiskContent_AndRemainsClean()
    {
        const string original = "original";
        const string replacement = "replacement";
        var relativePath = "clean-reload.txt";
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, original);
        var document = _workspace.OpenDocument(absolutePath, original);
        Assert.False(document.IsDirty);

        File.WriteAllText(absolutePath, replacement);
        var mutation = AgentFileMutationResult.Success(
            AgentContentRevision.FromUtf8Text(replacement),
            replacement.Length,
            "Replace succeeded.");
        var proposal = CreateProposal(AgentFileProposalOperation.Replace, relativePath);

        var result = Reconcile(proposal, mutation);

        Assert.Equal(AgentDocumentReconciliationOutcome.ReloadedClean, result.Outcome);
        Assert.Equal(replacement, document.Content);
        Assert.False(document.IsDirty);
        Assert.False(document.IsDiskAbsent);
        Assert.True(_uiDispatcher.WasInvoked);
    }

    [Fact]
    public void DirtyOpenDocument_PreservesBuffer_AndReturnsExternalConflict()
    {
        const string diskContent = "disk";
        const string bufferContent = "dirty buffer";
        var relativePath = "dirty-conflict.txt";
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, diskContent);
        var document = _workspace.OpenDocument(absolutePath, "stale buffer");
        document.Content = bufferContent;
        Assert.True(document.IsDirty);

        File.WriteAllText(absolutePath, "new disk");
        var mutation = AgentFileMutationResult.Success(
            AgentContentRevision.FromUtf8Text("new disk"),
            "new disk".Length,
            "Replace succeeded.");
        var proposal = CreateProposal(AgentFileProposalOperation.Replace, relativePath);

        var result = Reconcile(proposal, mutation);

        Assert.Equal(AgentDocumentReconciliationOutcome.ExternalConflict, result.Outcome);
        Assert.Equal(bufferContent, document.Content);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void DeletedCleanDocument_SurfacesDiskDeletion_WithoutInventingContent()
    {
        const string original = "delete me";
        var relativePath = "deleted-clean.txt";
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, original);
        var document = _workspace.OpenDocument(absolutePath, original);
        Assert.False(document.IsDirty);
        File.Delete(absolutePath);

        var mutation = AgentFileMutationResult.DeleteSuccess("Deleted file.");
        var proposal = CreateProposal(AgentFileProposalOperation.Delete, relativePath);

        var result = Reconcile(proposal, mutation);

        Assert.Equal(AgentDocumentReconciliationOutcome.DiskDeletedClean, result.Outcome);
        Assert.Equal(original, document.Content);
        Assert.False(document.IsDirty);
        Assert.True(document.IsDiskAbsent);
    }

    [Fact]
    public void DeletedDirtyDocument_PreservesBuffer_AndFlagsDiskAbsence()
    {
        const string original = "dirty delete";
        var relativePath = "deleted-dirty.txt";
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, original);
        var document = _workspace.OpenDocument(absolutePath, original);
        document.Content = "unsaved edits";
        Assert.True(document.IsDirty);
        File.Delete(absolutePath);

        var mutation = AgentFileMutationResult.DeleteSuccess("Deleted file.");
        var proposal = CreateProposal(AgentFileProposalOperation.Delete, relativePath);

        var result = Reconcile(proposal, mutation);

        Assert.Equal(AgentDocumentReconciliationOutcome.DiskDeletedDirty, result.Outcome);
        Assert.Equal("unsaved edits", document.Content);
        Assert.True(document.IsDirty);
        Assert.True(document.IsDiskAbsent);
    }

    [Fact]
    public void UnopenedDocument_DoesNotOpenTab()
    {
        const string content = "never opened";
        var relativePath = "unopened.txt";
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, content);

        var mutation = AgentFileMutationResult.Success(
            AgentContentRevision.FromUtf8Text(content),
            content.Length,
            "Replace succeeded.");
        var proposal = CreateProposal(AgentFileProposalOperation.Replace, relativePath);

        var result = Reconcile(proposal, mutation);

        Assert.Equal(AgentDocumentReconciliationOutcome.NotApplicable, result.Outcome);
        Assert.Empty(_workspace.Documents);
    }

    [Fact]
    public void StaleWorkspaceGeneration_RejectsReconciliation()
    {
        const string content = "stale workspace";
        var relativePath = "stale-workspace.txt";
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, content);
        var document = _workspace.OpenDocument(absolutePath, content);
        _workspaceAuthority.IsStale = true;

        var mutation = AgentFileMutationResult.Success(
            AgentContentRevision.FromUtf8Text(content),
            content.Length,
            "Replace succeeded.");
        var proposal = CreateProposal(AgentFileProposalOperation.Replace, relativePath);

        var result = Reconcile(proposal, mutation);

        Assert.Equal(AgentDocumentReconciliationOutcome.StaleWorkspace, result.Outcome);
        Assert.Equal(content, document.Content);
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void ObserverFailure_Isolated_DoesNotThrowOrMutateBuffer()
    {
        const string bufferContent = "buffer";
        const string diskContent = "disk";
        var relativePath = "observer-failure.txt";
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, diskContent);
        var document = _workspace.OpenDocument(absolutePath, bufferContent);
        document.ReloadCleanContent(bufferContent);
        document.ContentChanged += (_, _) => throw new InvalidOperationException("observer failed");

        var mutation = AgentFileMutationResult.Success(
            AgentContentRevision.FromUtf8Text(diskContent),
            diskContent.Length,
            "Replace succeeded.");
        var proposal = CreateProposal(AgentFileProposalOperation.Replace, relativePath);

        var result = Reconcile(proposal, mutation);

        Assert.Equal(AgentDocumentReconciliationOutcome.ReloadedClean, result.Outcome);
        Assert.Equal(diskContent, document.Content);
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void Reconciliation_UsesUiDispatcher()
    {
        const string content = "ui dispatch";
        var relativePath = "ui-dispatch.txt";
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, content);
        _workspace.OpenDocument(absolutePath, content);
        _uiDispatcher.Reset();

        var mutation = AgentFileMutationResult.Success(
            AgentContentRevision.FromUtf8Text(content),
            content.Length,
            "Replace succeeded.");
        var proposal = CreateProposal(AgentFileProposalOperation.Replace, relativePath);

        Reconcile(proposal, mutation);

        Assert.True(_uiDispatcher.WasInvoked);
        Assert.True(_uiDispatcher.LastInvokeRanSynchronously);
    }

    [Fact]
    public void PostMutationRace_ReturnsWithoutReloadingCleanDocument()
    {
        const string original = "original";
        const string replacement = "replacement";
        const string raced = "raced";
        var relativePath = "post-mutation-race.txt";
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, original);
        var document = _workspace.OpenDocument(absolutePath, original);

        File.WriteAllText(absolutePath, replacement);
        var mutation = AgentFileMutationResult.Success(
            AgentContentRevision.FromUtf8Text(replacement),
            replacement.Length,
            "Replace succeeded.");
        File.WriteAllText(absolutePath, raced);
        var proposal = CreateProposal(AgentFileProposalOperation.Replace, relativePath);

        var result = Reconcile(proposal, mutation);

        Assert.Equal(AgentDocumentReconciliationOutcome.PostMutationRace, result.Outcome);
        Assert.Equal(original, document.Content);
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void FailedMutation_ReturnsNotApplicable()
    {
        var relativePath = "failed-mutation.txt";
        var absolutePath = Path.Combine(_workspaceRoot, relativePath);
        File.WriteAllText(absolutePath, "content");
        _workspace.OpenDocument(absolutePath, "content");

        var mutation = AgentFileMutationResult.Rejected(
            AgentFileMutationOutcome.Conflict,
            "Mutation conflict.");
        var proposal = CreateProposal(AgentFileProposalOperation.Replace, relativePath);

        var result = Reconcile(proposal, mutation);

        Assert.Equal(AgentDocumentReconciliationOutcome.NotApplicable, result.Outcome);
    }

    private AgentDocumentReconciliationResult Reconcile(
        AgentFileActionProposal proposal,
        AgentFileMutationResult mutation) =>
        _reconciler.ReconcileAfterMutation(
            _scope,
            _workspaceAuthority,
            proposal,
            mutation,
            CancellationToken.None);

    private AgentFileActionProposal CreateProposal(
        AgentFileProposalOperation operation,
        string relativePath)
    {
        var path = AgentWorkspaceRelativePath.Normalize(relativePath);
        var proposal = operation switch
        {
            AgentFileProposalOperation.Create => new AgentFileProposal(
                operation,
                path,
                baseExists: false,
                baseRevision: null,
                proposedRevision: AgentContentRevision.FromUtf8Text("placeholder"),
                boundedChangeSummary: "summary"),
            AgentFileProposalOperation.Replace => new AgentFileProposal(
                operation,
                path,
                baseExists: true,
                baseRevision: AgentContentRevision.FromUtf8Text("base"),
                proposedRevision: AgentContentRevision.FromUtf8Text("proposed"),
                boundedChangeSummary: "summary"),
            AgentFileProposalOperation.Delete => new AgentFileProposal(
                operation,
                path,
                baseExists: true,
                baseRevision: AgentContentRevision.FromUtf8Text("base"),
                proposedRevision: null,
                boundedChangeSummary: "summary"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        return new AgentFileActionProposal(
            AgentFileProposalId.New(),
            proposal,
            _scope,
            AgentActionRequestFingerprint.FromDigest(new string('a', 64)),
            proposal.BaseRevision);
    }

    private sealed class TestEditorUiDispatcher : IEditorUiDispatcher
    {
        public bool WasInvoked { get; private set; }

        public bool LastInvokeRanSynchronously { get; private set; }

        public void Reset()
        {
            WasInvoked = false;
            LastInvokeRanSynchronously = false;
        }

        public void Invoke(Action action)
        {
            WasInvoked = true;
            LastInvokeRanSynchronously = true;
            action();
        }

        public T Invoke<T>(Func<T> func)
        {
            WasInvoked = true;
            LastInvokeRanSynchronously = true;
            return func();
        }

        public void Post(Action action)
        {
            WasInvoked = true;
            LastInvokeRanSynchronously = true;
            action();
        }
    }
}
