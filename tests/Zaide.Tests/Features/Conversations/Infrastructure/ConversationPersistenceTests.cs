using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Domain;
using Zaide.Features.Conversations.Infrastructure;
using Zaide.Features.Settings.Infrastructure;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Tests.Features.Agents;
using Zaide.Tests.Features.Conversations;

namespace Zaide.Tests.Features.Conversations.Infrastructure;

/// <summary>
/// Phase 14 M6 recovery matrix for conversation workspace persistence.
/// </summary>
public sealed class ConversationPersistenceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storePath;
    private readonly string _lastKnownGoodPath;
    private readonly string _tempPath;

    public ConversationPersistenceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZaideConvPersist_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _storePath = Path.Combine(_tempDir, "conversations.json");
        _lastKnownGoodPath = Path.Combine(_tempDir, "conversations.json.lastknowngood");
        _tempPath = Path.Combine(_tempDir, "conversations.json.tmp");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public async Task RoundTrip_PreservesConversationsDraftsUnreadAndActiveSelection()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var state = new TownhallState();
        var uiState = new TownhallConversationUiState();
        var bridge = new TownhallConversationPersistenceBridge(state, uiState);
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var coordinator = AgentExecutionTestSupport.CreateCoordinator(
            host,
            new StubExecutionService(_ => Task.FromResult(AgentExecutionResult.Success("ok"))),
            store);
        var vm = ConversationsTestSupport.CreateTownhallViewModel(
            state,
            catalog,
            store,
            host,
            coordinator,
            uiState,
            bridge);

        var channelId = vm.ActiveChannelId!;
        store.AppendEntry(
            ConversationId.ForChannel(channelId),
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                DateTimeOffset.UtcNow,
                "channel hello"));

        var agentId = vm.Agents.First(a => a.Role == "agent").ActorId;
        vm.OpenDirectConversationCommand.Execute(agentId).Subscribe();
        vm.DraftText = "saved draft";
        Assert.True(store.TryGetDirectConversation(ActorId.HumanUser, agentId, out var direct));
        store.AppendEntry(
            direct!.Id,
            ConversationEntry.UserChat(
                ConversationEntryId.New(),
                ActorId.HumanUser,
                DateTimeOffset.UtcNow,
                "dm hello"));

        vm.SelectChannelCommand.Execute(channelId).Subscribe();
        uiState.SetLastReadEntryId(
            ConversationId.ForChannel(channelId),
            store.TryGet(ConversationId.ForChannel(channelId), out var channelConversation)
                ? channelConversation!.Entries[^1].Id
                : null);

        using (var persistence = CreatePersistence(store, bridge))
        {
            persistence.RequestSave();
            await Task.Delay(400);
        }

        var reloadStore = ConversationsTestSupport.CreateStore();
        var reloadState = new TownhallState();
        var reloadUiState = new TownhallConversationUiState();
        var reloadBridge = new TownhallConversationPersistenceBridge(reloadState, reloadUiState);
        using var reloadPersistence = CreatePersistence(reloadStore, reloadBridge);
        Assert.Equal(ConversationPersistenceLoadResult.Loaded, reloadPersistence.LoadResult);

        var reloadHost = ConversationsTestSupport.CreatePanelHost(catalog, reloadStore);
        var reloadCoordinator = AgentExecutionTestSupport.CreateCoordinator(
            reloadHost,
            new StubExecutionService(_ => Task.FromResult(AgentExecutionResult.Success("ok"))),
            reloadStore);
        var reloadVm = ConversationsTestSupport.CreateTownhallViewModel(
            reloadState,
            catalog,
            reloadStore,
            reloadHost,
            reloadCoordinator,
            reloadUiState,
            reloadBridge,
            reloadPersistence);

        Assert.Equal(channelId, reloadVm.ActiveChannelId);
        Assert.True(reloadStore.TryGetChannelConversation(channelId, out var restoredChannel));
        Assert.Single(restoredChannel!.Entries);
        Assert.Equal("channel hello", restoredChannel.Entries[0].Content);
        Assert.True(reloadStore.TryGetDirectConversation(ActorId.HumanUser, agentId, out var restoredDirect));
        Assert.Single(restoredDirect!.Entries);
        Assert.Equal("saved draft", reloadUiState.GetDraft(restoredDirect.Id));
        Assert.False(reloadVm.IsDirectSendBusy);
    }

    [Fact]
    public void MissingFile_StartsWithSeedBehaviorWithoutThrow()
    {
        var store = ConversationsTestSupport.CreateStore();
        using var persistence = CreatePersistence(store, bridge: null);
        Assert.Equal(ConversationPersistenceLoadResult.Missing, persistence.LoadResult);
        Assert.Empty(store.ListConversations());

        var vm = ConversationsTestSupport.CreateTownhallViewModel(store: store);
        Assert.Equal(3, vm.Channels.Count);
        Assert.False(vm.IsDirectSendBusy);
    }

    [Fact]
    public void CorruptJson_FallsBackToLastKnownGood()
    {
        var goodSnapshot = BuildSampleSnapshot();
        var goodJson = ConversationSnapshotSerializer.Serialize(goodSnapshot);
        File.WriteAllText(_lastKnownGoodPath, goodJson);
        File.WriteAllText(_storePath, "{ not-json");

        var store = ConversationsTestSupport.CreateStore();
        using var persistence = CreatePersistence(store, bridge: null);
        Assert.Equal(ConversationPersistenceLoadResult.Corrupt, persistence.LoadResult);
        Assert.Single(store.ListConversations());
        Assert.Equal("channel-1", store.ListConversations()[0].Id.TryGetChannelId(out var id) ? id : null);
    }

    [Fact]
    public void UnsupportedFutureSchema_DoesNotOverwriteKnownGood()
    {
        var goodSnapshot = BuildSampleSnapshot();
        var goodJson = ConversationSnapshotSerializer.Serialize(goodSnapshot);
        File.WriteAllText(_lastKnownGoodPath, goodJson);
        File.WriteAllText(_storePath, """{"schemaVersion":99,"channels":[],"conversations":[]}""");

        var store = ConversationsTestSupport.CreateStore();
        using var persistence = CreatePersistence(store, bridge: null);
        Assert.Equal(ConversationPersistenceLoadResult.UnsupportedVersion, persistence.LoadResult);
        Assert.Empty(store.ListConversations());

        persistence.RequestSave();
        Thread.Sleep(400);
        Assert.Equal(goodJson.Trim(), File.ReadAllText(_lastKnownGoodPath).Trim());
        Assert.Contains("\"schemaVersion\":99", File.ReadAllText(_storePath).Replace(" ", string.Empty));
    }

    [Fact]
    public void InterruptedWrite_LeavesMainAndLastKnownGoodIntact()
    {
        var goodSnapshot = BuildSampleSnapshot();
        var goodJson = ConversationSnapshotSerializer.Serialize(goodSnapshot);
        File.WriteAllText(_storePath, goodJson);
        File.WriteAllText(_lastKnownGoodPath, goodJson);
        File.WriteAllText(_tempPath, """{"schemaVersion":1,"channels":[],"conversations":[]}""");

        var store = ConversationsTestSupport.CreateStore();
        using var persistence = CreatePersistence(store, bridge: null);
        Assert.Equal(ConversationPersistenceLoadResult.Loaded, persistence.LoadResult);
        Assert.Equal(goodJson.Trim(), File.ReadAllText(_storePath).Trim());
        Assert.Equal(goodJson.Trim(), File.ReadAllText(_lastKnownGoodPath).Trim());
    }

    [Fact]
    public async Task AtomicReplace_WritesCompleteJson()
    {
        var store = ConversationsTestSupport.CreateStore();
        store.CreateChannelConversation("channel-1");
        using var persistence = CreatePersistence(store, bridge: null);
        persistence.RequestSave();
        await Task.Delay(400);

        var json = await File.ReadAllTextAsync(_storePath);
        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.True(json.TrimEnd().EndsWith('}'));
        Assert.False(File.Exists(_tempPath));
    }

    [Fact]
    public void Reload_DoesNotDuplicateEntryIds()
    {
        var entryId = ConversationEntryId.New();
        var snapshot = BuildSampleSnapshot(entryId);
        File.WriteAllText(_storePath, ConversationSnapshotSerializer.Serialize(snapshot));

        var firstStore = ConversationsTestSupport.CreateStore();
        using (var firstPersistence = CreatePersistence(firstStore, bridge: null))
        {
            Assert.Equal(ConversationPersistenceLoadResult.Loaded, firstPersistence.LoadResult);
        }

        var secondStore = ConversationsTestSupport.CreateStore();
        using var secondPersistence = CreatePersistence(secondStore, bridge: null);
        var conversation = secondStore.ListConversations().Single();
        Assert.Single(conversation.Entries);
        Assert.Equal(entryId, conversation.Entries[0].Id);
    }

    [Fact]
    public void StorePath_IsIndependentFromSettingsPath()
    {
        var settingsPath = SettingsPathResolver.GetSettingsPath();
        var conversationPath = ConversationStorePathResolver.GetStorePath();

        Assert.NotEqual(settingsPath, conversationPath);
        Assert.DoesNotContain("settings.json", conversationPath, StringComparison.Ordinal);
        Assert.Contains("conversations", conversationPath, StringComparison.Ordinal);
    }

    private ConversationPersistenceService CreatePersistence(
        ConversationStore store,
        TownhallConversationPersistenceBridge? bridge) =>
        new(store, bridge, _storePath, _lastKnownGoodPath, _tempPath);

    private static ConversationWorkspaceSnapshot BuildSampleSnapshot(
        ConversationEntryId? entryId = null)
    {
        var channelConversationId = ConversationId.ForChannel("channel-1");
        var entry = ConversationEntry.UserChat(
            entryId ?? ConversationEntryId.New(),
            ActorId.HumanUser,
            DateTimeOffset.UtcNow,
            "hello");

        return ConversationSnapshotSerializer.FromDomain(
            new[]
            {
                Conversation.Restore(
                    channelConversationId,
                    ConversationKind.Channel,
                    ConversationParticipants.ForChannel(),
                    new[] { entry })
            },
            new[]
            {
                new PersistedChannelSnapshot
                {
                    Id = "channel-1",
                    Name = "townhall-main",
                    Pinned = true
                }
            },
            activeConversationId: channelConversationId.Value,
            drafts: new System.Collections.Generic.Dictionary<string, string>(),
            lastReadEntryIds: new System.Collections.Generic.Dictionary<string, string>());
    }

    private sealed class StubExecutionService : IAgentExecutionService
    {
        private readonly Func<string, Task<AgentExecutionResult>> _handler;

        public StubExecutionService(Func<string, Task<AgentExecutionResult>> handler) =>
            _handler = handler;

        public Task<AgentExecutionResult> ExecuteAsync(string userMessage, CancellationToken ct = default) =>
            _handler(userMessage);
    }
}
