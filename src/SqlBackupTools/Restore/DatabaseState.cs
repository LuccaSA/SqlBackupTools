namespace SqlBackupTools.Restore
{
    public enum DatabaseState
    {
        ONLINE = 0,
        RESTORING = 1,
        RECOVERING = 2,
        RECOVERY_PENDING = 3,
        SUSPECT = 4,
        EMERGENCY = 5,
        OFFLINE = 6
    }
}