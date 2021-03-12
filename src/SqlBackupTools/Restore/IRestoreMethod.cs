using System;
using System.Threading.Tasks;

namespace SqlBackupTools.Restore
{
    public interface IRestoreMethod
    {
        Task<Exception> RestoreFullAsync(RestoreItem item);
        Task<Exception> RestoreFullDiffLogAsync(RestoreItem item, bool startFromFull);
        Task<Exception> RunRecoveryAsync(RestoreItem item);
    }
}
