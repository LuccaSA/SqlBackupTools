using Dapper;
using ParallelAsync;
using SqlBackupTools.Helpers;
using SqlBackupTools.Restore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Serilog;

namespace SqlBackupTools
{
    internal static class DropRunner
    {
        internal static async Task DropDatabasesAsync(DropDatabaseCommand drop, ILogger logger, CancellationToken token)
        {
            await using var connection = drop.CreateConnectionMars();

            var serverInfos = await connection.GetServerInfosAsync();
            var databases = (await connection.GetUserDatabasesAsync()).ToList();

            if (drop.DuplicatesIgnored != null && drop.DuplicatesIgnored.Any())
            {
                var exclude = drop.DuplicatesIgnored.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                databases = databases.Where(d => !exclude.Contains(d.Name)).ToList();
            }

            if (databases.Count == 0)
            {
                logger.Information("Nothing to drop");
                return;
            }

            if (!drop.Force)
            {
                await Console.Out.WriteLineAsync($"Are you sure to drop all {databases.Count} databases on server {serverInfos.ServerName}?  Please type the server name");
                var read = await Console.In.ReadLineAsync();
                if (!string.Equals(read, serverInfos.ServerName, StringComparison.OrdinalIgnoreCase))
                {
                    logger.Error("Drop operation aborded, bad confimation input");
                    return;
                }
                else
                {
                    logger.Information("Drop confirmation for " + read);
                }
            }
            else
            {
                logger.Warning("Drop mode forced ");
            }

            var sw = Stopwatch.StartNew();
            var po = new ParallelizeOption
            {
                FailMode = Fail.Smart,
                MaxDegreeOfParallelism = drop.Threads
            };

            int counter = 0;
            int total = databases.Count;

            var state = databases.ToDictionary(i => i.Name);

            await databases
                .ParallelizeAsync(async (item, cancel) =>
                {
                    int i = Interlocked.Increment(ref counter);
                    try
                    {
                        if (state.TryGetValue(item.Name, out var databaseInfo) && databaseInfo.State != DatabaseState.RESTORING)
                        {
                            await connection.ExecuteAsync($"ALTER DATABASE [{item.Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", commandTimeout: _timeout);
                        }
                        await connection.ExecuteAsync($"DROP DATABASE [{item.Name}]", commandTimeout: _timeout);
                        await connection.ExecuteAsync($"EXEC msdb.dbo.sp_delete_database_backuphistory @database_name = N'{item.Name}'", commandTimeout: _timeout);
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, $"[{i}/{total}]Error while dropping " + item.Name);
                        return false;
                    }
                    logger.Information($"[{i}/{total}] : {item.Name} dropped successfully");
                    return true;
                }, po, token);
            sw.Stop();
            logger.Information("Drop finished in " + sw.Elapsed.HumanizedTimeSpan());
        }

        private const int _timeout = 20;
    }
}
