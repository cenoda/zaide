using System;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents.Application;

public sealed class Phase17ActionContractsPolicyTests
{
    [Fact]
    public void AgentActionPolicyClassifier_ClassifiesLockedPolicy()
    {
        Assert.Equal(
            AgentActionPermissionClassification.AllowedByLockedPolicy,
            AgentActionPolicyClassifier.Classify(CreatePayload(AgentActionKind.ReadFile)));
        Assert.Equal(
            AgentActionPermissionClassification.RequiresUserDecision,
            AgentActionPolicyClassifier.Classify(CreatePayload(AgentActionKind.CreateFile)));
        Assert.Equal(
            AgentActionPermissionClassification.RequiresUserDecision,
            AgentActionPolicyClassifier.Classify(CreatePayload(AgentActionKind.ReplaceFile)));
        Assert.Equal(
            AgentActionPermissionClassification.RequiresUserDecision,
            AgentActionPolicyClassifier.Classify(CreatePayload(AgentActionKind.DeleteFile)));
        Assert.Equal(
            AgentActionPermissionClassification.RequiresUserDecision,
            AgentActionPolicyClassifier.Classify(CreatePayload(AgentActionKind.ExecuteCommand)));
    }

    [Fact]
    public void AgentActionBudgets_ValidatePositiveFiniteRejectsZero()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgentActionBudgets.ValidatePositiveFinite(TimeSpan.Zero, "timeout"));

        Assert.Equal("timeout", exception.ParamName);
    }

    [Fact]
    public void AgentContentRevision_FromDigest_RejectsUppercaseHex()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AgentContentRevision.FromDigest(new string('A', 64)));

        Assert.Equal("digest", exception.ParamName);
    }

    [Fact]
    public void AgentFileProposal_ValidatesCreateRevisionRules()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentFileProposal(
                AgentFileProposalOperation.Create,
                AgentWorkspaceRelativePath.Normalize("new.txt"),
                baseExists: true,
                baseRevision: null,
                proposedRevision: AgentContentRevision.FromUtf8Text("hello"),
                boundedChangeSummary: "create"));

        Assert.Contains("missing base file", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentActionAuditSummary_RedactsSecretsAndBoundsText()
    {
        var secretSummary = new AgentActionAuditSummary("token=super-secret-value");
        Assert.Equal("[redacted]", secretSummary.Text);
        Assert.True(secretSummary.WasRedacted);
    }

    [Fact]
    public void AgentActionDisplaySummary_TruncatesOversizedDetail()
    {
        var lines = string.Join('\n', Enumerable.Repeat("line", 2_500));
        var summary = new AgentActionDisplaySummary(
            AgentActionKind.ReplaceFile,
            "Replace workspace file",
            lines,
            wasTruncated: false);

        Assert.True(summary.WasTruncated);
        Assert.True(summary.LineCount <= AgentActionBudgets.PermissionPreviewSummaryMaxLines);
    }

    private static AgentActionPayload CreatePayload(AgentActionKind kind) =>
        kind switch
        {
            AgentActionKind.ReadFile => new AgentReadFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("README.md")),
            AgentActionKind.CreateFile => new AgentCreateFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("new.txt"),
                "hello"),
            AgentActionKind.ReplaceFile => new AgentReplaceFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("existing.txt"),
                AgentContentRevision.FromUtf8Text("before"),
                "after"),
            AgentActionKind.DeleteFile => new AgentDeleteFileActionPayload(
                AgentWorkspaceRelativePath.Normalize("existing.txt"),
                AgentContentRevision.FromUtf8Text("before")),
            AgentActionKind.ExecuteCommand => new AgentExecuteCommandActionPayload(
                "dotnet",
                new[] { "build" },
                AgentWorkspaceRelativePath.Normalize(".")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported kind."),
        };
}
