using System.IO;
using CommandLine;

namespace SqlBackupTools
{
    [Verb("clean", HelpText = "Clean backup folders")]
    public class CleanCommand : GeneralCommandInfos
    {
        [Option('c', "cleanedFolder", Required = true, HelpText = "Root folder to move old database backups into")]
        public DirectoryInfo CleanedFolder { get; set; }
    }
}