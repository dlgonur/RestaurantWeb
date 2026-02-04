using Npgsql;
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

        public OperationResult<List<MutfakSiparisVm>> GetPendingOrders()
        {
            try
            {
                using var conn = _cf.Create();
                conn.Open();

                // read-only => tx şart değil ama istersen BeginTransaction(IsolationLevel.ReadCommitted) da olur
                return _repo.GetPendingOrders(conn); // ★
            }
            catch
            {
                return OperationResult<List<MutfakSiparisVm>>.Fail("Beklenmeyen hata.");
            }
        }

        public OperationResult SetItemStatus(int kalemId, short durum, string? actorUsername)
        {
            using var conn = _cf.Create();
            conn.Open();

            using var tx = conn.BeginTransaction();
            try
            {
                var res = _repo.SetItemStatus(conn, tx, kalemId, durum, actorUsername); // ★
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
