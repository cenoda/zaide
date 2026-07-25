using System;
using System.Threading;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

/// <summary>
/// Phase 17 M4 — immutable create/replace/delete file proposals with bounded
/// diff/summary presentation, stale-base detection, and explicit accept/deny flow.
/// </summary>
public sealed class Phase17ProposalTests
{
    private readonly string _workspaceRoot;
    private readonly WorkspaceActionScope _scope;
    private readonly WorkspaceFileReader _fileReader;

    public Phase17ProposalTests()
    {
        _workspaceRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "zaide-p17-proposal-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_workspaceRoot);

        _scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_workspaceRoot);
        _fileReader = new WorkspaceFileReader();
    }

    [Fact]
    public void AgentFileProposalId_New_CreatesUniqueIds()
    {
        var id1 = AgentFileProposalId.New();
        var id2 = AgentFileProposalId.New();

        Assert.NotEqual(id1, id2);
        Assert.NotEqual(id1.Value, id2.Value);
        Assert.Equal(32, id1.Value.Length); // 16 bytes = 32 hex chars
    }

    [Fact]
    public void AgentFileProposalId_FromValue_ValidatesFormat()
    {
        // Valid 32-character lowercase hex
        var validId = AgentFileProposalId.FromValue(new string('a', 32));
        Assert.Equal(new string('a', 32), validId.Value);

        // Too short
        var exception = Assert.Throws<ArgumentException>(() =>
            AgentFileProposalId.FromValue("abc"));
        Assert.Contains("32 lowercase hexadecimal characters", exception.Message);

        // Too long
        exception = Assert.Throws<ArgumentException>(() =>
            AgentFileProposalId.FromValue(new string('a', 33)));
        Assert.Contains("32 lowercase hexadecimal characters", exception.Message);

        // Uppercase
        exception = Assert.Throws<ArgumentException>(() =>
            AgentFileProposalId.FromValue("ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEF"));
        Assert.Contains("lowercase hexadecimal characters", exception.Message);

        // Empty
        exception = Assert.Throws<ArgumentException>(() =>
            AgentFileProposalId.FromValue(""));
        Assert.Contains("required", exception.Message);
    }

    [Fact]
    public void AgentFileProposal_ValidatesCreateRevisionRules()
    {
        var path = AgentWorkspaceRelativePath.Normalize("new.txt");
        var proposedRevision = AgentContentRevision.FromUtf8Text("hello");

        // Valid create proposal
        var validProposal = new AgentFileProposal(
            AgentFileProposalOperation.Create,
            path,
            baseExists: false,
            baseRevision: null,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "create new.txt");

        Assert.Equal(AgentFileProposalOperation.Create, validProposal.Operation);
        Assert.Equal(path, validProposal.Path);
        Assert.False(validProposal.BaseExists);
        Assert.Null(validProposal.BaseRevision);
        Assert.Equal(proposedRevision, validProposal.ProposedRevision);

        // Create with baseExists = true should fail
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentFileProposal(
                AgentFileProposalOperation.Create,
                path,
                baseExists: true,
                baseRevision: null,
                proposedRevision: proposedRevision,
                boundedChangeSummary: "create"));
        Assert.Contains("missing base file", exception.Message);

        // Create with baseRevision should fail
        exception = Assert.Throws<ArgumentException>(() =>
            new AgentFileProposal(
                AgentFileProposalOperation.Create,
                path,
                baseExists: false,
                baseRevision: AgentContentRevision.FromUtf8Text("existing"),
                proposedRevision: proposedRevision,
                boundedChangeSummary: "create"));
        Assert.Contains("cannot include a base revision", exception.Message);

        // Create without proposedRevision should fail
        exception = Assert.Throws<ArgumentException>(() =>
            new AgentFileProposal(
                AgentFileProposalOperation.Create,
                path,
                baseExists: false,
                baseRevision: null,
                proposedRevision: null,
                boundedChangeSummary: "create"));
        Assert.Contains("require a proposed revision", exception.Message);
    }

    [Fact]
    public void AgentFileProposal_ValidatesReplaceRevisionRules()
    {
        var path = AgentWorkspaceRelativePath.Normalize("existing.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("before");
        var proposedRevision = AgentContentRevision.FromUtf8Text("after");

        // Valid replace proposal
        var validProposal = new AgentFileProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: baseRevision,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "replace existing.txt");

        Assert.Equal(AgentFileProposalOperation.Replace, validProposal.Operation);
        Assert.True(validProposal.BaseExists);
        Assert.Equal(baseRevision, validProposal.BaseRevision);
        Assert.Equal(proposedRevision, validProposal.ProposedRevision);

        // Replace with baseExists = false should fail
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentFileProposal(
                AgentFileProposalOperation.Replace,
                path,
                baseExists: false,
                baseRevision: baseRevision,
                proposedRevision: proposedRevision,
                boundedChangeSummary: "replace"));
        Assert.Contains("require an existing base file", exception.Message);

        // Replace without baseRevision should fail
        exception = Assert.Throws<ArgumentException>(() =>
            new AgentFileProposal(
                AgentFileProposalOperation.Replace,
                path,
                baseExists: true,
                baseRevision: null,
                proposedRevision: proposedRevision,
                boundedChangeSummary: "replace"));
        Assert.Contains("require base and proposed revisions", exception.Message);

        // Replace without proposedRevision should fail
        exception = Assert.Throws<ArgumentException>(() =>
            new AgentFileProposal(
                AgentFileProposalOperation.Replace,
                path,
                baseExists: true,
                baseRevision: baseRevision,
                proposedRevision: null,
                boundedChangeSummary: "replace"));
        Assert.Contains("require base and proposed revisions", exception.Message);
    }

    [Fact]
    public void AgentFileProposal_ValidatesDeleteRevisionRules()
    {
        var path = AgentWorkspaceRelativePath.Normalize("existing.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("existing");

        // Valid delete proposal
        var validProposal = new AgentFileProposal(
            AgentFileProposalOperation.Delete,
            path,
            baseExists: true,
            baseRevision: baseRevision,
            proposedRevision: null,
            boundedChangeSummary: "delete existing.txt");

        Assert.Equal(AgentFileProposalOperation.Delete, validProposal.Operation);
        Assert.True(validProposal.BaseExists);
        Assert.Equal(baseRevision, validProposal.BaseRevision);
        Assert.Null(validProposal.ProposedRevision);

        // Delete with baseExists = false should fail
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentFileProposal(
                AgentFileProposalOperation.Delete,
                path,
                baseExists: false,
                baseRevision: null,
                proposedRevision: null,
                boundedChangeSummary: "delete"));
        Assert.Contains("require an existing base file", exception.Message);

        // Delete without baseRevision should fail
        exception = Assert.Throws<ArgumentException>(() =>
            new AgentFileProposal(
                AgentFileProposalOperation.Delete,
                path,
                baseExists: true,
                baseRevision: null,
                proposedRevision: null,
                boundedChangeSummary: "delete"));
        Assert.Contains("require a base revision", exception.Message);

        // Delete with proposedRevision should fail
        exception = Assert.Throws<ArgumentException>(() =>
            new AgentFileProposal(
                AgentFileProposalOperation.Delete,
                path,
                baseExists: true,
                baseRevision: baseRevision,
                proposedRevision: AgentContentRevision.FromUtf8Text("new"),
                boundedChangeSummary: "delete"));
        Assert.Contains("cannot include a proposed revision", exception.Message);
    }

    [Fact]
    public void AgentFileActionProposal_BindsComponentsImmutably()
    {
        var proposalId = AgentFileProposalId.New();
        var path = AgentWorkspaceRelativePath.Normalize("test.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("before");
        var proposedRevision = AgentContentRevision.FromUtf8Text("after");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: baseRevision,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "replace test.txt");

        var actionProposal = new AgentFileActionProposal(
            proposalId,
            proposal,
            _scope,
            fingerprint,
            baseRevision);

        // Verify all components are accessible
        Assert.Equal(proposalId, actionProposal.ProposalId);
        Assert.Equal(AgentFileProposalOperation.Replace, actionProposal.Operation);
        Assert.Equal(path, actionProposal.Path);
        Assert.True(actionProposal.BaseExists);
        Assert.Equal(baseRevision, actionProposal.BaseRevision);
        Assert.Equal(proposedRevision, actionProposal.ProposedRevision);
        Assert.Equal("replace test.txt", actionProposal.BoundedChangeSummary);
        Assert.Equal(_scope, actionProposal.WorkspaceScope);
        Assert.Equal(fingerprint, actionProposal.PermissionFingerprint);
        Assert.Equal(baseRevision, actionProposal.PermissionFingerprintBaseRevision);
    }

    [Fact]
    public void AgentFileActionProposal_DetectsStaleBaseForCreate()
    {
        var proposalId = AgentFileProposalId.New();
        var path = AgentWorkspaceRelativePath.Normalize("new.txt");
        var proposedRevision = AgentContentRevision.FromUtf8Text("content");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Create,
            path,
            baseExists: false,
            baseRevision: null,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "create new.txt");

        var actionProposal = new AgentFileActionProposal(
            proposalId,
            proposal,
            _scope,
            fingerprint,
            null);

        // For create: if current base exists (not null), then it's stale
        Assert.True(actionProposal.IsBaseStale(AgentContentRevision.FromUtf8Text("some-content")));
        Assert.False(actionProposal.IsBaseStale(null));
    }

    [Fact]
    public void AgentFileActionProposal_DetectsStaleBaseForReplace()
    {
        var proposalId = AgentFileProposalId.New();
        var path = AgentWorkspaceRelativePath.Normalize("existing.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("original");
        var proposedRevision = AgentContentRevision.FromUtf8Text("modified");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: baseRevision,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "replace existing.txt");

        var actionProposal = new AgentFileActionProposal(
            proposalId,
            proposal,
            _scope,
            fingerprint,
            baseRevision);

        // Same revision - not stale
        Assert.False(actionProposal.IsBaseStale(baseRevision));

        // Different revision - stale
        var differentRevision = AgentContentRevision.FromUtf8Text("changed");
        Assert.True(actionProposal.IsBaseStale(differentRevision));
    }

    [Fact]
    public void AgentFileActionProposal_DetectsStaleBaseForDelete()
    {
        var proposalId = AgentFileProposalId.New();
        var path = AgentWorkspaceRelativePath.Normalize("existing.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("original");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Delete,
            path,
            baseExists: true,
            baseRevision: baseRevision,
            proposedRevision: null,
            boundedChangeSummary: "delete existing.txt");

        var actionProposal = new AgentFileActionProposal(
            proposalId,
            proposal,
            _scope,
            fingerprint,
            baseRevision);

        // Same revision - not stale
        Assert.False(actionProposal.IsBaseStale(baseRevision));

        // Different revision - stale
        var differentRevision = AgentContentRevision.FromUtf8Text("changed");
        Assert.True(actionProposal.IsBaseStale(differentRevision));
    }

    [Fact]
    public void AgentFileActionProposal_PermissionFingerprintMatchesBase()
    {
        var proposalId = AgentFileProposalId.New();
        var path = AgentWorkspaceRelativePath.Normalize("test.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("content");
        var proposedRevision = AgentContentRevision.FromUtf8Text("new-content");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: baseRevision,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "replace test.txt");

        // Matching base revision
        var matchingActionProposal = new AgentFileActionProposal(
            proposalId,
            proposal,
            _scope,
            fingerprint,
            baseRevision);
        Assert.True(matchingActionProposal.PermissionFingerprintMatchesBase());

        // Non-matching base revision
        var nonMatchingActionProposal = new AgentFileActionProposal(
            proposalId,
            proposal,
            _scope,
            fingerprint,
            AgentContentRevision.FromUtf8Text("different"));
        Assert.False(nonMatchingActionProposal.PermissionFingerprintMatchesBase());
    }

    [Fact]
    public void AgentFileProposalGenerator_CreateProposal_ValidatesProposedContentBudget()
    {
        var path = AgentWorkspaceRelativePath.Normalize("new.txt");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        // Create payload with oversized content - this should fail at payload construction
        var oversizedContent = new string('a', AgentActionBudgets.ProposedFileTextMaxBytes + 1);
        
        // The payload constructor should throw for oversized content
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentCreateFileActionPayload(path, oversizedContent));
        
        Assert.Contains("exceeds the maximum byte budget", exception.Message);
    }

    [Fact]
    public void AgentFileProposalGenerator_CreateProposal_CreateOperationSucceeds()
    {
        var path = AgentWorkspaceRelativePath.Normalize("new.txt");
        var content = "Hello, World!";
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var payload = new AgentCreateFileActionPayload(path, content);

        var result = AgentFileProposalGenerator.CreateProposal(
            _scope,
            payload,
            _fileReader,
            fingerprint,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Proposal);
        Assert.Equal(AgentFileProposalOperation.Create, result.Proposal.Operation);
        Assert.Equal(path, result.Proposal.Path);
        Assert.False(result.Proposal.BaseExists);
        Assert.Null(result.Proposal.BaseRevision);
        Assert.Equal(AgentContentRevision.FromUtf8Text(content), result.Proposal.ProposedRevision);
        Assert.Contains("Create file:", result.Proposal.BoundedChangeSummary);
        Assert.Contains("Operation: create", result.Proposal.BoundedChangeSummary);
    }

    [Fact]
    public void AgentFileProposalGenerator_CreateProposal_ReplaceOperationWithMissingFileFails()
    {
        var path = AgentWorkspaceRelativePath.Normalize("nonexistent.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("expected");
        var content = "New content";
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var payload = new AgentReplaceFileActionPayload(path, baseRevision, content);

        var result = AgentFileProposalGenerator.CreateProposal(
            _scope,
            payload,
            _fileReader,
            fingerprint,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Exception);
        Assert.Contains("Base file read failed", result.Exception.Message);
    }

    [Fact]
    public void AgentFileProposalGenerator_CreateProposal_ReplaceOperationWithExistingFileSucceeds()
    {
        // Create a test file
        var filePath = System.IO.Path.Combine(_workspaceRoot, "existing.txt");
        var baseContent = "Original content";
        System.IO.File.WriteAllText(filePath, baseContent);

        var path = AgentWorkspaceRelativePath.Normalize("existing.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text(baseContent);
        var newContent = "Modified content";
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var payload = new AgentReplaceFileActionPayload(path, baseRevision, newContent);

        var result = AgentFileProposalGenerator.CreateProposal(
            _scope,
            payload,
            _fileReader,
            fingerprint,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Proposal);
        Assert.Equal(AgentFileProposalOperation.Replace, result.Proposal.Operation);
        Assert.Equal(path, result.Proposal.Path);
        Assert.True(result.Proposal.BaseExists);
        Assert.Equal(baseRevision, result.Proposal.BaseRevision);
        Assert.Equal(AgentContentRevision.FromUtf8Text(newContent), result.Proposal.ProposedRevision);
        Assert.Contains("Replace file:", result.Proposal.BoundedChangeSummary);
        Assert.Contains("Operation: replace", result.Proposal.BoundedChangeSummary);
    }

    [Fact]
    public void AgentFileProposalGenerator_CreateProposal_DeleteOperationWithExistingFileSucceeds()
    {
        // Create a test file
        var filePath = System.IO.Path.Combine(_workspaceRoot, "to-delete.txt");
        var baseContent = "Content to delete";
        System.IO.File.WriteAllText(filePath, baseContent);

        var path = AgentWorkspaceRelativePath.Normalize("to-delete.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text(baseContent);
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var payload = new AgentDeleteFileActionPayload(path, baseRevision);

        var result = AgentFileProposalGenerator.CreateProposal(
            _scope,
            payload,
            _fileReader,
            fingerprint,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Proposal);
        Assert.Equal(AgentFileProposalOperation.Delete, result.Proposal.Operation);
        Assert.Equal(path, result.Proposal.Path);
        Assert.True(result.Proposal.BaseExists);
        Assert.Equal(baseRevision, result.Proposal.BaseRevision);
        Assert.Null(result.Proposal.ProposedRevision);
        Assert.Contains("Delete file:", result.Proposal.BoundedChangeSummary);
        Assert.Contains("Operation: delete", result.Proposal.BoundedChangeSummary);
    }

    [Fact]
    public void AgentFileProposalGenerator_CreateProposal_DeleteOperationWithMissingFileFails()
    {
        var path = AgentWorkspaceRelativePath.Normalize("nonexistent.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("expected");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var payload = new AgentDeleteFileActionPayload(path, baseRevision);

        var result = AgentFileProposalGenerator.CreateProposal(
            _scope,
            payload,
            _fileReader,
            fingerprint,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Exception);
        Assert.Contains("Base file read failed", result.Exception.Message);
    }

    [Fact]
    public void AgentFileProposalGenerator_TruncatePreview_RespectsBounds()
    {
        // Test with content that exceeds the preview budget
        var largeContent = new string('x', 20000); // Much larger than MaxPreviewBytes (8KB)

        var result = AgentFileProposalGenerator.TruncatePreview(largeContent);

        // Should be truncated
        Assert.Contains("(truncated)", result);
        Assert.True(result.Length < 10000); // Should be much less than original
    }

    [Fact]
    public void AgentFileProposalGenerator_TruncatePreview_HandlesEmptyContent()
    {
        var result = AgentFileProposalGenerator.TruncatePreview("");
        Assert.Equal("(empty)", result);
    }

    [Fact]
    public void AgentFileProposalGenerator_TruncatePreview_HandlesNullContent()
    {
        var result = AgentFileProposalGenerator.TruncatePreview(null);
        Assert.Equal("(empty)", result);
    }

    [Fact]
    public void AgentFileProposalResult_SuccessAndFailureStates()
    {
        var proposalId = AgentFileProposalId.New();
        var path = AgentWorkspaceRelativePath.Normalize("test.txt");
        var proposedRevision = AgentContentRevision.FromUtf8Text("content");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Create,
            path,
            baseExists: false,
            baseRevision: null,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "create test.txt");

        var actionProposal = new AgentFileActionProposal(
            proposalId,
            proposal,
            _scope,
            fingerprint,
            null);

        // Success state
        var successResult = AgentFileProposalResult.Success(actionProposal);
        Assert.True(successResult.IsSuccess);
        Assert.Equal(actionProposal, successResult.Proposal);
        Assert.Null(successResult.Exception);

        // Failure state
        var exception = new InvalidOperationException("Test failure");
        var failureResult = AgentFileProposalResult.Failed(exception);
        Assert.False(failureResult.IsSuccess);
        Assert.Equal(exception, failureResult.Exception);

        // Failure with message
        var messageResult = AgentFileProposalResult.Failed("Test message");
        Assert.False(messageResult.IsSuccess);
        Assert.NotNull(messageResult.Exception);
        Assert.Contains("Test message", messageResult.Exception.Message);
    }

    [Fact]
    public void AgentFileActionProposal_Equality()
    {
        var proposalId = AgentFileProposalId.New();
        var path = AgentWorkspaceRelativePath.Normalize("test.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("content");
        var proposedRevision = AgentContentRevision.FromUtf8Text("new-content");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");

        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: baseRevision,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "replace test.txt");

        var actionProposal1 = new AgentFileActionProposal(
            proposalId,
            proposal,
            _scope,
            fingerprint,
            baseRevision);

        var actionProposal2 = new AgentFileActionProposal(
            proposalId,
            proposal,
            _scope,
            fingerprint,
            baseRevision);

        Assert.Equal(actionProposal1, actionProposal2);
        Assert.True(actionProposal1 == actionProposal2);
        Assert.False(actionProposal1 != actionProposal2);

        // Different proposal ID
        var differentProposalId = AgentFileProposalId.New();
        var actionProposal3 = new AgentFileActionProposal(
            differentProposalId,
            proposal,
            _scope,
            fingerprint,
            baseRevision);

        Assert.NotEqual(actionProposal1, actionProposal3);
        Assert.True(actionProposal1 != actionProposal3);
    }
}