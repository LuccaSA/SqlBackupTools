using System;
using System.IO;

namespace SqlBackupTools.Restore
{
    public static class BackupExtensions
    {
        private static (int index, BackupFileType fileType) GetBackupType(ReadOnlySpan<char> name)
        {
            BackupFileType fileType = BackupFileType.None;
            int index = name.LastIndexOf("_LOG_", StringComparison.Ordinal);
            if (index != -1)
            {
                fileType = BackupFileType.LOG;
                index += 5;
            }
            else
            {
                index = name.LastIndexOf("_DIFF_", StringComparison.Ordinal);
                if (index != -1)
                {
                    fileType = BackupFileType.DIFF;
                    index += 6;
                }
                else
                {
                    index = name.LastIndexOf("_FULL_", StringComparison.Ordinal);
                    if (index != -1)
                    {
                        fileType = BackupFileType.FULL;
                        index += 6;
                    }
                }
            }

            return (index, fileType);
        }

        public static DateTime BackupDate(this FileInfo file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            ReadOnlySpan<char> name = file.Name;
            var (index, fileType) = GetBackupType(name);
            if (fileType != BackupFileType.None && TryParseBackupNameDate(name.Slice(index, 15), out DateTime date))
            {
                return date;
            }
            return file.LastWriteTime;
        }

        private static bool TryParseBackupNameDate(this ReadOnlySpan<char> segment, out DateTime dateTime)
        {
            //20200819_140220
            if (int.TryParse(segment.Slice(0, 4), out int year) &&
                int.TryParse(segment.Slice(4, 2), out int month) &&
                int.TryParse(segment.Slice(6, 2), out int day) &&
                int.TryParse(segment.Slice(9, 2), out int hour) &&
                int.TryParse(segment.Slice(11, 2), out int min) &&
                int.TryParse(segment.Slice(13, 2), out int sec))
            {
                dateTime = new DateTime(year, month, day, hour, min, sec);
                return true;
            }

            dateTime = DateTime.MinValue;
            return false;
        }
    }
}
