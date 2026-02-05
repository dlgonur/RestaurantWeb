
// Personeller tablosu için veri erişim katmanı

using Npgsql;
    using NpgsqlTypes;
    using RestaurantWeb.Helpers;
    using RestaurantWeb.Models;

    namespace RestaurantWeb.Data
    {
        public class PersonelRepository
        {
            private readonly string _connStr;

            public PersonelRepository(IConfiguration configuration)
            {
                _connStr = configuration.GetConnectionString("PostgreSqlConnection")
                          ?? throw new InvalidOperationException("Connection string not found.");
            }

            public OperationResult<List<Personel>> GetAll()
            {
                try
                {
                    var list = new List<Personel>();

                    using var conn = new NpgsqlConnection(_connStr);
                    conn.Open();

                    const string sql = @"
                        SELECT id, ad_soyad, kullanici_adi, rol, aktif_mi, olusturma_tarihi
                        FROM personeller
                        ORDER BY id;
                    ";

                    using var cmd = new NpgsqlCommand(sql, conn);
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        list.Add(new Personel
                        {
                            Id = reader.GetInt32(0),
                            AdSoyad = reader.GetString(1),
                            KullaniciAdi = reader.GetString(2),
                            Rol = (PersonelRol)reader.GetInt32(3),
                            AktifMi = reader.GetBoolean(4),
                            OlusturmaTarihi = reader.GetDateTime(5)
                        });
                    }

                    if (list.Count == 0)
                        return OperationResult<List<Personel>>.Ok(list, "Henüz personel kaydı bulunmuyor.");

                    return OperationResult<List<Personel>>.Ok(list, "");
                }
                catch (PostgresException ex)
                {
                    return OperationResult<List<Personel>>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
                }
                catch (Exception)
                {
                    return OperationResult<List<Personel>>.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
                }
            }

            public OperationResult<List<Personel>> GetAllFiltered(bool? aktifMi, string? qAdSoyad, string? qKullaniciAdi)
            {
                try
                {
                    var list = new List<Personel>();

                    qAdSoyad = (qAdSoyad ?? "").Trim();
                    qKullaniciAdi = (qKullaniciAdi ?? "").Trim();

                    using var conn = new NpgsqlConnection(_connStr);
                    conn.Open();

                    // WHERE'i dinamik kur (parametreli - injection yok)
                    var where = new List<string>();
                    if (aktifMi.HasValue)
                        where.Add("aktif_mi = @aktif");

                    if (!string.IsNullOrWhiteSpace(qAdSoyad))
                        where.Add("ad_soyad ILIKE @qAd");

                    if (!string.IsNullOrWhiteSpace(qKullaniciAdi))
                        where.Add("kullanici_adi ILIKE @qUser");

                    var whereSql = where.Count > 0 ? ("WHERE " + string.Join(" AND ", where)) : "";

                    var sql = $@"
                SELECT id, ad_soyad, kullanici_adi, rol, aktif_mi, olusturma_tarihi
                FROM personeller
                {whereSql}
                ORDER BY id;
            ";

                    using var cmd = new NpgsqlCommand(sql, conn);

                    if (aktifMi.HasValue)
                        cmd.Parameters.AddWithValue("@aktif", aktifMi.Value);

                    if (!string.IsNullOrWhiteSpace(qAdSoyad))
                        cmd.Parameters.AddWithValue("@qAd", $"%{qAdSoyad}%");

                    if (!string.IsNullOrWhiteSpace(qKullaniciAdi))
                        cmd.Parameters.AddWithValue("@qUser", $"%{qKullaniciAdi}%");

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new Personel
                        {
                            Id = reader.GetInt32(0),
                            AdSoyad = reader.GetString(1),
                            KullaniciAdi = reader.GetString(2),
                            Rol = (PersonelRol)reader.GetInt32(3),
                            AktifMi = reader.GetBoolean(4),
                            OlusturmaTarihi = reader.GetDateTime(5)
                        });
                    }

                    if (list.Count == 0)
                        return OperationResult<List<Personel>>.Ok(list, "Filtreye uygun personel bulunamadı.");

                    return OperationResult<List<Personel>>.Ok(list, "");
                }
                catch (PostgresException ex)
                {
                    return OperationResult<List<Personel>>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
                }
                catch (Exception)
                {
                    return OperationResult<List<Personel>>.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
                }
            }

            public OperationResult<Personel> GetById(int id)
            {
                if (id <= 0)
                    return OperationResult<Personel>.Fail("Geçersiz personel id.");

                try
                {
                    using var conn = new NpgsqlConnection(_connStr);
                    conn.Open();

                    const string sql = @"
                        SELECT id, ad_soyad, kullanici_adi, rol, aktif_mi, olusturma_tarihi
                        FROM personeller
                        WHERE id = @id;
                    ";

                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@id", id);

                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                        return OperationResult<Personel>.Fail("Personel bulunamadı.");

                    var p = new Personel
                    {
                        Id = reader.GetInt32(0),
                        AdSoyad = reader.GetString(1),
                        KullaniciAdi = reader.GetString(2),
                        Rol = (PersonelRol)reader.GetInt32(3),
                        AktifMi = reader.GetBoolean(4),
                        OlusturmaTarihi = reader.GetDateTime(5)
                    };

                    return OperationResult<Personel>.Ok(p);
                }
                catch (PostgresException ex)
                {
                    return OperationResult<Personel>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
                }
                catch (Exception)
                {
                    return OperationResult<Personel>.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
                }
            }

            public OperationResult<int> Add(string adSoyad, string kullaniciAdi, string sifrePlain, PersonelRol rol)
            {
                adSoyad = (adSoyad ?? "").Trim();
                kullaniciAdi = (kullaniciAdi ?? "").Trim();

                if (string.IsNullOrWhiteSpace(adSoyad))
                    return OperationResult<int>.Fail("Ad Soyad boş bırakılamaz.");

                if (string.IsNullOrWhiteSpace(kullaniciAdi))
                    return OperationResult<int>.Fail("Kullanıcı adı boş bırakılamaz.");

                if (string.IsNullOrWhiteSpace(sifrePlain))
                    return OperationResult<int>.Fail("Şifre boş bırakılamaz.");

                if ((int)rol <= 0)
                    return OperationResult<int>.Fail("Geçerli bir rol seçiniz.");

                var (hash, salt) = PasswordHasher.CreateHash(sifrePlain);

                try
                {
                    using var conn = new NpgsqlConnection(_connStr);
                    conn.Open();

                    const string sql = @"
    INSERT INTO personeller (ad_soyad, kullanici_adi, sifre_hash, sifre_salt, rol)
    VALUES (@ad_soyad, @kullanici_adi, @sifre_hash, @sifre_salt, @rol)
    RETURNING id;
    ";

                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@ad_soyad", adSoyad);
                    cmd.Parameters.AddWithValue("@kullanici_adi", kullaniciAdi);
                    cmd.Parameters.AddWithValue("@sifre_hash", hash);
                    cmd.Parameters.AddWithValue("@sifre_salt", salt);
                    cmd.Parameters.Add("@rol", NpgsqlDbType.Integer).Value = (int)rol;

                    var newIdObj = cmd.ExecuteScalar();

                    if (newIdObj == null || newIdObj == DBNull.Value)
                        return OperationResult<int>.Fail("Personel eklenemedi. (Id alınamadı)");

                    var newId = Convert.ToInt32(newIdObj);

                    return OperationResult<int>.Ok(newId, $"Personel '{adSoyad}' eklendi.");
                }
                catch (PostgresException ex) when (ex.SqlState == "23505")
                {
                    return OperationResult<int>.Fail($"Kullanıcı adı '{kullaniciAdi}' zaten mevcut.");
                }
                catch (PostgresException ex)
                {
                    return OperationResult<int>.Fail($"Veritabanı işlemi sırasında bir hata oluştu. (Kod: {ex.SqlState})");
                }
                catch (Exception)
                {
                    return OperationResult<int>.Fail("Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.");
                }
            }


            public OperationResult Update(int id, string adSoyad, string kullaniciAdi, PersonelRol rol)
            {
                adSoyad = (adSoyad ?? "").Trim();
                kullaniciAdi = (kullaniciAdi ?? "").Trim();

                if (id <= 0)
                    return OperationResult.Fail("Geçersiz personel id.");

                if (string.IsNullOrWhiteSpace(adSoyad))
                    return OperationResult.Fail("Ad Soyad boş bırakılamaz.");

                if (string.IsNullOrWhiteSpace(kullaniciAdi))
                    return OperationResult.Fail("Kullanıcı adı boş bırakılamaz.");

                if ((int)rol <= 0)
                    return OperationResult.Fail("Geçerli bir rol seçiniz.");

                try
                {
                    using var conn = new NpgsqlConnection(_connStr);
                    conn.Open();

                    const string sql = @"
                        UPDATE personeller
                        SET ad_soyad = @ad_soyad,
                            kullanici_adi = @kullanici_adi,
                            rol = @rol
                        WHERE id = @id;
                    ";

                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@ad_soyad", adSoyad);
                    cmd.Parameters.AddWithValue("@kullanici_adi", kullaniciAdi);
                    cmd.Parameters.Add("@rol", NpgsqlDbType.Integer).Value = (int)rol;

                    var affected = cmd.ExecuteNonQuery();
                    if (affected == 0)
                        return OperationResult.Fail("Personel bulunamadı.");

                    return OperationResult.Ok("Personel güncellendi.");
                }
                catch (PostgresException ex) when (ex.SqlState == "23505")
                {
                    return OperationResult.Fail($"Kullanıcı adı '{kullaniciAdi}' zaten mevcut.");
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
                    return OperationResult<bool>.Fail("Geçersiz personel id.");

                try
                {
                    using var conn = new NpgsqlConnection(_connStr);
                    conn.Open();

                    const string sql = @"
                        UPDATE personeller
                        SET aktif_mi = NOT aktif_mi
                        WHERE id = @id
                        RETURNING ad_soyad, aktif_mi;
                    ";

                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@id", id);

                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                        return OperationResult<bool>.Fail("Personel bulunamadı.");

                    var adSoyad = reader.GetString(0);
                    var yeniDurum = reader.GetBoolean(1);

                    var msg = yeniDurum
                        ? $"Personel '{adSoyad}' aktif edildi."
                        : $"Personel '{adSoyad}' pasif edildi.";

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

            // Admin şifre reset: yeni hash+salt üretip DB’de günceller
            public OperationResult ResetPassword(int id, string newPlainPassword) 
            {
                if (id <= 0) return OperationResult.Fail("Geçersiz personel id."); 
                if (string.IsNullOrWhiteSpace(newPlainPassword)) return OperationResult.Fail("Yeni şifre boş olamaz."); 

                var (hash, salt) = PasswordHasher.CreateHash(newPlainPassword); 

                try
                {
                    using var conn = new NpgsqlConnection(_connStr);
                    conn.Open();

                    const string sql = @"
                UPDATE personeller
                SET sifre_hash = @hash,
                    sifre_salt = @salt
                WHERE id = @id;
            "; 

                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@hash", hash);
                    cmd.Parameters.AddWithValue("@salt", salt);

                    var affected = cmd.ExecuteNonQuery();
                    if (affected == 0)
                        return OperationResult.Fail("Personel bulunamadı."); 

                    return OperationResult.Ok("Şifre resetlendi."); 
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

        }
    }
