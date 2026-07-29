using System;
using System.IO;

namespace Zaide.Features.Agents.Infrastructure.Transparency.Storage;

/// <summary>
/// Moves unreadable or incompatible on-disk artifacts into a workspace quarantine
/// directory with a bounded reason marker.
/// </summary>
internal static class AgentDurableRecordQuarantineWriter
{
    public static void QuarantineFile(
        string sourcePath,
        string workspaceDirectory,
        string reason)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        try
        {
            var quarantineDir = AgentDurableRecordPathResolver.GetQuarantineDirectory(workspaceDirectory);
            Directory.CreateDirectory(quarantineDir);
            var fileName = Path.GetFileName(sourcePath);
            var destination = Path.Combine(
                quarantineDir,
                $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{reason}_{fileName}");
            File.Move(sourcePath, destination, overwrite: true);
        }
        catch
        {
            // Best-effort quarantine must not break the store.
        }
    }
}
