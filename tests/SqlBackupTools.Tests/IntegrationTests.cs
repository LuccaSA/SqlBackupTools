using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Core;
using SqlBackupTools.Tests.Utils;
using Xunit;

namespace SqlBackupTools.Tests
{
    public class RestoreFullLog : IClassFixture<DatabaseBackupsFixture>
    {
        private readonly DatabaseBackupsFixture _backupsFixture;

        public RestoreFullLog(DatabaseBackupsFixture backupsFixture)
        {
            _backupsFixture = backupsFixture;
        }

        [Theory]
        [MemberData(nameof(GetRestoreCommands))]
        public async Task RestoreSimpleCases(RestoreCommand restoreCommand)
        {
            // prepare
            restoreCommand.Hostname = TestContext.SqlInstance;
            restoreCommand.Databases = _backupsFixture.DatabaseNames;
            restoreCommand.BackupFolders = new DirectoryInfo(_backupsFixture.Folder.FullName).GetDirectories();

            // restore
            await restoreCommand.LaunchCommandAsync(Logger.None, CancellationToken.None);

            // clean
            foreach (var db in _backupsFixture.DatabaseNames)
            {
                var sql = TestContext.SqlInstance.SqlConnection("master");
                await sql.DeleteDbAsync(db);
            }
        }

        public static IEnumerable<object[]> GetRestoreCommands
        {
            get
            {
                return new[]
                {
                    new object[] {new RestoreCommand {Brentozar = true}},
                    new object[] {new RestoreCommand {Brentozar = true, IgnoreAlreadyPresentInMsdb = true}},
                    new object[] {new RestoreCommand {Brentozar = true, FullOnly = true } },
                    new object[] {new RestoreCommand {ContinueLogs = true}},
                    new object[] {new RestoreCommand {FullOnly = true}},
                    new object[] {new RestoreCommand {NoChecksum = true}},
                    new object[] {new RestoreCommand {ReverseOrder = true}},
                    new object[] {new RestoreCommand {Threads = 1}},
                    new object[] {new RestoreCommand {Threads = 64}},
                    new object[] {new RestoreCommand {Timeout = 30}},
                };
            }
        }



    }
}
