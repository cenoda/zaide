using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Zaide.Features.Conversations.Application;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Conversations.Infrastructure;

/// <summary>
/// Application-lifetime file persistence for the conversation workspace snapshot.
/// Loads synchronously on construction; debounced atomic saves on mutation.
/// </summary>
internal sealed class ConversationPersistenceService : IDisposable
{
    private readonly ConversationStore _store;
    private readonly IConversationWorkspacePersistenceBridge? _bridge;
    private readonly string _storePath;
    private readonly string _lastKnownGoodPath;
    private readonly string _tempPath;
    private readonly object _saveGate = new();
    private readonly Timer _debounceTimer;
    private volatile bool _persistWritesEnabled = true;
    private volatile bool _saveScheduled;
    private bool _disposed;

    public ConversationPersistenceLoadResult LoadResult { get; }

    internal ConversationPersistenceService(
        IConversationStore store,
        IConversationWorkspacePersistenceBridge? bridge = null)
        : this(
            store,
            bridge,
            ConversationStorePathResolver.GetStorePath(),
            ConversationStorePathResolver.GetLastKnownGoodPath(),
            ConversationStorePathResolver.GetTempPath())
    {
    }

    internal ConversationPersistenceService(
        IConversationStore store,
        IConversationWorkspacePersistenceBridge? bridge,
        string storePath,
        string lastKnownGoodPath,
        string tempPath)
    {
        if (store is not ConversationStore conversationStore)
        {
            throw new ArgumentException(
                "Conversation persistence requires the production ConversationStore implementation.",
                nameof(store));
        }

        _store = conversationStore;
        _bridge = bridge;
        _storePath = storePath;
        _lastKnownGoodPath = lastKnownGoodPath;
        _tempPath = tempPath;

        LoadResult = LoadAndHydrate();

        _store.EntryAppended += OnStoreChanged;
        if (_bridge is not null)
        {
            _bridge.PresentationStateChanged += OnPresentationChanged;
        }

        _debounceTimer = new Timer(
            _ => FlushScheduledSave(),
            state: null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    internal void RequestSave()
    {
        if (!_persistWritesEnabled || _disposed)
        {
            return;
        }

        _saveScheduled = true;
        _debounceTimer.Change(TimeSpan.FromMilliseconds(250), Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _store.EntryAppended -= OnStoreChanged;
        if (_bridge is not null)
        {
            _bridge.PresentationStateChanged -= OnPresentationChanged;
        }

        _debounceTimer.Dispose();
        if (_saveScheduled)
        {
            SaveSnapshot();
        }
    }

    private void OnStoreChanged(ConversationId conversationId, ConversationEntry entry) =>
        RequestSave();

    private void OnPresentationChanged() => RequestSave();

    private void FlushScheduledSave()
    {
        if (!_saveScheduled || _disposed)
        {
            return;
        }

        lock (_saveGate)
        {
            if (!_saveScheduled)
            {
                return;
            }

            _saveScheduled = false;
            SaveSnapshot();
        }
    }

    private ConversationPersistenceLoadResult LoadAndHydrate()
    {
        var snapshot = TryLoadFrom(_storePath, out var result);
        if (snapshot is null && result == ConversationPersistenceLoadResult.Corrupt)
        {
            snapshot = TryLoadFrom(_lastKnownGoodPath, out _);
        }

        if (snapshot is null)
        {
            if (result == ConversationPersistenceLoadResult.UnsupportedVersion)
            {
                _persistWritesEnabled = false;
            }

            return result;
        }

        var conversations = ConversationSnapshotSerializer.ToDomainConversations(snapshot);
        _store.RestoreFromPersistence(conversations);
        _bridge?.ApplyRestoredSnapshot(snapshot);

        if (result == ConversationPersistenceLoadResult.Loaded)
        {
            SaveLastKnownGood(snapshot);
        }

        return result;
    }

    private ConversationWorkspaceSnapshot? TryLoadFrom(
        string path,
        out ConversationPersistenceLoadResult result)
    {
        if (!File.Exists(path))
        {
            result = ConversationPersistenceLoadResult.Missing;
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var snapshot = ConversationSnapshotSerializer.Deserialize(json, out var unsupported);
            if (unsupported)
            {
                result = ConversationPersistenceLoadResult.UnsupportedVersion;
                return null;
            }

            if (snapshot is null)
            {
                result = ConversationPersistenceLoadResult.Corrupt;
                return null;
            }

            result = ConversationPersistenceLoadResult.Loaded;
            return snapshot;
        }
        catch (IOException)
        {
            result = ConversationPersistenceLoadResult.Corrupt;
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            result = ConversationPersistenceLoadResult.Corrupt;
            return null;
        }
    }

    private void SaveSnapshot()
    {
        if (!_persistWritesEnabled)
        {
            return;
        }

        try
        {
            var conversations = _store.ListConversations();
            var snapshot = _bridge is not null
                ? _bridge.CapturePresentationSnapshot(conversations)
                : ConversationSnapshotSerializer.FromDomain(
                    conversations,
                    Array.Empty<PersistedChannelSnapshot>(),
                    activeConversationId: null,
                    drafts: new Dictionary<string, string>(),
                    lastReadEntryIds: new Dictionary<string, string>());

            var json = ConversationSnapshotSerializer.Serialize(snapshot);
            var dir = Path.GetDirectoryName(_storePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_tempPath, json);
            File.Move(_tempPath, _storePath, overwrite: true);
            File.WriteAllText(_lastKnownGoodPath, json);
        }
        catch (IOException)
        {
            // Best-effort — persistence must not break the shell.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort — persistence must not break the shell.
        }
    }

    private void SaveLastKnownGood(ConversationWorkspaceSnapshot snapshot)
    {
        try
        {
            var json = ConversationSnapshotSerializer.Serialize(snapshot);
            var dir = Path.GetDirectoryName(_lastKnownGoodPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_lastKnownGoodPath, json);
        }
        catch
        {
            // Best-effort.
        }
    }
}
