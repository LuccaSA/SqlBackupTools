using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;
using SqlBackupTools.Helpers;

namespace SqlBackupTools.Restore
{
    public class ReportState
    {
        public double ParallelRatio { get; internal set; }
        public TimeSpan TotalTime { get; internal set; }

        public List<(RestoreItem Item, string Error)> Errors { get; } = new List<(RestoreItem Item, string Error)>();
        public List<(string Name, DirectoryInfo Path)> MissingFull { get; } = new List<(string Name, DirectoryInfo Path)>();
        public List<(string Name, DirectoryInfo Path)> MissingFullMoreThan24Hours { get; } = new List<(string Name, DirectoryInfo Path)>();
        public List<(string name, int count, List<DirectoryInfo> excluded)> DuplicatesExcluded { get; set; }
        public List<DatabaseInfo> BackupNotFoundDbExists { get; } = new List<DatabaseInfo>();
        public TimeSpan? AvgRpo { get; internal set; }
        public TimeSpan? MaxRpo { get; internal set; }
        public List<(string Name, TimeSpan Rpo)> RpoOutliers { get; } = new List<(string Name, TimeSpan Rpo)>();
        public List<RestoreItem> Restored { get; internal set; }
        public int TotalProcessed { get; internal set; }

        public ReportStatus Status { get; internal set; } = ReportStatus.Ok;
        public ServerInfos Info { get; internal set; }
        public string Mode { get; internal set; }
    }

    [Flags]
    public enum ReportStatus
    {
        Ok = 0,
        Warning = 1,
        Error = 2
    }

    public class RestoreState
    {
        public RestoreState(ILogger logger, RestoreCommand restoreCommand, SqlConnection sqlConnection)
        {
            Loggger = logger;
            RestoreCommand = restoreCommand;
            SqlConnection = sqlConnection;
            StartDate = DateTime.Now;
        }

        public DateTime StartDate { get; }

        public ILogger Loggger { get; }
        public RestoreCommand RestoreCommand { get; }
        public SqlConnection SqlConnection { get; }

        public Dictionary<string, DatabaseInfo> ActualDbs { get; set; }
        public Dictionary<string, RestoreHistoryInfo> RestoredDbs { get; set; }
        public List<RestoreItem> Directories { get; set; }

        public ConcurrentBag<RestoreItem> MissingBackupFull { get; } = new ConcurrentBag<RestoreItem>();
        public ConcurrentBag<RestoreItem> OkBackupFull { get; } = new ConcurrentBag<RestoreItem>();
        public ConcurrentBag<(Exception e, RestoreItem dir)> ExceptionBackupFull { get; } = new ConcurrentBag<(Exception e, RestoreItem dir)>();

        public TimeSpan Elapsed => _sw.Elapsed;
        public TimeSpan Accumulated { get; set; }
        public ServerInfos ServerInfos { get; set; }
        public List<(string name, int count, List<DirectoryInfo> excluded)> DuplicatesExcluded { get; } = new List<(string name, int count, List<DirectoryInfo> excluded)>();

        private readonly Stopwatch _sw = new Stopwatch();
        private int _increment = 0;

        public int Increment()
        {
            return Interlocked.Increment(ref _increment);
        }

        public void LogStarting()
        {
            _sw.Start();
            Loggger.Information($"BackupFolders : {string.Join(" ", RestoreCommand.BackupFolders.Select(d => d.FullName))}");
            Loggger.Information($"PostScripts   : {string.Join(" ", RestoreCommand.PostScripts.Select(d => d.FullName))}");
            Loggger.Information($"FullOnly      : {RestoreCommand.FullOnly}");
            Loggger.Information($"ContinueLogs  : {RestoreCommand.ContinueLogs}");
            Loggger.Information($"RunRecovery   : {RestoreCommand.RunRecovery}");

            Loggger.Information($"Starting job on {RestoreCommand.Threads} threads");
        }


        public async Task<ReportState> GetReportStateAsync()
        {
            _sw.Stop();

            ReportState reportState = new ReportState
            {
                Info = ServerInfos,
                ParallelRatio = Math.Round(Accumulated.TotalMilliseconds / Elapsed.TotalMilliseconds, 2),
                TotalTime = _sw.Elapsed,
                TotalProcessed = Directories.Count,
                Mode = $"{(RestoreCommand.FullOnly ? "Full" : "Full + Logs")} {(RestoreCommand.RunRecovery ? "with recovery" : "no recovery")}",
                DuplicatesExcluded = DuplicatesExcluded,
                Status = ReportStatus.Ok
            };

            // exceptions
            if (ExceptionBackupFull.Count != 0)
            {
                foreach (var e in ExceptionBackupFull.OrderBy(i => i.dir.Name))
                {
                    reportState.Errors.Add((e.dir, e.e.Message));
                }
                reportState.Status |= ReportStatus.Error;
            }

            // folder found without full backup
            if (MissingBackupFull.Count != 0)
            {
                foreach (var missing in MissingBackupFull.OrderBy(i => i.Name))
                {
                    if (missing.BaseDirectoryInfo.CreationTime > DateTime.Now - TimeSpan.FromHours(24))
                    {
                        reportState.MissingFull.Add((missing.Name, missing.BaseDirectoryInfo));
                    }
                    else
                    {
                        reportState.MissingFullMoreThan24Hours.Add((missing.Name, missing.BaseDirectoryInfo));
                        reportState.Status |= ReportStatus.Warning;
                    }
                }
            }

            // db exists, but no backup found
            if (RestoreCommand.Databases == null || !RestoreCommand.Databases.Any())
            {
                var backupsInJob = Directories.Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var dbs = await DatabasesQueryAsync(RestoreCommand);
                foreach (var db in dbs.Where(dir => !backupsInJob.Contains(dir.Name)).OrderBy(i => i.Name))
                {
                    reportState.BackupNotFoundDbExists.Add(db);
                    reportState.Status |= ReportStatus.Warning;
                }
            }

            // restored fine
            reportState.Restored = OkBackupFull.OrderBy(i => i.Name).ToList();

            // RPO
            var rpos = new Dictionary<string, TimeSpan>();
            foreach (var item in Directories.Where(i => i.Exception == null))
            {
                if (item.RpoRecentRestore != null)
                {
                    var up = item.RpoCurrentRestore ?? StartDate;
                    rpos.Add(item.Name, up - item.RpoRecentRestore.Value);
                }
                else
                {
                    Loggger.Debug("Missing recent restore for " + item.Name);
                }
            }

            if (RestoredDbs.Values.Any(i => !string.IsNullOrEmpty(i.LastBackupPath)))
            {
                if (rpos.Values.Any())
                {
                    reportState.AvgRpo = TimeSpan.FromTicks((long)rpos.Values.Average(i => i.Ticks));
                    reportState.MaxRpo = rpos.Values.Max();
                }
                else
                {
                    reportState.AvgRpo = TimeSpan.Zero;
                    reportState.MaxRpo = TimeSpan.Zero;
                }

                TimeSpan? outOfLimit = RestoreCommand.RpoLimit != null ? TimeSpan.FromMinutes(RestoreCommand.RpoLimit.Value) : 2 * reportState.AvgRpo;
                if (reportState.MaxRpo > outOfLimit)
                {
                    reportState.Status |= ReportStatus.Warning;
                    foreach (var o in rpos.Where(i => i.Value > outOfLimit))
                    {
                        reportState.RpoOutliers.Add((o.Key, o.Value));
                    }
                }
            }
            return reportState;
        }

        public void LogFinished(ReportState reportState)
        {
            _sw.Stop();

            reportState.ParallelRatio = Math.Round(Accumulated.TotalMilliseconds / Elapsed.TotalMilliseconds, 2);
            reportState.TotalTime = _sw.Elapsed;

            Loggger.Information($"Parallel ratio {  reportState.ParallelRatio}");
            Loggger.Information($"Finished job in {reportState.TotalTime}");

            foreach (var db in reportState.BackupNotFoundDbExists)
            {
                Loggger.Warning($"Warning backup not found for [{db.Name}]. This database exists on server, but no backup found. Need to be dropped ?");
            }

            foreach (var missing in reportState.MissingFull)
            {
                Loggger.Error($"Missing backup FULL for {missing.Name}. [{missing.Path}]");
            }

            if (reportState.AvgRpo != null && reportState.MaxRpo != null)
            {
                Loggger.Information($"RPO : AVG={reportState.AvgRpo.Value.HumanizedTimeSpan()}, MAX={reportState.MaxRpo.Value.HumanizedTimeSpan()}");
                if (reportState.RpoOutliers.Count != 0)
                {
                    var outliers = string.Join(" , ", reportState.RpoOutliers.Select(i => $"[{i.Name}]:{i.Rpo.HumanizedTimeSpan()}"));
                    Loggger.Warning($"Outliers : " + outliers);
                }
            }
        }

        public async Task LoadRestoreHistoryAsync()
        {
            var restored = await RestoreHistoryQueryAsync(RestoreCommand);
            RestoredDbs = restored.ToDictionary(i => i.DbName, StringComparer.OrdinalIgnoreCase);
        }


        public async Task LoadDatabasesAsync()
        {
            var dbs = await DatabasesQueryAsync(RestoreCommand);
            ActualDbs = dbs.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<IEnumerable<RestoreHistoryInfo>> RestoreHistoryQueryAsync(RestoreCommand restoreCommand)
        {
            return (await SqlConnection.QueryAsync<RestoreHistoryInfo>(
                @"SELECT s.dbName as dbName, d.state, bmf.physical_device_name as lastBackupPath, s.restore_date as restoreDate 
FROM (SELECT rh.destination_database_name as dbName, rh.restore_date,rh.backup_set_id, DENSE_RANK() OVER (PARTITION BY rh.destination_database_name ORDER BY rh.restore_history_id DESC) AS rnk  
      FROM [msdb].[dbo].[restorehistory] rh ) as s
INNER JOIN [msdb].[dbo].[backupset] bs ON s.backup_set_id = bs.backup_set_id
INNER JOIN [msdb].[dbo].[backupmediafamily] bmf ON bs.media_set_id = bmf.media_set_id
INNER JOIN sys.databases d ON d.name = s.dbName 
WHERE s.rnk = 1", commandTimeout: _systemQueryTimeout)).Where(d => !restoreCommand.IsDatabaseIgnored(d.DbName));
        }

        private async Task<IEnumerable<DatabaseInfo>> DatabasesQueryAsync(RestoreCommand restoreCommand)
        {
            return (await SqlConnection.GetUserDatabasesAsync()).Where(d => !restoreCommand.IsDatabaseIgnored(d.Name));
        }

        public async Task LoadServerInfosAsync()
        {
            ServerInfos = await SqlConnection.GetServerInfosAsync();
        }

        private const int _systemQueryTimeout = 10;

        public Task EnsuredIndexsExistsAsync()
        {
            return SqlConnection.ExecuteAsync(@"USE [msdb]
IF NOT EXISTS (SELECT 1 FROM sys.indexes 
    WHERE name='IX_restorehistory_destination_database_name' 
    AND object_id = OBJECT_ID('dbo.restorehistory'))
BEGIN
	CREATE NONCLUSTERED INDEX [IX_restorehistory_destination_database_name]
	ON [dbo].[restorehistory] ([destination_database_name])
	INCLUDE ([restore_date],[backup_set_id])
END", commandTimeout: _systemQueryTimeout * 6);
        }
    }

    public static class QueryHelpers
    {
        public static Task<ServerInfos> GetServerInfosAsync(this SqlConnection sqlConnection) =>
            sqlConnection.QueryFirstAsync<ServerInfos>(@"SELECT
HOST_NAME() as Hostname,  
SERVERPROPERTY('InstanceDefaultDataPath') AS DataPath,
SERVERPROPERTY('InstanceDefaultLogPath') AS LogPath,
@@version as Version,
@@SERVERNAME as ServerName", commandTimeout: _systemQueryTimeout);

        public static Task<IEnumerable<DatabaseInfo>> GetUserDatabasesAsync(this SqlConnection sqlConnection) =>
            sqlConnection.QueryAsync<DatabaseInfo>(@"select name, state 
from sys.databases where database_id > 4 ", commandTimeout: _systemQueryTimeout);

        private const int _systemQueryTimeout = 10;
    }
}
