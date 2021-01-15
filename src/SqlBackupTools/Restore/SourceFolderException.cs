using System;

namespace SqlBackupTools.Restore
{
    public class SourceFolderException : Exception
    {
        public SourceFolderException()
            : base("Error while analyzing source folder")
        {

        }
    }
}
