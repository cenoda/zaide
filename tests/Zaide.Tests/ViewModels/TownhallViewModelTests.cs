using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using Xunit;
using Zaide.Models;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

public class TownhallViewModelTests
{
    private static TownhallViewModel CreateViewModel()
    {
        var state = new TownhallState();
        return new TownhallViewModel(state);
    }

    /// <summary>
    /// Verifies that initial sample data is loaded correctly.
    /// </summary>
    [Fact]
    public void InitialState_SampleChannelsExist()
    {
        var vm = CreateViewModel();

        // Should have at least 3 channels
        Assert.True(vm.Channels.Count >= 3);

        // Verify channel properties exist
        var firstChannel = vm.Channels[0];
        Assert.NotNull(firstChannel.Id);
        Assert.NotNull(firstChannel.Name);
        Assert.False(string.IsNullOrEmpty(firstChannel.Id));
    }

    /// <summary>
    /// Verifies that at least one channel is active by default.
    /// </summary>
    [Fact]
    public void InitialState_OneChannelIsActive()
    {
        var vm = CreateViewModel();

        // Should have an active channel set
        Assert.NotNull(vm.ActiveChannelId);
    }

    /// <summary>
    /// Verifies that selecting a channel changes the active state.
    /// </summary>
    [Fact]
    public void SelectChannel_ChangesActiveChannel()
    {
        var vm = CreateViewModel();

        // Get initial active channel
        var initialActiveChannelId = vm.ActiveChannelId;
        Assert.NotNull(initialActiveChannelId);

        // Find a different channel to select
        string? otherChannelId = null;
        foreach (var channel in vm.Channels)
        {
            if (channel.Id != initialActiveChannelId)
            {
                otherChannelId = channel.Id;
                break;
            }
        }

        Assert.NotNull(otherChannelId);

        // Select the different channel
        vm.SelectChannelCommand.Execute(otherChannelId).Subscribe();

        // Verify active channel has changed
        Assert.Equal(otherChannelId, vm.ActiveChannelId);
        Assert.NotEqual(initialActiveChannelId, vm.ActiveChannelId);
    }

    /// <summary>
    /// Verifies that selecting a channel updates Channel.IsActive flags.
    /// </summary>
    [Fact]
    public void SelectChannel_UpdatesIsActiveFlags()
    {
        var vm = CreateViewModel();

        // Get initial active channel
        var initialActiveId = vm.ActiveChannelId;
        Assert.NotNull(initialActiveId);

        // Find a different channel to select
        string? otherChannelId = null;
        foreach (var channel in vm.Channels)
        {
            if (channel.Id != initialActiveId)
            {
                otherChannelId = channel.Id;
                break;
            }
        }

        Assert.NotNull(otherChannelId);

        // Before selection: verify active channel has IsActive=true, others false
        var initialChannel = vm.Channels.FirstOrDefault(c => c.Id == initialActiveId);
        Assert.True(initialChannel!.IsActive);
        
        var otherChannel = vm.Channels.FirstOrDefault(c => c.Id == otherChannelId);
        Assert.False(otherChannel!.IsActive);

        // Select the different channel
        vm.SelectChannelCommand.Execute(otherChannelId).Subscribe();

        // After selection: verify active state has flipped
        Assert.Equal(otherChannelId, vm.ActiveChannelId);
        Assert.False(vm.Channels.FirstOrDefault(c => c.Id == initialActiveId)!.IsActive);
        Assert.True(vm.Channels.FirstOrDefault(c => c.Id == otherChannelId)!.IsActive);
    }

    /// <summary>
    /// Verifies that sending a message appends it to the messages collection.
    /// </summary>
    [Fact]
    public void SendMessage_AppendsToMessages()
    {
        var vm = CreateViewModel();

        // Get initial count (should be 2 for townhall-main channel)
        var initialCount = vm.Messages.Count;
        Assert.Equal(2, initialCount);  // Two sample messages in townhall-main

        // Set draft text and send
        vm.DraftText = "Test message";
        vm.SendMessageCommand.Execute().Subscribe();

        // Verify message was added
        Assert.Equal(initialCount + 1, vm.Messages.Count);

        // Verify the last message has correct properties
        var lastMessage = vm.Messages[vm.Messages.Count - 1];
        Assert.Equal("Test message", lastMessage.Content);
        Assert.NotNull(lastMessage.Id);
    }

    /// <summary>
    /// Verifies that sending a message clears the draft text.
    /// </summary>
    [Fact]
    public void SendMessage_ClearsDraft()
    {
        var vm = CreateViewModel();

        // Set draft text
        vm.DraftText = "Test message";

        // Send message
        vm.SendMessageCommand.Execute().Subscribe();

        // Verify draft is cleared
        Assert.Empty(vm.DraftText);
    }

    /// <summary>
    /// Verifies that empty or whitespace-only draft does not send a message.
    /// </summary>
    [Fact]
    public void EmptyDraft_DoesNotSend()
    {
        var vm = CreateViewModel();

        // Get initial count
        var initialCount = vm.Messages.Count;

        // Send with empty draft
        vm.DraftText = "";
        vm.SendMessageCommand.Execute().Subscribe();

        // No new message should be added
        Assert.Equal(initialCount, vm.Messages.Count);

        // Also test whitespace-only
        vm.DraftText = "   ";
        vm.SendMessageCommand.Execute().Subscribe();

        // Still no new message
        Assert.Equal(initialCount, vm.Messages.Count);
    }

    /// <summary>
    /// Verifies that sending multiple messages appends them all.
    /// </summary>
    [Fact]
    public void SendMessage_MultipleMessages_AreAllPresent()
    {
        var vm = CreateViewModel();

        // Get initial count
        var initialCount = vm.Messages.Count;

        // Send first message
        vm.DraftText = "First message";
        vm.SendMessageCommand.Execute().Subscribe();

        // Verify first message was added
        Assert.Equal(initialCount + 1, vm.Messages.Count);
        Assert.Equal("First message", vm.Messages[initialCount].Content);

        // Send second message
        vm.DraftText = "Second message";
        vm.SendMessageCommand.Execute().Subscribe();

        // Verify both messages exist with correct content
        Assert.Equal(initialCount + 2, vm.Messages.Count);
        Assert.Equal("First message", vm.Messages[initialCount].Content);
        Assert.Equal("Second message", vm.Messages[initialCount + 1].Content);
    }

    /// <summary>
    /// Verifies that channel selection can be done multiple times.
    /// </summary>
    [Fact]
    public void SelectChannel_MultipleSelections_WorkCorrectly()
    {
        var vm = CreateViewModel();

        // Get initial active channel
        var initialId = vm.ActiveChannelId;
        Assert.NotNull(initialId);

        // Find a second channel
        string? channel1Id = null;
        foreach (var channel in vm.Channels)
        {
            if (channel.Id != initialId)
            {
                channel1Id = channel.Id;
                break;
            }
        }

        Assert.NotNull(channel1Id);
        vm.SelectChannelCommand.Execute(channel1Id).Subscribe();
        Assert.Equal(channel1Id, vm.ActiveChannelId);

        // Find a third channel
        string? channel2Id = null;
        foreach (var channel in vm.Channels)
        {
            if (channel.Id != initialId && channel.Id != channel1Id)
            {
                channel2Id = channel.Id;
                break;
            }
        }

        Assert.NotNull(channel2Id);
        vm.SelectChannelCommand.Execute(channel2Id).Subscribe();
        Assert.Equal(channel2Id, vm.ActiveChannelId);

        // Go back to first
        vm.SelectChannelCommand.Execute(initialId!).Subscribe();
        Assert.Equal(initialId, vm.ActiveChannelId);
    }

    /// <summary>
    /// Verifies that DraftText property syncs with TownhallState.
    /// </summary>
    [Fact]
    public void DraftText_SyncsToState()
    {
        var state = new TownhallState();
        var vm = new TownhallViewModel(state);

        // Set draft text via ViewModel
        vm.DraftText = "Test draft";

        // Verify it's synced to state
        Assert.Equal("Test draft", state.DraftText);
    }

    /// <summary>
    /// Verifies that channel switching properly maintains per-channel messages with different content.
    /// </summary>
    [Fact]
    public void SelectChannel_SwitchesMessagesToActiveChannel()
    {
        var vm = CreateViewModel();

        // Get initial active channel and its message count/content
        var initialActiveId = vm.ActiveChannelId;
        Assert.NotNull(initialActiveId);
        
        var initialMessageCount = vm.Messages.Count;
        Assert.True(initialMessageCount > 0, "Initial active channel should have messages");

        // Verify initial channel has specific messages (townhall-main has 2 messages)
        Assert.Equal(2, initialMessageCount);
        var firstMsgContent = vm.Messages[0].Content;
        Assert.Contains("Townhall workspace", firstMsgContent);

        // Switch to another channel that has different messages (ai-status has 1 message)
        string? otherChannelId = null;
        foreach (var channel in vm.Channels)
        {
            if (channel.Id != initialActiveId)
            {
                otherChannelId = channel.Id;
                break;
            }
        }

        Assert.NotNull(otherChannelId);
        
        // Execute the switch
        vm.SelectChannelCommand.Execute(otherChannelId).Subscribe();
        
        // Verify active channel changed
        Assert.Equal(otherChannelId, vm.ActiveChannelId);

        // Verify Messages collection now references the other channel's messages
        // ai-status channel should have 1 message with "System check complete"
        Assert.Equal(1, vm.Messages.Count);
        Assert.Contains("System check complete", vm.Messages[0].Content);

        // Switch back to initial channel and verify messages are restored
        vm.SelectChannelCommand.Execute(initialActiveId).Subscribe();
        Assert.Equal(initialActiveId, vm.ActiveChannelId);
        Assert.Equal(2, vm.Messages.Count);
        Assert.Contains("Townhall workspace", vm.Messages[0].Content);
    }
}
