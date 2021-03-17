using System.Collections.Generic;
using System.IO;
using CommandLine;

namespace SqlBackupTools
{
    [Verb("clean", HelpText = "Clean backup folders")]
    public class CleanCommand : GeneralCommandInfos
    {
        [Option('f', "folder", Required = true, HelpText = "Root backup folder")]
        public IEnumerable<DirectoryInfo> BackupFolders { get; set; }
        
        [Option('c', "cleanedFolder", Required = true, HelpText = "Root folder to move old database backups into")]
        public DirectoryInfo CleanedFolder { get; set; }

        [Option(  "force", Required = true, HelpText = "force the clean operation")]
        public bool Force { get; set; }
    }
}
