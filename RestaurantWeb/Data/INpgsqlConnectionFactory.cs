using Npgsql;

namespace RestaurantWeb.Data
{
    public interface INpgsqlConnectionFactory
    {
        NpgsqlConnection Create();
    }
}
