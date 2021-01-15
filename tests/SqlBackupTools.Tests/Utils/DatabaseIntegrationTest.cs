namespace SqlBackupTools.Tests
{
    //public abstract class DatabaseIntegrationTest : IAsyncLifetime
    //{
    //    private string _databaseName = null;
    //    protected string DbName
    //    {
    //        get
    //        {
    //            if (_databaseName == null)
    //                _databaseName = GenerateDbName;
    //            return _databaseName;
    //        }
    //    }

    //    public async Task InitializeAsync()
    //    {
    //        await DbName.CreateDbAsync(Hostname);
    //        await PrepareDatabaseAsync();
    //    }

    //    protected virtual Task PrepareDatabaseAsync() => Task.CompletedTask;

    //    public async Task DisposeAsync()
    //    {
    //        await DbName.DeleteDbAsync(Hostname);
    //    }

    //    protected virtual string Hostname => "(localdb)\\mssqllocaldb";

    //    protected virtual string GenerateDbName => "test" + Guid.NewGuid().ToString("N");

    //    public SqlConnection NewSqlConnection() => Hostname.SqlConnection(DbName);
    //}
}
