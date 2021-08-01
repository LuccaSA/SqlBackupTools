using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;

namespace SqlBackupTools
{
    [Verb("restore", HelpText = "Restore databases")]
    public class RestoreCommand : GeneralCommandInfos
    {
        [Option('f', "folder", Required = true, HelpText = "Root backup folder")]
        public IEnumerable<DirectoryInfo> BackupFolders { get; set; }
        
        [Option('b', "brentozar", HelpText = "Brentozar mode")]
        public bool Brentozar { get; set; }

        [Option("fullOnly", HelpText = "Restore only backup full. If false, then everything is restored")]
        public bool FullOnly { get; set; }

        [Option("databaseName", HelpText = "Filter on specific databases")]
        public IEnumerable<string> Databases { get; set; }

        [Option("ignoreDatabases", HelpText = "Exclude specific databases from all the process")]
        public IEnumerable<string> IgnoreDatabases { get; set; }

        private HashSet<string> _ignoredDbs;
        private readonly object _ignoredDbLock = new object();
        internal HashSet<string> GetIgnoredDbs()
        {
            if (_ignoredDbs != null)
                return _ignoredDbs;
            lock (_ignoredDbLock)
            {
                _ignoredDbs ??= (IgnoreDatabases ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            }
            return _ignoredDbs;
        }

        public bool IsDatabaseIgnored(string name)
        {
            var ignoreList = GetIgnoredDbs();
            if (ignoreList.Count == 0)
            {
                return false;
            }
            if (ignoreList.Contains(name))
            {
                return true;
            }
            foreach (var i in ignoreList)
            {
                if (name.Contains(i, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        [Option("postScripts", HelpText = "Sql post scripts to execute")]
        public IEnumerable<FileInfo> PostScripts { get; set; } = Enumerable.Empty<FileInfo>();
        internal List<string> LoadedScript = null;
        internal readonly object ScriptLock = new object();

        [Option("postScriptFilter", HelpText = "Execute script on databases starting with postScriptFilter")]
        public string PostScriptFilter { get; set; }

        [Option("continueLogs", HelpText = "Whether or not you are continuing to restore logs after the database has already been restored without recovering it")]
        public bool ContinueLogs { get; set; }

        [Option("runRecovery", HelpText = "Whether or not to recover the database (RESTORE DATABASE WITH RECOVERY so that it is now usable)")]
        public bool RunRecovery { get; set; }

        [Option("rpoLimit", HelpText = "Limit in minutes before creating a RPO warning")]
        public int? RpoLimit { get; set; }

        [Option("maxTransferSize", HelpText = "RESTORE MAXTRANSFERSIZE option. Maximum value : 4194304")]
        public int? MaxTransferSize { get; internal set; }

        [Option("bufferCount", HelpText = "RESTORE BUFFERCOUNT. Don't try too high values")]
        public int? BufferCount { get; internal set; }

        [Option("noChecksum", HelpText = "RESTORE NO_CHECKSUM. Ignore checksum while restoring.")]
        public bool NoChecksum { get; internal set; }

        [Option("reverseOrder", HelpText = "Start with small databases")]
        public bool ReverseOrder { get; set; }

        [Option("unchecked", HelpText = "Folder with backup to \"consume\". Must be used in conjunction with checked folder")]
        public IEnumerable<DirectoryInfo> Unchecked { get; set; }

        [Option("checked", HelpText = "Folder to move \"consumed\" backups. Must be used in conjunction with unchecked folder")]
        public DirectoryInfo Checked { get; set; }

        [Option("legacy", HelpText = "Legacy mode")]
        public bool Legacy { get; set; }

        [Option("IgnoreAlreadyPresentInMsdb", HelpText = "Ignore LOG backups already present in msdb")]
        public bool IgnoreAlreadyPresentInMsdb { get; set; }

        public bool IsUncheckedModeEnable
        {
            get
            {
                if (Unchecked == null && Checked != null || Checked != null && Checked == null)
                {
                    throw new CommandLineException("Checked and Unchecked must both be defined");
                }
                return Unchecked != null && Checked != null;
            }
        }

        [Option("checkDb", HelpText = "runs DBCC CHECKDB")]
        public bool CheckDb { get; set; }
    }
}
