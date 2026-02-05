// Mutfak tarafı DB erişimi: “açık sipariş + mutfak kalemleri” listesini üretir
// ve kalem durum güncellemesini (row-lock + log) transaction içinde yapar.

using Npgsql;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Data
{
    public class MutfakRepository
    {
        public MutfakRepository(IConfiguration configuration){}

        // Açık siparişlerdeki mutfak kalemlerini (durum 0/1/2) sipariş bazında gruplayıp döner.
        // Not: Connection dışarıdan gelir (service açar), burada sadece query+map yapılır.
        public OperationResult<List<MutfakSiparisVm>> GetPendingOrders(NpgsqlConnection conn) 
        {
            try
            {
                // Açık siparişler + kalemleri
                const string sql = @"
            SELECT s.id as siparis_id, m.masa_no, s.olusturma_tarihi,
                   sk.id as kalem_id, u.ad as urun_ad, sk.adet, sk.durum
            FROM siparisler s
            JOIN masalar m ON m.id = s.masa_id
            JOIN siparis_kalemleri sk ON sk.siparis_id = s.id
            JOIN urunler u ON u.id = sk.urun_id
            WHERE s.durum = 0
              AND sk.durum IN (0,1,2) 
            ORDER BY s.id DESC, sk.durum ASC, u.ad ASC;
        ";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var r = cmd.ExecuteReader();

                var map = new Dictionary<int, MutfakSiparisVm>();

                while (r.Read())
                {
                    var siparisId = r.GetInt32(0);
                    var masaNo = r.GetInt32(1);
                    var olusturma = r.GetDateTime(2);

                    var kalemId = r.GetInt32(3);
                    var urunAd = r.GetString(4);
                    var adet = r.GetInt32(5);
                    var durum = r.GetInt16(6);

                    if (!map.TryGetValue(siparisId, out var s))
                    {
                        s = new MutfakSiparisVm
                        {
                            SiparisId = siparisId,
                            MasaNo = masaNo,
                            OlusturmaTarihi = olusturma
                        };
                        map[siparisId] = s;
                    }


                    s.Kalemler.Add(new MutfakKalemVm
                    {
                        KalemId = kalemId,
                        UrunAd = urunAd,
                        Adet = adet,
                        Durum = durum
                    });
                }

                return OperationResult<List<MutfakSiparisVm>>.Ok(map.Values.ToList());
            }
            catch (PostgresException ex)
            {
                return OperationResult<List<MutfakSiparisVm>>.Fail($"DB hatası. (Kod: {ex.SqlState})");
            }
            catch
            {
                return OperationResult<List<MutfakSiparisVm>>.Fail("Beklenmeyen hata.");
            }
        }


        // Kalem durumunu günceller: önce kalemi ve bağlı siparişi lock’lar (sipariş açık olmalı),
        // sonra update yapar ve log’a “ITEM_STATUS” kaydı düşer.
        public OperationResult SetItemStatus(NpgsqlConnection conn, NpgsqlTransaction tx,
            int kalemId, short durum, string? actorUsername) 
        {
            if (kalemId <= 0) return OperationResult.Fail("Geçersiz kalem.");
            if (durum < 0 || durum > 3) return OperationResult.Fail("Geçersiz durum.");

            try
            {
                const string getOldSql = @"
SELECT sk.siparis_id, sk.durum
FROM siparis_kalemleri sk
JOIN siparisler s ON s.id = sk.siparis_id
WHERE sk.id = @id
  AND s.durum = 0
FOR UPDATE;
";

                int siparisId;
                short oldDurum;

                using (var getCmd = new NpgsqlCommand(getOldSql, conn, tx))
                {
                    getCmd.Parameters.AddWithValue("@id", kalemId);
                    using var r = getCmd.ExecuteReader();
                    if (!r.Read())
                        return OperationResult.Fail("Kalem bulunamadı veya sipariş kapalı.");

                    siparisId = r.GetInt32(0);
                    oldDurum = r.GetInt16(1);
                }

                const string updSql = @"UPDATE siparis_kalemleri SET durum=@d WHERE id=@id;";
                using (var cmd = new NpgsqlCommand(updSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@d", durum);
                    cmd.Parameters.AddWithValue("@id", kalemId);
                    cmd.ExecuteNonQuery();
                }

                // Audit trail: kim, hangi kalemi hangi durumdan hangi duruma çekti?
                SiparisLogRepository.AddLog(
                    conn, tx,
                    siparisId,
                    "ITEM_STATUS",
                    oldDurum.ToString(),
                    durum.ToString(),
                    actorUsername
                );

                return OperationResult.Ok("Güncellendi.");
            }
            catch (PostgresException ex)
            {
                return OperationResult.Fail($"DB hatası. (Kod: {ex.SqlState})");
            }
            catch
            {
                return OperationResult.Fail("Beklenmeyen hata.");
            }
        }

    }
}
