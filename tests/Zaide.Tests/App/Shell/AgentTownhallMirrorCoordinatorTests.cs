using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Zaide.App.Shell;
using Zaide.Features.Agents.Application;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Presentation;
using Zaide.Features.Townhall.Domain;
using Zaide.Features.Townhall.Presentation;

namespace Zaide.Tests.App.Shell;

/// <summary>
/// Focused behavioral coverage for the M9a-extracted
/// <see cref="AgentTownhallMirrorCoordinator"/> (mirroring once, routing,
/// cancellation). Public MWVM send coverage remains in
/// <see cref="MainWindowViewModelTests"/>.
/// </summary>
public sealed class AgentTownhallMirrorCoordinatorTests
{
    private static (AgentTownhallMirrorCoordinator Coordinator, AgentPanelHost Host, TownhallViewModel Townhall, AgentPanelState Panel, Mock<IAgentExecutionCoordinator> Exec)
        CreateSut(string statusOnCompletion = "Idle", bool appendAssistantOutput = true)
    {
        var host = new AgentPanelHost();
        var panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        var exec = new Mock<IAgentExecutionCoordinator>();
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((id, msg, _) =>
            {
                var p = host.Panels.FirstOrDefault(pp => pp.PanelId == id);
                if (p is null)
                    return;
                p.OutputHistory.Add($"User: {msg}");
                if (appendAssistantOutput && statusOnCompletion != "Error")
                    p.OutputHistory.Add("Assistant: Hello back");
                else if (appendAssistantOutput && statusOnCompletion == "Error")
                    p.OutputHistory.Add("Error: Request failed");
                p.Status = statusOnCompletion;
                p.IsBusy = false;
            })
            .Returns(Task.CompletedTask);

        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = new TownhallViewModel(new TownhallState());
        var channelId = townhall.Channels[0].Id;
        townhall.SelectChannelCommand.Execute(channelId).Subscribe();

        var coordinator = new AgentTownhallMirrorCoordinator(router, host, townhall);
        return (coordinator, host, townhall, panel, exec);
    }

    [Fact]
    public async Task SendAsync_MirrorsUserMessageExactlyOnce()
    {
        var (sut, _, townhall, panel, _) = CreateSut();
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "Hello once", CancellationToken.None);

        var userEntries = townhall.Messages
            .Skip(before)
            .Where(m => m.SenderId == "user-1")
            .ToList();
        Assert.Single(userEntries);
        Assert.Equal("Hello once", userEntries[0].Content);
        Assert.Equal(TownhallMessageKind.Chat, userEntries[0].Kind);
    }

    [Fact]
    public async Task SendAsync_SuccessfulResponse_MirroredExactlyOnce()
    {
        var (sut, _, townhall, panel, _) = CreateSut();
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "Hello", CancellationToken.None);

        Assert.Equal(before + 2, townhall.Messages.Count);
        var responseEntries = townhall.Messages
            .Skip(before)
            .Where(m => m.SenderId == "agent-1" && m.Kind == TownhallMessageKind.Chat)
            .ToList();
        Assert.Single(responseEntries);
        Assert.Equal("Assistant: Hello back", responseEntries[0].Content);
        Assert.Equal("User: Hello", panel.OutputHistory[0]);
        Assert.Equal("Assistant: Hello back", panel.OutputHistory[1]);
    }

    [Fact]
    public async Task SendAsync_Error_MirroredExactlyOnce()
    {
        var (sut, _, townhall, panel, _) = CreateSut(statusOnCompletion: "Error");
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "Hello", CancellationToken.None);

        Assert.Equal(before + 2, townhall.Messages.Count);
        var errorEntries = townhall.Messages
            .Skip(before)
            .Where(m => m.Kind == TownhallMessageKind.AgentError)
            .ToList();
        Assert.Single(errorEntries);
        Assert.Equal("Error: Request failed", errorEntries[0].Content);
        Assert.Equal("agent-1", errorEntries[0].SenderId);
    }

    [Fact]
    public async Task SendAsync_RoutingFailure_MirroredOnce_DoesNotCallExecution()
    {
        var (sut, _, townhall, panel, exec) = CreateSut();
        var before = townhall.Messages.Count;

        await sut.SendAsync(panel.PanelId, "@NonExistentAgent hello", CancellationToken.None);

        Assert.Equal(before + 2, townhall.Messages.Count);
        Assert.Equal(TownhallMessageKind.Chat, townhall.Messages[before].Kind);
        Assert.Equal(TownhallMessageKind.AgentError, townhall.Messages[before + 1].Kind);
        Assert.Contains("Routing failed", townhall.Messages[before + 1].Content);
        exec.Verify(
            c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_PassesCancellationTokenToRouterExecution()
    {
        var host = new AgentPanelHost();
        var panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        var exec = new Mock<IAgentExecutionCoordinator>();
        CancellationToken observed = default;
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, ct) => observed = ct)
            .Returns(Task.CompletedTask);

        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = new TownhallViewModel(new TownhallState());
        townhall.SelectChannelCommand.Execute(townhall.Channels[0].Id).Subscribe();
        var sut = new AgentTownhallMirrorCoordinator(router, host, townhall);

        using var cts = new CancellationTokenSource();
        await sut.SendAsync(panel.PanelId, "token check", cts.Token);

        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task SendAsync_CancelledToken_PropagatesWithoutExtraMirrorBeyondUser()
    {
        var host = new AgentPanelHost();
        var panel = host.CreatePanel("agent-1", "Test Agent", "avatar_test");
        var exec = new Mock<IAgentExecutionCoordinator>();
        exec.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var router = new AgentRouter(new MentionParser(), host, exec.Object);
        var townhall = new TownhallViewModel(new TownhallState());
        townhall.SelectChannelCommand.Execute(townhall.Channels[0].Id).Subscribe();
        var sut = new AgentTownhallMirrorCoordinator(router, host, townhall);
        var before = townhall.Messages.Count;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.SendAsync(panel.PanelId, "cancel me", CancellationToken.None));

        // User message is mirrored before routing; no response/error after cancel.
        Assert.Equal(before + 1, townhall.Messages.Count);
        Assert.Equal("cancel me", townhall.Messages[before].Content);
        Assert.Equal(TownhallMessageKind.Chat, townhall.Messages[before].Kind);
    }
}
