using Npgsql;
using RestaurantWeb.Models;
using RestaurantWeb.Models.ViewModels;

namespace RestaurantWeb.Data
{
    public class SiparisLogRepository
    {
        private readonly string _connStr; // ⭐

        public SiparisLogRepository(IConfiguration configuration) // ⭐
        {
            _connStr = configuration.GetConnectionString("PostgreSqlConnection")
                      ?? throw new InvalidOperationException("Connection string not found.");
        }

        // WRITE: transaction içinden çağrılır
        public static void AddLog(NpgsqlConnection conn, NpgsqlTransaction tx,
            int siparisId, string action, string? oldValue, string? newValue, string? actorUsername)
        {
            const string sql = @"
                INSERT INTO siparis_log (siparis_id, action, old_value, new_value, actor_username)
                VALUES (@sid, @a, @o, @n, @u);
            ";

            using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@sid", siparisId);
            cmd.Parameters.AddWithValue("@a", action);
            cmd.Parameters.AddWithValue("@o", (object?)oldValue ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@n", (object?)newValue ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", (object?)actorUsername ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        // READ: UI için
        public OperationResult<List<SiparisLogItemVm>> GetLogs(int siparisId) // ⭐
        {
            if (siparisId <= 0)
                return OperationResult<List<SiparisLogItemVm>>.Fail("Geçersiz sipariş id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
                    SELECT id, siparis_id, action, old_value, new_value, actor_username, created_at
                    FROM siparis_log
                    WHERE siparis_id = @sid
                    ORDER BY created_at DESC, id DESC;
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@sid", siparisId);

                var list = new List<SiparisLogItemVm>();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new SiparisLogItemVm
                    {
                        Id = r.GetInt32(0),
                        SiparisId = r.GetInt32(1),
                        Action = r.GetString(2),
                        OldValue = r.IsDBNull(3) ? null : r.GetString(3),
                        NewValue = r.IsDBNull(4) ? null : r.GetString(4),
                        ActorUsername = r.IsDBNull(5) ? null : r.GetString(5),
                        CreatedAt = r.GetDateTime(6)
                    });
                }

                return OperationResult<List<SiparisLogItemVm>>.Ok(list);
            }
            catch (PostgresException ex)
            {
                return OperationResult<List<SiparisLogItemVm>>.Fail($"DB hatası. (Kod: {ex.SqlState})");
            }
            catch
            {
                return OperationResult<List<SiparisLogItemVm>>.Fail("Beklenmeyen hata.");
            }
        }
    }
}
