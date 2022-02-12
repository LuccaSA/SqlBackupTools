using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using Microsoft.Data.SqlClient;
using SqlBackupTools.Helpers;

namespace SqlBackupTools
{
    public abstract class GeneralCommandInfos
    {
        protected GeneralCommandInfos()
        {
            Threads = Environment.ProcessorCount;
        }

        [Option('h', "hostname", Required = true, HelpText = "SQL Server hostname")]
        public string Hostname { get; set; }
        
        [Option('l', "login", HelpText = "SQL Server login")]
        public string Login { get; set; }

        [Option('p', "password", HelpText = "SQL Server password")]
        public string Password { get; set; }

        [Option('v', "verbose", HelpText = "Log details")]
        public bool Verbose { get; set; }

        [Option("timeout", HelpText = "SQL Command timeout in seconds")]
        public int Timeout { get; set; } = 90 * 60; // 1h

        [Option('e', "encrypt", HelpText = "Uses SSL encryption for all data sent between the client and server")]
        public bool Encrypt { get; set; }

        [Option("logs", HelpText = "Log folder")]
        public DirectoryInfo LogsPath { get; set; }

        [Option('t', "threads", HelpText = "Parallel threads")]
        public int Threads { get; set; }


        [Option("smtp", HelpText = "Smtp server to send email")]
        public string Smtp { get; set; }
        [Option("email", HelpText = "Email address to send email")]
        public string Email { get; set; }

        [Option("slackSecret", HelpText = "Slack token")]
        public string SlackSecret { get; internal set; }

        [Option("slackChannel", HelpText = "Slack channel")]
        public string SlackChannel { get; internal set; }

        [Option("slackOnlyOnError", HelpText = "Send slack message only on warning or error")]
        public bool SlackOnlyOnError { get; set; }

        [Option("slackTitle", HelpText = "Slack message title")]
        public string SlackTitle { get; internal set; }

        public virtual void Validate()
        {

        }

        public SqlConnection CreateConnectionMars(string database = "master")
        {
            var builder = Hostname.PrepareSqlConnectionStringBuilder(database, Login, Password, Timeout);
            var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
            return connection;
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
