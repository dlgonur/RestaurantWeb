using Npgsql;
using RestaurantWeb.Helpers;  (PasswordHasher, PasswordGenerator)

namespace RestaurantWeb.Data
{
    public static class DbSeeder
    {
        public static void SeedMasalar(string connStr, int masaAdedi = 20, int defaultKapasite = 4)
        {
            if (string.IsNullOrWhiteSpace(connStr))
                throw new InvalidOperationException("Connection string boş olamaz.");

            using var conn = new NpgsqlConnection(connStr);
            conn.Open();

            const string countSql = @"SELECT COUNT(*) FROM masalar;";
            using var countCmd = new NpgsqlCommand(countSql, conn);
            var count = (long)countCmd.ExecuteScalar()!;

            if (count > 0)
                return;

            using var tx = conn.BeginTransaction();

            const string insertSql = @"
                INSERT INTO masalar (masa_no, kapasite, aktif_mi, durum)
                VALUES (@masa_no, @kapasite, TRUE, 0);
            ";

            using var insertCmd = new NpgsqlCommand(insertSql, conn, tx);
            insertCmd.Parameters.Add("@masa_no", NpgsqlTypes.NpgsqlDbType.Integer);
            insertCmd.Parameters.Add("@kapasite", NpgsqlTypes.NpgsqlDbType.Integer);

            for (int i = 1; i <= masaAdedi; i++)
            {
                insertCmd.Parameters["@masa_no"].Value = i;
                insertCmd.Parameters["@kapasite"].Value = defaultKapasite;
                insertCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public static (string Username, string Password, bool Created) SeedAdmin(string connStr) 
        {
            if (string.IsNullOrWhiteSpace(connStr))
                throw new InvalidOperationException("Connection string boş olamaz."); 

            const string username = "admin"; 
            var password = PasswordGenerator.Generate(12);  

            using var conn = new NpgsqlConnection(connStr); 
            conn.Open(); 

            // Admin var mı? (idempotent)
            const string existsSql = @"SELECT 1 FROM personeller WHERE kullanici_adi=@u LIMIT 1;"; 
            using (var existsCmd = new NpgsqlCommand(existsSql, conn)) 
            {
                existsCmd.Parameters.AddWithValue("@u", username); 
                var exists = existsCmd.ExecuteScalar(); 
                if (exists != null) 
                    return (username, "******", false);  
            }

            // Yoksa oluştur
            var (hash, salt) = PasswordHasher.CreateHash(password); 

            // rol: Admin = 1 (enum -> bitmask)
            const int adminRol = 1; 

            using var tx = conn.BeginTransaction(); 

            const string insertSql = @"
                INSERT INTO personeller (ad_soyad, kullanici_adi, sifre_hash, sifre_salt, rol, aktif_mi)
                VALUES (@ad, @kadi, @hash, @salt, @rol, TRUE);
            "; 

            using (var cmd = new NpgsqlCommand(insertSql, conn, tx)) 
            {
                cmd.Parameters.AddWithValue("@ad", "System Admin"); 
                cmd.Parameters.AddWithValue("@kadi", username); 
                cmd.Parameters.AddWithValue("@hash", hash); 
                cmd.Parameters.AddWithValue("@salt", salt); 
                cmd.Parameters.AddWithValue("@rol", adminRol); 
                cmd.ExecuteNonQuery(); 
            }

            tx.Commit(); 

            // Şifreyi bir kere göstermek için (lokalde kurulum kolaylaşsın)
            // İstersen bunu ILogger'a bağlarız; şimdilik Console yeterli. 
            Console.WriteLine($"[SeedAdmin] Created admin user. Username={username} Password={password}"); 

            return (username, password, true); 
        }
    }
}
