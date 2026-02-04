using Npgsql;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Data
{
    public class RaporRepository
    {
        private readonly string _connStr;

        public RaporRepository(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("PostgreSqlConnection")
                      ?? throw new InvalidOperationException("Connection string not found.");
        }

        public OperationResult<DashboardVm> GetDashboard(DateTime baslangic, DateTime bitis, string mode) 
        {
            var start = baslangic.Date;
            var endExclusive = bitis.Date.AddDays(1);

            // sadece whitelist 
            var m = (mode ?? "payment").ToLowerInvariant(); 
            if (m != "payment" && m != "close") m = "payment"; 

            var dateCol = (m == "close") ? "s.kapandi_tarihi" : "o.alindi_tarihi"; 

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                var vm = new DashboardVm
                {
                    Baslangic = start,
                    Bitis = bitis.Date,
                    Mode = m
                };

                // 1) Genel özet
                var summarySql = @$"
SELECT
    COUNT(DISTINCT s.id) AS siparis_sayisi,
    COALESCE(SUM(o.tutar), 0) AS toplam_ciro,
    CASE 
        WHEN COUNT(DISTINCT s.id) = 0 THEN 0
        ELSE COALESCE(SUM(o.tutar), 0) / COUNT(DISTINCT s.id)
    END AS ortalama_sepet
FROM siparisler s
JOIN odemeler o ON o.siparis_id = s.id
WHERE s.durum = 1
  AND {dateCol} >= @start
  AND {dateCol} <  @end;
";

                using (var cmd = new NpgsqlCommand(summarySql, conn))
                {
                    cmd.Parameters.AddWithValue("@start", start);
                    cmd.Parameters.AddWithValue("@end", endExclusive);

                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        vm.SiparisSayisi = r.GetInt32(0);
                        vm.ToplamCiro = r.GetDecimal(1);
                        vm.OrtalamaSepet = r.GetDecimal(2);
                    }
                }

                // 2) Ödeme dağılımı
                var paymentSql = $@"
SELECT o.yontem, SUM(o.tutar)
FROM odemeler o
JOIN siparisler s ON s.id = o.siparis_id
WHERE s.durum = 1
  AND {dateCol} >= @start
  AND {dateCol} <  @end
GROUP BY o.yontem
ORDER BY o.yontem;
";

                using (var p = new NpgsqlCommand(paymentSql, conn))
                {
                    p.Parameters.AddWithValue("@start", start);
                    p.Parameters.AddWithValue("@end", endExclusive);

                    using var r = p.ExecuteReader();
                    while (r.Read())
                    {
                        var yontem = ((OdemeYontemi)r.GetInt16(0)).ToString();
                        var tutar = r.GetDecimal(1);
                        vm.OdemeDagilimi[yontem] = tutar;
                    }
                }

                // 3) Trend
                var trendSql = $@"
SELECT DATE({dateCol}) AS gun, COALESCE(SUM(o.tutar),0) AS ciro
FROM odemeler o
JOIN siparisler s ON s.id = o.siparis_id
WHERE s.durum = 1
  AND {dateCol} >= @start
  AND {dateCol} <  @end
GROUP BY DATE({dateCol})
ORDER BY gun;
";

                using (var tr = new NpgsqlCommand(trendSql, conn))
                {
                    tr.Parameters.AddWithValue("@start", start);
                    tr.Parameters.AddWithValue("@end", endExclusive);

                    using var r = tr.ExecuteReader();
                    while (r.Read())
                    {
                        vm.GunlukCiro.Add((r.GetDateTime(0), r.GetDecimal(1)));
                    }
                }

                // 4) Kategori bazlı ciro
                string kategoriSql;

                if (m == "close")
                {
                    kategoriSql = @"
SELECT
    k.ad AS kategori_ad,
    COALESCE(SUM(sk.adet), 0) AS toplam_adet,
    COALESCE(SUM(sk.satir_toplam), 0) AS brut_ciro,
    COALESCE(
        SUM(
            sk.satir_toplam
            - (
                CASE
                    WHEN COALESCE(s.ara_toplam, 0) = 0 THEN 0
                    ELSE (sk.satir_toplam / s.ara_toplam) * COALESCE(s.iskonto_tutar, 0)
                END
            )
        ),
        0
    ) AS net_ciro
FROM siparisler s
JOIN siparis_kalemleri sk ON sk.siparis_id = s.id
JOIN urunler u ON u.id = sk.urun_id
JOIN kategoriler k ON k.id = u.kategori_id
WHERE s.durum = 1
  AND s.kapandi_tarihi >= @start
  AND s.kapandi_tarihi <  @end
GROUP BY k.ad
ORDER BY net_ciro DESC, brut_ciro DESC, kategori_ad ASC;
"; 
                }
                else
                {
                    kategoriSql = @"
WITH pay AS (
    SELECT siparis_id, MIN(alindi_tarihi) AS pay_time
    FROM odemeler
    GROUP BY siparis_id
)
SELECT
    k.ad AS kategori_ad,
    COALESCE(SUM(sk.adet), 0) AS toplam_adet,
    COALESCE(SUM(sk.satir_toplam), 0) AS brut_ciro,
    COALESCE(
        SUM(
            sk.satir_toplam
            - (
                CASE
                    WHEN COALESCE(s.ara_toplam, 0) = 0 THEN 0
                    ELSE (sk.satir_toplam / s.ara_toplam) * COALESCE(s.iskonto_tutar, 0)
                END
            )
        ),
        0
    ) AS net_ciro
FROM siparisler s
JOIN pay ON pay.siparis_id = s.id
JOIN siparis_kalemleri sk ON sk.siparis_id = s.id
JOIN urunler u ON u.id = sk.urun_id
JOIN kategoriler k ON k.id = u.kategori_id
WHERE s.durum = 1
  AND pay.pay_time >= @start
  AND pay.pay_time <  @end
GROUP BY k.ad
ORDER BY net_ciro DESC, brut_ciro DESC, kategori_ad ASC;
"; 
                }

                using (var kc = new NpgsqlCommand(kategoriSql, conn))
                {
                    kc.Parameters.AddWithValue("@start", start);
                    kc.Parameters.AddWithValue("@end", endExclusive);

                    using var r = kc.ExecuteReader();
                    while (r.Read())
                    {
                        vm.KategoriCiro.Add(new KategoriCiroVm
                        {
                            KategoriAd = r.GetString(0),
                            ToplamAdet = Convert.ToInt32(r.GetInt64(1)), 
                            BrutCiro = r.GetDecimal(2),
                            NetCiro = r.GetDecimal(3)
                        });
                    }
                }

                // 5) Top 10 ürün
                string topSql;

                if (m == "close")
                {
                    topSql = @"
SELECT u.ad AS urun_ad,
       SUM(sk.adet)::int AS adet,
       COALESCE(SUM(sk.satir_toplam),0) AS ciro
FROM siparisler s
JOIN siparis_kalemleri sk ON sk.siparis_id = s.id
JOIN urunler u ON u.id = sk.urun_id
WHERE s.durum = 1
  AND s.kapandi_tarihi >= @start
  AND s.kapandi_tarihi <  @end
GROUP BY u.ad
ORDER BY adet DESC, ciro DESC
LIMIT 10;
"; 
                }
                else
                {
                    topSql = @"
SELECT u.ad AS urun_ad,
       SUM(sk.adet)::int AS adet,
       COALESCE(SUM(sk.satir_toplam),0) AS ciro
FROM odemeler o
JOIN siparisler s ON s.id = o.siparis_id
JOIN siparis_kalemleri sk ON sk.siparis_id = s.id
JOIN urunler u ON u.id = sk.urun_id
WHERE s.durum = 1
  AND o.alindi_tarihi >= @start
  AND o.alindi_tarihi <  @end
GROUP BY u.ad
ORDER BY adet DESC, ciro DESC
LIMIT 10;
"; 
                }

                using (var tcmd = new NpgsqlCommand(topSql, conn))
                {
                    tcmd.Parameters.AddWithValue("@start", start);
                    tcmd.Parameters.AddWithValue("@end", endExclusive);

                    using var r = tcmd.ExecuteReader();
                    while (r.Read())
                    {
                        vm.TopUrunler.Add((r.GetString(0), r.GetInt32(1), r.GetDecimal(2)));
                    }
                }

                // 6) Personel performans
                var perfSql = $@"
SELECT 
    p.id AS personel_id,
    p.ad_soyad,
    COUNT(DISTINCT s.id) AS siparis_sayisi,
    COALESCE(SUM(o.tutar), 0) AS ciro
FROM siparisler s
JOIN personeller p ON p.id = s.kapatan_personel_id
JOIN odemeler o ON o.siparis_id = s.id
WHERE s.durum = 1
  AND {dateCol} >= @start
  AND {dateCol} <  @end
GROUP BY p.id, p.ad_soyad
ORDER BY ciro DESC, siparis_sayisi DESC, p.ad_soyad ASC;
";

                using (var pcmd = new NpgsqlCommand(perfSql, conn))
                {
                    pcmd.Parameters.AddWithValue("@start", start);
                    pcmd.Parameters.AddWithValue("@end", endExclusive);

                    using var r = pcmd.ExecuteReader();
                    while (r.Read())
                    {
                        vm.PersonelPerformans.Add(new PersonelPerfVm
                        {
                            PersonelId = r.GetInt32(0),
                            AdSoyad = r.GetString(1),
                            SiparisSayisi = Convert.ToInt32(r.GetInt64(2)), 
                            Ciro = r.GetDecimal(3)
                        });
                    }
                }

                return OperationResult<DashboardVm>.Ok(vm);
            }
            catch (PostgresException ex)
            {
                return OperationResult<DashboardVm>.Fail($"DB hatası. (Kod: {ex.SqlState})");
            }
            catch
            {
                return OperationResult<DashboardVm>.Fail("Beklenmeyen hata.");
            }
        }
    }
}
