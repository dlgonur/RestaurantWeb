using Npgsql;
using RestaurantWeb.Data;
using RestaurantWeb.Models;
using RestaurantWeb.Models.Dtos;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public class SiparisService : ISiparisService
    {
        private readonly INpgsqlConnectionFactory _cf;
        private readonly SiparisRepository _siparisRepo;
        private readonly SiparisHistoryRepository _historyRepo;
        private readonly SiparisLogRepository _logRepo;

        public SiparisService(
            INpgsqlConnectionFactory cf,
            SiparisRepository siparisRepo,
            SiparisHistoryRepository historyRepo,
            SiparisLogRepository logRepo)
        {
            _cf = cf;
            _siparisRepo = siparisRepo;
            _historyRepo = historyRepo;
            _logRepo = logRepo;
        }

        public OperationResult<int> GetMasaNoById(int masaId)
            => _siparisRepo.GetMasaNoById(masaId);

        public OperationResult<int?> GetOpenOrderId(int masaId)
            => _siparisRepo.GetOpenOrderId(masaId);

        public OperationResult<SiparisAdisyonVm> GetSiparisAdisyon(int siparisId)
            => _siparisRepo.GetSiparisAdisyon(siparisId);

        public OperationResult<List<SiparisListItemVm>> GetOrders(DateTime baslangic, DateTime bitis, int? masaNo)
            => _historyRepo.GetOrders(baslangic, bitis, masaNo);

        public OperationResult<SiparisDetayVm> GetOrderDetail(int siparisId)
            => _historyRepo.GetOrderDetail(siparisId);

        public OperationResult<List<SiparisLogItemVm>> GetLogs(int siparisId)
            => _logRepo.GetLogs(siparisId);

        public OperationResult<List<ProductListItemDto>> GetActiveProducts(int? kategoriId = null) 
    => _siparisRepo.GetActiveProducts(kategoriId); 

        public OperationResult<List<CategoryItemDto>> GetActiveCategories() 
            => _siparisRepo.GetActiveCategories(); 


        public OperationResult SubmitOrder(int siparisId, List<(int UrunId, int Adet)> items)
        {
            if (siparisId <= 0) return OperationResult.Fail("Geçersiz sipariş id.");
            if (items == null || items.Count == 0) return OperationResult.Fail("Sepet boş.");

            using var conn = _cf.Create();
            conn.Open();

            using var tx = conn.BeginTransaction();
            try
            {
                var res = _siparisRepo.SubmitOrder(conn, tx, siparisId, items); // ★
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
                return OperationResult.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
            }
        }

        public OperationResult UpdateDiscountRate(int siparisId, decimal iskontoOran, string? actorUsername)
        {
            using var conn = _cf.Create();
            conn.Open();

            using var tx = conn.BeginTransaction();
            try
            {
                var res = _siparisRepo.UpdateDiscountRate(conn, tx, siparisId, iskontoOran, actorUsername); // ★
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
                return OperationResult.Fail("Beklenmeyen bir hata oluştu.");
            }
        }

        public OperationResult CloseOrderWithPayment(int siparisId, OdemeYontemi yontem, string? actorUsername, int kapatanPersonelId)
        {
            using var conn = _cf.Create();
            conn.Open();

            using var tx = conn.BeginTransaction();
            try
            {
                var res = _siparisRepo.CloseOrderWithPayment(conn, tx, siparisId, yontem, actorUsername, kapatanPersonelId); // ★
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
                return OperationResult.Fail("Beklenmeyen bir hata oluştu.");
            }
        }
    }
}
