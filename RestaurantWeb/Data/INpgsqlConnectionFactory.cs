// PostgreSQL bağlantısı üretmek için soyutlama.
// Amaç: Connection oluşturma mantığını tek yerde toplamak
// ve repository'leri doğrudan connection string bağımlılığından ayırmak.

using Npgsql;

namespace RestaurantWeb.Data
{
    public interface INpgsqlConnectionFactory
    {
        NpgsqlConnection Create();
    }
}
