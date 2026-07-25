using System;
using System.IO;
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

    [Fact]
    public void AgentActionRequestComposer_RejectsUnresolvedCommandExecutable()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AgentActionRequestComposer.Compose(
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                ActorId.HumanUser,
                ActorId.PanelSeed("alpha"),
                AgentBackendId.FromValue("backend:test"),
                WorkspaceIdentity.New(),
                WorkspaceGeneration.Initial,
                new AgentExecuteCommandActionPayload(
                    "dotnet",
                    new[] { "build" },
                    AgentWorkspaceRelativePath.Normalize("."))));

        Assert.Equal("payload", exception.ParamName);
        Assert.Contains("absolute path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentActionRequestFingerprint_CommandFingerprintChangesWhenBoundFieldsChange()
    {
        var workspaceA = WorkspaceIdentity.New();
        var workspaceB = WorkspaceIdentity.New();
        var generationA = WorkspaceGeneration.Initial;
        var generationB = new WorkspaceGeneration(generationA.Value + 1);
        var runA = ExecutionRunId.New();
        var runB = ExecutionRunId.New();
        var executableA = Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", "dotnet");
        var executableB = Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", "git");
        var baseline = CreateResolvedCommand(executableA, new[] { "build" }, AgentWorkspaceRelativePath.Normalize("."));

        var baselineFingerprint = AgentActionRequestFingerprintComputer.Compute(
            workspaceA,
            generationA,
            runA,
            baseline);

        var differentExecutable = CreateResolvedCommand(executableB, new[] { "build" }, baseline.WorkingDirectory);
        var differentArguments = CreateResolvedCommand(executableA, new[] { "test" }, baseline.WorkingDirectory);
        var differentWorkingDirectory = CreateResolvedCommand(
            executableA,
            new[] { "build" },
            AgentWorkspaceRelativePath.Normalize("src"));
        var deniedExecutable = CreateResolvedCommand(
            Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", "bash"),
            new[] { "-c", "echo" },
            baseline.WorkingDirectory);

        Assert.NotEqual(
            baselineFingerprint,
            AgentActionRequestFingerprintComputer.Compute(workspaceB, generationA, runA, baseline));
        Assert.NotEqual(
            baselineFingerprint,
            AgentActionRequestFingerprintComputer.Compute(workspaceA, generationB, runA, baseline));
        Assert.NotEqual(
            baselineFingerprint,
            AgentActionRequestFingerprintComputer.Compute(workspaceA, generationA, runB, baseline));
        Assert.NotEqual(
            baselineFingerprint,
            AgentActionRequestFingerprintComputer.Compute(workspaceA, generationA, runA, differentExecutable));
        Assert.NotEqual(
            baselineFingerprint,
            AgentActionRequestFingerprintComputer.Compute(workspaceA, generationA, runA, differentArguments));
        Assert.NotEqual(
            baselineFingerprint,
            AgentActionRequestFingerprintComputer.Compute(workspaceA, generationA, runA, differentWorkingDirectory));
        Assert.NotEqual(
            baselineFingerprint,
            AgentActionRequestFingerprintComputer.Compute(workspaceA, generationA, runA, deniedExecutable));
    }

    [Fact]
    public void AgentActionDisplaySummary_CommandSummaryBindsResolvedExecutableAndDenylist()
    {
        var executable = Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", "dotnet");
        Assert.True(
            AgentResolvedCommand.TryCreate(
                new AgentExecuteCommandActionPayload(
                    executable,
                    new[] { "build" },
                    AgentWorkspaceRelativePath.Normalize(".")),
                out var resolvedCommand,
                out _));

        var summary = AgentActionDisplaySummaryBuilder.Build(resolvedCommand!);

        Assert.Contains(resolvedCommand!.CanonicalAbsoluteExecutablePath, summary.DetailText, StringComparison.Ordinal);
        Assert.Contains("Denylist: Allowed", summary.DetailText, StringComparison.Ordinal);
    }

    private static AgentResolvedCommand CreateResolvedCommand(
        string executable,
        string[] arguments,
        AgentWorkspaceRelativePath workingDirectory)
    {
        Assert.True(
            AgentResolvedCommand.TryCreate(
                new AgentExecuteCommandActionPayload(executable, arguments, workingDirectory),
                out var resolvedCommand,
                out var error),
            error);
        return resolvedCommand!;
    }
}
