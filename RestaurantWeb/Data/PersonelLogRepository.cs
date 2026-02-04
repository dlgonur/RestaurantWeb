using Npgsql;
using RestaurantWeb.Models;

namespace RestaurantWeb.Data
{
    public class PersonelLogRepository
    {
        private readonly INpgsqlConnectionFactory _cf; // ★

        public PersonelLogRepository(INpgsqlConnectionFactory cf) // ★
        {
            _cf = cf; // ★
        }

        public void Add(PersonelLog log)
        {
            using var conn = _cf.Create(); // ★
            conn.Open();

            const string sql = @"
INSERT INTO personel_loglari
(
    actor_personel_id, actor_kullanici_adi,
    target_personel_id, target_kullanici_adi,
    aksiyon, old_rol, new_rol, old_aktif_mi, new_aktif_mi,
    aciklama, ip
)
VALUES
(
    @actor_id, @actor_u,
    @target_id, @target_u,
    @aksiyon, @old_rol, @new_rol, @old_aktif, @new_aktif,
    @aciklama, @ip
);
";

            using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@actor_id", (object?)log.ActorPersonelId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@actor_u", (object?)log.ActorKullaniciAdi ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@target_id", (object?)log.TargetPersonelId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@target_u", (object?)log.TargetKullaniciAdi ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@aksiyon", log.Aksiyon);
            cmd.Parameters.AddWithValue("@old_rol", (object?)log.OldRol ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@new_rol", (object?)log.NewRol ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@old_aktif", (object?)log.OldAktifMi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@new_aktif", (object?)log.NewAktifMi ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@aciklama", (object?)log.Aciklama ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ip", (object?)log.Ip ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        public List<PersonelLog> GetList(DateTime? start, DateTime? end, string? aksiyon, string? targetUsername, int limit)
        {
            using var conn = _cf.Create(); // ★
            conn.Open();

            var sql = @"
SELECT
    id,
    actor_personel_id,
    actor_kullanici_adi,
    target_personel_id,
    target_kullanici_adi,
    aksiyon,
    old_rol,
    new_rol,
    old_aktif_mi,
    new_aktif_mi,
    aciklama,
    ip,
    created_at
FROM personel_loglari
WHERE 1=1
";

            if (start.HasValue)
                sql += " AND created_at >= @start"; // ★

            if (end.HasValue)
                sql += " AND created_at < @end"; // ★

            if (!string.IsNullOrWhiteSpace(aksiyon))
                sql += " AND aksiyon = @aksiyon"; // ★

            if (!string.IsNullOrWhiteSpace(targetUsername))
                sql += " AND target_kullanici_adi ILIKE @target"; // ★

            sql += " ORDER BY created_at DESC LIMIT @limit;"; // ★

            using var cmd = new NpgsqlCommand(sql, conn);

            if (start.HasValue)
                cmd.Parameters.AddWithValue("@start", start.Value.Date); // ★

            if (end.HasValue)
                cmd.Parameters.AddWithValue("@end", end.Value.Date.AddDays(1)); // ★

            if (!string.IsNullOrWhiteSpace(aksiyon))
                cmd.Parameters.AddWithValue("@aksiyon", aksiyon); // ★

            if (!string.IsNullOrWhiteSpace(targetUsername))
                cmd.Parameters.AddWithValue("@target", $"%{targetUsername}%"); // ★

            cmd.Parameters.AddWithValue("@limit", limit); // ★

            var list = new List<PersonelLog>();
            using var r = cmd.ExecuteReader();

            while (r.Read())
            {
                list.Add(new PersonelLog
                {
                    Id = r.GetInt32(0),
                    ActorPersonelId = r.IsDBNull(1) ? null : r.GetInt32(1),
                    ActorKullaniciAdi = r.IsDBNull(2) ? null : r.GetString(2),
                    TargetPersonelId = r.IsDBNull(3) ? null : r.GetInt32(3),
                    TargetKullaniciAdi = r.IsDBNull(4) ? null : r.GetString(4),
                    Aksiyon = r.GetString(5),
                    OldRol = r.IsDBNull(6) ? null : r.GetInt32(6),
                    NewRol = r.IsDBNull(7) ? null : r.GetInt32(7),
                    OldAktifMi = r.IsDBNull(8) ? null : r.GetBoolean(8),
                    NewAktifMi = r.IsDBNull(9) ? null : r.GetBoolean(9),
                    Aciklama = r.IsDBNull(10) ? null : r.GetString(10),
                    Ip = r.IsDBNull(11) ? null : r.GetString(11),
                    CreatedAt = r.GetDateTime(12)
                });
            }

            return list;
        }
    }
}
