using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public interface IBackupService
    {
        OperationResult<string> CreateBackup(string? actorUsername); 
        OperationResult<List<BackupItemVm>> ListBackups();
        OperationResult<(byte[] Bytes, string ContentType, string DownloadName)> GetBackupFile(string fileName);
        OperationResult DeleteBackup(string fileName);
    }
}
