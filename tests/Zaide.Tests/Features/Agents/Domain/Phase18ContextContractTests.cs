using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Domain;

public sealed class Phase18ContextContractTests
{
    [Fact]
    public void AgentContextManifest_RejectsNullItemsCollection()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentContextManifest(
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                AgentContextPolicyLevel.Standard,
                items: null!,
                CreateTokenBudget(),
                Array.Empty<AgentContextTruncationDecision>(),
                Array.Empty<AgentContextExclusionDecision>(),
                DateTimeOffset.UtcNow));

        Assert.Contains("Items collection cannot be null.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentContextManifest_RejectsNullCollectionElements()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateManifest(items: new AgentContextItem[] { null! }));

        Assert.Contains("Context items cannot contain null entries.", exception.Message, StringComparison.Ordinal);
        Assert.Equal("items", exception.ParamName);
    }

    [Fact]
    public void AgentContextManifest_RejectsNonUtcAssembledAt()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateManifest(assembledAtUtc: new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.FromHours(9))));

        Assert.Contains("Assembly timestamp must be UTC.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentContextManifest_RejectsInvalidPolicyLevel()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateManifest(policyLevel: (AgentContextPolicyLevel)999));

        Assert.Equal("policyLevelApplied", exception.ParamName);
    }

    [Fact]
    public void AgentContextManifest_RejectsNegativeItemTokenCounts()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateItem(estimatedTokenCount: -1));

        Assert.Equal("estimatedTokenCount", exception.ParamName);
    }

    [Fact]
    public void AgentContextManifest_TotalTokenCountUsesCheckedArithmetic()
    {
        var item = CreateItem(estimatedTokenCount: int.MaxValue);

        Assert.Throws<OverflowException>(() =>
            CreateManifest(items: new[] { item, item }));
    }

    [Fact]
    public void AgentContextManifest_ItemsCollectionCannotBeModifiedThroughWrappedInterface()
    {
        var manifest = CreateManifest(items: new[] { CreateItem() });

        Assert.Throws<NotSupportedException>(() =>
            ((IList<AgentContextItem>)manifest.Items).Add(CreateItem()));
    }

    [Fact]
    public void AgentContextExclusionDecision_RejectsInconsistentHardExclusionState()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentContextExclusionDecision(
                sourceId: null,
                hardExclusionId: AgentContextHardExclusionId.TerminalScrollback,
                reason: "scrollback policy",
                isHardExclusion: false));

        Assert.Contains(
            "Hard exclusion id cannot be set when isHardExclusion is false.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AgentContextExclusionDecision_RejectsHardExclusionWithoutId()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentContextExclusionDecision(
                sourceId: AgentContextSourceId.ActiveFile,
                hardExclusionId: null,
                reason: "policy",
                isHardExclusion: true));

        Assert.Contains(
            "Hard exclusion id is required when isHardExclusion is true.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AgentContextExclusionDecision_RejectsSourceIdOnHardExclusion()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentContextExclusionDecision(
                sourceId: AgentContextSourceId.ActiveFile,
                hardExclusionId: AgentContextHardExclusionId.TerminalScrollback,
                reason: "policy",
                isHardExclusion: true));

        Assert.Equal(
            "Context source id and hard exclusion id are mutually exclusive.",
            exception.Message);
    }

    [Fact]
    public void AgentContextItem_RejectsNullContent()
    {
        var provenance = CreateProvenance();

        foreach (var sourceId in new[]
        {
            AgentContextSourceId.ActiveFile,
            AgentContextSourceId.ProjectContext,
            AgentContextSourceId.BuildTestFailure,
        })
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new AgentContextItem(
                    sourceId,
                    content: null!,
                    scopeDescriptor: "workspace",
                    fingerprint: "fp-1",
                    redactionState: AgentContextRedactionState.None,
                    estimatedTokenCount: 0,
                    provenance: provenance));

            Assert.Equal("content", exception.ParamName);
        }
    }

    [Fact]
    public void AgentContextItem_ProcessingFailedCannotRetainContent()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentContextItem(
                AgentContextSourceId.ActiveFile,
                content: "secret",
                scopeDescriptor: "workspace",
                fingerprint: "fp-1",
                redactionState: AgentContextRedactionState.ProcessingFailed,
                estimatedTokenCount: 0,
                provenance: CreateProvenance()));

        Assert.Contains(
            "Processing-failed context items cannot retain content.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AgentContextItem_RedactedStateRequiresRedactionReason()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentContextItem(
                AgentContextSourceId.ActiveFile,
                content: "value",
                scopeDescriptor: "workspace",
                fingerprint: "fp-1",
                redactionState: AgentContextRedactionState.Partial,
                estimatedTokenCount: 1,
                provenance: CreateProvenance()));

        Assert.Contains(
            "Redaction reason is required when content was redacted.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AgentContextDisclosurePayload_HasNoAgentContextManifestOrRawContent()
    {
        var forbiddenPropertyTypes = new[]
        {
            typeof(AgentContextManifest),
            typeof(AgentContextItem),
        };

        var violations = typeof(AgentContextDisclosurePayload)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                forbiddenPropertyTypes.Any(type => type.IsAssignableFrom(property.PropertyType))
                || property.Name.Contains("Content", StringComparison.Ordinal))
            .Select(property => property.Name)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void AgentContextDisclosurePayload_MatchesAgentEventPayloadType()
    {
        var occurredAt = new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);
        var receivedAt = occurredAt.AddMilliseconds(100);

        var agentEvent = new AgentEvent(
            AgentEventId.New(),
            schemaVersion: AgentEvent.CurrentSchemaVersion,
            sessionId: AgentSessionId.New(),
            runId: ExecutionRunId.New(),
            conversationId: ConversationId.NewDirect(),
            backendId: AgentBackendId.FromValue("backend:legacy-openai-compatible"),
            sequence: 1,
            occurredAtUtc: occurredAt,
            receivedAtUtc: receivedAt,
            causationEventId: null,
            evidenceLevel: AgentActivityEvidenceLevel.ZaideExecuted,
            kind: AgentEventKind.ContextDisclosed,
            payload: CreateDisclosurePayload());

        Assert.IsType<AgentContextDisclosurePayload>(agentEvent.Payload);
    }

    private static AgentContextManifest CreateManifest(
        AgentContextPolicyLevel policyLevel = AgentContextPolicyLevel.Standard,
        IEnumerable<AgentContextItem>? items = null,
        DateTimeOffset? assembledAtUtc = null)
    {
        return new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            policyLevel,
            items ?? new[] { CreateItem() },
            CreateTokenBudget(),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            assembledAtUtc ?? DateTimeOffset.UtcNow);
    }

    private static AgentContextTokenBudget CreateTokenBudget() =>
        new(
            AgentContextPolicyLevel.Standard,
            requestedBudget: 4_000,
            actualTokenCount: 0);

    private static AgentContextProvenance CreateProvenance() =>
        new(
            sourceServiceIdentity: "service:project-context",
            snapshotGeneration: 1,
            wasLiveSnapshot: true,
            redactionApplied: false);

    private static AgentContextItem CreateItem(int estimatedTokenCount = 0) =>
        new(
            AgentContextSourceId.ActiveFile,
            content: "class Program {}",
            scopeDescriptor: "workspace",
            fingerprint: "fp-1",
            redactionState: AgentContextRedactionState.None,
            estimatedTokenCount: estimatedTokenCount,
            provenance: CreateProvenance());

    private static AgentContextDisclosurePayload CreateDisclosurePayload() =>
        new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Standard,
            disclosedSourceIds: new[] { AgentContextSourceId.ActiveFile },
            itemCount: 1,
            estimatedTokenCount: 10,
            redactionSummary: new AgentContextDisclosureRedactionSummary(
                itemsWithNoRedaction: 1,
                itemsWithPartialRedaction: 0,
                itemsWithFullRedaction: 0,
                itemsDroppedAfterProcessingFailure: 0),
            boundarySummary: new AgentContextDisclosureBoundarySummary(
                excludedSourceCount: 0,
                hardExclusionCount: 0,
                truncatedItemCount: 0,
                droppedItemCount: 0));
}
