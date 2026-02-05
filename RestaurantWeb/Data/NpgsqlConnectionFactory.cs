// PostgreSQL bağlantılarını tek noktadan üretir.
// Amaç: connection string’i merkezi tutmak ve
// service/repository katmanlarında “new NpgsqlConnection(...)”
// tekrarını ve config bağımlılığını dağıtmamak.

using Npgsql;

namespace RestaurantWeb.Data
{
    public class NpgsqlConnectionFactory : INpgsqlConnectionFactory
    {
        private readonly string _connStr;

        public NpgsqlConnectionFactory(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("PostgreSqlConnection")
                      ?? throw new InvalidOperationException("Connection string not found.");
        }

        // Her çağrıda yeni bir connection üretir.
        // Açma/kapama ve transaction yönetimi service katmanının sorumluluğundadır.
        public NpgsqlConnection Create()
            => new NpgsqlConnection(_connStr);
    }
}
