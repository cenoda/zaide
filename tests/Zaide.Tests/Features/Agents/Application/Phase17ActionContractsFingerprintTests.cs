using System;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.Agents.Application;

public sealed class Phase17ActionContractsFingerprintTests
{
    [Fact]
    public void AgentActionRequestFingerprint_IsStableForIdenticalRequests()
    {
        var workspace = WorkspaceIdentity.New();
        var generation = WorkspaceGeneration.Initial;
        var runId = ExecutionRunId.New();
        var payload = new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("README.md"));

        var first = AgentActionRequestFingerprintComputer.Compute(workspace, generation, runId, payload);
        var second = AgentActionRequestFingerprintComputer.Compute(workspace, generation, runId, payload);

        Assert.Equal(first, second);
    }

    [Fact]
    public void AgentActionRequestFingerprint_ChangesWhenPayloadChanges()
    {
        var workspace = WorkspaceIdentity.New();
        var generation = WorkspaceGeneration.Initial;
        var runId = ExecutionRunId.New();

        var readA = new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("a.txt"));
        var readB = new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("b.txt"));

        var fingerprintA = AgentActionRequestFingerprintComputer.Compute(workspace, generation, runId, readA);
        var fingerprintB = AgentActionRequestFingerprintComputer.Compute(workspace, generation, runId, readB);

        Assert.NotEqual(fingerprintA, fingerprintB);
    }

    [Fact]
    public void AgentActionCorrelationRegistry_ReplaysMatchingFingerprint()
    {
        var registry = new AgentActionCorrelationRegistry();
        var key = AgentActionCorrelationKey.FromValue("tool-call-1");
        var fingerprint = AgentActionRequestFingerprint.FromCanonicalText("kind=ReadFile");
        var terminal = new AgentActionResult(
            AgentActionId.New(),
            AgentActionAttemptId.New(),
            AgentActionResultKind.Failed,
            AgentActionFailureKind.ExecutionFailed,
            "not executed");

        registry.RecordTerminalResult(key, fingerprint, terminal);

        Assert.True(registry.TryGetTerminalResult(key, fingerprint, out var replay));
        Assert.Equal(terminal.Summary, replay!.Summary);
    }

    [Fact]
    public void AgentActionCorrelationRegistry_RejectsMismatchedFingerprintReuse()
    {
        var registry = new AgentActionCorrelationRegistry();
        var key = AgentActionCorrelationKey.FromValue("tool-call-1");
        var firstFingerprint = AgentActionRequestFingerprint.FromCanonicalText("kind=ReadFile");
        var secondFingerprint = AgentActionRequestFingerprint.FromCanonicalText("kind=CreateFile");
        registry.RecordTerminalResult(
            key,
            firstFingerprint,
            new AgentActionResult(
                AgentActionId.New(),
                AgentActionAttemptId.New(),
                AgentActionResultKind.Denied,
                AgentActionFailureKind.PermissionDenied,
                "denied"));

        Assert.True(registry.TryRejectMismatchedFingerprint(key, secondFingerprint, out var rejection));
        Assert.Equal(AgentActionFailureKind.CorrelationKeyMismatch, rejection!.FailureKind);
    }

    [Fact]
    public void AgentActionRequestComposer_BuildsDisplayReadyNonReadSummary()
    {
        var request = AgentActionRequestComposer.Compose(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            ActorId.HumanUser,
            ActorId.PanelSeed("alpha"),
            AgentBackendId.FromValue("backend:test"),
            WorkspaceIdentity.New(),
            WorkspaceGeneration.Initial,
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("new.txt"),
                "hello"));

        Assert.Equal(AgentActionKind.CreateFile, request.DisplaySummary.Kind);
        Assert.Contains("Scope: this exact request only.", request.DisplaySummary.DetailText, StringComparison.Ordinal);
        Assert.NotEqual(default(AgentActionRequestFingerprint), request.Fingerprint);
    }
}
