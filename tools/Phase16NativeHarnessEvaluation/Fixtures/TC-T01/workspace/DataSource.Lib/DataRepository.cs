using Tuning.T01.DataSource.Contracts;

namespace Tuning.T01.DataSource.Lib;

public sealed class DataRepository : IDataSource
{
    public string FetchData() => "repository-data";
}
