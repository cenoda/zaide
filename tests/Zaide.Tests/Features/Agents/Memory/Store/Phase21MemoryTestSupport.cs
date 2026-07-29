using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Memory.Store;

internal static class Phase21MemoryTestSupport
{
    public static readonly ActorId TestAuthor = ActorId.FromValue("human:user-1");

    public static (string RootDirectory, AgentDurableWorkspaceStorageKey WorkspaceKey, string WorkspaceRoot) CreateWorkspaceFixture()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZaidePhase21Memory_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDirectory);
        var workspaceRoot = Path.Combine(rootDirectory, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        var workspaceKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(workspaceRoot);
        return (rootDirectory, workspaceKey, workspaceRoot);
    }

    public static AgentDurableRecordFileStore CreateStore(string rootDirectory) =>
        new(
            rootDirectory,
            new AgentDurableRecordMigrator(new IAgentDurableRecordMigration[]
            {
                new AgentDurableRecordMigrationV0ToV1(),
            }));

    public static PathDerivedAgentDurableWorkspaceStorageKeyResolver CreateKeyResolver() =>
        new();

    public static AgentMemoryCoordinator CreateCoordinator(AgentDurableRecordFileStore store)
    {
        var writer = new AgentMemoryStoreWriter(store);
        var inspector = new AgentMemoryInspector(store);
        var policy = new AgentMemoryPolicyEvaluator();
        var resolver = CreateKeyResolver();
        return new AgentMemoryCoordinator(writer, inspector, policy, resolver, store);
    }

    public static AgentMemoryProvenance CreateProvenance(
        string sourceRevision = "rev-1",
        AgentMemorySourceKind sourceKind = AgentMemorySourceKind.User) =>
        new(TestAuthor, sourceRevision, sourceKind);

    public static AgentMemoryScopeTarget CreateAgentScope(ActorId? actorId = null) =>
        new(AgentMemoryScope.Agent, actorId: actorId ?? TestAuthor);

    public static AgentMemoryScopeTarget CreateConversationScope(ConversationId? conversationId = null) =>
        new(
            AgentMemoryScope.Conversation,
            conversationId: conversationId ?? ConversationId.ForChannel("general"));

    public static AgentMemoryScopeTarget CreateSessionScope(string sessionId = "session:test-1") =>
        new(AgentMemoryScope.Session, sessionId: sessionId);

    public static AgentMemoryScopeTarget CreateProjectScope(string projectId = "project:test") =>
        new(AgentMemoryScope.ProjectShared, projectId: projectId);

    public static IReadOnlyList<AgentDurableRecordEnvelope> ReplayMemoryRecords(
        AgentDurableRecordFileStore store,
        AgentDurableWorkspaceStorageKey workspaceKey,
        long afterOrderingSequence = 0,
        int maxRecords = 256)
    {
        var replay = store.Replay(new AgentDurableRecordReplayRequest(
            workspaceKey,
            AgentDurableRecordClass.Memory,
            afterOrderingSequence,
            maxRecords));
        return replay.Records;
    }

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
        }
    }
}
