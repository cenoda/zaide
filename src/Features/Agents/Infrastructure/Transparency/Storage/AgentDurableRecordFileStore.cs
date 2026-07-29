using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Infrastructure.Transparency.Storage;

/// <summary>
/// Agents-owned file-backed durable record store with workspace isolation,
/// ordering, idempotency, replay, migration, quarantine, and fail-closed
/// multi-writer coordination.
/// </summary>
internal sealed class AgentDurableRecordFileStore : IAgentDurableRecordStore, IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableRecordMigrator _migrator;
    private readonly object _gate = new();
    private readonly Dictionary<string, WorkspacePartition> _partitions = new(StringComparer.Ordinal);
    private bool _disposed;

    public AgentDurableRecordFileStore()
        : this(
            AgentDurableRecordPathResolver.GetRootDirectory(),
            new AgentDurableRecordMigrator(new IAgentDurableRecordMigration[]
            {
                new AgentDurableRecordMigrationV0ToV1(),
            }))
    {
    }

    internal AgentDurableRecordFileStore(
        string rootDirectory,
        AgentDurableRecordMigrator migrator)
    {
        _rootDirectory = rootDirectory;
        _migrator = migrator;
    }

    public AgentDurableRecordLoadOutcome LoadWorkspace(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        ThrowIfDisposed();
        var partition = GetOrLoadPartition(workspaceKey);
        return partition.LoadOutcome;
    }

    public AgentDurableRecordAppendResult TryAppend(AgentDurableRecordAppendRequest request)
    {
        ThrowIfDisposed();
        var partition = GetOrLoadPartition(request.WorkspaceKey);

        if (!partition.WritesEnabled)
        {
            return new AgentDurableRecordAppendResult(AgentDurableRecordAppendStatus.WritesDisabled);
        }

        if (!AgentDurableRecordPartitionLock.TryAcquire(partition.LockPath, out var partitionLock))
        {
            return new AgentDurableRecordAppendResult(AgentDurableRecordAppendStatus.ContentionFailed);
        }

        using (partitionLock!)
        {
            lock (_gate)
            {
                ReloadPartitionIfNeeded(partition);

                if (!partition.WritesEnabled)
                {
                    return new AgentDurableRecordAppendResult(AgentDurableRecordAppendStatus.WritesDisabled);
                }

                if (!string.Equals(
                        partition.Index.WorkspaceKey,
                        request.WorkspaceKey.Value,
                        StringComparison.Ordinal))
                {
                    return new AgentDurableRecordAppendResult(AgentDurableRecordAppendStatus.WorkspaceMismatch);
                }

                var classKey = request.RecordClass.ToString();
                if (!partition.Index.ClassState.TryGetValue(classKey, out var classState))
                {
                    classState = new AgentDurableRecordClassState();
                    partition.Index.ClassState[classKey] = classState;
                }

                if (classState.IdempotencyKeys.Contains(request.IdempotencyKey))
                {
                    var existing = partition.Index.Records
                        .Where(r => r.RecordClass == request.RecordClass
                            && string.Equals(r.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
                        .OrderByDescending(r => r.OrderingSequence)
                        .FirstOrDefault();

                    if (existing is not null)
                    {
                        var existingPath = Path.Combine(
                            AgentDurableRecordPathResolver.GetRecordsDirectory(partition.Directory),
                            request.RecordClass.ToString(),
                            existing.FileName);
                        var existingEnvelope = TryReadEnvelope(existingPath);
                        return new AgentDurableRecordAppendResult(
                            AgentDurableRecordAppendStatus.DuplicateIgnored,
                            existingEnvelope);
                    }

                    return new AgentDurableRecordAppendResult(AgentDurableRecordAppendStatus.DuplicateIgnored);
                }

                var recordId = AgentDurableRecordId.New();
                var orderingSequence = classState.NextOrderingSequence;
                classState.NextOrderingSequence = orderingSequence + 1;
                classState.IdempotencyKeys.Add(request.IdempotencyKey);

                var envelope = new AgentDurableRecordEnvelope(
                    AgentDurableRecordPartitionIndex.CurrentSchemaVersion,
                    recordId,
                    request.RecordClass,
                    request.WorkspaceKey,
                    orderingSequence,
                    request.IdempotencyKey,
                    request.RecordedAtUtc,
                    request.ScopeReferences,
                    request.PayloadJson);

                var recordPath = AgentDurableRecordPathResolver.GetRecordPath(
                    partition.Directory,
                    request.RecordClass,
                    orderingSequence,
                    recordId.Value);
                var recordTempPath = AgentDurableRecordPathResolver.GetRecordTempPath(recordPath);
                var recordDirectory = Path.GetDirectoryName(recordPath)!;
                Directory.CreateDirectory(recordDirectory);

                var recordJson = AgentDurableRecordSerializer.SerializeEnvelope(envelope);
                File.WriteAllText(recordTempPath, recordJson);
                File.Move(recordTempPath, recordPath, overwrite: true);

                partition.Index.Records.Add(new AgentDurableRecordIndexEntry
                {
                    RecordId = recordId.Value,
                    RecordClass = request.RecordClass,
                    OrderingSequence = orderingSequence,
                    IdempotencyKey = request.IdempotencyKey,
                    FileName = Path.GetFileName(recordPath),
                });

                SaveIndex(partition);
                partition.EnvelopesByClass[request.RecordClass] = MergeEnvelope(
                    partition.EnvelopesByClass.GetValueOrDefault(request.RecordClass),
                    envelope);

                return new AgentDurableRecordAppendResult(
                    AgentDurableRecordAppendStatus.Appended,
                    envelope);
            }
        }
    }

    public AgentDurableRecordReplayResult Replay(AgentDurableRecordReplayRequest request)
    {
        ThrowIfDisposed();
        var partition = GetOrLoadPartition(request.WorkspaceKey);

        lock (_gate)
        {
            ReloadPartitionIfNeeded(partition);
            var envelopes = partition.EnvelopesByClass.GetValueOrDefault(request.RecordClass)
                ?? Array.Empty<AgentDurableRecordEnvelope>();

            var selected = envelopes
                .Where(e => e.OrderingSequence > request.AfterOrderingSequence)
                .OrderBy(e => e.OrderingSequence)
                .Take(request.MaxRecords)
                .ToArray();

            var nextSequence = selected.Length == 0
                ? request.AfterOrderingSequence
                : selected[^1].OrderingSequence;

            return new AgentDurableRecordReplayResult(
                selected,
                new AgentDurableRecordReplayCursor(request.RecordClass, nextSequence));
        }
    }

    public string GetWorkspaceDirectoryPath(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        ThrowIfDisposed();
        return Path.Combine(_rootDirectory, workspaceKey.Value);
    }

    public void Flush()
    {
        ThrowIfDisposed();
        FlushCore();
    }

    private void FlushCore()
    {
        lock (_gate)
        {
            foreach (var partition in _partitions.Values)
            {
                if (!partition.Dirty)
                {
                    continue;
                }

                if (!AgentDurableRecordPartitionLock.TryAcquire(partition.LockPath, out var partitionLock))
                {
                    continue;
                }

                using (partitionLock!)
                {
                    SaveIndex(partition);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        FlushCore();
        _disposed = true;
    }

    private WorkspacePartition GetOrLoadPartition(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        lock (_gate)
        {
            if (_partitions.TryGetValue(workspaceKey.Value, out var existing))
            {
                return existing;
            }

            var partition = LoadPartition(workspaceKey);
            _partitions[workspaceKey.Value] = partition;
            return partition;
        }
    }

    private void ReloadPartitionIfNeeded(WorkspacePartition partition)
    {
        if (!File.Exists(partition.IndexPath))
        {
            return;
        }

        var lastWrite = File.GetLastWriteTimeUtc(partition.IndexPath);
        if (lastWrite <= partition.IndexLoadedAtUtc)
        {
            return;
        }

        var reloaded = LoadPartition(AgentDurableWorkspaceStorageKey.FromValue(partition.Index.WorkspaceKey));
        partition.LoadOutcome = reloaded.LoadOutcome;
        partition.WritesEnabled = reloaded.WritesEnabled;
        partition.Index = reloaded.Index;
        partition.EnvelopesByClass = reloaded.EnvelopesByClass;
        partition.IndexLoadedAtUtc = reloaded.IndexLoadedAtUtc;
        partition.Dirty = false;
    }

    private WorkspacePartition LoadPartition(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        var directory = Path.Combine(_rootDirectory, workspaceKey.Value);
        var indexPath = AgentDurableRecordPathResolver.GetIndexPath(directory);
        var lastKnownGoodPath = AgentDurableRecordPathResolver.GetIndexLastKnownGoodPath(directory);
        var lockPath = AgentDurableRecordPathResolver.GetLockPath(directory);

        if (!File.Exists(indexPath) && File.Exists(ResolveInterruptedTempIndex(directory)))
        {
            AgentDurableRecordQuarantineWriter.QuarantineFile(
                ResolveInterruptedTempIndex(directory)!,
                directory,
                "interrupted-index-write");
        }

        var loadOutcome = AgentDurableRecordLoadOutcome.Missing;
        var writesEnabled = true;
        AgentDurableRecordPartitionIndex index;

        if (!File.Exists(indexPath))
        {
            index = CreateEmptyIndex(workspaceKey);
            loadOutcome = AgentDurableRecordLoadOutcome.Missing;
        }
        else
        {
            var loaded = TryLoadIndex(indexPath, directory, out var outcome, out var migrated);
            if (loaded is null && outcome == AgentDurableRecordLoadOutcome.Corrupt)
            {
                loaded = TryLoadIndex(lastKnownGoodPath, directory, out outcome, out migrated);
            }

            if (loaded is null)
            {
                index = CreateEmptyIndex(workspaceKey);
                writesEnabled = outcome != AgentDurableRecordLoadOutcome.UnsupportedVersion;
                loadOutcome = outcome;
            }
            else
            {
                index = loaded;
                loadOutcome = migrated
                    ? AgentDurableRecordLoadOutcome.Migrated
                    : AgentDurableRecordLoadOutcome.Loaded;
            }
        }

        if (!string.Equals(index.WorkspaceKey, workspaceKey.Value, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(index.WorkspaceKey))
        {
            writesEnabled = false;
            loadOutcome = AgentDurableRecordLoadOutcome.Quarantined;
        }
        else if (string.IsNullOrEmpty(index.WorkspaceKey))
        {
            index.WorkspaceKey = workspaceKey.Value;
        }

        var envelopesByClass = LoadEnvelopes(directory, index, out var quarantinedAny);
        if (quarantinedAny && loadOutcome == AgentDurableRecordLoadOutcome.Loaded)
        {
            loadOutcome = AgentDurableRecordLoadOutcome.Quarantined;
        }

        return new WorkspacePartition
        {
            Directory = directory,
            IndexPath = indexPath,
            LockPath = lockPath,
            Index = index,
            LoadOutcome = loadOutcome,
            WritesEnabled = writesEnabled && index.SchemaVersion <= AgentDurableRecordPartitionIndex.CurrentSchemaVersion,
            EnvelopesByClass = envelopesByClass,
            IndexLoadedAtUtc = DateTime.UtcNow,
        };
    }

    private AgentDurableRecordPartitionIndex? TryLoadIndex(
        string indexPath,
        string workspaceDirectory,
        out AgentDurableRecordLoadOutcome outcome,
        out bool migrated)
    {
        migrated = false;
        outcome = AgentDurableRecordLoadOutcome.Missing;

        if (!File.Exists(indexPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(indexPath);
            var index = AgentDurableRecordSerializer.DeserializeIndex(json);
            if (index is null)
            {
                outcome = AgentDurableRecordLoadOutcome.Corrupt;
                AgentDurableRecordQuarantineWriter.QuarantineFile(indexPath, workspaceDirectory, "corrupt-index");
                return null;
            }

            if (index.SchemaVersion > AgentDurableRecordPartitionIndex.CurrentSchemaVersion)
            {
                outcome = AgentDurableRecordLoadOutcome.UnsupportedVersion;
                return null;
            }

            if (index.SchemaVersion < AgentDurableRecordPartitionIndex.CurrentSchemaVersion)
            {
                var backupPath = AgentDurableRecordPathResolver.GetPreMigrationBackupPath(workspaceDirectory);
                File.WriteAllText(backupPath, json);
                var (migratedJson, didMigrate) = _migrator.Migrate(json, index.SchemaVersion);
                if (!didMigrate)
                {
                    outcome = AgentDurableRecordLoadOutcome.UnsupportedVersion;
                    return null;
                }

                index = AgentDurableRecordSerializer.DeserializeIndex(migratedJson);
                if (index is null)
                {
                    outcome = AgentDurableRecordLoadOutcome.Corrupt;
                    return null;
                }

                index.SchemaVersion = AgentDurableRecordPartitionIndex.CurrentSchemaVersion;
                var tempPath = AgentDurableRecordPathResolver.GetIndexTempPath(workspaceDirectory);
                File.WriteAllText(tempPath, AgentDurableRecordSerializer.SerializeIndex(index));
                File.Move(tempPath, indexPath, overwrite: true);
                migrated = true;
            }

            outcome = AgentDurableRecordLoadOutcome.Loaded;
            return index;
        }
        catch (IOException)
        {
            outcome = AgentDurableRecordLoadOutcome.Corrupt;
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            outcome = AgentDurableRecordLoadOutcome.Corrupt;
            return null;
        }
    }

    private static Dictionary<AgentDurableRecordClass, IReadOnlyList<AgentDurableRecordEnvelope>> LoadEnvelopes(
        string workspaceDirectory,
        AgentDurableRecordPartitionIndex index,
        out bool quarantinedAny)
    {
        quarantinedAny = false;
        var envelopesByClass = new Dictionary<AgentDurableRecordClass, List<AgentDurableRecordEnvelope>>();

        foreach (var entry in index.Records.OrderBy(r => r.OrderingSequence))
        {
            var recordPath = Path.Combine(
                AgentDurableRecordPathResolver.GetRecordsDirectory(workspaceDirectory),
                entry.RecordClass.ToString(),
                entry.FileName);

            var envelope = TryReadEnvelope(recordPath);
            if (envelope is null)
            {
                quarantinedAny = true;
                AgentDurableRecordQuarantineWriter.QuarantineFile(recordPath, workspaceDirectory, "unreadable-record");
                continue;
            }

            if (envelope.SchemaVersion > AgentDurableRecordPartitionIndex.CurrentSchemaVersion)
            {
                quarantinedAny = true;
                AgentDurableRecordQuarantineWriter.QuarantineFile(
                    recordPath,
                    workspaceDirectory,
                    "unsupported-record-version");
                continue;
            }

            if (!envelopesByClass.TryGetValue(entry.RecordClass, out var list))
            {
                list = new List<AgentDurableRecordEnvelope>();
                envelopesByClass[entry.RecordClass] = list;
            }

            list.Add(envelope);
        }

        return envelopesByClass.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<AgentDurableRecordEnvelope>)pair.Value.OrderBy(e => e.OrderingSequence).ToArray());
    }

    private static AgentDurableRecordEnvelope? TryReadEnvelope(string recordPath)
    {
        if (!File.Exists(recordPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(recordPath);
            return AgentDurableRecordSerializer.DeserializeEnvelope(json);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveInterruptedTempIndex(string workspaceDirectory) =>
        File.Exists(AgentDurableRecordPathResolver.GetIndexTempPath(workspaceDirectory))
            ? AgentDurableRecordPathResolver.GetIndexTempPath(workspaceDirectory)
            : null;

    private static AgentDurableRecordPartitionIndex CreateEmptyIndex(
        AgentDurableWorkspaceStorageKey workspaceKey) =>
        new()
        {
            SchemaVersion = AgentDurableRecordPartitionIndex.CurrentSchemaVersion,
            WorkspaceKey = workspaceKey.Value,
        };

    private void SaveIndex(WorkspacePartition partition)
    {
        Directory.CreateDirectory(partition.Directory);
        var json = AgentDurableRecordSerializer.SerializeIndex(partition.Index);
        var tempPath = AgentDurableRecordPathResolver.GetIndexTempPath(partition.Directory);
        var indexPath = partition.IndexPath;
        var lastKnownGoodPath = AgentDurableRecordPathResolver.GetIndexLastKnownGoodPath(partition.Directory);

        File.WriteAllText(tempPath, json);
        File.Move(tempPath, indexPath, overwrite: true);
        File.WriteAllText(lastKnownGoodPath, json);
        partition.Dirty = false;
        partition.IndexLoadedAtUtc = DateTime.UtcNow;
    }

    private static IReadOnlyList<AgentDurableRecordEnvelope> MergeEnvelope(
        IReadOnlyList<AgentDurableRecordEnvelope>? existing,
        AgentDurableRecordEnvelope envelope)
    {
        if (existing is null || existing.Count == 0)
        {
            return new[] { envelope };
        }

        var list = existing.ToList();
        list.Add(envelope);
        return list.OrderBy(e => e.OrderingSequence).ToArray();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AgentDurableRecordFileStore));
        }
    }

    private sealed class WorkspacePartition
    {
        public required string Directory { get; init; }

        public required string IndexPath { get; init; }

        public required string LockPath { get; init; }

        public required AgentDurableRecordPartitionIndex Index { get; set; }

        public AgentDurableRecordLoadOutcome LoadOutcome { get; set; }

        public bool WritesEnabled { get; set; }

        public Dictionary<AgentDurableRecordClass, IReadOnlyList<AgentDurableRecordEnvelope>> EnvelopesByClass { get; set; } =
            new();

        public DateTime IndexLoadedAtUtc { get; set; }

        public bool Dirty { get; set; }
    }
}
