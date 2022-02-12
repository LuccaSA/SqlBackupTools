using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using SqlBackupTools.Helpers;

namespace SqlBackupTools.Tests.Utils
{
    public static class SqlDbHelper
    {
        public static async Task CreateDbAsync(this SqlConnection sql, string databaseName)
        {
            if (databaseName == null) throw new ArgumentNullException(nameof(databaseName));
            await sql.DeleteDbAsync(databaseName);
            await sql.ExecuteAsync("CREATE DATABASE " + databaseName);
        }

        public static async Task DeleteDbAsync(this SqlConnection sql, string databaseName)
        {
            if (databaseName == null) throw new ArgumentNullException(nameof(databaseName));
            bool isRestoring = await sql.GetStateAsync(databaseName) == "RESTORING";
            string deleteQuery = $"IF EXISTS (select 1 from sys.databases WHERE name = '{databaseName}') BEGIN ";
            if (isRestoring)
            {
                deleteQuery += $"DROP DATABASE {databaseName}";
            }
            else
            {
                deleteQuery += $"ALTER DATABASE {databaseName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE  DROP DATABASE {databaseName}";
            }
            deleteQuery += " END";

            await sql.ExecuteAsyncWithRetry(deleteQuery);
        }

        private static async Task<string> GetStateAsync(this SqlConnection sql, string databaseName)
        {
            await using SqlCommand cmd = sql.CreateCommand();
            cmd.CommandText = $"SELECT state_desc FROM sys.databases WHERE name = '{databaseName}'";
            var rdr = await cmd.ExecuteReaderAsync();
            return (await rdr.ReadAsync()) ? rdr.GetString(0) : null;
        }

        public static SqlConnection SqlConnection(this string hostName, string dbName = null, int timeout = 120)
        {
            var builder = hostName.PrepareSqlConnectionStringBuilder(dbName);
            var sql = new SqlConnection(builder.ConnectionString);
            sql.Open();
            return sql;
        }
         

        private static async Task ExecuteAsyncWithRetry(this SqlConnection sql, string sqlCommand, int retry = 5)
        {
            await using SqlCommand cmd = sql.CreateCommand();
            while (true)
            {
                try
                {
                    cmd.CommandText = sqlCommand;
                    await cmd.ExecuteNonQueryAsync();
                    break;
                }
                catch
                {
                    if (retry-- <= 0)
                    {
                        throw;
                    }
                    await Task.Delay(50);
                }
            }
        }
    }
}
