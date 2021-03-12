using System;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace SqlBackupTools.Restore.Brentozar
{
    public class BrentozarRestoreMethod : IRestoreMethod
    {
        private readonly SqlConnection _sqlConnection;
        private readonly RestoreState _state;

        public BrentozarRestoreMethod(RestoreState state, SqlConnection sqlConnection)
        {
            _state = state;
            _sqlConnection = sqlConnection;
        }

        public async Task<Exception> RestoreFullDiffLogAsync(RestoreItem item, bool startFromFull)
        {
            try
            {
                try
                {
                    if (_state.RestoreCommand.IgnoreAlreadyPresentInMsdb)
                    {
                        await _sqlConnection.ExecuteAsync("[dbo].[sp_DatabaseRestore]", new
                        {
                            Database = item.Name,
                            BackupPathFull = item.Full?.FullName,
                            BackupPathDiff = item.Diff?.FullName,
                            BackupPathLog = item.Log?.FullName,
                            ContinueLogs = _state.RestoreCommand.ContinueLogs ? 1 : 0,
                            RunRecovery = _state.RestoreCommand.RunRecovery ? 1 : 0,
                            IgnoreAlreadyPresentInMsdb = 1
                        },
                            commandTimeout: _state.RestoreCommand.Timeout,
                            commandType: CommandType.StoredProcedure);
                    }
                    else
                    {
                        await _sqlConnection.ExecuteAsync("[dbo].[sp_DatabaseRestore]", new
                        {
                            Database = item.Name,
                            BackupPathFull = item.Full?.FullName,
                            BackupPathDiff = item.Diff?.FullName,
                            BackupPathLog = item.Log?.FullName,
                            ContinueLogs = _state.RestoreCommand.ContinueLogs ? 1 : 0,
                            RunRecovery = _state.RestoreCommand.RunRecovery ? 1 : 0
                        },
                            commandTimeout: _state.RestoreCommand.Timeout,
                            commandType: CommandType.StoredProcedure);
                    }

                }
                catch (SqlException sqle) when (sqle.Number == 4319 || sqle.Number == 4305 || sqle.Number == 3013 || sqle.Number == 3119)
                {
                    _state.Loggger.Information($"[{item.Name}] : retry in full, error {sqle.Number}");
                    // previous restore ko, retry
                    await _sqlConnection.ExecuteAsync("[dbo].[sp_DatabaseRestore]", new
                    {
                        Database = item.Name,
                        BackupPathFull = item.Full?.FullName,
                        BackupPathDiff = item.Diff?.FullName,
                        BackupPathLog = item.Log?.FullName,
                        ContinueLogs = false,
                        RunRecovery = _state.RestoreCommand.RunRecovery ? 1 : 0
                    },
                        commandTimeout: _state.RestoreCommand.Timeout,
                        commandType: CommandType.StoredProcedure);

                    return sqle;
                }
            }
            catch (Exception e)
            {
                string message = $"Error restoring backup {item.Name} : {e.GetType().Name} : {e.Message}";
                message += $"{Environment.NewLine}FULL : {item.Full?.FullName}";
                message += $"{Environment.NewLine}DIFF : {item.Diff?.FullName}";
                message += $"{Environment.NewLine}LOG : {item.Log?.FullName}";
                _state.Loggger.Error(message);
                return e;
            }

            return null;
        }

        public async Task<Exception> RunRecoveryAsync(RestoreItem item)
        {
            var sb = new StringBuilder();
            sb.Append("RESTORE DATABASE [");
            sb.Append(item.Name);
            sb.Append("] WITH RECOVERY'");
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

        public async Task<Exception> RestoreFullAsync(RestoreItem item)
        {
            try
            {
                await _sqlConnection.ExecuteAsync("[dbo].[sp_DatabaseRestore]", new
                {
                    Database = item.Name,
                    BackupPathFull = item.Full?.FullName,
                    ContinueLogs = 0,
                    RunRecovery = _state.RestoreCommand.RunRecovery ? 1 : 0
                },
                    commandTimeout: _state.RestoreCommand.Timeout,
                    commandType: CommandType.StoredProcedure);
            }
            catch (Exception e)
            {
                string message = $"Error restoring backup {item.Name} : {e.GetType().Name} : {e.Message}";
                message += $"{Environment.NewLine}FULL : {item.Full?.FullName}";
                _state.Loggger.Error(message);
                return e;
            }
            return null;
        }
    }
}
