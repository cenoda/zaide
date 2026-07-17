using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Application;
using Zaide.Features.Terminal.Presentation;

namespace Zaide.Tests.Features.Terminal.Presentation;

public class TerminalTabCloseBehaviorTests
{
    static TerminalTabCloseBehaviorTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static readonly System.Action<System.Action> RunInline = action => action();

    [Fact]
    public void ShouldHideBottomPanelInsteadOfClosing_ReturnsTrue_ForSoleTab()
    {
        using var host = CreateHost();

        var result = TerminalTabCloseBehavior.ShouldHideBottomPanelInsteadOfClosing(host, host.Tabs[0]);

        Assert.True(result);
    }

    [Fact]
    public void ShouldHideBottomPanelInsteadOfClosing_ReturnsFalse_WhenMultipleTabsExist()
    {
        using var host = CreateHost();
        host.NewTab();

        var result = TerminalTabCloseBehavior.ShouldHideBottomPanelInsteadOfClosing(host, host.Tabs[0]);

        Assert.False(result);
    }

    private static TerminalHost CreateHost()
    {
        var factory = new Mock<ITerminalSessionFactory>();
        factory.Setup(f => f.CreateSession()).Returns(() =>
        {
            var service = new Mock<ITerminalService>();
            return new TerminalViewModel(service.Object, RunInline);
        });

        return new TerminalHost(factory.Object);
    }
}
