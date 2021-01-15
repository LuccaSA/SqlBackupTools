using System;

namespace SqlBackupTools.Restore.Native
{
    public class FileListInfo
    {
        public string LogicalName { get; set; }
        public string PhysicalName { get; set; }
        public char Type { get; set; }
        public string FileGroupName { get; set; }
        public decimal Size { get; set; }
        public decimal MaxSize { get; set; }
        public long FileID { get; set; }
        public decimal CreateLSN { get; set; }
        public decimal DropLSN { get; set; }
        public Guid UniqueID { get; set; }
        public decimal ReadOnlyLSN { get; set; }
        public decimal ReadWriteLSN { get; set; }
        public long BackupSizeInBytes { get; set; }
        public int SourceBlockSize { get; set; }
        public int FileGroupID { get; set; }
        public Guid LogGroupGUID { get; set; }
        public decimal DifferentialBaseLSN { get; set; }
        public Guid DifferentialBaseGUID { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsPresent { get; set; }
        public byte[] TDEThumbprint { get; set; }
        public string SnapshotUrl { get; set; }
    }
}