using System;
using Xunit;
using Zaide.Features.Agents.Domain.Transparency;

namespace Zaide.Tests.Features.Agents.Transparency.Storage;

/// <summary>
/// Phase 21 M1 durable record envelope contract tests.
/// </summary>
public sealed class Phase21RecordContractTests
{
    [Fact]
    public void Envelope_RequiresSchemaVersionOrderingAndIdempotencyKey()
    {
        var workspaceKey = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot("/tmp/phase21-contract");
        var envelope = new AgentDurableRecordEnvelope(
            schemaVersion: 1,
            recordId: AgentDurableRecordId.New(),
            recordClass: AgentDurableRecordClass.Audit,
            workspaceKey: workspaceKey,
            orderingSequence: 1,
            idempotencyKey: "idem-1",
            recordedAtUtc: DateTimeOffset.UtcNow,
            scopeReferences: new AgentDurableRecordScopeReferences(
                conversationId: "conversation:1",
                sessionId: "session:1",
                runId: "run:1",
                backendId: "backend:zaide-native-harness"),
            payloadJson: """{"kind":"audit-marker"}""");

        Assert.Equal(1, envelope.SchemaVersion);
        Assert.Equal(AgentDurableRecordClass.Audit, envelope.RecordClass);
        Assert.Equal("idem-1", envelope.IdempotencyKey);
        Assert.Equal("conversation:1", envelope.ScopeReferences.ConversationId);
    }

    [Fact]
    public void RetentionPolicy_OwnsDistinctDefaultsPerRecordClass()
    {
        Assert.True(AgentDurableRecordRetentionPolicy.GetDefaultRetentionDays(AgentDurableRecordClass.Trace) >= 0);
        Assert.True(AgentDurableRecordRetentionPolicy.GetDefaultRetentionDays(AgentDurableRecordClass.Usage) >= 0);
        Assert.True(AgentDurableRecordRetentionPolicy.GetDefaultRetentionDays(AgentDurableRecordClass.SessionRecovery) >= 0);
        Assert.True(AgentDurableRecordRetentionPolicy.GetDefaultRetentionDays(AgentDurableRecordClass.Audit) >= 0);
        Assert.True(AgentDurableRecordRetentionPolicy.GetDefaultRetentionDays(AgentDurableRecordClass.Memory) >= 0);
        Assert.NotEqual(
            AgentDurableRecordRetentionPolicy.GetDefaultRetentionDays(AgentDurableRecordClass.Trace),
            AgentDurableRecordRetentionPolicy.GetDefaultRetentionDays(AgentDurableRecordClass.Memory));
    }

    [Fact]
    public void WorkspaceStorageKey_FromWorkspaceRoot_IsStableForSamePath()
    {
        var first = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot("/tmp/phase21-same");
        var second = AgentDurableWorkspaceStorageKey.FromWorkspaceRoot("/tmp/phase21-same");
        Assert.Equal(first, second);
        Assert.StartsWith("ws:", first.Value, StringComparison.Ordinal);
    }
}
