using System;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

public sealed class Phase21ExportTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentTransparencyLifecycleCoordinator _coordinator;

    public Phase21ExportTests()
    {
        (_rootDirectory, _workspaceKey) = Phase21TransparencyIntegrationTestSupport.CreateWorkspaceFixture();
        _store = Phase21TransparencyIntegrationTestSupport.CreateStore(_rootDirectory);
        var memoryCoordinator = Phase21TransparencyIntegrationTestSupport.CreateMemoryCoordinator(_store);
        var memoryLifecycle = new AgentMemoryLifecycleService(memoryCoordinator.Inspector);
        _coordinator = new AgentTransparencyLifecycleCoordinator(_store, memoryLifecycle);

        memoryCoordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21TransparencyIntegrationTestSupport.CreateAgentScope(),
            "Export me",
            Phase21TransparencyIntegrationTestSupport.CreateProvenance(),
            idempotencyKey: "export-record"));
    }

    public void Dispose()
    {
        _store.Dispose();
        Phase21TransparencyIntegrationTestSupport.DeleteDirectory(_rootDirectory);
    }

    [Fact]
    public void Export_PreservesRecordOwnerSemanticsAndSchemaMarkers()
    {
        var export = _coordinator.Export(_workspaceKey);

        Assert.Equal(_workspaceKey, export.WorkspaceKey);
        Assert.Equal(AgentTransparencyLifecycleStatus.Accepted, export.Status);

        var memorySection = export.Sections.Single(s => s.RecordClass == AgentDurableRecordClass.Memory);
        Assert.False(memorySection.IsUnavailable);
        Assert.Contains("memoryId", memorySection.PayloadJsonLines[0], System.StringComparison.Ordinal);
    }
}
