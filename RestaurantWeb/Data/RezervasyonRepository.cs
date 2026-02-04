using Npgsql;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;
using NpgsqlTypes;

namespace RestaurantWeb.Data
{
    public class RezervasyonRepository
    {
        private readonly string _connStr;

        public RezervasyonRepository(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("PostgreSqlConnection")
                      ?? throw new InvalidOperationException("Connection string not found.");
        }

        public record RezInfo(int RezervasyonId, int MasaId, string MusteriAd, DateTime RezTarih);

        public List<RezInfo> GetDueActiveReservations(DateTime now, int graceMinutes) 
        {
            using var conn = new NpgsqlConnection(_connStr);
            conn.Open();

            const string sql = @"
SELECT
    r.id,
    r.masa_id,
    r.musteri_ad,
    r.rezervasyon_tarihi
FROM rezervasyonlar r
WHERE r.durum = 0
  AND r.rezervasyon_tarihi <= @now
  AND r.rezervasyon_tarihi >= (@now - make_interval(mins => @grace))
ORDER BY r.rezervasyon_tarihi ASC;
";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.Parameters.AddWithValue("@grace", graceMinutes);

            var list = new List<RezInfo>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new RezInfo(
                    r.GetInt32(0),
                    r.GetInt32(1),
                    r.GetString(2),
                    r.GetDateTime(3)
                ));
            }

            return list;
        }

        public Dictionary<int, RezInfo> GetActiveReservationsForTablesInWindow(
                    int[] masaIds, DateTime now, int windowMinutes)
        {
            if (masaIds == null || masaIds.Length == 0) return new Dictionary<int, RezInfo>();

            using var conn = new NpgsqlConnection(_connStr);
            conn.Open();

            // make_interval yerine doğrudan parametre ile gelen interval'i kullanıyoruz
            const string sql = @"
SELECT DISTINCT ON (r.masa_id)
    r.id,
    r.masa_id,
    r.musteri_ad,
    r.rezervasyon_tarihi
FROM rezervasyonlar r
WHERE r.durum = 0
  AND r.masa_id = ANY(@ids)
  AND @now BETWEEN (r.rezervasyon_tarihi - @interval)
              AND (r.rezervasyon_tarihi + @interval)
ORDER BY r.masa_id, r.rezervasyon_tarihi ASC;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ids", masaIds);
            cmd.Parameters.AddWithValue("@now", now);

            // TimeSpan olarak gönderiyoruz (En güvenli yöntem)
            cmd.Parameters.Add("@interval", NpgsqlDbType.Interval).Value = TimeSpan.FromMinutes(windowMinutes);

            var map = new Dictionary<int, RezInfo>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var rezId = r.GetInt32(0);
                var masaId = r.GetInt32(1);
                var ad = r.GetString(2);
                var tarih = r.GetDateTime(3);
                map[masaId] = new RezInfo(rezId, masaId, ad, tarih);
            }

            return map;
        }

        public OperationResult MarkUsed(NpgsqlConnection conn, NpgsqlTransaction tx, int rezervasyonId) 
        {
            if (rezervasyonId <= 0) return OperationResult.Fail("Geçersiz rezervasyon."); 

            const string sql = @"
UPDATE rezervasyonlar
SET durum = 2
WHERE id = @id AND durum = 0;"; 

            using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@id", rezervasyonId);

            var affected = cmd.ExecuteNonQuery();
            if (affected == 1) return OperationResult.Ok("Rezervasyon kullanıldı."); 
            return OperationResult.Fail("Rezervasyon bulunamadı / aktif değil."); 
        }

        public OperationResult Create(RezervasyonCreateVm model)
        {
            if (model.MasaId <= 0) return OperationResult.Fail("Geçersiz masa.");
            if (string.IsNullOrWhiteSpace(model.MusteriAd)) return OperationResult.Fail("Müşteri adı zorunlu.");
            if (model.RezervasyonTarihi == default) return OperationResult.Fail("Rezervasyon tarihi zorunlu.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
INSERT INTO rezervasyonlar (masa_id, musteri_ad, telefon, rezervasyon_tarihi, kisi_sayisi, notlar, durum)
VALUES (@masa_id, @ad, @tel, @tarih, @kisi, @not, 0);
";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@masa_id", model.MasaId);
                cmd.Parameters.AddWithValue("@ad", model.MusteriAd.Trim());
                cmd.Parameters.AddWithValue("@tel", model.Telefon.Trim());
                cmd.Parameters.AddWithValue("@tarih", model.RezervasyonTarihi);
                cmd.Parameters.AddWithValue("@kisi", (object?)model.KisiSayisi ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@not", (object?)model.Notlar?.Trim() ?? DBNull.Value);

                var affected = cmd.ExecuteNonQuery();
                if (affected == 1)
                    return OperationResult.Ok("Rezervasyon oluşturuldu.");

                return OperationResult.Fail("Rezervasyon oluşturulamadı.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return OperationResult.Fail("Bu masa için aynı tarih/saatte zaten rezervasyon var.");
            }
            catch (PostgresException ex)
            {
                return OperationResult.Fail($"Veritabanı hatası. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult.Fail("Beklenmeyen bir hata oluştu.");
            }
        }

        public OperationResult<int> Create(NpgsqlConnection conn, NpgsqlTransaction tx, RezervasyonCreateVm model) 
        {
            if (model.MasaId <= 0) return OperationResult<int>.Fail("Geçersiz masa.");
            if (string.IsNullOrWhiteSpace(model.MusteriAd)) return OperationResult<int>.Fail("Müşteri adı zorunlu.");
            if (model.RezervasyonTarihi == default) return OperationResult<int>.Fail("Rezervasyon tarihi zorunlu.");

            const string sql = @"
INSERT INTO rezervasyonlar (masa_id, musteri_ad, telefon, rezervasyon_tarihi, kisi_sayisi, notlar, durum)
VALUES (@masa_id, @ad, @tel, @tarih, @kisi, @not, 0)
RETURNING id;
";

            using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@masa_id", model.MasaId);
            cmd.Parameters.AddWithValue("@ad", model.MusteriAd.Trim());
            cmd.Parameters.AddWithValue("@tel", (object?)model.Telefon?.Trim() ?? DBNull.Value);
            var rezTarih = DateTime.SpecifyKind(model.RezervasyonTarihi, DateTimeKind.Unspecified); 
            cmd.Parameters.Add("@tarih", NpgsqlDbType.Timestamp).Value = rezTarih; 
            cmd.Parameters.AddWithValue("@kisi", (object?)model.KisiSayisi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@not", (object?)model.Notlar?.Trim() ?? DBNull.Value);

            try
            {
                var idObj = cmd.ExecuteScalar();
                // geçmişe rezervasyon olmasın 
                var now = DateTime.Now;
                if (model.RezervasyonTarihi <= now)
                    return OperationResult<int>.Fail("Rezervasyon zamanı geçmiş olamaz.");

                if (idObj == null) return OperationResult<int>.Fail("Rezervasyon oluşturulamadı.");
                return OperationResult<int>.Ok(Convert.ToInt32(idObj), "Rezervasyon oluşturuldu.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return OperationResult<int>.Fail("Bu masa için aynı tarih/saatte zaten rezervasyon var.");
            }
        }

        public OperationResult<bool> HasWindowConflict(NpgsqlConnection conn, NpgsqlTransaction tx,
                    int masaId, DateTime tarih, int windowMinutes)
        {
            if (masaId <= 0) return OperationResult<bool>.Fail("Geçersiz masa.");

            // Tarih kontrolü
            if (tarih == default) return OperationResult<bool>.Fail("Rezervasyon tarihi zorunlu.");

            // Güvenlik: windowMinutes eksi olamaz
            if (windowMinutes < 0) windowMinutes = 0;

            // Timestamp (no tz) standardize
            tarih = DateTime.SpecifyKind(tarih, DateTimeKind.Unspecified);

            // C# tarafında Interval oluşturuyoruz. 
            // Bu sayede SQL içinde 'make_interval' fonksiyonunu parametreyle çağırma riskine girmiyoruz.
            var interval = TimeSpan.FromMinutes(windowMinutes);

            const string sql = @"
SELECT EXISTS (
  SELECT 1
  FROM rezervasyonlar
  WHERE masa_id = @masa_id
    AND durum = 0
    AND rezervasyon_tarihi >= (@tarih - @interval)
    AND rezervasyon_tarihi <= (@tarih + @interval)
);";
            /*
               Mantık Özeti:
               Mevcut Rezervasyon (R), Yeni Talep (T), Süre (W)
               Çakışma Şartı: R, [T-W, T+W] aralığında mı?
               Örnek: Ortalama Süre: 2 Saat (120 dk)
               Mevcut: 20:00 (R)

               Senaryo 1: Yeni Talep 19:00 (T)
               Aralık: 19:00 - 2h = 17:00, 19:00 + 2h = 21:00
               20:00 bu aralıkta mı? EVET. (Çakışma Var, Bloklanır) -> Doğru.

               Senaryo 2: Yeni Talep 21:00 (T)
               Aralık: 21:00 - 2h = 19:00, 21:00 + 2h = 23:00
               20:00 bu aralıkta mı? EVET. (Çakışma Var, Bloklanır) -> Doğru.

               Senaryo 3: Yeni Talep 17:59 (T)
               Aralık: 15:59 - 19:59.
               20:00 bu aralıkta mı? HAYIR. (İzin verilir) -> Doğru.
            */

            using var cmd = new NpgsqlCommand(sql, conn, tx);

            cmd.Parameters.Add("@masa_id", NpgsqlDbType.Integer).Value = masaId;
            cmd.Parameters.Add("@tarih", NpgsqlDbType.Timestamp).Value = tarih;

            // PostgreSQL 'interval' tipi olarak gönderiyoruz
            cmd.Parameters.Add("@interval", NpgsqlDbType.Interval).Value = interval;

            var exists = (bool)cmd.ExecuteScalar()!;
            return OperationResult<bool>.Ok(exists);
        }

        public OperationResult Cancel(int rezervasyonId) 
        {
            if (rezervasyonId <= 0) return OperationResult.Fail("Geçersiz rezervasyon.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
UPDATE rezervasyonlar
SET durum = 1
WHERE id = @id AND durum = 0
RETURNING id;
"; 

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", rezervasyonId);

                var idObj = cmd.ExecuteScalar();
                if (idObj != null && idObj != DBNull.Value)
                    return OperationResult.Ok("Rezervasyon iptal edildi.");

                // aktif değilse: ya yok ya zaten iptal
                const string checkSql = "SELECT 1 FROM rezervasyonlar WHERE id = @id LIMIT 1;"; 
                using var c = new NpgsqlCommand(checkSql, conn);
                c.Parameters.AddWithValue("@id", rezervasyonId);
                var exists = c.ExecuteScalar() != null;

                return exists
                    ? OperationResult.Fail("Rezervasyon zaten iptal.")
                    : OperationResult.Fail("Rezervasyon bulunamadı.");
            }
            catch (PostgresException ex)
            {
                return OperationResult.Fail($"Veritabanı hatası. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult.Fail("Beklenmeyen bir hata oluştu.");
            }
        }

        public OperationResult<bool> HasActiveConflict(NpgsqlConnection conn, NpgsqlTransaction tx, int masaId, DateTime rezervasyonTarihi) 
        {
            if (masaId <= 0) return OperationResult<bool>.Fail("Geçersiz masa.");
            if (rezervasyonTarihi == default) return OperationResult<bool>.Fail("Rezervasyon tarihi zorunlu.");

            const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM rezervasyonlar
    WHERE masa_id = @masa_id
      AND rezervasyon_tarihi = @tarih
      AND durum = 0
);
";
            using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@masa_id", masaId);
            cmd.Parameters.AddWithValue("@tarih", rezervasyonTarihi);

            var exists = (bool)cmd.ExecuteScalar()!;
            return OperationResult<bool>.Ok(exists);
        }

        public List<RezervasyonListItemVm> GetList(DateTime? start, DateTime? end, int? masaNo, short? durum, string? q, int limit = 200)
        {
            using var conn = new NpgsqlConnection(_connStr);
            conn.Open();

            // limit clamp 
            if (limit <= 0) limit = 200; 
            if (limit > 1000) limit = 1000; 

            var sql = @"
SELECT
    r.id,
    r.masa_id,
    m.masa_no,
    r.musteri_ad,
    r.telefon,
    r.rezervasyon_tarihi,
    r.kisi_sayisi,
    r.notlar,
    r.durum,
    r.olusturma_tarihi
FROM rezervasyonlar r
JOIN masalar m ON m.id = r.masa_id
WHERE 1=1
";

            if (start.HasValue)
                sql += " AND r.rezervasyon_tarihi >= @start"; 

            if (end.HasValue)
                sql += " AND r.rezervasyon_tarihi < @end"; 

            if (masaNo.HasValue)
                sql += " AND m.masa_no = @masaNo"; 

            if (durum.HasValue)
                sql += " AND r.durum = @durum"; 

            if (!string.IsNullOrWhiteSpace(q))
                sql += " AND (r.musteri_ad ILIKE @q OR r.telefon ILIKE @q)"; 

            sql += " ORDER BY r.rezervasyon_tarihi DESC LIMIT @limit;"; 

            using var cmd = new NpgsqlCommand(sql, conn);

            if (start.HasValue)
                cmd.Parameters.AddWithValue("@start", start.Value); 

            if (end.HasValue)
                cmd.Parameters.AddWithValue("@end", end.Value); 

            if (masaNo.HasValue)
                cmd.Parameters.AddWithValue("@masaNo", masaNo.Value); 

            if (durum.HasValue)
                cmd.Parameters.AddWithValue("@durum", durum.Value); 

            if (!string.IsNullOrWhiteSpace(q))
                cmd.Parameters.AddWithValue("@q", $"%{q.Trim()}%"); 

            cmd.Parameters.AddWithValue("@limit", limit); 

            var list = new List<RezervasyonListItemVm>();

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new RezervasyonListItemVm
                {
                    Id = r.GetInt32(0),
                    MasaId = r.GetInt32(1),
                    MasaNo = r.GetInt32(2),
                    MusteriAd = r.GetString(3),
                    Telefon = r.IsDBNull(4) ? null : r.GetString(4),
                    RezervasyonTarihi = r.GetDateTime(5),
                    KisiSayisi = r.IsDBNull(6) ? null : r.GetInt32(6),
                    Notlar = r.IsDBNull(7) ? null : r.GetString(7),
                    Durum = r.GetInt16(8),
                    OlusturmaTarihi = r.GetDateTime(9)
                });
            }

            return list;
        }
    }

}
