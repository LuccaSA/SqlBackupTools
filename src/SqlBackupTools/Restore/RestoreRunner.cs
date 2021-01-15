using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using ParallelAsync;
using Serilog;
using SqlBackupTools.Notification;
using SqlBackupTools.Restore.Brentozar;
using SqlBackupTools.Restore.Native;

namespace SqlBackupTools.Restore
{
    public static class RestoreRunner
    {
        public static async Task RestoreAsync(RestoreCommand restoreCommand, ILogger logger, CancellationToken cancellationToken)
        {
            await using var connection = restoreCommand.CreateConnectionMars();

            var state = new RestoreState(logger, restoreCommand, connection);
            state.LogStarting();

            await state.EnsuredIndexsExistsAsync();

            await Task.WhenAll(
                state.LoadServerInfosAsync(),
                state.LoadDatabasesAsync(),
                state.LoadRestoreHistoryAsync(),
                state.PrepareRestoreJobAsync());

            logger.Information($"{state.Directories.Count} directories found");
            logger.Information("Starting...");
            var po = new ParallelizeOption
            {
                FailMode = Fail.Smart,
                MaxDegreeOfParallelism = restoreCommand.Threads
            };

            IRestoreMethod method = restoreCommand.Brentozar
                ? (IRestoreMethod)new BrentozarRestoreMethod(state, connection)
                : new NativeRestoreMethod(state);

            await state.Directories
                .ParallelizeAsync((item, cancel) => RestoreBackupAsync(method, item, state), po, cancellationToken)
                .ForEachAsync((value, token) =>
                {
                    state.Accumulated = state.Accumulated.Add(value.Item.Elapsed);
                    return Task.CompletedTask;
                });

            var report = await state.GetReportStateAsync();
            state.LogFinished(report);

            var slack = new SlackSender(new SlackClient(logger));
            await slack.ReportAsync(report, state.RestoreCommand.SlackChannel, state.RestoreCommand.SlackSecret, state.RestoreCommand.SlackOnlyOnError);

            await report.SendMailAsync(state.RestoreCommand.Email, state.RestoreCommand.Smtp, cancellationToken);

            if (report.Status.HasFlag(ReportStatus.Error))
            {
                Environment.ExitCode = 1;
            }
        }

        private static async Task<RestoreItem> RestoreBackupAsync(IRestoreMethod restoreMethod, RestoreItem item, RestoreState state)
        {
            RestoreCommand restoreCommand = state.RestoreCommand;

            state.Loggger.Debug($"Starting restore for {item.Name}");
            item.SetStart();

            await using var sqlConnection = restoreCommand.CreateConnectionMars();
            Exception exception;
            if (restoreCommand.FullOnly)
            {
                exception = await restoreMethod.RestoreFullAsync(item);
            }
            else
            {
                bool startFromFull = StartFromFull(state, item);
                exception = await restoreMethod.RestoreFullDiffLogAsync(item, startFromFull);
            }

            var incremented = state.Increment();
            state.Loggger.Information($"[{incremented}/{state.Directories.Count}] OK : {item.Name} {item.Stats()}");

            if (exception != null)
            {
                state.ExceptionBackupFull.Add((exception, item));
                item.SetError(exception);
                state.Loggger.Error(exception, $"Error restoring {item.Name}");
                return item;
            }

            try
            {
                Exception scriptException = null;
                if (restoreCommand.RunRecovery)
                {
                    await using var sqlConnectionOnDatabase = restoreCommand.CreateConnectionMars(item.Name);
                    scriptException = await PostScriptExecuteAsync(state, sqlConnectionOnDatabase, item);
                }

                if (scriptException != null)
                {
                    state.ExceptionBackupFull.Add((scriptException, item));
                    item.SetError(scriptException);
                }

                if (restoreCommand.IsUncheckedModeEnable)
                {
                    // overwrite destination if exists
                    var target = Path.Combine(restoreCommand.Checked.FullName, item.Name);
                    if (Directory.Exists(target))
                    {
                        Directory.Delete(target, true);
                    }
                    item.BaseDirectoryInfo.MoveTo(target);
                }

                if (scriptException != null)
                {
                    return item;
                }
            }
            catch (Exception e)
            {
                state.Loggger.Error(e, "error moving folder");
                item.SetError(e);
                state.Loggger.Error(e, $"Error moving {item.Name}");
                return item;
            }

            item.SetSuccess();
            state.OkBackupFull.Add(item);

            state.Loggger.Debug($"Finished restore for {item.Name}");
            return item;
        }

        private static bool StartFromFull(RestoreState state, RestoreItem item)
        {
            if (!state.ActualDbs.ContainsKey(item.Name))
            {
                // no db present, we need to start from full
                return true;
            }

            if (state.ActualDbs[item.Name].State != DatabaseState.RESTORING)
            {
                // need restart from scratch
                state.Loggger.Debug($"Can't continue logs : {item.Name} in state {state.ActualDbs[item.Name].State}. Restarting from FULL.");
                return true;
            }

            // db present in restoring mode, we can continue logs
            return false;
        }

        private static async Task<Exception> PostScriptExecuteAsync(RestoreState state, SqlConnection sqlConnection, RestoreItem restoreItem)
        {
            if (state.RestoreCommand.PostScripts == null)
            {
                return null;
            }
            if (!state.RestoreCommand.PostScripts.Any())
            {
                state.RestoreCommand.PostScripts = null;
                return null;
            }

            if (!string.IsNullOrEmpty(state.RestoreCommand.PostScriptFilter))
            {
                if (!restoreItem.Name.StartsWith(state.RestoreCommand.PostScriptFilter, StringComparison.OrdinalIgnoreCase))
                {
                    state.Loggger.Debug("Ignoring database " + restoreItem.Name + " because of PostScriptFilter : " + state.RestoreCommand.PostScriptFilter);
                    return null;
                }
            }

            try
            {
                var scripts = LoadScripts(state.RestoreCommand);
                state.Loggger.Debug("Applying " + scripts.Count + " sql scripts"); 
                foreach (var script in scripts)
                {
                    await sqlConnection.ExecuteAsync(script, commandTimeout: 180);
                }
            }
            catch (Exception e)
            {
                state.Loggger.Error(e, $"[{restoreItem.Name}] Error while executing post scripts");
                return e;
            }
            return null;
        }

        private static List<string> LoadScripts(RestoreCommand restoreCommand)
        {
            if (restoreCommand.LoadedScript != null)
            {
                return restoreCommand.LoadedScript;
            }
            lock (restoreCommand.ScriptLock)
            {
                restoreCommand.LoadedScript = new List<string>();
                foreach (var file in restoreCommand.PostScripts)
                {
                    string data = File.ReadAllText(file.FullName);
                    restoreCommand.LoadedScript.AddRange(Regex.Split(data, @"\bGO\b").Where(i => !string.IsNullOrWhiteSpace(i)));
                }
                return restoreCommand.LoadedScript;
            }
        }
    }
}
