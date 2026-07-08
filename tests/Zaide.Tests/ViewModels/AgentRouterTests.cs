using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.ViewModels;

namespace Zaide.Tests.ViewModels;

/// <summary>
/// Phase 6.1 M2: focused tests for the routing orchestration seam.
/// Covers parse resolution, direct-send vs routed-send dispatch, and all failure
/// cases. No Townhall dependency or assertions — Townhall visibility is owned by
/// <see cref="MainWindowViewModel"/> and covered in MainWindowViewModelTests.
/// </summary>
public sealed class AgentRouterTests
{
    private static AgentRouter CreateRouter(
        AgentPanelHost host,
        out Mock<IAgentExecutionCoordinator> coordinator)
    {
        coordinator = new Mock<IAgentExecutionCoordinator>();
        coordinator
            .Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var parser = new MentionParser(host);
        return new AgentRouter(parser, host, coordinator.Object);
    }

    // ── Direct-send dispatch ─────────────────────────────────────────────────

    [Fact]
    public async Task RouteAndExecuteAsync_NoMention_DispatchesDirectSendToSourcePanel()
    {
        var host = new AgentPanelHost();
        var source = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var router = CreateRouter(host, out var coordinator);

        var result = await router.RouteAndExecuteAsync(source.PanelId, "hello world");

        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.True(result.Request!.IsDirectSend);
        Assert.Null(result.Request.TargetAgentName);
        coordinator.Verify(
            c => c.SendAsync(source.PanelId, "hello world", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Routed-send dispatch ─────────────────────────────────────────────────

    [Fact]
    public async Task RouteAndExecuteAsync_ValidMention_DispatchesToTargetPanelWithStrippedContent()
    {
        var host = new AgentPanelHost();
        var source = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var target = host.CreatePanel("agent-2", "Beta", "avatar_beta");
        var router = CreateRouter(host, out var coordinator);

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Beta please review");

        Assert.True(result.Success);
        Assert.NotNull(result.Request);
        Assert.False(result.Request!.IsDirectSend);
        Assert.Equal("Beta", result.Request.TargetAgentName);
        Assert.Equal("please review", result.Request.ContentAfterStrip);
        // Dispatch must target the resolved TARGET panel's id, not the source.
        coordinator.Verify(
            c => c.SendAsync(target.PanelId, "please review", It.IsAny<CancellationToken>()),
            Times.Once);
        coordinator.Verify(
            c => c.SendAsync(source.PanelId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Failure cases: no dispatch ───────────────────────────────────────────

    [Fact]
    public async Task RouteAndExecuteAsync_UnknownTarget_ReturnsFailureAndDoesNotDispatch()
    {
        var host = new AgentPanelHost();
        var source = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var router = CreateRouter(host, out var coordinator);

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Ghost hello");

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Equal("Unknown target", result.FailureReason);
        coordinator.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_AmbiguousTarget_ReturnsFailureAndDoesNotDispatch()
    {
        var host = new AgentPanelHost();
        var source = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        host.CreatePanel("agent-2", "Beta", "avatar_beta");
        host.CreatePanel("agent-3", "Beta", "avatar_beta2"); // duplicate name
        var router = CreateRouter(host, out var coordinator);

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Beta hello");

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Equal("Ambiguous target", result.FailureReason);
        coordinator.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_MultipleMentions_ReturnsFailureAndDoesNotDispatch()
    {
        var host = new AgentPanelHost();
        var source = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        host.CreatePanel("agent-2", "Beta", "avatar_beta");
        var router = CreateRouter(host, out var coordinator);

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Alpha @Beta hello");

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Equal("Multiple mentions", result.FailureReason);
        coordinator.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_EmptyInput_ReturnsFailureAndDoesNotDispatch()
    {
        var host = new AgentPanelHost();
        var source = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        var router = CreateRouter(host, out var coordinator);

        var result = await router.RouteAndExecuteAsync(source.PanelId, "   ");

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Equal("Empty input", result.FailureReason);
        coordinator.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RouteAndExecuteAsync_EmptyContentAfterStripping_ReturnsFailureAndDoesNotDispatch()
    {
        var host = new AgentPanelHost();
        var source = host.CreatePanel("agent-1", "Alpha", "avatar_alpha");
        host.CreatePanel("agent-2", "Beta", "avatar_beta");
        var router = CreateRouter(host, out var coordinator);

        var result = await router.RouteAndExecuteAsync(source.PanelId, "@Beta");

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Equal("Empty content after stripping", result.FailureReason);
        coordinator.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
