using Microsoft.Extensions.DependencyInjection;
using Tuning.T01.Consumer.C;
using Tuning.T01.DataSource.Contracts;
using Xunit;

namespace Tuning.T01.Di.Tests;

public sealed class ConsumerCDataSourceDiTests
{
    [Fact]
    public void Resolves_IDataSource_and_FetchData_returns_data()
    {
        var services = new ServiceCollection();
        services.AddConsumerC();
        var provider = services.BuildServiceProvider();

        var dataSource = provider.GetRequiredService<IDataSource>();
        var service = provider.GetRequiredService<ConsumerCDataService>();

        Assert.NotNull(dataSource);
        Assert.Equal("repository-data", dataSource.FetchData());
        Assert.Equal("repository-data", service.Query());
    }
}
