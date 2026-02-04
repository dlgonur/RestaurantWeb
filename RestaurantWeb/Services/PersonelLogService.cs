using RestaurantWeb.Data;
using RestaurantWeb.Models;

namespace RestaurantWeb.Services
{
    public class PersonelLogService : IPersonelLogService
    {
        private const int DefaultLimit = 200; // ★
        private const int MaxLimit = 1000; // ★

        private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase) // ★
        {
            "CREATE",
            "UPDATE",
            "TOGGLE_ACTIVE",
            "SET_PASSWORD"
        };

        private readonly PersonelLogRepository _repo;
        private readonly ILogger<PersonelLogService> _logger;

        public PersonelLogService(PersonelLogRepository repo, ILogger<PersonelLogService> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public OperationResult<List<PersonelLog>> GetList(
            DateTime? baslangic,
            DateTime? bitis,
            string? aksiyon,
            string? targetKullaniciAdi,
            int limit = DefaultLimit // ★
        )
        {
            try
            {
                // normalize
                aksiyon = string.IsNullOrWhiteSpace(aksiyon) ? null : aksiyon.Trim(); // ★
                targetKullaniciAdi = string.IsNullOrWhiteSpace(targetKullaniciAdi) ? null : targetKullaniciAdi.Trim(); // ★

                // limit cap
                if (limit <= 0) limit = DefaultLimit; // ★
                if (limit > MaxLimit) limit = MaxLimit; // ★

                // aksiyon whitelist (UI dışından gelirse reject)
                if (aksiyon != null && !AllowedActions.Contains(aksiyon)) // ★
                    return OperationResult<List<PersonelLog>>.Fail("Geçersiz aksiyon filtresi."); // ★

                // tarih swap (controller’da da yapılabilir ama service daha güvenli)
                if (baslangic.HasValue && bitis.HasValue && bitis.Value.Date < baslangic.Value.Date) // ★
                    (baslangic, bitis) = (bitis, baslangic); // ★

                var list = _repo.GetList(baslangic, bitis, aksiyon, targetKullaniciAdi, limit); // ★
                return OperationResult<List<PersonelLog>>.Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Personel log listesi alınırken hata oluştu.");
                return OperationResult<List<PersonelLog>>.Fail("Teknik bir hata oluştu.");
            }
        }
    }
}
