using System;
using Xunit;
using Zaide.Features.Agents.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class Phase19ContractsLoopHistoryTests
{
    private static readonly DateTimeOffset RecordedAt = DateTimeOffset.Parse("2026-07-27T12:00:00Z");

    [Fact]
    public void Phase19Contracts_LoopHistory_Append_PreservesImmutability()
    {
        var history = NativeHarnessLoopHistory.Empty;
        var record = new NativeHarnessUserTurnRecord(turnIndex: 0, RecordedAt, "hello");

        var updated = history.Append(record);

        Assert.Equal(0, history.Count);
        Assert.Equal(1, updated.Count);
        Assert.Same(record, updated.Records[0]);
    }

    [Fact]
    public void Phase19Contracts_LoopHistory_RejectsToolResultWithoutMatchingCall()
    {
        var history = NativeHarnessLoopHistory.Empty.Append(
            new NativeHarnessUserTurnRecord(0, RecordedAt, "read file"));

        var toolCallId = NativeHarnessToolCallId.FromValue("call-1");
        var exception = Assert.Throws<InvalidOperationException>(() =>
            history.Append(
                new NativeHarnessToolResultRecord(
                    0,
                    RecordedAt,
                    toolCallId,
                    AgentActionResultKind.Succeeded,
                    "ok")));

        Assert.Contains("preceding tool call", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase19Contracts_LoopHistory_AcceptsToolCallThenResultPair()
    {
        var toolCallId = NativeHarnessToolCallId.FromValue("call-1");
        var history = NativeHarnessLoopHistory.Empty
            .Append(new NativeHarnessUserTurnRecord(0, RecordedAt, "read file"))
            .Append(
                new NativeHarnessToolCallRecord(
                    0,
                    RecordedAt,
                    toolCallId,
                    AgentActionKind.ReadFile,
                    "read_file",
                    """{"path":"src/Program.cs"}"""))
            .Append(
                new NativeHarnessToolResultRecord(
                    0,
                    RecordedAt,
                    toolCallId,
                    AgentActionResultKind.Succeeded,
                    "file contents"));

        Assert.Equal(3, history.Count);
        Assert.True(history.TryGetLatestToolCall(toolCallId, out var call));
        Assert.Equal(AgentActionKind.ReadFile, call!.ActionKind);
    }

    [Fact]
    public void Phase19Contracts_LoopHistory_RejectsDecreasingTurnIndex()
    {
        var history = NativeHarnessLoopHistory.Empty.Append(
            new NativeHarnessAssistantTurnRecord(1, RecordedAt, "done"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            history.Append(new NativeHarnessUserTurnRecord(0, RecordedAt, "late")));

        Assert.Contains("Turn index", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase19Contracts_ToolCallRecord_RejectsBlankArgumentsJson()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new NativeHarnessToolCallRecord(
                0,
                RecordedAt,
                NativeHarnessToolCallId.FromValue("call-1"),
                AgentActionKind.ReadFile,
                "read_file",
                " "));

        Assert.Equal("argumentsJson", exception.ParamName);
    }
}
