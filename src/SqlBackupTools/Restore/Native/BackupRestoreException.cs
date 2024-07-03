using System;

namespace SqlBackupTools.Restore.Native
{
    public class BackupRestoreException : Exception
    {
        public BackupRestoreException(string message)
            : base(message)
        {
        }

        public BackupRestoreException(string message, Exception e)
            : base(message, e)
        {
        }
    }
}
