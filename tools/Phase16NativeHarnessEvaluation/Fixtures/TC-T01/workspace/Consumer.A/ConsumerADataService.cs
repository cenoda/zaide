using Tuning.T01.DataSource.Contracts;

namespace Tuning.T01.Consumer.A;

public sealed class ConsumerADataService
{
    private readonly IDataSource _dataSource;

    public ConsumerADataService(IDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public string Read() => _dataSource.FetchData();
}
