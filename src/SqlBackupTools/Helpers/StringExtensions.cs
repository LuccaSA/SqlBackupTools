using System.Collections.Generic;

namespace SqlBackupTools.Helpers
{
    public static class StringExtensions
    {
        public static IEnumerable<T> Yield<T>(this T source)
        {
            yield return source;
        }
    }
}
