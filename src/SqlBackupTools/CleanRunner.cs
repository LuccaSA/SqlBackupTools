using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using SqlBackupTools.Helpers;

namespace SqlBackupTools
{
    public static class CleanRunner
    {
        public static async Task CleanAsync(CleanCommand cleanInfos, ILogger logger, CancellationToken cancellationToken)
        {
            logger.Information($"Starting cleanup");
            // SQL dbs
            var sw = Stopwatch.StartNew();
            await using var connection = cleanInfos.CreateConnectionMars();

            var actualDatabases = await GetActualDatabasesAsync(connection);
            var actualDirectories = await EnumerateDirectoriesAsync(cleanInfos);

            // move folders for unreferenced databases
            var toMove = actualDirectories.Where(backup => !actualDatabases.Contains(backup.Name)).ToList();

            if (toMove.Count != 0 && toMove.Count == actualDirectories.Count)
            {
                throw new DangerousOperationException("You probably try to clean the wrong folder : 100% of folder will be moved");
            }

            if (toMove.Count != 0 && toMove.Count >= actualDirectories.Count / (double)10)
            {
                throw new DangerousOperationException("You probably try to clean the wrong folder : more than 10% of folder will be moved");
            }
            sw.Stop();
            logger.Information($"Crawled in {sw.Elapsed.HumanizedTimeSpan()}");
            if (toMove.Count > 0)
            {
                sw = Stopwatch.StartNew();
                foreach (var dir in toMove)
                {
                    try
                    {
                        logger.Information($"Moving {dir.Name} to {cleanInfos.CleanedFolder}");
                        MoveDirectoryWithRename(dir, cleanInfos.CleanedFolder);
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "Error while moving " + dir.Name);
                    }
                }
                sw.Stop();
                logger.Information("Folders moved in " + sw.Elapsed.HumanizedTimeSpan());
            }
        }

        private static Task<List<DirectoryInfo>> EnumerateDirectoriesAsync(CleanCommand cleanInfos)
        {
            return Task.Run(async () =>
            {
                await Task.Yield();
                var backupFolder = cleanInfos.BackupFolders.First();
                var actualDirectories = backupFolder.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                return actualDirectories;
            });
        }

        private static async Task<HashSet<string>> GetActualDatabasesAsync(SqlConnection connection)
        {
            var dbs = await connection.QueryAsync<string>("select name from sys.databases where database_id > 4 ");
            var actualDatabases = dbs.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            return actualDatabases;
        }

        private static readonly bool _caseSensitive = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static void MoveDirectoryWithRename(DirectoryInfo source, DirectoryInfo targetFolder)
        {
            int retry = 0;
            string targetName;
            DirectoryInfo[] duplicate;
            do
            {
                targetName = retry == 0 ? source.Name : $"{source.Name}_{retry}";
                duplicate = targetFolder.GetDirectories(targetName, new EnumerationOptions
                {
                    RecurseSubdirectories = false,
                    MatchCasing = _caseSensitive ? MatchCasing.CaseSensitive : MatchCasing.CaseInsensitive
                });
                retry++;
            } while (duplicate.Length != 0);

            source.MoveTo(Path.Combine(targetFolder.FullName, targetName));
        }
    }
}
