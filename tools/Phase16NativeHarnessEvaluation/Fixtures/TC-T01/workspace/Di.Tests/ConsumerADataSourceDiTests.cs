using Microsoft.Extensions.DependencyInjection;
using Tuning.T01.Consumer.A;
using Tuning.T01.DataSource.Contracts;
using Xunit;

namespace Tuning.T01.Di.Tests;

public sealed class ConsumerADataSourceDiTests
{
    [Fact]
    public void Resolves_IDataSource_and_FetchData_returns_data()
    {
        var services = new ServiceCollection();
        services.AddConsumerA();
        var provider = services.BuildServiceProvider();

        var dataSource = provider.GetRequiredService<IDataSource>();
        var service = provider.GetRequiredService<ConsumerADataService>();

        Assert.NotNull(dataSource);
        Assert.Equal("repository-data", dataSource.FetchData());
        Assert.Equal("repository-data", service.Read());
    }
}
