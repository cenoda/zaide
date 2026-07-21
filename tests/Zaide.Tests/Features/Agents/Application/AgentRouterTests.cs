using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Zaide.Tests.Features.Conversations;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Conversations.Contracts;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Tests.Features.Agents.Application;

/// <summary>
/// Phase 6.1 M2 + Phase 14 M7: routing orchestration seam.
/// Resolves mentions against the actor catalog roster (not open-panel names).
/// </summary>
public sealed class AgentRouterTests
{
    private static (
        AgentRouter Router,
        AgentPanelHost Host,
        Mock<IAgentExecutionCoordinator> Coordinator,
        IActorCatalog Catalog,
        IConversationStore Store) CreateSurface()
    {
        var catalog = ConversationsTestSupport.CreateCatalog();
        var store = ConversationsTestSupport.CreateStore();
        var host = ConversationsTestSupport.CreatePanelHost(catalog, store);
        var coordinator = new Mock<IAgentExecutionCoordinator>();
        coordinator
            .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((panelId, _, _) =>
            {
                var panel = host.Panels.FirstOrDefault(p => p.PanelId == panelId);
                return Task.FromResult(
                    panel is null
                        ? null
                        : AgentExecutionTestSupport.SuccessResult(panel));
            });
        var router = new AgentRouter(
            new MentionParser(),
            host,
            coordinator.Object,
            catalog,
            store);
        return (router, host, coordinator, catalog, store);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_NoMention_DispatchesDirectSendToSourcePanel()
    {
        var (router, host, coordinator, _, _) = CreateSurface();
        var source = host.CreatePanel("agent-1", "Source", "avatar_source");

        var result = await router.RouteAndExecuteAsync(source.PanelId, "hello world");

        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.True(result.Request!.IsDirectSend);
        Assert.Equal(source.ActorId, result.Request.TargetActorId);
        Assert.Equal(source.PanelId, result.Request.TargetPanelId);
        Assert.Equal(source.ConversationId, result.Request.ConversationId);
        Assert.Null(typeof(RouteRequest).GetProperty("TargetAgentName"));
        Assert.NotNull(result.ExecutionResult);
        coordinator.Verify(
            c => c.SendAsync(source.PanelId, "hello world", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_ValidMention_DispatchesToTargetPanelWithStrippedContent()
    {
        var (router, host, coordinator, _, _) = CreateSurface();
        var source = host.CreatePanel("agent-1", "Source", "avatar_source");
        var target = host.GetOrCreatePanelForActor(ActorId.PanelSeed("beta"));

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Beta please review");

        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.False(result.Request!.IsDirectSend);
        Assert.Equal(target.ActorId, result.Request.TargetActorId);
        Assert.Equal(target.PanelId, result.Request.TargetPanelId);
        Assert.Equal("please review", result.Request.ContentAfterStrip);
        coordinator.Verify(
            c => c.SendAsync(target.PanelId, "please review", It.IsAny<CancellationToken>()),
            Times.Once);
        coordinator.Verify(
            c => c.SendAsync(source.PanelId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_MentionTarget_DoesNotRequireOpenTargetPanel()
    {
        var (router, host, coordinator, _, _) = CreateSurface();
        var source = host.CreatePanel("agent-source", "Source Agent", "avatar_source");
        Assert.DoesNotContain(host.Panels, p => p.ActorId == ActorId.PanelSeed("beta"));

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Beta hello without tab");

        Assert.True(result.Success);
        Assert.Equal(ActorId.PanelSeed("beta"), result.Request!.TargetActorId);
        Assert.False(result.Request.IsDirectSend);
        var betaPanel = host.Panels.First(p => p.ActorId == ActorId.PanelSeed("beta"));
        coordinator.Verify(
            c => c.SendAsync(betaPanel.PanelId, "hello without tab", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_UnknownTarget_AppendsRoutingFailureToSourceConversation()
    {
        var (router, host, coordinator, _, store) = CreateSurface();
        var source = host.CreatePanel("agent-1", "Source", "avatar_source");

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Ghost hello");

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Equal("Unknown target", result.FailureReason);
        Assert.NotNull(result.ExecutionResult);
        Assert.Equal(ExecutionRunOutcome.RoutingFailure, result.ExecutionResult!.Run.Outcome);
        Assert.True(store.TryGet(source.ConversationId, out var conversation));
        Assert.Contains(
            conversation!.Entries,
            e => e.Kind == ConversationEntryKind.RoutingFailure && e.Content == "Unknown target");
        coordinator.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_AmbiguousTarget_ReturnsFailureAndDoesNotDispatch()
    {
        var (router, host, coordinator, _, _) = CreateSurface();
        var source = host.CreatePanel("agent-1", "Source", "avatar_source");
        host.CreatePanel("agent-twin-a", "Twin", "avatar_a");
        host.CreatePanel("agent-twin-b", "Twin", "avatar_b");

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Twin hello");

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Equal("Ambiguous target", result.FailureReason);
        Assert.NotNull(result.ExecutionResult);
        Assert.Equal(ExecutionRunOutcome.RoutingFailure, result.ExecutionResult!.Run.Outcome);
        coordinator.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_MultipleMentions_ReturnsFailureAndDoesNotDispatch()
    {
        var (router, host, coordinator, _, _) = CreateSurface();
        var source = host.CreatePanel("agent-1", "Source", "avatar_source");

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Alpha @Beta hello");

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Equal("Multiple mentions", result.FailureReason);
        Assert.NotNull(result.ExecutionResult);
        Assert.Equal(ExecutionRunOutcome.RoutingFailure, result.ExecutionResult!.Run.Outcome);
        coordinator.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_EmptyInput_ReturnsFailureAndDoesNotDispatch()
    {
        var (router, host, coordinator, _, store) = CreateSurface();
        var source = host.CreatePanel("agent-1", "Source", "avatar_source");

        var result = await router.RouteAndExecuteAsync(source.PanelId, "   ");

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Equal("Empty input", result.FailureReason);
        Assert.NotNull(result.ExecutionResult);
        Assert.Equal(ExecutionRunOutcome.RoutingFailure, result.ExecutionResult!.Run.Outcome);
        Assert.True(store.TryGet(source.ConversationId, out var conversation));
        Assert.Contains(conversation!.Entries, e => e.Kind == ConversationEntryKind.RoutingFailure);
        coordinator.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_EmptyContentAfterStripping_ReturnsFailureAndDoesNotDispatch()
    {
        var (router, host, coordinator, _, _) = CreateSurface();
        var source = host.CreatePanel("agent-1", "Source", "avatar_source");

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Beta");

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Equal("Empty content after stripping", result.FailureReason);
        Assert.NotNull(result.ExecutionResult);
        Assert.Equal(ExecutionRunOutcome.RoutingFailure, result.ExecutionResult!.Run.Outcome);
        coordinator.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
