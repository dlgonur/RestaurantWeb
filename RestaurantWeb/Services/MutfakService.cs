// Mutfak operasyonlarını yönetir: mutfak kuyruğunu (bekleyen kalemler) getirir,
// ve kalem durumunu transaction içinde günceller + loglamayı repo üzerinden tetikler.
// Controller sadece HTTP/Partial döner; servis connection/transaction orkestrasyonudur.

using RestaurantWeb.Data;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public class MutfakService : IMutfakService
    {
        private readonly INpgsqlConnectionFactory _cf;
        private readonly MutfakRepository _repo;

        public MutfakService(INpgsqlConnectionFactory cf, MutfakRepository repo)
        {
            _cf = cf;
            _repo = repo;
        }

        // Mutfak kuyruğunu okur (read-only): connection açıp repo'ya delege eder.
        public OperationResult<List<MutfakSiparisVm>> GetPendingOrders()
        {
            try
            {
                using var conn = _cf.Create();
                conn.Open();

                return _repo.GetPendingOrders(conn);
            }
            catch
            {
                return OperationResult<List<MutfakSiparisVm>>.Fail("Beklenmeyen hata.");
            }
        }

        // Kalem durumunu günceller: write operasyonu olduğu için transaction ile atomic çalışır.
        // Repo burada hem update hem de sipariş log/akışını tek transaction içinde tutar.
        public OperationResult SetItemStatus(int kalemId, short durum, string? actorUsername)
        {
            using var conn = _cf.Create();
            conn.Open();

            using var tx = conn.BeginTransaction();
            try
            {
                var res = _repo.SetItemStatus(conn, tx, kalemId, durum, actorUsername); 
                if (!res.Success)
                {
                    tx.Rollback();
                    return res;
                }

                tx.Commit();
                return res;
            }
            catch
            {
                tx.Rollback();
                return OperationResult.Fail("Beklenmeyen hata.");
            }
        }
    }
}
