using Microsoft.Extensions.DependencyInjection;
using Tuning.T01.DataSource.Contracts;
using Tuning.T01.DataSource.Lib;

namespace Tuning.T01.Consumer.A;

public static class ConsumerADependencyInjectionExtensions
{
    public static IServiceCollection AddConsumerA(this IServiceCollection services)
    {
        services.AddSingleton<IDataSource, DataRepository>();
        services.AddSingleton<ConsumerADataService>();
        return services;
    }
}
