using Npgsql;
using RestaurantWeb.Models;
using NpgsqlTypes;


namespace RestaurantWeb.Data
{
   public class UrunRepository
    {
        private readonly string _connStr;

        public UrunRepository(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("PostgreSqlConnection")
                      ?? throw new InvalidOperationException("Connection string not found.");
        }

        public OperationResult<List<Urun>> GetAllWithKategori()
        {
            try
            {
                var list = new List<Urun>();

                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
    SELECT u.id,
           u.kategori_id,
           u.ad,
           u.fiyat,
           u.aktif_mi,
           u.olusturma_tarihi,
           u.stok,
           k.ad AS kategori_ad,  -- ★ Buraya virgül eklendi
           (u.resim IS NOT NULL) AS resim_var
    FROM urunler u
    JOIN kategoriler k ON k.id = u.kategori_id
    ORDER BY u.id;
";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new Urun
                    {
                        Id = reader.GetInt32(0),
                        KategoriId = reader.GetInt32(1),
                        Ad = reader.GetString(2),
                        Fiyat = reader.GetDecimal(3),
                        AktifMi = reader.GetBoolean(4),
                        OlusturmaTarihi = reader.GetDateTime(5),
                        Stok = reader.GetInt32(6),
                        KategoriAd = reader.GetString(7),                        
                    });
                }

                if (list.Count == 0)
                    return OperationResult<List<Urun>>.Ok(list, "Henüz ürün kaydı bulunmuyor.");

                return OperationResult<List<Urun>>.Ok(list, "");
            }
            catch (PostgresException ex)
            {
                return OperationResult<List<Urun>>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception) 
            {
                return OperationResult<List<Urun>>.Fail($"Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
            }

        }

        public OperationResult<Urun> GetByIdWithKategori(int id)
        {
            if (id <= 0)
                return OperationResult<Urun>.Fail("Geçersiz ürün id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
    SELECT u.id, u.kategori_id, u.ad, u.fiyat, u.stok, u.aktif_mi, u.olusturma_tarihi,
           k.ad AS kategori_ad,
           u.resim, u.resim_mime, u.resim_adi
    FROM urunler u
    JOIN kategoriler k ON k.id = u.kategori_id
    WHERE u.id = @id;
";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return OperationResult<Urun>.Fail("Ürün bulunamadı.");

                var urun = new Urun
                {
                    Id = reader.GetInt32(0),
                    KategoriId = reader.GetInt32(1),
                    Ad = reader.GetString(2),
                    Fiyat = reader.GetDecimal(3),
                    Stok = reader.GetInt32(4),
                    AktifMi = reader.GetBoolean(5),
                    OlusturmaTarihi = reader.GetDateTime(6),
                    KategoriAd = reader.GetString(7),
                    Resim = reader.IsDBNull(8) ? null : (byte[])reader[8], 
                    ResimMime = reader.IsDBNull(9) ? null : reader.GetString(9), 
                    ResimAdi = reader.IsDBNull(10) ? null : reader.GetString(10) 
                };


                return OperationResult<Urun>.Ok(urun);
            }
            catch (PostgresException ex)
            {
                return OperationResult<Urun>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<Urun>.Fail("Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
            }
        }

        public OperationResult Add(int kategoriId, string ad, decimal fiyat, int stok, byte[]? resim, string? resimMime, string? resimAdi)
        {

            if (kategoriId <= 0) 
                return OperationResult.Fail("Geçersiz kategori seçimi."); 

            if (string.IsNullOrWhiteSpace(ad)) 
                return OperationResult.Fail("Ürün adı boş bırakılamaz."); 

            if (fiyat < 0) 
                return OperationResult.Fail("Fiyat 0'dan küçük olamaz.");

            if (stok < 0)
                return OperationResult.Fail("Stok 0'dan küçük olamaz.");

            ad = ad.Trim(); 

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
    INSERT INTO urunler (kategori_id, ad, fiyat, stok, resim, resim_mime, resim_adi)
    VALUES (@kategori_id, @ad, @fiyat, @stok, @resim, @resim_mime, @resim_adi);
";


                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@kategori_id", kategoriId);
                cmd.Parameters.AddWithValue("@ad", ad);
                cmd.Parameters.Add("@fiyat", NpgsqlDbType.Numeric).Value = fiyat;
                cmd.Parameters.AddWithValue("@stok", stok); // ★
                cmd.Parameters.AddWithValue("@resim", (object?)resim ?? DBNull.Value); // ★
                cmd.Parameters.AddWithValue("@resim_mime", (object?)resimMime ?? DBNull.Value); // ★
                cmd.Parameters.AddWithValue("@resim_adi", (object?)resimAdi ?? DBNull.Value); // ★

                if (stok < 0) // ★
                    return OperationResult.Fail("Stok 0'dan küçük olamaz."); // ★

                var affected = cmd.ExecuteNonQuery();
                if (affected == 1) 
                {
                    return OperationResult.Ok($"Ürün '{ad}' başarıyla eklendi.");
                }
                return OperationResult.Fail("Ürün eklenemedi. Lütfen tekrar deneyin.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return OperationResult.Fail($"Ürün '{ad}' zaten mevcut.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23503") 
            {
                return OperationResult.Fail("Seçilen kategori bulunamadı. Lütfen geçerli bir kategori seçin."); 
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

        public OperationResult Update(int id, int kategoriId, string ad, decimal fiyat, int stok, byte[]? resim, string? resimMime, string? resimAdi, bool resimGuncellensin) // ★
        {
            if (id <= 0)
                return OperationResult.Fail("Geçersiz ürün id.");

            if (kategoriId <= 0)
                return OperationResult.Fail("Geçersiz kategori seçimi.");

            if (string.IsNullOrWhiteSpace(ad))
                return OperationResult.Fail("Ürün adı boş bırakılamaz.");

            if (fiyat < 0)
                return OperationResult.Fail("Fiyat 0'dan küçük olamaz.");

            if (stok < 0)
                return OperationResult.Fail("Stok 0'dan küçük olamaz.");

            ad = ad.Trim();

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sqlWithImage = @"
UPDATE urunler
SET kategori_id = @kategori_id,
    ad = @ad,
    fiyat = @fiyat,
    stok = @stok,
    resim = @resim,
    resim_mime = @resim_mime,
    resim_adi = @resim_adi
WHERE id = @id;
";

                const string sqlNoImage = @"
UPDATE urunler
SET kategori_id = @kategori_id,
    ad = @ad,
    fiyat = @fiyat,
    stok = @stok
WHERE id = @id;
";

                var sql = resimGuncellensin ? sqlWithImage : sqlNoImage; // ★
                using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@kategori_id", kategoriId);
                cmd.Parameters.AddWithValue("@ad", ad);
                cmd.Parameters.Add("@fiyat", NpgsqlDbType.Numeric).Value = fiyat;
                cmd.Parameters.AddWithValue("@stok", stok);

                if (resimGuncellensin) // ★
                {
                    cmd.Parameters.AddWithValue("@resim", (object?)resim ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@resim_mime", (object?)resimMime ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@resim_adi", (object?)resimAdi ?? DBNull.Value);
                }

                var affected = cmd.ExecuteNonQuery();
                if (affected == 0)
                    return OperationResult.Fail("Ürün bulunamadı.");

                return OperationResult.Ok("Ürün güncellendi.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return OperationResult.Fail($"Ürün '{ad}' zaten mevcut.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                return OperationResult.Fail("Seçilen kategori bulunamadı. Lütfen geçerli bir kategori seçin.");
            }
            catch (PostgresException ex)
            {
                return OperationResult.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult.Fail("Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
            }
        }

        public OperationResult Delete(int id)
        {
            if (id <= 0) 
                return OperationResult.Fail("Geçersiz ürün id."); 

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
                    DELETE FROM urunler 
                    WHERE id = @id
                    RETURNING ad";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);


                var obj = cmd.ExecuteScalar(); 
                if (obj is not string ad)      
                    return OperationResult.Fail("Ürün bulunamadı.");

                return OperationResult.Ok($"Ürün {ad} başarıyla silindi.");
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
                return OperationResult<bool>.Fail("Geçersiz ürün id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            UPDATE urunler
            SET aktif_mi = NOT aktif_mi
            WHERE id = @id
            RETURNING ad, aktif_mi;
        ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return OperationResult<bool>.Fail("Ürün bulunamadı.");

                var ad = reader.GetString(0);
                var yeniDurum = reader.GetBoolean(1);

                var msg = yeniDurum
                    ? $"Ürün '{ad}' aktif edildi."
                    : $"Ürün '{ad}' pasif edildi.";

                return OperationResult<bool>.Ok(yeniDurum, msg);
            }
            catch (PostgresException ex)
            {
                return OperationResult<bool>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<bool>.Fail("Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
            }
        }

        // ★
        public OperationResult<(byte[] Bytes, string Mime)> GetResimByUrunId(int id)
        {
            if (id <= 0)
                return OperationResult<(byte[], string)>.Fail("Geçersiz ürün id.");

            try
            {
                using var conn = new Npgsql.NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            SELECT resim, resim_mime
            FROM urunler
            WHERE id = @id;
        ";

                using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return OperationResult<(byte[], string)>.Fail("Ürün bulunamadı.");

                if (reader.IsDBNull(0) || reader.IsDBNull(1))
                    return OperationResult<(byte[], string)>.Fail("Bu ürüne ait resim bulunamadı.");

                var bytes = (byte[])reader[0];
                var mime = reader.GetString(1);

                return OperationResult<(byte[], string)>.Ok((bytes, mime));
            }
            catch (Npgsql.PostgresException ex)
            {
                return OperationResult<(byte[], string)>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<(byte[], string)>.Fail("Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
            }
        }


    }
}
