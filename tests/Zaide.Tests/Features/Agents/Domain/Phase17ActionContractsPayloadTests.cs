using System;
using System.IO;
using Xunit;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class Phase17ActionContractsPayloadTests
{
    [Fact]
    public void AgentActionPayload_EnforcesExactKindMatching()
    {
        var read = new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize("src/App.cs"));

        Assert.True(AgentActionPayload.MatchesKind(AgentActionKind.ReadFile, read));
        Assert.False(AgentActionPayload.MatchesKind(AgentActionKind.CreateFile, read));
    }

    [Fact]
    public void AgentCreateFileActionPayload_RejectsOversizedText()
    {
        var oversized = new string('x', AgentActionBudgets.ProposedFileTextMaxBytes + 1);
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("new.txt"),
                oversized));

        Assert.Equal("proposedText", exception.ParamName);
    }

    [Fact]
    public void AgentReplaceFileActionPayload_ComputesProposedRevision()
    {
        var baseRevision = AgentContentRevision.FromUtf8Text("before");
        var payload = new AgentReplaceFileActionPayload(
            AgentWorkspaceRelativePath.Normalize("src/App.cs"),
            baseRevision,
            "after");

        Assert.Equal(AgentContentRevision.FromUtf8Text("after"), payload.ProposedRevision);
    }

    [Fact]
    public void AgentDeleteFileActionPayload_RequiresBaseRevision()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentDeleteFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("src/App.cs"),
                default));

        Assert.Equal("baseRevision", exception.ParamName);
    }

    [Fact]
    public void AgentExecuteCommandActionPayload_RejectsBlankArguments()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentExecuteCommandActionPayload(
                "dotnet",
                new[] { "build", " " },
                AgentWorkspaceRelativePath.Normalize(".")));

        Assert.Equal("arguments", exception.ParamName);
    }

    [Fact]
    public void AgentResolvedCommand_RejectsUnresolvedExecutableToken()
    {
        Assert.False(
            AgentResolvedCommand.TryCreate(
                new AgentExecuteCommandActionPayload(
                    "dotnet",
                    new[] { "build" },
                    AgentWorkspaceRelativePath.Normalize(".")),
                out _,
                out var error));
        Assert.Contains("absolute path", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentCommandDenylist_ClassifiesShellInterpretersAndPrivilegeEscalation()
    {
        var bash = Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", "bash");
        var sudo = Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", "sudo");
        var dotnet = Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", "dotnet");

        Assert.Equal(
            AgentCommandDenylistClassification.DeniedShellInterpreter,
            AgentCommandDenylist.Classify(bash).Classification);
        Assert.Equal(
            AgentCommandDenylistClassification.DeniedPrivilegeEscalation,
            AgentCommandDenylist.Classify(sudo).Classification);
        Assert.Equal(
            AgentCommandDenylistClassification.Allowed,
            AgentCommandDenylist.Classify(dotnet).Classification);
    }

    [Fact]
    public void AgentWorkspaceRelativePath_RejectsTraversal()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AgentWorkspaceRelativePath.Normalize("../secret.txt"));

        Assert.Equal("workspaceRelativePath", exception.ParamName);
    }

    [Fact]
    public void AgentWorkspaceRelativePath_NormalizesSeparators()
    {
        var path = AgentWorkspaceRelativePath.Normalize("src\\App\\Program.cs");
        Assert.Equal("src/App/Program.cs", path.NormalizedPath);
    }
}
