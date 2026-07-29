using System;
using System.IO;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Storage;

internal static class Phase21StorageTestSupport
{
    public static (string RootDirectory, AgentDurableWorkspaceStorageKey WorkspaceKey) CreateWorkspaceFixture()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZaidePhase21Storage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDirectory);
        var workspaceRoot = Path.Combine(rootDirectory, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        var workspaceKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(workspaceRoot);
        return (rootDirectory, workspaceKey);
    }

    public static AgentDurableRecordFileStore CreateStore(string rootDirectory) =>
        new(
            rootDirectory,
            new AgentDurableRecordMigrator(new IAgentDurableRecordMigration[]
            {
                new AgentDurableRecordMigrationV0ToV1(),
            }));

    public static AgentDurableRecordAppendRequest CreateAppendRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        AgentDurableRecordClass recordClass,
        string idempotencyKey,
        string payloadJson = """{"marker":"phase21-m1"}""") =>
        new(
            workspaceKey,
            recordClass,
            idempotencyKey,
            payloadJson,
            new AgentDurableRecordScopeReferences(
                conversationId: "conversation:test",
                sessionId: "session:test",
                runId: "run:test",
                backendId: "backend:zaide-native-harness"));

    public static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
