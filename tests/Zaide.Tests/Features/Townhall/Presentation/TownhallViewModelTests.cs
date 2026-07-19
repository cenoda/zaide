using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using Xunit;
using Zaide.Tests.Features.Conversations;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.Tests.Features.Townhall.Presentation;

public class TownhallViewModelTests
{
    private static TownhallViewModel CreateViewModel()
    {
        var state = new TownhallState();
        return ConversationsTestSupport.CreateTownhallViewModel(state);
    }

    /// <summary>
    /// Verifies that initial session seed state is loaded correctly.
    /// </summary>
    [Fact]
    public void InitialState_SeedChannelsExist()
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

        // Get initial count (should be 0 for townhall-main channel)
        var initialCount = vm.Messages.Count;
        Assert.Equal(0, initialCount);  // No seeded starter messages; channels and agents are structural seed state

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
        var vm = ConversationsTestSupport.CreateTownhallViewModel(state);

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
        Assert.Equal(0, initialMessageCount);

        // Switch to another channel; the switch itself logs a ChannelEvent
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
        // ai-status channel starts empty; the switch logs 1 ChannelEvent
        Assert.Single(vm.Messages);
        Assert.Equal(TownhallMessageKind.ChannelEvent, vm.Messages[0].Kind);

        // Switch back to initial channel and verify messages are restored
        vm.SelectChannelCommand.Execute(initialActiveId).Subscribe();
        Assert.Equal(initialActiveId, vm.ActiveChannelId);
        // townhall-main starts empty; the switch logs 1 ChannelEvent
        Assert.Single(vm.Messages);
        Assert.Equal(TownhallMessageKind.ChannelEvent, vm.Messages[0].Kind);
    }

    /// <summary>
    /// Verifies that sending a message produces an entry with Kind == Chat.
    /// </summary>
    [Fact]
    public void SendMessage_ProducesChatKindEntry()
    {
        var vm = CreateViewModel();
        vm.DraftText = "Hello from test";
        vm.SendMessageCommand.Execute().Subscribe();

        var last = vm.Messages[vm.Messages.Count - 1];
        Assert.Equal(TownhallMessageKind.Chat, last.Kind);
        Assert.Equal("Hello from test", last.Content);
    }

    /// <summary>
    /// Verifies that switching channels produces a ChannelEvent entry in the new channel.
    /// </summary>
    [Fact]
    public void SelectChannel_ProducesChannelEventEntry()
    {
        var vm = CreateViewModel();
        var initialId = vm.ActiveChannelId;
        Assert.NotNull(initialId);

        string? otherId = vm.Channels.FirstOrDefault(c => c.Id != initialId)?.Id;
        Assert.NotNull(otherId);

        var targetChannel = vm.Channels.First(c => c.Id == otherId);
        vm.SelectChannelCommand.Execute(otherId).Subscribe();

        // The event is appended to the newly active channel's collection
        var messagesInNew = vm.Messages; // now points to other
        var last = messagesInNew[messagesInNew.Count - 1];
        Assert.Equal(TownhallMessageKind.ChannelEvent, last.Kind);
        Assert.Equal($"Switched to #{targetChannel.Name}", last.Content);
        Assert.Equal("user-1", last.SenderId);
        Assert.Equal("User", last.SenderName);
    }

    /// <summary>
    /// Verifies that switching to the same channel does not produce a duplicate entry.
    /// </summary>
    [Fact]
    public void SelectChannel_SameChannel_NoDuplicateEntry()
    {
        var vm = CreateViewModel();
        var initialId = vm.ActiveChannelId;
        Assert.NotNull(initialId);

        var initialCount = vm.Messages.Count;

        // Re-select same channel (setter no-ops)
        vm.SelectChannelCommand.Execute(initialId).Subscribe();

        Assert.Equal(initialCount, vm.Messages.Count);
    }

    /// <summary>
    /// Verifies that FilterMode.ChatOnly yields only Chat-kind entries from the seeded
    /// townhall-main channel (0 Chat messages initially).
    /// </summary>
    [Fact]
    public void FilterMode_ChatOnly_YieldsOnlyChatEntries()
    {
        var vm = CreateViewModel();

        IReadOnlyList<TownhallMessage>? latest = null;
        using var sub = vm.FilteredMessages.Subscribe(list => latest = list);

        vm.FilterMode = FilterMode.ChatOnly;

        Assert.NotNull(latest);
        Assert.Empty(latest!);
        Assert.All(latest, m => Assert.Equal(TownhallMessageKind.Chat, m.Kind));
    }

    /// <summary>
    /// Verifies that FilterMode.ActivityOnly yields only non-Chat entries.
    /// The seeded townhall-main channel has no non-Chat entries initially, so this
    /// triggers a ChannelEvent by switching away and back, then filters for it.
    /// </summary>
    [Fact]
    public void FilterMode_ActivityOnly_YieldsOnlyNonChatEntries()
    {
        var vm = CreateViewModel();
        var initialId = vm.ActiveChannelId;
        Assert.NotNull(initialId);

        var otherId = vm.Channels.First(c => c.Id != initialId).Id;

        // Switch away and back to generate ChannelEvent entries in the initial channel.
        vm.SelectChannelCommand.Execute(otherId).Subscribe();
        vm.SelectChannelCommand.Execute(initialId!).Subscribe();

        IReadOnlyList<TownhallMessage>? latest = null;
        using var sub = vm.FilteredMessages.Subscribe(list => latest = list);

        vm.FilterMode = FilterMode.ActivityOnly;

        Assert.NotNull(latest);
        Assert.NotEmpty(latest!);
        Assert.All(latest, m => Assert.NotEqual(TownhallMessageKind.Chat, m.Kind));
    }

    /// <summary>
    /// Verifies that FilterMode.All yields every message in the active channel.
    /// </summary>
    [Fact]
    public void FilterMode_All_YieldsAllEntries()
    {
        var vm = CreateViewModel();

        IReadOnlyList<TownhallMessage>? latest = null;
        using var sub = vm.FilteredMessages.Subscribe(list => latest = list);

        vm.FilterMode = FilterMode.ChatOnly;
        vm.FilterMode = FilterMode.All;

        Assert.NotNull(latest);
        Assert.Equal(vm.Messages.Count, latest!.Count);
    }

    /// <summary>
    /// Verifies that switching channels does not leave a stale CollectionChanged subscription
    /// on the previously active channel's collection (single top-level Switch() fix).
    /// Appending to the old channel's collection after switching away should not cause
    /// FilteredMessages to re-fire.
    /// </summary>
    [Fact]
    public void FilteredMessages_SwitchingChannels_DoesNotLeakStaleSubscription()
    {
        var vm = CreateViewModel();
        var initialId = vm.ActiveChannelId;
        Assert.NotNull(initialId);

        var initialMessages = vm.Messages;
        var otherId = vm.Channels.First(c => c.Id != initialId).Id;

        vm.SelectChannelCommand.Execute(otherId).Subscribe();

        var fireCount = 0;
        using var sub = vm.FilteredMessages.Subscribe(_ => fireCount++);
        var countAfterSubscribe = fireCount;

        // Mutate the stale (previously active) collection directly; this should NOT
        // trigger FilteredMessages since only the current channel's collection is observed.
        initialMessages.Add(new TownhallMessage
        {
            Id = "stale-msg",
            SenderId = "user-1",
            SenderName = "User",
            SenderAvatar = "avatar-user",
            Content = "Stale append",
            Timestamp = DateTimeOffset.UtcNow,
            Kind = TownhallMessageKind.Chat
        });

        Assert.Equal(countAfterSubscribe, fireCount);
    }

    /// <summary>
    /// Verifies that AddMirroredActivity with kind=Chat appends a Chat entry to the active channel.
    /// </summary>
    [Fact]
    public void AddMirroredActivity_ChatKind_AppendsChatEntry()
    {
        var vm = CreateViewModel();
        var initialCount = vm.Messages.Count;

        vm.AddMirroredActivity(TownhallMessageKind.Chat, "Hello from agent panel",
            senderId: "agent-1", senderName: "Zaide Agent");

        Assert.Equal(initialCount + 1, vm.Messages.Count);
        var last = vm.Messages[vm.Messages.Count - 1];
        Assert.Equal(TownhallMessageKind.Chat, last.Kind);
        Assert.Equal("Hello from agent panel", last.Content);
        Assert.Equal("agent-1", last.SenderId);
        Assert.Equal("Zaide Agent", last.SenderName);
    }

    /// <summary>
    /// Verifies that AddMirroredActivity with kind=AgentError appends an AgentError entry.
    /// </summary>
    [Fact]
    public void AddMirroredActivity_AgentErrorKind_AppendsAgentErrorEntry()
    {
        var vm = CreateViewModel();
        var initialCount = vm.Messages.Count;

        vm.AddMirroredActivity(TownhallMessageKind.AgentError, "Request failed: timeout",
            senderId: "agent-1", senderName: "Zaide Agent");

        Assert.Equal(initialCount + 1, vm.Messages.Count);
        var last = vm.Messages[vm.Messages.Count - 1];
        Assert.Equal(TownhallMessageKind.AgentError, last.Kind);
        Assert.Equal("Request failed: timeout", last.Content);
        Assert.Equal("agent-1", last.SenderId);
        Assert.Equal("Zaide Agent", last.SenderName);
        Assert.Equal("avatar-agent", last.SenderAvatar);
    }

    /// <summary>
    /// Verifies that AddMirroredActivity targets the currently active channel.
    /// After switching channels, the entry appears in the new active channel, not the old one.
    /// </summary>
    [Fact]
    public void AddMirroredActivity_TargetsActiveChannel()
    {
        var vm = CreateViewModel();
        var initialId = vm.ActiveChannelId;
        Assert.NotNull(initialId);

        // Send a mirrored activity on the initial channel
        vm.AddMirroredActivity(TownhallMessageKind.Chat, "Message on initial channel",
            senderId: "user-1", senderName: "User");

        // Switch to another channel
        var otherId = vm.Channels.First(c => c.Id != initialId).Id;
        vm.SelectChannelCommand.Execute(otherId).Subscribe();

        // Send a mirrored activity on the other channel
        vm.AddMirroredActivity(TownhallMessageKind.Chat, "Message on other channel",
            senderId: "user-1", senderName: "User");

        // The other channel's messages should contain the second message
        var otherMessages = vm.Messages;
        var lastOnOther = otherMessages[otherMessages.Count - 1];
        Assert.Equal("Message on other channel", lastOnOther.Content);

        // Switch back to initial channel and verify the first message is still there
        vm.SelectChannelCommand.Execute(initialId!).Subscribe();
        var initialMessages = vm.Messages;
        Assert.Contains(initialMessages, m => m.Content == "Message on initial channel");
    }

    /// <summary>
    /// Verifies that AddMirroredActivity with kind=Chat uses "avatar-user" for user senderId
    /// and "avatar-agent" for non-user senderId.
    /// </summary>
    [Fact]
    public void AddMirroredActivity_SetsCorrectAvatarBySender()
    {
        var vm = CreateViewModel();

        // User sender
        vm.AddMirroredActivity(TownhallMessageKind.Chat, "User message",
            senderId: "user-1", senderName: "User");
        var userMsg = vm.Messages[vm.Messages.Count - 1];
        Assert.Equal("avatar-user", userMsg.SenderAvatar);

        // Agent sender
        vm.AddMirroredActivity(TownhallMessageKind.Chat, "Agent message",
            senderId: "agent-5", senderName: "Some Agent");
        var agentMsg = vm.Messages[vm.Messages.Count - 1];
        Assert.Equal("avatar-agent", agentMsg.SenderAvatar);
    }

    /// <summary>
    /// Verifies that sending a message while ChatOnly filter is active correctly updates FilteredMessages.
    /// This tests the CollectionChanged reactivity fix.
    /// </summary>
    [Fact]
    public void SendMessage_WhileChatOnlyFilter_UpdatesFilteredList()
    {
        var vm = CreateViewModel();
        vm.FilterMode = FilterMode.ChatOnly;
        var initialFilteredCount = 0;
        // Subscribe briefly to capture
        using var sub = vm.FilteredMessages.Subscribe(list => { initialFilteredCount = list.Count; });
        var beforeSend = initialFilteredCount;
        vm.DraftText = "Filtered test message";
        vm.SendMessageCommand.Execute().Subscribe();
        // After send, filtered should have increased by 1 (new Chat entry)
        var after = 0;
        using var sub2 = vm.FilteredMessages.Subscribe(list => { after = list.Count; });
        Assert.Equal(beforeSend + 1, after);
    }

    /// <summary>
    /// Reproduces the Phase 6 smoke-test bug: a mirrored activity added to the
    /// active channel (e.g. an agent-panel send) must refresh FilteredMessages
    /// without any channel switch or filter change. Previously the initial
    /// active channel's CollectionChanged was never subscribed, so the chat
    /// panel only updated after the user switched a tab/filter.
    /// </summary>
    [Fact]
    public void MirroredActivity_UpdatesFilteredMessages_WithoutTabOrFilterChange()
    {
        var vm = CreateViewModel();

        IReadOnlyList<TownhallMessage>? latest = null;
        using var sub = vm.FilteredMessages.Subscribe(list => latest = list);

        Assert.NotNull(latest);
        var beforeCount = latest!.Count;

        // Mirror an activity exactly as MainWindowViewModel.SendAgentMessageAsync does
        // for an agent-panel send — no channel switch, no filter change.
        vm.AddMirroredActivity(
            kind: TownhallMessageKind.Chat,
            content: "Hello from agent panel",
            senderId: "user-1",
            senderName: "User");

        Assert.NotNull(latest);
        Assert.Equal(beforeCount + 1, latest!.Count);
        Assert.Contains(latest, m => m.Content == "Hello from agent panel");
    }

}
