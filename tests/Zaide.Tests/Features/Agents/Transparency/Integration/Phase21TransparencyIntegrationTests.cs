using System;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Application.Transparency;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Transparency.Integration;

public sealed class Phase21TransparencyIntegrationTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentTransparencyLifecycleCoordinator _lifecycleCoordinator;
    private readonly AgentMemoryCoordinator _memoryCoordinator;

    public Phase21TransparencyIntegrationTests()
    {
        (_rootDirectory, _workspaceKey) = Phase21TransparencyIntegrationTestSupport.CreateWorkspaceFixture();
        _store = Phase21TransparencyIntegrationTestSupport.CreateStore(_rootDirectory);
        _memoryCoordinator = Phase21TransparencyIntegrationTestSupport.CreateMemoryCoordinator(_store);
        var memoryLifecycle = new AgentMemoryLifecycleService(_memoryCoordinator.Inspector);
        _lifecycleCoordinator = new AgentTransparencyLifecycleCoordinator(_store, memoryLifecycle);

        Phase21TransparencyIntegrationTestSupport.SubmitTrace(_store, _workspaceKey);

        Phase21TransparencyIntegrationTestSupport.SubmitUsage(_store, _workspaceKey);

        _memoryCoordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21TransparencyIntegrationTestSupport.CreateAgentScope(),
            "Integrated memory fact",
            Phase21TransparencyIntegrationTestSupport.CreateProvenance(),
            idempotencyKey: "integration-memory"));
    }

    public void Dispose()
    {
        _store.Dispose();
        Phase21TransparencyIntegrationTestSupport.DeleteDirectory(_rootDirectory);
    }

    [Fact]
    public void Export_AllRecordClassesRemainIndependent()
    {
        var export = _lifecycleCoordinator.Export(_workspaceKey);

        Assert.Equal(5, export.Sections.Count);
        Assert.Contains(
            export.Sections,
            section => section.RecordClass == AgentDurableRecordClass.Trace && section.RecordCount > 0);
        Assert.Contains(
            export.Sections,
            section => section.RecordClass == AgentDurableRecordClass.Usage && section.RecordCount > 0);
        Assert.Contains(
            export.Sections,
            section => section.RecordClass == AgentDurableRecordClass.Memory && section.RecordCount > 0);
    }

    [Fact]
    public void Migrate_LoadsWorkspaceWithoutCrossClassDeletion()
    {
        var outcome = _lifecycleCoordinator.Migrate(_workspaceKey);
        Assert.NotEqual(AgentDurableRecordLoadOutcome.Quarantined, outcome);

        var export = _lifecycleCoordinator.Export(_workspaceKey);
        var memorySection = export.Sections.Single(s => s.RecordClass == AgentDurableRecordClass.Memory);
        var traceSection = export.Sections.Single(s => s.RecordClass == AgentDurableRecordClass.Trace);
        Assert.True(memorySection.RecordCount > 0);
        Assert.True(traceSection.RecordCount > 0);
    }
}
