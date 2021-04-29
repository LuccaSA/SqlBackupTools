using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace SqlBackupTools.Restore.Native
{
    public class NativeRestoreMethod : IRestoreMethod
    {

        private readonly RestoreState _state;

        public NativeRestoreMethod(RestoreState state)
        {
            _state = state;
        }

        public Task<Exception> RestoreFullAsync(RestoreItem item)
        {
            return InternalFullDiffLogAsync(item, startFromFull: true, restoreLogs: false);
        }

        public Task<Exception> RestoreFullDiffLogAsync(RestoreItem item, bool startFromFull)
        {
            return InternalFullDiffLogAsync(item, startFromFull, restoreLogs: true);
        }

        public async Task<Exception> RunRecoveryAsync(RestoreItem item)
        {
            var sb = new StringBuilder();
            sb.Append("RESTORE DATABASE [");
            sb.Append(item.Name);
            sb.Append("] WITH RECOVERY");
            try
            {
                await using var sqlConnection = _state.RestoreCommand.CreateConnectionMars();
                _state.Loggger.Debug("running RECOVERY : " + item.Name);
                await sqlConnection.ExecuteAsync(sb.ToString(), commandTimeout: _state.RestoreCommand.Timeout);
            }
            catch (Exception e)
            {
                _state.Loggger.Debug(e, item.Name + " : Error while running RECOVERY");
                return e;
            }
            return null;
        }

        private async Task<Exception> InternalFullDiffLogAsync(RestoreItem item, bool startFromFull, bool restoreLogs)
        {
            await using var sqlConnection = _state.RestoreCommand.CreateConnectionMars();

            bool modeFileList = false;
            bool forceRestoreFull = startFromFull;
            int maxRetry = 2;
            while (maxRetry > 0)
            {
                maxRetry--;
                try
                {
                    FileInfo lastRestored = null;
                    if (!_state.ActualDbs.TryGetValue(item.Name, out var databaseInfo) ||
                        databaseInfo.State != DatabaseState.RESTORING ||
                        forceRestoreFull)
                    {
                        lastRestored = await RestoreFullAsync(item, modeFileList, sqlConnection);
                        item.RpoRecentRestore = lastRestored.BackupDate();
                    }
                    else
                    {
                        if (_state.RestoredDbs.TryGetValue(item.Name, out RestoreHistoryInfo historyInfo) &&
                            !string.IsNullOrEmpty(historyInfo.LastBackupPath))
                        {
                            lastRestored = new FileInfo(historyInfo.LastBackupPath);
                            var restoreLogsFrom = lastRestored.BackupDate();
                            item.RpoRecentRestore = restoreLogsFrom;
                            _state.Loggger.Debug("Last restore made :  path=" + historyInfo.LastBackupPath + ", date=" +
                                                 restoreLogsFrom);
                        }
                        else
                        {
                            throw new BackupRestoreException("No backup full, and no database in recovering state, can't continue");
                        }
                    }

                    if (restoreLogs && !_state.RestoreCommand.FullOnly)
                    {
                        await RestoreLogAsync(item, lastRestored, sqlConnection);
                    }

                    break;
                }
                catch (SqlException sqle) when (sqle.IsRecoverable())
                {
                    // RESTORE DATABASE is terminating abnormally.
                    item.StatsDropped++;
                    _state.Loggger.Debug(sqle, item.Name + " : Error on first attempt, retrying from scratch");
                    forceRestoreFull = true;
                    bool singleUserMode = false;
                    try
                    {
                        if (_state.ActualDbs.TryGetValue(item.Name, out var databaseInfo) && databaseInfo.State != DatabaseState.RESTORING)
                        {
                            singleUserMode = true;
                            await sqlConnection.ExecuteAsync($"ALTER DATABASE [{item.Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", commandTimeout: _state.RestoreCommand.Timeout);
                        }
                        await sqlConnection.ExecuteAsync($"DROP DATABASE [{item.Name}]", commandTimeout: _state.RestoreCommand.Timeout);
                        await sqlConnection.ExecuteAsync($"EXEC msdb.dbo.sp_delete_database_backuphistory @database_name = N'{item.Name}'", commandTimeout: _state.RestoreCommand.Timeout);
                    }
                    catch (Exception e)
                    {
                        _state.Loggger.Error(e, $"Error while trying to drop db{(singleUserMode ? " in SINGLE_USER mode" : string.Empty)}");
                        return e;
                    }
                }
                catch (SqlException sqle)
                    when (sqle.Number == 3234 || sqle.Number == 5133)
                {
                    // 3234 : logical filename mismatch, try with FileInfo
                    // 5133 : Directory lookup for the file failed with the operating system error
                    modeFileList = true;
                }
                catch (Exception e)
                {
                    return e;
                }
            }
            return null;
        }

        private async Task<FileInfo> RestoreFullAsync(RestoreItem item, bool modeFileList, SqlConnection sqlConnection)
        {
            FileInfo fullFile = item.Full
                .EnumerateFiles("*.bak", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.BackupDate())
                .FirstOrDefault();

            if (fullFile == null)
            {
                throw new BackupRestoreException(item.Name + " : No backup full found");
            }

            var fullCmd = await RestoreFullSqlCommandAsync(item, modeFileList, sqlConnection, fullFile);
            try
            {
                _state.Loggger.Debug("restoring FULL : " + fullFile.FullName);
                await sqlConnection.ExecuteAsync(fullCmd, commandTimeout: _state.RestoreCommand.Timeout);
                item.StatsFullRestored++;
            }
            catch (Exception e)
            {
                _state.Loggger.Debug(e, item.Name + " : Error while restoring FULL with command " + fullCmd);
                throw;
            }


            return fullFile;
        }

        private async Task RestoreLogAsync(RestoreItem item, FileInfo lastRestored, SqlConnection sqlConnection)
        {
            if (item.Log == null)
                return;

            DateTime restoreLogsFrom = lastRestored.BackupDate();

            FileInfo lastLogFile = null;
            if (lastRestored?.Directory?.Name == "LOG")
            {
                lastLogFile = lastRestored;
            }
            _state.Loggger.Debug("Scraping TRN files from " + restoreLogsFrom);

            var backupLogsToRestore = new Stack<(FileInfo logFile, RetryStrategy retry)>();
            foreach (var logFileInfo in item.Log
                .EnumerateFiles("*.trn", SearchOption.TopDirectoryOnly)
                .Where(l => l.BackupDate() > restoreLogsFrom)
                .OrderByDescending(l => l.BackupDate()))
            {
                backupLogsToRestore.Push((logFileInfo, RetryStrategy.None));
            }

            string cmdLog = string.Empty;
            try
            {
                _state.Loggger.Debug(backupLogsToRestore.Count + " trn files to restore");

                int retryCountUsedFile = 3;
                while (backupLogsToRestore.Count != 0)
                {
                    var (currentLogFile, retryStrategy) = backupLogsToRestore.Pop();
                    try
                    {
                        _state.Loggger.Debug("restoring LOG : " + currentLogFile.FullName);

                        switch (retryStrategy)
                        {
                            case RetryStrategy.None:
                                cmdLog = RestoreLogCommand(item, currentLogFile);
                                await sqlConnection.ExecuteAsync(cmdLog, commandTimeout: _state.RestoreCommand.Timeout);
                                break;
                            case RetryStrategy.ExtractHeaders:
                                var infos = await sqlConnection.QueryAsync<Headers>(
                                    $"RESTORE HEADERONLY FROM DISK='{currentLogFile.FullName}'",
                                    commandTimeout: _state.RestoreCommand.Timeout);

                                foreach (var info in infos.OrderBy(i => int.Parse(i.Position, NumberFormatInfo.InvariantInfo)))
                                {
                                    cmdLog = RestoreLogCommand(item, currentLogFile, int.Parse(info.Position, NumberFormatInfo.InvariantInfo));
                                    await sqlConnection.ExecuteAsync(cmdLog, commandTimeout: _state.RestoreCommand.Timeout);
                                }
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(retryStrategy));
                        }
                    }
                    catch (SqlException sqle)
                        when (sqle.Number == 3634 || sqle.Number == 3201)
                    {
                        //3634 : The process cannot access the file because it is being used by another process.
                        //3203 : Cannot open backup device.
                        if (retryCountUsedFile-- == 0)
                        {
                            throw;
                        }
                        await Task.Delay(10_000); // 10s
                        backupLogsToRestore.Push((currentLogFile, RetryStrategy.None));
                    }
                    catch (SqlException sqle)
                        when (sqle.Number == 4326)
                    {
                        _state.Loggger.Debug($"Too early log file selected (Error 4326)[{item.FullName}], retrying with next one");
                        _state.Loggger.Debug(sqle.Message);
                    }
                    catch (SqlException sqle)
                        when (sqle.Number == 4305)
                    {
                        //The log in this backup set begins at LSN x, which is too recent to apply to the database. An earlier log backup that includes LSN x can be restored.

                        // - retrying the last log with extract from HeaderOnly
                        // - adding the current one again
                        if (lastLogFile == null)
                        {
                            throw new BackupRestoreException("todo : extract last trn from backup history");
                        }
                        backupLogsToRestore.Push((currentLogFile, RetryStrategy.None));
                        backupLogsToRestore.Push((lastLogFile, RetryStrategy.ExtractHeaders));
                    }

                    lastLogFile = currentLogFile;
                    item.StatsLogRestored++;
                }

            }
            catch (Exception e)
            {
                _state.Loggger.Debug(cmdLog);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Remaining logs: " + backupLogsToRestore.Count);
                while (backupLogsToRestore.TryPop(out (FileInfo logFile, RetryStrategy retry) logItem))
                {
                    sb.AppendLine(logItem.logFile.Name);
                }
                _state.Loggger.Debug(e, item.Name + " " + sb);
                throw;
            }
        }

        private async Task<string> RestoreFullSqlCommandAsync(RestoreItem item, bool modeFileList,
            SqlConnection sqlConnection, FileInfo fullFile)
        {
            var dataPath = Path.Combine(_state.ServerInfos.DataPath, item.Name + ".mdf");
            var logPath = Path.Combine(_state.ServerInfos.LogPath, item.Name + "_log.ldf");

            string logicalData = item.Name;
            string logicalLog = $"{item.Name}_Log";

            if (modeFileList)
            {
                var infos = await sqlConnection.QueryAsync<FileListInfo>($"RESTORE FILELISTONLY FROM DISK='{fullFile.FullName}'",
                    commandTimeout: _state.RestoreCommand.Timeout);
                logicalData = infos.Where(f => f.Type == 'D').Select(f => f.LogicalName).First();
                logicalLog = infos.Where(f => f.Type == 'L').Select(f => f.LogicalName).First();
            }

            var sb = new StringBuilder();
            sb.Append("RESTORE DATABASE [");
            sb.Append(item.Name);
            sb.Append("] FROM DISK = N'");
            sb.Append(fullFile.FullName);
            sb.Append("' WITH NORECOVERY");
            sb.Append(", REPLACE, MOVE '");
            sb.Append(logicalData);
            sb.Append("' TO '");
            sb.Append(dataPath);
            sb.Append("', MOVE '");
            sb.Append(logicalLog);
            sb.Append("' TO '");
            sb.Append(logPath);
            sb.Append('\'');
            if (_state.RestoreCommand.MaxTransferSize.HasValue)
            {
                sb.Append(", MAXTRANSFERSIZE = ");
                sb.Append(_state.RestoreCommand.MaxTransferSize.Value);
            }
            if (_state.RestoreCommand.BufferCount.HasValue)
            {
                sb.Append(", BUFFERCOUNT = ");
                sb.Append(_state.RestoreCommand.BufferCount.Value);
            }
            if (_state.RestoreCommand.NoChecksum)
            {
                sb.Append(", NO_CHECKSUM");
            }
            return sb.ToString();
        }

        private string RestoreLogCommand(RestoreItem item, FileInfo lastLog, int fileId = 1)
        {
            var sb = new StringBuilder();
            sb.Append("RESTORE LOG [");
            sb.Append(item.Name);
            sb.Append("] FROM  DISK = N'");
            sb.Append(lastLog.FullName);

            sb.Append("' WITH FILE = ");
            sb.Append(fileId);
            sb.Append(", ");

            sb.Append("NORECOVERY");

            if (_state.RestoreCommand.MaxTransferSize.HasValue)
            {
                sb.Append(", MAXTRANSFERSIZE = ");
                sb.Append(_state.RestoreCommand.MaxTransferSize.Value);
            }

            if (_state.RestoreCommand.BufferCount.HasValue)
            {
                sb.Append(", BUFFERCOUNT = ");
                sb.Append(_state.RestoreCommand.BufferCount.Value);
            }

            if (_state.RestoreCommand.NoChecksum)
            {
                sb.Append(", NO_CHECKSUM");
            }

            return sb.ToString();
        }
    }
}
