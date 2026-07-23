using Microsoft.Extensions.DependencyInjection;
using Tuning.T01.DataSource.Contracts;
using Tuning.T01.DataSource.Lib;

namespace Tuning.T01.Consumer.B;

public static class ConsumerBDependencyInjectionExtensions
{
    public static IServiceCollection AddConsumerB(this IServiceCollection services)
    {
        services.AddSingleton<IDataSource, DataRepository>();
        services.AddSingleton<ConsumerBDataService>();
        return services;
    }
}
