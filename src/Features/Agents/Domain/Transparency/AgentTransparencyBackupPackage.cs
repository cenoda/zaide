using System;

namespace Zaide.Features.Agents.Domain.Transparency;

internal sealed class AgentTransparencyBackupPackage
{
    public AgentTransparencyBackupPackage(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string backupDirectory,
        DateTimeOffset createdAtUtc,
        AgentTransparencyLifecycleStatus status,
        string? unavailableReason = null)
    {
        // Accepted backups must point at a real directory. Failure statuses
        // (missing/unavailable partitions) intentionally carry an empty path so
        // clean-profile Backup can return a truthful package without throwing.
        if (status == AgentTransparencyLifecycleStatus.Accepted
            && string.IsNullOrWhiteSpace(backupDirectory))
        {
            throw new ArgumentException(
                "Backup directory is required for an accepted backup.",
                nameof(backupDirectory));
        }

        WorkspaceKey = workspaceKey;
        BackupDirectory = backupDirectory ?? string.Empty;
        CreatedAtUtc = createdAtUtc;
        Status = status;
        UnavailableReason = unavailableReason;
    }

    public AgentDurableWorkspaceStorageKey WorkspaceKey { get; }

    public string BackupDirectory { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public AgentTransparencyLifecycleStatus Status { get; }

    public string? UnavailableReason { get; }
}
