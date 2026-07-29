using System;
using System.IO;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;
using Zaide.Features.Conversations.Domain;
using Zaide.Tests.Features.Agents.Transparency.Trace;
using Zaide.Tests.Features.Agents.Transparency.Usage;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

internal static class Phase21TransparencyIntegrationTestSupport
{
    public static readonly ActorId TestAuthor = ActorId.FromValue("human:user-1");

    public static (string RootDirectory, AgentDurableWorkspaceStorageKey WorkspaceKey) CreateWorkspaceFixture()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZaidePhase21Integration_" + Guid.NewGuid().ToString("N"));
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

    public static AgentMemoryCoordinator CreateMemoryCoordinator(AgentDurableRecordFileStore store)
    {
        var writer = new AgentMemoryStoreWriter(store);
        var inspector = new AgentMemoryInspector(store);
        var policy = new AgentMemoryPolicyEvaluator();
        var resolver = new PathDerivedAgentDurableWorkspaceStorageKeyResolver();
        return new AgentMemoryCoordinator(writer, inspector, policy, resolver, store);
    }

    public static AgentTraceCaptureSink CreateTraceSink(
        AgentDurableRecordFileStore store,
        out AgentTraceBoundedCaptureQueue queue)
    {
        var resolver = new PathDerivedAgentDurableWorkspaceStorageKeyResolver();
        queue = Phase21TraceTestSupport.CreateQueue(store, resolver);
        return Phase21TraceTestSupport.CreateSink(queue, resolver);
    }

    public static AgentUsageCaptureSink CreateUsageSink(AgentDurableRecordFileStore store) =>
        Phase21UsageTestSupport.CreateSink(store);

    public static void SubmitTrace(AgentDurableRecordFileStore store, AgentDurableWorkspaceStorageKey workspaceKey)
    {
        var sink = CreateTraceSink(store, out var queue);
        sink.EnableCapture();
        sink.TrySubmit(Phase21TraceTestSupport.CreateRequest(
            workspaceKey,
            Phase21TraceTestSupport.NativeHarnessBackendId,
            AgentTraceKind.Request,
            """{"method":"initialize"}""",
            idempotencyKey: "integration-trace"));
        Phase21TraceTestSupport.WaitForQueueDrain(queue, expectedWritten: 1);
    }

    public static void SubmitUsage(AgentDurableRecordFileStore store, AgentDurableWorkspaceStorageKey workspaceKey)
    {
        var sink = CreateUsageSink(store);
        sink.EnableCapture();
        sink.TrySubmit(Phase21UsageTestSupport.CreateRequest(
            workspaceKey,
            Phase21UsageTestSupport.NativeHarnessBackendId,
            AgentUsageKind.TotalTokens,
            AgentUsageValueOrigin.Reported,
            "tokens",
            "count",
            42,
            idempotencyKey: "integration-usage"));
    }

    public static AgentMemoryProvenance CreateProvenance() =>
        new(TestAuthor, "integration-rev", AgentMemorySourceKind.User);

    public static AgentMemoryScopeTarget CreateAgentScope() =>
        new(AgentMemoryScope.Agent, actorId: TestAuthor);

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
