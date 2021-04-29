using Microsoft.Data.SqlClient;

namespace SqlBackupTools.Restore.Native
{
    public static class SqlExceptionExtension
    {
        public static bool IsRecoverable(this SqlException sql)
        {
            return
                sql.Number ==  4305 || //The log in this backup set terminates at LSN, which is too early to apply to the database.
                sql.Number == 4319 || //A previous restore operation was interrupted and did not complete processing
                sql.Number == 824 || //SQL Server detected a logical consistency-based I/O error
                sql.Number ==  3446 || //Primary log file is not available for database '%.*ls'. The log cannot be backed up.
                sql.Number == 3101 || //Exclusive access could not be obtained because the database is in use.
                sql.Number == 3201; // Cannot open backup device.
        }
    }
}
