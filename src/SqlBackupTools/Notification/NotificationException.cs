using System;

namespace SqlBackupTools.Notification
{
    public class NotificationException : Exception
    {
        public NotificationException(string message)
            : base(message)
        {
        }
    }
}
