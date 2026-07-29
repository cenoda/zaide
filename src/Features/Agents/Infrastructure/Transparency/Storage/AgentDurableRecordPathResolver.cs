using System.IO;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Settings.Infrastructure;

namespace Zaide.Features.Agents.Infrastructure.Transparency.Storage;

/// <summary>
/// Resolves Agents-owned Phase 21 durable record paths under the Zaide config
/// directory, isolated from conversation and settings schemas.
/// </summary>
internal static class AgentDurableRecordPathResolver
{
    public static string GetRootDirectory() =>
        Path.Combine(SettingsPathResolver.GetSettingsDirectory(), "agents-durable");

    public static string GetWorkspaceDirectory(string workspaceKeyValue) =>
        Path.Combine(GetRootDirectory(), workspaceKeyValue);

    public static string GetIndexPath(string workspaceDirectory) =>
        Path.Combine(workspaceDirectory, "index.json");

    public static string GetIndexTempPath(string workspaceDirectory) =>
        Path.Combine(workspaceDirectory, "index.json.tmp");

    public static string GetIndexLastKnownGoodPath(string workspaceDirectory) =>
        Path.Combine(workspaceDirectory, "index.json.lastknowngood");

    public static string GetPreMigrationBackupPath(string workspaceDirectory) =>
        Path.Combine(workspaceDirectory, "index.pre-migration-backup");

    public static string GetLockPath(string workspaceDirectory) =>
        Path.Combine(workspaceDirectory, ".partition.lock");

    public static string GetQuarantineDirectory(string workspaceDirectory) =>
        Path.Combine(workspaceDirectory, "quarantine");

    public static string GetRecordsDirectory(string workspaceDirectory) =>
        Path.Combine(workspaceDirectory, "records");

    public static string GetRecordPath(
        string workspaceDirectory,
        AgentDurableRecordClass recordClass,
        long orderingSequence,
        string recordIdValue)
    {
        var classDir = Path.Combine(
            GetRecordsDirectory(workspaceDirectory),
            recordClass.ToString());
        return Path.Combine(classDir, $"{orderingSequence:D10}_{recordIdValue}.json");
    }

    public static string GetRecordTempPath(string finalRecordPath) =>
        finalRecordPath + ".tmp";
}
