using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using Zaide.Models;

namespace Zaide.Tests.Models;

public class TownhallMessageTests
{
    [Fact]
    public void DefaultConstructor_SetsKindToChat()
    {
        var msg = new TownhallMessage();

        Assert.Equal(TownhallMessageKind.Chat, msg.Kind);
    }

    [Fact]
    public void DefaultConstructor_SourceProviderIsNull()
    {
        var msg = new TownhallMessage();

        Assert.Null(msg.SourceProvider);
    }

    [Fact]
    public void DefaultConstructor_SourceModelIsNull()
    {
        var msg = new TownhallMessage();

        Assert.Null(msg.SourceModel);
    }

    [Fact]
    public void DefaultConstructor_ThreadIdIsNull()
    {
        var msg = new TownhallMessage();

        Assert.Null(msg.ThreadId);
    }

    [Fact]
    public void DefaultConstructor_MetadataIsNull()
    {
        var msg = new TownhallMessage();

        Assert.Null(msg.Metadata);
    }

    [Fact]
    public void CanSetAndReadChatKind()
    {
        var msg = new TownhallMessage { Kind = TownhallMessageKind.Chat };

        Assert.Equal(TownhallMessageKind.Chat, msg.Kind);
    }

    [Fact]
    public void CanSetAndReadChannelEventKind()
    {
        var msg = new TownhallMessage { Kind = TownhallMessageKind.ChannelEvent };

        Assert.Equal(TownhallMessageKind.ChannelEvent, msg.Kind);
    }

    [Fact]
    public void CanSetAndReadAgentActionKind()
    {
        var msg = new TownhallMessage { Kind = TownhallMessageKind.AgentAction };

        Assert.Equal(TownhallMessageKind.AgentAction, msg.Kind);
    }

    [Fact]
    public void CanSetAndReadAgentThinkKind()
    {
        var msg = new TownhallMessage { Kind = TownhallMessageKind.AgentThink };

        Assert.Equal(TownhallMessageKind.AgentThink, msg.Kind);
    }

    [Fact]
    public void CanSetAndReadToolCallKind()
    {
        var msg = new TownhallMessage { Kind = TownhallMessageKind.ToolCall };

        Assert.Equal(TownhallMessageKind.ToolCall, msg.Kind);
    }

    [Fact]
    public void CanSetAndReadToolResultKind()
    {
        var msg = new TownhallMessage { Kind = TownhallMessageKind.ToolResult };

        Assert.Equal(TownhallMessageKind.ToolResult, msg.Kind);
    }

    [Fact]
    public void CanSetAndReadAgentErrorKind()
    {
        var msg = new TownhallMessage { Kind = TownhallMessageKind.AgentError };

        Assert.Equal(TownhallMessageKind.AgentError, msg.Kind);
    }

    [Fact]
    public void CanSetAndReadSystemKind()
    {
        var msg = new TownhallMessage { Kind = TownhallMessageKind.System };

        Assert.Equal(TownhallMessageKind.System, msg.Kind);
    }

    /// <summary>
    /// Verifies that all eight enum values are distinct.
    /// </summary>
    [Fact]
    public void AllKinds_AreDistinct()
    {
        var values = Enum.GetValues<TownhallMessageKind>();

        Assert.Equal(8, values.Length);
        Assert.Contains(TownhallMessageKind.Chat, values);
        Assert.Contains(TownhallMessageKind.ChannelEvent, values);
        Assert.Contains(TownhallMessageKind.AgentAction, values);
        Assert.Contains(TownhallMessageKind.AgentThink, values);
        Assert.Contains(TownhallMessageKind.ToolCall, values);
        Assert.Contains(TownhallMessageKind.ToolResult, values);
        Assert.Contains(TownhallMessageKind.AgentError, values);
        Assert.Contains(TownhallMessageKind.System, values);
    }

    [Fact]
    public void CanSetAndReadSourceProvider()
    {
        var msg = new TownhallMessage { SourceProvider = "openai" };

        Assert.Equal("openai", msg.SourceProvider);
    }

    [Fact]
    public void CanSetAndReadSourceModel()
    {
        var msg = new TownhallMessage { SourceModel = "gpt-4" };

        Assert.Equal("gpt-4", msg.SourceModel);
    }

    [Fact]
    public void CanSetAndReadThreadId()
    {
        var msg = new TownhallMessage { ThreadId = "thread-abc-123" };

        Assert.Equal("thread-abc-123", msg.ThreadId);
    }

    [Fact]
    public void CanSetAndReadMetadata()
    {
        var metadata = new Dictionary<string, string>
        {
            ["token_count"] = "412",
            ["tool_call_id"] = "call_abc"
        };

        var msg = new TownhallMessage { Metadata = metadata };

        Assert.NotNull(msg.Metadata);
        Assert.Equal("412", msg.Metadata["token_count"]);
        Assert.Equal("call_abc", msg.Metadata["tool_call_id"]);
    }

    [Fact]
    public void SourceProvider_CanBeSetToNull()
    {
        var msg = new TownhallMessage { SourceProvider = "openai" };
        msg.SourceProvider = null;

        Assert.Null(msg.SourceProvider);
    }

    [Fact]
    public void SourceModel_CanBeSetToNull()
    {
        var msg = new TownhallMessage { SourceModel = "gpt-4" };
        msg.SourceModel = null;

        Assert.Null(msg.SourceModel);
    }

    [Fact]
    public void ThreadId_CanBeSetToNull()
    {
        var msg = new TownhallMessage { ThreadId = "thread-abc" };
        msg.ThreadId = null;

        Assert.Null(msg.ThreadId);
    }

    [Fact]
    public void Metadata_CanBeSetToNull()
    {
        var msg = new TownhallMessage
        {
            Metadata = new Dictionary<string, string> { ["k"] = "v" }
        };
        msg.Metadata = null;

        Assert.Null(msg.Metadata);
    }

    [Fact]
    public void ExistingFields_StillWork()
    {
        var now = DateTimeOffset.UtcNow;
        var msg = new TownhallMessage
        {
            Id = "test-id",
            SenderId = "sender-1",
            SenderName = "Test Sender",
            SenderAvatar = "avatar-test",
            Content = "Hello",
            Timestamp = now
        };

        Assert.Equal("test-id", msg.Id);
        Assert.Equal("sender-1", msg.SenderId);
        Assert.Equal("Test Sender", msg.SenderName);
        Assert.Equal("avatar-test", msg.SenderAvatar);
        Assert.Equal("Hello", msg.Content);
        Assert.Equal(now, msg.Timestamp);
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesKindAndNewFields()
    {
        var metadata = new Dictionary<string, string>
        {
            ["token_count"] = "99"
        };

        var msg = new TownhallMessage
        {
            Id = "msg-serial-1",
            SenderId = "agent-1",
            SenderName = "Agent",
            SenderAvatar = "avatar-agent",
            Content = "Round-trip test",
            Timestamp = DateTimeOffset.UtcNow,
            Kind = TownhallMessageKind.ToolCall,
            SourceProvider = "anthropic",
            SourceModel = "claude-3.5-sonnet",
            ThreadId = "thread-roundtrip",
            Metadata = metadata
        };

        var json = JsonSerializer.Serialize(msg);
        var deserialized = JsonSerializer.Deserialize<TownhallMessage>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(msg.Id, deserialized.Id);
        Assert.Equal(msg.SenderId, deserialized.SenderId);
        Assert.Equal(msg.SenderName, deserialized.SenderName);
        Assert.Equal(msg.SenderAvatar, deserialized.SenderAvatar);
        Assert.Equal(msg.Content, deserialized.Content);
        Assert.Equal(msg.Kind, deserialized.Kind);
        Assert.Equal(msg.SourceProvider, deserialized.SourceProvider);
        Assert.Equal(msg.SourceModel, deserialized.SourceModel);
        Assert.Equal(msg.ThreadId, deserialized.ThreadId);
        Assert.NotNull(deserialized.Metadata);
        Assert.Equal("99", deserialized.Metadata["token_count"]);
    }
}
