using Npgsql;
using RestaurantWeb.Models;
using RestaurantWeb.Models.Dtos;

namespace RestaurantWeb.Data
{
    public class CatalogRepository
    {
        private readonly string _connStr;

        public CatalogRepository(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("PostgreSqlConnection")
                      ?? throw new InvalidOperationException("Connection string not found.");
        }

        public OperationResult<List<CatalogCategoryDto>> GetActiveCategories() // ★
        {
            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
SELECT id, ad
FROM kategoriler
WHERE aktif_mi = TRUE
ORDER BY ad;";

                using var cmd = new NpgsqlCommand(sql, conn);

                var list = new List<CatalogCategoryDto>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new CatalogCategoryDto
                    {
                        Id = r.GetInt32(0),
                        Ad = r.GetString(1)
                    });
                }

                return OperationResult<List<CatalogCategoryDto>>.Ok(list); // ★
            }
            catch (PostgresException ex)
            {
                return OperationResult<List<CatalogCategoryDto>>.Fail(
                    $"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})"
                ); // ★
            }
            catch
            {
                return OperationResult<List<CatalogCategoryDto>>.Fail("Beklenmeyen hata."); // ★
            }
        }

        public OperationResult<List<CatalogProductDto>> GetActiveProducts(int? kategoriId) // ★
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

                var list = new List<CatalogProductDto>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new CatalogProductDto
                    {
                        Id = r.GetInt32(0),
                        Ad = r.GetString(1),
                        Fiyat = r.GetDecimal(2),
                        Stok = r.GetInt32(3),
                        Kategori = r.GetString(4)
                    });
                }

                return OperationResult<List<CatalogProductDto>>.Ok(list); // ★
            }
            catch (PostgresException ex)
            {
                return OperationResult<List<CatalogProductDto>>.Fail(
                    $"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})"
                ); // ★
            }
            catch
            {
                return OperationResult<List<CatalogProductDto>>.Fail("Beklenmeyen hata."); // ★
            }
        }
    }
}
