using Npgsql;
using RestaurantWeb.Models;
using System.Collections.Generic;

namespace RestaurantWeb.Data
{
    public class MasaRepository
    {
        private readonly string _connStr;

        public MasaRepository(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("PostgreSqlConnection")
                      ?? throw new InvalidOperationException("Connection string not found.");
        }

        public OperationResult<List<Masa>> GetAll()
        {
            try
            {
                var list = new List<Masa>();

                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
                    SELECT id, masa_no, kapasite, aktif_mi, durum 
                    FROM masalar
                    ORDER BY masa_no;
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    // DB null gelmez diye tasarladık ama yine de korumalı okuyoruz
                    list.Add(new Masa
                    {
                        Id = reader.GetInt32(0),
                        MasaNo = reader.GetInt32(1),
                        Kapasite = reader.GetInt32(2),
                        AktifMi = reader.GetBoolean(3),
                        Durum = (MasaDurumu)reader.GetInt16(4)
                    });
                }

                if (list.Count == 0)
                    return OperationResult<List<Masa>>.Ok(list, "Henüz masa kaydı bulunmuyor.");

                return OperationResult<List<Masa>>.Ok(list, "");
            }
            catch (PostgresException ex)
            {
                // Diğer DB hataları
                return OperationResult<List<Masa>>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                // DB dışı hatalar
                return OperationResult<List<Masa>>.Fail(
                    "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin."
                );
            }
        }

        public OperationResult<Masa> GetById(int id)
        {
            if (id <= 0)
                return OperationResult<Masa>.Fail("Geçersiz masa id.");

            try
            { 
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
    SELECT id, masa_no, kapasite, aktif_mi, durum 
    FROM masalar
    WHERE id = @id;
";


                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return OperationResult<Masa>.Fail("Masa bulunamadı.");


                var masa = new Masa
                {
                    Id = reader.GetInt32(0),
                    MasaNo = reader.GetInt32(1),
                    Kapasite = reader.GetInt32(2),
                    AktifMi = reader.GetBoolean(3),
                    Durum = (MasaDurumu)reader.GetInt16(4)
                };

                return OperationResult<Masa>.Ok(masa);

            }
            catch (PostgresException ex)
            {
                return OperationResult<Masa>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<Masa>.Fail("Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
            }
        }

        public OperationResult Add(int masaNo, int kapasite)
        {
            if (masaNo <= 0)
                return OperationResult.Fail("Masa numarası 0'dan büyük olmalıdır.");

            if (kapasite <= 0)
                return OperationResult.Fail("Kapasite 0'dan büyük olmalıdır.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr); 
                conn.Open();

                const string sql = @"
                    INSERT INTO masalar (masa_no, kapasite)
                    VALUES (@masa_no, @kapasite);
                ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@masa_no", masaNo);
                cmd.Parameters.AddWithValue("@kapasite", kapasite);

                var affected = cmd.ExecuteNonQuery();
                if (affected == 1)
                    return OperationResult.Ok($"Masa {masaNo} eklendi. (Kapasite: {kapasite})");

                return OperationResult.Fail("Masa eklenemedi. Lütfen tekrar deneyin.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                // unique violation (masa_no unique)
                return OperationResult.Fail($"Masa {masaNo} zaten mevcut.");
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

        public OperationResult Update(int id, int masaNo, int kapasite) 
        {
            if (id <= 0) 
                return OperationResult.Fail("Geçersiz masa id."); 

            if (masaNo <= 0)
                return OperationResult.Fail("Geçersiz masa numarası.");

            if (kapasite <= 0)
                return OperationResult.Fail("Kapasite 0'dan büyük olmalıdır.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            UPDATE masalar
            SET masa_no = @masa_no, kapasite = @kapasite
            WHERE id = @id;"; 

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id); 
                cmd.Parameters.AddWithValue("@masa_no", masaNo);
                cmd.Parameters.AddWithValue("@kapasite", kapasite);

                var affected = cmd.ExecuteNonQuery();
                if (affected == 1)
                    return OperationResult.Ok($"Masa {masaNo} güncellendi. (Kapasite: {kapasite})");

                return OperationResult.Fail("Masa bulunamadı."); 
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return OperationResult.Fail($"Masa {masaNo} zaten mevcut.");
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
                return OperationResult<bool>.Fail("Geçersiz masa id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
            UPDATE masalar
            SET aktif_mi = NOT aktif_mi
            WHERE id = @id
            RETURNING masa_no, aktif_mi;
        ";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                    return OperationResult<bool>.Fail("Masa bulunamadı.");

                var masaNo = reader.GetInt32(0);
                var yeniDurum = reader.GetBoolean(1);

                var msg = yeniDurum
                    ? $"Masa {masaNo} aktif yapıldı."
                    : $"Masa {masaNo} pasif yapıldı.";

                return OperationResult<bool>.Ok(yeniDurum, msg);
            }
            catch (PostgresException ex)
            {
                return OperationResult<bool>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
            }
            catch (Exception)
            {
                return OperationResult<bool>.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
            }
        }

        public OperationResult Delete(int id)
        {
            if (id <= 0)
                return OperationResult.Fail("Geçersiz masa id.");

            try
            {
                using var conn = new NpgsqlConnection(_connStr);
                conn.Open();

                const string sql = @"
                    DELETE FROM masalar 
                    WHERE id = @id
                    RETURNING masa_no";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);

                
                var result = cmd.ExecuteScalar();
                if (result == null)
                {
                    return OperationResult.Fail("Masa bulunamadı ya da zaten silinmiş.");
                }

                var masaNo = (int)result;
                return OperationResult.Ok($"Masa {masaNo} silindi.");
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

        public MasaDurumDto? GetDurumAndLock(NpgsqlConnection conn, NpgsqlTransaction tx, int masaId)
        {
            // Transaction dışarıdan geliyor, biz sadece sorguyu çalıştırıyoruz.
            const string sql = @"
        SELECT id, aktif_mi, durum
        FROM masalar
        WHERE id = @id
        FOR UPDATE; -- Satırı kilitler, concurrency için kritik
    ";

            using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@id", masaId);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new MasaDurumDto
            {
                Id = r.GetInt32(0),
                AktifMi = r.GetBoolean(1),
                Durum = (MasaDurumu)r.GetInt16(2)
            };
        }
        public class MasaDurumDto
        {
            public int Id { get; set; }
            public bool AktifMi { get; set; }
            public MasaDurumu Durum { get; set; }
        }

    }
}
