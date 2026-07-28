using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Application.Acp;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Acp.Backend;

public sealed class Phase20ContextTests
{
    [Fact]
    public void Phase20Context_ProcessingFailedItems_AreExcludedFromPrompt()
    {
        var manifest = CreateManifest(
            new AgentContextItem(
                AgentContextSourceId.ActiveFile,
                content: string.Empty,
                scopeDescriptor: "workspace/src/Program.cs",
                fingerprint: "fp-failed",
                redactionState: AgentContextRedactionState.ProcessingFailed,
                estimatedTokenCount: 0,
                provenance: CreateProvenance()),
            new AgentContextItem(
                AgentContextSourceId.ProjectContext,
                content: "visible project context",
                scopeDescriptor: "workspace",
                fingerprint: "fp-project",
                redactionState: AgentContextRedactionState.None,
                estimatedTokenCount: 8,
                provenance: CreateProvenance()));

        var promptText = AcpContextManifestEncoder.BuildContextText(manifest);

        Assert.DoesNotContain("fp-failed", promptText, StringComparison.Ordinal);
        Assert.Contains("visible project context", promptText, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase20Context_HardExclusions_AreRecordedInPrompt()
    {
        var manifest = new AgentContextManifest(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Standard,
            Array.Empty<AgentContextItem>(),
            CreateTokenBudget(),
            Array.Empty<AgentContextTruncationDecision>(),
            new[]
            {
                new AgentContextExclusionDecision(
                    sourceId: default,
                    hardExclusionId: AgentContextHardExclusionId.TerminalScrollback,
                    reason: "Always excluded.",
                    isHardExclusion: true),
            },
            DateTimeOffset.UtcNow);

        var promptText = AcpContextManifestEncoder.BuildContextText(manifest);

        Assert.Contains("Hard exclusion applied", promptText, StringComparison.Ordinal);
        Assert.Contains(AgentContextHardExclusionId.TerminalScrollback.Value, promptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Phase20Context_ManifestItems_AppearInAcpPromptBlocks()
    {
        IReadOnlyList<AcpContentBlock>? capturedPrompt = null;
        var script = new AcpFakeSessionScript
        {
            CapturePrompt = prompt => capturedPrompt = prompt,
        };

        var manifest = CreateManifest(
            new AgentContextItem(
                AgentContextSourceId.ActiveFile,
                content: "class Program {}",
                scopeDescriptor: "workspace/src/Program.cs",
                fingerprint: "fp-active",
                redactionState: AgentContextRedactionState.None,
                estimatedTokenCount: 4,
                provenance: CreateProvenance()));

        var backend = new AcpAgentBackend(
            _ => Task.FromResult<IAcpSessionClient>(new AcpFakeSessionClient(script)),
            () => "/tmp/zaide-acp");

        var context = new AgentBackendExecutionContext(
            new AgentBackendRequest(
                AgentSessionId.New(),
                ExecutionRunId.New(),
                ConversationId.NewDirect(),
                ActorId.FromValue("actor:user"),
                ActorId.FromValue("actor:agent"),
                ConversationEntryId.New(),
                "use context",
                manifest),
            new UnavailableAgentActionBroker());

        await foreach (var _ in backend.ExecuteAsync(context, CancellationToken.None))
        {
        }

        Assert.NotNull(capturedPrompt);
        var combined = string.Join('\n', capturedPrompt!.Select(block => block.Text ?? string.Empty));
        Assert.Contains("class Program {}", combined, StringComparison.Ordinal);
        Assert.Contains(AgentContextSourceId.ActiveFile.Value, combined, StringComparison.Ordinal);
    }

    private static AgentContextManifest CreateManifest(params AgentContextItem[] items) =>
        new(
            AgentSessionId.New(),
            ExecutionRunId.New(),
            ConversationId.NewDirect(),
            AgentContextPolicyLevel.Standard,
            items,
            CreateTokenBudget(),
            Array.Empty<AgentContextTruncationDecision>(),
            Array.Empty<AgentContextExclusionDecision>(),
            DateTimeOffset.UtcNow);

    private static AgentContextTokenBudget CreateTokenBudget() =>
        new(AgentContextPolicyLevel.Standard, requestedBudget: 4_000, actualTokenCount: 0);

    private static AgentContextProvenance CreateProvenance() =>
        new(
            sourceServiceIdentity: "service:test",
            snapshotGeneration: 1,
            wasLiveSnapshot: true,
            redactionApplied: false);
}
