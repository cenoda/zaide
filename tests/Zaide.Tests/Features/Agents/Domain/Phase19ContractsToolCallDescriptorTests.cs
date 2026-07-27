using System;
using Xunit;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class Phase19ContractsToolCallDescriptorTests
{
    [Fact]
    public void Phase19Contracts_ToolCallDescriptor_SupportsAllPhase17ActionKinds()
    {
        foreach (var kind in new[]
                 {
                     AgentActionKind.ReadFile,
                     AgentActionKind.CreateFile,
                     AgentActionKind.ReplaceFile,
                     AgentActionKind.DeleteFile,
                     AgentActionKind.ExecuteCommand,
                 })
        {
            Assert.True(NativeHarnessToolCallDescriptor.IsSupportedActionKind(kind));
        }
    }

    [Fact]
    public void Phase19Contracts_ToolCallDescriptor_RejectsUndefinedActionKind()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NativeHarnessToolCallDescriptor(
                NativeHarnessToolCallId.FromValue("call-1"),
                (AgentActionKind)999,
                "tool",
                "{}"));

        Assert.Equal("actionKind", exception.ParamName);
    }

    [Fact]
    public void Phase19Contracts_ToolCallId_RejectsBlankValue()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            NativeHarnessToolCallId.FromValue(" "));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Phase19Contracts_BackendId_DefinesNativeHarnessIdentity()
    {
        Assert.Equal("backend:zaide-native-harness", AgentBackendIds.NativeHarnessValue);
        Assert.Equal(AgentBackendIds.NativeHarnessValue, AgentBackendIds.NativeHarness.Value);
    }
}
