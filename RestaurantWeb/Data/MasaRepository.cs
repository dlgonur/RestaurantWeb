// Masa tablosu için ham DB erişimi (Npgsql).
// CRUD + aktif/pasif toggle ve rezervasyon/sipariş senaryoları için satır kilitleme (FOR UPDATE) içerir.

using Npgsql;
using RestaurantWeb.Models;


namespace RestaurantWeb.Data
{
    public class MasaRepository
    {
        private readonly string _connStr;

        public MasaRepository(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("PostgreSqlConnection")
                      ?? throw new InvalidOperationException("Connection string not found.");
        }

        // Tüm masaları listeler (board + admin CRUD için)
        public OperationResult<List<Masa>> GetAll()
        {
            try
            {
                var list = new List<Masa>();

                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
                    SELECT id, masa_no, kapasite, aktif_mi, durum 
                    FROM masalar
                    ORDER BY masa_no;
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    // DB -> entity mapping
                    list.Add(new Masa
                    {
                        Id = reader.GetInt32(0),
                        MasaNo = reader.GetInt32(1),
                        Kapasite = reader.GetInt32(2),
                        AktifMi = reader.GetBoolean(3),
                        Durum = (MasaDurumu)reader.GetInt16(4)
                    });
                }

                if (list.Count == 0)
                    return OperationResult<List<Masa>>.Ok(list, "Henüz masa kaydı bulunmuyor.");

                return OperationResult<List<Masa>>.Ok(list, "");
            }
            catch (PostgresException ex)
            {
                return OperationResult<List<Masa>>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<List<Masa>>.Fail(
                    "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin."
                );
            }
        }

        public OperationResult<Masa> GetById(int id)
        {
            if (id <= 0)
                return OperationResult<Masa>.Fail("Geçersiz masa id.");

            try
            { 
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
    SELECT id, masa_no, kapasite, aktif_mi, durum 
    FROM masalar
    WHERE id = @id;
";


                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return OperationResult<Masa>.Fail("Masa bulunamadı.");


                var masa = new Masa
                {
                    Id = reader.GetInt32(0),
                    MasaNo = reader.GetInt32(1),
                    Kapasite = reader.GetInt32(2),
                    AktifMi = reader.GetBoolean(3),
                    Durum = (MasaDurumu)reader.GetInt16(4)
                };

                return OperationResult<Masa>.Ok(masa);

            }
            catch (PostgresException ex)
            {
                return OperationResult<Masa>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<Masa>.Fail("Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
            }
        }

        public OperationResult Add(int masaNo, int kapasite)
        {
            if (masaNo <= 0)
                return OperationResult.Fail("Masa numarası 0'dan büyük olmalıdır.");

            if (kapasite <= 0)
                return OperationResult.Fail("Kapasite 0'dan büyük olmalıdır.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr); 
                conn.Open();

                const string sql = @"
                    INSERT INTO masalar (masa_no, kapasite)
                    VALUES (@masa_no, @kapasite);
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@masa_no", masaNo);
                cmd.Parameters.AddWithValue("@kapasite", kapasite);

                var affected = cmd.ExecuteNonQuery();
                if (affected == 1)
                    return OperationResult.Ok($"Masa {masaNo} eklendi. (Kapasite: {kapasite})");

                return OperationResult.Fail("Masa eklenemedi. Lütfen tekrar deneyin.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return OperationResult.Fail($"Masa {masaNo} zaten mevcut.");
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

        public OperationResult Update(int id, int masaNo, int kapasite) 
        {
            if (id <= 0) 
                return OperationResult.Fail("Geçersiz masa id."); 

            if (masaNo <= 0)
                return OperationResult.Fail("Geçersiz masa numarası.");

            if (kapasite <= 0)
                return OperationResult.Fail("Kapasite 0'dan büyük olmalıdır.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            UPDATE masalar
            SET masa_no = @masa_no, kapasite = @kapasite
            WHERE id = @id;"; 

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id); 
                cmd.Parameters.AddWithValue("@masa_no", masaNo);
                cmd.Parameters.AddWithValue("@kapasite", kapasite);

                var affected = cmd.ExecuteNonQuery();
                if (affected == 1)
                    return OperationResult.Ok($"Masa {masaNo} güncellendi. (Kapasite: {kapasite})");

                return OperationResult.Fail("Masa bulunamadı."); 
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return OperationResult.Fail($"Masa {masaNo} zaten mevcut.");
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
        public OperationResult<bool> ToggleAktif(int id)
        {
            if (id <= 0)
                return OperationResult<bool>.Fail("Geçersiz masa id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            UPDATE masalar
            SET aktif_mi = NOT aktif_mi
            WHERE id = @id
            RETURNING masa_no, aktif_mi;
        ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                    return OperationResult<bool>.Fail("Masa bulunamadı.");

                var masaNo = reader.GetInt32(0);
                var yeniDurum = reader.GetBoolean(1);

                var msg = yeniDurum
                    ? $"Masa {masaNo} aktif yapıldı."
                    : $"Masa {masaNo} pasif yapıldı.";

                return OperationResult<bool>.Ok(yeniDurum, msg);
            }
            catch (PostgresException ex)
            {
                return OperationResult<bool>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<bool>.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
            }
        }

        public OperationResult Delete(int id)
        {
            if (id <= 0)
                return OperationResult.Fail("Geçersiz masa id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                // 1) Masa var mı + masa_no al (mesaj için) // ★
                const string sqlMasaNo = @"SELECT masa_no FROM masalar WHERE id = @id;";
                using (var cmdNo = new NpgsqlCommand(sqlMasaNo, conn))
                {
                    cmdNo.Parameters.AddWithValue("@id", id);
                    var masaNoObj = cmdNo.ExecuteScalar();
                    if (masaNoObj == null)
                        return OperationResult.Fail("Masa bulunamadı ya da zaten silinmiş.");

                    var masaNo = Convert.ToInt32(masaNoObj);

                    // 2) Bağlı kayıt var mı? (FK restrict yüzünden silinemez) // ★
                    const string sqlHasRefs = @"
                SELECT
                    EXISTS (SELECT 1 FROM siparisler WHERE masa_id = @id) AS has_siparis,
                    EXISTS (SELECT 1 FROM rezervasyonlar WHERE masa_id = @id) AS has_rez;
            ";

                    using (var cmdRefs = new NpgsqlCommand(sqlHasRefs, conn))
                    {
                        cmdRefs.Parameters.AddWithValue("@id", id);

                        using var r = cmdRefs.ExecuteReader();
                        r.Read();

                        var hasSiparis = r.GetBoolean(0);
                        var hasRez = r.GetBoolean(1);

                        if (hasSiparis || hasRez)
                        {
                            // Burada “hard delete” yerine pasif yapmayı öneriyoruz // ★
                            var detay = (hasSiparis, hasRez) switch
                            {
                                (true, true) => "sipariş ve rezervasyon kayıtları",
                                (true, false) => "sipariş kayıtları",
                                (false, true) => "rezervasyon kayıtları",
                                _ => "ilişkili kayıtlar"
                            };

                            return OperationResult.Fail($"Masa {masaNo} silinemez: Bu masaya bağlı {detay} var. (Öneri: Pasif Yap)");
                        }
                    }

                    // 3) Bağ yoksa sil // ★
                    const string sqlDelete = @"DELETE FROM masalar WHERE id = @id;";
                    using var cmdDel = new NpgsqlCommand(sqlDelete, conn);
                    cmdDel.Parameters.AddWithValue("@id", id);

                    var affected = cmdDel.ExecuteNonQuery();
                    if (affected == 1)
                        return OperationResult.Ok($"Masa {masaNo} silindi.");

                    return OperationResult.Fail("Masa bulunamadı ya da zaten silinmiş.");
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "23001" || ex.SqlState == "23503") // ★
            {
                // 23001: restrict violation, 23503: foreign key violation
                return OperationResult.Fail("Masa silinemedi: Bu masaya bağlı kayıtlar var. (Öneri: Pasif Yap)");
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

        // Transaction dışarıdan yönetilir; burada sadece satırı kilitleyip durumu döndürürüz
        public MasaDurumDto? GetDurumAndLock(NpgsqlConnection conn, NpgsqlTransaction tx, int masaId)
        {
            const string sql = @"
        SELECT id, aktif_mi, durum
        FROM masalar
        WHERE id = @id
        FOR UPDATE; -- Satırı kilitler, concurrency için kritik
    ";

            using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@id", masaId);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new MasaDurumDto
            {
                Id = r.GetInt32(0),
                AktifMi = r.GetBoolean(1),
                Durum = (MasaDurumu)r.GetInt16(2)
            };
        }

        // Board / rezervasyon otomasyonu için minimal masa durum DTO’su
        public class MasaDurumDto
        {
            public int Id { get; set; }
            public bool AktifMi { get; set; }
            public MasaDurumu Durum { get; set; }
        }

    }
}
