using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SqlBackupTools.Tests
{
    public static class TestContext
    {
        private static string _sqlInstance;
        private static DirectoryInfo _backupFolder;

        public static string SqlInstance
        {
            get
            {
                if (_sqlInstance == null)
                {
                    var fromEnv = Environment.GetEnvironmentVariable("TEST_SQL_INSTANCE");
                    _sqlInstance = string.IsNullOrWhiteSpace(fromEnv) ? "localhost" : fromEnv;
                }
                return _sqlInstance;
            }
        }

        public static DirectoryInfo BackupFolder
        {
            get
            {
                if (_backupFolder == null)
                {
                    var fromEnv = Environment.GetEnvironmentVariable("TEST_SQL_BACKUP_FOLDER");
                    if (string.IsNullOrWhiteSpace(fromEnv))
                    {
                        var dir = new DirectoryInfo(Path.GetTempPath()).CreateSubdirectory(Guid.NewGuid().ToString("N"));

                        _backupFolder = dir;
                    }
                    else
                    {
                        _backupFolder = new DirectoryInfo(fromEnv);
                    }

                    _backupFolder.SetFullAccessForEveryOne();

                }
                return _backupFolder;
            }
        }

        private static void SetFullAccessForEveryOne(this DirectoryInfo directoryInfo)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                var account = (NTAccount)sid.Translate(typeof(NTAccount));

                var acl = directoryInfo.GetAccessControl();
                acl.AddAccessRule(new FileSystemAccessRule(account.Value, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                directoryInfo.SetAccessControl(acl);
            }
            else
            {
                throw new PlatformNotSupportedException("TODO : ACL management for specific platforms");
            }
        }
    }
}
