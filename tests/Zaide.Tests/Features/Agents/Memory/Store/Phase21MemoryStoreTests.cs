using System;
using System.Linq;
using Xunit;
using Zaide.Features.Agents.Application.Memory;
using Zaide.Features.Agents.Domain.Transparency;
using Zaide.Features.Agents.Domain.Transparency.Memory;
using Zaide.Features.Agents.Infrastructure.Transparency.Storage;

namespace Zaide.Tests.Features.Agents.Memory.Store;

public sealed class Phase21MemoryStoreTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly AgentDurableWorkspaceStorageKey _workspaceKey;
    private readonly AgentDurableRecordFileStore _store;
    private readonly AgentMemoryCoordinator _coordinator;

    public Phase21MemoryStoreTests()
    {
        (_rootDirectory, _workspaceKey, _) = Phase21MemoryTestSupport.CreateWorkspaceFixture();
        _store = Phase21MemoryTestSupport.CreateStore(_rootDirectory);
        _coordinator = Phase21MemoryTestSupport.CreateCoordinator(_store);
    }

    public void Dispose()
    {
        _store.Dispose();
        Phase21MemoryTestSupport.DeleteDirectory(_rootDirectory);
    }

    [Fact]
    public void Create_PersistsScopedMemoryWithProvenance()
    {
        var result = _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "The project uses PostgreSQL for persistence.",
            Phase21MemoryTestSupport.CreateProvenance(sourceRevision: "user-edit-1"),
            idempotencyKey: "create-1"));

        Assert.Equal(AgentMemoryOperationStatus.Accepted, result.Status);
        Assert.NotNull(result.MemoryId);

        var record = _coordinator.Inspector.TryGetRecord(_workspaceKey, result.MemoryId!.Value);
        Assert.NotNull(record);
        Assert.Equal(AgentMemoryStatus.Active, record!.Status);
        Assert.Equal(AgentMemoryScope.Agent, record.ScopeTarget.Scope);
        Assert.Equal("user-edit-1", record.Provenance.SourceRevision);
        Assert.Equal(AgentMemoryLimits.PayloadSchemaVersion, record.SchemaVersion);
    }

    [Fact]
    public void Create_SupportsAllAdmittedScopes()
    {
        var scopes = new[]
        {
            Phase21MemoryTestSupport.CreateSessionScope(),
            Phase21MemoryTestSupport.CreateAgentScope(),
            Phase21MemoryTestSupport.CreateConversationScope(),
            Phase21MemoryTestSupport.CreateProjectScope(),
        };

        for (var i = 0; i < scopes.Length; i++)
        {
            var result = _coordinator.Create(new AgentMemoryCreateRequest(
                _workspaceKey,
                scopes[i],
                $"Scope content {i}",
                Phase21MemoryTestSupport.CreateProvenance(sourceRevision: $"rev-{i}"),
                idempotencyKey: $"scope-{i}"));

            Assert.Equal(AgentMemoryOperationStatus.Accepted, result.Status);
        }

        var summary = _coordinator.Inspector.GetSummary(_workspaceKey);
        Assert.Equal(4, summary.ActiveRecords);
    }

    [Fact]
    public void Correct_UpdatesContentWithoutRewritingHistory()
    {
        var created = _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "Original fact",
            Phase21MemoryTestSupport.CreateProvenance(sourceRevision: "rev-1"),
            idempotencyKey: "correct-create"));

        var corrected = _coordinator.Correct(new AgentMemoryCorrectRequest(
            _workspaceKey,
            created.MemoryId!.Value,
            "Corrected fact",
            Phase21MemoryTestSupport.CreateProvenance(sourceRevision: "rev-2"),
            idempotencyKey: "correct-op"));

        Assert.Equal(AgentMemoryOperationStatus.Accepted, corrected.Status);

        var record = _coordinator.Inspector.TryGetRecord(_workspaceKey, created.MemoryId!.Value);
        Assert.Equal("Corrected fact", record!.Content);
        Assert.Equal("rev-2", record.Provenance.SourceRevision);

        var envelopes = Phase21MemoryTestSupport.ReplayMemoryRecords(_store, _workspaceKey);
        Assert.Equal(2, envelopes.Count);
    }

    [Fact]
    public void Disable_MarksMemoryNonRetrievable()
    {
        var created = _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "Disable me",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "disable-create"));

        var disabled = _coordinator.Disable(new AgentMemoryDisableRequest(
            _workspaceKey,
            created.MemoryId!.Value,
            Phase21MemoryTestSupport.CreateProvenance(sourceRevision: "disable-rev"),
            idempotencyKey: "disable-op"));

        Assert.Equal(AgentMemoryOperationStatus.Accepted, disabled.Status);

        var record = _coordinator.Inspector.TryGetRecord(_workspaceKey, created.MemoryId!.Value);
        Assert.Equal(AgentMemoryStatus.Disabled, record!.Status);
        Assert.False(record.IsRetrievable);
    }

    [Fact]
    public void Supersede_LinksReplacementAndMarksOldRecordSuperseded()
    {
        var created = _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "Old API version",
            Phase21MemoryTestSupport.CreateProvenance(sourceRevision: "v1"),
            idempotencyKey: "supersede-create"));

        var superseded = _coordinator.Supersede(new AgentMemorySupersedeRequest(
            _workspaceKey,
            created.MemoryId!.Value,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "New API version",
            Phase21MemoryTestSupport.CreateProvenance(sourceRevision: "v2"),
            idempotencyKey: "supersede-op"));

        Assert.Equal(AgentMemoryOperationStatus.Accepted, superseded.Status);

        var oldRecord = _coordinator.Inspector.TryGetRecord(_workspaceKey, created.MemoryId!.Value);
        Assert.Equal(AgentMemoryStatus.Superseded, oldRecord!.Status);
        Assert.Equal(superseded.MemoryId, oldRecord.SupersededByMemoryId);

        var newRecord = _coordinator.Inspector.TryGetRecord(_workspaceKey, superseded.MemoryId!.Value);
        Assert.Equal(AgentMemoryStatus.Active, newRecord!.Status);
        Assert.Equal(created.MemoryId, newRecord.SupersedesMemoryId);
    }

    [Fact]
    public void Delete_TombstonesMemoryWithoutRemovingAuditTrail()
    {
        var created = _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "Delete me",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "delete-create"));

        var deleted = _coordinator.Delete(new AgentMemoryDeleteRequest(
            _workspaceKey,
            created.MemoryId!.Value,
            Phase21MemoryTestSupport.CreateProvenance(sourceRevision: "delete-rev"),
            idempotencyKey: "delete-op"));

        Assert.Equal(AgentMemoryOperationStatus.Accepted, deleted.Status);

        var visible = _coordinator.Inspector.GetRecords(_workspaceKey, 0, 10, includeDeleted: false);
        Assert.DoesNotContain(visible, r => r.MemoryId == created.MemoryId);

        var all = _coordinator.Inspector.GetRecords(_workspaceKey, 0, 10, includeDeleted: true);
        var tombstone = Assert.Single(all, r => r.MemoryId == created.MemoryId);
        Assert.Equal(AgentMemoryStatus.Deleted, tombstone.Status);

        var envelopes = Phase21MemoryTestSupport.ReplayMemoryRecords(_store, _workspaceKey);
        Assert.True(envelopes.Count >= 2);
    }

    [Fact]
    public void Create_DuplicateIdempotencyKeyIsIgnored()
    {
        var first = _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "First",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "dup-key"));

        var second = _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "Second",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "dup-key"));

        Assert.Equal(AgentMemoryOperationStatus.Accepted, first.Status);
        Assert.Equal(AgentMemoryOperationStatus.DuplicateIgnored, second.Status);
    }

    [Fact]
    public void Memory_UsesSeparateM1RecordClass()
    {
        _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "Isolated",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "class-sep"));

        var memoryRecords = _store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey, AgentDurableRecordClass.Memory));
        var usageRecords = _store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey, AgentDurableRecordClass.Usage));
        var traceRecords = _store.Replay(new AgentDurableRecordReplayRequest(
            _workspaceKey, AgentDurableRecordClass.Trace));

        Assert.Single(memoryRecords.Records);
        Assert.Empty(usageRecords.Records);
        Assert.Empty(traceRecords.Records);
    }

    [Fact]
    public void CrossWorkspace_AccessIsDeniedByDefault()
    {
        var (_, otherKey, _) = Phase21MemoryTestSupport.CreateWorkspaceFixture();
        using var otherStore = Phase21MemoryTestSupport.CreateStore(_rootDirectory);
        var otherCoordinator = Phase21MemoryTestSupport.CreateCoordinator(otherStore);

        var created = _coordinator.Create(new AgentMemoryCreateRequest(
            _workspaceKey,
            Phase21MemoryTestSupport.CreateAgentScope(),
            "Workspace A only",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "cross-ws-create"));

        var fromOther = otherCoordinator.Inspector.TryGetRecord(otherKey, created.MemoryId!.Value);
        Assert.Null(fromOther);

        var denied = otherCoordinator.Correct(new AgentMemoryCorrectRequest(
            otherKey,
            created.MemoryId!.Value,
            "Attempted cross-workspace edit",
            Phase21MemoryTestSupport.CreateProvenance(),
            idempotencyKey: "cross-ws-correct"));

        Assert.Equal(AgentMemoryOperationStatus.NotFound, denied.Status);
    }
}
