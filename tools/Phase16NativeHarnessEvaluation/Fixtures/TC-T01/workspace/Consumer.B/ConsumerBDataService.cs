using Tuning.T01.DataSource.Contracts;

namespace Tuning.T01.Consumer.B;

public sealed class ConsumerBDataService
{
    private readonly IDataSource _dataSource;

    public ConsumerBDataService(IDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public string Load() => _dataSource.FetchData();
}
