// Sipariş domain’inin “çekirdek” repository’si.
// - Sipariş oluşturma / açık sipariş bulma (EnsureOpenOrderForTable)
// - Sepeti siparişe yazma: kalem upsert + stok düşme + toplamları yeniden hesaplama (SubmitOrder)
// - İskonto güncelleme + log (UpdateDiscountRate)
// - Ödeme ile kapatma: ödeme kaydı + sipariş kapatma + masa boşaltma + log (CloseOrderWithPayment)
// Not: Bu dosyada bazı metotlar dışarıdan conn/tx alır (atomic işlem için), bazıları kendi conn açar (read-only).


using Npgsql;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;
using RestaurantWeb.Models.Dtos;

namespace RestaurantWeb.Data
{
    public class SiparisRepository
    {
        private readonly string _connStr;

        public SiparisRepository(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("PostgreSqlConnection")
                      ?? throw new InvalidOperationException("Connection string not found.");
        }

        // Sipariş kalemlerinden ara toplamı toplar, mevcut iskonto oranını okur ve
        // ara_toplam/iskonto_tutar/toplam alanlarını tek noktadan günceller.
        // Bu metot transaction içinde çağrılmalı (tutarlılık için).
        private void RecalculateTotals(NpgsqlConnection conn, NpgsqlTransaction tx, int siparisId) 
        {
            // 1) Kalemlerden ara toplam (satir_toplam) hesapla
            const string sumSql = @"SELECT COALESCE(SUM(satir_toplam),0) FROM siparis_kalemleri WHERE siparis_id=@id;"; 
            decimal araToplam; 
            using (var s = new NpgsqlCommand(sumSql, conn, tx)) 
            {
                s.Parameters.AddWithValue("@id", siparisId); 
                araToplam = Convert.ToDecimal(s.ExecuteScalar()); 
            }

            // 2) İskonto oranını siparişten oku
            const string rateSql = @"SELECT iskonto_oran FROM siparisler WHERE id=@id;";
            decimal iskontoOran; 
            using (var r = new NpgsqlCommand(rateSql, conn, tx)) 
            {
                r.Parameters.AddWithValue("@id", siparisId); 
                iskontoOran = Convert.ToDecimal(r.ExecuteScalar() ?? 0m); 
            }

            // 3) Finansal hesap: 2 ondalık, away-from-zero (kasa davranışı
            var iskontoTutar = Math.Round(araToplam * (iskontoOran / 100m), 2, MidpointRounding.AwayFromZero); 
            var toplam = araToplam - iskontoTutar; 
            if (toplam < 0) toplam = 0;

            // 4) Sipariş özet alanlarını güncelle
            const string totalsSql = @"
        UPDATE siparisler
        SET ara_toplam=@a, iskonto_tutar=@it, toplam=@t
        WHERE id=@id;
    "; 

            using (var t = new NpgsqlCommand(totalsSql, conn, tx)) 
            {
                t.Parameters.AddWithValue("@a", araToplam); 
                t.Parameters.AddWithValue("@it", iskontoTutar); 
                t.Parameters.AddWithValue("@t", toplam); 
                t.Parameters.AddWithValue("@id", siparisId); 
                t.ExecuteNonQuery(); 
            }
        }

        // Masaya “açık sipariş” garanti eder:
        // - Masayı FOR UPDATE ile kilitler (concurrency)
        // - Açık sipariş varsa id’sini döner, yoksa oluşturur
        // - Masayı Dolu durumuna çeker
        public OperationResult<int> EnsureOpenOrderForTable(NpgsqlConnection conn, NpgsqlTransaction tx, int masaId) 
        {
            if (masaId <= 0)
                return OperationResult<int>.Fail("Geçersiz masa id.");

            if (conn == null)
                return OperationResult<int>.Fail("DB bağlantısı yok.");

            if (tx == null)
                return OperationResult<int>.Fail("DB transaction yok.");

            try
            {
                // 1) Masa var mı + aktif mi? (satır kilitlenir)
                const string tableSql = @"
            SELECT id, aktif_mi, durum
            FROM masalar
            WHERE id = @masa_id
            FOR UPDATE;
        ";

                bool masaVar = false;
                bool aktifMi = false;

                using (var tableCmd = new NpgsqlCommand(tableSql, conn, tx))
                {
                    tableCmd.Parameters.AddWithValue("@masa_id", masaId);
                    using var r = tableCmd.ExecuteReader();
                    if (r.Read())
                    {
                        masaVar = true;
                        aktifMi = r.GetBoolean(1);
                    }
                }

                if (!masaVar) return OperationResult<int>.Fail("Masa bulunamadı.");
                if (!aktifMi) return OperationResult<int>.Fail("Bu masa pasif. Sipariş açılamaz.");


                // 2) Açık sipariş var mı? (durum=0 -> açık)
                const string existingOrderSql = @"
            SELECT id
            FROM siparisler
            WHERE masa_id = @masa_id AND durum = 0
            LIMIT 1;
        ";

                int? openOrderId = null;
                using (var existingCmd = new NpgsqlCommand(existingOrderSql, conn, tx))
                {
                    existingCmd.Parameters.AddWithValue("@masa_id", masaId);
                    var scalar = existingCmd.ExecuteScalar();
                    if (scalar != null)
                        openOrderId = Convert.ToInt32(scalar);
                }

                // 3) Yoksa yeni sipariş oluştur (default finansal alanlar 0)
                if (openOrderId == null)
                {
                    const string insertOrderSql = @"
                INSERT INTO siparisler (masa_id, durum, ara_toplam, iskonto_oran, iskonto_tutar, toplam)  
                VALUES (@masa_id, 0, 0, 0, 0, 0)  
                RETURNING id;
            ";

                    try
                    {
                        using var insertCmd = new NpgsqlCommand(insertOrderSql, conn, tx);
                        insertCmd.Parameters.AddWithValue("@masa_id", masaId);

                        var newIdObj = insertCmd.ExecuteScalar();
                        if (newIdObj == null)
                            return OperationResult<int>.Fail("Sipariş oluşturulamadı.");

                        openOrderId = Convert.ToInt32(newIdObj);
                    }
                    catch (PostgresException ex) when (ex.SqlState == "23505")
                    {
                        // Concurrency: başka istek aynı anda açtı → mevcut siparişi tekrar oku
                        using var retryCmd = new NpgsqlCommand(existingOrderSql, conn, tx);
                        retryCmd.Parameters.AddWithValue("@masa_id", masaId);
                        var scalar2 = retryCmd.ExecuteScalar();
                        if (scalar2 == null)
                            return OperationResult<int>.Fail("Bu masa için zaten açık bir sipariş var. Lütfen tekrar deneyin.");

                        openOrderId = Convert.ToInt32(scalar2);
                    }
                }

                // 4) Masayı “Dolu” yap (idempotent: zaten doluysa dokunmaz)
                const string updateTableSql = @"
    UPDATE masalar
    SET durum = 1
    WHERE id = @masa_id AND durum <> 1;
";

                using (var updCmd = new NpgsqlCommand(updateTableSql, conn, tx))
                {
                    updCmd.Parameters.AddWithValue("@masa_id", masaId);
                    updCmd.ExecuteNonQuery();
                }

                return OperationResult<int>.Ok(openOrderId!.Value, "Sipariş hazır.");
            }
            catch (PostgresException ex)
            {
                return OperationResult<int>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<int>.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
            }
        }

        // Controller tarafında “açık sipariş var mı?” kontrolü için (kendi connection’ını açar)
        public OperationResult<int?> GetOpenOrderId(int masaId) 
        {
            if (masaId <= 0)
                return OperationResult<int?>.Fail("Geçersiz masa id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
                    SELECT id
                    FROM siparisler
                    WHERE masa_id = @masa_id AND durum = 0
                    LIMIT 1;
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@masa_id", masaId);

                var scalar = cmd.ExecuteScalar();
                if (scalar == null)
                    return OperationResult<int?>.Ok(null, "Bu masada açık sipariş yok.");

                return OperationResult<int?>.Ok(Convert.ToInt32(scalar));
            }
            catch (PostgresException ex)
            {
                return OperationResult<int?>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<int?>.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
            }
        }

        // UI’da “Masa No” göstermek için (id -> masa_no)
        public OperationResult<int> GetMasaNoById(int masaId) 
        {
            if (masaId <= 0)
                return OperationResult<int>.Fail("Geçersiz masa id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            SELECT masa_no
            FROM masalar
            WHERE id = @id;
        ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", masaId);

                var scalar = cmd.ExecuteScalar();
                if (scalar == null)
                    return OperationResult<int>.Fail("Masa bulunamadı.");

                return OperationResult<int>.Ok(Convert.ToInt32(scalar));
            }
            catch (PostgresException ex)
            {
                return OperationResult<int>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<int>.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
            }
        }

        // Sepeti siparişe yazar:
        // - Siparişi ve ürünleri kilitler (FOR UPDATE)
        // - Kalemleri UPSERT eder (adet birikir)
        // - Stok düşer (stok>=adet guard ile)
        // - Totals (ara_toplam/iskonto/toplam) tek noktadan hesaplanır
        public OperationResult SubmitOrder(
            NpgsqlConnection conn, NpgsqlTransaction tx,
            int siparisId, List<(int UrunId, int Adet)> items)
        {
            if (siparisId <= 0)
                return OperationResult.Fail("Geçersiz sipariş id.");

            if (items == null || items.Count == 0)
                return OperationResult.Fail("Sepet boş.");

            try
            {
                // 1) Sipariş açık mı? (satır kilitleyerek)
                const string orderCheckSql = @"
            SELECT id
            FROM siparisler
            WHERE id = @id AND durum = 0
            FOR UPDATE;
        ";
                using (var checkCmd = new NpgsqlCommand(orderCheckSql, conn, tx))
                {
                    checkCmd.Parameters.AddWithValue("@id", siparisId);
                    var exists = checkCmd.ExecuteScalar();
                    if (exists == null)
                        return OperationResult.Fail("Sipariş bulunamadı veya açık değil.");
                }

                // 2) Ürünleri lockla (stok aynı anda düşmesin diye)
                const string productSql = @"
            SELECT id, fiyat, stok
            FROM urunler
            WHERE id = ANY(@ids)
            FOR UPDATE;
        ";

                var urunIds = items.Select(x => x.UrunId).Distinct().ToArray();
                var productMap = new Dictionary<int, (decimal Fiyat, int Stok)>();

                using (var prodCmd = new NpgsqlCommand(productSql, conn, tx))
                {
                    prodCmd.Parameters.AddWithValue("@ids", urunIds);

                    using var r = prodCmd.ExecuteReader();
                    while (r.Read())
                    {
                        var id = r.GetInt32(0);
                        var fiyat = r.GetDecimal(1);
                        var stok = r.GetInt32(2);
                        productMap[id] = (fiyat, stok);
                    }
                }

                // 3) Input validasyon
                foreach (var it in items)
                {
                    if (it.Adet <= 0)
                        return OperationResult.Fail("Adet 0'dan büyük olmalıdır.");

                    if (!productMap.ContainsKey(it.UrunId))
                        return OperationResult.Fail($"Ürün bulunamadı. (UrunId: {it.UrunId})");
                }

                // 4) Aynı ürünler tek kaleme toplanır (stok/kalem işlemleri sadeleşir)
                var grouped = items
                    .GroupBy(x => x.UrunId)
                    .Select(g => new { UrunId = g.Key, Adet = g.Sum(x => x.Adet) })
                    .ToList();

                // 5) Stok ön kontrol
                foreach (var g in grouped)
                {
                    var stok = productMap[g.UrunId].Stok;
                    if (stok < g.Adet)
                        return OperationResult.Fail($"Stok yetersiz. (UrunId: {g.UrunId}, Stok: {stok}, İstenen: {g.Adet})");
                }

                // 6) Kalem UPSERT: adet birikir, birim_fiyat son fiyatla güncellenir
                const string upsertItemSql = @"
            INSERT INTO siparis_kalemleri (siparis_id, urun_id, adet, birim_fiyat, satir_toplam)
            VALUES (@siparis_id, @urun_id, @adet, @birim_fiyat, @adet * @birim_fiyat)  
            ON CONFLICT (siparis_id, urun_id)
            DO UPDATE SET
                adet = siparis_kalemleri.adet + EXCLUDED.adet,
                birim_fiyat = EXCLUDED.birim_fiyat,
                satir_toplam = (siparis_kalemleri.adet + EXCLUDED.adet) * EXCLUDED.birim_fiyat;
        ";

                foreach (var g in grouped)
                {
                    var fiyat = productMap[g.UrunId].Fiyat;

                    using var itemCmd = new NpgsqlCommand(upsertItemSql, conn, tx);
                    itemCmd.Parameters.AddWithValue("@siparis_id", siparisId);
                    itemCmd.Parameters.AddWithValue("@urun_id", g.UrunId);
                    itemCmd.Parameters.AddWithValue("@adet", g.Adet);
                    itemCmd.Parameters.AddWithValue("@birim_fiyat", fiyat);
                    itemCmd.ExecuteNonQuery();
                }

                // 7) Stok düş: aynı anda azalma olursa “stok >= adet” guard yakalar
                const string stockUpdateSql = @"
            UPDATE urunler
            SET stok = stok - @adet
            WHERE id = @urun_id AND stok >= @adet;  
        ";

                foreach (var g in grouped)
                {
                    using var stokCmd = new NpgsqlCommand(stockUpdateSql, conn, tx);
                    stokCmd.Parameters.AddWithValue("@urun_id", g.UrunId);
                    stokCmd.Parameters.AddWithValue("@adet", g.Adet);

                    var affected = stokCmd.ExecuteNonQuery(); // *
                    if (affected == 0) // *
                        return OperationResult.Fail($"Stok yetersiz. (UrunId: {g.UrunId})"); // *
                }

                // 8) Finansal alanlar tek noktadan güncellensin
                RecalculateTotals(conn, tx, siparisId);

                return OperationResult.Ok("Sipariş kaydedildi.");
            }
            catch (PostgresException ex)
            {
                return OperationResult.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
            }
        }

        // Katalog: aktif ürünler (Siparis ekranı JS için)
        public OperationResult<List<ProductListItemDto>> GetActiveProducts(int? kategoriId = null) 
        {
            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                var sql = @"
            SELECT u.id, u.ad, u.fiyat, u.stok, k.ad as kategori_ad
            FROM urunler u
            JOIN kategoriler k ON k.id = u.kategori_id
            WHERE u.aktif_mi = TRUE
        ";

                if (kategoriId.HasValue)
                    sql += " AND u.kategori_id = @kid ";

                sql += " ORDER BY k.ad, u.ad;";

                using var cmd = new NpgsqlCommand(sql, conn);
                if (kategoriId.HasValue)
                    cmd.Parameters.AddWithValue("@kid", kategoriId.Value);

                var list = new List<ProductListItemDto>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new ProductListItemDto
                    {
                        Id = r.GetInt32(0),
                        Ad = r.GetString(1),
                        Fiyat = r.GetDecimal(2),
                        Stok = r.GetInt32(3),
                        Kategori = r.GetString(4)
                    });
                }

                return OperationResult<List<ProductListItemDto>>.Ok(list);
            }
            catch (PostgresException ex)
            {
                return OperationResult<List<ProductListItemDto>>.Fail($"Veritabanı hatası. (Kod: {ex.SqlState})");
            }
            catch
            {
                return OperationResult<List<ProductListItemDto>>.Fail("Ürünler yüklenemedi.");
            }
        }

        // Sipariş kalemlerini DTO olarak döner (adisyon/detay ekranları için)
        public OperationResult<List<SiparisDetayDto>> GetSiparisDetaylari(int siparisId) 
        {
            if (siparisId <= 0)
                return OperationResult<List<SiparisDetayDto>>.Fail("Geçersiz sipariş id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            SELECT sk.urun_id, u.ad, sk.adet, sk.birim_fiyat, sk.satir_toplam
            FROM siparis_kalemleri sk
            JOIN urunler u ON u.id = sk.urun_id
            WHERE sk.siparis_id = @sid
            ORDER BY u.ad;
        ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@sid", siparisId);

                var list = new List<SiparisDetayDto>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new SiparisDetayDto
                    {
                        UrunId = r.GetInt32(0),
                        UrunAd = r.GetString(1),
                        Adet = r.GetInt32(2),
                        BirimFiyat = r.GetDecimal(3),
                        SatirToplam = r.GetDecimal(4)
                    });
                }

                return OperationResult<List<SiparisDetayDto>>.Ok(list);
            }
            catch (PostgresException ex)
            {
                return OperationResult<List<SiparisDetayDto>>.Fail($"Veritabanı hatası. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<List<SiparisDetayDto>>.Fail("Beklenmeyen bir hata oluştu.");
            }
        }

        // Katalog: aktif kategoriler (Siparis ekranı JS için)
        public OperationResult<List<CategoryItemDto>> GetActiveCategories() 
        {
            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            SELECT id, ad
            FROM kategoriler
            WHERE aktif_mi = TRUE
            ORDER BY ad;
        ";

                using var cmd = new NpgsqlCommand(sql, conn);

                var list = new List<CategoryItemDto>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new CategoryItemDto
                    {
                        Id = r.GetInt32(0),
                        Ad = r.GetString(1)
                    });
                }

                return OperationResult<List<CategoryItemDto>>.Ok(list);
            }
            catch (PostgresException ex)
            {
                return OperationResult<List<CategoryItemDto>>.Fail($"Veritabanı hatası. (Kod: {ex.SqlState})");
            }
            catch
            {
                return OperationResult<List<CategoryItemDto>>.Fail("Kategoriler yüklenemedi.");
            }
        }

        // Açık sipariş için iskonto oranını değiştirir, totals’ı yeniler ve değişikliği loglar.
        public OperationResult UpdateDiscountRate(NpgsqlConnection conn, NpgsqlTransaction tx,
    int siparisId, decimal iskontoOran, string? actorUsername) 
        {
            if (siparisId <= 0)
                return OperationResult.Fail("Geçersiz sipariş id.");

            if (iskontoOran < 0 || iskontoOran > 100)
                return OperationResult.Fail("İskonto oranı 0 ile 100 arasında olmalıdır.");

            try
            {
                // Siparişi kilitle (açık değilse güncellenmez)
                const string checkSql = @"SELECT id FROM siparisler WHERE id=@id AND durum=0 FOR UPDATE;";
                using (var c = new NpgsqlCommand(checkSql, conn, tx))
                {
                    c.Parameters.AddWithValue("@id", siparisId);
                    if (c.ExecuteScalar() == null)
                        return OperationResult.Fail("Sipariş bulunamadı veya açık değil.");
                }
                // Eski oranı loglamak için oku
                const string oldSql = @"SELECT iskonto_oran FROM siparisler WHERE id=@id;";
                decimal oldOran;
                using (var o = new NpgsqlCommand(oldSql, conn, tx))
                {
                    o.Parameters.AddWithValue("@id", siparisId);
                    oldOran = Convert.ToDecimal(o.ExecuteScalar());
                }
                // Yeni oranı set et
                const string setSql = @"UPDATE siparisler SET iskonto_oran=@oran WHERE id=@id;";
                using (var u = new NpgsqlCommand(setSql, conn, tx))
                {
                    u.Parameters.AddWithValue("@oran", iskontoOran);
                    u.Parameters.AddWithValue("@id", siparisId);
                    u.ExecuteNonQuery();
                }

                // Finansal alanları güncelle
                RecalculateTotals(conn, tx, siparisId);

                // İzlenebilirlik: değişiklik logu
                SiparisLogRepository.AddLog(
                    conn, tx,
                    siparisId,
                    "DISCOUNT",
                    oldOran.ToString("0.##"),
                    iskontoOran.ToString("0.##"),
                    actorUsername
                );

                return OperationResult.Ok("İskonto güncellendi."); 
            }
            catch (PostgresException ex)
            {
                return OperationResult.Fail($"Veritabanı hatası. (Kod: {ex.SqlState})");
            }
            catch
            {
                return OperationResult.Fail("Beklenmeyen hata.");
            }
        }

        // Sipariş adisyonunu (özet + kalemler) tek VM olarak döner
        public OperationResult<SiparisAdisyonVm> GetSiparisAdisyon(int siparisId) 
        {
            if (siparisId <= 0)
                return OperationResult<SiparisAdisyonVm>.Fail("Geçersiz sipariş id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                // 1) Sipariş özetini çek
                const string headerSql = @"
            SELECT ara_toplam, iskonto_oran, iskonto_tutar, toplam
            FROM siparisler
            WHERE id = @sid;
        ";

                decimal araToplam = 0m, iskontoOran = 0m, iskontoTutar = 0m, toplam = 0m;

                using (var h = new NpgsqlCommand(headerSql, conn))
                {
                    h.Parameters.AddWithValue("@sid", siparisId);
                    using var r = h.ExecuteReader();
                    if (!r.Read())
                        return OperationResult<SiparisAdisyonVm>.Fail("Sipariş bulunamadı.");

                    araToplam = r.GetDecimal(0);
                    iskontoOran = r.GetDecimal(1);
                    iskontoTutar = r.GetDecimal(2);
                    toplam = r.GetDecimal(3);
                }

                // 2) Kalemleri çek
                const string itemsSql = @"
            SELECT sk.urun_id, u.ad, sk.adet, sk.birim_fiyat, sk.satir_toplam
            FROM siparis_kalemleri sk
            JOIN urunler u ON u.id = sk.urun_id
            WHERE sk.siparis_id = @sid
            ORDER BY u.ad;
        ";

                var items = new List<SiparisDetayDto>();
                using (var cmd = new NpgsqlCommand(itemsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@sid", siparisId);
                    using var rr = cmd.ExecuteReader();
                    while (rr.Read())
                    {
                        items.Add(new SiparisDetayDto
                        {
                            UrunId = rr.GetInt32(0),
                            UrunAd = rr.GetString(1),
                            Adet = rr.GetInt32(2),
                            BirimFiyat = rr.GetDecimal(3),
                            SatirToplam = rr.GetDecimal(4)
                        });
                    }
                }

                var vm = new SiparisAdisyonVm
                {
                    Items = items,
                    AraToplam = araToplam,
                    IskontoOran = iskontoOran,
                    IskontoTutar = iskontoTutar,
                    Toplam = toplam
                };

                return OperationResult<SiparisAdisyonVm>.Ok(vm);
            }
            catch (PostgresException ex)
            {
                return OperationResult<SiparisAdisyonVm>.Fail($"Veritabanı hatası. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<SiparisAdisyonVm>.Fail("Beklenmeyen bir hata oluştu.");
            }
        }

        // Ödeme ile siparişi kapatır:
        // - sipariş satırını kilitler
        // - ödeme insert eder (idempotent: zaten varsa OK)
        // - siparişi kapatır (idempotent: sadece açıksa)
        // - masayı boşaltır
        // - PAYMENT ve CLOSE loglarını yazar
        public OperationResult CloseOrderWithPayment(NpgsqlConnection conn, NpgsqlTransaction tx,
            int siparisId, OdemeYontemi yontem, string? actorUsername, int kapatanPersonelId)
        {
            if (siparisId <= 0)
                return OperationResult.Fail("Geçersiz sipariş id.");

            // Controller atlanırsa diye ekstra güvenlik
            if (!Enum.IsDefined(typeof(OdemeYontemi), yontem)) 
                return OperationResult.Fail("Geçersiz ödeme yöntemi."); 

            try
            {
                // Siparişi kilitle + mevcut durumunu oku (idempotency için)
                const string getSql = @"
            SELECT masa_id, toplam, durum
            FROM siparisler
            WHERE id = @id
            FOR UPDATE;
        "; 

                int masaId;
                decimal toplam;
                int durum; // 0=open, 1=closed varsayımı  

                using (var cmd = new NpgsqlCommand(getSql, conn, tx)) 
                {
                    cmd.Parameters.AddWithValue("@id", siparisId);
                    using var r = cmd.ExecuteReader();
                    if (!r.Read())
                        return OperationResult.Fail("Sipariş bulunamadı.");

                    masaId = r.GetInt32(0);
                    toplam = r.GetDecimal(1);
                    durum = r.GetInt32(2); 
                }

                // Zaten kapalıysa: tekrar ödeme/masa/log yapmadan OK dön
                if (durum != 0) 
                    return OperationResult.Ok("Sipariş zaten kapalı/ödenmiş.");

                // Ödeme kaydı: uniq constraint varsa ikinci denemede 23505 (idempotent OK) 
                const string paySql = @"
            INSERT INTO odemeler (siparis_id, tutar, yontem)
            VALUES (@sid, @tutar, @yontem);
        ";

                try
                {
                    using (var p = new NpgsqlCommand(paySql, conn, tx))
                    {
                        p.Parameters.AddWithValue("@sid", siparisId);
                        p.Parameters.AddWithValue("@tutar", toplam);
                        p.Parameters.AddWithValue("@yontem", (short)yontem);
                        p.ExecuteNonQuery();
                    }
                }
                catch (PostgresException ex) when (ex.SqlState == "23505") 
                {
                    // Bu sipariş için zaten ödeme var → idempotent close  
                    return OperationResult.Ok("Sipariş zaten kapalı/ödenmiş."); 
                }

                // Siparişi kapat (yalnızca açıksa)
                const string closeSql = @"
            UPDATE siparisler
            SET durum = 1,
                kapandi_tarihi = NOW(),
                kapatan_personel_id = @pid
            WHERE id = @id AND durum = 0;
        ";

                int closeAffected; 

                using (var c = new NpgsqlCommand(closeSql, conn, tx)) 
                {
                    c.Parameters.AddWithValue("@id", siparisId);
                    c.Parameters.AddWithValue("@pid", kapatanPersonelId);
                    closeAffected = c.ExecuteNonQuery(); 
                }

                if (closeAffected == 0) 
                {
                    // Başka bir istek bu siparişi kapatmış → idempotent OK, masa/log yapma  
                    return OperationResult.Ok("Sipariş zaten kapalı/ödenmiş."); 
                }

                // Masayı boşalt (sipariş kapanınca masa tekrar “Boş”a döner)
                const string masaSql = @"UPDATE masalar SET durum = 0 WHERE id = @mid;";

                using (var m = new NpgsqlCommand(masaSql, conn, tx))
                {
                    m.Parameters.AddWithValue("@mid", masaId);
                    m.ExecuteNonQuery();
                }

                // Ödeme ve kapanış logları (audit trail)
                SiparisLogRepository.AddLog(
                    conn, tx,
                    siparisId,
                    "PAYMENT",
                    null,
                    $"Yontem={(short)yontem};Tutar={toplam:0.00}",
                    actorUsername
                );

                SiparisLogRepository.AddLog(
                    conn, tx,
                    siparisId,
                    "CLOSE",
                    "Acik",
                    "Kapali",
                    actorUsername
                );

                return OperationResult.Ok("Ödeme alındı, sipariş kapatıldı.");
            }
            catch (PostgresException ex)
            {
                return OperationResult.Fail($"Veritabanı hatası. (Kod: {ex.SqlState})");
            }
            catch
            {
                return OperationResult.Fail("Beklenmeyen bir hata oluştu.");
            }
        }


    }
}
