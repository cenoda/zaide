using Tuning.T01.DataSource.Contracts;

namespace Tuning.T01.Consumer.C;

public sealed class ConsumerCDataService
{
    private readonly IDataSource _dataSource;

    public ConsumerCDataService(IDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public string Query() => _dataSource.FetchData();
}
