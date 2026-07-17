using ReactiveUI.Builder;
using Xunit;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;
using Zaide.Features.Terminal.Application;
using Zaide.Features.Terminal.Presentation;

namespace Zaide.Tests.Features.Terminal.Application;

public class TerminalSessionFactoryTests
{
    static TerminalSessionFactoryTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }
    [Fact]
    public void CreateSession_ReturnsNonNullViewModel()
    {
        var factory = new TerminalSessionFactory();
        using var vm = factory.CreateSession();
        Assert.NotNull(vm);
    }

    [Fact]
    public void CreateSession_ReturnsDifferentInstancesOnEachCall()
    {
        var factory = new TerminalSessionFactory();
        using var vm1 = factory.CreateSession();
        using var vm2 = factory.CreateSession();
        Assert.NotSame(vm1, vm2);
    }
}
