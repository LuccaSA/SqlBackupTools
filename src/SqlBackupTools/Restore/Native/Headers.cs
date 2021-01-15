namespace SqlBackupTools.Restore.Native
{
    public class Headers
    { 
        public string BackupType { get; set; }
        public string Compressed { get; set; }
        public string Position { get; set; }
        public string DeviceType { get; set; }
        public string UserName { get; set; }
        public string ServerName { get; set; }
        public string DatabaseName { get; set; } 
    }
}