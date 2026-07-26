using Xunit;

namespace Zaide.Tests.Infrastructure;

[CollectionDefinition("AvaloniaUiInitialization", DisableParallelization = true)]
public sealed class AvaloniaUiInitializationCollection : ICollectionFixture<AvaloniaUiInitializationFixture>
{
}

public sealed class AvaloniaUiInitializationFixture
{
    public AvaloniaUiInitializationFixture()
    {
        ReactiveUiTestBootstrap.EnsureInitialized();
        ReactiveUiTestBootstrap.EnsureApplication();
        ReactiveUiTestBootstrap.RegisterDefaultActivationForViewFetcher();
    }
}
