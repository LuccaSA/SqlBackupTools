using System;
using System.Globalization;

namespace SqlBackupTools.Helpers
{
    public static class TimeSpanExtensions
    {
        public static String HumanizeSize(this long size)
        {
            string[] suf = { "B", "Ko", "Mo", "Go", "To", "Po", "Eo" };
            if (size == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(size);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(size) * num).ToString(CultureInfo.InvariantCulture) + " " + suf[place];
        }

        public static string HumanizedTimeSpan(this TimeSpan t, int parts = 2)
        {
            string result = string.Empty;
            if (t.TotalDays >= 1 && parts > 0)
            {
                result += $"{t.Days}d ";
                parts--;
            }
            if (t.TotalHours >= 1 && parts > 0)
            {
                result += $"{t.Hours}h ";
                parts--;
            }
            if (t.TotalMinutes >= 1 && parts > 0)
            {
                result += $"{t.Minutes}m ";
                parts--;
            }
            if (t.Seconds >= 1 && parts > 0)
            {
                result += $"{t.Seconds}s ";
                parts--;
            }
            if (t.Milliseconds >= 1 && parts > 0)
            {
                result += $"{t.Milliseconds}ms ";
            }
            return result.TrimEnd();
        }
    }
}
