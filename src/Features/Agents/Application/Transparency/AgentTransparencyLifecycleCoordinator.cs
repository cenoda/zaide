using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Features.Agents.Application.Transparency;

/// <summary>
/// Neutral export, backup, restore, and migration coordinator over record-owner
/// contracts. Each record class retains independent semantics.
/// </summary>
internal sealed class AgentTransparencyLifecycleCoordinator : IAgentTransparencyLifecycleCoordinator
{
    private readonly IAgentDurableRecordStore _store;
    private readonly IAgentMemoryLifecycleService _memoryLifecycle;

    public AgentTransparencyLifecycleCoordinator(
        IAgentDurableRecordStore store,
        IAgentMemoryLifecycleService memoryLifecycle)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _memoryLifecycle = memoryLifecycle ?? throw new ArgumentNullException(nameof(memoryLifecycle));
    }

    public AgentTransparencyExportPackage Export(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        var sections = new List<AgentTransparencyExportSection>();
        var hasPartialUnavailable = false;

        foreach (var recordClass in Enum.GetValues<AgentDurableRecordClass>())
        {
            sections.Add(ExportClass(workspaceKey, recordClass, ref hasPartialUnavailable));
        }

        var memoryPackage = _memoryLifecycle.Export(workspaceKey);
        var memorySection = sections.First(s => s.RecordClass == AgentDurableRecordClass.Memory);
        sections.Remove(memorySection);
        sections.Add(new AgentTransparencyExportSection(
            AgentDurableRecordClass.Memory,
            memoryPackage.Records.Count,
            memoryPackage.PartialUnavailable,
            memoryPackage.PartialUnavailable ? "Memory export reported partial unavailable evidence." : null,
            memoryPackage.Records
                .Select(record => AgentMemoryLifecycleSerializer.SerializeRecordSummary(record))
                .ToArray()));

        if (memoryPackage.PartialUnavailable)
        {
            hasPartialUnavailable = true;
        }

        return new AgentTransparencyExportPackage(
            workspaceKey,
            DateTimeOffset.UtcNow,
            sections,
            hasPartialUnavailable
                ? AgentTransparencyLifecycleStatus.PartialUnavailable
                : AgentTransparencyLifecycleStatus.Accepted);
    }

    public AgentTransparencyBackupPackage Backup(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        var load = _store.LoadWorkspace(workspaceKey);
        if (load == AgentDurableRecordLoadOutcome.UnsupportedVersion
            || load == AgentDurableRecordLoadOutcome.Quarantined)
        {
            return new AgentTransparencyBackupPackage(
                workspaceKey,
                string.Empty,
                DateTimeOffset.UtcNow,
                AgentTransparencyLifecycleStatus.Rejected,
                $"Workspace partition unavailable: {load}");
        }

        var sourceDirectory = _store.GetWorkspaceDirectoryPath(workspaceKey);
        if (!Directory.Exists(sourceDirectory))
        {
            return new AgentTransparencyBackupPackage(
                workspaceKey,
                string.Empty,
                DateTimeOffset.UtcNow,
                AgentTransparencyLifecycleStatus.NotFound,
                "Workspace partition directory not found.");
        }

        var backupRoot = Path.Combine(
            Path.GetTempPath(),
            "ZaidePhase21Backup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupRoot);
        CopyDirectory(sourceDirectory, backupRoot);

        return new AgentTransparencyBackupPackage(
            workspaceKey,
            backupRoot,
            DateTimeOffset.UtcNow,
            AgentTransparencyLifecycleStatus.Accepted);
    }

    public AgentTransparencyRestoreResult Restore(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
        {
            throw new ArgumentException("Backup directory is required.", nameof(backupDirectory));
        }

        if (!Directory.Exists(backupDirectory))
        {
            return new AgentTransparencyRestoreResult(
                workspaceKey,
                AgentTransparencyLifecycleStatus.NotFound,
                AgentDurableRecordLoadOutcome.Quarantined,
                "Backup directory not found.");
        }

        var targetDirectory = _store.GetWorkspaceDirectoryPath(workspaceKey);
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }

        Directory.CreateDirectory(targetDirectory);
        CopyDirectory(backupDirectory, targetDirectory);

        var loadOutcome = _store.LoadWorkspace(workspaceKey);
        return new AgentTransparencyRestoreResult(
            workspaceKey,
            AgentTransparencyLifecycleStatus.Accepted,
            loadOutcome);
    }

    public AgentDurableRecordLoadOutcome Migrate(AgentDurableWorkspaceStorageKey workspaceKey)
    {
        return _store.LoadWorkspace(workspaceKey);
    }

    private AgentTransparencyExportSection ExportClass(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentDurableRecordClass recordClass,
        ref bool hasPartialUnavailable)
    {
        if (recordClass == AgentDurableRecordClass.Memory)
        {
            return new AgentTransparencyExportSection(
                recordClass,
                recordCount: 0,
                isUnavailable: false);
        }

        var load = _store.LoadWorkspace(workspaceKey);
        if (load == AgentDurableRecordLoadOutcome.UnsupportedVersion
            || load == AgentDurableRecordLoadOutcome.Quarantined)
        {
            hasPartialUnavailable = true;
            return new AgentTransparencyExportSection(
                recordClass,
                recordCount: 0,
                isUnavailable: true,
                unavailableReason: $"Partition unavailable: {load}");
        }

        var envelopes = ReplayAll(workspaceKey, recordClass);
        return new AgentTransparencyExportSection(
            recordClass,
            envelopes.Count,
            isUnavailable: false,
            payloadJsonLines: envelopes.Select(envelope => envelope.PayloadJson).ToArray());
    }

    private IReadOnlyList<AgentDurableRecordEnvelope> ReplayAll(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentDurableRecordClass recordClass)
    {
        const int pageSize = 256;
        long cursor = 0;
        var envelopes = new List<AgentDurableRecordEnvelope>();

        while (true)
        {
            var replay = _store.Replay(new AgentDurableRecordReplayRequest(
                workspaceKey,
                recordClass,
                cursor,
                pageSize));

            if (replay.Records.Count == 0)
            {
                break;
            }

            envelopes.AddRange(replay.Records);
            cursor = replay.Records[^1].OrderingSequence;
            if (replay.Records.Count < pageSize)
            {
                break;
            }
        }

        return envelopes;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
