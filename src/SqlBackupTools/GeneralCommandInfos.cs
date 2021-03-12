using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using Microsoft.Data.SqlClient;

namespace SqlBackupTools
{
    public class GeneralCommandInfos
    {
        public GeneralCommandInfos()
        {
            Threads = Environment.ProcessorCount;
        }

        [Option('h', "hostname", Required = true, HelpText = "SQL Server hostname")]
        public string Hostname { get; set; }

        [Option('f', "folder", Required = true, HelpText = "Root backup folder")]
        public IEnumerable<DirectoryInfo> BackupFolders { get; set; }

        [Option('l', "login", HelpText = "SQL Server login")]
        public string Login { get; set; }

        [Option('p', "password", HelpText = "SQL Server password")]
        public string Password { get; set; }

        [Option('v', "verbose", HelpText = "Log details")]
        public bool Verbose { get; set; }

        [Option("timeout", HelpText = "SQL Command timeout in seconds")]
        public int Timeout { get; set; } = 90 * 60;

        [Option("logs", HelpText = "Log folder")]
        public DirectoryInfo LogsPath { get; set; }

        [Option('t', "threads", HelpText = "Parallel threads")]
        public int Threads { get; set; }

        public virtual void Validate()
        {

        }

        public SqlConnection CreateConnectionMars(string database = "master")
        {
            var builder = PrepareSqlConnectionStringBuilder(database);
            builder.MultipleActiveResultSets = true;
            var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
            return connection;
        }

        private SqlConnectionStringBuilder PrepareSqlConnectionStringBuilder(string database)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = Hostname,
                InitialCatalog = database,
                ApplicationName = "SqlBackupTools",
                ConnectTimeout = 30 * 60 // 30 min,
            };
            if (!String.IsNullOrWhiteSpace(Login) && !String.IsNullOrWhiteSpace(Password))
            {
                builder.UserID = Login;
                builder.Password = Password;
            }
            else
            {
                builder.IntegratedSecurity = true;
            }

            return builder;
        }
    }

    [Verb("drop", HelpText = "Drop all databases")]
    public class DropDatabaseCommand : GeneralCommandInfos
    {
        [Option("ignoreDatabases", HelpText = "Exclude specific databases from drop command")]
        public IEnumerable<string> DuplicatesIgnored { get; set; }


        [Option("force", HelpText = "Avoid confirmation before database drop")]
        public bool Force { get; set; }
    }
}
