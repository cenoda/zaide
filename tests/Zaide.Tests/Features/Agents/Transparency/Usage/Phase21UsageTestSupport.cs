using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zaide.Features.Agents.Application.Transparency.Usage;
using Zaide.Features.Agents.Contracts.Transparency;
using Zaide.Features.Agents.Contracts.Transparency.Usage;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Usage;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Usage;

internal static class Phase21UsageTestSupport
{
    public const string NativeHarnessBackendId = "backend:zaide-native-harness";
    public const string AcpBackendId = "backend:acp";

    public static (string RootDirectory, AgentDurableWorkspaceStorageKey WorkspaceKey) CreateWorkspaceFixture()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "ZaidePhase21Usage_" + Guid.NewGuid().ToString("N"));
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

    public static PathDerivedAgentDurableWorkspaceStorageKeyResolver CreateKeyResolver() =>
        new();

    public static AgentUsageCaptureSink CreateSink(
        AgentDurableRecordFileStore store) =>
        new(AgentUsageCaptureLimits.Default, store);

    public static AgentUsageCoordinator CreateCoordinator(
        AgentDurableRecordFileStore store,
        AgentUsageCaptureSink? sink = null)
    {
        sink ??= CreateSink(store);
        var inspector = new AgentUsageInspector(store);
        var resolver = CreateKeyResolver();
        return new AgentUsageCoordinator(sink, inspector, resolver);
    }

    public static AgentUsageCaptureRequest CreateRequest(
        AgentDurableWorkspaceStorageKey workspaceKey,
        string backendId,
        AgentUsageKind kind,
        AgentUsageValueOrigin origin,
        string metricName,
        string unit,
        decimal value,
        string? model = null,
        string? currency = null,
        string? pricingSourceId = null,
        int? pricingSourceVersion = null,
        string? pricingFormula = null,
        string? idempotencyKey = null) =>
        new(
            workspaceKey,
            backendId,
            kind,
            origin,
            metricName,
            unit,
            value,
            new AgentUsageRecordScope(
                conversationId: "conversation:usage-test",
                sessionId: "session:usage-test",
                runId: "run:usage-test",
                backendId: backendId),
            model: model,
            pricingSourceId: pricingSourceId,
            pricingSourceVersion: pricingSourceVersion,
            pricingFormula: pricingFormula,
            currency: currency,
            idempotencyKey: idempotencyKey);

    public static IReadOnlyList<AgentDurableRecordEnvelope> ReplayUsageRecords(
        AgentDurableRecordFileStore store,
        AgentDurableWorkspaceStorageKey workspaceKey,
        long afterOrderingSequence = 0,
        int maxRecords = 256)
    {
        var replay = store.Replay(new AgentDurableRecordReplayRequest(
            workspaceKey,
            AgentDurableRecordClass.Usage,
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
