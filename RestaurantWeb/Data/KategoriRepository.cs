using Npgsql;
using RestaurantWeb.Models;

namespace RestaurantWeb.Data
{
    public class KategoriRepository
    {
        private readonly string _connStr;

        public KategoriRepository(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("PostgreSqlConnection")
                      ?? throw new InvalidOperationException("Connection string not found.");
        }

        public OperationResult<List<Kategori>> GetAll()
        {
            try
            {
                var list = new List<Kategori>();

                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
                SELECT id, ad, aktif_mi, olusturma_tarihi
                FROM kategoriler
                ORDER BY id;
            ";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new Kategori
                    {
                        Id = reader.GetInt32(0),
                        Ad = reader.GetString(1),
                        AktifMi = reader.GetBoolean(2),
                        OlusturmaTarihi = reader.GetDateTime(3)
                    });
                }

                if (list.Count == 0)
                {
                    return OperationResult<List<Kategori>>.Ok(list, "Hiç kategori bulunamadı.");
                }
                return OperationResult<List<Kategori>>.Ok(list, "");
            }
            catch (PostgresException ex)
            { 
                // Diğer DB hataları
                return OperationResult<List<Kategori>>.Fail(
                    $"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})"
                );
            }
            catch (Exception)
            {
                // DB dışı beklenmeyen hatalar
                return OperationResult<List<Kategori>>.Fail(
                    "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyiniz."
                );
            }
        }

        public OperationResult<Kategori> GetById(int id)
        {
            if (id <= 0)
                return OperationResult<Kategori>.Fail("Geçersiz kategori id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            SELECT id, ad, aktif_mi, olusturma_tarihi
            FROM kategoriler
            WHERE id = @id;
        ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return OperationResult<Kategori>.Fail("Kategori bulunamadı.");

                var kategori = new Kategori
                {
                    Id = reader.GetInt32(0),
                    Ad = reader.GetString(1),
                    AktifMi = reader.GetBoolean(2),
                    OlusturmaTarihi = reader.GetDateTime(3)
                };

                return OperationResult<Kategori>.Ok(kategori);
            }
            catch (PostgresException ex)
            {
                return OperationResult<Kategori>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<Kategori>.Fail("Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
            }
        }

        public OperationResult Add(string ad)
        {

            if (string.IsNullOrWhiteSpace(ad)) 
                return OperationResult.Fail("Kategori adı boş bırakılamaz."); 

            ad = ad.Trim(); 

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = "INSERT INTO kategoriler (ad) VALUES (@ad)";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ad", ad);  

                var affected = cmd.ExecuteNonQuery();
                if (affected == 1)
                {
                    return OperationResult.Ok($"Kategori '{ad}' başarıyla eklendi.");
                }
                return OperationResult.Fail("Kategori eklenemedi. Lütfen tekrar deneyin.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return OperationResult.Fail($"Kategori '{ad}' zaten mevcut.");
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

        public OperationResult Update(int id, string ad)
        {
            if (id <= 0)
                return OperationResult.Fail("Geçersiz kategori id.");

            if (string.IsNullOrWhiteSpace(ad))
                return OperationResult.Fail("Kategori adı boş bırakılamaz.");

            ad = ad.Trim();

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            UPDATE kategoriler
            SET ad = @ad
            WHERE id = @id;
        ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@ad", ad);

                var affected = cmd.ExecuteNonQuery();
                if (affected == 0)
                    return OperationResult.Fail("Kategori bulunamadı.");

                return OperationResult.Ok("Kategori güncellendi.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return OperationResult.Fail($"Kategori '{ad}' zaten mevcut.");
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
                return OperationResult.Fail("Geçersiz kategori id.");
            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql =
                    @"
                    DELETE FROM kategoriler 
                    WHERE id = @id
                    RETURNING ad";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                var result = cmd.ExecuteScalar();
                if (result == null)
                {
                    return OperationResult.Fail("Kategori bulunamadı.");
                }
                var ad = result.ToString();
                return OperationResult.Ok($"Kategori '{ad}' silindi.");
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation ||
                    ex.SqlState == PostgresErrorCodes.RestrictViolation)
            {
                return OperationResult.Fail("Bu kategoriye bağlı ürünler bulunmaktadır. Önce ürünleri silmeniz gerekir.");
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

        public OperationResult<bool> HasProducts(int kategoriId)
        {
            if (kategoriId <= 0)
                return OperationResult<bool>.Fail("Geçersiz kategori id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            SELECT EXISTS (
                SELECT 1 FROM urunler WHERE kategori_id = @id
            );
        ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", kategoriId);

                var exists = (bool)cmd.ExecuteScalar()!;
                return OperationResult<bool>.Ok(exists);
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

        public OperationResult<List<Urun>> GetProductsByKategoriId(int kategoriId, int limit = 20)
        {
            if (kategoriId <= 0)
                return OperationResult<List<Urun>>.Fail("Geçersiz kategori id.");

            try
            {
                var list = new List<Urun>();

                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            SELECT id, kategori_id, ad, fiyat, aktif_mi
            FROM urunler
            WHERE kategori_id = @kid
            ORDER BY id
            LIMIT @limit;
        ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@kid", kategoriId);
                cmd.Parameters.AddWithValue("@limit", limit);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new Urun
                    {
                        Id = reader.GetInt32(0),
                        KategoriId = reader.GetInt32(1),
                        Ad = reader.GetString(2),
                        Fiyat = reader.GetDecimal(3),
                        AktifMi = reader.GetBoolean(4)
                    });
                }

                return OperationResult<List<Urun>>.Ok(list);
            }
            catch (PostgresException ex)
            {
                return OperationResult<List<Urun>>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<List<Urun>>.Fail("Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
            }
        }

        public OperationResult ToggleAktif(int id)
        {
            if (id <= 0)
                return OperationResult.Fail("Geçersiz kategori id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            UPDATE kategoriler
            SET aktif_mi = NOT aktif_mi
            WHERE id = @id
            RETURNING aktif_mi;
        ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                var result = cmd.ExecuteScalar();
                if (result == null)
                    return OperationResult.Fail("Kategori bulunamadı.");

                var yeniDurum = (bool)result;
                return OperationResult.Ok(yeniDurum ? "Kategori aktif edildi." : "Kategori pasif edildi.");
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

    }
}
