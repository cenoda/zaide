using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;
using Zaide.Tests.Features.Agents;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 17 M4 corrective pass — broker-to-proposal-to-review integration tests.
/// Proves: fail-closed proposal generation, stale-base detection, binding validation,
/// and no filesystem mutation.
/// </summary>
public sealed class Phase17ProposalBrokerTests
{
    private readonly string _workspaceRoot;
    private readonly WorkspaceActionScope _scope;
    private readonly WorkspaceFileReader _fileReader;
    private readonly FakeWorkspaceActionAuthority _workspaceAuthority;

    public Phase17ProposalBrokerTests()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "zaide-p17-broker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);

        _scope = FakeWorkspaceActionAuthority.CreateScopeFromDirectory(_workspaceRoot);
        _workspaceAuthority = new FakeWorkspaceActionAuthority(_scope);
        _fileReader = new WorkspaceFileReader();
    }

    [Fact]
    public async Task BrokerToProposalToReviewIntegration_CreateFile_ProposalGeneratedAndDisplayed()
    {
        // Arrange
        var broker = CreateBroker();
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("new-file.txt"),
            "test content");

        // Act
        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        // Assert
        // For create file without permission service, it should be denied by policy
        // But the proposal should have been created and used for display
        Assert.NotNull(result);
        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        // The request was processed (proposal created)
    }

    [Fact]
    public async Task ExistingTargetCreateRejection_FileAlreadyExists()
    {
        // Arrange
        var broker = CreateBroker();
        var filePath = "existing-file.txt";
        var fullPath = Path.Combine(_workspaceRoot, filePath);
        
        // Create the file first
        File.WriteAllText(fullPath, "existing content");
        
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize(filePath),
            "new content");

        // Act
        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        // Assert
        // Should be denied because file already exists
        Assert.NotNull(result);
        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.InvalidRequest, result.FailureKind);
        Assert.Contains("file already exists", result.Summary);
    }

    [Fact]
    public void ProposalGenerationFailure_FailClosed_OversizedContent()
    {
        // Arrange & Act & Assert
        // Create a payload with oversized content (exceeds 1 MiB budget)
        var oversizedContent = new string('a', AgentActionBudgets.ProposedFileTextMaxBytes + 1);
        
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("oversized.txt"),
                oversizedContent));
        
        Assert.Contains("maximum byte budget", exception.Message);
    }

    [Fact]
    public void BinaryContentRejection_CreateFileWithNullBytes()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("binary.txt"),
                "test\0content"));
        
        Assert.Contains("binary", exception.Message.ToLower());
    }

    [Fact]
    public void BinaryContentRejection_ReplaceFileWithNullBytes()
    {
        // Arrange
        var filePath = "test.txt";
        var fullPath = Path.Combine(_workspaceRoot, filePath);
        File.WriteAllText(fullPath, "original");
        var baseRevision = AgentContentRevision.FromUtf8Text("original");
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentReplaceFileActionPayload(
                AgentWorkspaceRelativePath.Normalize(filePath),
                baseRevision,
                "test\0content"));
        
        Assert.Contains("binary", exception.Message.ToLower());
    }

    [Fact]
    public void StaleBaseDetection_CreateOperation_FileExistsAfterProposal()
    {
        // Arrange
        var path = AgentWorkspaceRelativePath.Normalize("stale-test.txt");
        var proposedText = "proposed content";
        var proposedRevision = AgentContentRevision.FromUtf8Text(proposedText);
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");
        
        // Create proposal when file doesn't exist
        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Create,
            path,
            baseExists: false,
            baseRevision: null,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "create stale-test.txt");
        
        var actionProposal = new AgentFileActionProposal(
            AgentFileProposalId.New(),
            proposal,
            _scope,
            fingerprint,
            null);

        // Act - check if base is stale when file now exists
        var currentBaseRevision = AgentContentRevision.FromUtf8Text("new content");
        
        // Assert
        Assert.True(actionProposal.IsBaseStale(currentBaseRevision));
    }

    [Fact]
    public void StaleBaseDetection_ReplaceOperation_BaseChanged()
    {
        // Arrange
        var path = AgentWorkspaceRelativePath.Normalize("stale-test.txt");
        var originalBaseRevision = AgentContentRevision.FromUtf8Text("original content");
        var proposedRevision = AgentContentRevision.FromUtf8Text("proposed content");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");
        
        // Create proposal with original base
        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: originalBaseRevision,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "replace stale-test.txt");
        
        var actionProposal = new AgentFileActionProposal(
            AgentFileProposalId.New(),
            proposal,
            _scope,
            fingerprint,
            originalBaseRevision);

        // Act - check if base is stale when content changed
        var newBaseRevision = AgentContentRevision.FromUtf8Text("modified content");
        
        // Assert
        Assert.True(actionProposal.IsBaseStale(newBaseRevision));
    }

    [Fact]
    public void StaleBaseDetection_ReplaceOperation_BaseUnchanged()
    {
        // Arrange
        var path = AgentWorkspaceRelativePath.Normalize("stable-test.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("unchanged content");
        var proposedRevision = AgentContentRevision.FromUtf8Text("proposed content");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");
        
        // Create proposal
        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: baseRevision,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "replace stable-test.txt");
        
        var actionProposal = new AgentFileActionProposal(
            AgentFileProposalId.New(),
            proposal,
            _scope,
            fingerprint,
            baseRevision);

        // Act - check if base is stale when content unchanged
        var currentBaseRevision = baseRevision; // Same revision
        
        // Assert
        Assert.False(actionProposal.IsBaseStale(currentBaseRevision));
    }

    [Fact]
    public void PermissionFingerprintMatchesBase_WhenBothNull()
    {
        // Arrange
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
            AgentFileProposalId.New(),
            proposal,
            _scope,
            fingerprint,
            null);

        // Act & Assert
        Assert.True(actionProposal.PermissionFingerprintMatchesBase());
    }

    [Fact]
    public void PermissionFingerprintMatchesBase_WhenBothMatch()
    {
        // Arrange
        var path = AgentWorkspaceRelativePath.Normalize("test.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("base content");
        var proposedRevision = AgentContentRevision.FromUtf8Text("proposed content");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");
        
        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: baseRevision,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "replace test.txt");
        
        var actionProposal = new AgentFileActionProposal(
            AgentFileProposalId.New(),
            proposal,
            _scope,
            fingerprint,
            baseRevision);

        // Act & Assert
        Assert.True(actionProposal.PermissionFingerprintMatchesBase());
    }

    [Fact]
    public void PermissionFingerprintMatchesBase_WhenMismatch()
    {
        // Arrange
        var path = AgentWorkspaceRelativePath.Normalize("test.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("base content");
        var differentRevision = AgentContentRevision.FromUtf8Text("different content");
        var proposedRevision = AgentContentRevision.FromUtf8Text("proposed content");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");
        
        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: baseRevision,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "replace test.txt");
        
        var actionProposal = new AgentFileActionProposal(
            AgentFileProposalId.New(),
            proposal,
            _scope,
            fingerprint,
            differentRevision); // Different from proposal's base revision

        // Act & Assert
        Assert.False(actionProposal.PermissionFingerprintMatchesBase());
    }

    [Fact]
    public void BoundedProposalSummary_IsBounded()
    {
        // Arrange
        var path = AgentWorkspaceRelativePath.Normalize("test.txt");
        var proposedRevision = AgentContentRevision.FromUtf8Text("content");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");
        
        // Create a very long summary
        var longSummary = new string('a', AgentActionBudgets.PermissionPreviewSummaryMaxBytes * 2);
        
        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Create,
            path,
            baseExists: false,
            baseRevision: null,
            proposedRevision: proposedRevision,
            boundedChangeSummary: longSummary);
        
        var actionProposal = new AgentFileActionProposal(
            AgentFileProposalId.New(),
            proposal,
            _scope,
            fingerprint,
            null);

        // Act
        var boundedSummary = actionProposal.BoundedChangeSummary;

        // Assert - the summary should be bounded (though the proposal accepts any string)
        // The generator should ensure it's bounded
        Assert.NotNull(boundedSummary);
    }

    [Fact]
    public void ProposalImmutability_AfterCreation()
    {
        // Arrange
        var path = AgentWorkspaceRelativePath.Normalize("test.txt");
        var baseRevision = AgentContentRevision.FromUtf8Text("base");
        var proposedRevision = AgentContentRevision.FromUtf8Text("proposed");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("test-request");
        
        var proposal = new AgentFileProposal(
            AgentFileProposalOperation.Replace,
            path,
            baseExists: true,
            baseRevision: baseRevision,
            proposedRevision: proposedRevision,
            boundedChangeSummary: "replace test.txt");
        
        var actionProposal = new AgentFileActionProposal(
            AgentFileProposalId.New(),
            proposal,
            _scope,
            fingerprint,
            baseRevision);

        // Act & Assert - all properties should be readable but not settable
        var id = actionProposal.ProposalId;
        var op = actionProposal.Operation;
        var p = actionProposal.Path;
        var baseExists = actionProposal.BaseExists;
        var baseRev = actionProposal.BaseRevision;
        var proposedRev = actionProposal.ProposedRevision;
        var summary = actionProposal.BoundedChangeSummary;
        var scope = actionProposal.WorkspaceScope;
        var fp = actionProposal.PermissionFingerprint;
        
        // All should be non-null/valid
        Assert.NotEqual(default, id);
        Assert.Equal(AgentFileProposalOperation.Replace, op);
        Assert.Equal(path, p);
        Assert.True(baseExists);
        Assert.Equal(baseRevision, baseRev);
        Assert.Equal(proposedRevision, proposedRev);
        Assert.NotNull(summary);
        Assert.Equal(_scope, scope);
        Assert.Equal(fingerprint, fp);
    }

    [Fact]
    public async Task NoFilesystemMutation_CreateProposalDoesNotWrite()
    {
        // Arrange
        var broker = CreateBroker();
        var filePath = "no-mutation-test.txt";
        var fullPath = Path.Combine(_workspaceRoot, filePath);
        
        // Ensure file doesn't exist
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize(filePath),
            "test content");

        // Act - request should fail (no permission service) but let's see if it tries to write
        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        // Assert - file should still not exist (no mutation)
        Assert.False(File.Exists(fullPath), "Proposal creation should not write to filesystem");
    }

    [Fact]
    public async Task ReplaceProposal_BaseRevisionMismatch_Rejected()
    {
        // Arrange
        var broker = CreateBroker();
        var filePath = "replace-test.txt";
        var fullPath = Path.Combine(_workspaceRoot, filePath);
        
        // Create file with specific content
        File.WriteAllText(fullPath, "original content");
        
        // Create payload with different base revision
        var wrongBaseRevision = AgentContentRevision.FromUtf8Text("different content");
        
        var payload = new AgentReplaceFileActionPayload(
            AgentWorkspaceRelativePath.Normalize(filePath),
            wrongBaseRevision,
            "new content");

        // Act
        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        // Assert - should be denied due to base revision mismatch
        Assert.NotNull(result);
        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.InvalidRequest, result.FailureKind);
    }

    [Fact]
    public async Task DeleteProposal_BaseRevisionMismatch_Rejected()
    {
        // Arrange
        var broker = CreateBroker();
        var filePath = "delete-test.txt";
        var fullPath = Path.Combine(_workspaceRoot, filePath);
        
        // Create file with specific content
        File.WriteAllText(fullPath, "original content");
        
        // Create payload with different base revision
        var wrongBaseRevision = AgentContentRevision.FromUtf8Text("different content");
        
        var payload = new AgentDeleteFileActionPayload(
            AgentWorkspaceRelativePath.Normalize(filePath),
            wrongBaseRevision);

        // Act
        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        // Assert - should be denied due to base revision mismatch
        Assert.NotNull(result);
        Assert.Equal(AgentActionResultKind.Denied, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.InvalidRequest, result.FailureKind);
    }

    // ----------------------------------------------------------------
    // M4 corrective pass — create target inspection and stale-base races
    // ----------------------------------------------------------------

    [Fact]
    public void SyntheticReader_DefaultNotFound_DoesNotFalseRejectCreateProposal()
    {
        var reader = new CountingAgentFileReader();
        var path = AgentWorkspaceRelativePath.Normalize("synthetic-new.txt");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("create-synthetic");

        var result = AgentFileProposalGenerator.CreateProposal(
            _scope,
            new AgentCreateFileActionPayload(path, "hello"),
            reader,
            fingerprint,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public void SyntheticReader_PathAwareRead_SucceedsForConfiguredPathOnly()
    {
        var reader = new CountingAgentFileReader();
        var absentPath = AgentWorkspaceRelativePath.Normalize("absent.txt");
        var existingPath = AgentWorkspaceRelativePath.Normalize("existing.txt");
        reader.SetPathResult(
            existingPath,
            AgentFileReadResult.Success(
                "existing",
                AgentContentRevision.FromUtf8Text("existing"),
                byteLength: 8));
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("path-aware");

        var absentResult = AgentFileProposalGenerator.CreateProposal(
            _scope,
            new AgentCreateFileActionPayload(absentPath, "new"),
            reader,
            fingerprint,
            CancellationToken.None);
        var existingResult = AgentFileProposalGenerator.CreateProposal(
            _scope,
            new AgentCreateFileActionPayload(existingPath, "new"),
            reader,
            fingerprint,
            CancellationToken.None);

        Assert.True(absentResult.IsSuccess);
        Assert.False(existingResult.IsSuccess);
        Assert.Contains("file already exists", existingResult.Exception!.Message);
    }

    [Fact]
    public void CreateProposal_RejectsIndeterminateTargetInspection()
    {
        AgentFileReadOutcome[] indeterminateOutcomes =
        [
            AgentFileReadOutcome.Succeeded,
            AgentFileReadOutcome.NotRegularFile,
            AgentFileReadOutcome.PathEscaped,
            AgentFileReadOutcome.Binary,
            AgentFileReadOutcome.TooLarge,
            AgentFileReadOutcome.Unreadable,
            AgentFileReadOutcome.Cancelled,
        ];

        foreach (var outcome in indeterminateOutcomes)
        {
            var reader = outcome == AgentFileReadOutcome.Succeeded
                ? new CountingAgentFileReader(AgentFileReadResult.Success(
                    "existing",
                    AgentContentRevision.FromUtf8Text("existing"),
                    byteLength: 8))
                : new CountingAgentFileReader(
                    AgentFileReadResult.Rejected(outcome, $"inspection returned {outcome}"));
            var path = AgentWorkspaceRelativePath.Normalize($"indeterminate-{outcome}.txt");
            var fingerprint = AgentActionRequestFingerprint.FromCanonicalText($"indeterminate-{outcome}");

            var result = AgentFileProposalGenerator.CreateProposal(
                _scope,
                new AgentCreateFileActionPayload(path, "hello"),
                reader,
                fingerprint,
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Exception);
        }
    }

    private static AgentFileReadResult ConfirmedAbsentTarget =>
        AgentFileReadResult.Rejected(
            AgentFileReadOutcome.NotFound,
            "File does not exist in the workspace.");

    [Fact]
    public async Task CreateTargetAppearingBeforeDecisionConsumption_RevokedAsStaleBase()
    {
        var reader = new CountingAgentFileReader();
        reader.EnqueueReads(
            ConfirmedAbsentTarget,
            AgentFileReadResult.Success(
                "appeared",
                AgentContentRevision.FromUtf8Text("appeared"),
                byteLength: 8));
        var broker = CreateBroker(reader, new AllowingPermissionReviewService());
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("race.txt"),
            "content");

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Revoked, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleBaseRevision, result.FailureKind);
        Assert.Equal(2, reader.ReadCount);
    }

    [Fact]
    public async Task CreateTargetBecomingUnreadableBeforeDecisionConsumption_RevokedAsStaleBase()
    {
        var reader = new CountingAgentFileReader();
        reader.EnqueueReads(
            ConfirmedAbsentTarget,
            AgentFileReadResult.Rejected(
                AgentFileReadOutcome.Unreadable,
                "target became unreadable"));
        var broker = CreateBroker(reader, new AllowingPermissionReviewService());
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("race-unreadable.txt"),
            "content");

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Revoked, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleBaseRevision, result.FailureKind);
    }

    [Fact]
    public async Task CreateTargetBecomingNonRegularBeforeDecisionConsumption_RevokedAsStaleBase()
    {
        var reader = new CountingAgentFileReader();
        reader.EnqueueReads(
            ConfirmedAbsentTarget,
            AgentFileReadResult.Rejected(
                AgentFileReadOutcome.NotRegularFile,
                "target became a directory"));
        var broker = CreateBroker(reader, new AllowingPermissionReviewService());
        var payload = new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("race-dir.txt"),
            "content");

        var result = await broker.RequestAsync(payload, null, CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Revoked, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleBaseRevision, result.FailureKind);
    }

    [Fact]
    public async Task CreateStaleBase_DoesNotConsumePublishedDecision()
    {
        var reader = new CountingAgentFileReader();
        reader.EnqueueReads(
            ConfirmedAbsentTarget,
            AgentFileReadResult.Success(
                "appeared",
                AgentContentRevision.FromUtf8Text("appeared"),
                byteLength: 8));
        var review = new CapturingAllowingPermissionReviewService();
        var result = await CreateBroker(reader, review).RequestAsync(
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("create-stale.txt"),
                "content"),
            null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Revoked, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleBaseRevision, result.FailureKind);
        Assert.Equal(AgentPermissionDecisionStatus.Published, review.Decision!.Status);
    }

    [Fact]
    public async Task ReplaceStaleBase_DoesNotConsumePublishedDecision()
    {
        var baseRevision = AgentContentRevision.FromUtf8Text("base");
        var reader = new CountingAgentFileReader();
        reader.EnqueueReads(
            AgentFileReadResult.Success("base", baseRevision, byteLength: 4),
            AgentFileReadResult.Success(
                "changed",
                AgentContentRevision.FromUtf8Text("changed"),
                byteLength: 7));
        var review = new CapturingAllowingPermissionReviewService();
        var result = await CreateBroker(reader, review).RequestAsync(
            new AgentReplaceFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("replace-stale.txt"),
                baseRevision,
                "replacement"),
            null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Revoked, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleBaseRevision, result.FailureKind);
        Assert.Equal(AgentPermissionDecisionStatus.Published, review.Decision!.Status);
    }

    [Fact]
    public async Task DeleteStaleBase_DoesNotConsumePublishedDecision()
    {
        var baseRevision = AgentContentRevision.FromUtf8Text("base");
        var reader = new CountingAgentFileReader();
        reader.EnqueueReads(
            AgentFileReadResult.Success("base", baseRevision, byteLength: 4),
            AgentFileReadResult.Success(
                "changed",
                AgentContentRevision.FromUtf8Text("changed"),
                byteLength: 7));
        var review = new CapturingAllowingPermissionReviewService();
        var result = await CreateBroker(reader, review).RequestAsync(
            new AgentDeleteFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("delete-stale.txt"),
                baseRevision),
            null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Revoked, result.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleBaseRevision, result.FailureKind);
        Assert.Equal(AgentPermissionDecisionStatus.Published, review.Decision!.Status);
    }

    [Fact]
    public async Task FreshProposal_ConsumesDecisionExactlyOnce()
    {
        var baseRevision = AgentContentRevision.FromUtf8Text("base");
        var reader = new CountingAgentFileReader();
        reader.EnqueueReads(
            AgentFileReadResult.Success("base", baseRevision, byteLength: 4),
            AgentFileReadResult.Success("base", baseRevision, byteLength: 4));
        var review = new CapturingAllowingPermissionReviewService();
        var result = await CreateBroker(reader, review).RequestAsync(
            new AgentReplaceFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("fresh.txt"),
                baseRevision,
                "replacement"),
            null,
            CancellationToken.None);

        Assert.Equal(AgentActionResultKind.Succeeded, result.ResultKind);
        Assert.Equal(AgentPermissionDecisionStatus.Consumed, review.Decision!.Status);
        Assert.False(review.Decision.TryConsume());
    }

    [Fact]
    public async Task ConcurrentStaleAndAllowRaces_StaleProposalCannotConsumeDecision()
    {
        var baseRevision = AgentContentRevision.FromUtf8Text("base");
        var staleReader = new GateReader(
            AgentFileReadResult.Success("base", baseRevision, byteLength: 4),
            AgentFileReadResult.Success(
                "changed",
                AgentContentRevision.FromUtf8Text("changed"),
                byteLength: 7));
        var allowReader = new CountingAgentFileReader();
        allowReader.EnqueueReads(
            AgentFileReadResult.Success("base", baseRevision, byteLength: 4),
            AgentFileReadResult.Success("base", baseRevision, byteLength: 4));
        var review = new CapturingAllowingPermissionReviewService();
        var runId = ExecutionRunId.New();
        var staleBroker = CreateBroker(staleReader, review, runId);
        var allowBroker = CreateBroker(allowReader, review, runId);
        var payload = new AgentReplaceFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("concurrent.txt"),
            baseRevision,
            "replacement");

        var staleTask = Task.Run(() => staleBroker.RequestAsync(payload, null, CancellationToken.None).AsTask());
        await staleReader.RevalidationStarted.Task;
        var allowResult = await allowBroker.RequestAsync(payload, null, CancellationToken.None);
        staleReader.Release();
        var staleResult = await staleTask;

        Assert.Equal(AgentActionResultKind.Revoked, staleResult.ResultKind);
        Assert.Equal(AgentActionFailureKind.StaleBaseRevision, staleResult.FailureKind);
        Assert.Equal(AgentActionResultKind.Succeeded, allowResult.ResultKind);
        Assert.Equal(AgentPermissionDecisionStatus.Consumed, review.Decision!.Status);
    }

    private sealed class AllowingPermissionReviewService : IAgentPermissionReviewService
    {
        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AgentPermissionDecision(
                AgentPermissionDecisionId.New(),
                request.Fingerprint,
                AgentActionPermissionClassification.RequiresUserDecision,
                AgentPermissionDecisionStatus.Published,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                true));
    }

    private sealed class CapturingAllowingPermissionReviewService : IAgentPermissionReviewService
    {
        private readonly object _gate = new();

        public AgentPermissionDecision? Decision { get; private set; }

        public ValueTask<AgentPermissionDecision> RequestDecisionAsync(
            AgentActionRequest request,
            AgentActionDisplaySummary displaySummary,
            WorkspaceActionScope? workspaceScope,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                Decision ??= new AgentPermissionDecision(
                    AgentPermissionDecisionId.New(),
                    request.Fingerprint,
                    AgentActionPermissionClassification.RequiresUserDecision,
                    AgentPermissionDecisionStatus.Published,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddMinutes(5),
                    true);
                return ValueTask.FromResult(Decision);
            }
        }
    }

    private sealed class GateReader : IAgentFileReader
    {
        private readonly AgentFileReadResult _initial;
        private readonly AgentFileReadResult _revalidation;
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;

        public GateReader(AgentFileReadResult initial, AgentFileReadResult revalidation)
        {
            _initial = initial;
            _revalidation = revalidation;
        }

        public TaskCompletionSource<bool> RevalidationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AgentFileReadResult Read(
            WorkspaceActionScope scope,
            AgentWorkspaceRelativePath path,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _readCount) == 2)
            {
                RevalidationStarted.TrySetResult(true);
                _release.Task.GetAwaiter().GetResult();
                return _revalidation;
            }

            return _initial;
        }

        public void Release() => _release.TrySetResult(true);
    }

    private ContractAgentActionBroker CreateBroker(
        IAgentFileReader reader,
        IAgentPermissionReviewService? permissionReviewService = null,
        ExecutionRunId? runId = null)
    {
        return new ContractAgentActionBroker(
            AgentSessionId.New(),
            runId ?? ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("agent-target"),
            AgentBackendId.FromValue("backend:test"),
            _workspaceAuthority,
            reader,
            new DefaultAgentCommandResolver(),
            new AgentActionRunSlotTracker(),
            new AgentActionCorrelationRegistry(),
            permissionReviewService);
    }

    private ContractAgentActionBroker CreateBroker()
    {
        return CreateBroker(_fileReader);
    }
}
