// Personel loglarını listeleme tarafında “input hygiene” yapar:
// tarih/limit/aksiyon filtrelerini normalize eder ve repository sorgusuna güvenli parametrelerle iletir.

using RestaurantWeb.Data;
using RestaurantWeb.Models;

namespace RestaurantWeb.Services
{
    public class PersonelLogService : IPersonelLogService
    {
        private const int DefaultLimit = 200; 
        private const int MaxLimit = 1000;

        // UI dışından gelebilecek değerler için whitelist
        // (repo tarafında string birleştirme yok ama iş kuralı olarak kısıtlıyoruz)
        private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase) 
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

        // Log listesi: filtreleri normalize eder, limit’i sınırlar, aksiyon filtresini doğrular.
        public OperationResult<List<PersonelLog>> GetList(
            DateTime? baslangic,
            DateTime? bitis,
            string? aksiyon,
            string? targetKullaniciAdi,
            int limit = DefaultLimit 
        )
        {
            try
            {
                aksiyon = string.IsNullOrWhiteSpace(aksiyon) ? null : aksiyon.Trim(); 
                targetKullaniciAdi = string.IsNullOrWhiteSpace(targetKullaniciAdi) ? null : targetKullaniciAdi.Trim(); 

                if (limit <= 0) limit = DefaultLimit; 
                if (limit > MaxLimit) limit = MaxLimit; 

                if (aksiyon != null && !AllowedActions.Contains(aksiyon)) 
                    return OperationResult<List<PersonelLog>>.Fail("Geçersiz aksiyon filtresi.");

                // Tarih aralığı ters verilirse düzelt 
                if (baslangic.HasValue && bitis.HasValue && bitis.Value.Date < baslangic.Value.Date) 
                    (baslangic, bitis) = (bitis, baslangic); 

                var list = _repo.GetList(baslangic, bitis, aksiyon, targetKullaniciAdi, limit); 
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
