using System;

namespace SqlBackupTools
{
    public class CommandLineException : Exception
    {
        public CommandLineException(string message)
            : base(message)
        {
        }
    }
}