using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using ParallelAsync;
using SqlBackupTools.Tests.Utils;
using Xunit;

namespace SqlBackupTools.Tests
{
    public class DatabaseBackupsFixture : IAsyncLifetime
    {
        private string Prefix => "Andromeda_";
        private readonly int _timeout = 60;
        protected virtual int DbCount => 16;
        public DirectoryInfo Folder { get; private set; }
        public List<string> DatabaseNames { get; } = new();

        public Task InitializeAsync()
        {
            Folder = PrepareFolder();
            return PrepareBackupsAsync(Folder);
        }

        private static DirectoryInfo PrepareFolder()
        {
            var backupFolder = TestContext.BackupFolder;
            var folder = backupFolder.CreateSubdirectory(Guid.NewGuid().ToString("N"));
            folder.Create();
            return folder;
        }

        private async Task PrepareBackupsAsync(DirectoryInfo folder)
        {
            var sqlMaster = TestContext.SqlInstance.SqlConnection("master");
            await PrepareDatabaseScriptsAsync(sqlMaster);

            for (int i = 0; i < DbCount; i++)
            {
                string db = Prefix + Guid.NewGuid().ToString("N");
                DatabaseNames.Add(db);
            }

            await DatabaseNames.ParallelizeAsync(
                async (db, ct) =>
                {
                    await CreateBackupsAsync(folder, sqlMaster, db);
                    return true;
                }, new ParallelizeOption
                {
                    FailMode = Fail.Fast,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                }, CancellationToken.None);
        }

        protected virtual async Task CreateBackupsAsync(DirectoryInfo folder, SqlConnection sqlMaster, string db)
        {
            await sqlMaster.ExecuteAsync("CREATE DATABASE " + db,
                commandTimeout: _timeout);

            await sqlMaster.ExecuteAsync(
                $"EXECUTE [dbo].[DatabaseBackup] @Databases = '{db}', @Directory = '{folder.FullName}', @BackupType = 'FULL'",
                commandTimeout: _timeout);

            // create table 
            var sqlDb = TestContext.SqlInstance.SqlConnection(db);
            await sqlDb.ExecuteAsync("CREATE TABLE [dbo].[Test]([Id] [int] IDENTITY(1,1) NOT NULL) ON [PRIMARY]");
            await sqlDb.ExecuteAsync("CHECKPOINT",
                commandTimeout: _timeout);

            await sqlMaster.ExecuteAsync(
                $"EXECUTE [dbo].[DatabaseBackup] @Databases = '{db}', @Directory = '{folder.FullName}', @BackupType = 'LOG'",
                commandTimeout: _timeout);

            // add data
            for (int k = 0; k < 42; k++)
            {
                await sqlDb.ExecuteAsync("INSERT INTO [dbo].[Test] DEFAULT VALUES");
            }
            await sqlDb.ExecuteAsync("CHECKPOINT",
                commandTimeout: _timeout);

            await sqlMaster.ExecuteAsync(
                $"EXECUTE [dbo].[DatabaseBackup] @Databases = '{db}', @Directory = '{folder.FullName}', @BackupType = 'LOG'",
                commandTimeout: _timeout);

            await sqlMaster.DeleteDbAsync(db);
        }

        public Task DisposeAsync()
        {
            Folder.Delete(true);
            return Task.CompletedTask;
        }

        private static async Task PrepareDatabaseScriptsAsync(SqlConnection sqlMaster)
        {
            var client = new HttpClient();

            await DownloadAndExecuteScriptAsync(client, sqlMaster, "https://raw.githubusercontent.com/olahallengren/sql-server-maintenance-solution/master/CommandExecute.sql");
            await DownloadAndExecuteScriptAsync(client, sqlMaster, "https://raw.githubusercontent.com/olahallengren/sql-server-maintenance-solution/master/DatabaseBackup.sql");
        }

        private static async Task DownloadAndExecuteScriptAsync(HttpClient client, SqlConnection sql, string uri)
        {
            var backupScript = await client.GetStringAsync(uri);
            foreach (var command in Regex.Split(backupScript, "\nGO"))
            {
                await sql.ExecuteAsync(command);
            }
        }
    }
}
