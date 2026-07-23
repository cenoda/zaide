using Microsoft.Extensions.DependencyInjection;
using Tuning.T01.DataSource.Contracts;
using Tuning.T01.DataSource.Lib;

namespace Tuning.T01.Consumer.C;

public static class ConsumerCDependencyInjectionExtensions
{
    public static IServiceCollection AddConsumerC(this IServiceCollection services)
    {
        services.AddSingleton<IDataSource, DataRepository>();
        services.AddSingleton<ConsumerCDataService>();
        return services;
    }
}
