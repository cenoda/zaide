using System;
using System.Collections.Generic;
using System.IO;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Continuity;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Contracts.Continuity;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Continuity;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Continuity;

internal static class Phase21ContinuityTestSupport
{
    public static (string RootDirectory, string WorkspaceRoot, AgentDurableWorkspaceStorageKey WorkspaceKey)
        CreateWorkspaceFixture()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZaidePhase21Continuity_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDirectory);
        var workspaceRoot = Path.Combine(rootDirectory, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        var workspaceKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot(workspaceRoot);
        return (rootDirectory, workspaceRoot, workspaceKey);
    }

    public static AgentDurableRecordFileStore CreateStore(string rootDirectory) =>
        new(
            rootDirectory,
            new AgentDurableRecordMigrator(new IAgentDurableRecordMigration[]
            {
                new AgentDurableRecordMigrationV0ToV1(),
            }));

    public static AgentSessionContinuityCoordinator CreateCoordinator(
        AgentDurableRecordFileStore store,
        AgentActorBackendBindingStore? bindingStore = null)
    {
        bindingStore ??= new AgentActorBackendBindingStore();
        var writer = new AgentSessionContinuityCheckpointWriter(store);
        var inspector = new AgentSessionContinuityInspector(store);
        var adapters = new IAgentBackendContinuityAdapter[]
        {
            new NativeHarnessAgentContinuityAdapter(),
            new AcpAgentContinuityAdapter(),
        };
        var revalidator = new AgentSessionContinuityRevalidator(bindingStore, adapters);
        var resolver = new PathDerivedAgentDurableWorkspaceStorageKeyResolver();
        return new AgentSessionContinuityCoordinator(
            writer,
            inspector,
            revalidator,
            bindingStore,
            adapters,
            resolver,
            store);
    }

    public static AgentSessionContinuityCheckpoint CreateInterruptedCheckpoint(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string workspaceRoot,
        ConversationId conversationId,
        AgentSessionId sessionId,
        ActorId actorId,
        AgentBackendId backendId,
        AgentRunStatus runStatus = AgentRunStatus.Running)
    {
        var fingerprint = AgentSessionContinuityBindingFingerprint.Compute(
            actorId,
            backendId,
            workspaceRoot);

        var scope = new AgentSessionContinuityScope(
            actorId,
            conversationId,
            sessionId,
            ExecutionRunId.New(),
            backendId,
            workspaceKey,
            workspaceRoot);

        return new AgentSessionContinuityCheckpoint(
            AgentSessionContinuityCheckpointPhase.BeforeApplicationShutdown,
            scope,
            AgentSessionContinuityClassification.Recoverable,
            AgentSessionStatus.Running,
            runStatus,
            AgentSessionContinuityLimits.PayloadSchemaVersion,
            fingerprint,
            capabilitySnapshotVersion: 1,
            DateTimeOffset.UtcNow);
    }

    public static void SeedBinding(
        AgentActorBackendBindingStore bindingStore,
        ActorId actorId,
        AgentBackendId backendId)
    {
        if (backendId == AgentBackendIds.Acp)
        {
            var runtime = new AcpRuntimeIdentity(
                Path.Combine(Path.GetTempPath(), "zaide-acp-fake"),
                Array.Empty<string>());
            bindingStore.SetBinding(
                new AgentActorBackendBinding(
                    actorId,
                    backendId,
                    runtime,
                    "acp-fake-agent",
                    "phase-21-m4"));
            return;
        }

        bindingStore.SetBinding(new AgentActorBackendBinding(actorId, backendId));
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
