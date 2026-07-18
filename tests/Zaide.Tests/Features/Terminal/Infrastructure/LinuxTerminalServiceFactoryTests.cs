using Xunit;
using Zaide.Features.Terminal.Contracts;
using Zaide.Features.Terminal.Infrastructure;

namespace Zaide.Tests.Features.Terminal.Infrastructure;

public class LinuxTerminalServiceFactoryTests
{
    [Fact]
    public void Create_ReturnsNonNullService()
    {
        var factory = new LinuxTerminalServiceFactory();
        using var service = factory.Create();
        Assert.NotNull(service);
    }

    [Fact]
    public void Create_ReturnsDifferentInstancesOnEachCall()
    {
        var factory = new LinuxTerminalServiceFactory();
        using var service1 = factory.Create();
        using var service2 = factory.Create();
        Assert.NotSame(service1, service2);
    }

    [Fact]
    public void Create_ReturnsITerminalService()
    {
        var factory = new LinuxTerminalServiceFactory();
        using var service = factory.Create();
        Assert.IsAssignableFrom<ITerminalService>(service);
    }
}
