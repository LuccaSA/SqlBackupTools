using System;

namespace SqlBackupTools
{
    public sealed class DangerousOperationException : Exception
    {
        public DangerousOperationException(string message) : base(message)
        {
        }
    }
}
