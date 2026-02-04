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

        public NpgsqlConnection Create()
            => new NpgsqlConnection(_connStr);
    }
}
