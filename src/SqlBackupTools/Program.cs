using System;
using CommandLine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SqlBackupTools.Restore;
using SqlBackupTools.SerilogAsync;

namespace SqlBackupTools
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (!args.TryParseCommand(out var command))
            {
                return 1;
            }

            using var logger = CreateLogger(command);
            using var cts = RegisterCancellation(logger);
            try
            {
                await command.LaunchCommandAsync(logger, cts.Token);
            }
            catch (Exception e)
            {
                logger.Error(e, "General failure");
                return 1;
            }
            return 0;
        }

        internal static async Task LaunchCommandAsync(this GeneralCommandInfos command, ILogger logger, CancellationToken ct)
        {
            switch (command)
            {
                case CleanCommand clean:
                    await CleanRunner.CleanAsync(clean, logger, ct);
                    break;
                case RestoreCommand restoreCommand:
                    await RestoreRunner.RestoreAsync(restoreCommand, logger, ct);
                    break;
                case DropDatabaseCommand drop:
                    await DropRunner.DropDatabasesAsync(drop, logger, ct);
                    break;
            }
        }

        internal static bool TryParseCommand(this string[] args, out GeneralCommandInfos command)
        {
            var parsed = Parser.Default.ParseArguments<RestoreCommand, CleanCommand, DropDatabaseCommand>(args);

            IEnumerable<Error> errors = null;
            command = parsed.MapResult(
                (RestoreCommand restoreCommand) => restoreCommand,
                (CleanCommand cleanCommand) => cleanCommand,
                (DropDatabaseCommand dropCommand) => dropCommand,
                parsingErrors =>
                {
                    errors = parsingErrors;
                    return (GeneralCommandInfos)null;
                });

            if (errors != null && errors.Any())
            {
                return false;
            }

            command.Validate();
            return true;
        }

        private static CancellationTokenSource RegisterCancellation(Logger logger)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, __) =>
            {
                logger.Warning("Cancellation requested");
                cts.Cancel();
            };
            return cts;
        }

        private static Logger CreateLogger(GeneralCommandInfos commandInfos)
        {
            var logFileName = commandInfos switch
            {
                CleanCommand _ => "clean.log",
                RestoreCommand _ => "restore.log",
                DropDatabaseCommand _ => "drop.log",
                _ => throw new NotImplementedException("Type not supported")
            };

            var level = commandInfos.Verbose ? LogEventLevel.Debug : LogEventLevel.Information;
            var loggerConf = new LoggerConfiguration()
                .WriteTo.Async(conf =>
                {
                    var path = commandInfos.LogsPath?.Exists == true ? Path.Combine(commandInfos.LogsPath.FullName, logFileName) : "logs/" + logFileName;
                    conf.File(path, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31);
                    conf.Console();
                }).MinimumLevel.Is(level);

            return loggerConf.CreateLogger();
        }
    }
}
