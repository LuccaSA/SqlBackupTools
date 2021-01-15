using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SqlBackupTools.Restore
{
    public class RestoreItem
    {
        private RestoreItem()
        {
            _stopwatch = new Stopwatch();
        }

        private readonly Stopwatch _stopwatch;


        public DirectoryInfo BaseDirectoryInfo { get; private set; }
        public DirectoryInfo Full { get; private set; }
        public DirectoryInfo Diff { get; private set; }
        public DirectoryInfo Log { get; private set; }
        public Exception Exception { get; private set; }

        public TimeSpan Elapsed => _stopwatch.Elapsed;
        public string Name => BaseDirectoryInfo.Name;
        public string FullName => BaseDirectoryInfo.FullName;

        public long FullSize { get; private set; }

        public int StatsFullRestored { get; set; }
        public int StatsDiffRestored { get; set; }
        public int StatsLogRestored { get; set; }
        public int StatsDropped { get; set; }
        public DateTime? RpoRecentRestore { get; internal set; }
        public DateTime? RpoCurrentRestore { get; internal set; }

        public string Stats()
        {
            if (StatsFullRestored + StatsDiffRestored + StatsLogRestored == 0)
            {
                return string.Empty;
            }
            var stats = new List<string>(); 
            if (StatsFullRestored != 0)
            {
                stats.Add(StatsFullRestored + " full");
            }
            if (StatsDiffRestored != 0)
            {
                stats.Add( + StatsDiffRestored + " diff");
            }
            if (StatsLogRestored != 0)
            {
                stats.Add(  + StatsLogRestored + " log");
            }
            if (StatsDropped != 0)
            {
                stats.Add(+StatsDropped + " drop database");
            }
            return $"({string.Join(", ", stats)})";
        }

        public static bool TryCreateRestoreItem(DirectoryInfo item, RestoreState state, out RestoreItem restoreItem)
        {
            restoreItem = new RestoreItem();
            restoreItem.BaseDirectoryInfo = item;

            if (!TryGetFullFolder(state.RestoreCommand, item, out var full))
            {
                state.MissingBackupFull.Add(restoreItem);
                return false;
            }

            restoreItem.Full = full;
            restoreItem.FullSize = full.GetFiles("*.bak").Sum(i => i.Length);

            restoreItem.Diff = item.EnumerateDirectories("DIFF").FirstOrDefault();
            restoreItem.Log = item.EnumerateDirectories("LOG").FirstOrDefault();

            return true;
        }

        private static bool TryGetFullFolder(RestoreCommand restoreCommand, DirectoryInfo item, out DirectoryInfo full)
        {
            if (restoreCommand.Legacy)
            {
                full = item;
            }
            else
            {
                full = item.EnumerateDirectories("FULL").FirstOrDefault();
            }
            return full != null && full.Exists;
        }

        public void SetStart()
        {
            _stopwatch.Start();
        }

        public void SetError(Exception exception)
        {
            Exception = exception;
            _stopwatch.Stop();
        }

        public void SetSuccess()
        {
            _stopwatch.Stop();
        }
    }
}
