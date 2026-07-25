using System;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Workspace.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class Phase17ActionContractsIdentityTests
{
    [Fact]
    public void AgentActionId_New_CreatesPrefixedNonDefaultValue()
    {
        var id = AgentActionId.New();

        Assert.NotEqual(default(AgentActionId), id);
        Assert.StartsWith("action:", id.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentActionAttemptId_FromValue_RejectsBlankValue()
    {
        var exception = Assert.Throws<ArgumentException>(() => AgentActionAttemptId.FromValue(" "));
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void AgentPermissionDecisionId_FromValue_RejectsWrongPrefix()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AgentPermissionDecisionId.FromValue("decision:abc"));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void WorkspaceIdentity_FromValue_RejectsWrongPrefix()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            WorkspaceIdentity.FromValue("workspace-root"));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void WorkspaceGeneration_RejectsZero()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceGeneration(0));
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void AgentActionCorrelationKey_RejectsOversizedValue()
    {
        var oversized = new string('a', AgentActionBudgets.BackendCorrelationKeyMaxBytes + 1);
        var exception = Assert.Throws<ArgumentException>(() =>
            AgentActionCorrelationKey.FromValue(oversized));

        Assert.Equal("value", exception.ParamName);
    }
}
