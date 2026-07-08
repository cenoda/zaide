using System;
using Moq;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Views;

namespace Zaide.Tests.Views;

public class TerminalPanelSubscriptionsTests
{
    private static readonly Action<Action> RunInline = action => action();

    [Fact]
    public void SubscribeToRestarted_AllowsDisposalAfterViewModelReferenceIsDropped()
    {
        var service = new Mock<ITerminalService>();
        var session = new TerminalViewModel(service.Object, RunInline);
        var callCount = 0;

        var subscription = TerminalPanelSubscriptions.SubscribeToRestarted(session, () => callCount++);

        session = null;
        subscription.Dispose();

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void SubscribeToRestarted_ReturnsEmptyDisposableWhenSessionIsNull()
    {
        var subscription = TerminalPanelSubscriptions.SubscribeToRestarted(null, () => throw new InvalidOperationException("should not be called"));

        subscription.Dispose();
    }
}
