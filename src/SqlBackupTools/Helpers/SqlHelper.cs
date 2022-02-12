using System;
using Microsoft.Data.SqlClient;

namespace SqlBackupTools.Helpers
{
    internal static class SqlHelper
    {
        public static SqlConnectionStringBuilder PrepareSqlConnectionStringBuilder(this string hostName, string database = null, string login = null, string password = null, int timeout = 120, bool encrypt = false)
        {
            if (string.IsNullOrWhiteSpace(hostName))
            {
                hostName = "localhost";
            }
            if (string.IsNullOrWhiteSpace(database))
            {
                database = "master";
            }
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = hostName,
                InitialCatalog = database,
                ApplicationName = "SqlBackupTools",
                ConnectTimeout = timeout,
                Encrypt = encrypt,
                MultipleActiveResultSets = true
            };
            if (!String.IsNullOrWhiteSpace(login) && !String.IsNullOrWhiteSpace(password))
            {
                builder.UserID = login;
                builder.Password = password;
            }
            else
            {
                builder.IntegratedSecurity = true;
            }

            return builder;
        }
    }
}
