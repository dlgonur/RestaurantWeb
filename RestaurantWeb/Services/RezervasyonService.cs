// Rezervasyon iş kuralları katmanı.
// Amaç: Controller'dan DB detaylarını ayırmak; transaction/lock, çakışma ve masa durumu kurallarını tek yerde toplamak.

using Npgsql;
using RestaurantWeb.Data;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Services
{
    public class RezervasyonService : IRezervasyonService
    {
        private readonly RezervasyonRepository _rezRepo;
        private readonly MasaRepository _masaRepo;
        private readonly IConfiguration _cfg;

        public RezervasyonService(RezervasyonRepository rezRepo, MasaRepository masaRepo, IConfiguration cfg) 
        {
            _rezRepo = rezRepo;
            _masaRepo = masaRepo;
            _cfg = cfg;
        }

        // Rezervasyon oluşturur.
        public OperationResult Create(RezervasyonCreateVm model) 
        {
            if (model.MasaId <= 0) return OperationResult.Fail("Geçersiz masa."); 
            if (string.IsNullOrWhiteSpace(model.MusteriAd)) return OperationResult.Fail("Müşteri adı zorunlu."); 
            if (model.RezervasyonTarihi == default) return OperationResult.Fail("Rezervasyon tarihi zorunlu.");

            model.RezervasyonTarihi = DateTime.SpecifyKind(model.RezervasyonTarihi, DateTimeKind.Unspecified);

            // Rezervasyon blok penceresi (örn. 2 saat): aynı masa için yakın rezervasyonları engeller
            var blockHours = _cfg.GetValue<int?>("Reservation:BlockHoursDefault") ?? 2; 
            if (blockHours < 0) blockHours = 0; 
            if (blockHours > 24) blockHours = 24; 
            var windowMinutes = blockHours * 60;

            var connStr = _cfg.GetConnectionString("PostgreSqlConnection")
                          ?? throw new InvalidOperationException("Connection string not found.");

            using var conn = new NpgsqlConnection(connStr);
            conn.Open();
            using var tx = conn.BeginTransaction();

            // 1) Masa satırını kilitle (FOR UPDATE) + aktiflik kontrolü
            var masaDurum = _masaRepo.GetDurumAndLock(conn, tx, model.MasaId); 
            if (masaDurum == null) { tx.Rollback(); return OperationResult.Fail("Masa bulunamadı."); } 
            if (!masaDurum.AktifMi) { tx.Rollback(); return OperationResult.Fail("Bu masa pasif. Rezervasyon alınamaz."); }

            // 2) İş kuralı: Masa şu an doluysa ve rezervasyon zamanı "çok yakın" ise rezervasyon alma

            if (windowMinutes > 0)
            {
                var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified); 
                var start = model.RezervasyonTarihi.AddMinutes(-windowMinutes);
                var end = model.RezervasyonTarihi.AddMinutes(windowMinutes);

                if (now >= start && now <= end && masaDurum.Durum == MasaDurumu.Dolu) 
                {
                    tx.Rollback();
                    return OperationResult.Fail("Masa şu an dolu ve rezervasyon zaman penceresi içinde. Rezervasyon alınamaz."); 
                }
            }

            // 3) Çakışma kontrolü: aynı masa için +/- window aralığında aktif rezervasyon var mı?
            var conf = _rezRepo.HasWindowConflict(conn, tx, model.MasaId, model.RezervasyonTarihi, windowMinutes); 
            if (!conf.Success) { tx.Rollback(); return OperationResult.Fail(conf.Message); } 
            if (conf.Data) { tx.Rollback(); return OperationResult.Fail("Lütfen başka bir tarih seçin. Bu masa için seçtiğiniz zamana yakın bir rezervasyon bulunuyor."); } 

            // 4) Insert 
            var createRes = _rezRepo.Create(conn, tx, model); 
            if (!createRes.Success) { tx.Rollback(); return OperationResult.Fail(createRes.Message); } 

            tx.Commit();
            return OperationResult.Ok("Rezervasyon oluşturuldu."); 
        }

        public OperationResult Cancel(int rezervasyonId) => _rezRepo.Cancel(rezervasyonId);

        // Rezervasyon listeleme: filtre/doğrulama service'te, sorgu repository'de
        public OperationResult<List<RezervasyonListItemVm>> GetList(DateTime? baslangic, DateTime? bitis, int? masaNo, short? durum, string? q, int limit) 
        {
            try
            {
                // normalize (UI swap controller’da var ama service de güvenli olsun) 
                if (baslangic.HasValue && bitis.HasValue && bitis.Value < baslangic.Value) 
                    (baslangic, bitis) = (bitis, baslangic); 

                // q trim 
                q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(); 

                // limit clamp 
                if (limit <= 0) limit = 200; 
                if (limit > 1000) limit = 1000; 

                // durum doğrulama (0,1,2 dışında reject) 
                if (durum.HasValue && durum.Value != 0 && durum.Value != 1 && durum.Value != 2) 
                    return OperationResult<List<RezervasyonListItemVm>>.Fail("Geçersiz durum filtresi."); 

                var list = _rezRepo.GetList(baslangic, bitis, masaNo, durum, q, limit); 
                return OperationResult<List<RezervasyonListItemVm>>.Ok(list); 
            }
            catch (Exception)
            {
                return OperationResult<List<RezervasyonListItemVm>>.Fail("Teknik bir hata oluştu."); 
            }
        }

        // Rezervasyonu "kullanıldı" yapar (transaction içinde).
        public OperationResult MarkUsed(int rezervasyonId) 
        {
            try
            {
                var connStr = _cfg.GetConnectionString("PostgreSqlConnection")
                              ?? throw new InvalidOperationException("Connection string not found.");

                using var conn = new NpgsqlConnection(connStr);
                conn.Open();
                using var tx = conn.BeginTransaction();

                var res = _rezRepo.MarkUsed(conn, tx, rezervasyonId); 
                if (!res.Success) { tx.Rollback(); return res; }

                tx.Commit();
                return res;
            }
            catch
            {
                return OperationResult.Fail("Teknik bir hata oluştu.");
            }
        }

    }

}
