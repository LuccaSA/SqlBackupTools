using System;

namespace SqlBackupTools.Restore
{
    public class DatabaseInfo
    {
        public string Name { get; set; }
        public DatabaseState State { get; set; }
    }

    public class RestoreHistoryInfo
    {
        public string DbName { get; set; }
        public DatabaseState State { get; set; }
        public string LastBackupPath { get; set; }
        public DateTime RestoreDate { get; set; }
    }
}