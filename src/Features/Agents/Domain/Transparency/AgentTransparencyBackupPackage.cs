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
        if (string.IsNullOrWhiteSpace(backupDirectory))
        {
            throw new ArgumentException("Backup directory is required.", nameof(backupDirectory));
        }

        WorkspaceKey = workspaceKey;
        BackupDirectory = backupDirectory;
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
