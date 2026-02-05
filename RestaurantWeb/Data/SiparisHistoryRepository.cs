// Sipariş geçmişi (liste) ve sipariş detayı (header + kalemler + ödeme) sorgularını yapan repository.
// Amaç: UI tarafında “Sipariş Geçmişi” ekranını hızlı listelemek ve Detay ekranında tek siparişin tüm özetini göstermek.
// Not: Bu sınıf sadece okuma yapar; siparişin lifecycle/transaction işlemleri SiparisRepository’dedir.


using Npgsql;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Data
{
    public class SiparisHistoryRepository
    {
        private readonly string _connStr;

        public SiparisHistoryRepository(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("PostgreSqlConnection")
                      ?? throw new InvalidOperationException("Connection string not found.");
        }

        public OperationResult<List<SiparisListItemVm>> GetOrders(DateTime baslangic, DateTime bitis, int? masaNo = null)
        {
            try
            {
                // Tarih filtresi: bitiş günü dahil olsun diye [start, end+1) aralığı kullanıyoruz.
                var start = baslangic.Date;
                var endExclusive = bitis.Date.AddDays(1);

                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                var sql = @"
SELECT
    s.id,
    s.masa_id,
    m.masa_no,
    s.olusturma_tarihi,
    s.kapandi_tarihi,
    s.ara_toplam,
    s.iskonto_tutar,
    s.toplam,
    s.durum,
    COALESCE(o.yontem, NULL) AS yontem,
    COALESCE(o.tutar, 0)     AS odeme_tutar
FROM siparisler s
JOIN masalar m ON m.id = s.masa_id
LEFT JOIN LATERAL (
    SELECT o1.yontem, o1.tutar
    FROM odemeler o1
    WHERE o1.siparis_id = s.id
    ORDER BY o1.alindi_tarihi DESC
    LIMIT 1
) o ON true
WHERE s.olusturma_tarihi >= @start
  AND s.olusturma_tarihi <  @end
";
                // İsteğe bağlı masa filtresi (arama ekranı)
                if (masaNo.HasValue)
                    sql += " AND m.masa_no = @masaNo\n";

                sql += " ORDER BY s.id DESC;";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@end", endExclusive);

                if (masaNo.HasValue)
                    cmd.Parameters.AddWithValue("@masaNo", masaNo.Value);

                var list = new List<SiparisListItemVm>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    // Durum DB’de numeric/short tutuluyor → UI’da okunabilir string’e çeviriyoruz.
                    var durumShort = r.GetInt16(8);
                    string durumText = durumShort == 0 ? "Açık" : "Kapalı";

                    // Ödeme yoksa yontem null olabilir.
                    string yontemText = "";
                    if (!r.IsDBNull(9))
                        yontemText = ((OdemeYontemi)r.GetInt16(9)).ToString();

                    list.Add(new SiparisListItemVm
                    {
                        SiparisId = r.GetInt32(0),
                        MasaId = r.GetInt32(1),
                        MasaNo = r.GetInt32(2),
                        AcildiTarihi = r.GetDateTime(3),
                        KapandiTarihi = r.IsDBNull(4) ? null : r.GetDateTime(4),
                        AraToplam = r.GetDecimal(5),
                        IskontoTutar = r.GetDecimal(6),
                        Toplam = r.GetDecimal(7),
                        Durum = durumText,
                        OdemeYontemi = yontemText,
                        OdemeTutar = r.GetDecimal(10)
                    });
                }

                return OperationResult<List<SiparisListItemVm>>.Ok(list);
            }
            catch (PostgresException ex)
            {
                return OperationResult<List<SiparisListItemVm>>.Fail($"Veritabanı hatası. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<List<SiparisListItemVm>>.Fail("Beklenmeyen bir hata oluştu.");
            }
        }

        // 1) Header: siparişin özet bilgileri (masa + tutarlar + durum)
        public OperationResult<SiparisDetayVm> GetOrderDetail(int siparisId)
        {
            if (siparisId <= 0)
                return OperationResult<SiparisDetayVm>.Fail("Geçersiz sipariş id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                // 1) Header
                const string headerSql = @"
SELECT
    s.id,
    s.masa_id,
    m.masa_no,
    s.olusturma_tarihi,
    s.kapandi_tarihi,
    s.durum,
    s.ara_toplam,
    s.iskonto_oran,
    s.iskonto_tutar,
    s.toplam
FROM siparisler s
JOIN masalar m ON m.id = s.masa_id
WHERE s.id = @id;
";
                SiparisDetayVm? vm = null;

                using (var cmd = new NpgsqlCommand(headerSql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", siparisId);
                    using var r = cmd.ExecuteReader();
                    if (!r.Read())
                        return OperationResult<SiparisDetayVm>.Fail("Sipariş bulunamadı.");

                    vm = new SiparisDetayVm
                    {
                        SiparisId = r.GetInt32(0),
                        MasaId = r.GetInt32(1),
                        MasaNo = r.GetInt32(2),
                        AcildiTarihi = r.GetDateTime(3),
                        KapandiTarihi = r.IsDBNull(4) ? null : r.GetDateTime(4),
                        Durum = r.GetInt16(5),
                        AraToplam = r.GetDecimal(6),
                        IskontoOrani = r.GetDecimal(7),
                        IskontoTutar = r.GetDecimal(8),
                        Toplam = r.GetDecimal(9)
                    };
                }

                // 2) Items: sipariş kalemleri (ürün adı + adet + birim fiyat + satır toplam)
                const string itemsSql = @"
SELECT
    sk.urun_id,
    u.ad,
    sk.adet,
    sk.birim_fiyat,
    sk.satir_toplam
FROM siparis_kalemleri sk
JOIN urunler u ON u.id = sk.urun_id
WHERE sk.siparis_id = @sid
ORDER BY u.ad;
";
                using (var cmd = new NpgsqlCommand(itemsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@sid", siparisId);
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        vm!.Kalemler.Add(new SiparisKalemDetayVm
                        {
                            UrunId = r.GetInt32(0),
                            UrunAd = r.GetString(1),
                            Adet = r.GetInt32(2),
                            BirimFiyat = r.GetDecimal(3),
                            SatirToplam = r.GetDecimal(4)
                        });
                    }
                }

                // 3) Payment: varsa en son alınan ödeme bilgisini göster (detay ekranı için yeterli)
                const string paySql = @"
SELECT tutar, yontem, alindi_tarihi
FROM odemeler
WHERE siparis_id = @sid
ORDER BY alindi_tarihi DESC
LIMIT 1;
";
                using (var cmd = new NpgsqlCommand(paySql, conn))
                {
                    cmd.Parameters.AddWithValue("@sid", siparisId);
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        vm!.OdemeTutar = r.GetDecimal(0);
                        vm!.OdemeYontemi = ((OdemeYontemi)r.GetInt16(1)).ToString();
                        vm!.OdemeTarihi = r.GetDateTime(2);
                    }
                }

                return OperationResult<SiparisDetayVm>.Ok(vm!);
            }
            catch (PostgresException ex)
            {
                return OperationResult<SiparisDetayVm>.Fail($"Veritabanı hatası. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<SiparisDetayVm>.Fail("Beklenmeyen bir hata oluştu.");
            }
        }
    }
}
